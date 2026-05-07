// @ts-check
/**
 * @file Block engine for the Notion-like document editor.
 *
 * Provides:
 *   - Block-type registry (`BLOCK_TYPES`) consumed by the slash menu, block handle,
 *     and keyboard handlers.
 *   - `PLACEHOLDER_MAP` used by `makeBlock` and morph-block-type paths.
 *   - DOM primitives: `makeBlock`, `animateIn`, `insertAfterEl`, `focusBlockEnd/Start`,
 *     `getAllBlocks`, `getAdjacentBlock`, `morphBlock`.
 *   - Per-block keyboard + input wiring (`setupBlockListeners`) and the markdown shortcut
 *     detector.
 *
 * Imports from slash-menu.js are safe despite the import cycle (slash-menu.js → blocks.js)
 * because no top-level code reads the imported values during module init — only function
 * bodies, which run later.
 */

import { canvas } from './canvas.js';
import {
  isSlashOpen,
  openSlashMenu,
  handleSlashKeydown,
  refreshSlashMenuQuery,
  getSlashTriggerBlock,
} from './slash-menu.js';
import { enterSelectionMode } from './selection.js';

/**
 * @typedef {Object} BlockType
 * @property {string} id
 * @property {string} label
 * @property {string} icon
 * @property {string} desc
 * @property {string[]} keys
 */

/** @type {BlockType[]} */
export const BLOCK_TYPES = [
  {
    id: 'paragraph',
    label: 'Text',
    icon: '¶',
    desc: 'Plain text paragraph',
    keys: ['text', 'paragraph', 'p', 'plain'],
  },
  {
    id: 'h1',
    label: 'Heading 1',
    icon: 'H1',
    desc: 'Large section heading',
    keys: ['h1', 'heading1', 'heading 1', 'title', 'large'],
  },
  {
    id: 'h2',
    label: 'Heading 2',
    icon: 'H2',
    desc: 'Medium section heading',
    keys: ['h2', 'heading2', 'heading 2', 'medium'],
  },
  {
    id: 'h3',
    label: 'Heading 3',
    icon: 'H3',
    desc: 'Small section heading',
    keys: ['h3', 'heading3', 'heading 3', 'small'],
  },
  {
    id: 'callout',
    label: 'Callout',
    icon: '»',
    desc: 'Highlighted note or quote',
    keys: ['callout', 'note', 'tip', 'quote', 'blockquote', 'highlight'],
  },
  {
    id: 'code',
    label: 'Code',
    icon: '</>',
    desc: 'Monospace code block',
    keys: ['code', 'pre', 'snippet', 'mono', 'technical'],
  },
  {
    id: 'divider',
    label: 'Divider',
    icon: '—',
    desc: 'Horizontal separator',
    keys: ['divider', 'separator', 'hr', 'line', 'rule'],
  },
];

/** @type {Record<string, string>} */
export const PLACEHOLDER_MAP = {
  h1: 'Heading 1',
  h2: 'Heading 2',
  h3: 'Heading 3',
  callout: 'Type a note…',
  code: 'Code…',
  paragraph: "Start writing, or press '/' for commands…",
};

// ── Block creation ────────────────────────────────────────────────────────────

/**
 * @param {string} type
 * @param {string} [content]
 * @returns {HTMLElement}
 */
export function makeBlock(type, content = '') {
  if (type === 'divider') {
    const wrap = document.createElement('div');
    wrap.className = 'doc-divider';
    wrap.dataset.blockType = 'divider';
    const hr = document.createElement('hr');
    wrap.appendChild(hr);
    animateIn(wrap);
    return wrap;
  }

  const el = document.createElement('div');
  el.className = 'doc-text';
  el.contentEditable = 'true';
  el.spellcheck = false;
  el.dataset.blockType = type || 'paragraph';
  el.dataset.placeholder = PLACEHOLDER_MAP[type] || PLACEHOLDER_MAP.paragraph;

  if (content) el.textContent = content;

  animateIn(el);
  setupBlockListeners(el);
  return el;
}

/** @param {HTMLElement} el */
export function animateIn(el) {
  el.style.animation = 'none';
  requestAnimationFrame(() => {
    el.style.animation = 'blockIn 120ms cubic-bezier(0.16,1,0.3,1)';
  });
}

/**
 * @param {Node} newEl
 * @param {Node} refEl
 */
export function insertAfterEl(newEl, refEl) {
  if (!canvas) return;
  const next = refEl.nextSibling;
  if (next) canvas.insertBefore(newEl, next);
  else canvas.appendChild(newEl);
}

/** @param {HTMLElement | null} el */
export function focusBlockEnd(el) {
  if (!el || el.contentEditable !== 'true') return;
  el.focus();
  const sel = window.getSelection();
  if (!sel) return;
  const range = document.createRange();
  if (el.lastChild) {
    range.selectNodeContents(el);
    range.collapse(false);
  } else {
    range.setStart(el, 0);
    range.collapse(true);
  }
  sel.removeAllRanges();
  sel.addRange(range);
}

/** @param {HTMLElement | null} el */
export function focusBlockStart(el) {
  if (!el || el.contentEditable !== 'true') return;
  el.focus();
  const sel = window.getSelection();
  if (!sel) return;
  const range = document.createRange();
  range.setStart(el, 0);
  range.collapse(true);
  sel.removeAllRanges();
  sel.addRange(range);
}

/** @returns {HTMLElement[]} */
export function getAllBlocks() {
  if (!canvas) return [];
  return /** @type {HTMLElement[]} */ (
    [...canvas.children].filter(
      (c) =>
        c.classList.contains('doc-text') ||
        c.classList.contains('doc-chart-section') ||
        c.classList.contains('doc-divider')
    )
  );
}

/**
 * @param {HTMLElement} block
 * @param {1 | -1} dir
 * @returns {HTMLElement | null}
 */
export function getAdjacentBlock(block, dir) {
  const blocks = getAllBlocks();
  const idx = blocks.indexOf(block);
  if (idx === -1) return null;
  return blocks[idx + dir] || null;
}

/**
 * @param {HTMLElement | null} block
 * @param {string} type
 */
export function morphBlock(block, type) {
  if (!block || block.contentEditable !== 'true' || !canvas) return;

  if (type === 'divider') {
    const divider = makeBlock('divider');
    canvas.replaceChild(divider, block);
    const p = makeBlock('paragraph');
    insertAfterEl(p, divider);
    focusBlockStart(p);
    return;
  }

  block.dataset.blockType = type;
  block.dataset.placeholder = PLACEHOLDER_MAP[type] || PLACEHOLDER_MAP.paragraph;
  focusBlockEnd(block);
}

// ── Per-block listeners ───────────────────────────────────────────────────────

/** @param {HTMLElement} el */
export function setupBlockListeners(el) {
  if (/** @type {any} */ (el)._editorSetup) return;
  /** @type {any} */ (el)._editorSetup = true;
  el.addEventListener('keydown', /** @type {any} */ (onBlockKeydown));
  el.addEventListener('input', /** @type {any} */ (onBlockInput));
}

/** @param {KeyboardEvent} e */
function onBlockKeydown(e) {
  const block = /** @type {HTMLElement} */ (e.currentTarget);

  // ── Slash menu navigation ──
  if (isSlashOpen()) {
    handleSlashKeydown(e);
    return;
  }

  // ── Escape → block selection mode ──
  if (e.key === 'Escape') {
    e.preventDefault();
    enterSelectionMode(block);
    return;
  }

  // ── Enter → new paragraph below ──
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    const newBlock = makeBlock('paragraph');
    insertAfterEl(newBlock, block);
    focusBlockStart(newBlock);
    return;
  }

  // ── Backspace on empty block → remove ──
  if (e.key === 'Backspace') {
    const isEmpty = block.textContent === '' && !block.innerHTML.replace(/<br\s*\/?>/gi, '').trim();
    if (isEmpty) {
      e.preventDefault();
      const prev = getAdjacentBlock(block, -1);
      block.remove();
      if (prev && prev.contentEditable === 'true') focusBlockEnd(prev);
      return;
    }
  }

  // ── Slash: open command menu ──
  if (e.key === '/') {
    // Let the char be typed first, then open
    setTimeout(() => {
      if (!isSlashOpen()) openSlashMenu(block);
    }, 0);
  }

  // ── Tab → focus next block ──
  if (e.key === 'Tab') {
    e.preventDefault();
    const next = getAdjacentBlock(block, e.shiftKey ? -1 : 1);
    if (next && next.contentEditable === 'true') focusBlockStart(next);
  }
}

/** @param {Event} e */
function onBlockInput(e) {
  const block = /** @type {HTMLElement} */ (e.currentTarget);

  if (isSlashOpen() && getSlashTriggerBlock() === block) {
    refreshSlashMenuQuery(block);
    return;
  }

  checkMarkdown(block);
}

// ── Markdown shortcuts ────────────────────────────────────────────────────────

const MD_PATTERNS = [
  { re: /^# $/, type: 'h1' },
  { re: /^## $/, type: 'h2' },
  { re: /^### $/, type: 'h3' },
  { re: /^> $/, type: 'callout' },
  { re: /^```$/, type: 'code' },
  { re: /^---$/, type: 'divider' },
];

/** @param {HTMLElement} block */
function checkMarkdown(block) {
  if (!canvas) return;
  const text = block.textContent ?? '';

  for (const p of MD_PATTERNS) {
    if (p.re.test(text)) {
      block.textContent = '';

      if (p.type === 'divider') {
        const divider = makeBlock('divider');
        canvas.replaceChild(divider, block);
        const newBlock = makeBlock('paragraph');
        insertAfterEl(newBlock, divider);
        focusBlockStart(newBlock);
      } else {
        block.dataset.blockType = p.type;
        block.dataset.placeholder = PLACEHOLDER_MAP[p.type] || PLACEHOLDER_MAP.paragraph;
      }
      return;
    }
  }
}

/** Wire listeners on every existing `.doc-text` and ensure each has a default block type. */
export function initExistingBlocks() {
  if (!canvas) return;
  canvas.querySelectorAll('.doc-text').forEach((blockNode) => {
    const block = /** @type {HTMLElement} */ (blockNode);
    if (!block.dataset.blockType) block.dataset.blockType = 'paragraph';
    block.dataset.placeholder =
      PLACEHOLDER_MAP[block.dataset.blockType] || PLACEHOLDER_MAP.paragraph;
    setupBlockListeners(block);
  });
}
