// @ts-check
/**
 * @file Block-selection mode — Esc inside an editable block enters this mode, which
 * highlights the block and accepts Up/Down to move, Enter to re-edit, Backspace/Delete
 * to remove. Click outside (or focusin on another block) clears the selection.
 *
 * Multi-select via Shift+click is partially wired (it toggles the visual highlight) but
 * no actions operate on a multi-selection yet.
 */

import { focusBlockEnd, getAdjacentBlock } from './blocks.js';

/** @type {HTMLElement | null} */
let selectedBlock = null;

/** @param {HTMLElement} block */
export function enterSelectionMode(block) {
  clearSelection();
  selectedBlock = block;
  block.classList.add('block-selected');
  block.blur();
  document.addEventListener('keydown', onSelectionKeydown);
  document.addEventListener('mousedown', onSelectionMousedown);
}

export function clearSelection() {
  if (selectedBlock) {
    selectedBlock.classList.remove('block-selected');
    selectedBlock = null;
  }
  document.removeEventListener('keydown', onSelectionKeydown);
  document.removeEventListener('mousedown', onSelectionMousedown);
}

/** @param {MouseEvent} e */
function onSelectionMousedown(e) {
  // Shift+click on a block → multi-select (visual only for now)
  const t = /** @type {Element | null} */ (e.target);
  const clicked = t?.closest('.doc-text, .doc-chart-section, .doc-divider');
  if (!clicked) {
    clearSelection();
    return;
  }
  if (e.shiftKey && selectedBlock) {
    clicked.classList.toggle('block-selected');
  } else {
    clearSelection();
  }
}

/** @param {KeyboardEvent} e */
function onSelectionKeydown(e) {
  if (!selectedBlock) {
    clearSelection();
    return;
  }

  if (e.key === 'ArrowDown') {
    e.preventDefault();
    const next = getAdjacentBlock(selectedBlock, 1);
    if (next) enterSelectionMode(next);
  } else if (e.key === 'ArrowUp') {
    e.preventDefault();
    const prev = getAdjacentBlock(selectedBlock, -1);
    if (prev) enterSelectionMode(prev);
  } else if (e.key === 'Enter') {
    e.preventDefault();
    const block = selectedBlock;
    clearSelection();
    if (block.contentEditable === 'true') focusBlockEnd(block);
  } else if (e.key === 'Backspace' || e.key === 'Delete') {
    e.preventDefault();
    const next = getAdjacentBlock(selectedBlock, 1) || getAdjacentBlock(selectedBlock, -1);
    selectedBlock.remove();
    clearSelection();
    if (next && next.contentEditable === 'true') focusBlockEnd(next);
  } else if (e.key === 'Escape') {
    clearSelection();
  }
}
