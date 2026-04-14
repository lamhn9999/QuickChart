import {
    listIndexes, appState,
    backHome, gearBtnClicked, toggleFieldTypeMenu, changeFieldType,
} from './workspace.js';
import { chartInstances, muuriGrid, autoChart, closeZoom, initSparklines } from './chart.js';
import {
    filterClicked, filterPopupClicked, filterInputKey,
    excludeCheckboxClicked, filterIntSpecificKey, filterIntRangeKey,
    applyIntRangeFilter, applyDtRangeFilter, excludeDtCheckboxClicked,
    applyBoolFilter, excludeBoolCheckboxClicked, excludeIntCheckboxClicked,
    partialMatchStrCheckboxClicked,
} from './filter.js';

// ── Field selection ───────────────────────────────────────────────────────────

function fieldClicked(fieldindex) {
    if (appState.controller) appState.controller.abort("New fetch!");
    if (listIndexes.has(fieldindex)) listIndexes.delete(fieldindex);
    else listIndexes.add(fieldindex);
    autoChart();
}

// ── Expose functions to window for inline HTML event handlers ─────────────────

Object.assign(window, {
    // Navigation & field management
    backHome, fieldClicked, closeZoom,
    gearBtnClicked, toggleFieldTypeMenu, changeFieldType,
    // Filter handlers
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
    const chartGrid = document.getElementById("chartGrid");

    // ── Responsive column switching via ResizeObserver ────────────────────
    // Muuri items are sized by the CSS --grid-cols variable.
    // Switch to 1 column when the grid container is narrower than 500px.
    if (chartGrid) {
        const colObserver = new ResizeObserver(entries => {
            for (const entry of entries) {
                const w = entry.contentRect.width;
                const cols = w < 500 ? 1 : 2;
                chartGrid.style.setProperty('--grid-cols', cols);
                // Reflow Muuri and resize Chart.js canvases after column change
                if (muuriGrid) {
                    muuriGrid.refreshItems().layout();
                    chartInstances.forEach(c => c.resize());
                }
            }
        });
        colObserver.observe(chartGrid);
    }

    if (table) {
        table.addEventListener("click", e => {
            const th = e.target.closest("th");
            if (!th) return;

            const colIndex = th.cellIndex;

            table.querySelectorAll("tr").forEach(row => {
                const cell = row.children[colIndex];
                if (cell) {
                    cell.classList.toggle("col-selected");
                }
            });
        });
    }

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

    if (resizer && left) {
        let startX = 0;
        let startWidth = 0;
        let isDragging = false;

        resizer.addEventListener('mousedown', e => {
            isDragging = true;
            startX = e.clientX;
            startWidth = left.getBoundingClientRect().width;
            document.body.style.cursor = 'col-resize';
            document.body.style.userSelect = 'none';
        });

        document.addEventListener('mousemove', e => {
            if (!isDragging) return;
            const dx = e.clientX - startX;
            left.style.width = `${startWidth + dx}px`;
            // Keep Muuri layout and Chart.js canvases in sync while resizing
            if (muuriGrid) {
                muuriGrid.refreshItems().layout(true); // true = instant, no animation
                chartInstances.forEach(c => c.resize());
            }
        });

        document.addEventListener('mouseup', () => {
            isDragging = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
        });
    }
});
