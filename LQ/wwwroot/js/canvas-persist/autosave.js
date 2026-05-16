// @ts-check
/**
 * @file Debounced auto-save — observes `#doc-canvas` mutations and POSTs the serialized
 * document to /workspace/canvas/{canvasToken} after 1.5s of inactivity. After a
 * successful save we also rasterize `#doc-canvas` into a PNG and ship it to
 * /workspace/canvas/{canvasToken}/snapshot so the project grid's canvas card can
 * show a real thumbnail instead of an empty silhouette.
 *
 * The snapshot path is best-effort: a failure (library missing, oversized image,
 * network blip) just means the card stays on its previous PNG until next save —
 * the document save itself is unaffected.
 *
 * `beforeunload` flushes the document via `navigator.sendBeacon`. The snapshot
 * step is async + has a 4s budget, so it intentionally does NOT run on unload.
 */

import { isHydrating } from './state.js';
import { serializeCanvas } from './serialize.js';

// Resolve the save endpoint once at module load. The Razor view emits a
// `canvas-token` script tag carrying the canvas's PublicToken; without it
// (older legacy entry path) the save module disables itself.
function resolveCanvasToken() {
  const tag = document.getElementById('canvas-token');
  if (!tag) return null;
  try {
    const tok = JSON.parse(tag.textContent ?? '""');
    return typeof tok === 'string' && tok.length > 0 ? tok : null;
  } catch {
    return null;
  }
}

const CANVAS_TOKEN = resolveCanvasToken();
const SAVE_URL = CANVAS_TOKEN ? `/workspace/canvas/${CANVAS_TOKEN}` : null;
const SNAPSHOT_URL = CANVAS_TOKEN ? `/workspace/canvas/${CANVAS_TOKEN}/snapshot` : null;
const DEBOUNCE_MS = 1500;
// Snapshot output is fixed at 4:3 to match the project card's aspect, so the
// card can `object-fit: cover` without letterboxing.
//
// The card itself renders around ~290 CSS px wide; we capture at logical
// 480×360 and multiply by SNAPSHOT_DPR for the actual canvas so retina
// displays don't upscale-blur the bitmap. Hardcoded at 2 (not
// devicePixelRatio) so the PNG looks the same for every viewer regardless of
// the author's screen.
//
// Tall canvases are TOP-CROPPED to this aspect instead of squashed-to-fit —
// the goal is "above the fold" preview a la Notion / Figma, not a thumbnail of
// the entire scroll height (which renders as an unreadable sliver). The top
// of a canvas is where the title + first chart usually live, which is the
// most recognisable region.
const SNAPSHOT_W = 480;
const SNAPSHOT_H = 360;
const SNAPSHOT_DPR = 2;
const SNAPSHOT_ASPECT = SNAPSHOT_H / SNAPSHOT_W; // 0.75

/** @type {any} */
let _saveTimer = null;
/** @type {any} */
let _snapshotTimer = null;

// Schedule a snapshot capture, coalescing repeated requests. Public so the
// post-hydration path can trigger an initial thumbnail without a save.
export function scheduleSnapshot(delayMs = 250) {
  if (!SNAPSHOT_URL) return;
  if (_snapshotTimer) clearTimeout(_snapshotTimer);
  _snapshotTimer = setTimeout(() => { captureAndUploadSnapshot(); }, delayMs);
}

// Capture a PNG of `#doc-canvas` and POST it. Coalesced: we never run two
// captures in parallel (the library walks the DOM and is expensive), and a
// new save during an in-flight capture replaces the queued snapshot.
async function captureAndUploadSnapshot() {
  if (!SNAPSHOT_URL) return;
  const root = document.getElementById('doc-canvas');
  // @ts-ignore — global from CDN
  const lib = typeof htmlToImage !== 'undefined' ? htmlToImage : null;
  if (!root || !lib) return;

  // Source rectangle = full content width × top-cropped-to-card-aspect height.
  // clientWidth (not scrollWidth) skips the scrollbar gutter so the rendered
  // PNG never includes a slice of empty grey strip on the right.
  const contentW = root.clientWidth || root.scrollWidth;
  const contentH = root.scrollHeight;
  const srcW = Math.max(1, contentW);
  const srcH = Math.max(1, Math.min(contentH, Math.round(srcW * SNAPSHOT_ASPECT)));

  // Downscale only — upscaling tiny canvases would just be blurry. The output
  // matches the source's clipped aspect; for the typical case (tall canvas
  // cropped to 4:3) that's exactly SNAPSHOT_W × SNAPSHOT_H. Short canvases
  // produce a shorter PNG which the card's object-fit: cover handles.
  const scale = Math.min(SNAPSHOT_W / srcW, 1);
  const outW = Math.max(1, Math.round(srcW * scale));
  const outH = Math.max(1, Math.round(srcH * scale));

  try {
    const dataUrl = await lib.toPng(root, {
      // Source rectangle = top-cropped canvas region, top-left origin.
      width: srcW,
      height: srcH,
      // Logical output size — html-to-image multiplies by `pixelRatio` to get
      // the actual bitmap dimensions. outW × DPR is what lands in the PNG.
      canvasWidth: outW,
      canvasHeight: outH,
      pixelRatio: SNAPSHOT_DPR,
      backgroundColor: '#FBFBFD',
      cacheBust: true,
      skipFonts: false,
      style: {
        transform: 'none',
        transformOrigin: 'top left',
      },
    });
    const base64 = String(dataUrl).split(',', 2)[1];
    if (!base64) return;
    await fetch(SNAPSHOT_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pngBase64: base64 }),
    });
  } catch {
    /* best-effort; old preview stays until next save */
  }
}

async function flushSave() {
  if (isHydrating()) return;
  if (!SAVE_URL) return;
  const doc = serializeCanvas();
  if (!doc) return;
  try {
    const res = await fetch(SAVE_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ document: JSON.stringify(doc) }),
    });
    if (!res.ok) return;
    // Snapshot in a microtask-ish delay so the just-saved chart frames have a
    // chance to paint before we rasterize. 250ms is empirically enough on a
    // fresh chart insert; smaller mutations don't need any wait but extra
    // 250ms latency is invisible against the 1.5s debounce.
    scheduleSnapshot(250);
  } catch {
    /* network blip — next tick will retry on next edit */
  }
}

function scheduleSave() {
  if (isHydrating()) return;
  if (_saveTimer) clearTimeout(_saveTimer);
  _saveTimer = setTimeout(flushSave, DEBOUNCE_MS);
}

export function startAutoSave() {
  if (!SAVE_URL) return;
  const canvas = document.getElementById('doc-canvas');
  if (!canvas) return;

  const obs = new MutationObserver(scheduleSave);
  obs.observe(canvas, {
    childList: true,
    subtree: true,
    characterData: true,
    attributes: true,
    attributeFilter: ['style', 'data-block-type', 'data-blockType'],
  });

  window.addEventListener('beforeunload', () => {
    if (isHydrating()) return;
    const doc = serializeCanvas();
    if (!doc) return;
    const blob = new Blob([JSON.stringify({ document: JSON.stringify(doc) })], {
      type: 'application/json',
    });
    navigator.sendBeacon?.(SAVE_URL, blob);
    // Snapshot capture is async + library-bound — too slow for sendBeacon's
    // tight unload budget. Skip it here; the next session's first save will
    // produce a fresh thumbnail.
  });
}
