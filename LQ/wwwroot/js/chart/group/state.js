// @ts-check
/**
 * @file Chart-group registry — internal lookup from groupId → DOM card + Chart.js instances.
 *
 * Used by:
 *   - api.js (createChartGroup writes; removeChartGroup deletes; clearAllChartGroups iterates)
 *   - builder.js (appendChart never reaches into here directly, but the Drop-onto-card
 *     bridge `_chartDropOnCard` resolves a target groupId by reading dataset.groupId).
 *   - unavailable.js (writes a placeholder entry so removeChartGroup still works on
 *     foreign-file unavailable cards).
 *
 * Counter increments are exposed via `nextGroupId()` rather than a raw `let` export so
 * callers can't accidentally clobber it.
 */

let _groupCounter = 0;

/** @typedef {{ card: HTMLElement, instances: any[], ro: ResizeObserver | null }} GroupRecord */

/** @type {Map<number, GroupRecord>} */
const _groupData = new Map();

/** Increment + return the next unique group id. Monotonically increasing across the session. */
export function nextGroupId() {
  return ++_groupCounter;
}

/** @param {number} id */
export function getGroup(id) {
  return _groupData.get(id);
}

/**
 * @param {number} id
 * @param {GroupRecord} record
 */
export function setGroup(id, record) {
  _groupData.set(id, record);
}

/** @param {number} id */
export function deleteGroup(id) {
  return _groupData.delete(id);
}

/** Iterate every (id, record) pair. */
export function forEachGroup(/** @type {(record: GroupRecord, id: number) => void} */ fn) {
  _groupData.forEach(fn);
}

/** Wipe the registry. Caller is responsible for destroying chart instances + removing DOM. */
export function clearGroups() {
  _groupData.clear();
}
