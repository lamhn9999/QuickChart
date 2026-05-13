// @ts-check
/**
 * @file Filter-card selection: clicking a filter card switches the right
 * panel into "filter" mode and shows its Apply-to chart picker. Clicking
 * the canvas (or another card) restores Canvas mode. Hovering a row in the
 * Apply-to list highlights the corresponding chart on the canvas with a
 * pulsing ring.
 *
 * The right panel is shared with the Canvas-options surface — we toggle
 * `data-mode` on `#right-panel` instead of swapping panels in/out, so the
 * 120ms CSS transition (defined in Workspace.cshtml) gives a single
 * "context changes" feel rather than a panel swap.
 */

import { getFilter, getSelectedFilterId, setSelectedFilterId, listFilters } from './state.js';
import { forEachGroup } from '../chart/group/state.js';
import { replayFiltersFor } from './convert.js';

const RIGHT_PANEL_ID = 'right-panel';

/**
 * Wire global click + the right-panel mode UI. Idempotent.
 */
export function initFilterSelection() {
  // Filter-card click → select. We attach to #doc-canvas because filter
  // cards live there and we don't want to argue with the editor's own
  // click-to-deselect on every block in the document.
  const canvas = document.getElementById('doc-canvas');
  if (canvas) {
    canvas.addEventListener('mousedown', (e) => {
      const target = /** @type {Element | null} */ (e.target);
      const card = /** @type {HTMLElement | null} */ (target?.closest('.canvas-card.filter-card-host'));
      if (card) {
        const gid = Number(card.dataset.groupId);
        if (Number.isFinite(gid)) selectFilter(gid);
        return;
      }
      // Click on the canvas itself but not on any filter card → deselect.
      if (target?.closest('.canvas-card')) return; // clicking another card type: leave selection
      deselectFilter();
    }, true);
  }

  // Outside-canvas clicks (table panel, header, etc.) also deselect — except
  // clicks within the right panel itself, since that's part of the filter UI.
  document.addEventListener('mousedown', (e) => {
    const t = /** @type {Element | null} */ (e.target);
    if (!t) return;
    if (t.closest(`#${RIGHT_PANEL_ID}`)) return;
    if (t.closest('.canvas-card.filter-card-host')) return;
    if (t.closest('#doc-canvas')) return;
    deselectFilter();
  });
}

/**
 * Mark the given filter card as selected. Adds the .filter-selected class
 * for the blue glow and switches the right panel into filter mode.
 * @param {number} groupId
 */
export function selectFilter(groupId) {
  const rec = getFilter(groupId);
  if (!rec) return;
  // Clear previous selection first
  document.querySelectorAll('.canvas-card.filter-card-host.filter-selected')
    .forEach((el) => el.classList.remove('filter-selected'));
  setSelectedFilterId(groupId);
  const card = document.querySelector(`#doc-canvas .canvas-card[data-group-id="${groupId}"]`);
  card?.classList.add('filter-selected');
  setRightPanelMode('filter', rec);
}

/** Clear the current selection and restore canvas mode on the right panel. */
export function deselectFilter() {
  if (getSelectedFilterId() == null) return;
  setSelectedFilterId(null);
  document.querySelectorAll('.canvas-card.filter-card-host.filter-selected')
    .forEach((el) => el.classList.remove('filter-selected'));
  // Also clear any leftover canvas highlight so the next selection starts fresh.
  document.querySelectorAll('.canvas-card--highlight')
    .forEach((el) => el.classList.remove('canvas-card--highlight'));
  setRightPanelMode('canvas', null);
}

/**
 * Switch the right panel between canvas options and filter options. Pure
 * DOM munging — the actual styling/transition is in Workspace.cshtml CSS.
 * @param {'canvas' | 'filter'} mode
 * @param {import('./state.js').FilterRecord | null} rec
 */
function setRightPanelMode(mode, rec) {
  const panel = document.getElementById(RIGHT_PANEL_ID);
  if (!panel) return;
  panel.dataset.mode = mode;
  if (mode !== 'filter' || !rec) return;
  const nameEl = panel.querySelector('[data-rp-filter-name]');
  if (nameEl) nameEl.textContent = rec.fieldName;
  renderApplyToList(panel, rec);
}

/**
 * Build/refresh the chart-picker list inside the filter-mode right panel.
 * @param {HTMLElement} panel
 * @param {import('./state.js').FilterRecord} rec
 */
export function renderApplyToList(panel, rec) {
  const list = /** @type {HTMLElement | null} */ (panel.querySelector('[data-rp-chart-list]'));
  if (!list) return;
  list.innerHTML = '';

  /** @type {{ groupId: number, label: string }[]} */
  const charts = [];
  forEachGroup((g, gid) => {
    if (gid === rec.groupId) return;            // never list the host card
    const card = g.card;
    if (!card) return;
    const panel = /** @type {HTMLElement | null} */ (card.querySelector('[data-field-indexes]'));
    if (!panel) return;                         // not a chart card (e.g. another filter)
    const wrapper = /** @type {HTMLElement | null} */ (card.querySelector('[data-chart-title]'));
    const label = wrapper?.dataset.chartTitle ?? `Chart ${gid}`;
    charts.push({ groupId: gid, label });
  });

  if (charts.length === 0) {
    list.innerHTML = `<div class="rp-empty">No charts on this canvas yet.</div>`;
    return;
  }

  charts.forEach(({ groupId, label }) => {
    const row = document.createElement('label');
    row.className = 'rp-chart-item';
    row.dataset.groupId = String(groupId);
    const checked = rec.appliedTo.has(groupId) ? 'checked' : '';
    row.innerHTML = `
      <input type="checkbox" data-action="filter-rp:toggle"
             data-group-id="${groupId}" ${checked} />
      <span class="rp-chart-name">${escapeHtml(label)}</span>
    `;
    list.appendChild(row);
  });
}

/**
 * Hover-highlight handler. Adds .canvas-card--highlight to the target
 * chart card so its pulse-ring CSS animation plays.
 * @param {Event} e
 * @param {HTMLElement} root
 */
export function onApplyToHover(e, root) {
  const t = /** @type {Element | null} */ (e.target);
  const row = /** @type {HTMLElement | null} */ (t?.closest('.rp-chart-item'));
  if (!row || !root.contains(row)) return;
  const gid = row.dataset.groupId;
  if (!gid) return;
  const card = document.querySelector(`#doc-canvas .canvas-card[data-group-id="${gid}"]`);
  card?.classList.add('canvas-card--highlight');
}

/** @param {Event} e @param {HTMLElement} root */
export function onApplyToUnhover(e, root) {
  const t = /** @type {Element | null} */ (e.target);
  const row = /** @type {HTMLElement | null} */ (t?.closest('.rp-chart-item'));
  if (!row || !root.contains(row)) return;
  const gid = row.dataset.groupId;
  if (!gid) return;
  const card = document.querySelector(`#doc-canvas .canvas-card[data-group-id="${gid}"]`);
  card?.classList.remove('canvas-card--highlight');
}

/**
 * Toggle a chart in/out of the active filter's appliedTo set. Fires the
 * inline-filter pipeline so the chart updates (or reverts) immediately.
 *
 * @param {Event} e
 * @param {HTMLInputElement} input
 */
export async function onApplyToToggle(e, input) {
  const sel = getSelectedFilterId();
  if (sel == null) return;
  const rec = getFilter(sel);
  if (!rec) return;
  const gid = Number(input.dataset.groupId);
  if (!Number.isFinite(gid)) return;
  if (input.checked) rec.appliedTo.add(gid);
  else rec.appliedTo.delete(gid);

  // Bump the footer chart-count on the host card.
  const card = document.querySelector(`#doc-canvas .canvas-card[data-group-id="${rec.groupId}"]`);
  const countEl = card?.querySelector('[data-filter-applied-count]');
  if (countEl) countEl.textContent = String(rec.appliedTo.size);

  await replayFiltersFor([gid]);
}

function escapeHtml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
