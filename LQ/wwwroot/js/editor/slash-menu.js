// @ts-check
/**
 * @file `/` command palette — pops up below the caret when the user types `/` inside a
 * `.doc-text` block. Up/down navigates filtered types; Enter picks; Esc closes.
 *
 * The keyboard handler in blocks.js delegates Up/Down/Enter/Esc/Backspace to
 * `handleSlashKeydown(e)` while the menu is open; the input handler delegates to
 * `refreshSlashMenuQuery(block)` so the query stays in sync with the block's text.
 */

import { canvas } from './canvas.js';
import { BLOCK_TYPES, makeBlock, insertAfterEl, focusBlockStart, morphBlock } from './blocks.js';

const slash = {
  open: false,
  query: '',
  activeIndex: 0,
  /** @type {HTMLElement | null} */
  triggerBlock: null,
  /** @type {HTMLElement | null} */
  menu: null,
};

/** Initialize once on boot — creates the menu element + outside-click closer. */
export function initSlashMenu() {
  const menu = document.createElement('div');
  menu.id = 'doc-slash-menu';
  document.body.appendChild(menu);
  slash.menu = menu;

  document.addEventListener('mousedown', (e) => {
    const t = /** @type {Node} */ (e.target);
    if (slash.open && !menu.contains(t)) closeSlashMenu();
  });
}

export function isSlashOpen() {
  return slash.open;
}

export function getSlashTriggerBlock() {
  return slash.triggerBlock;
}

/** @param {HTMLElement} block */
export function openSlashMenu(block) {
  slash.open = true;
  slash.query = '';
  slash.activeIndex = 0;
  slash.triggerBlock = block;
  renderSlashMenu();
  positionSlashMenu();
  slash.menu?.classList.add('open');
}

export function closeSlashMenu() {
  slash.open = false;
  slash.menu?.classList.remove('open');
  slash.query = '';
  slash.triggerBlock = null;
}

/**
 * Up/Down/Enter/Esc/Backspace handler. Returns true when the key has been consumed and
 * the caller (blocks.js) should `return` immediately.
 *
 * @param {KeyboardEvent} e
 * @returns {boolean}  true if the key was consumed
 */
export function handleSlashKeydown(e) {
  const filtered = filterTypes();

  if (e.key === 'ArrowDown') {
    e.preventDefault();
    slash.activeIndex = Math.min(slash.activeIndex + 1, filtered.length - 1);
    renderSlashMenu();
    return true;
  }
  if (e.key === 'ArrowUp') {
    e.preventDefault();
    slash.activeIndex = Math.max(slash.activeIndex - 1, 0);
    renderSlashMenu();
    return true;
  }
  if (e.key === 'Enter') {
    e.preventDefault();
    const pick = filtered[slash.activeIndex];
    if (pick) pickSlashItem(pick.id);
    return true;
  }
  if (e.key === 'Escape') {
    closeSlashMenu();
    return true;
  }
  if (e.key === 'Backspace') {
    if (slash.query.length === 0) closeSlashMenu();
    // Let default backspace fire so the slash char gets deleted; input handler updates query.
    return false;
  }
  // Any other key while menu is open — let it pass through (typing extends the query).
  return false;
}

/**
 * Re-read `block.textContent` and refresh `slash.query` (or close if `/` is gone).
 * @param {HTMLElement} block
 */
export function refreshSlashMenuQuery(block) {
  const text = block.textContent ?? '';
  const idx = text.lastIndexOf('/');
  if (idx === -1) {
    closeSlashMenu();
  } else {
    slash.query = text.substring(idx + 1);
    renderSlashMenu();
    positionSlashMenu();
  }
}

/** Apply the chosen block type to the trigger block. */
export function pickSlashItem(/** @type {string} */ type) {
  const block = slash.triggerBlock;
  if (!block || !canvas) {
    closeSlashMenu();
    return;
  }

  // Strip the slash + query from the block content
  const text = block.textContent ?? '';
  const slashIdx = text.lastIndexOf('/');
  if (slashIdx !== -1) block.textContent = text.substring(0, slashIdx);

  closeSlashMenu();

  const isEmpty = (block.textContent ?? '').trim() === '';

  if (type === 'divider') {
    const divider = makeBlock('divider');
    if (isEmpty) {
      canvas.replaceChild(divider, block);
    } else {
      insertAfterEl(divider, block);
    }
    const p = makeBlock('paragraph');
    insertAfterEl(p, divider);
    focusBlockStart(p);
    return;
  }

  if (isEmpty) {
    morphBlock(block, type);
  } else {
    const newBlock = makeBlock(type);
    insertAfterEl(newBlock, block);
    focusBlockStart(newBlock);
  }
}

// ── Internal ──────────────────────────────────────────────────────────────────

function filterTypes() {
  const q = slash.query.toLowerCase();
  return q
    ? BLOCK_TYPES.filter(
        (bt) => bt.label.toLowerCase().includes(q) || bt.keys.some((k) => k.includes(q))
      )
    : BLOCK_TYPES;
}

function renderSlashMenu() {
  const menu = slash.menu;
  if (!menu) return;
  const filtered = filterTypes();

  if (filtered.length === 0) {
    menu.innerHTML = '<div class="slash-empty">No results for "' + slash.query + '"</div>';
    return;
  }

  if (slash.activeIndex >= filtered.length) slash.activeIndex = filtered.length - 1;

  menu.innerHTML = filtered
    .map(
      (bt, i) => `
      <div class="slash-item ${i === slash.activeIndex ? 'active' : ''}" data-type="${bt.id}">
        <div class="slash-icon">${bt.icon}</div>
        <div class="slash-text">
          <div class="slash-label">${bt.label}</div>
          <div class="slash-desc">${bt.desc}</div>
        </div>
      </div>
    `
    )
    .join('');

  menu.querySelectorAll('.slash-item').forEach((item) => {
    item.addEventListener('mousedown', (e) => {
      e.preventDefault();
      pickSlashItem(/** @type {HTMLElement} */ (item).dataset.type ?? '');
    });
  });
}

function positionSlashMenu() {
  const menu = slash.menu;
  if (!menu || !canvas) return;
  const sel = window.getSelection();
  /** @type {DOMRect | undefined} */
  let rect;

  if (sel && sel.rangeCount) rect = sel.getRangeAt(0).getBoundingClientRect();

  if (!rect || rect.width === 0) {
    rect = (slash.triggerBlock || canvas).getBoundingClientRect();
  }

  const mW = 272;
  const mH = 320;
  const vW = window.innerWidth;
  const vH = window.innerHeight;

  let top = rect.bottom + 6;
  let left = rect.left;

  if (top + mH > vH - 12) top = rect.top - mH - 6;
  if (top < 8) top = 8;
  if (left + mW > vW - 8) left = vW - mW - 8;
  if (left < 8) left = 8;

  menu.style.top = `${top}px`;
  menu.style.left = `${left}px`;
}
