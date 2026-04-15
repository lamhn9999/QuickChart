import { listIndexes, appState } from './workspace.js';

// ── Chart globals ─────────────────────────────────────────────────────────────
export let chartInstances = [];
export const chartArray = ['pie', 'bar', 'line', 'bar', 'scatter', 'polarArea', 'bar', 'mixed', 'matrix', 'mixedBar', 'bubble'];
export let muuriGrid = null;

// ── Global tooltip style ──────────────────────────────────────────────────────
Chart.defaults.plugins.tooltip.cornerRadius = 4;

// ── Theme tokens ─────────────────────────────────────────────────────────────
const DARK_TEXT   = 'rgba(255,255,255,0.65)';
const DARK_TITLE  = 'rgba(255,255,255,0.92)';
const DARK_GRID   = 'rgba(255,255,255,0.06)';
const LIGHT_TEXT  = '#52525b';
const LIGHT_TITLE = '#18181b';
const LIGHT_GRID  = 'rgba(0,0,0,0.08)';

const axisStyle = {
    ticks: { color: LIGHT_TEXT, font: { size: 11 } },
    grid: { color: LIGHT_GRID }
};

const stackedAxis = {
    ...axisStyle,
    stacked: true
};

/**
 * Switch a chart's text / grid colours to match a container background.
 * theme: 'dark'  → card (dark bg) — use light text
 *        'light' → zoom overlay (light bg) — use dark text
 */
function _applyChartTheme(chart, theme) {
    const isDark = theme === 'dark';
    const text   = isDark ? DARK_TEXT  : LIGHT_TEXT;
    const title  = isDark ? DARK_TITLE : LIGHT_TITLE;
    const grid   = isDark ? DARK_GRID  : LIGHT_GRID;

    if (chart.options.plugins?.legend?.labels)
        chart.options.plugins.legend.labels.color = text;
    if (chart.options.plugins?.title)
        chart.options.plugins.title.color = title;

    ['x', 'y', 'z'].forEach(axis => {
        const s = chart.options.scales?.[axis];
        if (!s) return;
        if (s.ticks) s.ticks.color = text;
        if (s.grid)  s.grid.color  = grid;
    });

    // Matrix: border colour as text
    if (chart.config.type === 'matrix') {
        chart.data.datasets.forEach(ds => { ds.borderColor = text; });
    }

    chart.update('none');
}

// ── Zoom state ────────────────────────────────────────────────────────────────
let _zoomedWrapper  = null;
let _zoomedParent   = null;
let _zoomedSibling  = null;
let _zoomedCssText  = '';

// ── Navigator state ───────────────────────────────────────────────────────────
let _navChartInst = null;   // Chart.js instance for the mini overview
let _navDragState  = null;  // active drag: { type, startX, startLeft, startWidth }

// ── Matrix helpers ────────────────────────────────────────────────────────────

function buildHeatmapData(xLabels, ySeries) {
    return ySeries.flatMap(row =>
        row.data.map((value, colIndex) => ({
            x: xLabels[colIndex],
            y: row.label,
            v: value ? value : 0,
        }))
    );
}

function buildMatrixDataset(sample) {
    const heatmapData = buildHeatmapData(sample.x, sample.y);

    const values = heatmapData.map(d => d.v);
    const minVal = 0;
    const maxVal = Math.max(...values);

    const xCount = new Set(heatmapData.map(d => d.x)).size;
    const yCount = new Set(heatmapData.map(d => d.y)).size;

    // Detect correlation matrix by title
    const isCorr = (sample.title || '').toLowerCase().includes('correlation');

    // Muted coolwarm: steel-blue → off-white → crimson (softer than pure blue/white/red)
    function corrToColor(v) {
        const t = (Math.max(-1, Math.min(1, v)) + 1) / 2;
        let r, g, b;
        if (t < 0.5) {
            const f = t / 0.5;
            r = Math.round(58  + f * (238 - 58));   // steel-blue → off-white
            g = Math.round(76  + f * (238 - 76));
            b = Math.round(192 + f * (238 - 192));
        } else {
            const f = (t - 0.5) / 0.5;
            r = Math.round(238 + f * (178 - 238));  // off-white → crimson
            g = Math.round(238 + f * (24  - 238));
            b = Math.round(238 + f * (43  - 238));
        }
        return `rgba(${r},${g},${b},0.92)`;
    }

    return [{
        type: 'matrix',
        label: sample.title ?? 'Heatmap',
        data: heatmapData,
        borderColor: 'rgba(255,255,255,0.12)',
        borderWidth: 1,

        backgroundColor(ctx) {
            const v = ctx.raw.v;
            if (isCorr) return corrToColor(v);
            const t = (v - minVal) / (maxVal - minVal || 1);
            return `rgba(0,123,255,${t})`;
        },

        width({ chart }) {
            const a = chart.chartArea;
            return a ? (a.right - a.left) / xCount - 2 : 0;
        },

        height({ chart }) {
            const a = chart.chartArea;
            return a ? (a.bottom - a.top) / yCount - 2 : 0;
        }
    }];
}

// ── Box-plot canvas drawing ───────────────────────────────────────────────────

function drawBoxPlot(chart, data) {
    const { labels, min, q1, med, q3, max } = data;
    const c  = chart.ctx;
    const xs = chart.scales.x;
    const ys = chart.scales.y;
    if (!xs || !ys || !labels.length) return;

    const n    = labels.length;
    const catW = n > 1
        ? Math.abs(xs.getPixelForValue(labels[1]) - xs.getPixelForValue(labels[0]))
        : (chart.chartArea.right - chart.chartArea.left);
    const half = catW * 0.28;   // IQR box half-width
    const cap  = catW * 0.13;   // whisker cap half-width

    c.save();
    c.beginPath();
    c.rect(chart.chartArea.left, chart.chartArea.top,
           chart.chartArea.right  - chart.chartArea.left,
           chart.chartArea.bottom - chart.chartArea.top);
    c.clip();

    labels.forEach((label, i) => {
        const cx   = xs.getPixelForValue(label);
        const yMin = ys.getPixelForValue(min[i]);
        const yQ1  = ys.getPixelForValue(q1[i]);
        const yMed = ys.getPixelForValue(med[i]);
        const yQ3  = ys.getPixelForValue(q3[i]);
        const yMax = ys.getPixelForValue(max[i]);

        const boxTop = Math.min(yQ1, yQ3);
        const boxBot = Math.max(yQ1, yQ3);
        const boxH   = Math.max(boxBot - boxTop, 1);

        // Whisker lines: Max→Q3 and Q1→Min
        c.beginPath();
        c.strokeStyle = 'rgba(54,162,235,0.75)';
        c.lineWidth   = 1.5;
        c.moveTo(cx, yMax);   c.lineTo(cx, boxTop);
        c.moveTo(cx, boxBot); c.lineTo(cx, yMin);
        c.stroke();

        // T-caps at Max and Min
        c.beginPath();
        c.moveTo(cx - cap, yMax); c.lineTo(cx + cap, yMax);
        c.moveTo(cx - cap, yMin); c.lineTo(cx + cap, yMin);
        c.stroke();

        // IQR box (Q1–Q3)
        const r = Math.min(3, half * 0.1, boxH * 0.1);
        c.beginPath();
        if (c.roundRect) {
            c.roundRect(cx - half, boxTop, half * 2, boxH, r);
        } else {
            c.rect(cx - half, boxTop, half * 2, boxH);
        }
        c.fillStyle   = 'rgba(54,162,235,0.22)';
        c.strokeStyle = 'rgba(54,162,235,0.9)';
        c.lineWidth   = 1.5;
        c.fill();
        c.stroke();

        // Median line — bright cyan stands out clearly against the blue box
        c.beginPath();
        c.strokeStyle = 'rgb(0,220,255)';
        c.lineWidth   = 2.5;
        c.moveTo(cx - half, yMed);
        c.lineTo(cx + half, yMed);
        c.stroke();
    });

    c.restore();
}

// ── Color helpers ─────────────────────────────────────────────────────────────

function getContrastTextColor(color) {
    const temp = document.createElement("div");
    temp.style.color = color;
    document.body.appendChild(temp);

    const rgb = getComputedStyle(temp).color;
    document.body.removeChild(temp);

    const [r, g, b] = rgb.match(/\d+/g).map(Number);
    const brightness = (r * 299 + g * 587 + b * 114) / 1000;

    return brightness > 128 ? "black" : "white";
}

function reformatColor(color, canvas) {
    canvas.style.backgroundColor = color;

    const textColor = getContrastTextColor(color);
    const chart = Chart.getChart(canvas);
    const isDark = textColor === 'white';
    _applyChartTheme(chart, isDark ? 'dark' : 'light');
}

// ── Field sparklines (collapsed card view) ────────────────────────────────────

function _fmtNum(n) {
    const a = Math.abs(n);
    if (a >= 1e9) return (n / 1e9).toFixed(1) + 'B';
    if (a >= 1e6) return (n / 1e6).toFixed(1) + 'M';
    if (a >= 1e3) return (n / 1e3).toFixed(1) + 'k';
    return n.toLocaleString(undefined, { maximumFractionDigits: 2 });
}

function _drawHistogram(canvas, nums) {
    const BINS = 14, W = 240, H = 28;
    canvas.width  = W;
    canvas.height = H;
    const ctx = canvas.getContext('2d');
    ctx.clearRect(0, 0, W, H);

    const min = Math.min(...nums), max = Math.max(...nums);
    if (min === max) {
        ctx.fillStyle = 'rgba(233,150,122,0.6)';
        ctx.fillRect(W * 0.1, 2, W * 0.8, H - 4);
        return [min, max];
    }

    const binSize = (max - min) / BINS;
    const counts  = new Array(BINS).fill(0);
    nums.forEach(n => {
        counts[Math.min(Math.floor((n - min) / binSize), BINS - 1)]++;
    });
    const maxC = Math.max(...counts);
    const gap  = 1;
    const barW = (W - gap * (BINS + 1)) / BINS;

    counts.forEach((c, i) => {
        const h = c / maxC * (H - 4);
        ctx.fillStyle = 'rgba(233,150,122,0.65)';
        ctx.fillRect(
            Math.round(gap + i * (barW + gap)),
            Math.round(H - h - 2),
            Math.round(barW),
            Math.round(h)
        );
    });
    return [min, max];
}

function _sparkBool(canvas, statDiv, rawValues) {
    canvas.style.display = 'none';
    const tN = rawValues.filter(v => v.toLowerCase() === 'true').length;
    const fN = rawValues.filter(v => v.toLowerCase() === 'false').length;
    const tot = tN + fN;
    if (tot === 0) return;
    const tP = Math.round(tN / tot * 100);
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

function _sparkNumeric(canvas, statDiv, rawValues) {
    const nums = rawValues.map(Number).filter(n => !isNaN(n));
    if (nums.length === 0) { canvas.style.display = 'none'; return; }
    canvas.style.display = 'block';
    const [min, max] = _drawHistogram(canvas, nums);
    statDiv.style.cssText = 'display:flex;justify-content:space-between';
    statDiv.innerHTML = `<span>${_fmtNum(min)}</span><span>${_fmtNum(max)}</span>`;
}

function _sparkCategorical(canvas, statDiv, rawValues) {
    const counts = {};
    rawValues.forEach(v => { counts[v] = (counts[v] || 0) + 1; });
    const total   = rawValues.length;
    const entries = Object.entries(counts).sort((a, b) => b[1] - a[1]);

    if (entries.length < 3) {
        // ── Mini pie + inline legend (row flex) ───────────────────────────
        canvas.style.display = 'none';
        statDiv.style.display = 'none';

        const PIE_COLORS = [
            'rgba(233,150,122,0.9)',
            'rgba(99,179,237,0.85)',
            'rgba(154,230,180,0.85)',
        ];

        const row = document.createElement('div');
        row.className = 'spark-row';
        row.style.cssText = 'display:flex;gap:8px;align-items:center;margin-top:4px';

        const pieCanvas = document.createElement('canvas');
        pieCanvas.width  = 52;
        pieCanvas.height = 52;
        pieCanvas.style.cssText = 'width:52px;height:52px;flex-shrink:0';

        const legendEl = document.createElement('div');
        legendEl.style.cssText = 'display:flex;flex-direction:column;gap:3px;flex:1;min-width:0';
        entries.forEach(([label, count], i) => {
            const pct  = Math.round(count / total * 100);
            const item = document.createElement('div');
            item.style.cssText = 'display:flex;align-items:center;gap:3px;font-size:9px;color:#a1a1aa';
            item.innerHTML =
                `<span style="width:7px;height:7px;border-radius:2px;background:${PIE_COLORS[i % PIE_COLORS.length]};flex-shrink:0;display:inline-block"></span>`
              + `<span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;flex:1">${label || '(empty)'}</span>`
              + `<span style="flex-shrink:0;margin-left:2px">${pct}%</span>`;
            legendEl.appendChild(item);
        });

        row.appendChild(pieCanvas);
        row.appendChild(legendEl);
        canvas.parentElement.appendChild(row);

        new Chart(pieCanvas, {
            type: 'pie',
            data: {
                labels: entries.map(([l]) => l),
                datasets: [{
                    data: entries.map(([, c]) => c),
                    backgroundColor: PIE_COLORS.slice(0, entries.length),
                    borderWidth: 0,
                }]
            },
            options: {
                responsive: false,
                animation: false,
                plugins: { legend: { display: false }, tooltip: { enabled: false } },
            }
        });

    } else {
        // ── Text list for ≥ 3 distinct values: top 3 solid, rest faded ──
        canvas.style.display = 'none';
        statDiv.style.cssText = '';
        statDiv.innerHTML = entries.slice(0, 6).map(([label, count], i) => {
            const faded      = i >= 3;
            const cleanLabel = (label || '(empty)').replace(/^"+|"+$/g, '');
            const color      = faded ? 'color:#52525b' : 'color:#a1a1aa';
            const cntClr     = faded ? 'color:#3f3f46' : 'color:#71717a';
            return `<div style="display:flex;justify-content:space-between;gap:4px;line-height:1.5;${faded ? 'opacity:0.45' : ''}">`
                 + `<span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap;${color}">${cleanLabel}</span>`
                 + `<span style="flex-shrink:0;tabular-nums;${cntClr}">${count}</span>`
                 + `</div>`;
        }).join('');
    }
}

function _sparkString(canvas, statDiv, rawValues) {
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
    document.querySelectorAll('[data-spark-col]').forEach(container => {
        const th = container.closest('th');
        if (!th) return;

        const colIdx      = th.cellIndex;
        const fieldType   = container.dataset.sparkType;
        const isCat       = container.dataset.sparkCategorical === 'true';

        const cells     = document.querySelectorAll(`#dataTable tbody tr td:nth-child(${colIdx + 1})`);
        const rawValues = Array.from(cells).map(td => td.textContent.trim()).filter(v => v !== '');
        if (rawValues.length === 0) return;

        const canvas  = container.querySelector('.sparkline-canvas');
        const statDiv = container.querySelector('.sparkline-stat');
        if (!canvas || !statDiv) return;

        if      (fieldType === 'Boolean')                   _sparkBool(canvas, statDiv, rawValues);
        else if (fieldType === 'Int32' || fieldType === 'Double') _sparkNumeric(canvas, statDiv, rawValues);
        else if (fieldType === 'String' && isCat)           _sparkCategorical(canvas, statDiv, rawValues);
    else if (fieldType === 'String')                    _sparkString(canvas, statDiv, rawValues);
        else                                                canvas.style.display = 'none';
    });
}

// ── autoChart ─────────────────────────────────────────────────────────────────

export async function autoChart() {
    const selPlot   = document.getElementById("selPlot");
    const noPlot    = document.getElementById("noPlot");
    const loader    = document.getElementById("loader");
    const chartGrid = document.getElementById("chartGrid");

    // ── Clear previous state ──────────────────────────────────────────────
    chartInstances.forEach(c => c.destroy());
    chartInstances.length = 0;

    // Destroy Muuri instance and empty the grid container
    if (muuriGrid) {
        muuriGrid.destroy();
        muuriGrid = null;
        console.log("[autoChart] Muuri grid destroyed.");
    }
    chartGrid.innerHTML = '';

    if (listIndexes.size === 0) {
        loader.hidden = true;
        selPlot.hidden = false;
        noPlot.hidden = true;
        return;
    }
    selPlot.hidden = true;
    noPlot.hidden  = true;
    loader.hidden  = false;

    // panelDivCopy is the inner content card — styling only, no layout role
    const panelDivTemplate = document.createElement("div");
    panelDivTemplate.classList.add("flex", "flex-col", "h-full");

    appState.controller = new AbortController();
    try {
        const response = await fetch("/workspace", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify([...listIndexes].map(Number)),
            signal: appState.controller.signal
        });
        const result = await response.json();

        loader.hidden = true;

        if (result.length === 0) {
            console.log("[autoChart] No charts returned.");
            selPlot.hidden = true;
            noPlot.hidden  = false;
            return;
        }

        // ── Init a fresh Muuri grid ───────────────────────────────────────
        muuriGrid = new Muuri(chartGrid, {
            items:       '.grid-item',
            layoutDuration: 300,
            layoutEasing:   'ease',
            dragEnabled:    true,
            dragHandle:     '.drag-handle',           // header bar is the drag handle
            dragStartPredicate: { distance: 5, delay: 0 },
        });
        console.log("[autoChart] Muuri grid initialised.");

        // ── Group samples by panelIndex ───────────────────────────────────
        const groupedResults = result.reduce((acc, sample) => {
            if (!acc[sample.panelIndex]) acc[sample.panelIndex] = [];
            acc[sample.panelIndex].push(sample);
            return acc;
        }, {});

        Object.keys(groupedResults).sort((a, b) => a - b).forEach(pIdx => {
            const panelSamples = groupedResults[pIdx];
            const panelDivCopy = panelDivTemplate.cloneNode();
            panelDivCopy.id = "panel" + pIdx;

            const wrappers = [];

            panelSamples.forEach((sample, sIdx) => {
                const wrapper = document.createElement("div");
                wrapper.className = "flex flex-col flex-1 relative";
                if (sIdx > 0) wrapper.style.display = 'none';

                wrapper.dataset.chartTitle = sample.title || `Chart ${sIdx + 1}`;

                const ctx = document.createElement("canvas");
                wrapper.appendChild(ctx);
                panelDivCopy.appendChild(wrapper);
                wrappers.push(wrapper);

                const isMatrix          = chartArray[sample.chartType] === 'matrix';
                const isMixed           = sample.chartType === 7;
                const isMixed_notStacked = sample.chartType === 9;
                const isBubble          = sample.chartType === 10;
                const isBar             = chartArray[sample.chartType] === 'bar';
                const isPie             = chartArray[sample.chartType] === 'pie';
                const isLine            = chartArray[sample.chartType] === 'line';
                const isHorizontalBar   = sample.chartType === 6;

                // Box plot: backend emits exactly 5 Y datasets named Min/Q1/Median/Q3/Max
                const isBoxPlot = !isMatrix && !isBubble
                    && sample.y.length >= 5
                    && sample.y[2]?.label === 'Median';

                const datasets = isMatrix
                    ? buildMatrixDataset(sample)
                    : isBubble
                        // Bubble: no labels array, type on each dataset
                        ? sample.y.map(ds => ({
                            type: 'bubble',
                            label: ds.label,
                            data: ds.data,
                        }))
                        : [
                            ...sample.y.map(ds => {
                                const base = {
                                    ...((isMixed || isMixed_notStacked) && {
                                        type: chartArray[ds.chartType]
                                    }),
                                    label: ds.label,
                                    data: ds.data,
                                    yAxisID: 'y',
                                    ...(isBar && { borderRadius: 4 }),
                                    ...(isLine && {
                                        tension: 0.3,
                                        pointRadius: 3,
                                        pointHoverRadius: 6,
                                    }),
                                };
                                // Regression trend line: showLine scatter, dashed, no points
                                if (typeof ds.label === 'string' && ds.label.startsWith('Trend:')) {
                                    return {
                                        ...base,
                                        type: 'scatter',
                                        showLine: true,
                                        pointRadius: 0,
                                        pointHoverRadius: 0,
                                        borderDash: [6, 4],
                                    };
                                }
                                return base;
                            })
                        ];

                // Box plot: keep 5 stat datasets invisible for y-axis scaling;
                // a per-chart inline plugin (set up below) draws the visual.
                let boxData = null;
                if (isBoxPlot) {
                    const yVal = d => (d !== null && typeof d === 'object' && 'y' in d) ? d.y : d;
                    boxData = {
                        labels: sample.x,
                        min: datasets[0].data.map(yVal),
                        q1:  datasets[1].data.map(yVal),
                        med: datasets[2].data.map(yVal),
                        q3:  datasets[3].data.map(yVal),
                        max: datasets[4].data.map(yVal),
                    };
                    datasets.forEach(ds => {
                        ds.pointRadius      = 0;
                        ds.pointHoverRadius = 1;
                        ds.showLine         = false;
                        ds.borderColor      = 'transparent';
                        ds.backgroundColor  = 'transparent';
                    });
                }

                if (!isMatrix && !isBubble && sample.z?.length > 0) {
                    datasets.push(
                        ...sample.z.map(ds => ({
                            ...((isMixed || isMixed_notStacked) && {
                                type: chartArray[ds.chartType]
                            }),
                            label: ds.label,
                            data: ds.data,
                            yAxisID: 'z',
                        }))
                    );
                }

                const cfg = {
                    // Bubble: type lives on each dataset, not at root
                    ...(!isBubble && !(isMixed || isMixed_notStacked) && {
                        type: chartArray[sample.chartType]
                    }),

                    data: {
                        // Bubble and pure scatter use {x,y,r} — no labels array
                        ...(isMatrix || isBubble ? {} : { labels: sample.x }),
                        datasets
                    },

                    options: {
                        animations: false,
                        responsive: true,

                        plugins: {
                            legend: {
                                labels: {
                                    color: LIGHT_TEXT,
                                    padding: 12,
                                    usePointStyle: true,
                                    pointStyleWidth: 8,
                                }
                            },
                            title: {
                                display: true,
                                // Backend now populates sample.title for new chart types
                                text: sample.title || "My Chart",
                                color: LIGHT_TITLE,
                                font: { size: 12, weight: '600', family: "'DM Sans', system-ui, sans-serif" },
                                padding: { top: 10, bottom: 6 },
                            }
                        },

                        scales: isMatrix
                            ? {
                                x: {
                                    type: "category",
                                    labels: sample.x,
                                    offset: true,
                                    ...axisStyle
                                },
                                y: {
                                    type: "category",
                                    labels: sample.y.map(r => r.label),
                                    offset: true,
                                    ...axisStyle
                                }
                            }
                            : isBubble
                                // Bubble needs linear x+y, no category axis
                                ? {
                                    x: { type: 'linear', ...axisStyle },
                                    y: { type: 'linear', ...axisStyle }
                                }
                                : isBoxPlot
                                    // Box plot: offset:true gives half-category padding on each side so
                                    // edge boxes aren't clipped by the chart area boundary
                                    ? {
                                        x: { type: 'category', labels: sample.x, offset: true, ...axisStyle, grid: { ...axisStyle.grid, display: false } },
                                        y: { position: 'left', ...axisStyle }
                                    }
                                    : isMixed
                                        ? {
                                            x: stackedAxis,
                                            y: {
                                                position: "left",
                                                ...stackedAxis
                                            }
                                        }
                                        : isPie
                                            ? {
                                                x: { display: false },
                                                y: { display: false }
                                            }
                                            : {
                                                x: {
                                                    ...axisStyle,
                                                    ...(isBar && !isHorizontalBar && { grid: { ...axisStyle.grid, display: false } })
                                                },
                                                y: {
                                                    position: "left",
                                                    ...axisStyle,
                                                    ...(isHorizontalBar && { grid: { ...axisStyle.grid, display: false } })
                                                }
                                            }
                    }
                };

                if (isBoxPlot && boxData) {
                    // Inline plugin: only active on this chart instance
                    cfg.plugins = [{
                        id: 'lqBoxPlot',
                        afterDatasetsDraw: (chart) => drawBoxPlot(chart, boxData),
                    }];
                    cfg.options.plugins.legend = { display: false };
                    cfg.options.interaction = { mode: 'index', axis: 'x', intersect: false };
                    cfg.options.plugins.tooltip = {
                        callbacks: {
                            title: (items) => boxData.labels[items[0]?.dataIndex ?? 0] ?? '',
                            label: (ctx) => {
                                const names = ['Min', 'Q1', 'Median', 'Q3', 'Max'];
                                const val   = ctx.raw?.y ?? ctx.raw;
                                const fmt   = v => typeof v === 'number'
                                    ? v.toLocaleString(undefined, { maximumFractionDigits: 4 })
                                    : String(v);
                                return ` ${names[ctx.datasetIndex] ?? ''}:  ${fmt(val)}`;
                            },
                            labelColor: () => ({
                                borderColor:     'rgba(233,150,122,0.8)',
                                backgroundColor: 'rgba(233,150,122,0.3)',
                                borderRadius: 2,
                            }),
                        }
                    };
                }

                if (isMatrix) {
                    cfg.options.plugins = cfg.options.plugins || {};
                    cfg.options.plugins.tooltip = cfg.options.plugins.tooltip || {};
                    cfg.options.plugins.tooltip.callbacks =
                        cfg.options.plugins.tooltip.callbacks || {};

                    cfg.options.plugins.tooltip.callbacks.title = (items) => {
                        const d = items[0].raw;
                        return `${d.y} / ${d.x}`;
                    };

                    cfg.options.plugins.tooltip.callbacks.label = (ctx) => {
                        const v = ctx.raw.v;
                        const isCorr = (sample.title || '').toLowerCase().includes('correlation');
                        return isCorr ? `r = ${v.toFixed(2)}` : `Value: ${v}`;
                    };

                    // Correlation matrices: cell text + color bar
                    const isCorr = (sample.title || '').toLowerCase().includes('correlation');
                    if (isCorr) {
                        cfg.options.layout = { padding: { right: 62 } };

                        cfg.plugins = [{
                            id: 'corrCellText',
                            afterDraw(chart) {
                                const { ctx, data: { datasets }, chartArea: ca } = chart;
                                const meta = chart.getDatasetMeta(0);

                                // ── Cell values ───────────────────────────
                                datasets[0].data.forEach((d, i) => {
                                    const el = meta.data[i];
                                    if (!el) return;
                                    ctx.save();
                                    ctx.fillStyle = Math.abs(d.v) > 0.5 ? '#ffffff' : '#333333';
                                    ctx.font = 'bold 11px sans-serif';
                                    ctx.textAlign = 'center';
                                    ctx.textBaseline = 'middle';
                                    const cp = el.getCenterPoint ? el.getCenterPoint() : { x: el.x, y: el.y };
                                    ctx.fillText(d.v.toFixed(2), cp.x, cp.y);
                                    ctx.restore();
                                });

                                // ── Color bar ─────────────────────────────
                                const barX = ca.right + 14;
                                const barY = ca.top;
                                const barW = 14;
                                const barH = ca.bottom - ca.top;

                                const grad = ctx.createLinearGradient(0, barY, 0, barY + barH);
                                grad.addColorStop(0,   'rgba(178,24,43,0.92)');
                                grad.addColorStop(0.5, 'rgba(238,238,238,0.92)');
                                grad.addColorStop(1,   'rgba(58,76,192,0.92)');

                                ctx.save();
                                ctx.beginPath();
                                if (ctx.roundRect) ctx.roundRect(barX, barY, barW, barH, 3);
                                else               ctx.rect(barX, barY, barW, barH);
                                ctx.fillStyle   = grad;
                                ctx.strokeStyle = 'rgba(255,255,255,0.15)';
                                ctx.lineWidth   = 0.5;
                                ctx.fill();
                                ctx.stroke();

                                // Tick marks and labels
                                const tickColor = chart.options.scales?.x?.ticks?.color ?? 'white';
                                const tickAlpha = tickColor === 'white' ? '0.75' : '0.8';
                                const lineAlpha = tickColor === 'white' ? '0.5'  : '0.4';
                                ctx.fillStyle   = tickColor === 'white'
                                    ? `rgba(255,255,255,${tickAlpha})`
                                    : `rgba(0,0,0,${tickAlpha})`;
                                ctx.font        = '10px sans-serif';
                                ctx.textAlign   = 'left';
                                ctx.textBaseline = 'middle';
                                [1, 0.5, 0, -0.5, -1].forEach(v => {
                                    const ty = barY + (1 - (v + 1) / 2) * barH;
                                    ctx.beginPath();
                                    ctx.strokeStyle = tickColor === 'white'
                                        ? `rgba(255,255,255,${lineAlpha})`
                                        : `rgba(0,0,0,${lineAlpha})`;
                                    ctx.lineWidth   = 1;
                                    ctx.moveTo(barX + barW,     ty);
                                    ctx.lineTo(barX + barW + 4, ty);
                                    ctx.stroke();
                                    ctx.fillText(v === 0 ? '0' : v.toFixed(1), barX + barW + 7, ty);
                                });

                                ctx.restore();
                            }
                        }];
                    }
                }

                if (!isMatrix && !isBubble && sample.z?.length > 0) {
                    cfg.options.scales.z = {
                        position: 'right',
                        ...axisStyle,
                    };
                }

                if (sample.chartType === 6) {
                    cfg.options.indexAxis = 'y';
                }

                if (chartArray[sample.chartType] === 'scatter') {
                    cfg.options.plugins.tooltip = { enabled: false };
                }

                const chartInstance = new Chart(ctx, cfg);
                chartInstances.push(chartInstance);
            });

            // Delegate DOM append + header wiring + grid registration
            appendChart(panelSamples, pIdx, wrappers, panelDivCopy);
        });

        // Refresh chart sizes after Muuri has laid out all items
        muuriGrid.on('layoutEnd', () => {
            chartInstances.forEach(c => c.resize());
        });
        muuriGrid.layout();
        console.log(`[autoChart] Layout triggered for ${Object.keys(groupedResults).length} panel(s).`);
    }
    catch (error) {
        if (error.name !== 'AbortError') {
            console.error("[autoChart] Fetch error:", error);
        }
    }
}

// ── Navigator ─────────────────────────────────────────────────────────────────

/** Normalise a dataset's data array so the navigator always gets plain numbers. */
function _navNormalise(data) {
    return data.map(d =>
        d === null || d === undefined ? null
            : typeof d === 'object' && 'y' in d ? d.y
                : typeof d === 'number'           ? d
                    : null
    );
}

/** Returns the pixel width of the navigator track. */
function _navTrackWidth() {
    return document.getElementById('navTrack').getBoundingClientRect().width;
}

/**
 * Apply a new selection window (both % values 0-100) and update the
 * main chart's x.min / x.max accordingly.
 */
function _navUpdate(leftPct, widthPct, mainChart) {
    // Clamp
    widthPct = Math.max(4, widthPct);
    leftPct  = Math.max(0, leftPct);
    if (leftPct + widthPct > 100) {
        if (_navDragState?.type === 'left') leftPct  = 100 - widthPct;
        else                                widthPct = 100 - leftPct;
    }
    leftPct = Math.max(0, leftPct);

    // Reposition DOM elements
    const win = document.getElementById('navWindow');
    win.style.left  = `${leftPct}%`;
    win.style.width = `${widthPct}%`;
    document.getElementById('navMaskLeft').style.width  = `${leftPct}%`;
    document.getElementById('navMaskRight').style.width = `${100 - leftPct - widthPct}%`;

    // Drive the main chart's x-axis
    const labels = mainChart?.data?.labels;
    if (!labels?.length) return;

    const n   = labels.length;
    const lo  = Math.round(leftPct / 100 * n);
    const hi  = Math.min(n - 1, Math.round((leftPct + widthPct) / 100 * n) - 1);
    mainChart.options.scales.x.min = lo;
    mainChart.options.scales.x.max = Math.max(lo, hi);
    mainChart.update('none');
}

/**
 * Wire up mouse drag events for the navigator window and handles.
 * Must be called once per openZoom(); cleaned up by _navDestroy().
 */
function _navSetupDrag(mainChart) {
    const win         = document.getElementById('navWindow');
    const leftHandle  = document.getElementById('navHandleLeft');
    const rightHandle = document.getElementById('navHandleRight');
    const track       = document.getElementById('navTrack');

    function curPos() {
        return {
            leftPct:  parseFloat(win.style.left)  || 0,
            widthPct: parseFloat(win.style.width) || 100,
        };
    }

    function startDrag(type, e) {
        e.stopPropagation();
        e.preventDefault();
        const clientX = e.touches ? e.touches[0].clientX : e.clientX;
        const { leftPct, widthPct } = curPos();
        _navDragState = { type, startX: clientX, startLeft: leftPct, startWidth: widthPct };
        document.body.style.setProperty('user-select', 'none', 'important');
    }

    const onMove = (e) => {
        if (!_navDragState) return;
        const clientX = e.touches ? e.touches[0].clientX : e.clientX;
        const dx = (clientX - _navDragState.startX) / _navTrackWidth() * 100;
        const { startLeft, startWidth, type } = _navDragState;

        if      (type === 'move')  _navUpdate(startLeft + dx, startWidth,       mainChart);
        else if (type === 'left')  _navUpdate(startLeft + dx, startWidth - dx,  mainChart);
        else if (type === 'right') _navUpdate(startLeft,       startWidth + dx, mainChart);
    };

    const onUp = () => {
        _navDragState = null;
        document.body.style.removeProperty('user-select');
    };

    // Click on the masked areas to jump the window centre
    const onTrackClick = (e) => {
        if (_navDragState) return;
        const id = e.target?.id;
        if (id !== 'navMaskLeft' && id !== 'navMaskRight' && id !== 'navTrack') return;
        const rect    = track.getBoundingClientRect();
        const clickPct = (e.clientX - rect.left) / rect.width * 100;
        const { widthPct } = curPos();
        _navUpdate(clickPct - widthPct / 2, widthPct, mainChart);
    };

    leftHandle.addEventListener('mousedown',  (e) => startDrag('left',  e));
    rightHandle.addEventListener('mousedown', (e) => startDrag('right', e));
    win.addEventListener('mousedown', (e) => {
        if (e.target === leftHandle || leftHandle.contains(e.target))  return;
        if (e.target === rightHandle || rightHandle.contains(e.target)) return;
        startDrag('move', e);
    });
    track.addEventListener('click', onTrackClick);

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup',   onUp);

    // Store cleanup so _navDestroy can remove global listeners
    _navChartInst._cleanupNav = () => {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup',   onUp);
        track.removeEventListener('click', onTrackClick);
    };
}

/**
 * Create the navigator mini-chart and show the navigator bar.
 * Call this from openZoom() after the main chart has been resized.
 */
function _navInit(mainChart) {
    const container = document.getElementById('zoomNavigator');
    const canvas    = document.getElementById('navigatorCanvas');
    const labels    = mainChart?.data?.labels;

    // Navigator only makes sense for labelled (category) charts
    if (!labels?.length || mainChart.config.type === 'matrix') {
        container.classList.add('hidden');
        return;
    }

    if (_navChartInst) {
        _navChartInst._cleanupNav?.();
        _navChartInst.destroy();
        _navChartInst = null;
    }

    // Build simplified datasets — always line/area regardless of original type
    const navDatasets = mainChart.data.datasets.map(ds => ({
        type:            'line',
        label:           ds.label,
        data:            _navNormalise(Array.isArray(ds.data) ? ds.data : []),
        borderColor:     'rgba(233,150,122,0.75)',
        backgroundColor: 'rgba(233,150,122,0.12)',
        borderWidth:     1.5,
        pointRadius:     0,
        fill:            true,
        tension:         0.35,
    }));

    _navChartInst = new Chart(canvas, {
        type: 'line',
        data: { labels, datasets: navDatasets },
        options: {
            responsive:           true,
            maintainAspectRatio:  false,
            animation:            false,
            plugins: {
                legend:  { display: false },
                tooltip: { enabled: false },
            },
            scales: {
                x: { ticks: { display: false }, grid: { display: false }, border: { display: false } },
                y: { ticks: { display: false }, grid: { display: false }, border: { display: false } },
            },
        },
    });

    // Full-range selection on open
    _navUpdate(0, 100, mainChart);
    container.classList.remove('hidden');
    _navSetupDrag(mainChart);
}

/** Tear down the navigator and reset the main chart's x-axis constraints. */
function _navDestroy(mainChart) {
    if (_navChartInst) {
        _navChartInst._cleanupNav?.();
        _navChartInst.destroy();
        _navChartInst = null;
    }
    _navDragState = null;
    document.getElementById('zoomNavigator').classList.add('hidden');

    // Remove x-axis clamps so the chart renders fully when returned to the grid
    if (mainChart?.options?.scales?.x) {
        delete mainChart.options.scales.x.min;
        delete mainChart.options.scales.x.max;
    }
}

// ── Zoom overlay ──────────────────────────────────────────────────────────────

export function openZoom(wrapper) {
    // Close any open menus first
    document.querySelectorAll('.plot-menu, .plot-colormap').forEach(m => m.classList.add('hidden'));

    const overlay = document.getElementById('zoomOverlay');
    const content = document.getElementById('zoomContent');

    _zoomedWrapper  = wrapper;
    _zoomedParent   = wrapper.parentElement;
    _zoomedSibling  = wrapper.nextElementSibling;
    _zoomedCssText  = wrapper.style.cssText;          // snapshot all inline styles

    document.getElementById('zoomTitle').textContent =
        wrapper.dataset.chartTitle || 'Chart';

    content.appendChild(wrapper);
    wrapper.style.cssText = 'display:flex; flex-direction:column; width:100%; height:100%;';

    overlay.classList.remove('hidden');
    document.addEventListener('keydown', _onEscClose);

    requestAnimationFrame(() => {
        const chart = Chart.getChart(wrapper.querySelector('canvas'));
        if (chart) {
            chart.resize();
            _applyChartTheme(chart, 'light');  // zoom overlay is light bg
            _navInit(chart);   // show the navigator now that the chart is live
        }
    });
}

export function closeZoom() {
    if (!_zoomedWrapper) return;

    // Capture the chart reference before we move the wrapper back
    const chartBeforeMove = Chart.getChart(_zoomedWrapper.querySelector('canvas'));
    _navDestroy(chartBeforeMove);   // tear down navigator + reset x-axis clamps

    document.getElementById('zoomOverlay').classList.add('hidden');
    document.removeEventListener('keydown', _onEscClose);

    if (_zoomedSibling) {
        _zoomedParent.insertBefore(_zoomedWrapper, _zoomedSibling);
    } else {
        _zoomedParent.appendChild(_zoomedWrapper);
    }
    _zoomedWrapper.style.cssText = _zoomedCssText;

    const wrapper = _zoomedWrapper; // ← capture before nullifying

    _zoomedWrapper = _zoomedParent = _zoomedSibling = null;
    _zoomedCssText = '';

    requestAnimationFrame(() => {
        const chart = Chart.getChart(wrapper.querySelector('canvas')); // ← safe local ref
        if (chart) {
            chart.resize();
            _applyChartTheme(chart, 'light');  // card is light bg
        }
        if (muuriGrid) muuriGrid.refreshItems().layout();
    });
}

function _onEscClose(e) {
    if (e.key === 'Escape') closeZoom();
}

// ── appendChart ───────────────────────────────────────────────────────────────
// Responsible for everything that happens AFTER a panel's charts are created:
//   1. Build ONE panel-level header toolbar (always in the DOM, never hidden)
//   2. Wrap the panel content in a Muuri grid item shell
//   3. Register the item with muuriGrid
//
// KEY DESIGN: The drag-handle lives on the single panel header, NOT on the
// individual chart wrappers. Previously each wrapper had its own header with
// drag-handle, so hiding a wrapper also hid its header. Muuri's querySelector
// could then resolve to a hidden element and treat the item as immovable.
// With one persistent header the drag-handle is always present and visible.
function appendChart(panelSamples, pIdx, wrappers, panelDivCopy) {
    // ── 1. Build ONE panel-level header (always visible, owns drag-handle) ─
    const header = document.createElement("div");
    header.className = "drag-handle flex justify-between items-center px-3 py-1.5 border-b border-zinc-100 bg-white/90 backdrop-blur-sm relative z-50 select-none cursor-grab active:cursor-grabbing rounded-t-2xl";

    // Drag grip icon (left side — visual affordance)
    const grip = document.createElement("div");
    grip.className = "text-zinc-300 hover:text-zinc-500 transition-colors pointer-events-none";
    grip.innerHTML = `
        <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
            <circle cx="9"  cy="5"  r="1.5"/><circle cx="15" cy="5"  r="1.5"/>
            <circle cx="9"  cy="12" r="1.5"/><circle cx="15" cy="12" r="1.5"/>
            <circle cx="9"  cy="19" r="1.5"/><circle cx="15" cy="19" r="1.5"/>
        </svg>`;

    const container = document.createElement("div");
    container.className = "flex items-center justify-end gap-2";

    // ── Background color button ───────────────────────────────────────────
    const bgbtnWrapper = document.createElement("div");
    bgbtnWrapper.className = "relative flex flex-col items-end";

    const bgbtn = document.createElement("button");
    bgbtn.className = "hover:cursor-pointer text-[11px] tracking-wide px-2.5 py-1 rounded-md border border-zinc-200 text-zinc-400 hover:text-[#E9967A] hover:border-[#E9967A]/40 bg-white hover:bg-zinc-50 transition-all flex items-center gap-1.5 focus:outline-none";
    bgbtn.innerHTML = `
        <svg class="w-3 h-3 opacity-70" fill="none" stroke="currentColor" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><path d="M12 2a10 10 0 0 1 0 20" stroke-linecap="round"/></svg>
        Bg color
        <svg class="w-2 h-2 transition-transform duration-200" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
        </svg>`;

    const colormap = document.createElement("div");
    colormap.className = "hidden absolute top-full right-0 mt-1.5 min-w-[150px] p-3 bg-white border border-zinc-200 rounded-xl shadow-xl shadow-black/8 z-30 flex flex-col gap-2 backdrop-blur-sm";
    colormap.innerHTML = `
        <input type="color" id="hiddenPicker-${pIdx}" class="w-full h-8 cursor-pointer rounded-md border-0 border-none bg-transparent" value="#ffffff">
        <input id="hexInput-${pIdx}" class="w-full bg-zinc-50 text-zinc-600 px-3 py-1.5 rounded-lg outline-none text-xs text-center border border-zinc-200 focus:border-[#E9967A]/60 transition-colors" placeholder="#ffffff">`;

    colormap.onclick = (e) => e.stopPropagation();

    const hiddenPicker = colormap.querySelector('input[type="color"]');
    const hexInput     = colormap.querySelector(`#hexInput-${pIdx}`);

    // Always apply color to whichever wrapper is currently visible
    const getActiveCanvas = () => {
        const active = wrappers.find(w => w.style.display !== 'none') ?? wrappers[0];
        return active.querySelector('canvas');
    };

    hiddenPicker.oninput = (e) => {
        const color = e.target.value;
        hexInput.value = color;
        reformatColor(color, getActiveCanvas());
    };
    hexInput.oninput = (e) => {
        const color = e.target.value;
        if (/^#[0-9A-F]{6}$/i.test(color)) {
            hiddenPicker.value = color;
            reformatColor(color, getActiveCanvas());
        }
    };

    colormap.classList.add('plot-colormap');
    bgbtn.classList.add('plot-bgbtn');

    bgbtn.onclick = (e) => {
        e.stopPropagation();
        const isHidden = colormap.classList.contains("hidden");
        document.querySelectorAll('.plot-menu, .plot-colormap').forEach(m => m.classList.add('hidden'));
        document.querySelectorAll('.plot-btn svg:last-of-type, .plot-bgbtn svg:last-of-type').forEach(s => s.classList.remove('rotate-180'));
        if (isHidden) {
            colormap.classList.remove("hidden");
            bgbtn.querySelectorAll('svg')[bgbtn.querySelectorAll('svg').length - 1].classList.add('rotate-180');
        }
    };

    bgbtnWrapper.appendChild(bgbtn);
    bgbtnWrapper.appendChild(colormap);

    // ── "Turn into" button (only when panel has multiple chart variants) ──
    if (panelSamples.length > 1) {
        const btnWrapper = document.createElement("div");
        btnWrapper.className = "relative flex flex-col items-end";

        const btn = document.createElement("button");
        btn.className = "hover:cursor-pointer text-[11px] tracking-wide px-2.5 py-1 rounded-md border border-zinc-200 text-zinc-400 hover:text-[#E9967A] hover:border-[#E9967A]/40 bg-white hover:bg-zinc-50 transition-all flex items-center gap-1.5";
        btn.innerHTML = `
            <svg class="w-3 h-3 opacity-70" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 16V4m0 0L3 8m4-4l4 4M17 8v12m0 0l4-4m-4 4l-4-4"/></svg>
            Turn into
            <svg class="w-2 h-2 transition-transform duration-200" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
            </svg>`;

        const menu = document.createElement("div");
        menu.className = "hidden absolute top-full right-0 mt-1.5 min-w-[140px] w-max max-w-[220px] bg-white border border-zinc-200 rounded-xl shadow-xl shadow-black/8 py-1.5 z-30 backdrop-blur-sm";

        panelSamples.forEach((targetSample, tIdx) => {
            const item = document.createElement("button");
            item.className = `w-full text-left px-3 py-1.5 text-xs transition-colors truncate ${tIdx === 0 ? 'text-[#E9967A] font-semibold bg-zinc-50' : 'text-zinc-500 hover:bg-zinc-100 hover:text-[#18181B]'}`;
            item.innerText = targetSample.title || `Chart ${tIdx + 1}`;
            item.onclick = (e) => {
                e.stopPropagation();

                // Show the target wrapper, hide all others
                wrappers.forEach((w, i) => {
                    w.style.display = i === tIdx ? 'flex' : 'none';
                });

                // Sync active-item styles in the dropdown
                menu.querySelectorAll('button').forEach((b, i) => {
                    b.className = `w-full text-left px-3 py-1.5 text-xs transition-colors truncate ${i === tIdx ? 'text-[#E9967A] font-semibold bg-zinc-50' : 'text-zinc-500 hover:bg-zinc-100 hover:text-[#18181B]'}`;
                });

                menu.classList.add("hidden");
                { const svgs = btn.querySelectorAll('svg'); svgs[svgs.length - 1].classList.remove('rotate-180'); }

                // Resize the newly visible chart so it fills the panel correctly
                const chart = Chart.getChart(wrappers[tIdx].querySelector('canvas'));
                if (chart) setTimeout(() => chart.resize(), 0);
            };
            menu.appendChild(item);
        });

        menu.classList.add('plot-menu');
        btn.classList.add('plot-btn');

        btn.onclick = (e) => {
            e.stopPropagation();
            const isHidden = menu.classList.contains("hidden");
            document.querySelectorAll('.plot-menu, .plot-colormap').forEach(m => m.classList.add('hidden'));
            document.querySelectorAll('.plot-btn svg:last-of-type, .plot-bgbtn svg:last-of-type').forEach(s => s.classList.remove('rotate-180'));
            if (isHidden) {
                menu.classList.remove("hidden");
                { const svgs = btn.querySelectorAll('svg'); svgs[svgs.length - 1].classList.add('rotate-180'); }
            }
        };

        btnWrapper.appendChild(btn);
        btnWrapper.appendChild(menu);
        container.appendChild(btnWrapper);
    }

    // ── Zoom button ───────────────────────────────────────────────────────
    const zoomBtn = document.createElement("button");
    zoomBtn.className = "hover:cursor-pointer p-1.5 rounded-md border border-zinc-200 text-zinc-400 hover:text-[#E9967A] hover:border-[#E9967A]/40 bg-white hover:bg-zinc-50 transition-all flex items-center";
    zoomBtn.title = "Fullscreen";
    zoomBtn.innerHTML = `
    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5
                 M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4"/>
    </svg>`;
    zoomBtn.onclick = (e) => {
        e.stopPropagation();
        const activeWrapper = wrappers.find(w => w.style.display !== 'none') ?? wrappers[0];
        openZoom(activeWrapper);
    };

    container.appendChild(zoomBtn);
    container.appendChild(bgbtnWrapper);
    header.appendChild(grip);
    header.appendChild(container);

    // Prepend the single header to the panel div (before all chart wrappers)
    panelDivCopy.prepend(header);

    // ── 2. Wrap in Muuri grid item shell ──────────────────────────────────
    // Structure: .grid-item > .grid-item-content > panelDivCopy
    // .grid-item      — Muuri positions this absolutely
    // .grid-item-content — provides padding, border, rounded corners
    const gridItem = document.createElement("div");
    gridItem.className = "grid-item";

    const gridItemContent = document.createElement("div");
    gridItemContent.className = "grid-item-content";
    gridItemContent.appendChild(panelDivCopy);
    gridItem.appendChild(gridItemContent);

    // ── 3. Register with Muuri ────────────────────────────────────────────
    // appendChildToGrid=false: we mount the DOM element ourselves so Muuri
    // doesn't double-append it. Then we call add() to let Muuri track it.
    document.getElementById("chartGrid").appendChild(gridItem);
    muuriGrid.add(gridItem, { layout: false }); // layout:false — batch; called once after all panels
    console.log(`[appendChart] Panel ${pIdx} added to grid (${panelSamples.length} variant(s)).`);
}
