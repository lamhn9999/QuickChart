// @ts-check
/**
 * @file Resolved reference to `#doc-canvas`. Read at module-load time — safe because
 * `<script type="module">` is deferred so the DOM is ready by then.
 *
 * Importers can assume `canvas` is non-null on the workspace view; on the home page or any
 * page without `#doc-canvas`, it's null and the editor's `index.js` early-returns before any
 * sub-module's init function runs.
 */

/** @type {HTMLElement | null} */
export const canvas = /** @type {HTMLElement | null} */ (document.getElementById('doc-canvas'));
