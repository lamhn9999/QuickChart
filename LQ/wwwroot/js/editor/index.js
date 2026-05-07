// @ts-check
/**
 * @file Editor entry point — replaces the old top-level `editor.js` IIFE.
 *
 * Loaded by `Views/Workspace/Workspace.cshtml` after the Chart.js global script tags. ES
 * modules are deferred, so `#doc-canvas` exists by the time this module's top-level code
 * runs.
 *
 * Sub-modules:
 *   - canvas.js       resolved `#doc-canvas` reference
 *   - blocks.js       BLOCK_TYPES, makeBlock, focus*, getAllBlocks, morphBlock,
 *                     setupBlockListeners (keyboard + input + markdown)
 *   - slash-menu.js   the `/` command palette
 *   - block-handle.js the per-block grip + context menu + Turn-into submenu + drag-reorder
 *   - toolbar.js      floating inline-format toolbar
 *   - selection.js    block-selection mode (Esc to enter, Up/Down/Backspace to navigate)
 */

import { canvas } from './canvas.js';
import {
  BLOCK_TYPES,
  PLACEHOLDER_MAP,
  initExistingBlocks,
  makeBlock,
  setupBlockListeners,
  focusBlockStart,
  insertAfterEl,
} from './blocks.js';
import { initSlashMenu } from './slash-menu.js';
import { initBlockHandle } from './block-handle.js';
import { initInlineToolbar } from './toolbar.js';
import { clearSelection } from './selection.js';

if (canvas) {
  initTitle();
  initSlashMenu();
  initBlockHandle();
  initInlineToolbar();
  initExistingBlocks();
  wireCanvasListeners();
  observeNewBlocks();
}

// ── Page title ────────────────────────────────────────────────────────────────

function initTitle() {
  if (!canvas) return;
  // Capture as a non-null local for the keydown closure below.
  const c = canvas;
  const titleEl = document.createElement('div');
  titleEl.id = 'doc-title';
  titleEl.contentEditable = 'true';
  titleEl.spellcheck = false;

  c.insertBefore(titleEl, c.firstChild);

  titleEl.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      const first = /** @type {HTMLElement | null} */ (c.querySelector('.doc-text'));
      if (first) focusBlockStart(first);
      else {
        const p = makeBlock('paragraph');
        insertAfterEl(p, titleEl);
        focusBlockStart(p);
      }
    }
  });
}

// ── Canvas-level listeners ────────────────────────────────────────────────────

function wireCanvasListeners() {
  if (!canvas) return;

  // Click empty area → exit selection mode
  canvas.addEventListener('click', (e) => {
    if (e.target === canvas) clearSelection();
  });

  // Refocusing a block clears selection
  canvas.addEventListener('focusin', (e) => {
    const t = /** @type {Element | null} */ (e.target);
    if (t?.classList.contains('doc-text')) clearSelection();
  });
}

// ── Auto-wire `.doc-text` blocks added later (e.g. by canvas-persist hydration) ──

function observeNewBlocks() {
  if (!canvas) return;
  const observer = new MutationObserver((mutations) => {
    for (const m of mutations) {
      for (const node of m.addedNodes) {
        if (node.nodeType !== 1) continue;
        const el = /** @type {Element} */ (node);
        if (el.classList.contains('doc-text') && !(/** @type {any} */ (el)._editorSetup)) {
          setupBlockListeners(/** @type {HTMLElement} */ (el));
        }
        el.querySelectorAll?.('.doc-text').forEach((child) => {
          if (!(/** @type {any} */ (child)._editorSetup)) {
            setupBlockListeners(/** @type {HTMLElement} */ (child));
          }
        });
      }
    }
  });

  observer.observe(canvas, { childList: true, subtree: true });
}

// Suppress "BLOCK_TYPES + PLACEHOLDER_MAP imported but unused" — the imports are kept for
// IDE autocomplete on the editor entry point. Remove if you're seeing real lint warnings.
void BLOCK_TYPES;
void PLACEHOLDER_MAP;
