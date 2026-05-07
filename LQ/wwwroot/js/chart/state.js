// @ts-check
/**
 * @file Chart-package shared mutable state.
 *
 * `chartInstances` is the live registry of every Chart.js instance currently in the canvas.
 * It's mutated from chart-group creation/replacement (push), removal (splice), and read by
 * the panel resizer (which calls `chart.resize()` on each one) and by the canvas-engine
 * resize handles (which only resize the chart they own — they don't iterate this list).
 */

/** @type {any[]} live Chart.js instances */
export const chartInstances = [];
