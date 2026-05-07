// @ts-check
/**
 * @file Filter popup open/close + chip-count badge.
 *
 * - `filterClicked` — toggles a filter popup; positioned with positionDropdown to escape overflow.
 * - `updateFilterChipUI` — refreshes the small numeric chip on the column header.
 *
 * Phase 11 removed `filterPopupClicked`. The function used to fire on every click inside
 * a filter popup and re-focus the first input; under delegation that stole focus from
 * inputs/buttons/checkboxes the user actually clicked. Browser default focus handling +
 * the popup-open path (which already focuses the first input) cover everything it
 * legitimately did. The "popup stays open on inside click" job is handled by the
 * outside-click closer in `workspace/index.js`, which skips clicks matching
 * `closest(POPUP_SELECTORS)`.
 */

import { positionDropdown } from '../core/dom.js';
import { closeMenus } from '../core/menus.js';
import { getFilter } from './state.js';

/**
 * @param {Event} _event
 * @param {HTMLElement} filterbtn  the filter trigger button
 */
export function filterClicked(_event, filterbtn) {
  // Note: don't call `event.stopPropagation()` here — the delegated dispatcher and the
  // outside-click closer are both on `document`, and stopPropagation can't separate
  // peer listeners on the same element. The closer in workspace/index.js skips clicks
  // on `.filter-btn` and inside `.filterPopup` instead.

  const popup = /** @type {HTMLElement} */ (filterbtn.parentElement?.querySelector('.filterPopup'));
  if (!popup) return;
  const wasOpen = !popup.classList.contains('hidden');

  // Close any other open dropdowns first
  closeMenus(['filter', 'gear']);

  if (!wasOpen) {
    // Position using fixed coords before revealing — escapes overflow:auto clipping
    positionDropdown(popup, filterbtn);
    popup.classList.remove('hidden');
    console.log(
      `[filterClicked] Opened filter popup for field index=${filterbtn.closest('th')?.['dataset']?.index}`
    );
  }

  // Not all filter variants have a text input (e.g. Bool only has buttons)
  /** @type {HTMLInputElement | null} */ (popup.querySelector('input'))?.focus();
}

/**
 * Refresh the small numeric badge on the column header that shows how many active values
 * the column's filter has.
 * @param {string | number} index
 */
export function updateFilterChipUI(index) {
  const th = document.querySelector(`th[data-index="${index}"]`);
  if (!th) return;
  const filter = getFilter(index);
  const chip = th.querySelector('.filter-chip');
  if (!chip) return;

  let len = 0;
  if (filter) {
    if (filter.filteringValues) len += filter.filteringValues.length;
    if (filter.filteringValuesInt) len += filter.filteringValuesInt.length;
    if (filter.filteringValuesDouble) len += filter.filteringValuesDouble.length;
    if (filter.intBound) len++;
    if (filter.doubleBound) len++;
    if (filter.dateTimeBound) len++;
  }

  if (len > 0) {
    chip.textContent = String(len);
    chip.classList.remove('hidden');
  } else {
    chip.classList.add('hidden');
  }
}
