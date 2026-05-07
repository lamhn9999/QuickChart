// @ts-check
/**
 * @file Card drag wiring + the global z-order counter.
 *
 * Every call to `nextZOrder()` returns an ever-increasing integer that drag-start handlers
 * assign to `card.style.zIndex` so the most-recently-touched card lifts above its neighbours.
 *
 * `makeDraggable(card)` wires every `.drag-handle` inside `card`:
 *   - `.tc-handle` (text-card grip) uses tap-detection: a release without significant
 *     movement fires a `tc-handle-tap` CustomEvent instead of dragging.
 *   - All other handles drag immediately on pointerdown.
 *
 * Idempotent — re-calling on a card with `[data-draggable]` skips re-wiring.
 */

import { snapWithGuides, clearGuides } from './snap.js';
import { updateCanvasSize } from './canvas.js';

let _zOrderCounter = 10;

/** Returns the next z-index to assign to a card. Bumps the global counter. */
export function nextZOrder() {
  return ++_zOrderCounter;
}

/**
 * Wire pointer-based drag on every `.drag-handle` inside `card`.
 * @param {HTMLElement} card
 */
export function makeDraggable(card) {
  card.querySelectorAll('.drag-handle').forEach((handleNode) => {
    const handle = /** @type {HTMLElement} */ (handleNode);
    if (handle.dataset.dragWired) return;
    handle.dataset.dragWired = '1';

    const isTcHandle = handle.classList.contains('tc-handle');
    let pX0 = 0;
    let pY0 = 0;
    let cX0 = 0;
    let cY0 = 0;
    let dragging = false;

    const onMove = (/** @type {PointerEvent} */ e) => {
      if (isTcHandle && !dragging) {
        const dx = e.clientX - pX0;
        const dy = e.clientY - pY0;
        if (dx * dx + dy * dy < 25) return; // 5px threshold before drag starts
        dragging = true;
        card.classList.add('is-dragging');
      }
      if (isTcHandle && !dragging) return;
      const { x, y } = snapWithGuides(card, cX0 + e.clientX - pX0, cY0 + e.clientY - pY0);
      card.style.left = `${x}px`;
      card.style.top = `${y}px`;
      updateCanvasSize();
    };

    const onUp = () => {
      if (isTcHandle && !dragging) {
        // Short press without drag → menu-open tap
        handle.dispatchEvent(new CustomEvent('tc-handle-tap', { bubbles: true }));
      }
      if (dragging || !isTcHandle) card.classList.remove('is-dragging');
      clearGuides();
      document.removeEventListener('pointermove', onMove);
      document.removeEventListener('pointerup', onUp);
      document.body.style.userSelect = '';
      document.body.style.cursor = '';
    };

    handle.addEventListener('pointerdown', (e) => {
      const t = e.target;
      if (t instanceof Element && t.closest('button,input,a,[role="button"]')) return;
      e.preventDefault();
      dragging = false;
      pX0 = e.clientX;
      pY0 = e.clientY;
      cX0 = card.offsetLeft;
      cY0 = card.offsetTop;
      if (!isTcHandle) {
        dragging = true;
        card.classList.add('is-dragging');
      }
      card.style.zIndex = String(nextZOrder());
      document.body.style.userSelect = 'none';
      document.body.style.cursor = 'grabbing';
      document.addEventListener('pointermove', onMove);
      document.addEventListener('pointerup', onUp);
    });
  });

  if (!card.dataset.draggable) {
    card.dataset.draggable = '1';
    // Click anywhere on the card → bring to front (capture phase so it runs before children).
    card.addEventListener(
      'pointerdown',
      () => {
        card.style.zIndex = String(nextZOrder());
      },
      true
    );
  }
}
