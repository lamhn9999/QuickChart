// @ts-check
/**
 * @file Backward-compat shim.
 *
 * The implementation has been split into ./filter/ (state, api, chip, popup, per-kind files).
 * This shim preserves the import path `'./filter.js'` used by main.js so consumers don't change.
 * Phase 3 will switch main.js to import from './filter/index.js' directly and delete this shim.
 */

export * from './filter/index.js';
