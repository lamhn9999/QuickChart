// @ts-check
/**
 * @file Per-block grip handle — the `⋮⋮` icon that appears next to a hovered block.
 *
 * Click the grip → context menu (Duplicate / Turn into ▸ / Delete). Drag the grip → reorder
 * blocks. Free-form `.text-card` (chart-canvas absolute card) responds to its own
 * `tc-handle-tap` synthetic event with the same context menu.
 *
 * This file replaces the old `window._tcCreate` indirection: the Duplicate action calls
 * `createTextCard` from `chart/text-card.js` directly. The window shim is kept by
 * `workspace/index.js` only for Razor inline-onclick handlers (Phase 8 will eliminate it).
 */

import { canvas } from './canvas.js';
import {
  BLOCK_TYPES,
  PLACEHOLDER_MAP,
  animateIn,
  focusBlockEnd,
  getAllBlocks,
  insertAfterEl,
  morphBlock,
  setupBlockListeners,
} from './blocks.js';
import { createTextCard } from '../chart/text-card.js';

const handle = {
  /** @type {HTMLElement | null} */
  el: null,
  /** @type {HTMLElement | null} */
  contextMenu: null,
  /** @type {HTMLElement | null} */
  turnMenu: null,
  /** @type {HTMLElement | null} */
  currentBlock: null,
  /** @type {any} */
  showTimer: null,
  isDragging: false,
  /** @type {HTMLElement | null} */
  dragBlock: null,
  /** @type {HTMLElement | null} */
  dragPlaceholder: null,
};

export function initBlockHandle() {
  if (!canvas) return;
  // Capture as a non-null local so closures below pass strict null checks
  // (TypeScript can't narrow across closure boundaries on the imported binding).
  const c = canvas;

  // ── Grip button ──
  const el = document.createElement('div');
  el.id = 'doc-handle';
  el.innerHTML = `<svg width="14" height="14" viewBox="0 0 14 14" fill="currentColor">
      <circle cx="4.5" cy="2.5" r="1.4"/><circle cx="9.5" cy="2.5" r="1.4"/>
      <circle cx="4.5" cy="7"   r="1.4"/><circle cx="9.5" cy="7"   r="1.4"/>
      <circle cx="4.5" cy="11.5" r="1.4"/><circle cx="9.5" cy="11.5" r="1.4"/>
    </svg>`;
  document.body.appendChild(el);
  handle.el = el;

  // ── Context menu ──
  const cm = document.createElement('div');
  cm.id = 'doc-handle-menu';
  cm.innerHTML = `
      <div class="hm-item" data-action="duplicate">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"/></svg>
        Duplicate
      </div>
      <div class="hm-item hm-turn" data-action="turnInto">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3l4 4-4 4"/><path d="M3 7h18"/><path d="M7 21l-4-4 4-4"/><path d="M21 17H3"/></svg>
        Turn into
        <svg class="hm-chevron" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M9 18l6-6-6-6"/></svg>
      </div>
      <div class="hm-sep"></div>
      <div class="hm-item hm-danger" data-action="delete">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14H6L5 6"/><path d="M9 6V4h6v2"/></svg>
        Delete
      </div>
    `;
  document.body.appendChild(cm);
  handle.contextMenu = cm;

  // ── Turn-into submenu ──
  const tm = document.createElement('div');
  tm.id = 'doc-turn-menu';
  tm.innerHTML = BLOCK_TYPES.filter((bt) => bt.id !== 'divider')
    .map(
      (bt) =>
        `<div class="hm-item" data-type="${bt.id}">
        <span class="hm-turn-icon">${bt.icon}</span>${bt.label}
      </div>`
    )
    .join('');
  document.body.appendChild(tm);
  handle.turnMenu = tm;

  // ── Handle: show on canvas hover ──
  canvas.addEventListener('mousemove', (e) => {
    const t = /** @type {Element | null} */ (e.target);
    const block = /** @type {HTMLElement | null} */ (
      t?.closest('.doc-text, .doc-chart-section, .doc-divider')
    );
    if (
      !block ||
      block.id === 'docDropCursor' ||
      block.id === 'doc-title' ||
      block.closest('.text-card')
    ) {
      scheduleHide();
      return;
    }
    handle.currentBlock = block;
    clearTimeout(handle.showTimer);
    handle.showTimer = setTimeout(() => showHandle(block), 200);
  });

  canvas.addEventListener('mouseleave', () => scheduleHide());
  el.addEventListener('mouseenter', () => clearTimeout(handle.showTimer));
  el.addEventListener('mouseleave', (e) => {
    const r = /** @type {Node | null} */ (e.relatedTarget);
    if (!r || !cm.contains(r)) scheduleHide();
  });

  // ── Handle click → context menu ──
  el.addEventListener('mousedown', (e) => {
    e.preventDefault();
    e.stopPropagation();
    if (cm.classList.contains('open')) closeContextMenu();
    else openContextMenu();
  });

  // ── Context menu actions ──
  cm.addEventListener('mousedown', (e) => {
    e.preventDefault();
    const t = /** @type {Element | null} */ (e.target);
    const item = /** @type {HTMLElement | null} */ (t?.closest('[data-action]'));
    if (!item) return;
    const action = item.dataset.action;
    const block = handle.currentBlock;
    if (!block) return;

    const isTcCard = block.classList.contains('text-card');

    if (action === 'delete') {
      closeContextMenu();
      block.remove();
    } else if (action === 'duplicate') {
      closeContextMenu();
      if (isTcCard) {
        // Duplicate the text card at a 20px offset using the carved factory
        const newCard = createTextCard({
          x: block.offsetLeft + 20,
          y: block.offsetTop + 20,
        });
        if (newCard) {
          const src = block.querySelector('.tc-content');
          const dst = newCard.querySelector('.tc-content');
          if (src && dst) dst.innerHTML = src.innerHTML;
        }
      } else {
        const clone = /** @type {HTMLElement} */ (block.cloneNode(true));
        /** @type {any} */ (clone)._editorSetup = false;
        animateIn(clone);
        insertAfterEl(clone, block);
        if (clone.contentEditable === 'true') {
          setupBlockListeners(clone);
          focusBlockEnd(clone);
        }
      }
    } else if (action === 'turnInto') {
      const cmRect = cm.getBoundingClientRect();
      tm.style.top = `${cmRect.top}px`;
      tm.style.left = `${cmRect.right + 4}px`;
      tm.classList.add('open');
    }
  });

  // ── Turn-into submenu clicks ──
  tm.addEventListener('mousedown', (e) => {
    e.preventDefault();
    const t = /** @type {Element | null} */ (e.target);
    const item = /** @type {HTMLElement | null} */ (t?.closest('[data-type]'));
    if (!item) return;
    const block = handle.currentBlock;
    closeTurnMenu();
    closeContextMenu();
    if (!block) return;

    const newType = item.dataset.type ?? '';
    if (block.classList.contains('text-card')) {
      const content = /** @type {HTMLElement | null} */ (block.querySelector('.tc-content'));
      if (content) {
        content.dataset.blockType = newType;
        content.dataset.placeholder = PLACEHOLDER_MAP[newType] || PLACEHOLDER_MAP.paragraph;
      }
    } else {
      morphBlock(block, newType);
    }
  });

  // ── Close on outside click ──
  document.addEventListener('mousedown', (e) => {
    const t = /** @type {Node | null} */ (e.target);
    if (t && !cm.contains(t) && !el.contains(t) && !tm.contains(t)) {
      closeContextMenu();
    }
  });

  // ── TC handle tap → open context menu for free-form text cards ──
  canvas.addEventListener('tc-handle-tap', (/** @type {any} */ e) => {
    const t = /** @type {Element | null} */ (e.target);
    const tcH = /** @type {HTMLElement | null} */ (t?.closest('.tc-handle'));
    if (!tcH) return;
    const card = /** @type {HTMLElement | null} */ (tcH.closest('.text-card'));
    if (!card) return;
    e.stopPropagation();
    closeContextMenu();
    handle.currentBlock = card;
    const rect = tcH.getBoundingClientRect();
    cm.style.top = `${rect.bottom + 4}px`;
    cm.style.left = `${rect.left}px`;
    cm.classList.add('open');
  });

  // ── Drag reorder ──
  el.draggable = true;

  el.addEventListener('dragstart', (e) => {
    const block = handle.currentBlock;
    if (!block) {
      e.preventDefault();
      return;
    }

    handle.isDragging = true;
    handle.dragBlock = block;
    block.style.opacity = '0.35';

    const ghost = document.createElement('div');
    ghost.style.cssText =
      'position:fixed;top:-999px;background:#18181B;color:#fff;padding:5px 12px;border-radius:6px;font-size:12px;font-family:"DM Sans",sans-serif;white-space:nowrap;';
    ghost.textContent = (block.textContent || 'Block').substring(0, 32);
    document.body.appendChild(ghost);
    e.dataTransfer?.setDragImage(ghost, 0, 0);
    requestAnimationFrame(() => ghost.remove());
    if (e.dataTransfer) {
      e.dataTransfer.effectAllowed = 'move';
      e.dataTransfer.setData('text/block-reorder', '1');
    }
  });

  el.addEventListener('dragend', () => {
    if (handle.dragBlock) handle.dragBlock.style.opacity = '';
    removeDragPlaceholder();
    handle.isDragging = false;
    handle.dragBlock = null;
  });

  canvas.addEventListener('dragover', (e) => {
    if (!handle.isDragging) return;
    e.preventDefault();
    e.stopPropagation();

    const blocks = getAllBlocks().filter((b) => b !== handle.dragBlock);
    removeDragPlaceholder();

    const ph = document.createElement('div');
    ph.className = 'doc-drag-ph';
    handle.dragPlaceholder = ph;

    /** @type {HTMLElement | null} */
    let insertBeforeEl = null;
    for (const b of blocks) {
      const r = b.getBoundingClientRect();
      if (e.clientY < r.top + r.height / 2) {
        insertBeforeEl = b;
        break;
      }
    }

    if (insertBeforeEl) c.insertBefore(ph, insertBeforeEl);
    else {
      // Insert before the drop cursor if it exists
      const cur = document.getElementById('docDropCursor');
      if (cur) c.insertBefore(ph, cur);
      else c.appendChild(ph);
    }
  });

  canvas.addEventListener('drop', (e) => {
    if (!handle.isDragging || !handle.dragBlock) return;
    e.preventDefault();
    e.stopPropagation();

    const ph = c.querySelector('.doc-drag-ph');
    if (ph) {
      c.insertBefore(handle.dragBlock, ph);
      ph.remove();
    }
    handle.isDragging = false;
  });
}

// ── helpers ──────────────────────────────────────────────────────────────────

/** @param {HTMLElement} block */
function showHandle(block) {
  const el = handle.el;
  if (!el) return;
  const blockRect = block.getBoundingClientRect();

  el.style.top = `${blockRect.top + blockRect.height / 2 - 12}px`;
  el.style.left = `${blockRect.left - 30}px`;
  el.classList.add('visible');
  requestAnimationFrame(() => el.classList.add('show'));
}

/** @type {any} */
let hideTimer = null;
function scheduleHide() {
  clearTimeout(hideTimer);
  hideTimer = setTimeout(() => {
    if (
      handle.contextMenu &&
      handle.el &&
      !handle.contextMenu.classList.contains('open') &&
      !handle.el.matches(':hover')
    ) {
      hideHandle();
    }
  }, 100);
}

function hideHandle() {
  clearTimeout(handle.showTimer);
  if (!handle.el) return;
  handle.el.classList.remove('show');
  setTimeout(() => {
    if (handle.el && !handle.el.classList.contains('show')) handle.el.classList.remove('visible');
  }, 150);
}

function openContextMenu() {
  if (!handle.el || !handle.contextMenu) return;
  const elRect = handle.el.getBoundingClientRect();
  const cm = handle.contextMenu;
  cm.style.top = `${elRect.top}px`;
  cm.style.left = `${elRect.right + 6}px`;
  cm.classList.add('open');
}

function closeContextMenu() {
  handle.contextMenu?.classList.remove('open');
  closeTurnMenu();
}

function closeTurnMenu() {
  handle.turnMenu?.classList.remove('open');
}

function removeDragPlaceholder() {
  if (!canvas) return;
  canvas.querySelectorAll('.doc-drag-ph').forEach((p) => p.remove());
  handle.dragPlaceholder = null;
}
