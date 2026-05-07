// @ts-check
/**
 * @file §3 TOOLTIP FORMAT — 2×2 grid of prefix/suffix inputs with a live preview row.
 *
 * Patches `chart.options.plugins.tooltip.callbacks.title` and `.label` so the user sees
 * "Week January Q1" / "$ 42 %" style labels in the tooltip.
 */

import { css, accordion, mkInput } from './primitives.js';

/**
 * @param {any} chart
 * @returns {HTMLElement}
 */
export function buildTooltipAccordion(chart) {
  return accordion('Tooltip Format', (body) => {
    let _tTP = '';
    let _tTS = '';
    let _tLP = '';
    let _tLS = '';

    // Live preview box
    const preview = document.createElement('div');
    css(
      preview,
      'background:rgba(0,0,0,0.03);border:1px solid #e4e4e7;border-radius:6px;padding:7px 10px;font-size:10px;line-height:1.65;margin-bottom:2px'
    );
    const updatePreview = () => {
      preview.innerHTML = `<div style="color:#52525b;font-weight:600;font-size:9.5px">${_tTP || ''}January${_tTS || ''}</div><div style="color:#E9967A;font-size:9px">● ${_tLP || ''}42${_tLS || ''}</div>`;
    };
    updatePreview();
    body.appendChild(preview);

    function applyTooltip() {
      chart.options.plugins.tooltip = chart.options.plugins.tooltip ?? {};
      chart.options.plugins.tooltip.callbacks = chart.options.plugins.tooltip.callbacks ?? {};
      chart.options.plugins.tooltip.callbacks.title = (/** @type {any} */ items) =>
        `${_tTP}${items[0]?.label ?? ''}${_tTS}`;
      chart.options.plugins.tooltip.callbacks.label = (/** @type {any} */ item) =>
        ` ${_tLP}${item.formattedValue ?? ''}${_tLS}`;
      chart.update('none');
      updatePreview();
    }

    const grid = document.createElement('div');
    css(grid, 'display:grid;grid-template-columns:1fr 1fr;gap:5px');

    /**
     * @param {string} lbl
     * @param {string} ph
     * @param {(e: any) => void} cb
     */
    function mkLabeledInput(lbl, ph, cb) {
      const w = document.createElement('div');
      css(w, 'display:flex;flex-direction:column;gap:3px');
      const l = document.createElement('span');
      css(l, 'font-size:8.5px;color:#a1a1aa;letter-spacing:0.04em');
      l.textContent = lbl;
      w.append(l, mkInput(ph, cb));
      return w;
    }
    grid.append(
      mkLabeledInput('Title prefix', 'e.g. Week ', (e) => {
        _tTP = e.target.value;
        applyTooltip();
      }),
      mkLabeledInput('Title suffix', 'e.g.  Q1', (e) => {
        _tTS = e.target.value;
        applyTooltip();
      }),
      mkLabeledInput('Value prefix', 'e.g. $', (e) => {
        _tLP = e.target.value;
        applyTooltip();
      }),
      mkLabeledInput('Value suffix', 'e.g. %', (e) => {
        _tLS = e.target.value;
        applyTooltip();
      })
    );
    body.appendChild(grid);
  });
}
