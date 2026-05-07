// @ts-check
/**
 * @file String + ListString filter — text-input chips with optional partial-match.
 */

import { getFilter, removeFilter } from './state.js';
import { sendFilterRequest } from './api.js';
import { appendChip } from './chip.js';

const STRING_CHIP_BG = 'darksalmon';

/**
 * Enter-key inside a string filter's text input → submit the value.
 * @param {KeyboardEvent} event
 * @param {HTMLInputElement} input
 */
export async function filterInputKey(event, input) {
  if (event.key !== 'Enter') return;
  event.preventDefault();

  const value = input.value;
  const th = input.closest('th');
  if (!th) return;
  const index = /** @type {any} */ (th).dataset.index;
  const popup = /** @type {HTMLElement | null} */ (input.closest('.filterPopup'));
  if (!popup) return;
  const isNot =
    /** @type {HTMLInputElement | null} */ (popup.querySelector('input[name="IsNot"]'))?.checked ||
    false;

  await sendFilterRequest(value.trim(), isNot, index, popup);
  appendStringChip(input, index);
}

/**
 * Add a chip for the input's current value (if non-empty and unique).
 * @param {HTMLInputElement} input
 * @param {string | number} index
 */
function appendStringChip(input, index) {
  const container = /** @type {Element} */ (input.previousElementSibling); // chip container above
  const value = input.value.trim();
  if (!value) return;

  // Dedup by visible text
  if (
    Array.from(container.children).some((c) => c.textContent?.replace('✕', '').trim() === value)
  ) {
    input.value = '';
    return;
  }

  const popup = /** @type {HTMLElement} */ (input.closest('.filterPopup'));

  appendChip({
    container,
    label: value,
    bg: STRING_CHIP_BG,
    onChipClick: () => {
      input.value = value;
      input.focus();
    },
    onRemove: async () => {
      const f = getFilter(index);
      if (f && f.filteringValues) {
        f.filteringValues = f.filteringValues.filter((v) => v !== value);
        if (f.filteringValues.length === 0) removeFilter(index);
      }
      const isNot =
        /** @type {HTMLInputElement | null} */ (popup.querySelector('input[name="IsNot"]'))
          ?.checked || false;
      await sendFilterRequest('', isNot, index, popup);
    },
  });

  input.value = '';
}
