// @ts-check
/**
 * @file Block-kind registry — each kind knows how to recognise, serialise, and hydrate one
 * canvas element. Adding a new persistable element type is a single new entry here.
 *
 * Order matters: the chart predicate must precede textCard because both share `.canvas-card`.
 * The chart predicate guards on `[data-field-indexes]` so the two are mutually exclusive —
 * order is defence-in-depth.
 *
 * `attachesItself: true` means hydrate already appends to `#doc-canvas` (chart cards and
 * text cards build absolutely-positioned children themselves). Flow blocks return their
 * element and let `hydrateCanvas` append.
 */

import { createChartGroup, createTextCard } from '../chart.js';
import { recreateFilterCard } from '../per-chart-filter/index.js';
import { getFilter } from '../per-chart-filter/state.js';

/**
 * @typedef {Object} BlockKind
 * @property {string} name
 * @property {(el: Element) => boolean} match
 * @property {(el: Element) => Record<string, any>} serialize
 * @property {(spec: any) => HTMLElement | null | Promise<HTMLElement | null>} hydrate
 * @property {boolean} [attachesItself]
 */

// ── DOM helpers for flow blocks ──────────────────────────────────────────────

/**
 * @param {string} blockType
 * @param {string} content
 */
function makeTextBlock(blockType, content) {
  const el = document.createElement('div');
  el.className = 'doc-text';
  el.contentEditable = 'true';
  el.spellcheck = false;
  el.dataset.blockType = blockType || 'paragraph';
  if (content) el.textContent = content;
  // editor.js MutationObserver auto-wires .doc-text listeners + placeholder.
  return el;
}

function makeDividerBlock() {
  const wrap = document.createElement('div');
  wrap.className = 'doc-divider';
  wrap.dataset.blockType = 'divider';
  wrap.appendChild(document.createElement('hr'));
  return wrap;
}

/**
 * The Razor view emits this so we know which CSVFile token counts as "implicit / native"
 * for chart blocks. Charts whose csvFileToken matches the owner are serialized without the
 * token (compactness + back-compat); foreign-file charts always carry it.
 */
export function getOwnerFileToken() {
  const tag = document.getElementById('canvas-owner-file-token');
  if (!tag) return null;
  try {
    const v = JSON.parse(tag.textContent ?? '');
    return typeof v === 'string' && v.length > 0 ? v : null;
  } catch {
    return null;
  }
}

/** @type {BlockKind[]} */
export const KINDS = [
  {
    name: 'text',
    match: (el) => el.classList.contains('doc-text'),
    serialize: (el) => ({
      blockType: /** @type {HTMLElement} */ (el).dataset.blockType || 'paragraph',
      content: el.textContent || '',
    }),
    hydrate: (spec) => makeTextBlock(spec.blockType, spec.content),
  },
  {
    name: 'divider',
    match: (el) => el.classList.contains('doc-divider'),
    serialize: () => ({}),
    hydrate: () => makeDividerBlock(),
  },
  {
    // Per-chart filter cards are absolutely-positioned canvas cards just
    // like chart and textCard. They must be matched BEFORE the chart kind
    // because they share `.canvas-card` (the filter-card-host class is
    // additive). No `[data-field-indexes]` lives inside a filter card, so
    // the chart matcher would still fall through correctly, but ordering
    // here is defence-in-depth.
    name: 'filter',
    match: (el) => el.classList.contains('canvas-card')
      && el.classList.contains('filter-card-host'),
    serialize: (el) => {
      const card = /** @type {HTMLElement} */ (el);
      const groupId = Number(card.dataset.groupId);
      const rec = Number.isFinite(groupId) ? getFilter(groupId) : null;
      if (!rec) return {};
      /** @type {Record<string, any>} */
      const out = {
        fieldIndex: rec.fieldIndex,
        appliedTo: [...rec.appliedTo],
        x: parseFloat(card.style.left) || 0,
        y: parseFloat(card.style.top) || 0,
        width: card.offsetWidth,
        height: card.offsetHeight,
      };
      // Same compactness rule as the chart kind: emit csvFileToken only when
      // the filter's source differs from this canvas's owner. Pre-existing
      // filter blocks (saved before this field existed) just default to the
      // owner on hydrate, matching their old behaviour.
      if (rec.csvFileToken && rec.csvFileToken !== getOwnerFileToken()) {
        out.csvFileToken = rec.csvFileToken;
      }
      return out;
    },
    hydrate: (spec) => recreateFilterCard(spec),
    attachesItself: true,
  },
  {
    name: 'chart',
    match: (el) =>
      el.classList.contains('canvas-card') &&
      !el.classList.contains('text-card') &&
      !el.classList.contains('filter-card-host') &&
      el.querySelector('[data-field-indexes]') !== null,
    serialize: (el) => {
      const card = /** @type {HTMLElement} */ (el);
      const panel = /** @type {HTMLElement | null} */ (card.querySelector('[data-field-indexes]'));
      if (!panel) return {};
      /** @type {number[]} */
      let fieldIndexes;
      try {
        fieldIndexes = JSON.parse(panel.dataset.fieldIndexes ?? '[]');
      } catch {
        fieldIndexes = [];
      }
      /** @type {Record<string, any>} */
      const out = {
        fieldIndexes,
        x: parseFloat(card.style.left) || 0,
        y: parseFloat(card.style.top) || 0,
        width: card.offsetWidth,
        height: card.offsetHeight,
      };
      // Emit csvFileToken only when the chart's source differs from this canvas's owner —
      // keeps the common (native) case compact and backward-compatible with rows persisted
      // before cross-file charts.
      const panelToken = panel.dataset.csvFileToken;
      if (panelToken && panelToken !== getOwnerFileToken()) out.csvFileToken = panelToken;
      const wrappers = [...panel.querySelectorAll('[data-chart-type]')];
      const visible =
        wrappers.find((w) => /** @type {HTMLElement} */ (w).style.display !== 'none') ??
        wrappers[0];
      const visibleEl = /** @type {HTMLElement | undefined} */ (visible);
      if (visibleEl?.dataset.chartType != null) {
        out.chartType = Number(visibleEl.dataset.chartType);
      }
      return out;
    },
    hydrate: async (spec) => {
      if (!Array.isArray(spec.fieldIndexes) || spec.fieldIndexes.length === 0) return null;
      // Width/height go through createChartGroup so the card is sized up-front. Chart.js
      // measures its parent at construction time; resizing AFTER would leave the canvas at
      // its default 480px until a later redraw.
      const groupId = await createChartGroup(spec.fieldIndexes, {
        x: spec.x ?? 0,
        y: spec.y ?? 0,
        csvFileToken: spec.csvFileToken ?? null,
        width: spec.width,
        height: spec.height,
      });
      if (groupId == null) return null;
      const card = /** @type {HTMLElement | null} */ (
        document.querySelector(`#doc-canvas .canvas-card[data-group-id="${groupId}"]`)
      );
      if (!card) return null;
      if (Number.isInteger(spec.chartType)) {
        const targets = card.querySelectorAll(`[data-chart-type="${spec.chartType}"]`);
        targets.forEach((targetNode) => {
          const target = /** @type {HTMLElement} */ (targetNode);
          const siblings = target.parentNode?.children ?? [];
          for (const w of siblings) {
            const we = /** @type {HTMLElement} */ (w);
            if (we.dataset && we.dataset.chartType !== undefined) {
              we.style.display = w === target ? 'flex' : 'none';
            }
          }
          const c = /** @type {HTMLCanvasElement | null} */ (target.querySelector('canvas'));
          // @ts-ignore — Chart is a runtime global
          const inst = c && (typeof Chart !== 'undefined' ? Chart.getChart(c) : null);
          if (inst) requestAnimationFrame(() => inst.resize());
        });
      }
      return card;
    },
    attachesItself: true,
  },
  {
    name: 'textCard',
    match: (el) => el.classList.contains('canvas-card')
      && el.classList.contains('text-card')
      && !el.classList.contains('filter-card-host'),
    serialize: (el) => {
      const card = /** @type {HTMLElement} */ (el);
      const c = /** @type {HTMLElement | null} */ (card.querySelector('.tc-content'));
      return {
        content: c?.textContent ?? '',
        blockType: c?.dataset.blockType || 'paragraph',
        x: parseFloat(card.style.left) || 0,
        y: parseFloat(card.style.top) || 0,
        width: card.offsetWidth,
        // height intentionally omitted — text cards auto-size vertically
      };
    },
    hydrate: (spec) => {
      const card = createTextCard({ x: spec.x ?? 0, y: spec.y ?? 0 });
      if (!card) return null;
      const c = /** @type {HTMLElement | null} */ (card.querySelector('.tc-content'));
      if (c) {
        if (spec.blockType) c.dataset.blockType = spec.blockType;
        if (spec.content) c.textContent = spec.content;
      }
      if (spec.width) card.style.width = `${spec.width}px`;
      return card;
    },
    attachesItself: true,
  },
];

export const KINDS_BY_NAME = Object.fromEntries(KINDS.map((k) => [k.name, k]));
