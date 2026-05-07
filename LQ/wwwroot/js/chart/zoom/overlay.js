// @ts-check
/**
 * @file Zoom overlay — full-screen modal that hosts a single chart wrapper plus the options
 * panel and navigator.
 *
 * `openZoom(wrapper)` snapshots where the wrapper lives, moves it into `#zoomContent`, fits
 * it to the overlay, builds the navigator + options panel, and listens for Escape.
 *
 * `closeZoom()` undoes all of that — restores the wrapper to its original parent + sibling
 * + inline cssText, tears down nav + panel, removes the keydown listener.
 *
 * State for the round-trip lives module-private here.
 */

import { closeMenus } from '../../core/menus.js';
import { applyChartTheme } from '../theme.js';
import { navInit, navDestroy } from './navigator.js';
import { initZoomPanel } from './panel/index.js';

/** @type {HTMLElement | null} */
let _zoomedWrapper = null;
/** @type {Element | null} */
let _zoomedParent = null;
/** @type {Element | null} */
let _zoomedSibling = null;
let _zoomedCssText = '';
/** @type {(() => void) | null} */
let _zoomPanelCleanup = null;

/**
 * @param {HTMLElement} wrapper  the chart wrapper (`<div data-chart-title=…>`) to zoom
 */
export function openZoom(wrapper) {
  // Close any open menus first
  closeMenus(['plot']);

  const overlay = document.getElementById('zoomOverlay');
  const content = document.getElementById('zoomContent');
  if (!overlay || !content) return;

  _zoomedWrapper = wrapper;
  _zoomedParent = wrapper.parentElement;
  _zoomedSibling = wrapper.nextElementSibling;
  _zoomedCssText = wrapper.style.cssText; // snapshot all inline styles

  const titleEl = document.getElementById('zoomTitle');
  if (titleEl) titleEl.textContent = wrapper.dataset.chartTitle || 'Chart';

  content.appendChild(wrapper);
  wrapper.style.cssText = 'display:flex; flex-direction:column; width:100%; height:100%;';

  overlay.classList.remove('hidden');
  document.addEventListener('keydown', _onEscClose);

  requestAnimationFrame(() => {
    const cv = /** @type {HTMLCanvasElement | null} */ (wrapper.querySelector('canvas'));
    if (!cv) return;
    // @ts-ignore — Chart is a runtime global
    const chart = Chart.getChart(cv);
    if (chart) {
      chart.resize();
      applyChartTheme(chart, 'light'); // zoom overlay is light bg
      navInit(chart); // show the navigator now that the chart is live
      _zoomPanelCleanup = initZoomPanel(
        chart,
        /** @type {HTMLElement | null} */ (document.getElementById('zoomOptionPane'))
      );
    }
  });
}

export function closeZoom() {
  if (!_zoomedWrapper || !_zoomedParent) return;

  // Tear down option panel
  if (_zoomPanelCleanup) {
    _zoomPanelCleanup();
    _zoomPanelCleanup = null;
  }

  // Capture the chart reference before we move the wrapper back
  const cvBefore = /** @type {HTMLCanvasElement | null} */ (_zoomedWrapper.querySelector('canvas'));
  // @ts-ignore — Chart is a runtime global
  const chartBeforeMove = cvBefore ? Chart.getChart(cvBefore) : null;
  navDestroy(chartBeforeMove); // tear down navigator + reset x-axis clamps

  document.getElementById('zoomOverlay')?.classList.add('hidden');
  document.removeEventListener('keydown', _onEscClose);

  if (_zoomedSibling) {
    _zoomedParent.insertBefore(_zoomedWrapper, _zoomedSibling);
  } else {
    _zoomedParent.appendChild(_zoomedWrapper);
  }
  _zoomedWrapper.style.cssText = _zoomedCssText;

  const wrapper = _zoomedWrapper; // ← capture before nullifying

  _zoomedWrapper = null;
  _zoomedParent = null;
  _zoomedSibling = null;
  _zoomedCssText = '';

  requestAnimationFrame(() => {
    const cv = /** @type {HTMLCanvasElement | null} */ (wrapper.querySelector('canvas'));
    if (!cv) return;
    // @ts-ignore — Chart is a runtime global
    const chart = Chart.getChart(cv); // ← safe local ref
    if (chart) {
      chart.resize();
      applyChartTheme(chart, 'light'); // card is light bg
    }
  });
}

/** @param {KeyboardEvent} e */
function _onEscClose(e) {
  if (e.key === 'Escape') closeZoom();
}
