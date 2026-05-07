// @ts-check
/**
 * @file Per-column sparkline rendering for the collapsed-card view.
 *
 * Reads raw values directly from the table tbody, picks a renderer based on `data-spark-type`
 * (Boolean / Int32 / Double / String), and paints into the column header's `.sparkline-canvas`
 * + `.sparkline-stat` slots.
 *
 * - Boolean → True/False stacked bar
 * - Int32 / Double → 14-bin histogram with min/max badges
 * - String + categorical (≤2 distinct) → mini pie chart
 * - String + categorical (≥3 distinct) → top-6 list
 * - String non-categorical → "N distinct" count
 */

import { fmtNum, drawHistogram } from './format.js';

/**
 * @param {HTMLCanvasElement} canvas
 * @param {HTMLElement} statDiv
 * @param {string[]} rawValues
 */
function sparkBool(canvas, statDiv, rawValues) {
  canvas.style.display = 'none';
  const tN = rawValues.filter((v) => v.toLowerCase() === 'true').length;
  const fN = rawValues.filter((v) => v.toLowerCase() === 'false').length;
  const tot = tN + fN;
  if (tot === 0) return;
  const tP = Math.round((tN / tot) * 100);
  const fP = 100 - tP;

  statDiv.style.cssText = '';
  statDiv.innerHTML = `
        <div style="display:flex;flex-direction:column;gap:3px;margin-top:2px">
            <div style="display:flex;align-items:center;gap:5px">
                <span style="width:28px;text-align:right;color:#a1a1aa;font-size:9px;flex-shrink:0">True</span>
                <div style="flex:1;height:8px;background:rgba(255,255,255,0.08);border-radius:4px;overflow:hidden">
                    <div style="height:100%;width:${tP}%;background:rgba(52,211,153,0.7);border-radius:4px"></div>
                </div>
                <span style="width:26px;color:#a1a1aa;font-size:9px;flex-shrink:0">${tP}%</span>
            </div>
            <div style="display:flex;align-items:center;gap:5px">
                <span style="width:28px;text-align:right;color:#a1a1aa;font-size:9px;flex-shrink:0">False</span>
                <div style="flex:1;height:8px;background:rgba(255,255,255,0.08);border-radius:4px;overflow:hidden">
                    <div style="height:100%;width:${fP}%;background:rgba(251,113,133,0.6);border-radius:4px"></div>
                </div>
                <span style="width:26px;color:#a1a1aa;font-size:9px;flex-shrink:0">${fP}%</span>
            </div>
        </div>`;
}

/**
 * @param {HTMLCanvasElement} canvas
 * @param {HTMLElement} statDiv
 * @param {string[]} rawValues
 */
function sparkNumeric(canvas, statDiv, rawValues) {
  const nums = rawValues.map(Number).filter((n) => !isNaN(n));
  if (nums.length === 0) {
    canvas.style.display = 'none';
    return;
  }
  canvas.style.display = 'block';
  const [min, max] = drawHistogram(canvas, nums);
  statDiv.style.cssText = 'display:flex;justify-content:space-between';
  statDiv.innerHTML = `<span>${fmtNum(min)}</span><span>${fmtNum(max)}</span>`;
}

/**
 * @param {HTMLCanvasElement} canvas
 * @param {HTMLElement} statDiv
 * @param {string[]} rawValues
 */
function sparkCategorical(canvas, statDiv, rawValues) {
  /** @type {Record<string, number>} */
  const counts = {};
  rawValues.forEach((v) => {
    counts[v] = (counts[v] || 0) + 1;
  });
  const total = rawValues.length;
  const entries = Object.entries(counts).sort((a, b) => b[1] - a[1]);

  if (entries.length < 3) {
    // ── Mini pie + inline legend (row flex) ───────────────────────────
    canvas.style.display = 'none';
    statDiv.style.display = 'none';

    const PIE_COLORS = ['rgba(233,150,122,0.9)', 'rgba(99,179,237,0.85)', 'rgba(154,230,180,0.85)'];

    const row = document.createElement('div');
    row.className = 'spark-row';
    row.style.cssText = 'display:flex;gap:8px;align-items:center;margin-top:4px';

    const pieCanvas = document.createElement('canvas');
    pieCanvas.width = 52;
    pieCanvas.height = 52;
    pieCanvas.style.cssText = 'width:52px;height:52px;flex-shrink:0';

    const legendEl = document.createElement('div');
    legendEl.style.cssText = 'display:flex;flex-direction:column;gap:3px;flex:1;min-width:0';
    entries.forEach(([label, count], i) => {
      const pct = Math.round((count / total) * 100);
      const item = document.createElement('div');
      item.style.cssText = 'display:flex;align-items:center;gap:3px;font-size:9px;color:#a1a1aa';
      item.innerHTML =
        `<span style="width:7px;height:7px;border-radius:2px;background:${PIE_COLORS[i % PIE_COLORS.length]};flex-shrink:0;display:inline-block"></span>` +
        `<span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;flex:1">${label || '(empty)'}</span>` +
        `<span style="flex-shrink:0;margin-left:2px">${pct}%</span>`;
      legendEl.appendChild(item);
    });

    row.appendChild(pieCanvas);
    row.appendChild(legendEl);
    canvas.parentElement?.appendChild(row);

    // @ts-ignore — Chart is a runtime global
    new Chart(pieCanvas, {
      type: 'pie',
      data: {
        labels: entries.map(([l]) => l),
        datasets: [
          {
            data: entries.map(([, c]) => c),
            backgroundColor: PIE_COLORS.slice(0, entries.length),
            borderWidth: 0,
          },
        ],
      },
      options: {
        responsive: false,
        animation: false,
        plugins: { legend: { display: false }, tooltip: { enabled: false } },
      },
    });
  } else {
    // ── Text list for ≥ 3 distinct values: top 3 solid, rest faded ──
    canvas.style.display = 'none';
    statDiv.style.cssText = '';
    statDiv.innerHTML = entries
      .slice(0, 6)
      .map(([label, count], i) => {
        const faded = i >= 3;
        const cleanLabel = (label || '(empty)').replace(/^"+|"+$/g, '');
        const color = faded ? 'color:#52525b' : 'color:#a1a1aa';
        const cntClr = faded ? 'color:#3f3f46' : 'color:#71717a';
        return (
          `<div style="display:flex;justify-content:space-between;gap:4px;line-height:1.5;${faded ? 'opacity:0.45' : ''}">` +
          `<span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;${color}">${cleanLabel}</span>` +
          `<span style="flex-shrink:0;tabular-nums;${cntClr}">${count}</span>` +
          `</div>`
        );
      })
      .join('');
  }
}

/**
 * @param {HTMLCanvasElement} canvas
 * @param {HTMLElement} statDiv
 * @param {string[]} rawValues
 */
function sparkString(canvas, statDiv, rawValues) {
  canvas.style.display = 'none';
  const distinct = new Set(rawValues).size;
  statDiv.style.cssText = '';
  statDiv.textContent = `${distinct.toLocaleString()} distinct`;
}

/**
 * Draw per-field distribution visuals inside the collapsed card view.
 * Called once on DOMContentLoaded — reads values directly from the table tbody.
 */
export function initSparklines() {
  document.querySelectorAll('[data-spark-col]').forEach((containerNode) => {
    const container = /** @type {HTMLElement} */ (containerNode);
    const th = container.closest('th');
    if (!th) return;

    const colIdx = th.cellIndex;
    const fieldType = container.dataset.sparkType;
    const isCat = container.dataset.sparkCategorical === 'true';

    const cells = document.querySelectorAll(`#dataTable tbody tr td:nth-child(${colIdx + 1})`);
    const rawValues = Array.from(cells)
      .map((td) => td.textContent?.trim() ?? '')
      .filter((v) => v !== '');
    if (rawValues.length === 0) return;

    const canvas = /** @type {HTMLCanvasElement | null} */ (
      container.querySelector('.sparkline-canvas')
    );
    const statDiv = /** @type {HTMLElement | null} */ (container.querySelector('.sparkline-stat'));
    if (!canvas || !statDiv) return;

    if (fieldType === 'Boolean') sparkBool(canvas, statDiv, rawValues);
    else if (fieldType === 'Int32' || fieldType === 'Double')
      sparkNumeric(canvas, statDiv, rawValues);
    else if (fieldType === 'String' && isCat) sparkCategorical(canvas, statDiv, rawValues);
    else if (fieldType === 'String') sparkString(canvas, statDiv, rawValues);
    else canvas.style.display = 'none';
  });
}
