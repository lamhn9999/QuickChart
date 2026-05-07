// @ts-check
/**
 * @file Debounced auto-save — observes `#doc-canvas` mutations and POSTs the serialized
 * document to /workspace/canvas after 1.5s of inactivity. `beforeunload` flushes via
 * `navigator.sendBeacon` so unsaved edits survive a page close.
 *
 * All save paths gate on `isHydrating()` so the boot-time hydration replay doesn't trigger
 * a save round-trip.
 */

import { isHydrating } from './state.js';
import { serializeCanvas } from './serialize.js';

const SAVE_URL = '/workspace/canvas';
const DEBOUNCE_MS = 1500;

/** @type {any} */
let _saveTimer = null;

async function flushSave() {
  if (isHydrating()) return;
  const doc = serializeCanvas();
  if (!doc) return;
  try {
    await fetch(SAVE_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ document: JSON.stringify(doc) }),
    });
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
  });
}
