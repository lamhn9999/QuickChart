// @ts-check
/**
 * @file 8-handle resize for chart cards + east-only resize for text cards.
 *
 * - `makeResizable(card, instancesRef, opts)` adds NW/N/NE/E/SE/S/SW/W handles. Every move
 *   call iterates `instancesRef` (an array of Chart.js instances owned by the card) and
 *   calls `chart.resize()` so the canvas inside reflows.
 * - `wireTextCardResize(card, handleE)` is simpler: only horizontal resize, height auto.
 */

import { snapResize, clearGuides } from './snap.js';
import { updateCanvasSize } from './canvas.js';

/**
 * @param {HTMLElement} card
 * @param {any[]} instancesRef  array of Chart.js instances owned by this card
 * @param {{ minW?: number, minH?: number }} [opts]
 */
export function makeResizable(card, instancesRef, opts = {}) {
  const minW = opts.minW ?? 280;
  const minH = opts.minH ?? 160;
  /** @type {[string, string][]} */
  const DIRS = [
    ['nw', 'top:-5px;left:-5px;width:10px;height:10px;cursor:nw-resize;border-radius:50%'],
    ['n', 'top:-4px;left:14px;right:14px;height:8px;cursor:n-resize'],
    ['ne', 'top:-5px;right:-5px;width:10px;height:10px;cursor:ne-resize;border-radius:50%'],
    ['e', 'top:14px;right:-4px;bottom:14px;width:8px;cursor:e-resize'],
    ['se', 'bottom:-5px;right:-5px;width:10px;height:10px;cursor:se-resize;border-radius:50%'],
    ['s', 'bottom:-4px;left:14px;right:14px;height:8px;cursor:s-resize'],
    ['sw', 'bottom:-5px;left:-5px;width:10px;height:10px;cursor:sw-resize;border-radius:50%'],
    ['w', 'top:14px;left:-4px;bottom:14px;width:8px;cursor:w-resize'],
  ];
  DIRS.forEach(([dir, css]) => {
    const h = document.createElement('div');
    h.dataset.resizeDir = dir;
    h.style.cssText = `position:absolute;${css};z-index:90;`;
    card.appendChild(h);

    let sx = 0;
    let sy = 0;
    let sw = 0;
    let sh = 0;
    let sl = 0;
    let st = 0;
    const onMove = (/** @type {PointerEvent} */ e) => {
      const dx = e.clientX - sx;
      const dy = e.clientY - sy;
      let W = sw;
      let H = sh;
      let L = sl;
      let T = st;
      if (dir.includes('e')) W = Math.max(minW, sw + dx);
      if (dir.includes('s')) H = Math.max(minH, sh + dy);
      if (dir.includes('w')) {
        W = Math.max(minW, sw - dx);
        L = sl + (sw - W);
      }
      if (dir.includes('n')) {
        H = Math.max(minH, sh - dy);
        T = st + (sh - H);
      }
      const snapped = snapResize(card, L, T, W, H, dir, sl + sw, st + sh, minW, minH);
      L = snapped.left; T = snapped.top; W = snapped.width; H = snapped.height;
      card.style.width = `${W}px`;
      card.style.height = `${H}px`;
      card.style.left = `${L}px`;
      card.style.top = `${T}px`;
      instancesRef.forEach((c) => c.resize());
      updateCanvasSize();
    };
    const onUp = () => {
      card.classList.remove('is-resizing');
      clearGuides();
      document.body.style.userSelect = '';
      document.body.style.cursor = '';
      document.removeEventListener('pointermove', onMove);
      document.removeEventListener('pointerup', onUp);
    };
    h.addEventListener('pointerdown', (e) => {
      e.stopPropagation();
      e.preventDefault();
      sx = e.clientX;
      sy = e.clientY;
      sw = card.offsetWidth;
      sh = card.offsetHeight;
      sl = card.offsetLeft;
      st = card.offsetTop;
      card.classList.add('is-resizing');
      document.body.style.userSelect = 'none';
      document.body.style.cursor = window.getComputedStyle(h).cursor;
      document.addEventListener('pointermove', onMove);
      document.addEventListener('pointerup', onUp);
    });
  });
}

/**
 * Horizontal-only resize for text cards (height stays auto-sized to content).
 * @param {HTMLElement} card
 * @param {HTMLElement} handleE  the east-edge resize strip
 */
export function wireTextCardResize(card, handleE) {
  let sx = 0;
  let sw = 0;
  let sl = 0;
  let st = 0;
  let sh = 0;
  const onMove = (/** @type {PointerEvent} */ e) => {
    let W = Math.max(160, sw + (e.clientX - sx));
    const snapped = snapResize(card, sl, st, W, sh, 'e', sl + sw, st + sh, 160, 0);
    card.style.width = `${snapped.width}px`;
    updateCanvasSize();
  };
  const onUp = () => {
    card.classList.remove('is-resizing');
    clearGuides();
    document.body.style.userSelect = '';
    document.body.style.cursor = '';
    document.removeEventListener('pointermove', onMove);
    document.removeEventListener('pointerup', onUp);
  };
  handleE.addEventListener('pointerdown', (e) => {
    e.stopPropagation();
    e.preventDefault();
    sx = e.clientX;
    sw = card.offsetWidth;
    sl = card.offsetLeft;
    st = card.offsetTop;
    sh = card.offsetHeight;
    card.classList.add('is-resizing');
    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'e-resize';
    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
  });
}
