// @ts-check
/**
 * @file Encapsulated filter state.
 *
 * The `filters` array is sparse — index = field index. Slots are either:
 *   - `undefined` (no filter on that column), OR
 *   - a filter spec object compatible with the server's POST /workspace/filter payload.
 *
 * External modules MUST go through these accessors. Direct mutation of the array is forbidden.
 * Live references returned by `getFilter()` may be mutated in place (push to `filteringValues`,
 * set `isNot`, etc.) — that's the whole point of returning the live object.
 */

/** @typedef {{leftBound: number, rightBound: number}} NumericBound */
/** @typedef {{leftBound: string, rightBound: string}} DateBound */

/**
 * @typedef {Object} FilterSpec
 * @property {number} fieldIndex
 * @property {number} fieldType                                    // see enumMap
 * @property {boolean} [isNot]
 * @property {boolean} [isPartialMatch]
 * @property {string[] | null}  [filteringValues]
 * @property {number[] | null}  [filteringValuesInt]
 * @property {number[] | null}  [filteringValuesDouble]
 * @property {NumericBound | null} [intBound]
 * @property {NumericBound | null} [doubleBound]
 * @property {DateBound | null}    [dateTimeBound]
 */

/** @type {(FilterSpec | undefined)[]} */
const filters = [];

/** Numeric tag used by the server's FieldType enum. Mirrors C# ChartType ordering. */
/** @type {Record<string, number>} */
export const enumMap = {
  String: 0,
  Int: 1,
  Double: 2,
  DateTime: 3,
  Bool: 4,
  ListString: 5,
};

/**
 * Live reference to the spec at `index`, or undefined.
 * @param {number | string} index
 * @returns {FilterSpec | undefined}
 */
export const getFilter = (index) => filters[/** @type {any} */ (index)];

/**
 * Replace (or create) the spec at `index`. Pass `undefined` to remove.
 * @param {number | string} index
 * @param {FilterSpec | undefined} spec
 */
export function setFilter(index, spec) {
  filters[/** @type {any} */ (index)] = spec;
}

/**
 * Remove the spec at `index` (sets it to undefined).
 * @param {number | string} index
 */
export const removeFilter = (index) => setFilter(index, undefined);

/** Snapshot of all currently-active specs (sparse holes filtered out). */
export const getActiveFilters = () => filters.filter((f) => f !== undefined);
