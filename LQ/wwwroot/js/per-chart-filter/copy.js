// @ts-check
/**
 * @file POST a filter spec to /workspace/canvas/append-filter in another file's canvas.
 *
 * Mirrors chart/group/copy.js. The persisted block on the target file:
 *   - keeps the source's fieldIndex verbatim (no name resolution in v1)
 *   - keeps the source's current selectedValues
 *   - **always carries csvFileToken**: the source file the fieldIndex is
 *     meaningful against. A filter copied to another canvas can't fall back
 *     to "use the canvas owner" because that's exactly the wrong-column bug
 *     we're fixing. If the host filter is native (rec.csvFileToken == null),
 *     we resolve the current canvas's owner via the same `getOwnerFileToken`
 *     helper kinds.js uses.
 *   - drops appliedTo entirely (a source-canvas group id has no meaning on
 *     the target). The target's hydrate path (recreateFilterCard) recomputes
 *     appliedTo from chart cards on that canvas whose csv-file-token matches
 *     this filter's csvFileToken — same-file scoping enforced both ways.
 *
 * Schema compatibility (does the source file's field still resolve on the
 * target?) is enforced by the backend access check + the frontend hydrate
 * fallback: /distinct returns NotFound when the user no longer has access to
 * the source, and recreateFilterCard returns null in that case.
 */

import { getFilter } from './state.js';
import { getOwnerFileToken } from '../canvas-persist/kinds.js';

/**
 * @param {HTMLElement} card            the .canvas-card.filter-card-host
 * @param {string}      targetFileToken target file's PublicToken (Guid)
 */
export async function copyFilterToFile(card, targetFileToken) {
  const groupId = Number(card.dataset.groupId);
  const rec = Number.isFinite(groupId) ? getFilter(groupId) : null;
  if (!rec) return;

  // Resolve source token: explicit on the record if the filter itself was
  // copied / hosts a foreign chart, otherwise this canvas's owner.
  const sourceToken = rec.csvFileToken ?? getOwnerFileToken();
  if (!sourceToken) {
    console.error('[copyFilterToFile] no source token available; aborting copy');
    return;
  }

  const width = card.offsetWidth || 320;
  const height = card.offsetHeight || 200;

  try {
    const r = await fetch('/workspace/canvas/append-filter', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        targetFileToken,
        filterSpec: {
          csvFileToken: sourceToken,
          fieldIndex: rec.fieldIndex,
          selectedValues: [...rec.selectedValues],
          width,
          height,
        },
      }),
    });
    if (!r.ok) console.error('[copyFilterToFile] failed:', r.status);
  } catch (err) {
    console.error('[copyFilterToFile] error:', err);
  }
}
