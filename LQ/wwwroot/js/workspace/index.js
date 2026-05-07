// @ts-check
/**
 * @file Workspace entry point — replaces the old top-level main.js.
 *
 * Loaded by `Views/Workspace/Workspace.cshtml`. Responsibilities:
 *   1. Wire DOM event handlers (header drag, plot drop, panel resize, column highlight,
 *      menu close).
 *   2. Register every named-handler `data-action` Razor uses (replaces the Phase 7 era
 *      `Object.assign(window, …)` shim).
 *
 * No state lives in this file — cross-cutting state comes from `core/state.js`.
 */

import {
  closeAllMenus,
  closeTablePanelMenus,
  POPUP_SELECTORS,
  TRIGGER_SELECTORS,
} from '../core/menus.js';
import { registerActions, wireDelegatedActions } from '../core/actions.js';
import {
  initSparklines,
  closeZoom,
  exportCanvas,
  createTextCard,
  setCanvasSize,
  setCanvasBg,
} from '../chart.js';

import { fieldClicked, headerDragStart, headerDragEnd } from './header-drag.js';
import { wirePlotPanelDropZone, installChartDropOnCard } from './drop-zones.js';
import { wirePanelResizer } from './resizer.js';
import { wireColumnHighlight } from './table.js';
import { gearBtnClicked, toggleFieldTypeMenu, changeFieldType } from './field-types.js';
import { toggleCategoriesMenu, toggleCategory } from './categories.js';

import {
  filterClicked,
  filterInputKey,
  filterIntSpecificKey,
  filterIntRangeKey,
  applyIntRangeFilter,
  applyDtRangeFilter,
  applyBoolFilter,
  partialMatchStrCheckboxClicked,
  excludeClicked,
} from '../filter/index.js';

// ── Action registrations ──────────────────────────────────────────────────────
// `data-action="header:click"` etc. dispatch into these. The matched element is the one
// carrying the attribute; handlers read extra args from sibling `data-*` attributes.

registerActions({
  // ── header drag/click ───────────────────────────────────────────────
  // Skip when the click came from a popup nested in the th OR from a trigger button
  // inside the th (filter/gear/etc.) — otherwise clicking a popup button or a menu
  // trigger also toggles the column's pending state. Same selector lists used by
  // wireColumnHighlight + the outside-click closer.
  'header:click': (e, th) => {
    if (
      e.target instanceof Element &&
      (e.target.closest(POPUP_SELECTORS) || e.target.closest(TRIGGER_SELECTORS))
    )
      return;
    const idx = parseInt(/** @type {HTMLElement} */ (th).dataset.index ?? '');
    if (!Number.isNaN(idx)) fieldClicked(idx);
  },
  'header:dragstart': (e, th) => {
    const idx = parseInt(/** @type {HTMLElement} */ (th).dataset.index ?? '');
    if (!Number.isNaN(idx))
      headerDragStart(/** @type {DragEvent} */ (e), idx, /** @type {HTMLElement} */ (th));
  },
  'header:dragend': (e, th) =>
    headerDragEnd(/** @type {DragEvent} */ (e), /** @type {HTMLElement} */ (th)),

  // ── gear / field-type / category menus ──────────────────────────────
  'gear:open': (e, btn) => gearBtnClicked(e, /** @type {HTMLElement} */ (btn)),
  'field-type:toggle-menu': (_e, btn) => toggleFieldTypeMenu(/** @type {HTMLElement} */ (btn)),
  'field-type:change': (e, btn) => {
    const th = /** @type {HTMLElement | null} */ (btn.closest('th'));
    const fieldIndex = parseInt(th?.dataset.index ?? '');
    const newType = /** @type {HTMLElement} */ (btn).dataset.newType ?? '';
    if (!Number.isNaN(fieldIndex)) changeFieldType(e, fieldIndex, newType);
  },
  'category:toggle-menu': (_e, btn) => toggleCategoriesMenu(/** @type {HTMLElement} */ (btn)),
  'category:toggle': (e, btn) => toggleCategory(e, /** @type {HTMLElement} */ (btn)),

  // ── filter ───────────────────────────────────────────────────────────
  'filter:open': (e, btn) => filterClicked(e, /** @type {HTMLElement} */ (btn)),
  'filter:string-input': (e, input) =>
    filterInputKey(/** @type {KeyboardEvent} */ (e), /** @type {HTMLInputElement} */ (input)),
  'filter:int-specific': (e, input) =>
    filterIntSpecificKey(/** @type {KeyboardEvent} */ (e), /** @type {HTMLInputElement} */ (input)),
  'filter:int-range': (e, input) =>
    filterIntRangeKey(/** @type {KeyboardEvent} */ (e), /** @type {HTMLInputElement} */ (input)),
  'filter:apply-int-range': (e, btn) =>
    applyIntRangeFilter(e, /** @type {HTMLButtonElement} */ (btn)),
  'filter:apply-dt-range': (e, btn) =>
    applyDtRangeFilter(e, /** @type {HTMLButtonElement} */ (btn)),
  'filter:apply-bool': (e, btn) => {
    const val = /** @type {HTMLElement} */ (btn).dataset.boolVal ?? 'true';
    applyBoolFilter(e, /** @type {HTMLButtonElement} */ (btn), val);
  },
  'filter:exclude': (e, checkbox) => {
    const kind = /** @type {HTMLElement} */ (checkbox).dataset.filterKind ?? 'string';
    excludeClicked(
      e,
      /** @type {HTMLInputElement} */ (checkbox),
      /** @type {'string' | 'int' | 'dt' | 'bool'} */ (kind)
    );
  },
  'filter:partial-match': (e, checkbox) =>
    partialMatchStrCheckboxClicked(e, /** @type {HTMLInputElement} */ (checkbox)),

  // ── canvas / zoom ────────────────────────────────────────────────────
  'canvas:add-text': () => createTextCard(),
  'canvas:export': () => exportCanvas(),
  'canvas:set-size': (_e, btn) => {
    const key = /** @type {HTMLElement} */ (btn).dataset.size ?? 'wide';
    setCanvasSize(/** @type {any} */ (key));
  },
  'canvas:set-bg': (_e, input) => {
    const v = /** @type {HTMLInputElement} */ (input).value;
    setCanvasBg(v);
  },
  'zoom:close': () => closeZoom(),
});

wireDelegatedActions();

// chart.js → workspace bridge for "drop header onto existing card"
installChartDropOnCard();

// ── DOM setup ─────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', () => {
  initSparklines();

  const resizer = /** @type {HTMLElement | null} */ (document.getElementById('drag'));
  const left = /** @type {HTMLElement | null} */ (document.getElementById('tablePanel'));
  const table = /** @type {HTMLTableElement | null} */ (document.getElementById('dataTable'));
  const plotPanel = /** @type {HTMLElement | null} */ (document.getElementById('plotPanel'));

  if (table) wireColumnHighlight(table);
  if (plotPanel) wirePlotPanelDropZone(plotPanel);
  if (resizer && left) wirePanelResizer(resizer, left);

  // Outside-click closes every dropdown; table-panel scroll closes table-side dropdowns
  //
  // The closer fires on the document — same listener phase as the delegated action
  // dispatcher (also document, bubble phase). `event.stopPropagation()` in an action
  // handler can't stop a peer document-level listener (only a different element's), so
  // we'd close the menu we just opened. Instead: explicitly skip when the click is
  // inside any open popup or on a known trigger button (selectors live in core/menus.js
  // and are shared with wireColumnHighlight + the 'header:click' guard).
  document.addEventListener('click', (e) => {
    const t = e.target;
    if (!(t instanceof Element)) return;
    if (t.closest(POPUP_SELECTORS) || t.closest(TRIGGER_SELECTORS)) return;
    closeAllMenus();
  });
  if (left) left.addEventListener('scroll', () => closeTablePanelMenus());
});
