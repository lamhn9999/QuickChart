// @ts-check
/**
 * @file Chart-package public surface — a thin re-export shim.
 *
 * After Phases 4–6, the implementation lives entirely under `chart/` (canvas-engine,
 * theme/colors/format/data-utils, sparklines, text-card, zoom/, group/). This file
 * preserves the import path `'./chart.js'` used by `workspace/` and `canvas-persist.js`
 * so consumers don't need to change.
 *
 * Module layout under chart/:
 *   chart/state.js                 chartInstances
 *   chart/theme.js                 DARK/LIGHT tokens, axisStyle, applyChartTheme
 *   chart/colors.js                getContrastTextColor, reformatColor
 *   chart/format.js                fmtNum, drawHistogram
 *   chart/data-utils.js            buildHeatmapData, buildMatrixDataset, drawBoxPlot
 *   chart/canvas-engine/canvas.js  docCanvas, getNextCardPosition, updateCanvasSize,
 *                                   exportCanvas, setCanvasSize, setCanvasBg
 *   chart/canvas-engine/snap.js    snapWithGuides, clearGuides
 *   chart/canvas-engine/draggable.js  makeDraggable, nextZOrder
 *   chart/canvas-engine/resizable.js  makeResizable, wireTextCardResize
 *   chart/text-card.js             createTextCard
 *   chart/sparklines.js            initSparklines
 *   chart/zoom/navigator.js        navInit, navDestroy
 *   chart/zoom/overlay.js          openZoom, closeZoom
 *   chart/zoom/panel/              initZoomPanel + 8 sub-modules
 *   chart/group/state.js           nextGroupId, getGroup, setGroup, deleteGroup
 *   chart/group/types.js           chartArray, CHART_META
 *   chart/group/owner.js           ownerFileToken
 *   chart/group/copy.js            copyChartToFile
 *   chart/group/unavailable.js     buildUnavailableCard
 *   chart/group/build-panel.js     buildPanel (Chart.js dataset/option config per type)
 *   chart/group/builder.js         appendChart (toolbar + drop-zone)
 *   chart/group/api.js             createChartGroup, replaceChartGroup, removeChartGroup,
 *                                   clearAllChartGroups
 */

// Side-effect import: api.js installs its removeChartGroup callback into builder.js on load.
import './chart/group/api.js';

// One global default tweak (cornerRadius) applied once per page load. Could move to a
// dedicated init module later, but it's small enough to keep here.
// @ts-ignore — Chart is a runtime global from chart.umd.min.js
Chart.defaults.plugins.tooltip.cornerRadius = 4;
// @ts-ignore
Chart.defaults.plugins.legend.labels.borderRadius = 4;

// ── Public re-exports ────────────────────────────────────────────────────────

export { chartInstances } from './chart/state.js';
export { chartArray } from './chart/group/types.js';

// Canvas + free-form layout
export { exportCanvas, setCanvasSize, setCanvasBg } from './chart/canvas-engine/canvas.js';

// Sparklines
export { initSparklines } from './chart/sparklines.js';

// Text cards
export { createTextCard } from './chart/text-card.js';

// Zoom overlay
export { openZoom, closeZoom } from './chart/zoom/overlay.js';

// Group lifecycle
export {
  createChartGroup,
  removeChartGroup,
  replaceChartGroup,
  clearAllChartGroups,
} from './chart/group/api.js';
