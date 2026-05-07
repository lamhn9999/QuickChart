// @ts-check
/**
 * @file Zoom-options panel orchestrator — replaces the 715-line `initZoomPanel` god function.
 *
 * Builds the shell (wrap/header/inner/collapse), takes a snapshot of the original data
 * (for the Sort accordion's "Original" reset), then calls each section builder in order.
 *
 * Returns a teardown function that clears `containerEl` so closeZoom() can detach.
 */

import { sectionLabel } from './primitives.js';
import { buildPanelShell } from './shell.js';
import { buildSeriesSection } from './series.js';
import { buildSortAccordion } from './sort.js';
import { buildTooltipAccordion } from './tooltip.js';
import { buildMergeAccordion } from './merge.js';
import { buildPluginsAccordion } from './plugins.js';

/**
 * Mount the options panel into `containerEl` for the given chart.
 *
 * @param {any} chart  Chart.js instance
 * @param {HTMLElement | null} containerEl
 * @returns {() => void}  teardown function
 */
export function initZoomPanel(chart, containerEl) {
  if (!containerEl) return () => {};

  // Snapshot original data (needed for sort reset and merge undo)
  const orig = {
    labels: [...(chart.data.labels ?? [])],
    datasets: chart.data.datasets.map((/** @type {any} */ ds) => ({
      ...ds,
      data: [...(ds.data ?? [])],
    })),
  };

  const { inner } = buildPanelShell(chart, containerEl);

  // §1  SERIES — always visible
  inner.appendChild(sectionLabel('Series'));
  const seriesCtx = buildSeriesSection(chart, inner);

  // §2  SORT — under "Display"
  inner.appendChild(sectionLabel('Display'));
  inner.appendChild(buildSortAccordion(chart, orig));

  // §3  TOOLTIP FORMAT
  inner.appendChild(buildTooltipAccordion(chart));

  // §4  MERGE DATASETS
  inner.appendChild(buildMergeAccordion(chart, seriesCtx));

  // §5  PLUGINS — only if Chart.js has any registered
  const pluginsAcc = buildPluginsAccordion(chart);
  if (pluginsAcc) inner.appendChild(pluginsAcc);

  return () => {
    containerEl.innerHTML = '';
    containerEl.style.cssText = '';
    containerEl.className = '';
  };
}
