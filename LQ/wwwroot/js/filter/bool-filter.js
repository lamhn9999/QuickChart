// @ts-check
/**
 * @file Bool filter — toggle a single true/false chip.
 */

import { enumMap, getFilter, setFilter, removeFilter } from './state.js';
import { executeFilterRequest } from './api.js';
import { appendChip } from './chip.js';
import { updateFilterChipUI } from './popup.js';

const BOOL_CHIP_BG = '#E9967A';

/**
 * Click "True" or "False" → set the filter to that single value.
 * @param {Event} event
 * @param {HTMLButtonElement} btn
 * @param {boolean | string} val
 */
export async function applyBoolFilter(event, btn, val) {
  event.stopPropagation();
  const th = btn.closest('th');
  if (!th) return;
  const index = /** @type {any} */ (th).dataset.index;
  const popup = /** @type {HTMLElement} */ (btn.closest('.filterPopup'));
  const fieldType = enumMap[/** @type {any} */ (popup).dataset.fieldType];
  const isNot =
    /** @type {HTMLInputElement | null} */ (popup.querySelector('input[name="IsNot"]'))?.checked ||
    false;

  let f = getFilter(index);
  if (!f) {
    f = {
      fieldIndex: parseInt(/** @type {any} */ (index)),
      fieldType,
      filteringValues: [],
      isNot,
    };
    setFilter(index, f);
  }
  f.fieldType = fieldType;
  // Strict toggle — overwrite to single value
  f.filteringValues = [/** @type {any} */ (val)];
  f.isNot = isNot;

  const container = /** @type {Element} */ (popup.querySelector('.filterValues'));
  container.innerHTML = '';
  appendBoolChip(container, index, val);

  updateFilterChipUI(index);
  await executeFilterRequest();
}

/**
 * @param {Element} container
 * @param {string | number} index
 * @param {boolean | string} value
 */
function appendBoolChip(container, index, value) {
  appendChip({
    container,
    label: String(value),
    bg: BOOL_CHIP_BG,
    onRemove: async () => {
      const f = getFilter(index);
      if (f && f.filteringValues) {
        f.filteringValues = f.filteringValues.filter((v) => v !== /** @type {any} */ (value));
        if (f.filteringValues.length === 0) removeFilter(index);
      }
      updateFilterChipUI(index);
      await executeFilterRequest();
    },
  });
}
