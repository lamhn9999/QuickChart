// @ts-check
/**
 * @file Action handlers wired into the existing workspace `registerActions`
 * delegation table. Each export here matches a `data-action` value emitted
 * by render.js or the right-panel markup in Workspace.cshtml.
 *
 * Side-effect contract: handlers mutate the FilterRecord, refresh the DOM
 * fragment that changed, then call replayFiltersFor() with the affected
 * chart group ids. They never write to the registry directly except through
 * state.js setters.
 */

import { getFilter, getSelectedFilterId, setSelectedFilterId } from './state.js';
import { replayFiltersFor, removeFilterCard } from './convert.js';
import { setModeLabel, buildChip } from './render.js';
import { renderApplyToList } from './selection.js';

/**
 * Pill click in the buttons variant. Plain click = single-select (replaces
 * everything). Ctrl/Cmd-click = toggle into the multi-select. Re-clicking
 * an already-active pill deselects it. After the mutation, fire the inline
 * filter through every chart in appliedTo.
 *
 * @param {MouseEvent} e
 * @param {HTMLElement} btn
 */
export async function onPillClick(e, btn) {
  const card = /** @type {HTMLElement | null} */ (btn.closest('.canvas-card'));
  if (!card) return;
  const gid = Number(card.dataset.groupId);
  const rec = getFilter(gid);
  if (!rec || rec.variant !== 'buttons') return;

  // Selecting on a non-selected filter card also brings it into selection
  // so the right panel reflects what the user is editing.
  if (getSelectedFilterId() !== gid) setSelectedFilterId(gid);

  const value = btn.dataset.value ?? '';
  const isMulti = e.ctrlKey || e.metaKey;
  const isActive = rec.selectedValues.has(value);

  if (isMulti) {
    if (isActive) rec.selectedValues.delete(value);
    else rec.selectedValues.add(value);
  } else {
    if (isActive && rec.selectedValues.size === 1) {
      rec.selectedValues.clear();
    } else {
      rec.selectedValues.clear();
      rec.selectedValues.add(value);
    }
  }

  // Re-paint pills' .active state in place — cheaper than re-rendering.
  card.querySelectorAll('.filter-pill').forEach((/** @type {Element} */ p) => {
    const el = /** @type {HTMLElement} */ (p);
    el.classList.toggle('active', rec.selectedValues.has(el.dataset.value ?? ''));
  });
  setModeLabel(card, rec);

  await replayFiltersFor(rec.appliedTo);
}

/**
 * Search-variant Enter key adds a chip. Empty string is allowed (matches
 * "(blank)" rows the same way the table-wide string filter does).
 *
 * @param {KeyboardEvent} e
 * @param {HTMLInputElement} input
 */
export async function onSearchEnter(e, input) {
  if (e.key !== 'Enter') return;
  e.preventDefault();
  const card = /** @type {HTMLElement | null} */ (input.closest('.canvas-card'));
  if (!card) return;
  const gid = Number(card.dataset.groupId);
  const rec = getFilter(gid);
  if (!rec || rec.variant !== 'search') return;

  const value = input.value.trim();
  if (rec.selectedValues.has(value)) {
    input.value = '';
    return;
  }
  rec.selectedValues.add(value);
  input.value = '';

  const chips = /** @type {HTMLElement | null} */ (card.querySelector('[data-filter-chips]'));
  chips?.appendChild(buildChip(value));
  setModeLabel(card, rec);

  await replayFiltersFor(rec.appliedTo);
}

/**
 * Remove a chip in the search variant. Wired via [data-action="filter:chip-remove"]
 * on the chip's × button.
 *
 * @param {MouseEvent} e
 * @param {HTMLElement} btn
 */
export async function onChipRemove(e, btn) {
  const chip = /** @type {HTMLElement | null} */ (btn.closest('.filter-chip'));
  const card = /** @type {HTMLElement | null} */ (btn.closest('.canvas-card'));
  if (!chip || !card) return;
  const gid = Number(card.dataset.groupId);
  const rec = getFilter(gid);
  if (!rec) return;

  const value = chip.dataset.value ?? '';
  rec.selectedValues.delete(value);
  chip.remove();
  setModeLabel(card, rec);

  await replayFiltersFor(rec.appliedTo);
}

/**
 * Footer "Clear" button — wipes the selection but leaves the appliedTo set
 * intact (the user's intent of "this filter targets these charts" survives).
 *
 * @param {MouseEvent} e
 * @param {HTMLElement} btn
 */
export async function onClear(e, btn) {
  const card = /** @type {HTMLElement | null} */ (btn.closest('.canvas-card'));
  if (!card) return;
  const gid = Number(card.dataset.groupId);
  const rec = getFilter(gid);
  if (!rec) return;

  rec.selectedValues.clear();
  if (rec.variant === 'buttons') {
    card.querySelectorAll('.filter-pill.active')
      .forEach((p) => p.classList.remove('active'));
  } else {
    const chips = card.querySelector('[data-filter-chips]');
    if (chips) chips.innerHTML = '';
  }
  setModeLabel(card, rec);

  await replayFiltersFor(rec.appliedTo);
}

/**
 * Right-panel chart-list events — re-exported here so the workspace action
 * dispatcher has one module to import from. Implementations live in
 * selection.js (they need the deselect/select primitives).
 */
export { onApplyToHover, onApplyToUnhover, onApplyToToggle } from './selection.js';

/**
 * "Use as filter" toolbar button on a chart card. Wired by builder.js. We
 * also expose the convertChartToFilter direct call there, but going through
 * an action handler keeps the data-action delegation symmetrical.
 *
 * @param {MouseEvent} e
 * @param {HTMLElement} btn
 */
export async function onUseAsFilter(e, btn) {
  const card = /** @type {HTMLElement | null} */ (btn.closest('.canvas-card'));
  if (!card) return;
  const { convertChartToFilter } = await import('./convert.js');
  await convertChartToFilter(card);
}

/** Re-export so the toolbar's remove-this-filter affordance can use it. */
export { removeFilterCard };
