// @ts-check
/**
 * @file §2 SORT — three-way segmented pill buttons (Original / Asc / Desc).
 *
 * Sorts by `chart.data.datasets[0].data` numerically. Original uses the snapshot taken at
 * panel-open time so a user can revert after mucking with the data.
 */

import { css } from './primitives.js';
import { accordion } from './primitives.js';

/**
 * @param {any} chart
 * @param {{ labels: any[], datasets: any[] }} orig snapshot of original data + labels
 * @returns {HTMLElement} accordion wrapper, ready to append into the panel inner
 */
export function buildSortAccordion(chart, orig) {
  return accordion('Sort', (body) => {
    const pillWrap = document.createElement('div');
    css(pillWrap, 'display:flex;gap:3px;background:rgba(0,0,0,0.05);border-radius:7px;padding:3px');

    /** @type {[string, string][]} */
    const SORTS = [
      ['original', 'Original'],
      ['asc', '↑ Asc'],
      ['desc', '↓ Desc'],
    ];
    let _activeSort = 'original';

    const pills = SORTS.map(([val, lbl]) => {
      const btn = document.createElement('button');
      btn.textContent = lbl;
      css(
        btn,
        `flex:1;padding:4px 2px;border:none;border-radius:5px;font-size:9.5px;font-weight:${val === _activeSort ? '600' : '500'};cursor:pointer;transition:background 0.12s,color 0.12s;background:${val === _activeSort ? '#E9967A' : 'transparent'};color:${val === _activeSort ? '#fff' : '#71717a'};font-family:inherit`
      );
      btn.addEventListener('click', () => {
        _activeSort = val;
        pills.forEach((p, pi) => {
          const active = SORTS[pi][0] === _activeSort;
          p.style.background = active ? '#E9967A' : 'transparent';
          p.style.color = active ? '#fff' : '#71717a';
          p.style.fontWeight = active ? '600' : '500';
        });
        if (!chart.data.labels?.length) return;
        const n = chart.data.labels.length;
        const idx = Array.from({ length: n }, (_, i) => i);
        if (val === 'original') {
          chart.data.labels = [...orig.labels];
          chart.data.datasets.forEach((/** @type {any} */ ds) => {
            const od = orig.datasets.find((d) => d.label === ds.label);
            if (od) ds.data = [...od.data];
          });
        } else {
          idx.sort((a, b) => {
            const va = parseFloat(chart.data.datasets[0]?.data?.[a]) || 0;
            const vb = parseFloat(chart.data.datasets[0]?.data?.[b]) || 0;
            return val === 'asc' ? va - vb : vb - va;
          });
          chart.data.labels = idx.map((i) => chart.data.labels[i]);
          chart.data.datasets.forEach((/** @type {any} */ ds) => {
            ds.data = idx.map((i) => ds.data[i]);
          });
        }
        chart.update('none');
      });
      pillWrap.appendChild(btn);
      return btn;
    });
    body.appendChild(pillWrap);
  });
}
