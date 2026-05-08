// @ts-check
/**
 * @file Per-column gear menu + change-field-type submenu.
 *
 * - `gearBtnClicked` toggles the gear popup, closing other dropdowns first.
 * - `toggleFieldTypeMenu` toggles the type submenu inside the gear popup.
 * - `changeFieldType` POSTs the new type, then optimistically updates the type label and
 *   rewrites the submenu's button list to exclude the now-current type.
 */

import { closeMenus } from '../core/menus.js';
import { positionDropdown } from '../core/dom.js';
import { showToast } from '../core/toast.js';

/**
 * Type-key (lowercase) → C# AssemblyQualifiedName-prefix used by `Field.TypeName`.
 * @type {Record<string, string>}
 */
const TYPE_LABEL = {
  string: 'String',
  int: 'Int32',
  double: 'Double',
  datetime: 'DateTime',
  bool: 'Boolean',
  list: 'List`1',
};

/**
 * @param {Event} event
 * @param {HTMLElement} button
 */
export function gearBtnClicked(event, button) {
  event.stopPropagation();

  const popup = /** @type {HTMLElement | null} */ (
    button.parentElement?.querySelector('.gearPopup')
  );
  if (!popup) return;
  const wasOpen = !popup.classList.contains('hidden');

  closeMenus(['filter', 'gear']);

  if (!wasOpen) {
    positionDropdown(popup, button);
    popup.classList.remove('hidden');
    button.querySelector('svg')?.classList.add('rotate-180');
    console.log(`[gearBtnClicked] Opened gear popup`);
  }
}

/**
 * @param {HTMLElement} btn
 */
export function toggleFieldTypeMenu(btn) {
  const popup = /** @type {HTMLElement | null} */ (btn.nextElementSibling);
  if (!popup) return;
  const isHidden = popup.classList.contains('hidden');

  closeMenus(['fieldType', 'category']);

  if (isHidden) {
    popup.classList.remove('hidden');
    btn.querySelector('svg')?.classList.add('rotate-180');
  }
}

/**
 * POST to /workspace/changeFieldType, then update the type-label cell and rewrite the submenu.
 * @param {Event} event
 * @param {number} fieldIndex
 * @param {string} newType  one of: 'string' | 'int' | 'double' | 'datetime' | 'bool' | 'list'
 */
export async function changeFieldType(event, fieldIndex, newType) {
  event.stopPropagation();

  closeMenus(['gear']);

  try {
    const formData = new FormData();
    formData.append('fieldIndex', String(fieldIndex));
    formData.append('newType', newType);

    const response = await fetch('/workspace/changeFieldType', {
      method: 'POST',
      body: formData,
    });
    const result = await response.json();

    if (result === 'Field updated') {
      const span = document.getElementById(`fieldType-${fieldIndex}`);
      if (span) {
        span.textContent = TYPE_LABEL[newType] || newType;
        const header = span.closest('th');
        const buttonsContainer = header?.querySelector('.type-menu-popup');
        if (buttonsContainer) {
          buttonsContainer.innerHTML = Object.keys(TYPE_LABEL)
            .filter((t) => t !== newType)
            .map(
              (t) =>
                `<button onclick="changeFieldType(event, ${fieldIndex}, '${t}')" class="text-left px-3 py-1.5 text-xs text-zinc-300 hover:text-white hover:bg-white/10 rounded-lg transition-colors w-full tracking-wide">${t}</button>`
            )
            .join('');
        }
      }
      showToast(`Field type successfully changed to ${newType}.`);
      console.log(`[changeFieldType] Field ${fieldIndex} → ${newType}`);
    } else {
      showToast('Failed to update field type.', true);
      console.warn(`[changeFieldType] Server rejected update for field ${fieldIndex}`);
    }
  } catch (e) {
    showToast('Error updating field type.', true);
    console.error('[changeFieldType] Fetch error:', e);
  }
}
