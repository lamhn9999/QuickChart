// @ts-check
/**
 * @file `#drag` divider that resizes the table panel against the canvas panel.
 * On every mousemove, also calls `chart.resize()` on every live Chart.js instance so
 * cards in the canvas adjust to their new container width.
 */

import { chartInstances } from '../chart.js';

/**
 * Wire up the column resize handle.
 * @param {HTMLElement} resizer  the `<div id="drag">` divider element
 * @param {HTMLElement} left     the table panel (`<div id="tablePanel">`) being resized
 */
export function wirePanelResizer(resizer, left) {
  let startX = 0;
  let startWidth = 0;
  let isDragging = false;

  resizer.addEventListener('mousedown', (e) => {
    isDragging = true;
    startX = e.clientX;
    startWidth = left.getBoundingClientRect().width;
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
  });

  document.addEventListener('mousemove', (e) => {
    if (!isDragging) return;
    left.style.width = `${startWidth + (e.clientX - startX)}px`;
    chartInstances.forEach((c) => c.resize());
  });

  document.addEventListener('mouseup', () => {
    isDragging = false;
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
  });
}
