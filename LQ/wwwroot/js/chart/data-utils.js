// @ts-check
/**
 * @file Per-chart-type data preparation helpers.
 *
 * - `buildHeatmapData` flattens a 2-D matrix into the {x, y, v} list chartjs-chart-matrix wants.
 * - `buildMatrixDataset` wraps that list into a Chart.js dataset with auto-cell-sizing and a
 *   colour ramp. If the chart's title contains "correlation" it switches to a coolwarm scale.
 * - `drawBoxPlot` is a custom canvas-drawing pass invoked from a Chart.js plugin hook (no
 *   built-in box-plot type in Chart.js 4).
 */

/**
 * @param {string[]} xLabels
 * @param {{ label: string, data: (number | null)[] }[]} ySeries
 */
export function buildHeatmapData(xLabels, ySeries) {
  return ySeries.flatMap((row) =>
    row.data.map((value, colIndex) => ({
      x: xLabels[colIndex],
      y: row.label,
      v: value ? value : 0,
    }))
  );
}

/**
 * Build the dataset list for chartjs-chart-matrix. Cell size auto-fits chart area / count.
 * @param {{ x: string[], y: { label: string, data: (number | null)[] }[], title?: string }} sample
 */
export function buildMatrixDataset(sample) {
  const heatmapData = buildHeatmapData(sample.x, sample.y);

  const values = heatmapData.map((d) => d.v);
  const minVal = 0;
  const maxVal = Math.max(...values);

  const xCount = new Set(heatmapData.map((d) => d.x)).size;
  const yCount = new Set(heatmapData.map((d) => d.y)).size;

  // Detect correlation matrix by title — switches to a coolwarm scale.
  const isCorr = (sample.title || '').toLowerCase().includes('correlation');

  // Muted coolwarm: steel-blue → off-white → crimson (softer than pure blue/white/red)
  /** @param {number} v */
  function corrToColor(v) {
    const t = (Math.max(-1, Math.min(1, v)) + 1) / 2;
    let r, g, b;
    if (t < 0.5) {
      const f = t / 0.5;
      r = Math.round(58 + f * (238 - 58));
      g = Math.round(76 + f * (238 - 76));
      b = Math.round(192 + f * (238 - 192));
    } else {
      const f = (t - 0.5) / 0.5;
      r = Math.round(238 + f * (178 - 238));
      g = Math.round(238 + f * (24 - 238));
      b = Math.round(238 + f * (43 - 238));
    }
    return `rgba(${r},${g},${b},0.92)`;
  }

  return [
    {
      type: 'matrix',
      label: sample.title ?? 'Heatmap',
      data: heatmapData,
      borderColor: 'rgba(255,255,255,0.12)',
      borderWidth: 1,

      backgroundColor(/** @type {any} */ ctx) {
        const v = ctx.raw.v;
        if (isCorr) return corrToColor(v);
        const t = (v - minVal) / (maxVal - minVal || 1);
        return `rgba(0,123,255,${t})`;
      },

      width(/** @type {{ chart: any }} */ { chart }) {
        const a = chart.chartArea;
        return a ? (a.right - a.left) / xCount - 2 : 0;
      },

      height(/** @type {{ chart: any }} */ { chart }) {
        const a = chart.chartArea;
        return a ? (a.bottom - a.top) / yCount - 2 : 0;
      },
    },
  ];
}

/**
 * Custom canvas pass for box-plot rendering — invoked from a Chart.js plugin's afterDraw hook.
 * @param {any} chart
 * @param {{ labels: string[], min: number[], q1: number[], med: number[], q3: number[], max: number[] }} data
 */
export function drawBoxPlot(chart, data) {
  const { labels, min, q1, med, q3, max } = data;
  const c = chart.ctx;
  const xs = chart.scales.x;
  const ys = chart.scales.y;
  if (!xs || !ys || !labels.length) return;

  const n = labels.length;
  const catW =
    n > 1
      ? Math.abs(xs.getPixelForValue(labels[1]) - xs.getPixelForValue(labels[0]))
      : chart.chartArea.right - chart.chartArea.left;
  const half = catW * 0.28; // IQR box half-width
  const cap = catW * 0.13; // whisker cap half-width

  c.save();
  c.beginPath();
  c.rect(
    chart.chartArea.left,
    chart.chartArea.top,
    chart.chartArea.right - chart.chartArea.left,
    chart.chartArea.bottom - chart.chartArea.top
  );
  c.clip();

  labels.forEach((label, i) => {
    const cx = xs.getPixelForValue(label);
    const yMin = ys.getPixelForValue(min[i]);
    const yQ1 = ys.getPixelForValue(q1[i]);
    const yMed = ys.getPixelForValue(med[i]);
    const yQ3 = ys.getPixelForValue(q3[i]);
    const yMax = ys.getPixelForValue(max[i]);

    const boxTop = Math.min(yQ1, yQ3);
    const boxBot = Math.max(yQ1, yQ3);
    const boxH = Math.max(boxBot - boxTop, 1);

    // Whisker lines: Max→Q3 and Q1→Min
    c.beginPath();
    c.strokeStyle = 'rgba(54,162,235,0.75)';
    c.lineWidth = 1.5;
    c.moveTo(cx, yMax);
    c.lineTo(cx, boxTop);
    c.moveTo(cx, boxBot);
    c.lineTo(cx, yMin);
    c.stroke();

    // T-caps at Max and Min
    c.beginPath();
    c.moveTo(cx - cap, yMax);
    c.lineTo(cx + cap, yMax);
    c.moveTo(cx - cap, yMin);
    c.lineTo(cx + cap, yMin);
    c.stroke();

    // IQR box (Q1–Q3)
    const r = Math.min(3, half * 0.1, boxH * 0.1);
    c.beginPath();
    if (c.roundRect) {
      c.roundRect(cx - half, boxTop, half * 2, boxH, r);
    } else {
      c.rect(cx - half, boxTop, half * 2, boxH);
    }
    c.fillStyle = 'rgba(54,162,235,0.22)';
    c.strokeStyle = 'rgba(54,162,235,0.9)';
    c.lineWidth = 1.5;
    c.fill();
    c.stroke();

    // Median line — bright cyan stands out against the blue box
    c.beginPath();
    c.strokeStyle = 'rgb(0,220,255)';
    c.lineWidth = 2.5;
    c.moveTo(cx - half, yMed);
    c.lineTo(cx + half, yMed);
    c.stroke();
  });

  c.restore();
}
