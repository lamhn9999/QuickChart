// @ts-check
/**
 * @file CSV picker — switches the active CSV file without a full page reload.
 *
 * On picker change: fetches the full workspace page from the existing GET
 * endpoint, extracts the table-panel DOM and the owner-file-token, then swaps
 * them in-place.  The canvas document and its blocks (charts, filters, text)
 * are left untouched — they all carry explicit `csvFileToken` so they remain
 * self-describing regardless of which file is active in the table panel.
 */

import { initSparklines } from '../chart/sparklines.js';
import { wireColumnHighlight } from './table.js';

/**
 * Swap `#tablePanel`'s inner HTML and the owner-file-token with fresh content
 * from the server response, then re-init sparklines + column highlighting.
 * @param {string} html — full-page HTML from the server
 * @returns {void}
 */
function swapTablePanel(html) {
  const parser = new DOMParser();
  const doc = parser.parseFromString(html, 'text/html');

  const oldPanel = document.getElementById('tablePanel');
  const newPanel = doc.getElementById('tablePanel');
  if (oldPanel && newPanel) {
    oldPanel.innerHTML = newPanel.innerHTML;
  }

  const oldToken = document.getElementById('canvas-owner-file-token');
  const newToken = doc.getElementById('canvas-owner-file-token');
  if (oldToken && newToken) {
    oldToken.textContent = newToken.textContent;
  }

  initSparklines();

  // Trigger the server-side sparkline renderer (pie charts, area lines,
  // histograms) that the inline script in Workspace.cshtml provides.
  const loadAll = /** @type {any} */ (window).loadAllSparklines;
  if (typeof loadAll === 'function') loadAll();

  const table = /** @type {HTMLTableElement | null} */ (
    document.getElementById('dataTable')
  );
  if (table) wireColumnHighlight(table);
}

/**
 * @param {Event} e
 */
async function onCsvPickerChange(e) {
  const select = /** @type {HTMLSelectElement} */ (e.target);
  const canvasToken = select.dataset.canvasToken;
  const fileToken = select.value;
  if (!canvasToken || !fileToken) return;

  select.disabled = true;

  try {
    const response = await fetch(`/workspace/${canvasToken}?file=${fileToken}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const html = await response.text();
    swapTablePanel(html);
  } catch (err) {
    console.error('[csvPicker] failed to switch file:', err);
  } finally {
    select.disabled = false;
  }
}

/** Register the CSV-picker change handler (delegated, survives panel swaps). */
export function initCsvPicker() {
  document.addEventListener('change', (e) => {
    const target = /** @type {Element} */ (e.target);
    if (target.id !== 'csvPicker') return;
    onCsvPickerChange(e);
  });
}
