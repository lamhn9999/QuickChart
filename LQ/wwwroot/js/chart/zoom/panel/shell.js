// @ts-check
/**
 * @file Outer shell of the zoom options panel — left-side panel that hosts every section.
 *
 * `buildPanelShell(chart, containerEl)` returns `{ wrap, inner }`:
 *   - `wrap` is the absolute-positioned `<div>` appended to `containerEl`.
 *   - `inner` is the scrollable body where each section appends its accordion / rows.
 * Also installs the collapse toggle tab on the left edge of the panel.
 */

import { css } from './primitives.js';

/**
 * Build wrap + header + scrollable inner + collapse toggle. Mounts into `containerEl`.
 *
 * @param {any} chart        Chart.js instance — used to call `.resize()` after collapse.
 * @param {HTMLElement} containerEl
 * @returns {{ wrap: HTMLElement, inner: HTMLElement }}
 */
export function buildPanelShell(chart, containerEl) {
  containerEl.innerHTML = '';
  containerEl.className = 'relative flex flex-col overflow-hidden';
  containerEl.style.cssText = '';

  // ── Outer wrap (light panel) ──────────────────────────────────────────
  const wrap = document.createElement('div');
  css(
    wrap,
    'width:264px;min-width:264px;height:100%;display:flex;flex-direction:column;background:#FAFAFA;border-left:1px solid #e4e4e7;position:relative;transition:width 0.2s,min-width 0.2s;overflow:hidden'
  );

  // ── Collapse toggle tab ───────────────────────────────────────────────
  let _collapsed = false;
  const toggleBtn = document.createElement('button');
  css(
    toggleBtn,
    'position:absolute;left:-12px;top:50%;transform:translateY(-50%);z-index:10;width:12px;height:44px;background:#FAFAFA;border:1px solid #e4e4e7;border-right:none;border-radius:6px 0 0 6px;cursor:pointer;display:flex;align-items:center;justify-content:center;color:#a1a1aa;transition:color 0.15s,background 0.15s;padding:0'
  );
  toggleBtn.innerHTML = `<svg width="8" height="8" id="_pChev" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" style="transition:transform 0.2s;flex-shrink:0"><polyline points="15 18 9 12 15 6"/></svg>`;
  toggleBtn.addEventListener('mouseenter', () => {
    toggleBtn.style.background = '#f0f0f0';
    toggleBtn.style.color = '#52525b';
  });
  toggleBtn.addEventListener('mouseleave', () => {
    toggleBtn.style.background = '#FAFAFA';
    toggleBtn.style.color = '#a1a1aa';
  });
  const _chevEl = /** @type {HTMLElement | null} */ (toggleBtn.querySelector('#_pChev'));
  toggleBtn.addEventListener('click', () => {
    _collapsed = !_collapsed;
    wrap.style.width = _collapsed ? '0' : '264px';
    wrap.style.minWidth = _collapsed ? '0' : '264px';
    if (_chevEl) _chevEl.style.transform = _collapsed ? 'rotate(180deg)' : '';
    setTimeout(() => {
      try {
        chart.resize();
      } catch {
        /* ignore */
      }
    }, 220);
  });

  // ── Panel header ──────────────────────────────────────────────────────
  const header = document.createElement('div');
  css(
    header,
    'padding:12px 14px 11px;border-bottom:1px solid #e4e4e7;display:flex;align-items:center;gap:7px;flex-shrink:0;background:#fff'
  );
  header.innerHTML = `
        <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="#E9967A" stroke-width="2" stroke-linecap="round">
            <rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/>
            <rect x="14" y="14" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/>
        </svg>
        <span style="font-size:10.5px;font-weight:700;letter-spacing:0.09em;text-transform:uppercase;color:#a1a1aa">Chart Options</span>`;

  // ── Scrollable inner ──────────────────────────────────────────────────
  const inner = document.createElement('div');
  css(
    inner,
    'flex:1;overflow-y:auto;padding:8px 10px 24px;display:flex;flex-direction:column;gap:4px;scrollbar-width:thin;scrollbar-color:rgba(0,0,0,0.1) transparent'
  );

  wrap.append(toggleBtn, header, inner);
  containerEl.appendChild(wrap);

  return { wrap, inner };
}
