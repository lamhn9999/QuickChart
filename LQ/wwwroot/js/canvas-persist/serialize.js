// @ts-check
/**
 * @file Walk `#doc-canvas` and produce a JSON-serialisable document `{ title, blocks[] }`.
 *
 * Each block carries a `kind` discriminator that pairs with the matching `KINDS_BY_NAME`
 * entry on hydration. The doc-title element is special-cased.
 */

import { KINDS } from './kinds.js';

/**
 * @returns {{ title: string, blocks: Array<{ kind: string } & Record<string, any>> } | null}
 */
export function serializeCanvas() {
  const canvas = document.getElementById('doc-canvas');
  if (!canvas) return null;

  const titleEl = canvas.querySelector('#doc-title');
  const title = titleEl ? (titleEl.textContent ?? '') : '';

  /** @type {Array<{ kind: string } & Record<string, any>>} */
  const blocks = [];
  for (const child of canvas.children) {
    if (child.id === 'doc-title') continue;
    const kind = KINDS.find((k) => k.match(child));
    if (!kind) continue;
    blocks.push({ kind: kind.name, ...kind.serialize(child) });
  }
  return { title, blocks };
}
