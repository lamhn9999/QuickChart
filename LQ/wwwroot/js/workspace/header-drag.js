// @ts-check
/**
 * @file Column header click & drag — pending-selection set + drag ghost.
 *
 * - `fieldClicked(idx)` toggles `idx` in `pendingIndexes` and updates header highlight + badge.
 * - `headerDragStart` builds a custom drag ghost and serialises the indexes to drop. After
 *   the drag image is captured it clears the pending set so the highlights vanish.
 * - `headerDragEnd` removes the per-row dragging class.
 *
 * `_dragStarted` is a module-private flag that prevents the synthetic `click` that follows
 * `dragstart` from immediately toggling the header back off.
 */

import { pendingIndexes } from '../core/state.js';

let _dragStarted = false;

/** Refresh `.header-pending` class on every column header. */
export function updateHeaderStates() {
  document.querySelectorAll('th[data-index]').forEach((th) => {
    const idx = parseInt(/** @type {HTMLElement} */ (th).dataset.index || '');
    th.classList.toggle('header-pending', pendingIndexes.has(idx));
  });
}

/** Refresh the "N selected" badge in the toolbar. */
export function updatePendingBadge() {
  const badge = document.getElementById('pendingBadge');
  const txt = document.getElementById('pendingBadgeText');
  if (!badge || !txt) return;
  const n = pendingIndexes.size;
  if (n > 0) {
    txt.textContent = `${n} selected`;
    badge.classList.remove('hidden');
  } else {
    badge.classList.add('hidden');
  }
}

/**
 * Clear the pending set + repaint header states & badge.
 * Used by drop-zones after the payload is captured / a chart is created.
 */
export function clearPending() {
  pendingIndexes.clear();
  updateHeaderStates();
  updatePendingBadge();
}

/**
 * Toggle a column's pending state. No-op when triggered immediately after a dragstart.
 * @param {number} fieldindex
 */
export function fieldClicked(fieldindex) {
  if (_dragStarted) return;
  if (pendingIndexes.has(fieldindex)) pendingIndexes.delete(fieldindex);
  else pendingIndexes.add(fieldindex);
  updateHeaderStates();
  updatePendingBadge();
}

/**
 * Build the drag ghost + dataTransfer payload from `pendingIndexes` ∪ `{fieldindex}`.
 *
 * Note: `th` is passed explicitly because Phase 8 routes drag events through delegated
 * dispatch on `document` — `event.currentTarget` is the document, NOT the column header.
 * Callers (workspace/index.js action registry) supply the matched element.
 *
 * @param {DragEvent} event
 * @param {number} fieldindex
 * @param {HTMLElement} th  the dragged column header
 */
export function headerDragStart(event, fieldindex, th) {
  _dragStarted = true;
  event.stopPropagation();
  if (!event.dataTransfer || !th) return;

  // All indexes to send: pending selection + the actively dragged header
  const toDrop = [...new Set([...pendingIndexes, fieldindex])];
  event.dataTransfer.setData('application/json', JSON.stringify(toDrop));
  event.dataTransfer.effectAllowed = 'copy';

  // Custom ghost pill
  const ghost = document.createElement('div');
  ghost.style.cssText =
    'position:fixed;top:-200px;left:0;background:#E9967A;color:#fff;padding:5px 14px 5px 10px;border-radius:100px;font-size:12px;font-weight:600;font-family:DM Sans,system-ui,sans-serif;display:flex;align-items:center;gap:8px;box-shadow:0 6px 20px rgba(233,150,122,0.5);white-space:nowrap;z-index:99999;pointer-events:none';
  const n = toDrop.length;
  if (n > 1) {
    ghost.innerHTML = `<span style="background:rgba(255,255,255,0.25);border-radius:50%;min-width:18px;height:18px;display:flex;align-items:center;justify-content:center;font-size:10px;font-weight:700">${n}</span>${n} columns`;
  } else {
    const name = th.querySelector('.field-name-span')?.textContent?.trim() || 'Column';
    ghost.innerHTML = `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>${name}`;
  }
  document.body.appendChild(ghost);
  event.dataTransfer.setDragImage(ghost, 16, 18);
  requestAnimationFrame(() => ghost.remove());

  // Clear pending highlights once the payload is captured
  clearPending();
  document.querySelectorAll('.col-selected').forEach((el) => el.classList.remove('col-selected'));

  th.classList.add('header-dragging');
}

/**
 * @param {DragEvent} _event
 * @param {HTMLElement | null} [th]  the dragged column header (passed by the dispatcher)
 */
export function headerDragEnd(_event, th) {
  if (th instanceof HTMLElement) th.classList.remove('header-dragging');
  // Reset after any click that follows dragend has already fired
  setTimeout(() => {
    _dragStarted = false;
  }, 0);
}
