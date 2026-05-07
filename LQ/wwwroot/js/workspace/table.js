// @ts-check
/**
 * @file Data-table updates and column-cell highlighting.
 *
 * - `updateTable(dto)` rewrites the body of `#dataTable` from a server response (used after
 *   a filter applies). Reuses existing rows + cells where possible to avoid full rebuilds.
 * - `wireColumnHighlight(table)` toggles the `.col-selected` class on every cell of a column
 *   when its `<th>` is clicked.
 */

import { POPUP_SELECTORS, TRIGGER_SELECTORS } from '../core/menus.js';

/**
 * @typedef {{ values: (string | number | null)[][] }} TableDTO
 */

/**
 * Re-render `#dataTable`'s tbody from `dto.values`. Reuses existing `<tr>`/`<td>` nodes
 * to avoid layout thrash; only creates new ones when the row count grows.
 * @param {TableDTO} dto
 */
export function updateTable(dto) {
  const tbody = /** @type {HTMLTableSectionElement | null} */ (
    document.querySelector('#dataTable tbody')
  );
  if (!tbody) return;
  const trs = tbody.querySelectorAll('tr');

  const dataRows = dto.values;

  dataRows.forEach((row, i) => {
    /** @type {HTMLTableRowElement} */
    let tr;

    if (trs[i]) {
      tr = trs[i];
    } else {
      tr = document.createElement('tr');
      tbody.appendChild(tr);
      row.forEach(() => {
        const td = document.createElement('td');
        tr.appendChild(td);
      });
    }

    const tds = tr.querySelectorAll('td');
    row.forEach((cell, j) => {
      tds[j].textContent = /** @type {any} */ (cell);
    });
  });

  // Remove surplus rows
  for (let i = dataRows.length; i < trs.length; i++) {
    trs[i].remove();
  }
}

/**
 * On click of any `<th>` inside `table`, toggle `.col-selected` on every cell of that column.
 * @param {HTMLTableElement} table
 */
export function wireColumnHighlight(table) {
  table.addEventListener('click', (e) => {
    const target = e.target;
    if (!(target instanceof Element)) return;
    // Skip: each filter / gear / category popup is nested inside the th for positioning,
    // and so are the trigger buttons that open them. A click on either bubbles up to the
    // table — `closest('th')` matches the ancestor th regardless. Without this guard,
    // clicking an input inside a popup OR clicking the filter/gear icon itself would
    // toggle `.col-selected` on every cell of that column.
    if (target.closest(POPUP_SELECTORS) || target.closest(TRIGGER_SELECTORS)) return;
    const th = target.closest('th');
    if (!th) return;
    const colIndex = th.cellIndex;
    table.querySelectorAll('tr').forEach((row) => {
      const cell = row.children[colIndex];
      if (cell) cell.classList.toggle('col-selected');
    });
  });
}
