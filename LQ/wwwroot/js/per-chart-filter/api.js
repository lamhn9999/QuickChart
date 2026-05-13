// @ts-check
/**
 * @file Server I/O for per-chart filters.
 *
 * One trip to GET /workspace/field/{i}/distinct on conversion (decides
 * variant), and one POST /workspace/chart per affected chart whenever the
 * selection changes. Patches the live Chart.js instance in place using the
 * exact same helper the table-wide refresh uses, so the visual contract
 * (no flash, dataset count sync, box-plot skip) stays identical.
 */

import { getGroup } from '../chart/group/state.js';
import { applySampleToInstance } from '../chart-refresh/index.js';

/**
 * @typedef {Object} DistinctResponse
 * @property {number}            distinctCount
 * @property {(string|null)[]}   values
 * @property {boolean}           isCategorical
 * @property {string | null}     typeName
 * @property {string | null}     fieldName
 */

/**
 * Fetch distinct-value metadata for a single field. The endpoint returns the
 * value list iff the count fits the limit; the caller uses both pieces to
 * decide between the buttons and search variants.
 *
 * When `csvFileToken` is provided, the server resolves the field against
 * THAT file's schema instead of the session's current file — required when
 * the filter was copied from another file (its fieldIndex is meaningful only
 * against its source schema).
 *
 * @param {number} fieldIndex
 * @param {number} [limit=8]
 * @param {string | null} [csvFileToken=null]
 * @returns {Promise<DistinctResponse | null>}
 */
export async function fetchFieldDistinct(fieldIndex, limit = 8, csvFileToken = null) {
  try {
    const qs = csvFileToken
      ? `?limit=${limit}&csvFileToken=${encodeURIComponent(csvFileToken)}`
      : `?limit=${limit}`;
    const r = await fetch(`/workspace/field/${fieldIndex}/distinct${qs}`);
    if (!r.ok) return null;
    return await r.json();
  } catch {
    return null;
  }
}

/**
 * Build the inline ValueFilterDTO sent to the per-chart endpoint. The shape
 * mirrors the same DTO the table-wide filter sends — only the transport
 * differs (request body here vs. session there). For numeric fields we
 * populate the typed array (filteringValuesInt / filteringValuesDouble) so
 * the server's value-coercion path matches what FilterTable does.
 *
 * @param {{fieldIndex: number, fieldType: number, selectedValues: Set<string>}} rec
 * @returns {object | null}  null when no values selected (caller should send an empty filter list)
 */
export function buildInlineFilter(rec) {
  if (rec.selectedValues.size === 0) return null;
  const values = [...rec.selectedValues];
  const base = {
    fieldIndex: rec.fieldIndex,
    fieldType: rec.fieldType,
    isNot: false,
    isPartialMatch: false,
  };
  switch (rec.fieldType) {
    case 1: // Int
      return { ...base, filteringValuesInt: values.map(Number).filter((n) => !Number.isNaN(n)) };
    case 2: // Double
      return { ...base, filteringValuesDouble: values.map(Number).filter((n) => !Number.isNaN(n)) };
    default:
      // String, DateTime, Bool, ListString — all transit as raw strings;
      // the server already coerces in DetectChartWithFiltersAsync.
      return { ...base, filteringValues: values };
  }
}

/**
 * Refresh a single chart group with an inline filter list. POSTs to
 * /workspace/chart and patches the live Chart.js instances in place. Mirrors
 * `refreshGroup` in chart-refresh/index.js — the differences are the endpoint
 * URL and the body carrying inlineFilters.
 *
 * @param {number} groupId
 * @param {object[]} inlineFilters  array of ValueFilterDTO-shaped objects
 */
export async function refreshChartWithFilters(groupId, inlineFilters) {
  const rec = getGroup(groupId);
  if (!rec) return;
  const { card, instances } = rec;
  if (!card || !instances || instances.length === 0) return;

  const fielEl = /** @type {HTMLElement | null} */ (card.querySelector('[data-field-indexes]'));
  if (!fielEl) return;
  /** @type {number[]} */
  let fieldIndexes;
  try {
    fieldIndexes = JSON.parse(fielEl.dataset.fieldIndexes || '[]');
  } catch {
    return;
  }
  if (fieldIndexes.length === 0) return;

  /** @type {any[] | null} */
  let samples = null;
  try {
    const response = await fetch('/workspace/chart', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        csvFileToken: fielEl.dataset.csvFileToken || null,
        fieldIndexes,
        inlineFilters,
      }),
    });
    if (!response.ok) return;
    samples = await response.json();
  } catch (e) {
    if (/** @type {any} */ (e)?.name !== 'AbortError') {
      console.error('[refreshChartWithFilters] fetch error:', e);
    }
    return;
  }
  if (!Array.isArray(samples) || samples.length === 0) return;

  // Same panel-position matching as chart-refresh/index.js — identical contract.
  /** @type {Record<number, any[]>} */
  const samplesByPanel = {};
  samples.forEach((s) => {
    const p = s.panelIndex;
    if (p == null) return;
    (samplesByPanel[p] = samplesByPanel[p] || []).push(s);
  });

  instances.forEach((/** @type {any} */ instance) => {
    const wrapper =
      instance.canvas instanceof HTMLElement ? instance.canvas.closest('[data-chart-type]') : null;
    if (!wrapper) return;
    const parent = wrapper.parentElement;
    if (!parent) return;
    const sIdx = Array.from(parent.children).indexOf(wrapper);
    if (sIdx === -1) return;
    const panelId = /** @type {HTMLElement} */ (parent).id || '';
    const panelIdx = parseInt(panelId.replace(/^panel(\d+).*$/, '$1'), 10);
    if (isNaN(panelIdx)) return;
    const panelSamples = samplesByPanel[panelIdx];
    if (!panelSamples || panelSamples.length <= sIdx) return;
    applySampleToInstance(instance, panelSamples[sIdx]);
  });
}
