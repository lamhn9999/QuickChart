// @ts-check
/**
 * @file Shared mutable flag for canvas-persist.
 *
 * `_hydrating` is set to true while we're rebuilding the canvas from saved JSON, then
 * cleared. Auto-save observes the canvas's MutationObserver — without this flag, hydration
 * itself would trigger a save round-trip, overwriting the very document we just loaded.
 */

let _hydrating = false;

export function isHydrating() {
  return _hydrating;
}

/** @param {boolean} v */
export function setHydrating(v) {
  _hydrating = v;
}
