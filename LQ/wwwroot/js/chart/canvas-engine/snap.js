// @ts-check
/**
 * @file Free-form canvas alignment guides + snap-to-edge during drag.
 *
 * `snapWithGuides(dragging, rawX, rawY)` is the per-mousemove call that:
 *   1. Snaps `rawX, rawY` to the nearest of 5 alignment lines (left/center/right/mirror)
 *      against every other card on the canvas.
 *   2. Draws blue 1px guide lines into a `_canvas-guides` overlay so the user sees the snap.
 * `clearGuides()` wipes the overlay (called on pointerup).
 */

import { docCanvas } from './canvas.js';

const SNAP_THRESHOLD = 6;

/**
 * Lazy-create the absolute-positioned overlay div that hosts guide lines.
 * @returns {HTMLElement | null}
 */
function guidesOverlay() {
  const canvas = docCanvas();
  if (!canvas) return null;
  let el = document.getElementById('_canvas-guides');
  if (!el) {
    el = document.createElement('div');
    el.id = '_canvas-guides';
    el.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:8000;overflow:hidden';
    canvas.prepend(el);
  }
  return el;
}

/** Wipe all guide lines. Called on pointerup. */
export function clearGuides() {
  const el = document.getElementById('_canvas-guides');
  if (el) el.innerHTML = '';
}

/**
 * Snap `rawX,rawY` to the nearest other card edge (within SNAP_THRESHOLD), and draw guide lines.
 *
 * @param {HTMLElement} dragging  the card being dragged
 * @param {number} rawX
 * @param {number} rawY
 * @returns {{ x: number, y: number }}  snapped coords (clamped ≥ 0)
 */
export function snapWithGuides(dragging, rawX, rawY) {
  const canvas = docCanvas();
  if (!canvas) return { x: rawX, y: rawY };
  const others = Array.from(canvas.querySelectorAll(':scope > .canvas-card')).filter(
    (c) => c !== dragging
  );
  const cW = dragging.offsetWidth;
  const cH = dragging.offsetHeight;
  let x = rawX;
  let y = rawY;
  /** @type {Set<number>} */
  const vSet = new Set();
  /** @type {Set<number>} */
  const hSet = new Set();

  others.forEach((o) => {
    const oel = /** @type {HTMLElement} */ (o);
    const ox = oel.offsetLeft;
    const oy = oel.offsetTop;
    const ow = oel.offsetWidth;
    const oh = oel.offsetHeight;
    /** @type {[number, number][]} */
    const verticals = [
      [x, ox],
      [x + cW, ox + ow],
      [x + cW / 2, ox + ow / 2],
      [x, ox + ow],
      [x + cW, ox],
    ];
    verticals.forEach(([dv, ov]) => {
      if (Math.abs(dv - ov) < SNAP_THRESHOLD) {
        x += ov - dv;
        vSet.add(ov);
      }
    });
    /** @type {[number, number][]} */
    const horizontals = [
      [y, oy],
      [y + cH, oy + oh],
      [y + cH / 2, oy + oh / 2],
      [y, oy + oh],
      [y + cH, oy],
    ];
    horizontals.forEach(([dv, ov]) => {
      if (Math.abs(dv - ov) < SNAP_THRESHOLD) {
        y += ov - dv;
        hSet.add(ov);
      }
    });
  });

  const overlay = guidesOverlay();
  if (overlay) {
    overlay.innerHTML = '';
    vSet.forEach((gx) => {
      const ln = document.createElement('div');
      ln.style.cssText = `position:absolute;left:${gx}px;top:0;bottom:0;width:1px;background:rgba(59,130,246,.7)`;
      overlay.appendChild(ln);
    });
    hSet.forEach((gy) => {
      const ln = document.createElement('div');
      ln.style.cssText = `position:absolute;top:${gy}px;left:0;right:0;height:1px;background:rgba(59,130,246,.7)`;
      overlay.appendChild(ln);
    });
  }
  return { x: Math.max(0, x), y: Math.max(0, y) };
}
