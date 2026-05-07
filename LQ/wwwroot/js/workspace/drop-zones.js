// @ts-check
/**
 * @file Plot-panel drag-over/drop wiring + the cross-module `_chartDropOnCard` shim.
 *
 * The plot panel is the empty area where dropping a column header creates a NEW chart group.
 * Dropping onto an existing card (handled inside chart.js) instead calls window._chartDropOnCard
 * which lives here — it cancels in-flight requests and dispatches to replace or create.
 */

import { appState } from '../core/state.js';
import { createChartGroup, replaceChartGroup } from '../chart.js';
import { clearPending } from './header-drag.js';

/**
 * Wire dragover/dragleave/drop on `plotPanel`. Dropping creates a chart group at the
 * cursor position within `#doc-canvas`.
 * @param {HTMLElement} plotPanel
 */
export function wirePlotPanelDropZone(plotPanel) {
  plotPanel.addEventListener('dragover', (e) => {
    if (!e.dataTransfer?.types.includes('application/json')) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
    plotPanel.classList.add('drop-active');
  });

  plotPanel.addEventListener('dragleave', (e) => {
    const related = /** @type {Node | null} */ (e.relatedTarget);
    if (!related || !plotPanel.contains(related)) plotPanel.classList.remove('drop-active');
  });

  plotPanel.addEventListener('drop', async (e) => {
    e.preventDefault();
    plotPanel.classList.remove('drop-active');
    if (!e.dataTransfer) return;
    try {
      const indexes = JSON.parse(e.dataTransfer.getData('application/json'));
      if (appState.controller) appState.controller.abort('panel-drop');
      clearPending();

      // Place card at cursor position within #doc-canvas
      const docCanvas = document.getElementById('doc-canvas');
      const stage = document.getElementById('canvas-stage') ?? plotPanel;
      const rect = docCanvas?.getBoundingClientRect();
      const scrollTop = stage.scrollTop;
      const scrollLeft = stage.scrollLeft;
      const dropPos = rect
        ? {
            x: e.clientX - rect.left + scrollLeft - 20,
            y: e.clientY - rect.top + scrollTop - 20,
          }
        : null;

      await createChartGroup(indexes, dropPos);
    } catch {
      /* swallow malformed dataTransfer */
    }
  });
}

/**
 * Install `window._chartDropOnCard` — chart.js calls this when a header is dropped onto an
 * existing chart card. Cancels any in-flight chart fetch, clears pending state, then either
 * replaces the target group or creates a new one (if `targetGroupId` is null).
 */
export function installChartDropOnCard() {
  /** @type {any} */ (window)._chartDropOnCard = async function (
    /** @type {number[]} */ combinedIndexes,
    /** @type {number | null} */ targetGroupId = null
  ) {
    document.getElementById('plotPanel')?.classList.remove('drop-active');
    if (appState.controller) appState.controller.abort('card-drop');
    clearPending();
    if (targetGroupId !== null) {
      await replaceChartGroup(targetGroupId, combinedIndexes);
    } else {
      await createChartGroup(combinedIndexes);
    }
  };
}
