// @ts-check
/**
 * @file Single source of truth for the workspace's dropdown / popover "kinds".
 *
 * Before Phase 1 the same forEach-hidden + forEach-rotate-180 loop was duplicated
 * in main.js (twice — click + scroll), workspace.js (4 places), and was about to
 * grow further. This module collapses all of it.
 *
 * Each kind: a CSS selector for the popup elements, plus an optional selector for
 * the chevron `<svg>` inside the trigger button (which gets a `rotate-180` class
 * while the popup is open).
 */

/**
 * @typedef {Object} MenuKind
 * @property {string} popup    — selector for the popup element(s)
 * @property {string | null} chevron — selector for the chevron svg(s); `null` if no chevron
 */

/** @type {Record<string, MenuKind>} */
export const MENU_KINDS = {
  filter: { popup: '.filterPopup', chevron: null },
  gear: { popup: '.gearPopup', chevron: '.gear-btn svg' },
  fieldType: { popup: '.type-menu-popup', chevron: '.type-menu-btn svg' },
  category: { popup: '.cat-menu-popup', chevron: '.cat-menu-btn svg' },
  plot: { popup: '.plot-menu, .plot-colormap', chevron: '.plot-btn svg, .plot-bgbtn svg' },
};

const ALL_KINDS = /** @type {const} */ (['filter', 'gear', 'fieldType', 'category', 'plot']);

/** Menus on the table panel (left side). Excludes `plot` which lives on the right. */
const TABLE_KINDS = /** @type {const} */ (['filter', 'gear', 'fieldType', 'category']);

/**
 * Comma-separated CSS selector list of every popup type. Use with `closest()` to detect
 * "is this click target inside a popup?" — used by:
 *   - the outside-click closer in `workspace/index.js` (skip-don't-close on inside clicks)
 *   - `wireColumnHighlight` in `workspace/table.js` (skip column-toggle on popup clicks)
 *   - the `'header:click'` action handler (skip pendingIndexes-toggle on popup clicks)
 *
 * Single source of truth: derived from `MENU_KINDS` at module load so adding a new menu
 * kind automatically extends every guard that uses this constant.
 */
export const POPUP_SELECTORS = Object.values(MENU_KINDS)
  .map((k) => k.popup)
  .join(', ');

/**
 * Comma-separated CSS selector list of every menu trigger button. Use with `closest()`
 * to detect "is this click on a button that opens a popup?" — used by the same three
 * sites as `POPUP_SELECTORS` so trigger clicks don't bleed into column-highlight or
 * header-pending side-effects.
 *
 * Hard-coded (not derived) because trigger classes are spread across views; if a new
 * trigger is added, append it here.
 */
export const TRIGGER_SELECTORS =
  '.filter-btn, .gear-btn, .type-menu-btn, .cat-menu-btn, .plot-btn, .plot-bgbtn';

/**
 * Hide popups of the given kinds and reset their chevrons.
 * @param {readonly string[]} kinds
 * @param {ParentNode} [scope] root to search within (default: document)
 */
export function closeMenus(kinds, scope = document) {
  for (const k of kinds) {
    const def = MENU_KINDS[k];
    if (!def) continue;
    scope.querySelectorAll(def.popup).forEach((el) => el.classList.add('hidden'));
    if (def.chevron) {
      scope.querySelectorAll(def.chevron).forEach((s) => s.classList.remove('rotate-180'));
    }
  }
}

/**
 * Close every kind. Called from the document-level outside-click handler.
 * @param {ParentNode} [scope]
 */
export function closeAllMenus(scope) {
  closeMenus(ALL_KINDS, scope);
}

/**
 * Close just the table-panel kinds. Called when the table panel scrolls.
 * @param {ParentNode} [scope]
 */
export function closeTablePanelMenus(scope) {
  closeMenus(TABLE_KINDS, scope);
}
