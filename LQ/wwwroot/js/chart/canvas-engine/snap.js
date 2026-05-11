// @ts-check
/**
 * @file Free-form canvas alignment guides + snap-to-edge during drag.
 *
 * `snapWithGuides(dragging, rawX, rawY)` is the per-mousemove call that:
 *   1. Collects every candidate alignment (edges, centers, mirror edges) against
 *      every other card on the canvas.
 *   2. Picks the **nearest** candidate per axis so behaviour is deterministic
 *      regardless of DOM order (no accumulation).
 *   3. Snaps to canvas container boundaries (top/left/right/bottom).
 *   4. Optionally snaps to a grid (toggled via `setGridSnap`).
 *   5. Draws guide lines + spacing labels so the user sees what they snapped to.
 * `clearGuides()` wipes the overlay (called on pointerup).
 */

import { docCanvas } from './canvas.js';

const SNAP_THRESHOLD = 8;
const GRID_SIZE = 20;

/** @type {boolean} */
let _gridSnap = false;

export function setGridSnap(force) {
  _gridSnap = force !== undefined ? force : !_gridSnap;
}

export function getGridSnap() {
  return _gridSnap;
}

function roundToGrid(v) {
  return Math.round(v / GRID_SIZE) * GRID_SIZE;
}

/**
 * Lazy-create the absolute-positioned overlay div that hosts guide lines.
 * @returns {HTMLElement | null}
 */
function guidesOverlay() {
  const canvas = docCanvas();
  if (!canvas) return null;
  let el = document.getElementById('_canvas-guides');
  if (!el) {
    el = document.createElement('div');
    el.id = '_canvas-guides';
    el.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:8000;overflow:hidden';
    canvas.prepend(el);
  }
  return el;
}

/** Wipe all guide lines. Called on pointerup. */
export function clearGuides() {
  const el = document.getElementById('_canvas-guides');
  if (el) el.innerHTML = '';
}

/**
 * Draw a guide line + optional spacing label.
 * @param {HTMLElement} overlay
 * @param {'v' | 'h'} axis
 * @param {number} pos
 * @param {string} [label]
 * @param {'blue' | 'purple'} [color]
 */
function drawGuide(overlay, axis, pos, label, color) {
  const isV = axis === 'v';
  const clr = color === 'purple' ? 'rgba(168,85,247,0.55)' : 'rgba(59,130,246,0.6)';

  const ln = document.createElement('div');
  ln.style.cssText = [
    'position:absolute',
    isV ? `left:${pos}px;top:0;bottom:0;width:1px` : `top:${pos}px;left:0;right:0;height:1px`,
    `background:${clr}`,
    'z-index:1',
  ].join(';');
  overlay.appendChild(ln);

  if (label) {
    const lbl = document.createElement('div');
    lbl.textContent = label;
    lbl.style.cssText = [
      'position:absolute',
      'font-size:10px',
      'color:rgba(59,130,246,0.9)',
      'background:rgba(255,255,255,0.85)',
      'padding:1px 5px',
      'border-radius:3px',
      'font-family:monospace',
      'pointer-events:none',
      'white-space:nowrap',
      'line-height:1.4',
      'letter-spacing:0',
      'z-index:2',
      isV ? `left:${pos + 4}px;top:4px` : `top:${pos + 4}px;left:4px`,
    ].join(';');
    overlay.appendChild(lbl);
  }
}

/**
 * Given an array of candidate delta values, pick the nearest one to zero
 * that is within SNAP_THRESHOLD.
 * @param {number[]} deltas
 * @returns {{ delta: number | null, index: number }}
 */
function pickNearest(deltas) {
  let best = { delta: null, index: -1 };
  for (let i = 0; i < deltas.length; i++) {
    const d = deltas[i];
    if (Math.abs(d) < SNAP_THRESHOLD) {
      if (best.delta === null || Math.abs(d) < Math.abs(best.delta)) {
        best = { delta: d, index: i };
      }
    }
  }
  return best;
}

/**
 * Build an array of all candidate snap deltas for the X axis.
 * For each alignment type, compute: delta = otherEdge - selfEdge
 * Applying ownPos + delta snaps the card so selfEdge aligns with otherEdge.
 *
 * @param {number} x  current unsnapped x
 * @param {number} w  dragging card width
 * @param {{ el: HTMLElement, ox: number, ow: number }[]} others
 * @returns {{ delta: number, guidePos: number, label: string | null }[]}
 */
function xCandidates(x, w, others) {
  /** @type {{ delta: number, guidePos: number, label: string | null }[]} */
  const cs = [];
  const selfEdges = [0, w / 2, w];

  for (const o of others) {
    const otherEdges = [0, o.ow / 2, o.ow];

    // Direct alignments: self-edge ↔ same other-edge (left↔left, center↔center, right↔right)
    for (let si = 0; si < 3; si++) {
      const se = selfEdges[si];
      const oe = otherEdges[si];
      const delta = (o.ox + oe) - (x + se);
      if (Math.abs(delta) < SNAP_THRESHOLD) {
        cs.push({ delta, guidePos: o.ox + oe, label: delta !== 0 ? `${Math.abs(Math.round(delta))}px` : null });
      }
    }

    // Mirror alignments: self-left ↔ other-right, self-right ↔ other-left
    const mirrorDeltas = [
      (o.ox + o.ow) - (x + 0),      // self-left ↔ other-right
      (o.ox + 0) - (x + w),          // self-right ↔ other-left
    ];
    for (const md of mirrorDeltas) {
      if (Math.abs(md) < SNAP_THRESHOLD) {
        cs.push({ delta: md, guidePos: md > 0 ? o.ox + o.ow : o.ox, label: md !== 0 ? `${Math.abs(Math.round(md))}px` : null });
      }
    }

    // Self-center ↔ other-left/other-right
    const centerCross = [
      (o.ox + 0) - (x + w / 2),      // self-center ↔ other-left
      (o.ox + o.ow) - (x + w / 2),   // self-center ↔ other-right
    ];
    for (const cd of centerCross) {
      if (Math.abs(cd) < SNAP_THRESHOLD) {
        cs.push({ delta: cd, guidePos: cd > 0 ? o.ox : o.ox + o.ow, label: cd !== 0 ? `${Math.abs(Math.round(cd))}px` : null });
      }
    }
  }

  return cs;
}

/**
 * Same as xCandidates but for the Y axis.
 */
function yCandidates(y, h, others) {
  /** @type {{ delta: number, guidePos: number, label: string | null }[]} */
  const cs = [];
  const selfEdges = [0, h / 2, h];

  for (const o of others) {
    const otherEdges = [0, o.oh / 2, o.oh];

    for (let si = 0; si < 3; si++) {
      const se = selfEdges[si];
      const oe = otherEdges[si];
      const delta = (o.oy + oe) - (y + se);
      if (Math.abs(delta) < SNAP_THRESHOLD) {
        cs.push({ delta, guidePos: o.oy + oe, label: delta !== 0 ? `${Math.abs(Math.round(delta))}px` : null });
      }
    }

    const mirrorDeltas = [
      (o.oy + o.oh) - (y + 0),
      (o.oy + 0) - (y + h),
    ];
    for (const md of mirrorDeltas) {
      if (Math.abs(md) < SNAP_THRESHOLD) {
        cs.push({ delta: md, guidePos: md > 0 ? o.oy + o.oh : o.oy, label: md !== 0 ? `${Math.abs(Math.round(md))}px` : null });
      }
    }

    const centerCross = [
      (o.oy + 0) - (y + h / 2),
      (o.oy + o.oh) - (y + h / 2),
    ];
    for (const cd of centerCross) {
      if (Math.abs(cd) < SNAP_THRESHOLD) {
        cs.push({ delta: cd, guidePos: cd > 0 ? o.oy : o.oy + o.oh, label: cd !== 0 ? `${Math.abs(Math.round(cd))}px` : null });
      }
    }
  }

  return cs;
}

/**
 * Snap rawX/rawY to canvas container boundaries.
 */
function snapToContainer(x, y, cW, cH, canvas, guides) {
  let sx = x;
  let sy = y;
  const canvasW = canvas.offsetWidth;
  const canvasH = canvas.offsetHeight;

  /** @type {{ pos: number, delta: number }[]} */
  const xSnaps = [
    { pos: 0, delta: -x },
    { pos: canvasW - cW, delta: canvasW - cW - x },
  ];

  let bestX = null;
  for (const c of xSnaps) {
    if (Math.abs(c.delta) < SNAP_THRESHOLD) {
      if (!bestX || Math.abs(c.delta) < Math.abs(bestX.delta)) {
        bestX = c;
      }
    }
  }
  if (bestX) {
    sx = bestX.pos;
    guides.push({ axis: 'v', pos: bestX.pos, label: null });
  }

  /** @type {{ pos: number, delta: number }[]} */
  const ySnaps = [
    { pos: 0, delta: -y },
    { pos: canvasH - cH, delta: canvasH - cH - y },
  ];

  let bestY = null;
  for (const c of ySnaps) {
    if (Math.abs(c.delta) < SNAP_THRESHOLD) {
      if (!bestY || Math.abs(c.delta) < Math.abs(bestY.delta)) {
        bestY = c;
      }
    }
  }
  if (bestY) {
    sy = bestY.pos;
    guides.push({ axis: 'h', pos: bestY.pos, label: null });
  }

  return { x: sx, y: sy };
}

/**
 * Snap a single moving edge against all other cards' 3 edge positions.
 * @param {number} edgePos
 * @param {(o: {ox:number,ow:number,oy:number,oh:number}) => number} getV0
 * @param {(o: {ox:number,ow:number,oy:number,oh:number}) => number} getVC
 * @param {(o: {ox:number,ow:number,oy:number,oh:number}) => number} getV1
 * @param {{ ox:number, ow:number, oy:number, oh:number }[]} others
 * @returns {{ delta: number | null, guidePos: number | null }}
 */
function snapEdge(edgePos, getV0, getVC, getV1, others) {
  let bestDelta = null;
  let bestGuide = null;
  for (const o of others) {
    const vs = [getV0(o), getVC(o), getV1(o)];
    for (const v of vs) {
      const d = v - edgePos;
      if (Math.abs(d) < SNAP_THRESHOLD) {
        if (bestDelta === null || Math.abs(d) < Math.abs(bestDelta)) {
          bestDelta = d;
          bestGuide = v;
        }
      }
    }
  }
  return { delta: bestDelta, guidePos: bestGuide };
}

/**
 * Snap card edges during resize. Each moving edge (determined by `dir`)
 * snaps independently to the nearest other-card edge, container edge, or grid.
 * The opposing edge stays fixed at its original position (`fixedRight` / `fixedBottom`).
 *
 * @param {HTMLElement} card
 * @param {number} left
 * @param {number} top
 * @param {number} width
 * @param {number} height
 * @param {string} dir - 'n','s','e','w','ne','nw','se','sw'
 * @param {number} fixedRight - original right (kept fixed)
 * @param {number} fixedBottom - original bottom (kept fixed)
 * @param {number} minW
 * @param {number} minH
 * @returns {{ left: number, top: number, width: number, height: number }}
 */
export function snapResize(card, left, top, width, height, dir, fixedRight, fixedBottom, minW, minH) {
  const canvas = docCanvas();
  if (!canvas) return { left, top, width, height };

  const others = Array.from(canvas.querySelectorAll(':scope > .canvas-card'))
    .filter(c => c !== card)
    .map(c => {
      const el = /** @type {HTMLElement} */ (c);
      return { ox: el.offsetLeft, oy: el.offsetTop, ow: el.offsetWidth, oh: el.offsetHeight };
    });

  let L = left; let T = top; let W = width; let H = height;
  /** @type {{ axis: 'v' | 'h', pos: number, label: string | null }[]} */
  const guides = [];

  const movesRight = dir.includes('e');
  const movesLeft = dir.includes('w');
  const movesBottom = dir.includes('s');
  const movesTop = dir.includes('n');

  // ── 1. Object-to-object snap (each moving edge independently) ─────
  if (movesRight && others.length > 0) {
    const r = snapEdge(L + W, (o) => o.ox, (o) => o.ox + o.ow / 2, (o) => o.ox + o.ow, others);
    if (r.delta !== null) {
      W += r.delta;
      guides.push({ axis: 'v', pos: /** @type {number} */ (r.guidePos), label: r.delta !== 0 ? `${Math.abs(Math.round(r.delta))}px` : null });
    }
  }
  if (movesLeft && others.length > 0) {
    const r = snapEdge(L, (o) => o.ox, (o) => o.ox + o.ow / 2, (o) => o.ox + o.ow, others);
    if (r.delta !== null) {
      L += r.delta;
      W = fixedRight - L;
      guides.push({ axis: 'v', pos: /** @type {number} */ (r.guidePos), label: r.delta !== 0 ? `${Math.abs(Math.round(r.delta))}px` : null });
    }
  }
  if (movesBottom && others.length > 0) {
    const r = snapEdge(T + H, (o) => o.oy, (o) => o.oy + o.oh / 2, (o) => o.oy + o.oh, others);
    if (r.delta !== null) {
      H += r.delta;
      guides.push({ axis: 'h', pos: /** @type {number} */ (r.guidePos), label: r.delta !== 0 ? `${Math.abs(Math.round(r.delta))}px` : null });
    }
  }
  if (movesTop && others.length > 0) {
    const r = snapEdge(T, (o) => o.oy, (o) => o.oy + o.oh / 2, (o) => o.oy + o.oh, others);
    if (r.delta !== null) {
      T += r.delta;
      H = fixedBottom - T;
      guides.push({ axis: 'h', pos: /** @type {number} */ (r.guidePos), label: r.delta !== 0 ? `${Math.abs(Math.round(r.delta))}px` : null });
    }
  }

  // ── 2. Container boundary snap ────────────────────────────────────
  const cW = canvas.offsetWidth;
  const cH = canvas.offsetHeight;
  if (movesLeft) {
    const d = 0 - L;
    if (Math.abs(d) < SNAP_THRESHOLD) { L = 0; W = fixedRight; guides.push({ axis: 'v', pos: 0, label: null }); }
  }
  if (movesRight) {
    const rightEdge = L + W;
    const d = cW - rightEdge;
    if (Math.abs(d) < SNAP_THRESHOLD) { W += d; guides.push({ axis: 'v', pos: cW, label: null }); }
  }
  if (movesTop) {
    const d = 0 - T;
    if (Math.abs(d) < SNAP_THRESHOLD) { T = 0; H = fixedBottom; guides.push({ axis: 'h', pos: 0, label: null }); }
  }
  if (movesBottom) {
    const bottomEdge = T + H;
    const d = cH - bottomEdge;
    if (Math.abs(d) < SNAP_THRESHOLD) { H += d; guides.push({ axis: 'h', pos: cH, label: null }); }
  }

  // ── 3. Grid snap (only applies to the moving edge) ────────────────
  if (_gridSnap) {
    if (movesLeft) {
      const snapped = roundToGrid(L);
      if (snapped !== L) { L = snapped; W = fixedRight - L; guides.push({ axis: 'v', pos: L, label: null }); }
    }
    if (movesRight) {
      const re = L + W;
      const snapped = roundToGrid(re);
      if (snapped !== re) { W += snapped - re; guides.push({ axis: 'v', pos: snapped, label: null }); }
    }
    if (movesTop) {
      const snapped = roundToGrid(T);
      if (snapped !== T) { T = snapped; H = fixedBottom - T; guides.push({ axis: 'h', pos: T, label: null }); }
    }
    if (movesBottom) {
      const be = T + H;
      const snapped = roundToGrid(be);
      if (snapped !== be) { H += snapped - be; guides.push({ axis: 'h', pos: snapped, label: null }); }
    }
  }

  // ── 4. Clamp to minimums ──────────────────────────────────────────
  W = Math.max(minW, W);
  H = Math.max(minH, H);

  // ── 5. Draw guides ────────────────────────────────────────────────
  const overlay = guidesOverlay();
  if (overlay) {
    overlay.innerHTML = '';
    const seen = new Set();
    for (const g of guides) {
      const key = g.axis + Math.round(g.pos);
      if (seen.has(key)) continue;
      seen.add(key);
      drawGuide(overlay, g.axis, g.pos, g.label, 'blue');
    }
  }

  return { left: L, top: T, width: W, height: H };
}

/**
 * Snap `rawX,rawY` to the nearest other card edge (within SNAP_THRESHOLD),
 * canvas container edges, and optional grid. Draws guide lines with spacing
 * labels for visual feedback.
 *
 * @param {HTMLElement} dragging  the card being dragged
 * @param {number} rawX
 * @param {number} rawY
 * @returns {{ x: number, y: number }}  snapped coords (clamped ≥ 0)
 */
export function snapWithGuides(dragging, rawX, rawY) {
  const canvas = docCanvas();
  if (!canvas) return { x: rawX, y: rawY };

  const others = Array.from(canvas.querySelectorAll(':scope > .canvas-card')).filter(
    (c) => c !== dragging
  ).map((c) => {
    const el = /** @type {HTMLElement} */ (c);
    return { el, ox: el.offsetLeft, oy: el.offsetTop, ow: el.offsetWidth, oh: el.offsetHeight };
  });

  const cW = dragging.offsetWidth;
  const cH = dragging.offsetHeight;
  let x = rawX;
  let y = rawY;

  /** @type {{ axis: 'v' | 'h', pos: number, label: string | null }[]} */
  const guides = [];

  // ── 1. Object-to-object snap (deterministic nearest) ──────────────
  if (others.length > 0) {
    const xcs = xCandidates(x, cW, others);
    const bestX = pickNearest(xcs.map((c) => c.delta));
    if (bestX.delta !== null) {
      x += bestX.delta;
      const match = xcs[bestX.index];
      guides.push({ axis: 'v', pos: match.guidePos, label: match.label });
    }

    const ycs = yCandidates(y, cH, others);
    const bestY = pickNearest(ycs.map((c) => c.delta));
    if (bestY.delta !== null) {
      y += bestY.delta;
      const match = ycs[bestY.index];
      guides.push({ axis: 'h', pos: match.guidePos, label: match.label });
    }
  }

  // ── 2. Container boundary snap ─────────────────────────────────────
  const cSnap = snapToContainer(x, y, cW, cH, canvas, guides);
  x = cSnap.x;
  y = cSnap.y;

  // ── 3. Grid snap (after object+container so grid refines, not fights) ──
  if (_gridSnap) {
    const gx = roundToGrid(x);
    const gy = roundToGrid(y);
    if (gx !== x) guides.push({ axis: 'v', pos: gx, label: null });
    if (gy !== y) guides.push({ axis: 'h', pos: gy, label: null });
    x = gx;
    y = gy;
  }

  // ── 4. Draw guides ─────────────────────────────────────────────────
  const overlay = guidesOverlay();
  if (overlay) {
    overlay.innerHTML = '';
    // De-duplicate by position (within 1px tolerance)
    const seen = new Set();
    for (const g of guides) {
      const key = g.axis + Math.round(g.pos);
      if (seen.has(key)) continue;
      seen.add(key);
      drawGuide(overlay, g.axis, g.pos, g.label, 'blue');
    }
  }

  return { x: Math.max(0, x), y: Math.max(0, y) };
}
