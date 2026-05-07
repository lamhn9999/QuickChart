// @ts-check
/**
 * @file Single chip factory used by every filter kind.
 *
 * Replaces the three near-identical functions (appendFilterValue, appendIntFilterValue,
 * appendBoolFilterValue) plus the two inline innerHTML chip constructions in
 * applyIntRangeFilter and applyDtRangeFilter.
 */

const BASE_CLASS =
  'filterValue rounded-xl text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full';

/**
 * Build a filter chip and append it to `container`. Returns the chip so callers can wire extra
 * behaviour (rare). All chip text is set with `textContent` — no XSS surface.
 *
 * @param {object} args
 * @param {Element} args.container
 * @param {string}  args.label                          visible chip text
 * @param {string}  [args.bg]                           CSS background color (default '#E9967A')
 * @param {(e: MouseEvent) => unknown | Promise<unknown>} args.onRemove   called when ✕ clicked
 * @param {(e: MouseEvent) => void} [args.onChipClick]  called when chip body (not ✕) is clicked
 * @returns {HTMLDivElement}
 */
export function appendChip({ container, label, bg = '#E9967A', onRemove, onChipClick }) {
  const chip = document.createElement('div');
  chip.className = BASE_CLASS;
  chip.style.backgroundColor = bg;

  const span = document.createElement('span');
  span.textContent = label;
  span.className = 'truncate min-w-0';

  const removeBtn = document.createElement('button');
  removeBtn.textContent = '✕';
  removeBtn.className = 'ml-1 font-bold hover:cursor-pointer';
  removeBtn.onclick = async (e) => {
    e.stopPropagation();
    chip.remove();
    await onRemove(e);
  };

  chip.appendChild(span);
  chip.appendChild(removeBtn);
  if (onChipClick) chip.onclick = onChipClick;

  container.appendChild(chip);
  return chip;
}
