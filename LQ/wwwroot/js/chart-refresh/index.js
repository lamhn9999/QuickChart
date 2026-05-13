// @ts-check
/**
 * @file Filter-driven chart data refresh.
 *
 * When filters are applied, this module re-fetches chart data from the AutoChart
 * endpoint (which reads session filters server-side) and updates existing Chart.js
 * instances IN PLACE — preserving their config, position, size, and styling.
 *
 * This is the core of the "immediate visual feedback" requirement: charts update
 * without any destroy/recreate flash. The only changes are `.data.labels` and
 * `.data.datasets[].data`, followed by `chart.update('none')`.
 *
 * Imported by: filter/reactor.js
 * Reads from: chart/group/state.js (read-only: forEachGroup, getGroup)
 * Reads from: chart/data-utils.js (buildHeatmapData — pure utility, no rendering)
 */

import { forEachGroup, getGroup } from '../chart/group/state.js';
import { buildHeatmapData } from '../chart/data-utils.js';

/**
 * Fetch updated data for every native chart group and apply it in-place.
 * Called after every filter execution.
 */
export async function refreshAllCharts() {
  /** @type {{ groupId: number, fieldIndexes: number[] }[]} */
  const groups = [];
  forEachGroup((record, groupId) => {
    const card = record.card;
    if (!card) return;
    const fielEl = /** @type {HTMLElement | null} */ (card.querySelector('[data-field-indexes]'));
    if (!fielEl) return;
    const fieldIndexes = JSON.parse(fielEl.dataset.fieldIndexes || '[]');
    if (!Array.isArray(fieldIndexes) || fieldIndexes.length === 0) return;
    if (fielEl.dataset.csvFileToken) return; // foreign-file charts: skip
    groups.push({ groupId, fieldIndexes });
  });
  if (groups.length === 0) return;

  // Fire all group refreshes in parallel
  await Promise.allSettled(groups.map((g) => refreshGroup(g.groupId, g.fieldIndexes)));
}

/**
 * Refresh one chart group.
 * @param {number} groupId
 * @param {number[]} fieldIndexes
 */
async function refreshGroup(groupId, fieldIndexes) {
  const rec = getGroup(groupId);
  if (!rec) return;
  const { card, instances } = rec;
  if (!card || !instances || instances.length === 0) return;

  /** @type {any[] | null} */
  let samples = null;
  try {
    const response = await fetch('/workspace', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fieldIndexes }),
    });
    if (!response.ok) return;
    samples = await response.json();
  } catch (e) {
    if (/** @type {any} */ (e)?.name !== 'AbortError')
      console.error('[refreshGroup] fetch error:', e);
    return;
  }
  if (!Array.isArray(samples) || samples.length === 0) return;

  // Group response samples by panelIndex for positional lookup
  /** @type {Record<number, any[]>} */
  const samplesByPanel = {};
  samples.forEach((s) => {
    const p = s.panelIndex;
    if (p == null) return;
    (samplesByPanel[p] = samplesByPanel[p] || []).push(s);
  });

  // Match each instance to a response sample by POSITION within its panel.
  // We cannot match by chartType because filtering can change which chart types
  // AutoChart recommends (e.g. SideBar→Bar when cardinality drops below 20).
  // Instead, wrappers are created in the same order as response samples (sIdx),
  // so position is stable across filter calls.
  instances.forEach((instance) => {
    const wrapper =
      instance.canvas instanceof HTMLElement
        ? instance.canvas.closest('[data-chart-type]')
        : null;
    if (!wrapper) return;

    // Wrapper is a direct child of panelDivCopy — its index is sIdx
    const parent = wrapper.parentElement;
    if (!parent) return;
    const sIdx = Array.from(parent.children).indexOf(wrapper);
    if (sIdx === -1) return;

    // Extract panelIndex from panelDivCopy id, e.g. "panel0_g1" → 0
    const panelId = /** @type {HTMLElement} */ (parent).id || '';
    const panelIdx = parseInt(panelId.replace(/^panel(\d+).*$/, '$1'), 10);
    if (isNaN(panelIdx)) return;

    const panelSamples = samplesByPanel[panelIdx];
    if (!panelSamples || panelSamples.length <= sIdx) return;

    const match = panelSamples[sIdx];
    applySampleToInstance(instance, match);

    // Update title on the wrapper
    if (match.title) wrapper.dataset.chartTitle = match.title;
  });
}

/**
 * Apply new data from a backend sample to a live Chart.js instance.
 * Preserves all config (type, scales, plugins, colors) — only swaps data arrays.
 *
 * Exported because the per-chart-filter module (per-chart-filter/api.js) needs
 * the exact same patching contract — dataset-count sync + box-plot skip — when
 * it patches a chart from the inline-filter endpoint response. Hoisting it
 * keeps that contract in one place rather than copy-pasted across two callers.
 *
 * @param {any} chart
 * @param {{ chartType: number, title?: string, x: any[], y: { label: string, data: any[] }[], z: any[] }} sample
 * note: sample.z is always an array in the server response (may be empty)
 */
export function applySampleToInstance(chart, sample) {
  const typeName = chart.config?.type;

  // Box plot check: if datasets have more than 4 entries and dataset[2] is 'Median',
  // the chart is a box plot rendered by lqBoxPlot plugin.  The raw data arrays are
  // updated but the plugin's boxData closure is stale — skip for now so we don't
  // show misleading whiskers.  Callers should use replaceChartGroup for these.
  const isBoxPlot =
    chart.data.datasets.length >= 5 &&
    chart.data.datasets[2]?.label === 'Median';
  if (isBoxPlot) {
    // Update raw datasets for underlying points (purely cosmetic — plugin stays stale).
    // Fall back to replaceChartGroup for correct rendering.
    sample.y.forEach((ds, i) => {
      if (chart.data.datasets[i]) chart.data.datasets[i].data = ds.data;
    });
    chart.update('none');
    return;
  }

  // --- Labels (matrix stores labels differently) ---
  if (typeName !== 'matrix') {
    chart.data.labels = sample.x;
  }

  // --- Synchronise dataset count — filtering may add or remove categories ---
  const expectedPrimary = sample.y.length;
  const expectedTotal = expectedPrimary + sample.z.length;

  // Remove excess datasets (e.g. a player filtered-out entirely by the filter)
  while (chart.data.datasets.length > expectedTotal) {
    chart.data.datasets.pop();
  }
  // Add missing datasets (e.g. a player that was previously filtered-out)
  while (chart.data.datasets.length < expectedTotal) {
    // Use first dataset as template to preserve chart-type-specific properties
    // (fill, stack, tension for area; type for mixed; borderRadius for bar; etc.)
    const template = chart.data.datasets[0] || {};
    const newDs = { data: [], label: '' };
    // Skip data, label, and color properties (borderColor, backgroundColor, borderWidth)
    const skipKeys = ['data', 'label', 'borderColor', 'backgroundColor', 'borderWidth'];
    Object.keys(template).forEach((key) => {
      if (!skipKeys.includes(key)) {
        newDs[key] = template[key];
      }
    });
    chart.data.datasets.push(newDs);
    _assignDatasetColor(chart, chart.data.datasets.length - 1, typeName);
  }

  // --- Primary datasets ---
  if (typeName === 'matrix') {
    const hd = buildHeatmapData(sample.x, sample.y);
    chart.data.datasets[0].data = hd;
  } else {
    sample.y.forEach((ds, i) => {
      if (!chart.data.datasets[i]) return;
      chart.data.datasets[i].data = ds.data;
      chart.data.datasets[i].label = ds.label;
    });
  }

  // --- Secondary axis (z) ---
  if (sample.z.length > 0) {
    const offset = expectedPrimary;
    sample.z.forEach((ds, i) => {
      const idx = offset + i;
      if (chart.data.datasets[idx]) {
        chart.data.datasets[idx].data = ds.data;
      }
    });
  }

  chart.update('none');
}

/**
 * Palette matching Chart.js v4.x internal colors plugin palette.
 * Used to colour freshly-added datasets so they don't render as grey.
 */
const _PALETTE = [
  'rgb(54, 162, 235)',
  'rgb(255, 99, 132)',
  'rgb(255, 205, 86)',
  'rgb(75, 192, 192)',
  'rgb(153, 102, 255)',
  'rgb(255, 159, 64)',
  'rgb(201, 203, 207)',
  'rgb(231, 233, 237)',
  'rgb(138, 182, 74)',
  'rgb(247, 141, 167)',
];

/** @param {string} color @param {number} alpha */
function _rgba(color, alpha) {
  return color.replace('rgb', 'rgba').replace(')', `, ${alpha})`);
}

/**
 * Assign borderColor / backgroundColor to a dataset that was dynamically added.
 * Mirrors the logic Chart.js's built-in colors plugin uses.
 * Only sets properties that the first dataset already has (to avoid adding properties
 * that shouldn't persist when filtering is removed).
 * @param {any} chart
 * @param {number} dsIndex
 * @param {string} typeName  chart.config.type
 */
function _assignDatasetColor(chart, dsIndex, typeName) {
  const ds = chart.data.datasets[dsIndex];
  const template = chart.data.datasets[0];
  if (!ds || !template) return;
  const color = _PALETTE[dsIndex % _PALETTE.length];

  if (typeName === 'line' || typeName === 'radar') {
    if ('borderColor' in template) ds.borderColor = color;
    if ('backgroundColor' in template) ds.backgroundColor = _rgba(color, 0.5);
  } else if (typeName === 'bar') {
    if ('backgroundColor' in template) ds.backgroundColor = color;
    if ('borderColor' in template) ds.borderColor = color;
  } else if (typeName === 'pie' || typeName === 'doughnut' || typeName === 'polarArea') {
    if ('backgroundColor' in template) ds.backgroundColor = color;
  } else if (typeName === 'scatter' || typeName === 'bubble') {
    ds.backgroundColor = _rgba(color, 0.5);
    ds.borderColor = color;
    ds.borderWidth = 1;
  }
  // matrix: dataset has its own colour logic from buildMatrixDataset — skip
}


