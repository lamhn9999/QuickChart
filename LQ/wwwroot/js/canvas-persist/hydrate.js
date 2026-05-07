// @ts-check
/**
 * @file Replay a serialised canvas document — invoked once on page boot if the Razor view
 * shipped a non-null `<script id="saved-canvas">`.
 *
 * The page's overall MutationObserver auto-save is paused via `setHydrating(true)` around
 * this call; otherwise hydration itself would trigger a save and overwrite the document
 * with a clone-with-incomplete-state.
 */

import { KINDS_BY_NAME } from './kinds.js';

/**
 * @param {{ title?: string, blocks?: Array<{ kind: string } & Record<string, any>> } | null} doc
 */
export async function hydrateCanvas(doc) {
  const canvas = document.getElementById('doc-canvas');
  if (!canvas || !doc || !Array.isArray(doc.blocks)) return;

  const titleEl = canvas.querySelector('#doc-title');
  if (titleEl && typeof doc.title === 'string') titleEl.textContent = doc.title;

  for (const spec of doc.blocks) {
    const kind = KINDS_BY_NAME[spec.kind];
    if (!kind) continue;
    const el = await kind.hydrate(spec);
    if (el && !kind.attachesItself) canvas.appendChild(el);
  }
}
