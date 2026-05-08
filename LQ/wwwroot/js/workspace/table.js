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

  // Build per-column numeric flag from thead data-spark-type attributes.
  const table = tbody.closest('table');
  const colIsNum = table
    ? Array.from(table.querySelectorAll('thead th')).map((th) => {
        const t = th.querySelector('[data-spark-type]')?.dataset.sparkType ?? '';
        return t === 'Int32' || t === 'Double';
      })
    : /** @type {boolean[]} */ ([]);

  const TR_CLASS =
    'even:bg-zinc-50/70 hover:bg-[#E9967A]/5 transition-colors duration-75 group/row cursor-default';
  const TD_BASE =
    'px-3 py-2 text-xs border-b border-zinc-100 whitespace-nowrap max-w-[220px] overflow-hidden text-ellipsis transition-colors duration-75';

  const trs = tbody.querySelectorAll('tr');
  const dataRows = dto.values;

  dataRows.forEach((row, i) => {
    /** @type {HTMLTableRowElement} */
    let tr;

    if (trs[i]) {
      tr = trs[i];
    } else {
      tr = document.createElement('tr');
      tr.className = TR_CLASS;
      tbody.appendChild(tr);
      row.forEach(() => tr.appendChild(document.createElement('td')));
    }

    const tds = tr.querySelectorAll('td');
    row.forEach((cell, j) => {
      const td = tds[j];
      const text = cell == null ? '' : String(cell);
      td.textContent = text;

      const isNum = colIsNum[j] ?? false;
      const isEmpty = text === '';
      const isZero = isNum && (text === '0' || text === '0.0' || text === '0.00');

      let cls = TD_BASE;
      if (isNum) cls += ' text-right tabular-nums';
      if (isEmpty) cls += ' text-zinc-300';
      else if (isZero) cls += ' text-zinc-400 group-hover/row:text-zinc-600';
      else cls += ' text-zinc-500 group-hover/row:text-zinc-900';

      td.className = cls;
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
