// @ts-check
/**
 * @file External left-side floating toolbar for a filter card.
 *
 * Pares the chart-card toolbar in chart/group/builder.js down to the only
 * three affordances that make sense for a filter card:
 *   1. Drag grip          — the only `.drag-handle` on the card; makeDraggable
 *                           wires it (no other surface initiates a drag).
 *   2. Remove              — tears down the filter via removeFilterCard, which
 *                           also re-runs replayFiltersFor on the affected
 *                           charts so they revert to whatever the remaining
 *                           filters say.
 *   3. Copy to file…       — same picker UI as the chart-card flavour. POSTs
 *                           through copy.js to /workspace/canvas/append-filter.
 *
 * Layout: absolutely positioned at `right:100%; top:0; bottom:0` so the
 * toolbar floats OUTSIDE the card's left edge (filter-card-host has
 * overflow:visible so it isn't clipped). Faded out at rest; opacity:1 when
 * the cursor is on the card or the toolbar.
 *
 * Wiring: direct onclicks (not data-action delegation) so the toolbar
 * matches the chart-card builder's pattern, and so the file-picker
 * dropdown — whose contents are dynamic per-open — doesn't need extra
 * action plumbing.
 */

import { removeFilterCard } from './convert.js';
import { copyFilterToFile } from './copy.js';

const GRIP_SVG = `<svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
  <circle cx="9" cy="5" r="1.5"/><circle cx="15" cy="5" r="1.5"/>
  <circle cx="9" cy="12" r="1.5"/><circle cx="15" cy="12" r="1.5"/>
  <circle cx="9" cy="19" r="1.5"/><circle cx="15" cy="19" r="1.5"/>
</svg>`;

const CLOSE_SVG = `<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor"
  stroke-width="2" viewBox="0 0 24 24"><path stroke-linecap="round"
  stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/></svg>`;

const COPY_SVG = `<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor"
  stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"
  viewBox="0 0 24 24"><rect x="9" y="9" width="11" height="11" rx="2"/>
  <path d="M5 15V5a2 2 0 0 1 2-2h10"/></svg>`;

/**
 * Build the external toolbar and attach it to `card`. Idempotent — if a
 * toolbar already exists on the card it's removed and rebuilt (covers the
 * convertChartToFilter → re-convert path).
 *
 * @param {HTMLElement} card     the .canvas-card.filter-card-host
 * @param {number}      groupId  the filter's groupId (also card.dataset.groupId)
 */
export function buildFilterToolbar(card, groupId) {
  card.querySelectorAll(':scope > .filter-toolbar').forEach((el) => el.remove());

  // ── Outer toolbar (also the .drag-handle) ──────────────────────────
  // `rounded-l-xl` (12px) matches .filter-card-host's border-radius so the
  // toolbar's left edge curves with the card's left edge.
  const header = document.createElement('div');
  header.className =
    'filter-toolbar drag-handle flex flex-col items-center gap-2 px-1.5 py-3 ' +
    'border-r border-zinc-100 bg-white/90 backdrop-blur-sm z-50 select-none ' +
    'cursor-grab active:cursor-grabbing rounded-l-xl opacity-0 transition-opacity flex-shrink-0';
  header.style.cssText = 'position:absolute;right:100%;top:0;bottom:0;';

  const grip = document.createElement('div');
  grip.className = 'text-zinc-300 hover:text-zinc-500 transition-colors pointer-events-none';
  grip.innerHTML = GRIP_SVG;

  const container = document.createElement('div');
  container.className = 'flex flex-col items-center gap-2';

  // ── Remove ──────────────────────────────────────────────────────────
  const removeBtn = document.createElement('button');
  removeBtn.className =
    'cursor-pointer h-7 w-7 flex items-center justify-center rounded-md ' +
    'border border-zinc-200 bg-white text-zinc-400 ' +
    'hover:text-red-400 hover:border-red-200 hover:bg-red-50/50 ' +
    'shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-all duration-150 ' +
    'focus:outline-none select-none';
  removeBtn.title = 'Remove filter';
  removeBtn.innerHTML = CLOSE_SVG;
  removeBtn.onclick = (e) => {
    e.stopPropagation();
    void removeFilterCard(groupId);
  };
  container.appendChild(removeBtn);

  // ── Copy to file… (dropdown) ────────────────────────────────────────
  // Wrapper holds the button + the popup, anchored to the wrapper so
  // top-full / right-0 positioning works without coordinate math.
  const copyWrapper = document.createElement('div');
  copyWrapper.className = 'relative';

  const copyBtn = document.createElement('button');
  copyBtn.className =
    'plot-btn cursor-pointer h-7 w-7 flex items-center justify-center rounded-md ' +
    'border border-zinc-200 bg-white text-zinc-400 ' +
    'hover:text-[#2383E2] hover:border-[#2383E2]/30 hover:bg-[#2383E2]/5 ' +
    'shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-all duration-150 ' +
    'focus:outline-none select-none';
  copyBtn.title = 'Copy filter to another file';
  copyBtn.innerHTML = COPY_SVG;

  const copyPanel = document.createElement('div');
  copyPanel.className =
    'plot-menu hidden absolute top-full right-0 mt-1.5 min-w-[220px] max-h-[260px] ' +
    'overflow-auto p-2 bg-white border border-zinc-200 rounded-xl shadow-xl ' +
    'shadow-black/8 z-30 flex flex-col gap-1';
  copyPanel.onclick = (e) => e.stopPropagation();

  copyBtn.onclick = async (e) => {
    e.stopPropagation();
    const isHidden = copyPanel.classList.contains('hidden');
    // Close any other open .plot-menu / .plot-colormap so only one popup is
    // alive at a time — same convention chart-card menus follow.
    document
      .querySelectorAll('.plot-menu, .plot-colormap')
      .forEach((m) => m.classList.add('hidden'));
    if (!isHidden) return;

    copyPanel.classList.remove('hidden');
    copyPanel.innerHTML = '<div class="text-xs text-zinc-400 px-2 py-1">Loading…</div>';
    try {
      const r = await fetch('/workspace/files-for-copy');
      if (!r.ok) throw new Error(String(r.status));
      const files = await r.json();
      copyPanel.innerHTML = '';
      if (!Array.isArray(files) || files.length === 0) {
        copyPanel.innerHTML =
          '<div class="text-xs text-zinc-400 px-2 py-1">No other files to copy to.</div>';
        return;
      }
      const heading = document.createElement('div');
      heading.className = 'text-[11px] uppercase tracking-wide text-zinc-400 px-2 py-1';
      heading.textContent = 'Copy filter to file…';
      copyPanel.appendChild(heading);

      files.forEach((/** @type {any} */ f) => {
        const row = document.createElement('button');
        row.className =
          'text-left text-xs px-2 py-1.5 rounded-md hover:bg-[#2383E2]/10 ' +
          'hover:text-[#2383E2] text-zinc-700 truncate';
        row.textContent = f.name;
        row.onclick = async () => {
          copyPanel.classList.add('hidden');
          await copyFilterToFile(card, f.token);
        };
        copyPanel.appendChild(row);
      });
    } catch {
      copyPanel.innerHTML =
        '<div class="text-xs text-red-400 px-2 py-1">Failed to load files.</div>';
    }
  };

  copyWrapper.appendChild(copyBtn);
  copyWrapper.appendChild(copyPanel);
  container.appendChild(copyWrapper);

  header.appendChild(grip);
  header.appendChild(container);
  card.appendChild(header);

  // Hover-toggle. mouseleave doesn't fire on parent → descendant transitions,
  // so a single pair of listeners on the card covers both the body and the
  // toolbar (the toolbar is a card descendant, just visually outside).
  const show = () => header.classList.remove('opacity-0');
  const hide = () => header.classList.add('opacity-0');
  card.addEventListener('mouseenter', show);
  card.addEventListener('mouseleave', hide);
}
