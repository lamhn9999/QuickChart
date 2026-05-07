// @ts-check
/**
 * @file §4 MERGE DATASETS — pick A + B, choose Sum/Avg/Diff, click Apply → new dataset
 * pushed into seriesState. Calls back into series.js to re-render the rows + sync to chart.
 */

import { css, accordion, mkSelect } from './primitives.js';

/** @typedef {{ dataset: any, visible: boolean }} SeriesState */

/**
 * @param {any} chart
 * @param {{ rebuild: () => void, syncDatasetsToChart: () => void, seriesState: SeriesState[] }} seriesCtx
 * @returns {HTMLElement}
 */
export function buildMergeAccordion(chart, seriesCtx) {
  return accordion('Merge Datasets', (body) => {
    /** @param {HTMLSelectElement} sel */
    function rebuildOpts(sel) {
      sel.innerHTML = '';
      chart.data.datasets.forEach((/** @type {any} */ ds, /** @type {number} */ i) => {
        const o = document.createElement('option');
        o.value = String(i);
        o.textContent = ds.label ?? `Dataset ${i}`;
        sel.appendChild(o);
      });
    }

    const selA = mkSelect();
    const selB = mkSelect();
    rebuildOpts(selA);
    rebuildOpts(selB);
    if (selB.options.length > 1) selB.selectedIndex = 1;

    const selGrid = document.createElement('div');
    css(selGrid, 'display:grid;grid-template-columns:1fr 1fr;gap:6px');

    /**
     * @param {string} lbl
     * @param {HTMLElement} sel
     */
    function mkLblSel(lbl, sel) {
      const w = document.createElement('div');
      css(w, 'display:flex;flex-direction:column;gap:3px');
      const l = document.createElement('span');
      css(l, 'font-size:8.5px;color:#a1a1aa;letter-spacing:0.04em');
      l.textContent = lbl;
      w.append(l, sel);
      return w;
    }
    selGrid.append(mkLblSel('Dataset A', selA), mkLblSel('Dataset B', selB));
    body.appendChild(selGrid);

    // Op pills
    let _op = 'sum';
    /** @type {[string, string][]} */
    const OPS = [
      ['sum', 'Sum'],
      ['avg', 'Avg'],
      ['diff', 'Diff'],
    ];
    const opWrap = document.createElement('div');
    css(opWrap, 'display:flex;gap:3px');
    const opBtns = OPS.map(([val, lbl]) => {
      const b = document.createElement('button');
      b.textContent = lbl;
      css(
        b,
        `flex:1;padding:5px 0;border:1px solid ${val === _op ? 'rgba(233,150,122,0.4)' : '#e4e4e7'};border-radius:5px;font-size:9.5px;background:${val === _op ? 'rgba(233,150,122,0.1)' : '#fff'};color:${val === _op ? '#E9967A' : '#71717a'};cursor:pointer;transition:background 0.1s,color 0.1s,border-color 0.1s;font-family:inherit`
      );
      b.addEventListener('click', () => {
        _op = val;
        opBtns.forEach((ob, oi) => {
          const a = OPS[oi][0] === _op;
          ob.style.background = a ? 'rgba(233,150,122,0.1)' : '#fff';
          ob.style.color = a ? '#E9967A' : '#71717a';
          ob.style.borderColor = a ? 'rgba(233,150,122,0.4)' : '#e4e4e7';
        });
      });
      opWrap.appendChild(b);
      return b;
    });
    body.appendChild(opWrap);

    const opLbl = document.createElement('span');
    css(opLbl, 'font-size:8.5px;color:#a1a1aa;letter-spacing:0.04em;margin-top:-2px');
    opLbl.textContent = 'Operation';
    body.insertBefore(opLbl, opWrap);

    const mergeBtn = document.createElement('button');
    css(
      mergeBtn,
      'width:100%;padding:7px;border:none;border-radius:6px;background:#E9967A;color:#fff;font-size:10px;font-weight:600;cursor:pointer;letter-spacing:0.02em;transition:background 0.1s;font-family:inherit;margin-top:2px'
    );
    mergeBtn.textContent = 'Apply Merge';
    mergeBtn.addEventListener('mouseenter', () => (mergeBtn.style.background = '#e08060'));
    mergeBtn.addEventListener('mouseleave', () => (mergeBtn.style.background = '#E9967A'));
    mergeBtn.addEventListener('click', () => {
      const ai = parseInt(selA.value);
      const bi = parseInt(selB.value);
      const dsA = chart.data.datasets[ai];
      const dsB = chart.data.datasets[bi];
      if (!dsA || !dsB) return;
      const newData = dsA.data.map((/** @type {any} */ va, /** @type {number} */ i) => {
        const a = parseFloat(va) || 0;
        const b = parseFloat(dsB.data[i]) || 0;
        return _op === 'sum' ? a + b : _op === 'avg' ? (a + b) / 2 : a - b;
      });
      const opLabels = { sum: 'Sum', avg: 'Avg', diff: 'Diff' };
      const mergedDs = {
        label: `${opLabels[/** @type {keyof typeof opLabels} */ (_op)]}(${dsA.label ?? ai}, ${dsB.label ?? bi})`,
        data: newData,
        borderColor: '#818cf8',
        backgroundColor: '#818cf855',
        borderWidth: 2,
        tension: 0.3,
      };
      seriesCtx.seriesState.push({ dataset: mergedDs, visible: true });
      seriesCtx.syncDatasetsToChart();
      rebuildOpts(selA);
      rebuildOpts(selB);
      seriesCtx.rebuild();
    });
    body.appendChild(mergeBtn);
  });
}
