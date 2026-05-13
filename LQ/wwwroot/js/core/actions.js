// @ts-check
/**
 * @file Delegated event-dispatch framework.
 *
 * Replaces the per-element inline `onclick="someFn(event, this)"` pattern with
 * `data-action="namespace:verb"` attributes that route through one document-level listener.
 *
 * Conventions:
 *   - `data-action="ns:verb"` is implicit click. Reading the action removes the chevron
 *     case from the most-common path.
 *   - `data-on-keydown="…"`, `data-on-input="…"`, `data-on-dragstart="…"`,
 *     `data-on-dragend="…"` for non-click events.
 *   - Handlers receive `(event, matched)` where `matched` is the element carrying the
 *     attribute (NOT necessarily `event.target`, which may be a nested span/svg).
 *   - Handlers read extra arguments from sibling `data-*` attributes on `matched` — there's
 *     no facility for passing args through the action name itself.
 *
 * Bubble phase is used. Programmatic `onclick` handlers attached to elements INSIDE a
 * `data-action` ancestor (e.g. filter-chip ✕ buttons created by chip.js) fire first in
 * the target/bubble phases; the dispatcher fires after, when bubble reaches the document.
 *
 * Action handlers MUST NOT call `event.stopPropagation()` — the dispatcher itself is on
 * the document, so stopping at the matched element is too late to influence the closer.
 * The "click-inside-menu shouldn't close it" behaviour is handled by the outside-click
 * closer in `workspace/index.js`, which explicitly skips clicks inside known menus.
 */

/** @type {Map<string, (event: Event, matched: Element) => void>} */
const registry = new Map();

// `mouseover` / `mouseout` bubble (their `mouseenter`/`mouseleave` counterparts
// don't), so they're the right delegation primitives for "hover a row inside
// this list" patterns — used by the per-chart filter right-panel chart
// picker. `change` rounds out the form-element triad alongside `click` and
// `input` so checkboxes/selects can dispatch their final-state event without
// piggybacking on `click` (clicking a label, keyboard toggle, etc.).
const EVENT_TYPES = /** @type {const} */ ([
  'click', 'keydown', 'input', 'change', 'mouseover', 'mouseout',
  'dragstart', 'dragend',
]);

/**
 * Register a handler for a named action. Idempotent — a re-register replaces the previous
 * handler. Names should follow `namespace:verb`, e.g. `filter:open`, `gear:change-type`.
 *
 * @param {string} name
 * @param {(event: Event, matched: Element) => void} handler
 */
export function registerAction(name, handler) {
  registry.set(name, handler);
}

/**
 * Convenience for registering many actions at once.
 * @param {Record<string, (event: Event, matched: Element) => void>} entries
 */
export function registerActions(entries) {
  for (const [name, fn] of Object.entries(entries)) registerAction(name, fn);
}

/**
 * Wire one document-level bubble-phase listener per supported event type. Idempotent —
 * calling twice rewires the same listeners (deduped by reference).
 */
export function wireDelegatedActions() {
  for (const type of EVENT_TYPES) {
    document.removeEventListener(type, /** @type {any} */ (dispatch));
    document.addEventListener(type, /** @type {any} */ (dispatch));
  }
}

/**
 * @param {Event} event
 */
function dispatch(event) {
  const t = event.target;
  if (!(t instanceof Element)) return;
  const attr = event.type === 'click' ? 'data-action' : `data-on-${event.type}`;
  const matched = t.closest(`[${attr}]`);
  if (!matched) return;
  const name = matched.getAttribute(attr);
  if (!name) return;
  const handler = registry.get(name);
  if (handler) handler(event, matched);
}
