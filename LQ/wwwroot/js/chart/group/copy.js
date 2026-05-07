// @ts-check
/**
 * @file POST a chart spec to /workspace/canvas/append-chart in another file's canvas.
 *
 * The "Copy chart to another file" submenu (built in builder.js) opens this. The source
 * token is the chart's foreign `csvFileToken` if it has one (already authored from another
 * file); otherwise it's the active workspace's owner token. We always send a source token
 * so the appended block is self-describing in the target canvas.
 */

import { ownerFileToken } from './owner.js';

/**
 * @param {HTMLElement} panelDiv          the chart's panel div (carries dataset.fieldIndexes / .csvFileToken)
 * @param {string | null} panelCsvFileToken  the chart's foreign token (passed to builder)
 * @param {string} targetFileToken          the file we're copying into
 */
export async function copyChartToFile(panelDiv, panelCsvFileToken, targetFileToken) {
  /** @type {number[]} */
  let fieldIndexes;
  try {
    fieldIndexes = JSON.parse(panelDiv.dataset.fieldIndexes || '[]');
  } catch {
    fieldIndexes = [];
  }
  if (!Array.isArray(fieldIndexes) || fieldIndexes.length === 0) return;

  const sourceToken = panelCsvFileToken || panelDiv.dataset.csvFileToken || ownerFileToken();
  if (!sourceToken) return;

  const card = /** @type {HTMLElement | null} */ (panelDiv.closest('.canvas-card'));
  const width = card?.offsetWidth || 480;
  const height = card?.offsetHeight || 361;

  try {
    const r = await fetch('/workspace/canvas/append-chart', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        targetFileToken,
        chartSpec: { csvFileToken: sourceToken, fieldIndexes, width, height },
      }),
    });
    if (!r.ok) console.error('[copyChartToFile] failed:', r.status);
  } catch (err) {
    console.error('[copyChartToFile] error:', err);
  }
}
