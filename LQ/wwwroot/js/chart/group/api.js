// @ts-check
/**
 * @file Chart-group lifecycle: create / replace / remove / clearAll.
 *
 * Each call posts to /workspace with the field indexes (creating fresh AutoChart
 * recommendations), groups the resulting samples by `panelIndex`, and asks build-panel.js
 * to render each panel into a new free-form canvas card. The card is wired with drag,
 * resize, and a ResizeObserver that re-flows the Chart.js instances when its size changes.
 *
 * Foreign-file charts (with `csvFileToken`) get a `_buildUnavailableCard` placeholder when
 * the source CSVFile is no longer accessible (403/404) — we don't auto-prune so a transient
 * permission issue doesn't lose the chart.
 *
 * On module load, we hand `removeChartGroup` to builder.js as a callback so its remove-button
 * onclick can dispatch back into us without forming an import cycle (see the ESM re-export
 * gotcha note in memory).
 */

import { appState } from '../../core/state.js';
import { chartInstances } from '../state.js';
import { docCanvas, getNextCardPosition, updateCanvasSize } from '../canvas-engine/canvas.js';
import { makeDraggable, nextZOrder } from '../canvas-engine/draggable.js';
import { makeResizable } from '../canvas-engine/resizable.js';
import { nextGroupId, getGroup, setGroup, deleteGroup, forEachGroup } from './state.js';
import { buildPanel } from './build-panel.js';
import { setRemoveGroupFn } from './builder.js';
import { buildUnavailableCard } from './unavailable.js';
import { ownerFileToken } from './owner.js';

/**
 * @param {number[] | Iterable<number>} fieldIndexes
 * @param {{ x?: number, y?: number, width?: number, height?: number, csvFileToken?: string | null, seriesState?: any[] | null } | null} [opts]
 */
export async function createChartGroup(fieldIndexes, opts = null) {
  const groupId = nextGroupId();
  const dropPos = opts ?? null;
  const csvFileToken = (opts && opts.csvFileToken) || ownerFileToken();
  const seriesState = (opts && opts.seriesState) || null;

  const selPlot = document.getElementById('selPlot');
  const noPlot = document.getElementById('noPlot');
  const loader = document.getElementById('loader');
  if (selPlot) selPlot.hidden = true;
  if (noPlot) noPlot.hidden = true;
  if (loader) loader.hidden = false;

  appState.controller = new AbortController();
  try {
    const response = await fetch('/workspace', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        csvFileToken,
        fieldIndexes: [...fieldIndexes].map(Number),
      }),
      signal: appState.controller.signal,
    });
    if (loader) loader.hidden = true;

    // Foreign-file charts may fail to fetch when the source CSVFile has been deleted or the
    // user no longer has access. Render a placeholder card instead of crashing the canvas.
    if (response.status === 403 || response.status === 404) {
      if (csvFileToken) {
        const card = buildUnavailableCard(
          groupId,
          /** @type {number[]} */ (Array.isArray(fieldIndexes) ? fieldIndexes : [...fieldIndexes]),
          csvFileToken,
          dropPos
        );
        docCanvas()?.appendChild(card);
        makeDraggable(card);
        updateCanvasSize();
        return groupId;
      }
      return null;
    }

    const result = await response.json();

    if (result.length === 0) {
      if (_groupCount() === 0) {
        if (selPlot) selPlot.hidden = true;
        if (noPlot) noPlot.hidden = false;
      }
      return null;
    }

    // ── Build absolutely-positioned card ──────────────────────────────
    const card = document.createElement('div');
    card.classList.add('canvas-card');
    card.dataset.groupId = String(groupId);

    const pos = dropPos ?? getNextCardPosition();
    const initW = opts?.width ?? 480;
    const initH = opts?.height;
    card.style.cssText =
      `position:absolute;left:${Math.max(0, pos.x ?? 0)}px;top:${Math.max(0, pos.y ?? 0)}px;` +
      `width:${initW}px;${initH ? `height:${initH}px;` : ''}z-index:${nextZOrder()}`;

    // Flex-row container for multiple panels
    const panelsRow = document.createElement('div');
    panelsRow.classList.add('canvas-card-panels', 'group');
    card.appendChild(panelsRow);

    const domsBefore = chartInstances.length;
    /** @type {Record<number, any[]>} */
    const grouped = result.reduce((/** @type {any} */ acc, /** @type {any} */ s) => {
      (acc[s.panelIndex] = acc[s.panelIndex] || []).push(s);
      return acc;
    }, {});
    Object.keys(grouped)
      .sort((a, b) => +a - +b)
      .forEach((pIdx) => {
        buildPanel(
          grouped[/** @type {any} */ (pIdx)],
          /** @type {any} */ (`${pIdx}_g${groupId}`),
          panelsRow,
          groupId,
          /** @type {number[]} */ (Array.isArray(fieldIndexes) ? fieldIndexes : [...fieldIndexes]),
          csvFileToken,
          seriesState
        );
      });

    const newInstances = chartInstances.slice(domsBefore);
    setGroup(groupId, { card, instances: newInstances, ro: null });

    docCanvas()?.appendChild(card);

    // Wire drag, resize, and a single resize-observer for the whole card
    makeDraggable(card);
    makeResizable(card, newInstances);

    const ro = new ResizeObserver(() => {
      newInstances.forEach((c) => c.resize());
    });
    ro.observe(card);
    const rec = getGroup(groupId);
    if (rec) rec.ro = ro;

    requestAnimationFrame(() => {
      newInstances.forEach((c) => c.resize());
      updateCanvasSize();
    });

    return groupId;
  } catch (/** @type {any} */ error) {
    if (loader) loader.hidden = true;
    if (error.name !== 'AbortError') console.error('[createChartGroup] Error:', error);
    if (_groupCount() === 0) {
      if (selPlot) selPlot.hidden = true;
      if (noPlot) noPlot.hidden = false;
    }
    return null;
  }
}

/** @param {number} groupId */
export function removeChartGroup(groupId) {
  const data = getGroup(groupId);
  if (!data) return;

  data.ro?.disconnect();
  data.instances.forEach((/** @type {any} */ c) => {
    c.destroy();
    const i = chartInstances.indexOf(c);
    if (i !== -1) chartInstances.splice(i, 1);
  });
  data.card?.remove();
  deleteGroup(groupId);

  if (_groupCount() === 0) {
    const selPlot = document.getElementById('selPlot');
    const noPlot = document.getElementById('noPlot');
    if (selPlot) selPlot.hidden = false;
    if (noPlot) noPlot.hidden = true;
  }
}

/**
 * Replace an existing card's content with charts from `newFieldIndexes`. The card keeps
 * its current position and size; chart instances + resize observer are torn down and
 * replaced by fresh ones.
 *
 * Replacement headers always come from the active workspace's table, so the rebuilt chart
 * is implicit/native — any csvFileToken the old card carried is dropped.
 *
 * @param {number} oldGroupId
 * @param {number[] | Iterable<number>} newFieldIndexes
 */
export async function replaceChartGroup(oldGroupId, newFieldIndexes) {
  const oldData = getGroup(oldGroupId);
  if (!oldData) return createChartGroup(newFieldIndexes);

  const card = oldData.card;

  const loader = document.getElementById('loader');
  const selPlot = document.getElementById('selPlot');
  const noPlot = document.getElementById('noPlot');
  if (loader) loader.hidden = false;

  appState.controller = new AbortController();
  try {
    const response = await fetch('/workspace', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        csvFileToken: ownerFileToken(),
        fieldIndexes: [...newFieldIndexes].map(Number),
      }),
      signal: appState.controller.signal,
    });
    const result = await response.json();
    if (loader) loader.hidden = true;
    if (result.length === 0) return null;

    // Tear down old instances + observer
    oldData.ro?.disconnect();
    oldData.instances.forEach((/** @type {any} */ c) => {
      c.destroy();
      const i = chartInstances.indexOf(c);
      if (i !== -1) chartInstances.splice(i, 1);
    });
    deleteGroup(oldGroupId);

    // Clear panels + old resize handles, keep card element in place
    const panelsRow = /** @type {HTMLElement | null} */ (card.querySelector('.canvas-card-panels'));
    if (panelsRow) panelsRow.innerHTML = '';
    card.querySelectorAll('[data-resize-dir]').forEach((el) => el.remove());

    const groupId = nextGroupId();
    card.dataset.groupId = String(groupId);

    const ownerFileTk = ownerFileToken();
    const domsBefore = chartInstances.length;
    /** @type {Record<number, any[]>} */
    const grouped = result.reduce((/** @type {any} */ acc, /** @type {any} */ s) => {
      (acc[s.panelIndex] = acc[s.panelIndex] || []).push(s);
      return acc;
    }, {});
    Object.keys(grouped)
      .sort((a, b) => +a - +b)
      .forEach((pIdx) => {
        buildPanel(
          grouped[/** @type {any} */ (pIdx)],
          /** @type {any} */ (`${pIdx}_g${groupId}`),
          /** @type {HTMLElement} */ (panelsRow),
          groupId,
          /** @type {number[]} */ (
            Array.isArray(newFieldIndexes) ? newFieldIndexes : [...newFieldIndexes]
          ),
          ownerFileTk,
          null
        );
      });

    const newInstances = chartInstances.slice(domsBefore);
    setGroup(groupId, { card, instances: newInstances, ro: null });

    // Re-wire draggable (new drag-handles were created by buildPanel)
    makeDraggable(card);
    makeResizable(card, newInstances);

    const ro = new ResizeObserver(() => {
      newInstances.forEach((c) => c.resize());
    });
    ro.observe(card);
    const rec = getGroup(groupId);
    if (rec) rec.ro = ro;

    requestAnimationFrame(() => {
      newInstances.forEach((c) => c.resize());
    });
    return groupId;
  } catch (/** @type {any} */ error) {
    if (loader) loader.hidden = true;
    if (error.name !== 'AbortError') console.error('[replaceChartGroup] Error:', error);
    if (_groupCount() === 0) {
      if (selPlot) selPlot.hidden = false;
      if (noPlot) noPlot.hidden = true;
    }
    return null;
  }
}

export function clearAllChartGroups() {
  /** @type {number[]} */
  const ids = [];
  forEachGroup((_rec, id) => ids.push(id));
  ids.forEach(removeChartGroup);
}

// ── helpers ──────────────────────────────────────────────────────────────────

/** Count of currently-registered groups (used by the empty-canvas placeholder logic). */
function _groupCount() {
  let n = 0;
  forEachGroup(() => {
    n++;
  });
  return n;
}

// ── builder.js callback wiring ───────────────────────────────────────────────
// Hand removeChartGroup to builder.js so its per-card "remove" button can fire it
// without forming an import cycle. See feedback_esm_reexport_local_scope.md.
setRemoveGroupFn(removeChartGroup);
