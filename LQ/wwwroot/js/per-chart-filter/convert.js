// @ts-check
/**
 * @file Convert a chart card → filter card, plus the hydrate path used by
 * canvas persistence. Both share the same registry and rendering machinery so
 * a card freshly converted in this session looks identical to one re-loaded
 * from app.Canvas.Document.
 *
 * The card's `groupId` is preserved across conversion so any reference (e.g.
 * persisted "applied to" sets pointing at this id) stays valid. The
 * underlying chart group's instances are torn down — a converted card no
 * longer renders Chart.js.
 */

import { getGroup, deleteGroup, forEachGroup, nextGroupId, setGroup } from '../chart/group/state.js';
import { chartInstances } from '../chart/state.js';
import { docCanvas } from '../chart/canvas-engine/canvas.js';
import { makeDraggable, nextZOrder } from '../chart/canvas-engine/draggable.js';
import { makeResizable } from '../chart/canvas-engine/resizable.js';
import { fetchFieldDistinct, refreshChartWithFilters, buildInlineFilter } from './api.js';
import { renderButtonsBody, renderSearchBody } from './render.js';
import { setFilter, getFilter, deleteFilter, fieldTypeFromTypeName, listFilters } from './state.js';
import { buildFilterToolbar } from './toolbar.js';

/** Resize floor — small enough for a few pills, large enough that pills don't truncate. */
const FILTER_MIN_W = 240;
const FILTER_MIN_H = 140;
/** Default height after converting from a chart card (chart cards are taller). */
const FILTER_DEFAULT_H = 200;

/** Threshold below which numeric fields render as buttons instead of search. */
const NUMERIC_BUTTONS_LIMIT = 999999;

/**
 * Take an existing chart card and replace its body with a filter UI tailored
 * to the card's single field. Card position, size and groupId are preserved.
 *
 * @param {HTMLElement} card  the .canvas-card element being converted
 */
export async function convertChartToFilter(card) {
  const groupId = Number(card.dataset.groupId);
  if (!Number.isFinite(groupId)) return;
  const group = getGroup(groupId);
  if (!group) return;

  const panel = /** @type {HTMLElement | null} */ (card.querySelector('[data-field-indexes]'));
  if (!panel) return;
  /** @type {number[]} */
  let fieldIndexes;
  try {
    fieldIndexes = JSON.parse(panel.dataset.fieldIndexes || '[]');
  } catch {
    return;
  }
  if (fieldIndexes.length !== 1) return; // gated upstream — defensive only
  const fieldIndex = fieldIndexes[0];

  // The host chart may itself be a copied chart (panel carries a foreign
  // csv-file-token). The filter inherits that token so its fieldIndex stays
  // bound to the same schema. Null means "this canvas's owner file".
  const csvFileToken = panel.dataset.csvFileToken || null;

  const distinct = await fetchFieldDistinct(fieldIndex, NUMERIC_BUTTONS_LIMIT + 1, csvFileToken);
  if (!distinct) return;

  // Tear down the chart group: destroy Chart.js instances + observer, free
  // the registry slot. We keep the same DOM card so position/size persist.
  group.ro?.disconnect();
  group.instances.forEach((/** @type {any} */ c) => {
    c.destroy();
    const i = chartInstances.indexOf(c);
    if (i !== -1) chartInstances.splice(i, 1);
  });
  deleteGroup(groupId);

  // Build the filter record + DOM body
  const variant = decideVariant(distinct);
  const fieldName = distinct.fieldName ?? `Field ${fieldIndex}`;
  const fieldType = fieldTypeFromTypeName(distinct.typeName);
  const appliedTo = computeInitialAppliedTo(fieldIndex, csvFileToken, groupId);
  const rec = {
    groupId,
    fieldIndex,
    csvFileToken,
    fieldName,
    fieldType,
    typeName: distinct.typeName ?? null,
    variant,
    availableValues: variant === 'buttons' ? distinct.values : [],
    selectedValues: new Set(),
    appliedTo,
  };
  setFilter(groupId, rec);

  // Swap the card's class + replace body in place. innerHTML='' wipes the
  // chart card's panels, external left toolbar, and 8 resize handles (all
  // direct children of the card), so we get a clean canvas-card to rebuild on.
  card.classList.add('filter-card-host');
  card.innerHTML = '';
  // Chart cards default to 361px tall; filter content needs ~140-200px.
  // Only normalise if we're shrinking — leave a deliberately tall card alone.
  const currentH = parseFloat(card.style.height || '');
  if (!Number.isFinite(currentH) || currentH > FILTER_DEFAULT_H + 40) {
    card.style.height = `${FILTER_DEFAULT_H}px`;
  }
  const body = variant === 'buttons' ? renderButtonsBody(rec) : renderSearchBody(rec);
  card.appendChild(body);

  // Filter cards don't need a ResizeObserver (no Chart.js inside) — keep the
  // registry record around so removeChartGroup-style helpers still find a
  // parking space if anything tries to look it up.
  setGroup(groupId, { card, instances: [], ro: null });

  // Re-wire drag (the new toolbar IS the .drag-handle) + 8-direction resize +
  // bring-to-front. makeDraggable is idempotent on the bring-to-front
  // listener (gated by card.dataset.draggable) and per-handle (gated by
  // handle.dataset.dragWired) so the chart card's prior wiring doesn't
  // double up.
  buildFilterToolbar(card, groupId);
  makeDraggable(card);
  makeResizable(card, [], { minW: FILTER_MIN_W, minH: FILTER_MIN_H });

  // No initial fetch: with empty selectedValues the spec is "no-op", every
  // applied chart already shows unfiltered data from when it was created.
}

/**
 * Hydrate path. Used by canvas-persist/kinds.js when reloading a saved
 * canvas. Builds the card from scratch (no prior chart state to tear down)
 * and re-applies any saved selection / appliedTo wiring.
 *
 * @param {{
 *   fieldIndex: number,
 *   csvFileToken?: string | null,
 *   x?: number, y?: number, width?: number, height?: number,
 *   selectedValues?: string[],
 *   appliedTo?: number[],
 * }} spec
 * @returns {Promise<HTMLElement | null>}
 */
export async function recreateFilterCard(spec) {
  if (typeof spec.fieldIndex !== 'number') return null;
  // Null/undefined csvFileToken means "native to this canvas" — the distinct
  // endpoint falls back to the session's current file in that case.
  const csvFileToken = spec.csvFileToken ?? null;
  const distinct = await fetchFieldDistinct(spec.fieldIndex, NUMERIC_BUTTONS_LIMIT + 1, csvFileToken);
  if (!distinct) return null;

  const groupId = nextGroupId();
  const variant = decideVariant(distinct);
  const fieldName = distinct.fieldName ?? `Field ${spec.fieldIndex}`;
  const fieldType = fieldTypeFromTypeName(distinct.typeName);

  // Note on appliedTo persistence: saved group ids belong to the previous
  // session's chart registry — the counter resets every page load, so they
  // don't map back. We fall back to the same "all charts on this field"
  // default that fresh conversions use, scoped to charts that share the
  // filter's source file. Manual unticks intentionally don't survive a reload
  // in v1; revisit if/when a stable per-chart identifier lands (e.g. a UUID
  // baked into the chart kind).
  const rec = {
    groupId,
    fieldIndex: spec.fieldIndex,
    csvFileToken,
    fieldName,
    fieldType,
    typeName: distinct.typeName ?? null,
    variant,
    availableValues: variant === 'buttons' ? distinct.values : [],
    selectedValues: new Set(),
    appliedTo: new Set((spec.appliedTo ?? []).filter((id) => Number.isFinite(id))),
  };
  setFilter(groupId, rec);

  // Build the absolutely-positioned host card. Both width AND height are
  // always set (height was previously optional) so the 8-direction resize
  // handles have a concrete starting box to anchor against.
  const card = document.createElement('div');
  card.classList.add('canvas-card', 'filter-card-host');
  card.dataset.groupId = String(groupId);
  card.style.cssText =
    `position:absolute;` +
    `left:${Math.max(0, spec.x ?? 0)}px;top:${Math.max(0, spec.y ?? 0)}px;` +
    `width:${spec.width ?? 320}px;` +
    `height:${spec.height ?? FILTER_DEFAULT_H}px;` +
    `z-index:${nextZOrder()}`;

  const body = variant === 'buttons' ? renderButtonsBody(rec) : renderSearchBody(rec);
  card.appendChild(body);

  docCanvas()?.appendChild(card);

  setGroup(groupId, { card, instances: [], ro: null });

  // Toolbar (drag handle + remove + copy-to-file) must exist before
  // makeDraggable runs — the toolbar's outer element IS the .drag-handle.
  buildFilterToolbar(card, groupId);
  makeDraggable(card);
  makeResizable(card, [], { minW: FILTER_MIN_W, minH: FILTER_MIN_H });

  return card;
}

/**
 * Pre-compute which chart cards on the canvas should start out wired to
 * this new filter. Per the approved plan: every chart whose fieldIndexes
 * contain the same field, excluding the host card itself.
 *
 * Same-file scoping: the filter's `csvFileToken` (null == native to canvas)
 * must match the chart panel's data-csv-file-token (also null == native).
 * Cross-file matches are skipped — a fieldIndex from File A can't mean
 * anything against File B's schema, and silently filtering the wrong column
 * is exactly the bug this token plumbs around.
 *
 * @param {number} fieldIndex
 * @param {string | null} csvFileToken  source token to match against; null == native
 * @param {number} excludeGroupId
 */
function computeInitialAppliedTo(fieldIndex, csvFileToken, excludeGroupId) {
  /** @type {Set<number>} */
  const out = new Set();
  forEachGroup((rec, gid) => {
    if (gid === excludeGroupId) return;
    const card = rec.card;
    const panel = /** @type {HTMLElement | null} */ (card?.querySelector('[data-field-indexes]'));
    if (!panel) return;
    // Normalise empty string → null so a missing dataset attribute compares
    // equal to a filter that's bound to the canvas owner.
    const panelToken = panel.dataset.csvFileToken || null;
    if (panelToken !== csvFileToken) return;
    try {
      const idxs = JSON.parse(panel.dataset.fieldIndexes || '[]');
      if (Array.isArray(idxs) && idxs.includes(fieldIndex)) out.add(gid);
    } catch { /* malformed dataset — skip */ }
  });
  return out;
}

/**
 * Decide the filter UI variant from the distinct-endpoint response. Mirrors
 * the rules in the approved plan §3:
 *   - categorical                            → buttons
 *   - numeric AND distinctCount <= limit     → buttons
 *   - everything else                        → search
 *
 * @param {import('./api.js').DistinctResponse} distinct
 * @returns {import('./state.js').FilterVariant}
 */
function decideVariant(distinct) {
  if (distinct.isCategorical) return 'buttons';
  if (distinct.distinctCount <= NUMERIC_BUTTONS_LIMIT && distinct.values.length > 0) return 'buttons';
  return 'search';
}

/**
 * For each chart in `targets`, post the union of every filter card whose
 * appliedTo set includes that chart. This is the "two filter cards on the
 * same field" path called out in the plan: per-chart, ALL applicable
 * inline filters are sent in one body.
 *
 * Cross-file safety: filters whose csvFileToken disagrees with the target
 * chart's panel token are dropped here, not in the dispatch above, because
 * the answer is per-chart (the same filter may match chart A and miss chart
 * B in the same call). Without this guard, a copied filter could land on a
 * chart from a different file and silently filter the wrong column.
 *
 * Exported via the events module — kept here because both convert (hydrate)
 * and events (selection change) need it.
 *
 * @param {Iterable<number>} targets  chart group ids to refresh
 */
export async function replayFiltersFor(targets) {
  const all = listFilters();
  await Promise.allSettled(
    [...new Set(targets)].map((groupId) => {
      const chartGroup = getGroup(groupId);
      const panel = /** @type {HTMLElement | null} */ (
        chartGroup?.card?.querySelector('[data-field-indexes]')
      );
      const chartToken = (panel?.dataset.csvFileToken) || null;
      const inline = all
        .filter((f) => f.appliedTo.has(groupId))
        .filter((f) => (f.csvFileToken ?? null) === chartToken)
        .map(buildInlineFilter)
        .filter((f) => f !== null);
      return refreshChartWithFilters(groupId, inline);
    })
  );
}

/**
 * Tear down a filter card cleanly. Mirrors removeChartGroup() for chart
 * cards: destroy registry entry, remove DOM, and refresh any charts that
 * were affected so they revert to whatever the remaining filters say.
 *
 * @param {number} groupId
 */
export async function removeFilterCard(groupId) {
  const rec = getFilter(groupId);
  if (!rec) return;
  const wasApplied = new Set(rec.appliedTo);
  deleteFilter(groupId);
  deleteGroup(groupId);
  // Find the host card by groupId and remove it.
  const card = document.querySelector(`#doc-canvas .canvas-card[data-group-id="${groupId}"]`);
  card?.remove();
  await replayFiltersFor(wasApplied);
}
