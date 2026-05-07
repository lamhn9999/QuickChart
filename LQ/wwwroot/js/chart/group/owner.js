// @ts-check
/**
 * @file Read the active workspace's owner CSVFile token from the inline JSON tag.
 *
 * The Razor view ships `<script id="canvas-owner-file-token" type="application/json">"…"</script>`
 * containing the Guid token of the CSVFile this workspace is editing. Used by `copy.js` as
 * a fallback "source token" when an internal (native) chart is copied into another file —
 * such charts don't carry their own `csvFileToken` dataset attribute.
 *
 * Returns `null` if the tag is missing, malformed, or empty (e.g. on the home page).
 */

/**
 * @returns {string | null}
 */
export function ownerFileToken() {
  const tag = document.getElementById('canvas-owner-file-token');
  if (!tag) return null;
  try {
    const v = JSON.parse(tag.textContent ?? '');
    return typeof v === 'string' && v.length > 0 ? v : null;
  } catch {
    return null;
  }
}
