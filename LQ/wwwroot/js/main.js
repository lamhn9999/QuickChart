import {
    appState,
    pendingIndexes,
    backHome, gearBtnClicked, toggleFieldTypeMenu, changeFieldType,
} from './workspace.js';
import { chartInstances, closeZoom, initSparklines, createChartGroup, replaceChartGroup, clearAllChartGroups, exportCanvas, createTextCard, setCanvasSize, setCanvasBg } from './chart.js';
import {
    filterClicked, filterPopupClicked, filterInputKey,
    excludeCheckboxClicked, filterIntSpecificKey, filterIntRangeKey,
    applyIntRangeFilter, applyDtRangeFilter, excludeDtCheckboxClicked,
    applyBoolFilter, excludeBoolCheckboxClicked, excludeIntCheckboxClicked,
    partialMatchStrCheckboxClicked,
} from './filter.js';

// ── Header state helpers ──────────────────────────────────────────────────────

function _updateHeaderStates() {
    document.querySelectorAll('th[data-index]').forEach(th => {
        const idx = parseInt(th.dataset.index);
        th.classList.toggle('header-pending', pendingIndexes.has(idx));
    });
}

function _updatePendingBadge() {
    const badge = document.getElementById('pendingBadge');
    const txt   = document.getElementById('pendingBadgeText');
    if (!badge) return;
    const n = pendingIndexes.size;
    if (n > 0) {
        txt.textContent = `${n} selected`;
        badge.classList.remove('hidden');
    } else {
        badge.classList.add('hidden');
    }
}

// ── Field selection (click = toggle pending, no charting) ─────────────────────

let _dragStarted = false;  // prevents the click that follows a dragstart from toggling

function fieldClicked(fieldindex) {
    if (_dragStarted) return;
    if (pendingIndexes.has(fieldindex)) pendingIndexes.delete(fieldindex);
    else                                pendingIndexes.add(fieldindex);
    _updateHeaderStates();
    _updatePendingBadge();
}

// ── Drag-and-drop ─────────────────────────────────────────────────────────────

function headerDragStart(event, fieldindex) {
    _dragStarted = true;
    event.stopPropagation();

    // All indexes to send: pending selection + the actively dragged header
    const toDrop = [...new Set([...pendingIndexes, fieldindex])];
    event.dataTransfer.setData('application/json', JSON.stringify(toDrop));
    event.dataTransfer.effectAllowed = 'copy';

    // Custom ghost pill
    const ghost = document.createElement('div');
    ghost.style.cssText = 'position:fixed;top:-200px;left:0;background:#E9967A;color:#fff;padding:5px 14px 5px 10px;border-radius:100px;font-size:12px;font-weight:600;font-family:DM Sans,system-ui,sans-serif;display:flex;align-items:center;gap:8px;box-shadow:0 6px 20px rgba(233,150,122,0.5);white-space:nowrap;z-index:99999;pointer-events:none';
    const n = toDrop.length;
    if (n > 1) {
        ghost.innerHTML = `<span style="background:rgba(255,255,255,0.25);border-radius:50%;min-width:18px;height:18px;display:flex;align-items:center;justify-content:center;font-size:10px;font-weight:700">${n}</span>${n} columns`;
    } else {
        const name = event.currentTarget.querySelector('.field-name-span')?.textContent?.trim() || 'Column';
        ghost.innerHTML = `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/></svg>${name}`;
    }
    document.body.appendChild(ghost);
    event.dataTransfer.setDragImage(ghost, 16, 18);
    requestAnimationFrame(() => ghost.remove());

    // Clear pending selection highlights and column highlight once the drag payload is captured
    pendingIndexes.clear();
    _updateHeaderStates();
    _updatePendingBadge();
    document.querySelectorAll('.col-selected').forEach(el => el.classList.remove('col-selected'));

    event.currentTarget.classList.add('header-dragging');
}

function headerDragEnd(event) {
    event.currentTarget.classList.remove('header-dragging');
    // Reset after any click that follows dragend has already fired
    setTimeout(() => { _dragStarted = false; }, 0);
}


// Called when a header is dropped onto an existing chart card (wired in chart.js)
window._chartDropOnCard = async function(combinedIndexes, targetGroupId = null) {
    document.getElementById('plotPanel')?.classList.remove('drop-active');
    if (appState.controller) appState.controller.abort('card-drop');
    pendingIndexes.clear();
    _updateHeaderStates();
    _updatePendingBadge();
    if (targetGroupId !== null) {
        await replaceChartGroup(targetGroupId, combinedIndexes);
    } else {
        await createChartGroup(combinedIndexes);
    }
};

// ── Expose to window for inline HTML handlers ─────────────────────────────────

Object.assign(window, {
    backHome, fieldClicked, closeZoom, exportCanvas,
    addTextCard: () => createTextCard(), _tcCreate: createTextCard, setCanvasSize, setCanvasBg,
    headerDragStart, headerDragEnd,
    gearBtnClicked, toggleFieldTypeMenu, changeFieldType,
    filterClicked, filterPopupClicked, filterInputKey,
    excludeCheckboxClicked, filterIntSpecificKey, filterIntRangeKey,
    applyIntRangeFilter, applyDtRangeFilter, excludeDtCheckboxClicked,
    applyBoolFilter, excludeBoolCheckboxClicked, excludeIntCheckboxClicked,
    partialMatchStrCheckboxClicked,
});

// ── DOM setup ─────────────────────────────────────────────────────────────────

document.addEventListener("DOMContentLoaded", () => {
    initSparklines();

    const resizer   = document.getElementById('drag');
    const left      = document.getElementById('tablePanel');
    const table     = document.getElementById("dataTable");
    const plotPanel = document.getElementById('plotPanel');

    // ── Column cell highlight on header click ─────────────────────────────
    if (table) {
        table.addEventListener("click", e => {
            const th = e.target.closest("th");
            if (!th) return;
            const colIndex = th.cellIndex;
            table.querySelectorAll("tr").forEach(row => {
                const cell = row.children[colIndex];
                if (cell) cell.classList.toggle("col-selected");
            });
        });
    }

    // ── Drop zone: empty plot panel ───────────────────────────────────────
    if (plotPanel) {
        plotPanel.addEventListener('dragover', e => {
            if (!e.dataTransfer.types.includes('application/json')) return;
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
            plotPanel.classList.add('drop-active');
        });

        plotPanel.addEventListener('dragleave', e => {
            if (!plotPanel.contains(e.relatedTarget))
                plotPanel.classList.remove('drop-active');
        });

        plotPanel.addEventListener('drop', async e => {
            e.preventDefault();
            plotPanel.classList.remove('drop-active');
            try {
                const indexes = JSON.parse(e.dataTransfer.getData('application/json'));
                if (appState.controller) appState.controller.abort('panel-drop');
                pendingIndexes.clear();
                _updateHeaderStates();
                _updatePendingBadge();

                // Place card at cursor position within #doc-canvas
                const docCanvas = document.getElementById('doc-canvas');
                const stage     = document.getElementById('canvas-stage') ?? plotPanel;
                const rect = docCanvas?.getBoundingClientRect();
                const scrollTop  = stage.scrollTop;
                const scrollLeft = stage.scrollLeft;
                const dropPos = rect
                    ? { x: e.clientX - rect.left + scrollLeft - 20,
                        y: e.clientY - rect.top  + scrollTop  - 20 }
                    : null;

                await createChartGroup(indexes, dropPos);
            } catch {}
        });
    }

    // ── Close menus on click ──────────────────────────────────────────────
    document.addEventListener("click", () => {
        document.querySelectorAll('.plot-menu, .plot-colormap').forEach(m => m.classList.add('hidden'));
        document.querySelectorAll('.plot-btn svg, .plot-bgbtn svg').forEach(s => s.classList.remove('rotate-180'));
        document.querySelectorAll('.filterPopup').forEach(fm => fm.classList.add('hidden'));
        document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
        document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));
        document.querySelectorAll('.type-menu-popup').forEach(p => p.classList.add('hidden'));
        document.querySelectorAll('.type-menu-btn svg').forEach(s => s.classList.remove('rotate-180'));
    });

    if (left) {
        left.addEventListener('scroll', () => {
            document.querySelectorAll('.filterPopup').forEach(fm => fm.classList.add('hidden'));
            document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
            document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));
            document.querySelectorAll('.type-menu-popup').forEach(p => p.classList.add('hidden'));
            document.querySelectorAll('.type-menu-btn svg').forEach(s => s.classList.remove('rotate-180'));
        });
    }

    // ── Column resize drag ────────────────────────────────────────────────
    if (resizer && left) {
        let startX = 0, startWidth = 0, isDragging = false;

        resizer.addEventListener('mousedown', e => {
            isDragging = true;
            startX = e.clientX;
            startWidth = left.getBoundingClientRect().width;
            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
        });

        document.addEventListener('mousemove', e => {
            if (!isDragging) return;
            left.style.width = `${startWidth + (e.clientX - startX)}px`;
            chartInstances.forEach(c => c.resize());
        });

        document.addEventListener('mouseup', () => {
            isDragging = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
        });
    }
});
