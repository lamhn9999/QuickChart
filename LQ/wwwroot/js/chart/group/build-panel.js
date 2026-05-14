// @ts-check
/**
 * @file Build a panel of Chart.js variants for a single ChartType-detection result.
 *
 * Each "sample" in `panelSamples` describes one chart variant (e.g. histogram and box-plot
 * are both rendered for a numeric column; the user toggles between them via the "Turn into"
 * deck). The first sample is shown; the rest are hidden but live (kept in the DOM so toggling
 * is instant).
 *
 * After all variants are instantiated, `appendChart()` is called to wrap the panel in the
 * shared chart-card header (drag handle, bg-color, zoom, copy-to-file, remove buttons).
 *
 * Splitting this further along chart-type branches is a future polish item — every branch
 * inside `panelSamples.forEach` is clearly delimited by `isMatrix` / `isPie` / `isBoxPlot` /
 * `isMatrix && isCorr` etc. flags.
 */

import { chartInstances } from '../state.js';
import { axisStyle, stackedAxis, LIGHT_TEXT, LIGHT_TITLE, LIGHT_GRID } from '../theme.js';
import { buildMatrixDataset, drawBoxPlot } from '../data-utils.js';
import { chartArray } from './types.js';
import { appendChart } from './builder.js';

/**
 * @param {any[]} panelSamples
 * @param {number} pIdx
 * @param {HTMLElement} gridContainer
 * @param {number | null} [groupId]
 * @param {number[] | null} [fieldIndexes]
 * @param {string | null} [csvFileToken]
 * @param {any[] | null} [savedSeriesState]
 */
export function buildPanel(
  panelSamples,
  pIdx,
  gridContainer,
  groupId = null,
  fieldIndexes = null,
  csvFileToken = null,
  savedSeriesState = null
) {
  const panelDiv = document.createElement('div');
  panelDiv.classList.add('flex', 'flex-col');
  panelDiv.id = 'panel' + pIdx;

  /** @type {HTMLElement[]} */
  const wrappers = [];

  panelSamples.forEach((sample, sIdx) => {
    const wrapper = document.createElement('div');
    wrapper.className = 'flex flex-col relative';
    if (sIdx > 0) wrapper.style.display = 'none';

    wrapper.dataset.chartTitle = sample.title || `Chart ${sIdx + 1}`;
    wrapper.dataset.chartType = String(sample.chartType);

    const ctx = document.createElement('canvas');
    wrapper.appendChild(ctx);
    panelDiv.appendChild(wrapper);
    wrappers.push(wrapper);

    const isMatrix = chartArray[sample.chartType] === 'matrix';
    const isMixed = sample.chartType === 7;
    const isMixed_notStacked = sample.chartType === 9;
    const isBubble = sample.chartType === 10;
    const isArea = sample.chartType === 11;
    const isRadar = sample.chartType === 12;
    const isBar = chartArray[sample.chartType] === 'bar';
    const isPie = chartArray[sample.chartType] === 'pie';
    const isLine = chartArray[sample.chartType] === 'line' && !isArea && !isRadar;
    const isHorizontalBar = sample.chartType === 6;

    if (isPie) {
      wrapper.removeChild(ctx);
      wrapper.classList.add('items-center', 'justify-center');
      const pieBox = document.createElement('div');
      pieBox.style.cssText = 'width:100%;position:relative;flex-shrink:0';
      pieBox.appendChild(ctx);
      wrapper.appendChild(pieBox);
    }

    const isBoxPlot =
      !isMatrix && !isBubble && sample.y.length >= 5 && sample.y[2]?.label === 'Median';

    const datasets = isMatrix
      ? buildMatrixDataset(sample)
      : isBubble
        ? sample.y.map((/** @type {any} */ ds) => ({
            type: 'bubble',
            label: ds.label,
            data: ds.data,
          }))
        : [
            ...sample.y.map((/** @type {any} */ ds) => {
              const base = {
                ...((isMixed || isMixed_notStacked) && {
                  type: chartArray[ds.chartType],
                }),
                label: ds.label,
                data: ds.data,
                ...(!isRadar && { yAxisID: 'y' }),
                ...(isBar && { borderRadius: 4 }),
                ...(isLine && {
                  tension: 0.3,
                  pointRadius: 3,
                  pointHoverRadius: 6,
                }),
                ...(isArea && {
                  fill: true,
                  stack: 'stack',
                  tension: 0.3,
                  pointRadius: 3,
                  pointHoverRadius: 6,
                }),
              };
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
            }),
          ];

    /** @type {{ labels: any[], min: number[], q1: number[], med: number[], q3: number[], max: number[] } | null} */
    let boxData = null;
    if (isBoxPlot) {
      const yVal = (/** @type {any} */ d) =>
        d !== null && typeof d === 'object' && 'y' in d ? d.y : d;
      boxData = {
        labels: sample.x,
        min: datasets[0].data.map(yVal),
        q1: datasets[1].data.map(yVal),
        med: datasets[2].data.map(yVal),
        q3: datasets[3].data.map(yVal),
        max: datasets[4].data.map(yVal),
      };
      datasets.forEach((/** @type {any} */ ds) => {
        ds.pointRadius = 0;
        ds.pointHoverRadius = 1;
        ds.showLine = false;
        ds.borderColor = 'transparent';
        ds.backgroundColor = 'transparent';
      });
    }

    if (!isMatrix && !isBubble && sample.z?.length > 0) {
      datasets.push(
        ...sample.z.map((/** @type {any} */ ds) => ({
          ...((isMixed || isMixed_notStacked) && {
            type: chartArray[ds.chartType],
          }),
          label: ds.label,
          data: ds.data,
          yAxisID: 'z',
        }))
      );
    }

    /** @type {any} */
    const cfg = {
      ...(!isBubble &&
        !(isMixed || isMixed_notStacked) && {
          type: chartArray[sample.chartType],
        }),
      data: {
        ...(isMatrix || isBubble ? {} : { labels: sample.x }),
        datasets,
      },
      options: {
        animations: false,
        responsive: true,
        maintainAspectRatio: true,
        aspectRatio: isMatrix
          ? 1
          : isPie || isBubble || isRadar
            ? 1.5
            : isHorizontalBar
              ? 1.2
              : 1.8,
        plugins: {
          legend: {
            ...(isPie && { position: 'right' }),
            labels: { color: LIGHT_TEXT, padding: 12, borderRadius: 4 },
          },
          title: {
            display: true,
            text: sample.title || 'My Chart',
            color: LIGHT_TITLE,
            font: { size: 12, weight: '600', family: "'DM Sans', system-ui, sans-serif" },
            padding: { top: 10, bottom: 6 },
          },
        },
        scales: isMatrix
          ? {
              x: { type: 'category', labels: sample.x, offset: true, ...axisStyle },
              y: {
                type: 'category',
                labels: sample.y.map((/** @type {any} */ r) => r.label),
                offset: true,
                ...axisStyle,
              },
            }
          : isBubble
            ? { x: { type: 'linear', ...axisStyle }, y: { type: 'linear', ...axisStyle } }
            : isBoxPlot
              ? {
                  x: {
                    type: 'category',
                    labels: sample.x,
                    offset: true,
                    ...axisStyle,
                    grid: { ...axisStyle.grid, display: false },
                  },
                  y: { position: 'left', ...axisStyle },
                }
              : isMixed
                ? { x: stackedAxis, y: { position: 'left', ...stackedAxis } }
                : isRadar
                  ? {
                      r: {
                        angleLines: { color: LIGHT_GRID },
                        grid: { color: LIGHT_GRID },
                        pointLabels: { color: LIGHT_TEXT, font: { size: 11 } },
                        ticks: {
                          color: LIGHT_TEXT,
                          backdropColor: 'transparent',
                          font: { size: 9 },
                        },
                        beginAtZero: true,
                      },
                    }
                  : isPie
                    ? { x: { display: false }, y: { display: false } }
                    : {
                        x: {
                          ...axisStyle,
                          ...(isBar &&
                            !isHorizontalBar && {
                              grid: { ...axisStyle.grid, display: false },
                            }),
                        },
                        y: {
                          position: 'left',
                          ...axisStyle,
                          ...(isHorizontalBar && {
                            grid: { ...axisStyle.grid, display: false },
                          }),
                        },
                      },
      },
    };

    if (isBoxPlot && boxData) {
      cfg.plugins = [
        {
          id: 'lqBoxPlot',
          afterDatasetsDraw: (/** @type {any} */ chart) => drawBoxPlot(chart, boxData),
        },
      ];
      cfg.options.plugins.legend = { display: false };
      cfg.options.interaction = { mode: 'index', axis: 'x', intersect: false };
      cfg.options.plugins.tooltip = {
        callbacks: {
          title: (/** @type {any[]} */ items) => boxData.labels[items[0]?.dataIndex ?? 0] ?? '',
          label: (/** @type {any} */ ctx) => {
            const names = ['Min', 'Q1', 'Median', 'Q3', 'Max'];
            const val = ctx.raw?.y ?? ctx.raw;
            const fmt = (/** @type {any} */ v) =>
              typeof v === 'number'
                ? v.toLocaleString(undefined, { maximumFractionDigits: 4 })
                : String(v);
            return ` ${names[ctx.datasetIndex] ?? ''}:  ${fmt(val)}`;
          },
          labelColor: () => ({
            borderColor: 'rgba(233,150,122,0.8)',
            backgroundColor: 'rgba(233,150,122,0.3)',
            borderRadius: 2,
          }),
        },
      };
    }

    if (isMatrix) {
      cfg.options.plugins = cfg.options.plugins || {};
      cfg.options.plugins.tooltip = cfg.options.plugins.tooltip || {};
      cfg.options.plugins.tooltip.callbacks = cfg.options.plugins.tooltip.callbacks || {};

      cfg.options.plugins.tooltip.callbacks.title = (/** @type {any[]} */ items) => {
        const d = items[0].raw;
        return `${d.y} / ${d.x}`;
      };
      cfg.options.plugins.tooltip.callbacks.label = (/** @type {any} */ ctx) => {
        const v = ctx.raw.v;
        const isCorr = (sample.title || '').toLowerCase().includes('correlation');
        return isCorr ? `r = ${v.toFixed(2)}` : `Value: ${v}`;
      };

      const isCorr = (sample.title || '').toLowerCase().includes('correlation');
      if (isCorr) {
        cfg.options.layout = { padding: { right: 62 } };
        cfg.plugins = [
          {
            id: 'corrCellText',
            afterDraw(/** @type {any} */ chart) {
              const {
                ctx,
                data: { datasets },
                chartArea: ca,
              } = chart;
              const meta = chart.getDatasetMeta(0);

              datasets[0].data.forEach((/** @type {any} */ d, /** @type {number} */ i) => {
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

              const barX = ca.right + 14;
              const barY = ca.top;
              const barW = 14;
              const barH = ca.bottom - ca.top;
              const grad = ctx.createLinearGradient(0, barY, 0, barY + barH);
              grad.addColorStop(0, 'rgba(178,24,43,0.92)');
              grad.addColorStop(0.5, 'rgba(238,238,238,0.92)');
              grad.addColorStop(1, 'rgba(58,76,192,0.92)');

              ctx.save();
              ctx.beginPath();
              if (ctx.roundRect) ctx.roundRect(barX, barY, barW, barH, 3);
              else ctx.rect(barX, barY, barW, barH);
              ctx.fillStyle = grad;
              ctx.strokeStyle = 'rgba(255,255,255,0.15)';
              ctx.lineWidth = 0.5;
              ctx.fill();
              ctx.stroke();

              const tickColor = chart.options.scales?.x?.ticks?.color ?? 'white';
              const tickAlpha = tickColor === 'white' ? '0.75' : '0.8';
              const lineAlpha = tickColor === 'white' ? '0.5' : '0.4';
              ctx.fillStyle =
                tickColor === 'white'
                  ? `rgba(255,255,255,${tickAlpha})`
                  : `rgba(0,0,0,${tickAlpha})`;
              ctx.font = '10px sans-serif';
              ctx.textAlign = 'left';
              ctx.textBaseline = 'middle';
              [1, 0.5, 0, -0.5, -1].forEach((v) => {
                const ty = barY + (1 - (v + 1) / 2) * barH;
                ctx.beginPath();
                ctx.strokeStyle =
                  tickColor === 'white'
                    ? `rgba(255,255,255,${lineAlpha})`
                    : `rgba(0,0,0,${lineAlpha})`;
                ctx.lineWidth = 1;
                ctx.moveTo(barX + barW, ty);
                ctx.lineTo(barX + barW + 4, ty);
                ctx.stroke();
                ctx.fillText(v === 0 ? '0' : v.toFixed(1), barX + barW + 7, ty);
              });
              ctx.restore();
            },
          },
        ];
      }
    }

    if (!isMatrix && !isBubble && sample.z?.length > 0) {
      cfg.options.scales.z = { position: 'right', ...axisStyle };
    }

    if (sample.chartType === 6) cfg.options.indexAxis = 'y';
    if (chartArray[sample.chartType] === 'scatter')
      cfg.options.plugins.tooltip = { enabled: false };

    // @ts-ignore — Chart is a runtime global
    const chartInstance = new Chart(ctx, cfg);
    chartInstances.push(chartInstance);

    // Initialize series state for zoom panel visibility toggles. When loaded from persistence,
    // apply the saved visibility state.
    chartInstance._lqSeriesState = chartInstance.data.datasets.map((/** @type {any} */ ds) => ({
      dataset: ds,
      visible: true,
    }));
    if (savedSeriesState && Array.isArray(savedSeriesState) && sIdx === 0) {
      // Apply saved visibility state by matching dataset labels
      chartInstance._lqSeriesState.forEach((state) => {
        const saved = savedSeriesState.find((s) => s.label === state.dataset.label);
        if (saved !== undefined) {
          state.visible = saved.visible;
        }
      });
      // Sync the chart to reflect the saved state
      chartInstance.data.datasets = chartInstance._lqSeriesState
        .filter((s) => s.visible)
        .map((s) => s.dataset);
      chartInstance.update('none');
    }
  });

  appendChart(
    panelSamples,
    pIdx,
    wrappers,
    panelDiv,
    gridContainer,
    groupId,
    fieldIndexes,
    csvFileToken
  );
}
