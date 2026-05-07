// @ts-check
/**
 * @file Tiny synchronous pub/sub bus.
 *
 * Subscribers are called in registration order on the same call stack as `emit()`.
 * Errors are caught & logged so one bad subscriber doesn't break the rest.
 *
 * Use this to decouple workspace state mutations from view code (Phase 3+).
 */

/** @type {Map<string, Set<(payload: any) => void>>} */
const subs = new Map();

/**
 * Subscribe `fn` to `event`. Returns a teardown function.
 * @template T
 * @param {string} event
 * @param {(payload: T) => void} fn
 * @returns {() => void}
 */
export function subscribe(event, fn) {
  let set = subs.get(event);
  if (!set) {
    set = new Set();
    subs.set(event, set);
  }
  set.add(fn);
  return () => {
    set?.delete(fn);
  };
}

/**
 * Emit `event` with payload, calling every subscriber synchronously.
 * @template T
 * @param {string} event
 * @param {T} [payload]
 */
export function emit(event, payload) {
  const set = subs.get(event);
  if (!set) return;
  for (const fn of set) {
    try {
      fn(payload);
    } catch (err) {
      console.error(`[events] subscriber for "${event}" threw:`, err);
    }
  }
}
