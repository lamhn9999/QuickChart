// @ts-check
/**
 * @file Public re-exports for the filter package.
 *
 * The four legacy `excludeXCheckboxClicked` names are kept as thin wrappers around the
 * unified `excludeClicked(event, checkbox, kind)` so main.js's window.* shims and Razor
 * inline `onclick=` handlers keep working without churn.
 *
 * `excludeBoolCheckboxClicked` is currently unused (the Bool filter popup has no Exclude
 * checkbox) but kept exported for symmetry / future use.
 */

import { excludeClicked } from './api.js';

// Popup open/close + chip badge
export { filterClicked } from './popup.js';

// Per-kind handlers
export { filterInputKey } from './string-filter.js';
export { filterIntSpecificKey, filterIntRangeKey, applyIntRangeFilter } from './number-filter.js';
export { applyDtRangeFilter } from './date-filter.js';
export { applyBoolFilter } from './bool-filter.js';

// Cross-kind action handlers
export { excludeClicked, partialMatchStrCheckboxClicked } from './api.js';

// Historically-public state primitive
export { enumMap } from './state.js';

// Legacy exclude handlers — thin dispatchers to the single excludeClicked
/** @param {Event} e @param {HTMLInputElement} cb */
export const excludeCheckboxClicked = (e, cb) => excludeClicked(e, cb, 'string');
/** @param {Event} e @param {HTMLInputElement} cb */
export const excludeIntCheckboxClicked = (e, cb) => excludeClicked(e, cb, 'int');
/** @param {Event} e @param {HTMLInputElement} cb */
export const excludeDtCheckboxClicked = (e, cb) => excludeClicked(e, cb, 'dt');
/** @param {Event} e @param {HTMLInputElement} cb */
export const excludeBoolCheckboxClicked = (e, cb) => excludeClicked(e, cb, 'bool');
