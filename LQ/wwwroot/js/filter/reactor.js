// @ts-check
/**
 * @file Filter → Chart refresh bridge.
 *
 * Subscribes to filter execution notifications and triggers `refreshAllCharts()`
 * with a small debounce so rapid multi-click filter actions batch into a single
 * chart refresh.  Single chokepoint — the reactor is the ONLY place that knows
 * about both the filter system and the chart system.
 *
 * Initialized from workspace/index.js via `initFilterReactor()`.
 */

import { onFilterApplied } from './api.js';
import { refreshAllCharts } from '../chart-refresh/index.js';

/**
 * @param {Function} fn
 * @param {number} ms
 * @returns {(...args: any[]) => void}
 */
function debounce(fn, ms) {
  /** @type {ReturnType<typeof setTimeout> | null} */
  let timer = null;
  return (...args) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      timer = null;
      fn(...args);
    }, ms);
  };
}

const refreshDebounced = debounce(refreshAllCharts, 150);

/** Subscribe to filter-post-execution notification. */
export function initFilterReactor() {
  onFilterApplied.add(refreshDebounced);
}
