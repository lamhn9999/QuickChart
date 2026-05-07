// @ts-check
/**
 * @file §1 SERIES — merged Legend + Colors + Dataset toggles section.
 *
 * Each row: [grip] [color-chip] [name — dbl-click to rename] [eye toggle].
 * Drag a row by its grip to reorder. The full source-of-truth for order + visibility lives
 * on `chart._lqSeriesState` so it survives zoom-out / zoom-in cycles.
 *
 * Exports `buildSeriesSection(chart, inner)` returning `{ rebuild }` so other sections
 * (merge.js especially) can ask for a re-render after they push a new dataset.
 */

import { css } from './primitives.js';

/** @typedef {{ dataset: any, visible: boolean }} SeriesState */

const EYE_OPEN = `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>`;
const EYE_CLOSED = `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>`;
const GRIP_ICON = `<svg width="8" height="12" viewBox="0 0 8 14" fill="currentColor"><circle cx="2" cy="2" r="1.2"/><circle cx="6" cy="2" r="1.2"/><circle cx="2" cy="7" r="1.2"/><circle cx="6" cy="7" r="1.2"/><circle cx="2" cy="12" r="1.2"/><circle cx="6" cy="12" r="1.2"/></svg>`;

// ── Color helpers ─────────────────────────────────────────────────────────

/** @param {any} raw */
function parseColorToHex(raw) {
  const color = Array.isArray(raw) ? raw[0] : raw;
  if (typeof color !== 'string') return null;
  if (/^#[0-9A-Fa-f]{6}$/i.test(color)) return color;
  if (/^#[0-9A-Fa-f]{3}$/i.test(color))
    return '#' + [color[1] + color[1], color[2] + color[2], color[3] + color[3]].join('');
  const m = color.match(/rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/);
  if (m) return '#' + [m[1], m[2], m[3]].map((n) => (+n).toString(16).padStart(2, '0')).join('');
  return null;
}

/**
 * @param {any} chart
 * @param {any} ds
 * @param {number} chartIdx  index in chart.data.datasets, or -1 for hidden datasets
 */
function dsColorHex(chart, ds, chartIdx) {
  let hex = parseColorToHex(ds.borderColor) ?? parseColorToHex(ds.backgroundColor);
  if (hex) return hex;
  if (chartIdx >= 0) {
    try {
      const style = chart.getDatasetMeta(chartIdx).controller?.getStyle?.();
      hex = parseColorToHex(style?.borderColor) ?? parseColorToHex(style?.backgroundColor);
      if (hex) return hex;
    } catch {
      /* ignore */
    }
  }
  return '#E9967A';
}

/**
 * Build the Series section into `inner`. Returns `{ rebuild }` so merge.js can re-render
 * the rows after pushing a new dataset.
 *
 * @param {any} chart
 * @param {HTMLElement} inner   panel scroll container — section is appended here
 * @returns {{ rebuild: () => void, syncDatasetsToChart: () => void, seriesState: SeriesState[] }}
 */
export function buildSeriesSection(chart, inner) {
  // ── Series state: source of truth for order + visibility ──────────────
  // Stored on the chart instance so it survives zoom-out / zoom-in cycles.
  if (!chart._lqSeriesState) {
    chart._lqSeriesState = chart.data.datasets.map((/** @type {any} */ ds) => ({
      dataset: ds,
      visible: true,
    }));
  }
  /** @type {SeriesState[]} */
  const seriesState = chart._lqSeriesState;
  // Re-sync in case zoom reset chart.data.datasets to only visible ones.
  chart.data.datasets = seriesState.filter((s) => s.visible).map((s) => s.dataset);

  /** @type {number | null} */
  let _dragSrcIdx = null;

  function syncDatasetsToChart() {
    chart.data.datasets = seriesState.filter((s) => s.visible).map((s) => s.dataset);
    chart.update('none');
  }

  const seriesContainer = document.createElement('div');
  css(seriesContainer, 'display:flex;flex-direction:column;gap:1px');

  function buildSeriesRows() {
    seriesContainer.innerHTML = '';
    let visibleCount = 0;
    seriesState.forEach((state, stateIdx) => {
      const ds = state.dataset;
      const isVis = state.visible;
      const chartIdx = isVis ? visibleCount++ : -1;
      const colorHex = dsColorHex(chart, ds, chartIdx);

      // ── Row ────────────────────────────────────────────────────────
      const row = document.createElement('div');
      row.draggable = true;
      row.classList.add('series-row');
      css(
        row,
        `display:flex;align-items:center;gap:6px;padding:4px 6px;border-radius:7px;transition:background 0.1s;cursor:default;opacity:${isVis ? 1 : 0.4};box-sizing:border-box`
      );
      row.addEventListener('mouseenter', () => {
        if (_dragSrcIdx === null) row.style.background = 'rgba(0,0,0,0.04)';
      });
      row.addEventListener('mouseleave', () => (row.style.background = 'transparent'));

      // ── Drag events ────────────────────────────────────────────────
      row.addEventListener('dragstart', (e) => {
        _dragSrcIdx = stateIdx;
        if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
        setTimeout(() => {
          row.style.opacity = '0.3';
        }, 0);
      });
      row.addEventListener('dragend', () => {
        // Only rebuild if drop didn't already handle it (drag cancelled / dropped outside)
        if (_dragSrcIdx !== null) {
          _dragSrcIdx = null;
          buildSeriesRows();
        }
      });
      row.addEventListener('dragover', (e) => {
        if (_dragSrcIdx === null || _dragSrcIdx === stateIdx) return;
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';
        seriesContainer.querySelectorAll('.series-row').forEach((node) => {
          const elx = /** @type {HTMLElement} */ (node);
          elx.style.borderTop = '';
          elx.style.borderBottom = '';
          elx.style.paddingTop = '4px';
          elx.style.paddingBottom = '4px';
        });
        if (stateIdx < (_dragSrcIdx ?? 0)) {
          row.style.borderTop = '2px solid rgba(233,150,122,0.75)';
          row.style.paddingTop = '2px';
        } else {
          row.style.borderBottom = '2px solid rgba(233,150,122,0.75)';
          row.style.paddingBottom = '2px';
        }
      });
      row.addEventListener('dragleave', () => {
        row.style.borderTop = '';
        row.style.borderBottom = '';
        row.style.paddingTop = '4px';
        row.style.paddingBottom = '4px';
      });
      row.addEventListener('drop', (e) => {
        e.preventDefault();
        if (_dragSrcIdx === null || _dragSrcIdx === stateIdx) return;
        const src = _dragSrcIdx;
        const [moved] = seriesState.splice(src, 1);
        const insertAt = src < stateIdx ? stateIdx - 1 : stateIdx;
        seriesState.splice(insertAt, 0, moved);
        _dragSrcIdx = null;
        syncDatasetsToChart();
        buildSeriesRows();
      });

      // ── Grip handle ────────────────────────────────────────────────
      const grip = document.createElement('span');
      css(
        grip,
        'display:flex;flex-shrink:0;color:#d4d4d8;cursor:grab;user-select:none;padding:2px 0'
      );
      grip.innerHTML = GRIP_ICON;
      grip.addEventListener('mouseenter', () => (grip.style.color = '#a1a1aa'));
      grip.addEventListener('mouseleave', () => (grip.style.color = '#d4d4d8'));

      // ── Color chip (click → hidden picker) ─────────────────────────
      const chip = document.createElement('div');
      css(
        chip,
        `width:13px;height:13px;border-radius:3px;background:${colorHex};flex-shrink:0;cursor:pointer;border:1.5px solid rgba(0,0,0,0.15);position:relative;transition:transform 0.1s,box-shadow 0.1s`
      );
      const hPick = document.createElement('input');
      hPick.type = 'color';
      hPick.value = colorHex;
      css(hPick, 'position:absolute;opacity:0;width:0;height:0;pointer-events:none;left:0;top:0');
      chip.appendChild(hPick);
      chip.addEventListener('mouseenter', () => {
        chip.style.transform = 'scale(1.2)';
        chip.style.boxShadow = '0 0 0 2.5px rgba(233,150,122,0.45)';
      });
      chip.addEventListener('mouseleave', () => {
        chip.style.transform = '';
        chip.style.boxShadow = '';
      });
      chip.addEventListener('click', (e) => {
        e.stopPropagation();
        hPick.click();
      });
      /** @type {any} */
      let _colorTimer = null;
      hPick.addEventListener('input', () => {
        const hex = hPick.value;
        chip.style.background = hex; // instant chip feedback
        ds.borderColor = hex;
        ds.backgroundColor = hex + '55';
        ds.pointBorderColor = hex;
        ds.pointBackgroundColor = hex + '55';

        // Patch cached element options so chart.draw() uses them immediately.
        const liveIdx = chart.data.datasets.indexOf(ds);
        if (liveIdx >= 0) {
          try {
            chart.getDatasetMeta(liveIdx).data?.forEach((/** @type {any} */ el) => {
              if (el.options) {
                el.options.borderColor = hex;
                el.options.backgroundColor = hex + '55';
              }
            });
          } catch {
            /* ignore */
          }
        }

        clearTimeout(_colorTimer);
        _colorTimer = setTimeout(() => chart.update('none'), 80);
      });

      // ── Name span (dbl-click → rename) ─────────────────────────────
      const nameSpan = document.createElement('span');
      css(
        nameSpan,
        'flex:1;min-width:0;font-size:11px;color:#27272a;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;border-radius:3px;padding:0 2px;user-select:none'
      );
      nameSpan.textContent = ds.label ?? `Dataset ${stateIdx}`;
      nameSpan.title = 'Double-click to rename';
      nameSpan.addEventListener('dblclick', (e) => {
        e.stopPropagation();
        row.draggable = false;
        const inp = document.createElement('input');
        inp.type = 'text';
        inp.value = nameSpan.textContent ?? '';
        css(
          inp,
          'flex:1;min-width:0;background:rgba(233,150,122,0.08);border:1px solid rgba(233,150,122,0.45);border-radius:4px;padding:2px 5px;font-size:11px;color:#18181b;outline:none;font-family:inherit'
        );
        row.replaceChild(inp, nameSpan);
        inp.focus();
        inp.select();
        const commit = () => {
          const v = inp.value.trim() || nameSpan.textContent;
          nameSpan.textContent = v;
          ds.label = v;
          chart.update('none');
          row.replaceChild(nameSpan, inp);
          row.draggable = true;
        };
        inp.addEventListener('blur', commit);
        inp.addEventListener('keydown', (ke) => {
          if (ke.key === 'Enter') {
            ke.preventDefault();
            commit();
          }
          if (ke.key === 'Escape') {
            row.replaceChild(nameSpan, inp);
            row.draggable = true;
          }
        });
      });

      // ── Eye toggle — fully removes/restores dataset from chart ──────
      const eyeBtn = document.createElement('button');
      css(
        eyeBtn,
        `width:20px;height:20px;border:none;background:none;cursor:pointer;display:flex;align-items:center;justify-content:center;color:${isVis ? '#a1a1aa' : '#d4d4d8'};flex-shrink:0;border-radius:4px;padding:0;transition:color 0.1s,background 0.1s`
      );
      eyeBtn.innerHTML = isVis ? EYE_OPEN : EYE_CLOSED;
      eyeBtn.title = isVis ? 'Hide' : 'Show';
      eyeBtn.addEventListener('mouseenter', () => {
        eyeBtn.style.color = isVis ? '#18181b' : 'rgba(233,150,122,0.8)';
        eyeBtn.style.background = 'rgba(0,0,0,0.06)';
      });
      eyeBtn.addEventListener('mouseleave', () => {
        eyeBtn.style.color = isVis ? '#a1a1aa' : '#d4d4d8';
        eyeBtn.style.background = 'none';
      });
      eyeBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        state.visible = !state.visible;
        syncDatasetsToChart();
        buildSeriesRows();
      });

      row.append(grip, chip, nameSpan, eyeBtn);
      seriesContainer.appendChild(row);
    });
  }
  buildSeriesRows();
  inner.appendChild(seriesContainer);

  return {
    rebuild: buildSeriesRows,
    syncDatasetsToChart,
    seriesState,
  };
}
