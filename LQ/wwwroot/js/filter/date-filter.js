// @ts-check
/**
 * @file DateTime range filter.
 */

import { enumMap, getFilter, setFilter, removeFilter } from './state.js';
import { executeFilterRequest } from './api.js';
import { appendChip } from './chip.js';
import { updateFilterChipUI } from './popup.js';

const DATE_CHIP_BG = '#E9967A';
const DT_MIN = '0001-01-01T00:00:00';
const DT_MAX = '9999-12-31T23:59:59';

/**
 * Apply min/max date bounds. Replaces any existing chip.
 * @param {Event} event
 * @param {HTMLButtonElement} btn
 */
export async function applyDtRangeFilter(event, btn) {
  event.stopPropagation();
  const th = btn.closest('th');
  if (!th) return;
  const index = /** @type {any} */ (th).dataset.index;
  const popup = /** @type {HTMLElement} */ (btn.closest('.filterPopup'));
  const fieldType = enumMap[/** @type {any} */ (popup).dataset.fieldType];

  const minInput = /** @type {HTMLInputElement} */ (popup.querySelector('.dt-range-min')).value;
  const maxInput = /** @type {HTMLInputElement} */ (popup.querySelector('.dt-range-max')).value;
  const isNot = /** @type {HTMLInputElement} */ (popup.querySelector('input[name="IsNot"]'))
    .checked;

  if (minInput === '' && maxInput === '') return;

  const minDt = minInput === '' ? DT_MIN : minInput;
  const maxDt = maxInput === '' ? DT_MAX : maxInput;

  let f = getFilter(index);
  if (!f) {
    f = {
      fieldIndex: parseInt(/** @type {any} */ (index)),
      fieldType,
      isNot,
      dateTimeBound: null,
    };
    setFilter(index, f);
  }

  f.fieldType = fieldType;
  f.dateTimeBound = { leftBound: minDt, rightBound: maxDt };
  f.isNot = isNot;

  updateFilterChipUI(index);

  const container = /** @type {Element} */ (popup.querySelector('.filterValues'));
  container.innerHTML = '';
  const minDpy = minInput === '' ? '-inf' : new Date(minInput).toLocaleString();
  const maxDpy = maxInput === '' ? '+inf' : new Date(maxInput).toLocaleString();
  // The original used a <br> between dates; preserve the visual line break
  const label = `${minDpy} to \n${maxDpy}`;

  const chip = appendChip({
    container,
    label,
    bg: DATE_CHIP_BG,
    onRemove: async () => {
      removeFilter(index);
      /** @type {HTMLInputElement} */ (popup.querySelector('.dt-range-min')).value = '';
      /** @type {HTMLInputElement} */ (popup.querySelector('.dt-range-max')).value = '';
      updateFilterChipUI(index);
      await executeFilterRequest();
    },
  });
  // Allow the explicit \n in label to wrap visually like the original <br>
  /** @type {HTMLElement} */ (chip.querySelector('span')).style.whiteSpace = 'pre-line';

  await executeFilterRequest();
}
