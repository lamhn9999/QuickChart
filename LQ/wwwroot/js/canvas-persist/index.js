// @ts-check
/**
 * @file Persist + hydrate the document canvas (`#doc-canvas`) for the current CSVFile.
 *
 * Wire-up:
 *   1. The Razor view emits `<script id="saved-canvas" type="application/json">` with the
 *      previously-saved document (or the literal "null").
 *   2. On DOMContentLoaded, hydrateCanvas reads that script and reconstructs blocks +
 *      chart cards.
 *   3. After hydration, a debounced auto-save observer watches `#doc-canvas` and POSTs to
 *      /workspace/canvas. beforeunload flushes via sendBeacon.
 *
 * Sub-modules:
 *   - state.js     `isHydrating()` / `setHydrating(v)` flag
 *   - kinds.js     KINDS registry (text, divider, chart, textCard) + KINDS_BY_NAME
 *   - serialize.js serializeCanvas → { title, blocks }
 *   - hydrate.js   hydrateCanvas (replays kind.hydrate per block)
 *   - autosave.js  startAutoSave (debounced MutationObserver + beforeunload sendBeacon)
 *
 * Adding a new persistable element type is a single new entry in KINDS.
 */

import { setHydrating } from './state.js';
import { hydrateCanvas } from './hydrate.js';
import { startAutoSave } from './autosave.js';

document.addEventListener('DOMContentLoaded', async () => {
  const tag = document.getElementById('saved-canvas');
  /** @type {any} */
  let doc = null;
  if (tag) {
    try {
      doc = JSON.parse(tag.textContent ?? 'null');
    } catch {
      doc = null;
    }
  }

  if (doc) {
    setHydrating(true);
    try {
      await hydrateCanvas(doc);
    } finally {
      setHydrating(false);
    }
  }

  startAutoSave();
});
