// @ts-check
/**
 * @file Build the per-group toolbar (drag handle, bg-color picker, "Turn into" deck, zoom,
 * remove, copy-to-file) and wire the per-card drop zone for "drop a header onto an existing
 * card to replace its data".
 *
 * Called once per group from `build-panel.js` after the Chart.js instances are live.
 *
 * To avoid a circular import with `./api.js` (which calls back into `appendChart`), the
 * remove-button hands off to a callback registered by api.js via the module-private
 * `_removeGroupFn`. Set with `setRemoveGroupFn(fn)`. If unset, the remove button no-ops.
 */

import { reformatColor } from '../colors.js';
import { openZoom } from '../zoom/overlay.js';
import { CHART_META } from './types.js';
import { copyChartToFile } from './copy.js';

/** @type {((groupId: number) => void) | null} */
let _removeGroupFn = null;

/**
 * api.js calls this once at module init so builder.js can dispatch remove-clicks back to
 * the lifecycle module without forming an import cycle.
 * @param {(groupId: number) => void} fn
 */
export function setRemoveGroupFn(fn) {
  _removeGroupFn = fn;
}

/**
 * @param {any[]} panelSamples
 * @param {number} pIdx
 * @param {HTMLElement[]} wrappers
 * @param {HTMLElement} panelDivCopy
 * @param {HTMLElement} gridContainer
 * @param {number | null} [groupId]
 * @param {number[] | null} [fieldIndexes]
 * @param {string | null} [csvFileToken]
 */
export function appendChart(
  panelSamples,
  pIdx,
  wrappers,
  panelDivCopy,
  gridContainer,
  groupId = null,
  fieldIndexes = null,
  csvFileToken = null
) {
  // ── 1. Build ONE panel-level header (always visible, owns drag-handle) ─
  const header = document.createElement('div');
  // `opacity-0` is safelisted in Views/tailwind_classes_in_js.html so the
  // Tailwind CLI compiles it; we toggle it from JS to fade the handle in/out.
  header.className =
    'drag-handle flex flex-col items-center gap-2 px-1.5 py-3 border-r border-zinc-100 bg-white/90 backdrop-blur-sm z-50 select-none cursor-grab active:cursor-grabbing rounded-l-2xl opacity-0 transition-opacity flex-shrink-0';

  // Drag grip icon (left side — visual affordance)
  const grip = document.createElement('div');
  grip.className = 'text-zinc-300 hover:text-zinc-500 transition-colors pointer-events-none';
  grip.innerHTML = `
        <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 24 24">
            <circle cx="9"  cy="5"  r="1.5"/><circle cx="15" cy="5"  r="1.5"/>
            <circle cx="9"  cy="12" r="1.5"/><circle cx="15" cy="12" r="1.5"/>
            <circle cx="9"  cy="19" r="1.5"/><circle cx="15" cy="19" r="1.5"/>
        </svg>`;

  const container = document.createElement('div');
  container.className = 'flex flex-col items-center gap-2';

  // ── Background color button ───────────────────────────────────────────
  const bgbtnWrapper = document.createElement('div');
  bgbtnWrapper.className = 'relative flex flex-col items-end';

  const bgbtn = document.createElement('button');
  bgbtn.className =
    'cursor-pointer h-7 w-7 flex items-center justify-center rounded-md border border-zinc-200 bg-white text-zinc-500 hover:text-[#E9967A] hover:border-[#E9967A]/30 hover:bg-[#E9967A]/5 shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-all duration-150 focus:outline-none select-none';
  bgbtn.title = 'Background color';
  bgbtn.innerHTML = `<svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z"/></svg>`;

  const colormap = document.createElement('div');
  colormap.className =
    'hidden absolute top-full right-0 mt-1.5 min-w-[150px] p-3 bg-white border border-zinc-200 rounded-xl shadow-xl shadow-black/8 z-30 flex flex-col gap-2 backdrop-blur-sm';
  colormap.innerHTML = `
        <input type="color" id="hiddenPicker-${pIdx}" class="w-full h-8 cursor-pointer rounded-md border-0 border-none bg-transparent" value="#ffffff">
        <input id="hexInput-${pIdx}" class="w-full bg-zinc-50 text-zinc-600 px-3 py-1.5 rounded-lg outline-none text-xs text-center border border-zinc-200 focus:border-[#E9967A]/60 transition-colors" placeholder="#ffffff">`;

  colormap.onclick = (e) => e.stopPropagation();

  const hiddenPicker = /** @type {HTMLInputElement} */ (
    colormap.querySelector('input[type="color"]')
  );
  const hexInput = /** @type {HTMLInputElement} */ (colormap.querySelector(`#hexInput-${pIdx}`));

  // Always apply color to whichever wrapper is currently visible
  const getActiveCanvas = () => {
    const active = wrappers.find((w) => w.style.display !== 'none') ?? wrappers[0];
    return /** @type {HTMLCanvasElement} */ (active.querySelector('canvas'));
  };

  hiddenPicker.oninput = (e) => {
    const color = /** @type {HTMLInputElement} */ (e.target).value;
    hexInput.value = color;
    reformatColor(color, getActiveCanvas());
  };
  hexInput.oninput = (e) => {
    const color = /** @type {HTMLInputElement} */ (e.target).value;
    if (/^#[0-9A-F]{6}$/i.test(color)) {
      hiddenPicker.value = color;
      reformatColor(color, getActiveCanvas());
    }
  };

  colormap.classList.add('plot-colormap');
  bgbtn.classList.add('plot-bgbtn');

  bgbtn.onclick = (e) => {
    e.stopPropagation();
    const isHidden = colormap.classList.contains('hidden');
    document
      .querySelectorAll('.plot-menu, .plot-colormap')
      .forEach((m) => m.classList.add('hidden'));
    document
      .querySelectorAll('.plot-btn svg:last-of-type, .plot-bgbtn svg:last-of-type')
      .forEach((s) => s.classList.remove('rotate-180'));
    if (isHidden) {
      colormap.classList.remove('hidden');
    }
  };

  bgbtnWrapper.appendChild(bgbtn);
  bgbtnWrapper.appendChild(colormap);

  // ── "Turn into" — stacked chart deck button ──────────────────────────
  {
    // Signature color per ChartType enum index (0–11)
    const TYPE_COLORS = [
      '#E9967A',
      '#60A5FA',
      '#34D399',
      '#FBBF24',
      '#A78BFA',
      '#FB7185',
      '#2DD4BF',
      '#FB923C',
      '#818CF8',
      '#86EFAC',
      '#F472B6',
      '#22D3EE',
      '#FCD34D', // 12 = Radar (amber)
    ];

    // Three card indices shown in the deck: back → front
    const DECK = [10, 1, 0];
    const idleXforms = [
      'rotate(-9deg) translate(-6px,2px)',
      'rotate(-3deg) translate(-2px,1px)',
      'rotate(2deg) translate(0,0)',
    ];
    const openXforms = [
      'rotate(-15deg) translate(-11px,3px)',
      'rotate(-3deg) translate(-2px,0)',
      'rotate(10deg) translate(9px,-1px)',
    ];

    const turnWrapper = document.createElement('div');
    turnWrapper.style.cssText =
      'position:relative;display:flex;flex-direction:column;align-items:flex-end;flex-shrink:0';

    // ── Deck button ───────────────────────────────────────────────────
    const turnBtn = document.createElement('button');
    turnBtn.title = 'Turn into';
    turnBtn.style.cssText =
      'position:relative;width:32px;height:28px;padding:0;display:flex;align-items:center;justify-content:center;border-radius:6px;border:1px solid #e4e4e7;background:#fff;cursor:pointer;box-shadow:0 1px 2px rgba(0,0,0,.05);transition:border-color 150ms,color 150ms,box-shadow 150ms;flex-shrink:0;outline:none';

    const stackEl = document.createElement('div');
    stackEl.style.cssText = 'position:relative;width:24px;height:22px;flex-shrink:0';

    const stackCards = DECK.map((typeIdx, i) => {
      const card = document.createElement('div');
      card.style.cssText = `position:absolute;left:2px;top:1px;width:18px;height:18px;border-radius:5px;background:${TYPE_COLORS[typeIdx]};display:flex;align-items:center;justify-content:center;transform:${idleXforms[i]};z-index:${i + 1};box-shadow:0 1px 4px rgba(0,0,0,.22);transition:transform 220ms cubic-bezier(.34,1.56,.64,1);color:rgba(255,255,255,.9);flex-shrink:0`;
      card.innerHTML = `<span style="width:10px;height:10px;display:flex;align-items:center;justify-content:center;flex-shrink:0">${CHART_META[typeIdx].svg}</span>`;
      stackEl.appendChild(card);
      return card;
    });

    turnBtn.appendChild(stackEl);

    /** @param {boolean} open */
    const setDeckState = (open) => {
      stackCards.forEach((c, i) => {
        c.style.transform = open ? openXforms[i] : idleXforms[i];
      });
      turnBtn.style.borderColor = open ? 'rgba(233,150,122,.45)' : '#e4e4e7';
      turnBtn.style.color = open ? '#E9967A' : '#71717a';
      turnBtn.style.boxShadow = open
        ? '0 0 0 3px rgba(233,150,122,.12),0 1px 2px rgba(0,0,0,.05)'
        : '0 1px 2px rgba(0,0,0,.05)';
    };

    turnBtn.addEventListener('mouseenter', () => {
      if (turnPanel.classList.contains('hidden')) setDeckState(true);
    });
    turnBtn.addEventListener('mouseleave', () => {
      if (turnPanel.classList.contains('hidden')) setDeckState(false);
    });
    document.addEventListener('click', () => {
      setDeckState(false);
    });

    // ── Panel ─────────────────────────────────────────────────────────
    // Lives on the .canvas-card (which has overflow:visible) rather than
    // inside .canvas-card-panels (overflow:hidden) so the popup isn't
    // clipped by the toolbar's parent. Positioned per-open against the
    // turnBtn's bounding rect so the panel anchors to the button's right edge.
    const turnPanel = document.createElement('div');
    turnPanel.className = 'hidden plot-menu';
    turnPanel.style.cssText =
      'position:absolute;background:#fff;border:1px solid #e4e4e7;border-radius:14px;box-shadow:0 12px 36px rgba(0,0,0,.11),0 2px 8px rgba(0,0,0,.06);padding:10px;z-index:30';
    turnPanel.onclick = (e) => e.stopPropagation();

    const rebuildGrid = () => {
      const activeIdx = wrappers.findIndex((w) => w.style.display !== 'none');
      const effectiveIdx = activeIdx === -1 ? 0 : activeIdx;
      const activeType = panelSamples[effectiveIdx]?.chartType;
      turnPanel.innerHTML = '';

      const grid = document.createElement('div');
      grid.style.cssText = 'display:grid;grid-template-columns:repeat(4,1fr);gap:5px';

      CHART_META.forEach((meta, typeIdx) => {
        const matchIdx = panelSamples.findIndex((s) => s.chartType === typeIdx);
        const available = matchIdx !== -1;
        const isActive = typeIdx === activeType;
        const col = TYPE_COLORS[typeIdx];

        const cell = document.createElement('button');
        cell.disabled = !available;
        cell.title = available ? meta.label : `${meta.label} — not available`;
        cell.style.cssText = `display:flex;flex-direction:column;align-items:center;justify-content:center;gap:4px;width:52px;height:56px;border-radius:10px;border:1.5px solid ${isActive ? col : 'transparent'};background:${isActive ? col + '1a' : 'transparent'};cursor:${available ? 'pointer' : 'not-allowed'};opacity:${available ? 1 : 0.25};transition:background 120ms;padding:0;outline:none;flex-shrink:0`;

        const chip = document.createElement('div');
        chip.style.cssText = `width:28px;height:28px;border-radius:7px;background:${col}${isActive ? '38' : '22'};display:flex;align-items:center;justify-content:center;color:${col};transition:background 120ms;flex-shrink:0`;
        chip.innerHTML = `<span style="width:15px;height:15px;display:flex;align-items:center;justify-content:center">${meta.svg}</span>`;

        const lbl = document.createElement('span');
        lbl.textContent = meta.label;
        lbl.style.cssText = `font-size:7.5px;line-height:1;font-weight:${isActive ? 700 : 500};color:${isActive ? col : '#71717a'};letter-spacing:.025em;text-align:center`;

        cell.appendChild(chip);
        cell.appendChild(lbl);

        if (available) {
          if (!isActive) {
            cell.addEventListener('mouseenter', () => {
              cell.style.background = col + '0f';
              chip.style.background = col + '38';
            });
            cell.addEventListener('mouseleave', () => {
              cell.style.background = 'transparent';
              chip.style.background = col + '22';
            });
          }
          cell.addEventListener('click', () => {
            const fromLabel = CHART_META[activeType]?.label ?? `type ${activeType}`;
            const toLabel = CHART_META[typeIdx]?.label ?? `type ${typeIdx}`;
            const fromSample = panelSamples[effectiveIdx];
            const toSample = panelSamples[matchIdx];
            const fromWrapper = wrappers[effectiveIdx];
            const toWrapper = wrappers[matchIdx];
            // @ts-ignore — Chart is a runtime global
            const fromInstance = Chart.getChart(fromWrapper?.querySelector('canvas'));
            // @ts-ignore — Chart is a runtime global
            const toInstance = Chart.getChart(toWrapper?.querySelector('canvas'));
            console.groupCollapsed(
              `[Turn into] ${fromLabel} (type ${activeType}) → ${toLabel} (type ${typeIdx})`
            );
            console.log('from:', {
              chartType: activeType,
              label: fromLabel,
              wrapperIdx: effectiveIdx,
              title: fromSample?.title,
              x: fromSample?.x,
              y: fromSample?.y,
              z: fromSample?.z,
              sample: fromSample,
              wrapper: fromWrapper,
              chart: fromInstance,
            });
            console.log('to:', {
              chartType: typeIdx,
              label: toLabel,
              wrapperIdx: matchIdx,
              title: toSample?.title,
              x: toSample?.x,
              y: toSample?.y,
              z: toSample?.z,
              sample: toSample,
              wrapper: toWrapper,
              chart: toInstance,
            });
            console.log(
              'groupId:',
              groupId,
              'fieldIndexes:',
              fieldIndexes,
              'panelSamples:',
              panelSamples
            );
            console.groupEnd();
            wrappers.forEach((w, i) => {
              w.style.display = i === matchIdx ? 'flex' : 'none';
            });
            // @ts-ignore — Chart is a runtime global
            const c = Chart.getChart(wrappers[matchIdx].querySelector('canvas'));
            if (c) requestAnimationFrame(() => c.resize());
            turnPanel.classList.add('hidden');
            setDeckState(false);
          });
        }
        grid.appendChild(cell);
      });
      turnPanel.appendChild(grid);
    };

    turnBtn.classList.add('plot-btn');
    turnBtn.onclick = (e) => {
      e.stopPropagation();
      const isHidden = turnPanel.classList.contains('hidden');
      document
        .querySelectorAll('.plot-menu, .plot-colormap')
        .forEach((m) => m.classList.add('hidden'));
      document
        .querySelectorAll('.plot-btn svg:last-of-type, .plot-bgbtn svg:last-of-type')
        .forEach((s) => s.classList.remove('rotate-180'));
      if (isHidden) {
        rebuildGrid();
        turnPanel.classList.remove('hidden');
        const card = turnBtn.closest('.canvas-card');
        if (card) {
          const cardR = card.getBoundingClientRect();
          const btnR = turnBtn.getBoundingClientRect();
          turnPanel.style.left = `${btnR.right - cardR.left + 6}px`;
          turnPanel.style.top = `${btnR.top - cardR.top}px`;
        }
        setDeckState(true);
      } else {
        setDeckState(false);
      }
    };

    turnWrapper.appendChild(turnBtn);
    container.appendChild(turnWrapper);
    // Attach the popup to the card itself so it escapes
    // .canvas-card-panels' overflow:hidden boundary.
    const cardForMenu = gridContainer.closest('.canvas-card') || gridContainer.parentNode;
    /** @type {ParentNode} */ (cardForMenu || turnWrapper).appendChild(turnPanel);
  }

  // ── Zoom button ───────────────────────────────────────────────────────
  const zoomBtn = document.createElement('button');
  zoomBtn.className =
    'cursor-pointer h-7 w-7 flex items-center justify-center rounded-md border border-zinc-200 bg-white text-zinc-500 hover:text-[#E9967A] hover:border-[#E9967A]/30 hover:bg-[#E9967A]/5 shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-all duration-150 focus:outline-none select-none';
  zoomBtn.title = 'Fullscreen';
  zoomBtn.innerHTML = `
    <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24">
        <path d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4"/>
    </svg>`;
  zoomBtn.onclick = (e) => {
    e.stopPropagation();
    const activeWrapper = wrappers.find((w) => w.style.display !== 'none') ?? wrappers[0];
    openZoom(activeWrapper);
  };

  // ── Remove group button (only for drag-created groups) ────────────────
  if (groupId !== null) {
    const removeBtn = document.createElement('button');
    removeBtn.className =
      'cursor-pointer h-7 w-7 flex items-center justify-center rounded-md border border-zinc-200 bg-white text-zinc-400 hover:text-red-400 hover:border-red-200 hover:bg-red-50/50 shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-all duration-150 focus:outline-none select-none';
    removeBtn.title = 'Remove chart';
    removeBtn.innerHTML = `<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/></svg>`;
    removeBtn.onclick = (e) => {
      e.stopPropagation();
      _removeGroupFn?.(groupId);
    };
    container.appendChild(removeBtn);

    // ── Use as filter button ──────────────────────────────────────────
    // Convert this chart card into a filter card scoped to its single
    // field. Only meaningful for single-field charts; disabled otherwise
    // (a tooltip explains why). The actual conversion is delegated via
    // data-action so per-chart-filter/events.js owns the import side.
    const isSingleField = Array.isArray(fieldIndexes) && fieldIndexes.length === 1;
    const filterBtn = document.createElement('button');
    filterBtn.className =
      'cursor-pointer h-7 w-7 flex items-center justify-center rounded-md border border-zinc-200 bg-white text-zinc-500 hover:text-[#2383E2] hover:border-[#2383E2]/30 hover:bg-[#2383E2]/5 shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-all duration-150 focus:outline-none select-none disabled:opacity-30 disabled:cursor-not-allowed disabled:hover:text-zinc-500 disabled:hover:border-zinc-200 disabled:hover:bg-white';
    filterBtn.title = isSingleField
      ? 'Use as filter'
      : 'Use as filter — drag a single column to enable';
    filterBtn.disabled = !isSingleField;
    filterBtn.dataset.action = 'filter:use-as-filter';
    filterBtn.innerHTML = `<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><path d="M3 4h18l-7 9v6l-4 2v-8z"/></svg>`;
    container.appendChild(filterBtn);

    // ── Copy to file… button ──────────────────────────────────────────
    // Lets the user copy this chart's spec into another CSVFile's canvas.
    // The picker fetches accessible files lazily and posts to
    // /workspace/canvas/append-chart with the source token baked in.
    const copyWrapper = document.createElement('div');
    copyWrapper.className = 'relative';

    const copyBtn = document.createElement('button');
    copyBtn.className =
      'plot-btn cursor-pointer h-7 w-7 flex items-center justify-center rounded-md border border-zinc-200 bg-white text-zinc-400 hover:text-[#E9967A] hover:border-[#E9967A]/30 hover:bg-[#E9967A]/5 shadow-[0_1px_2px_rgba(0,0,0,0.05)] transition-all duration-150 focus:outline-none select-none';
    copyBtn.title = 'Copy chart to another file';
    copyBtn.innerHTML = `<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"><rect x="9" y="9" width="11" height="11" rx="2"/><path d="M5 15V5a2 2 0 0 1 2-2h10"/></svg>`;

    const copyPanel = document.createElement('div');
    copyPanel.className =
      'plot-menu hidden absolute top-full right-0 mt-1.5 min-w-[220px] max-h-[260px] overflow-auto p-2 bg-white border border-zinc-200 rounded-xl shadow-xl shadow-black/8 z-30 flex flex-col gap-1';
    copyPanel.onclick = (e) => e.stopPropagation();

    copyBtn.onclick = async (e) => {
      e.stopPropagation();
      const isHidden = copyPanel.classList.contains('hidden');
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
        heading.textContent = 'Copy to file…';
        copyPanel.appendChild(heading);

        files.forEach((/** @type {any} */ f) => {
          const row = document.createElement('button');
          row.className =
            'text-left text-xs px-2 py-1.5 rounded-md hover:bg-[#E9967A]/10 hover:text-[#E9967A] text-zinc-700 truncate';
          row.textContent = f.name;
          row.onclick = async () => {
            copyPanel.classList.add('hidden');
            await copyChartToFile(panelDivCopy, csvFileToken, f.token);
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
  }

  container.appendChild(bgbtnWrapper);
  container.appendChild(zoomBtn);
  header.appendChild(grip);
  header.appendChild(container);

  // Append the header to the .canvas-card (parent of .canvas-card-panels)
  // rather than into the panels row itself. The panels row has
  // overflow:hidden which would clip a handle floated outside its left
  // edge; the .canvas-card has overflow:visible so the handle can sit
  // beside the chart without being clipped.
  // Absolutely position the handle so its right edge meets the card's
  // left edge — the handle appears OUTSIDE the card on the left and
  // never affects the chart's size or position.
  header.style.position = 'absolute';
  header.style.right = '100%';
  header.style.top = '0';
  header.style.bottom = '0';
  gridContainer.parentElement?.appendChild(header);

  // ── Hover-toggle: fade drag-handle in on chart hover OR handle hover ──
  // opacity:0 (rather than display:none) keeps the handle's bounding box
  // alive in its absolute slot beside the card, so mouseenter still fires
  // when the cursor enters that (invisible) region — the user can reveal
  // the handle by hovering either the chart or the handle's own zone.
  // The relatedTarget check on mouseleave keeps the handle visible while
  // the cursor moves between the chart and the handle.
  const showHandle = () => header.classList.remove('opacity-0');
  /** @param {MouseEvent} e */
  const hideHandle = (e) => {
    const goingTo = /** @type {Node | null} */ (e.relatedTarget);
    if (goingTo && (header.contains(goingTo) || gridContainer.contains(goingTo))) return;
    header.classList.add('opacity-0');
  };
  gridContainer.addEventListener('mouseenter', showHandle);
  gridContainer.addEventListener('mouseleave', hideHandle);
  header.addEventListener('mouseenter', showHandle);
  header.addEventListener('mouseleave', hideHandle);

  // ── Store group metadata on DOM for drop-onto-card ────────────────────
  if (groupId !== null) {
    panelDivCopy.dataset.groupId = String(groupId);
    panelDivCopy.dataset.fieldIndexes = JSON.stringify(fieldIndexes ? [...fieldIndexes] : []);
    // Foreign-file charts carry their source token; native charts (drawn
    // from the active workspace) leave the attribute unset so the
    // serializer treats them as implicit/native.
    if (csvFileToken) panelDivCopy.dataset.csvFileToken = csvFileToken;
    else delete panelDivCopy.dataset.csvFileToken;
  }

  // ── Drop zone: another column header dragged onto this panel ─────────
  if (groupId !== null) {
    panelDivCopy.addEventListener('dragover', (e) => {
      if (!e.dataTransfer?.types.includes('application/json')) return;
      e.preventDefault();
      e.dataTransfer.dropEffect = 'copy';
      panelDivCopy.classList.add('chart-drop-target');
    });
    panelDivCopy.addEventListener('dragleave', (e) => {
      const r = /** @type {Node | null} */ (e.relatedTarget);
      if (!r || !panelDivCopy.contains(r)) panelDivCopy.classList.remove('chart-drop-target');
    });
    panelDivCopy.addEventListener('drop', (e) => {
      e.preventDefault();
      e.stopPropagation();
      panelDivCopy.classList.remove('chart-drop-target');
      try {
        const dragged = JSON.parse(e.dataTransfer?.getData('application/json') ?? '[]');
        const existing = JSON.parse(panelDivCopy.dataset.fieldIndexes || '[]');
        const combined = [...new Set([...existing, ...dragged])];
        const fn = /** @type {any} */ (window)._chartDropOnCard;
        if (typeof fn === 'function') fn(combined, groupId);
      } catch {
        /* malformed dataTransfer */
      }
    });
  }

  gridContainer.appendChild(panelDivCopy);
  return panelDivCopy;
}
