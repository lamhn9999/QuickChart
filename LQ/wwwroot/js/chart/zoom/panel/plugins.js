// @ts-check
/**
 * @file §5 PLUGINS — toggle switch per registered Chart.js plugin.
 *
 * Reads the plugin list from `Chart.registry.plugins.items`, renders one toggle row each,
 * persists the on/off via `chart.options.plugins[id] = false` (Chart.js convention).
 */

import { css, accordion } from './primitives.js';

/**
 * @param {any} chart
 * @returns {HTMLElement | null}  null if no plugins are registered (caller skips appending)
 */
export function buildPluginsAccordion(chart) {
  // @ts-ignore — Chart is a runtime global
  const plugins = Chart.registry?.plugins?.items ?? [];
  if (!plugins.length) return null;

  return accordion('Plugins', (body) => {
    plugins.forEach((/** @type {any} */ plugin) => {
      const id = plugin.id ?? '(unnamed)';
      const version = plugin.version ?? '';
      const plugOpts = chart.options?.plugins?.[id];
      let _on = plugOpts !== false && plugOpts?.display !== false;

      const lbl = document.createElement('label');
      css(lbl, 'display:flex;align-items:center;gap:9px;cursor:pointer;padding:3px 0');

      // Toggle switch
      const sw = document.createElement('div');
      css(
        sw,
        `width:28px;height:16px;border-radius:8px;background:${_on ? '#E9967A' : '#e4e4e7'};position:relative;flex-shrink:0;transition:background 0.2s;cursor:pointer`
      );
      const knob = document.createElement('div');
      css(
        knob,
        `width:12px;height:12px;border-radius:50%;background:#fff;position:absolute;top:2px;left:${_on ? '14px' : '2px'};transition:left 0.2s;box-shadow:0 1px 3px rgba(0,0,0,0.2)`
      );
      sw.appendChild(knob);

      const nameSpan = document.createElement('span');
      css(
        nameSpan,
        'font-size:10px;color:#71717a;flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap'
      );
      nameSpan.textContent = version ? `${id} v${version}` : id;

      sw.addEventListener('click', () => {
        _on = !_on;
        sw.style.background = _on ? '#E9967A' : '#e4e4e7';
        knob.style.left = _on ? '14px' : '2px';
        chart.options.plugins = chart.options.plugins ?? {};
        if (!_on) chart.options.plugins[id] = false;
        else delete chart.options.plugins[id];
        chart.update('none');
      });
      lbl.append(sw, nameSpan);
      body.appendChild(lbl);
    });
  });
}
