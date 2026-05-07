// @ts-check
/**
 * @file Per-column "Categories" submenu — toggle which list-of-string categories are active.
 *
 * Optimistically flips the button state, then POSTs the full active-categories list. Reverts
 * the button if the server rejects.
 */

import { closeMenus } from '../core/menus.js';
import { showToast } from '../core/toast.js';

/**
 * @param {HTMLElement} btn
 */
export function toggleCategoriesMenu(btn) {
  const popup = /** @type {HTMLElement | null} */ (btn.nextElementSibling);
  if (!popup) return;
  const isHidden = popup.classList.contains('hidden');

  closeMenus(['category', 'fieldType']);

  if (isHidden) {
    popup.classList.remove('hidden');
    btn.querySelector('svg')?.classList.add('rotate-180');
  }
}

/**
 * @param {HTMLElement} btn
 * @param {boolean} active
 */
function setCategoryButtonActive(btn, active) {
  btn.dataset.catActive = active ? 'true' : 'false';
  btn.classList.toggle('bg-[#E9967A]/10', active);
  btn.classList.toggle('text-[#18181B]', active);
  btn.classList.toggle('text-zinc-600', !active);
  btn.classList.toggle('hover:bg-zinc-100', !active);
  btn.classList.toggle('hover:text-[#18181B]', !active);
  btn.querySelector('.cat-check')?.classList.toggle('invisible', !active);
}

/**
 * @param {Event} event
 * @param {HTMLElement} btn
 */
export async function toggleCategory(event, btn) {
  event.stopPropagation();

  const popup = /** @type {HTMLElement | null} */ (btn.closest('.cat-menu-popup'));
  if (!popup) return;
  const fieldIndex = parseInt(popup.dataset.fieldIndex || '', 10);
  const wasActive = btn.dataset.catActive === 'true';

  // Optimistic flip
  setCategoryButtonActive(btn, !wasActive);

  const categories = Array.from(popup.querySelectorAll('.cat-toggle-btn'))
    .filter((b) => /** @type {HTMLElement} */ (b).dataset.catActive === 'true')
    .map((b) => parseInt(/** @type {HTMLElement} */ (b).dataset.catValue || '', 10));

  try {
    const response = await fetch('/workspace/changeCategories', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fieldIndex, categories }),
    });
    const result = await response.json();
    if (result !== 'Categories updated') throw new Error(result);

    const sparkDiv = /** @type {HTMLElement | null} */ (
      document.querySelector(`[data-spark-col="${fieldIndex}"]`)
    );
    if (sparkDiv) {
      sparkDiv.dataset.sparkCategorical = categories.includes(0) ? 'true' : 'false';
    }
    showToast('Categories updated.');
  } catch (e) {
    setCategoryButtonActive(btn, wasActive);
    showToast('Failed to update categories.', true);
    console.error('[toggleCategory]', e);
  }
}
