// @ts-check
/**
 * @file Per-chart filter feature entry point. Imported once by
 * workspace/index.js. All per-card and right-panel event handlers are
 * exported from ./events for the workspace action-dispatch table to wire.
 *
 * The init step here only sets up the "click outside / click filter card"
 * selection contract, since that listens at the document level rather than
 * on a delegated data-action. Every other interaction uses data-action on
 * the rendered DOM and is routed by the workspace dispatcher.
 */

import { initFilterSelection } from './selection.js';

export function initPerChartFilter() {
  initFilterSelection();
}

// Re-export action handlers for workspace/index.js to register. Single
// import surface so the dispatcher table reads cleanly.
export {
  onPillClick,
  onSearchEnter,
  onChipRemove,
  onClear,
  onApplyToHover,
  onApplyToUnhover,
  onApplyToToggle,
  onUseAsFilter,
} from './events.js';

// Hydrate path for canvas persistence.
export { recreateFilterCard } from './convert.js';
