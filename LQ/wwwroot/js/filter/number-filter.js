// @ts-check
/**
 * @file Int + Double filter — specific values (Enter to add chip) and inclusive ranges.
 */

import { enumMap, getFilter, setFilter, removeFilter } from './state.js';
import { executeFilterRequest } from './api.js';
import { appendChip } from './chip.js';
import { updateFilterChipUI } from './popup.js';

const NUMBER_CHIP_BG = '#E9967A';
const INT_MIN = -2147483648;
const INT_MAX = 2147483647;
const DBL_MIN = -1.7976931348623157e308;
const DBL_MAX = 1.7976931348623157e308;

// ── Specific-value path ──────────────────────────────────────────────────────

/**
 * Enter inside the "specific value" input → add a chip.
 * @param {KeyboardEvent} event
 * @param {HTMLInputElement} input
 */
export async function filterIntSpecificKey(event, input) {
  if (event.key !== 'Enter') return;
  event.preventDefault();
  const value = input.value;
  if (value.trim() === '') return;

  const th = input.closest('th');
  if (!th) return;
  const index = /** @type {any} */ (th).dataset.index;
  const popup = /** @type {HTMLElement} */ (input.closest('.filterPopup'));
  const isNot = /** @type {HTMLInputElement} */ (popup.querySelector('input[name="IsNot"]'))
    .checked;

  await sendIntSpecificFilterRequest(value, isNot, index, input);
}

/**
 * Mutates filter state for a specific-value addition, draws a chip, re-executes.
 * @param {string} rawVal
 * @param {boolean} isNot
 * @param {string | number} index
 * @param {HTMLInputElement} input
 */
async function sendIntSpecificFilterRequest(rawVal, isNot, index, input) {
  const popup = /** @type {HTMLElement} */ (input.closest('.filterPopup'));
  const fieldType = enumMap[/** @type {any} */ (popup).dataset.fieldType];

  let f = getFilter(index);
  if (!f) {
    f = {
      fieldIndex: parseInt(/** @type {any} */ (index)),
      fieldType,
      filteringValuesInt: [],
      filteringValuesDouble: [],
      intBound: null,
      doubleBound: null,
      isNot,
    };
    setFilter(index, f);
  }

  // Specific-value path overrides any prior range bounds
  f.intBound = null;
  f.doubleBound = null;
  f.fieldType = fieldType;

  let displayVal = null;
  if (fieldType === 1) {
    // Int
    const v = parseInt(rawVal);
    if (!isNaN(v)) {
      if (!f.filteringValuesInt) f.filteringValuesInt = [];
      if (!f.filteringValuesInt.includes(v)) {
        f.filteringValuesInt.push(v);
        displayVal = v;
      }
    }
  } else {
    // Double
    const v = parseFloat(rawVal);
    if (!isNaN(v)) {
      if (!f.filteringValuesDouble) f.filteringValuesDouble = [];
      if (!f.filteringValuesDouble.includes(v)) {
        f.filteringValuesDouble.push(v);
        displayVal = v;
      }
    }
  }

  f.isNot = isNot;
  updateFilterChipUI(index);
  if (displayVal !== null) appendNumberSpecificChip(input, index, displayVal);

  await executeFilterRequest();
}

/**
 * @param {HTMLInputElement} input
 * @param {string | number} index
 * @param {number} value
 */
function appendNumberSpecificChip(input, index, value) {
  const container = /** @type {Element} */ (input.previousElementSibling);

  // Specific-value chips coexist with each other but replace any range chip
  Array.from(container.children).forEach((child) => {
    if (child.textContent?.includes(' to ')) child.remove();
  });

  appendChip({
    container,
    label: String(value),
    bg: NUMBER_CHIP_BG,
    onRemove: async () => {
      const f = getFilter(index);
      if (f) {
        f.filteringValuesInt = (f.filteringValuesInt || []).filter((v) => v !== value);
        f.filteringValuesDouble = (f.filteringValuesDouble || []).filter((v) => v !== value);
        const noInts = !f.filteringValuesInt || f.filteringValuesInt.length === 0;
        const noDbls = !f.filteringValuesDouble || f.filteringValuesDouble.length === 0;
        if (noInts && noDbls && !f.intBound && !f.doubleBound) removeFilter(index);
      }
      const popup = /** @type {HTMLElement | null} */ (input.closest('.filterPopup'));
      if (popup) {
        const isNot = /** @type {HTMLInputElement} */ (popup.querySelector('input[name="IsNot"]'))
          .checked;
        const cur = getFilter(index);
        if (cur) cur.isNot = isNot;
      }
      updateFilterChipUI(index);
      await executeFilterRequest();
    },
  });

  input.value = '';
}

// ── Range path ───────────────────────────────────────────────────────────────

/**
 * Enter inside either range input → submit the range.
 * @param {KeyboardEvent} event
 * @param {HTMLInputElement} input
 */
export async function filterIntRangeKey(event, input) {
  if (event.key !== 'Enter') return;
  event.preventDefault();
  const popup = /** @type {HTMLElement} */ (input.closest('.filterPopup'));
  const btn = /** @type {HTMLButtonElement} */ (popup.querySelector('button'));
  await applyIntRangeFilter(event, btn);
}

/**
 * Apply min/max range bounds. Replaces any existing range chip.
 * @param {Event} event
 * @param {HTMLButtonElement} btn
 */
export async function applyIntRangeFilter(event, btn) {
  event.stopPropagation();
  const th = btn.closest('th');
  if (!th) return;
  const index = /** @type {any} */ (th).dataset.index;
  const popup = /** @type {HTMLElement} */ (btn.closest('.filterPopup'));
  const fieldType = enumMap[/** @type {any} */ (popup).dataset.fieldType];

  const minInput = /** @type {HTMLInputElement} */ (popup.querySelector('.int-range-min')).value;
  const maxInput = /** @type {HTMLInputElement} */ (popup.querySelector('.int-range-max')).value;
  const isNot = /** @type {HTMLInputElement} */ (popup.querySelector('input[name="IsNot"]'))
    .checked;

  if (minInput === '' && maxInput === '') return;

  let f = getFilter(index);
  if (!f) {
    f = {
      fieldIndex: parseInt(/** @type {any} */ (index)),
      fieldType,
      filteringValuesInt: null,
      filteringValuesDouble: null,
      isNot,
    };
    setFilter(index, f);
  }

  f.fieldType = fieldType;
  f.filteringValuesInt = null;
  f.filteringValuesDouble = null;

  let minVal, maxVal;
  if (fieldType === 1) {
    // Int
    minVal = minInput === '' ? INT_MIN : parseInt(minInput);
    maxVal = maxInput === '' ? INT_MAX : parseInt(maxInput);
    f.intBound = { leftBound: minVal, rightBound: maxVal };
  } else {
    // Double
    minVal = minInput === '' ? DBL_MIN : parseFloat(minInput);
    maxVal = maxInput === '' ? DBL_MAX : parseFloat(maxInput);
    f.doubleBound = { leftBound: minVal, rightBound: maxVal };
  }
  f.isNot = isNot;

  updateFilterChipUI(index);

  const container = /** @type {Element} */ (popup.querySelector('.filterValues'));
  container.innerHTML = '';
  const label = `${minInput === '' ? '-inf' : minVal} to ${maxInput === '' ? '+inf' : maxVal}`;

  appendChip({
    container,
    label,
    bg: NUMBER_CHIP_BG,
    onRemove: async () => {
      const cur = getFilter(index);
      if (cur) {
        cur.intBound = null;
        cur.doubleBound = null;
      }
      const noInts = !cur?.filteringValuesInt || cur.filteringValuesInt.length === 0;
      const noDbls = !cur?.filteringValuesDouble || cur.filteringValuesDouble.length === 0;
      if (!cur || (noInts && noDbls)) removeFilter(index);
      /** @type {HTMLInputElement} */ (popup.querySelector('.int-range-min')).value = '';
      /** @type {HTMLInputElement} */ (popup.querySelector('.int-range-max')).value = '';
      updateFilterChipUI(index);
      await executeFilterRequest();
    },
  });

  await executeFilterRequest();
}
