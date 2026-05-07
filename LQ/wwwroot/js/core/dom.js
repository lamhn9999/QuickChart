// @ts-check
/**
 * @file Tiny DOM helpers shared across the workspace JS.
 * Keep this file dependency-free and side-effect-free.
 */

/**
 * Query for the first element matching `selector` within `root`.
 * @param {string} selector
 * @param {ParentNode} [root]
 * @returns {Element | null}
 */
export const $ = (selector, root = document) => root.querySelector(selector);

/**
 * Query for all elements matching `selector` within `root`. Returns a real Array (not NodeList) so
 * `.map` / `.filter` work without spreading.
 * @param {string} selector
 * @param {ParentNode} [root]
 * @returns {Element[]}
 */
export const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));

/**
 * Add an event listener and return a teardown function that removes it.
 * @template {keyof HTMLElementEventMap} K
 * @param {EventTarget} el
 * @param {K | string} type
 * @param {EventListenerOrEventListenerObject} handler
 * @param {boolean | AddEventListenerOptions} [opts]
 * @returns {() => void}
 */
export function on(el, type, handler, opts) {
  el.addEventListener(/** @type {string} */ (type), handler, opts);
  return () => el.removeEventListener(/** @type {string} */ (type), handler, opts);
}

/**
 * Delegated event listener: invokes `handler(event, matched)` when `event.target.closest(selector)`
 * matches and is contained in `root`. Returns a teardown function.
 * @param {Element | Document} root
 * @param {string} type
 * @param {string} selector
 * @param {(e: Event, matched: Element) => void} handler
 * @param {boolean | AddEventListenerOptions} [opts]
 * @returns {() => void}
 */
export function delegate(root, type, selector, handler, opts) {
  const wrapped = (/** @type {Event} */ e) => {
    const t = e.target;
    if (!(t instanceof Element)) return;
    const matched = t.closest(selector);
    if (matched && root.contains(matched)) handler(e, matched);
  };
  root.addEventListener(type, wrapped, opts);
  return () => root.removeEventListener(type, wrapped, opts);
}

/**
 * Position `popup` (a hidden popover) below `anchorEl`, flipped above if there isn't enough
 * room. Uses `position: fixed` so the popup escapes any `overflow: auto` clipping container.
 * Leaves the popup in `display: hidden` state — the caller must `popup.classList.remove('hidden')`
 * to actually show it. Width/height measured by briefly rendering it offscreen.
 *
 * @param {HTMLElement} popup
 * @param {Element} anchorEl
 */
export function positionDropdown(popup, anchorEl) {
  const MARGIN = 8; // min gap from viewport edges
  const GAP = 4; // gap between anchor bottom and popup top

  popup.style.visibility = 'hidden';
  popup.style.position = 'fixed';
  popup.style.top = '0px';
  popup.style.left = '0px';
  popup.style.transform = 'none'; // neutralise any Tailwind translate classes
  popup.classList.remove('hidden');

  const pw = popup.offsetWidth;
  const ph = popup.offsetHeight;

  popup.classList.add('hidden');
  popup.style.visibility = '';

  const rect = anchorEl.getBoundingClientRect();
  const vw = window.innerWidth;
  const vh = window.innerHeight;

  let top = rect.bottom + GAP;
  let left = rect.left;

  if (left < MARGIN) left = MARGIN;
  if (left + pw > vw - MARGIN) left = vw - pw - MARGIN;

  // Flip above anchor if not enough space below
  if (top + ph > vh - MARGIN) {
    top = rect.top - ph - GAP;
  }
  // Last resort: pin to top margin
  if (top < MARGIN) top = MARGIN;

  popup.style.top = `${top}px`;
  popup.style.left = `${left}px`;
  popup.style.zIndex = '9999';
}

/**
 * Clone the contents of a `<template id="…">` and return its first element child.
 * Throws if the template is missing — fail-loud is the right default for build-time UI.
 * @param {string} id
 * @returns {Element}
 */
export function cloneTemplate(id) {
  const tpl = document.getElementById(id);
  if (!(tpl instanceof HTMLTemplateElement)) {
    throw new Error(`cloneTemplate: <template id="${id}"> not found`);
  }
  const node = tpl.content.firstElementChild;
  if (!node) {
    throw new Error(`cloneTemplate: <template id="${id}"> is empty`);
  }
  return /** @type {Element} */ (node.cloneNode(true));
}
