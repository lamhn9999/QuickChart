// @ts-check
/**
 * @file Filter-card registry and selection state.
 *
 * One FilterRecord per filter card on the canvas, keyed by the same `groupId`
 * the chart-card registry uses (filter cards inherit their host chart card's
 * group id when converted, so persistence keeps a stable handle). The
 * selection state is the single currently-clicked filter card whose context
 * the right panel is showing.
 *
 * Why a separate registry from chart/group/state.js: chart groups own a
 * Chart.js instance + ResizeObserver; filter cards own a value-selection set +
 * an "apply to" set. Sharing the record shape would force the chart-refresh
 * module to know about filter internals.
 */

/**
 * @typedef {'buttons' | 'search'} FilterVariant
 *
 * @typedef {Object} FilterRecord
 * @property {number}        groupId         // matches the .canvas-card[data-group-id]
 * @property {number}        fieldIndex      // single-field by design (see plan §3)
 * @property {string|null}   csvFileToken    // source file this fieldIndex is bound to;
 *                                           //   null means "use the canvas owner".
 *                                           //   Set when a filter is copied from another
 *                                           //   file or hosts a foreign-file chart, so the
 *                                           //   fieldIndex never gets reinterpreted against
 *                                           //   the wrong schema.
 * @property {string}        fieldName
 * @property {number}        fieldType       // FilterFieldType enum (0..5)
 * @property {string|null}   typeName        // raw .NET type name from server
 * @property {FilterVariant} variant
 * @property {(string|null)[]} availableValues  // populated for buttons variant; [] for search
 * @property {Set<string>}   selectedValues  // currently-active filter values
 * @property {Set<number>}   appliedTo       // chart group ids this filter narrows
 */

/** @type {Map<number, FilterRecord>} */
const _filters = new Map();

/** @type {number | null} */
let _selectedFilterId = null;

/** @param {number} groupId @param {FilterRecord} rec */
export function setFilter(groupId, rec) {
  _filters.set(groupId, rec);
}

/** @param {number} groupId */
export function getFilter(groupId) {
  return _filters.get(groupId);
}

/** @param {number} groupId */
export function deleteFilter(groupId) {
  _filters.delete(groupId);
  if (_selectedFilterId === groupId) _selectedFilterId = null;
}

/** Iterate every (groupId, record) pair. */
export function forEachFilter(/** @type {(rec: FilterRecord, groupId: number) => void} */ fn) {
  _filters.forEach(fn);
}

/** All registered filter records, returned as a fresh array (safe to iterate while mutating). */
export function listFilters() {
  return [..._filters.values()];
}

export function getSelectedFilterId() {
  return _selectedFilterId;
}

/** @param {number | null} id */
export function setSelectedFilterId(id) {
  _selectedFilterId = id;
}

/**
 * Map a .NET TypeName onto the C# FilterFieldType enum used by ValueFilterDTO.
 * The same mapping lives in filter/state.js (enumMap); we duplicate the small
 * subset we need here to avoid importing that module's session-bound state.
 *
 * @param {string | null | undefined} typeName
 * @returns {number}  // FilterFieldType: 0=String 1=Int 2=Double 3=DateTime 4=Bool 5=ListString
 */
export function fieldTypeFromTypeName(typeName) {
  if (!typeName) return 0;
  if (typeName.startsWith('System.Int32') || typeName.startsWith('System.Int64')) return 1;
  if (typeName.startsWith('System.Double') || typeName.startsWith('System.Single')
   || typeName.startsWith('System.Decimal')) return 2;
  if (typeName.startsWith('System.DateTime')) return 3;
  if (typeName.startsWith('System.Boolean')) return 4;
  if (typeName.includes('List`1') && typeName.includes('System.String')) return 5;
  return 0; // System.String and anything unrecognised
}
