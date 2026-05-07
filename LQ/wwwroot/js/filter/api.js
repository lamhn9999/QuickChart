// @ts-check
/**
 * @file Filter network + cross-kind action handlers.
 *
 * - `executeFilterRequest()` — POSTs all active filters and updates the table.
 *   Owns `executingFilterController` to abort in-flight requests when a new one starts.
 * - `sendFilterRequest()` — string-filter helper used by filterInputKey + the string excludeClicked.
 * - `excludeClicked(event, checkbox, kind)` — single dispatcher replacing the four near-identical
 *   `excludeXCheckboxClicked` handlers.
 * - `partialMatchStrCheckboxClicked` — string-only IsPartialMatch toggle.
 */

import { appState } from '../core/state.js';
import { updateTable } from '../workspace/table.js';
import { enumMap, getFilter, removeFilter, getActiveFilters, setFilter } from './state.js';
import { updateFilterChipUI } from './popup.js';

/** @type {AbortController | null} */
let executingFilterController = null;

/**
 * Send the current `getActiveFilters()` list to /workspace/filter. Cancels any in-flight request.
 */
export async function executeFilterRequest() {
  if (executingFilterController) executingFilterController.abort();
  executingFilterController = new AbortController();
  appState.controller = executingFilterController;
  const activeFilters = getActiveFilters();
  console.log(`[executeFilterRequest] Sending ${activeFilters.length} active filter(s).`);
  try {
    const response = await fetch('/workspace/filter', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(activeFilters),
      signal: executingFilterController.signal,
    });
    const result = await response.json();
    updateTable(result);
  } catch (e) {
    if (!(e instanceof Error) || e.name !== 'AbortError')
      console.error('[executeFilterRequest] Fetch error:', e);
  }
}

/**
 * String-filter mutation path: add a value (or just toggle isNot/isPartialMatch when value is '').
 * Re-execs unless the popup carries the `executing` class (caller is batching).
 *
 * @param {string} value
 * @param {boolean} isNotCheckboxValue
 * @param {string | number} index
 * @param {Element} popup
 */
export async function sendFilterRequest(value, isNotCheckboxValue, index, popup) {
  const fieldType = enumMap[/** @type {any} */ (popup).dataset.fieldType] || 0;
  const isPartialMatch =
    /** @type {HTMLInputElement | null} */ (popup.querySelector('input[name="IsPartialMatch"]'))
      ?.checked || false;

  const existing = getFilter(index);

  if (value !== '') {
    let f = existing;
    if (!f) {
      f = {
        fieldIndex: parseInt(/** @type {any} */ (index)),
        fieldType,
        filteringValues: [],
        isPartialMatch,
      };
      setFilter(index, f);
    } else {
      f.fieldType = fieldType;
      f.isPartialMatch = isPartialMatch;
    }

    if (!f.filteringValues) f.filteringValues = [];
    if (!f.filteringValues.includes(value)) f.filteringValues.push(value);
    f.isNot = isNotCheckboxValue;
  } else if (existing) {
    existing.isNot = isNotCheckboxValue;
    existing.isPartialMatch = isPartialMatch;
    existing.fieldType = fieldType;
    if (!existing.filteringValues || existing.filteringValues.length === 0) {
      removeFilter(index);
    }
  }

  updateFilterChipUI(index);
  if (!popup.classList.contains('executing')) await executeFilterRequest();
}

/**
 * Single dispatcher for the IsNot exclude checkbox across all four filter kinds.
 *
 * - `string`: re-runs sendFilterRequest('') so the cleanup-empty-filter path runs (and
 *   isPartialMatch is re-read from the popup).
 * - `int`/`dt`/`bool`: trivial isNot toggle on the existing filter; no-op if none exists.
 *
 * @param {Event} event
 * @param {HTMLInputElement} checkbox
 * @param {'string' | 'int' | 'dt' | 'bool'} kind
 */
export async function excludeClicked(event, checkbox, kind) {
  event.stopPropagation();
  const th = checkbox.closest('th');
  if (!th) return;
  const index = /** @type {any} */ (th).dataset.index;

  if (kind === 'string') {
    const popup = checkbox.closest('.filterPopup');
    if (!popup) return;
    await sendFilterRequest('', checkbox.checked, index, popup);
    return;
  }

  // int / dt / bool — trivial isNot toggle
  const f = getFilter(index);
  if (f) {
    f.isNot = checkbox.checked;
    await executeFilterRequest();
  }
}

/**
 * String-only: toggle IsPartialMatch on an existing filter and re-execute.
 * @param {Event} event
 * @param {HTMLInputElement} checkbox
 */
export async function partialMatchStrCheckboxClicked(event, checkbox) {
  event.stopPropagation();
  const th = checkbox.closest('th');
  if (!th) return;
  const index = /** @type {any} */ (th).dataset.index;
  const f = getFilter(index);
  if (f) {
    f.isPartialMatch = checkbox.checked;
    await executeFilterRequest();
  }
}
