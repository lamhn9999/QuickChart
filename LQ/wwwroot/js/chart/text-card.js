// @ts-check
/**
 * @file Free-form text card for the canvas — a small editable rich-text block.
 *
 * Visually identical to a chart card but the body is `contentEditable`. Has its own
 * 6-dot drag handle on the left, a delete button on hover, and an east-only resize strip.
 * Imported (and called as `_tcCreate`/`addTextCard`) from `editor.js` and the toolbar.
 */

import { docCanvas, updateCanvasSize } from './canvas-engine/canvas.js';
import { makeDraggable, nextZOrder } from './canvas-engine/draggable.js';
import { wireTextCardResize } from './canvas-engine/resizable.js';

/**
 * Insert a new text card into `#doc-canvas`. Defaults to centred placement; pass `dropPos`
 * (in canvas coords) to drop at the cursor.
 *
 * @param {{ x: number, y: number } | null} [dropPos]
 * @returns {HTMLElement | null}
 */
export function createTextCard(dropPos = null) {
  const canvas = docCanvas();
  if (!canvas) return null;

  const card = document.createElement('div');
  card.classList.add('canvas-card', 'text-card');

  const pos = dropPos ?? {
    x: Math.max(20, canvas.offsetWidth / 2 - 160),
    y: Math.max(20, canvas.offsetHeight / 2 - 30),
  };
  card.style.cssText = `position:absolute;left:${pos.x}px;top:${pos.y}px;width:320px;z-index:${nextZOrder()}`;

  // Hover zone — extends left to cover the handle area, eliminating dead zones
  const hoverZone = document.createElement('div');
  hoverZone.className = 'tc-hover-zone';

  // Six-dot drag handle on the left
  const tcHandle = document.createElement('div');
  tcHandle.className = 'tc-handle drag-handle';
  tcHandle.setAttribute('title', 'Drag to move');
  tcHandle.innerHTML = `<svg width="10" height="14" viewBox="0 0 10 14" fill="currentColor" aria-hidden="true"><circle cx="2" cy="2" r="1.5"/><circle cx="8" cy="2" r="1.5"/><circle cx="2" cy="7" r="1.5"/><circle cx="8" cy="7" r="1.5"/><circle cx="2" cy="12" r="1.5"/><circle cx="8" cy="12" r="1.5"/></svg>`;

  // Editable content area
  const content = document.createElement('div');
  content.className = 'tc-content';
  content.dataset.placeholder = 'Type something…';
  content.contentEditable = 'true';
  content.spellcheck = false;
  content.addEventListener('focus', () => card.classList.add('is-focused'));
  content.addEventListener('blur', () => card.classList.remove('is-focused'));

  hoverZone.appendChild(tcHandle);
  hoverZone.appendChild(content);

  // Delete button — appears on hover at top-right
  const delBtn = document.createElement('button');
  delBtn.className = 'tc-delete';
  delBtn.title = 'Delete';
  delBtn.innerHTML = `<svg width="8" height="8" viewBox="0 0 8 8" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="1" y1="1" x2="7" y2="7"/><line x1="7" y1="1" x2="1" y2="7"/></svg>`;
  delBtn.addEventListener('pointerdown', (e) => e.stopPropagation());
  delBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    card.remove();
    updateCanvasSize();
  });

  // East resize strip — horizontal only, height stays auto
  const resizeE = document.createElement('div');
  resizeE.className = 'tc-resize-e';

  card.appendChild(hoverZone);
  card.appendChild(delBtn);
  card.appendChild(resizeE);
  canvas.appendChild(card);

  makeDraggable(card);
  wireTextCardResize(card, resizeE);
  updateCanvasSize();

  requestAnimationFrame(() => content.focus());
  return card;
}
