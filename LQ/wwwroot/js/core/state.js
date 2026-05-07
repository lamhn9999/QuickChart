// @ts-check
/**
 * @file Cross-cutting workspace state.
 *
 * - `pendingIndexes` — field indexes the user has clicked but not yet dropped onto the canvas.
 *   Read by chart.js (creating chart groups), header-drag (toggle), drop-zones (clear on drop).
 * - `appState.controller` — the in-flight AbortController for chart-data fetches. Filter +
 *   drop-zone code aborts whatever's in flight before kicking off the next request.
 * - `listIndexes` — currently exported for forward-compat with the unimplemented
 *   "exclude row" feature. Nothing populates it yet (see CLAUDE.md §6).
 */

/** @type {Set<number>} */
export const pendingIndexes = new Set();

/** @type {Set<number>} */
export const listIndexes = new Set();

/** @type {{ controller: AbortController | null }} */
export const appState = { controller: null };
