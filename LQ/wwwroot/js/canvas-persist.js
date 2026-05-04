// Persist + hydrate the document canvas (#doc-canvas) for the current CSVFile.
//
// Wire-up:
//   1. The Razor view emits <script id="saved-canvas" type="application/json">
//      with the previously-saved document (or the literal "null").
//   2. On DOMContentLoaded, hydrateCanvas() reads that script and reconstructs
//      blocks + chart cards.
//   3. After hydration, a debounced auto-save observer watches #doc-canvas and
//      POSTs to /workspace/canvas. beforeunload flushes via sendBeacon.
//
// Adding a new persistable element type is a single new entry in the KINDS
// registry below — define how to recognise, serialise, and hydrate it.

import { createChartGroup, createTextCard } from './chart.js?v=3';

const SAVE_URL = '/workspace/canvas';
const DEBOUNCE_MS = 1500;

// The Razor view emits this so we know which CSVFile token counts as "implicit /
// native" for chart blocks. Charts whose csvFileToken matches the owner are
// serialized without the token (compactness + back-compat); foreign-file
// charts always carry it.
function getOwnerFileToken() {
    const tag = document.getElementById('canvas-owner-file-token');
    if (!tag) return null;
    try {
        const v = JSON.parse(tag.textContent);
        return typeof v === 'string' && v.length > 0 ? v : null;
    } catch {
        return null;
    }
}

// ── DOM helpers for flow blocks ──────────────────────────────────────────────

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

// ── Block-kind registry ──────────────────────────────────────────────────────
//
// Each entry knows how to recognise, serialise, and hydrate one canvas element.
// `match(el)`     — predicate, used by the serializer to find the kind.
// `serialize(el)` — returns the kind-specific payload (no `kind` field; wrapper adds it).
// `hydrate(spec)` — builds the DOM. May be sync or async.
// `attachesItself`— true when hydrate already appends the element to #doc-canvas
//                   (chart cards and text cards build absolutely-positioned
//                   children themselves; flow blocks expect the wrapper to append).
//
// Order matters: the chart predicate must precede textCard because both share
// `.canvas-card`. The chart predicate guards on `[data-field-indexes]` so the
// two are mutually exclusive — order is defence-in-depth.

const KINDS = [
    {
        name: 'text',
        match: el => el.classList.contains('doc-text'),
        serialize: el => ({
            blockType: el.dataset.blockType || 'paragraph',
            content:   el.textContent || '',
        }),
        hydrate: spec => makeTextBlock(spec.blockType, spec.content),
    },
    {
        name: 'divider',
        match: el => el.classList.contains('doc-divider'),
        serialize: () => ({}),
        hydrate: () => makeDividerBlock(),
    },
    {
        name: 'chart',
        match: el => el.classList.contains('canvas-card')
                  && !el.classList.contains('text-card')
                  && el.querySelector('[data-field-indexes]') !== null,
        serialize: el => {
            const panel = el.querySelector('[data-field-indexes]');
            let fieldIndexes;
            try { fieldIndexes = JSON.parse(panel.dataset.fieldIndexes); }
            catch { fieldIndexes = []; }
            const out = {
                fieldIndexes,
                x: parseFloat(el.style.left) || 0,
                y: parseFloat(el.style.top)  || 0,
                width:  el.offsetWidth,
                height: el.offsetHeight,
            };
            // Emit csvFileToken only when the chart's source differs from this
            // canvas's owner — keeps the common (native) case compact and
            // backward-compatible with rows persisted before cross-file charts.
            const panelToken = panel.dataset.csvFileToken;
            if (panelToken && panelToken !== getOwnerFileToken()) {
                out.csvFileToken = panelToken;
            }
            return out;
        },
        hydrate: async spec => {
            if (!Array.isArray(spec.fieldIndexes) || spec.fieldIndexes.length === 0) return null;
            const groupId = await createChartGroup(
                spec.fieldIndexes,
                { x: spec.x ?? 0, y: spec.y ?? 0, csvFileToken: spec.csvFileToken ?? null }
            );
            if (groupId == null) return null;
            const card = document.querySelector(`#doc-canvas .canvas-card[data-groupId="${groupId}"]`);
            if (!card) return null;
            if (spec.width)  card.style.width  = `${spec.width}px`;
            if (spec.height) card.style.height = `${spec.height}px`;
            return card;
        },
        attachesItself: true,
    },
    {
        name: 'textCard',
        match: el => el.classList.contains('canvas-card')
                  && el.classList.contains('text-card'),
        serialize: el => ({
            content: el.querySelector('.tc-content')?.textContent ?? '',
            x: parseFloat(el.style.left) || 0,
            y: parseFloat(el.style.top)  || 0,
            width:  el.offsetWidth,
            // height intentionally omitted — text cards auto-size vertically
        }),
        hydrate: spec => {
            const card = createTextCard({ x: spec.x ?? 0, y: spec.y ?? 0 });
            if (!card) return null;
            const c = card.querySelector('.tc-content');
            if (c && spec.content) c.textContent = spec.content;
            if (spec.width) card.style.width = `${spec.width}px`;
            return card;
        },
        attachesItself: true,
    },
];

const KINDS_BY_NAME = Object.fromEntries(KINDS.map(k => [k.name, k]));

// ── Serialization / hydration ────────────────────────────────────────────────

function serializeCanvas() {
    const canvas = document.getElementById('doc-canvas');
    if (!canvas) return null;

    const titleEl = canvas.querySelector('#doc-title');
    const title = titleEl ? titleEl.textContent : '';

    const blocks = [];
    for (const child of canvas.children) {
        if (child.id === 'doc-title') continue;
        const kind = KINDS.find(k => k.match(child));
        if (!kind) continue;
        blocks.push({ kind: kind.name, ...kind.serialize(child) });
    }
    return { title, blocks };
}

async function hydrateCanvas(doc) {
    const canvas = document.getElementById('doc-canvas');
    if (!canvas || !doc || !Array.isArray(doc.blocks)) return;

    const titleEl = canvas.querySelector('#doc-title');
    if (titleEl && typeof doc.title === 'string') titleEl.textContent = doc.title;

    for (const spec of doc.blocks) {
        const kind = KINDS_BY_NAME[spec.kind];
        if (!kind) continue;
        const el = await kind.hydrate(spec);
        if (el && !kind.attachesItself) canvas.appendChild(el);
    }
}

// ── Auto-save ────────────────────────────────────────────────────────────────

let _saveTimer = null;
let _hydrating = false;

async function flushSave() {
    if (_hydrating) return;
    const doc = serializeCanvas();
    if (!doc) return;
    try {
        await fetch(SAVE_URL, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ document: JSON.stringify(doc) }),
        });
    } catch { /* network blip — next tick will retry on next edit */ }
}

function scheduleSave() {
    if (_hydrating) return;
    if (_saveTimer) clearTimeout(_saveTimer);
    _saveTimer = setTimeout(flushSave, DEBOUNCE_MS);
}

function startAutoSave() {
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
        if (_hydrating) return;
        const doc = serializeCanvas();
        if (!doc) return;
        const blob = new Blob(
            [JSON.stringify({ document: JSON.stringify(doc) })],
            { type: 'application/json' }
        );
        navigator.sendBeacon?.(SAVE_URL, blob);
    });
}

// ── Boot ─────────────────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', async () => {
    const tag = document.getElementById('saved-canvas');
    let doc = null;
    if (tag) {
        try { doc = JSON.parse(tag.textContent); }
        catch { doc = null; }
    }

    if (doc) {
        _hydrating = true;
        try { await hydrateCanvas(doc); }
        finally { _hydrating = false; }
    }

    startAutoSave();
});
