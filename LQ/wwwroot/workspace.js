let chartInstances = [];
const listIndexes = new Set();
const chartArray = ['pie', 'bar', 'line', 'bar', 'scatter', 'polarArea', 'bar', 'mixed', 'matrix', 'mixedBar'];
let controller;
let muuriGrid = null;
let filters = [];
// Zoom state
let _zoomedWrapper  = null;
let _zoomedParent   = null;
let _zoomedSibling  = null;
let _zoomedCssText  = '';

const axisStyle = {
    ticks: { color: "white" },
    grid: { color: "lightgray" }
};

const stackedAxis = {
    ...axisStyle,
    stacked: true
};

function backHome() {
    window.location.href = '/';
}

function buildHeatmapData(xLabels, ySeries) {
    return ySeries.flatMap(row =>
        row.data.map((value, colIndex) => ({
            x: xLabels[colIndex],
            y: row.label,
            v: value ? value : 0,
        }))
    );
}

function buildMatrixDataset(sample) {
    const heatmapData = buildHeatmapData(sample.x, sample.y);

    const values = heatmapData.map(d => d.v);
    const minVal = 0;
    const maxVal = Math.max(...values);

    const xCount = new Set(heatmapData.map(d => d.x)).size;
    const yCount = new Set(heatmapData.map(d => d.y)).size;

    return [{
        type: 'matrix',
        label: sample.title ?? 'Heatmap',
        data: heatmapData,
        borderColor: '#fff',
        borderWidth: 1,

        backgroundColor(ctx) {
            const v = ctx.raw.v;
            const t = (v - minVal) / (maxVal - minVal || 1);
            return `rgba(0,123,255,${t})`;
        },

        width({ chart }) {
            const a = chart.chartArea;
            return a ? (a.right - a.left) / xCount - 2 : 0;
        },

        height({ chart }) {
            const a = chart.chartArea;
            return a ? (a.bottom - a.top) / yCount - 2 : 0;
        }
    }];
}

function positionDropdown(popup, anchorEl) {
    const MARGIN = 8; // min gap from viewport edges (px)
    const GAP    = 4; // gap between anchor bottom and popup top (px)

    popup.style.visibility = 'hidden';
    popup.style.position   = 'fixed';
    popup.style.top        = '0px';
    popup.style.left       = '0px';
    popup.style.transform  = 'none'; // neutralise any Tailwind translate classes
    popup.classList.remove('hidden');

    const pw = popup.offsetWidth;
    const ph = popup.offsetHeight;

    popup.classList.add('hidden');
    popup.style.visibility = '';

    const rect = anchorEl.getBoundingClientRect();
    const vw   = window.innerWidth;
    const vh   = window.innerHeight;

    let top  = rect.bottom + GAP;
    let left = rect.left;

    if (left < MARGIN)            left = MARGIN;
    if (left + pw > vw - MARGIN)  left = vw - pw - MARGIN;

    // Flip above anchor if not enough space below
    if (top + ph > vh - MARGIN) {
        top = rect.top - ph - GAP;
    }
    // Last resort: pin to top margin
    if (top < MARGIN) top = MARGIN;

    popup.style.top    = `${top}px`;
    popup.style.left   = `${left}px`;
    popup.style.zIndex = '9999';
}

function filterClicked(event, filterbtn) {
    event.stopPropagation();

    const popup   = filterbtn.parentElement.querySelector(".filterPopup");
    const wasOpen = !popup.classList.contains("hidden");

    // Close all open dropdowns first
    document.querySelectorAll('.filterPopup').forEach(fm => fm.classList.add('hidden'));
    document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
    document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));

    if (!wasOpen) {
        // Position using fixed coords before revealing — escapes overflow:auto clipping
        positionDropdown(popup, filterbtn);
        popup.classList.remove('hidden');
        console.log(`[filterClicked] Opened filter popup for field index=${filterbtn.closest('th')?.dataset?.index}`);
    }

    // Not all filter variants have a text input (e.g. Bool only has buttons)
    popup.querySelector("input")?.focus();
}

function filterPopupClicked(event, filterpopup) {
    event.stopPropagation();
    filterpopup.querySelector("input")?.focus();
}

async function filterInputKey(event, input) {
    if (event.key === "Enter") {
        event.preventDefault();

        const value = input.value;
        const columnHeader = input.closest("th");
        const index = columnHeader.dataset.index;
        const popup = input.closest(".filterPopup");
        const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]')?.checked || false;

        await sendFilterRequest(value.trim(), isNotCheckboxValue, index, popup);
        appendFilterValue(input, index);
    }
}

function appendFilterValue(input, index) {
    const container = input.previousElementSibling; // chip container above input
    const value = input.value.trim();
    if (!value) return;

    if (Array.from(container.children).some(c => c.textContent.replace('✕', '').trim() === value)) {
        input.value = "";
        return;
    }

    const popup = input.closest(".filterPopup");

    const chip = document.createElement("div");
    chip.className =
        "filterValue rounded-xl bg-[darksalmon] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";

    const text = document.createElement("span");
    text.textContent = value;
    text.className = "truncate min-w-0";

    const removeBtn = document.createElement("button");
    removeBtn.textContent = "✕";
    removeBtn.className = "ml-1 font-bold hover:cursor-pointer";

    removeBtn.onclick = async (e) => {
        e.stopPropagation();

        if (filters[index] && filters[index].filteringValues) {
            filters[index].filteringValues =
                filters[index].filteringValues.filter(v => v !== value);

            if (filters[index].filteringValues.length === 0) {
                filters[index] = undefined;
            }
        }

        chip.remove();
        const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]')?.checked || false;
        await sendFilterRequest("", isNotCheckboxValue, index, popup);
    };

    chip.onclick = () => {
        input.value = value;
        input.focus();
    };

    chip.appendChild(text);
    chip.appendChild(removeBtn);
    container.appendChild(chip);

    input.value = "";
}

function updateFilterChipUI(index) {
    const th = document.querySelector(`th[data-index="${index}"]`);
    if (!th) return;
    const filter = filters[index];
    const chip = th.querySelector('.filter-chip');
    if (!chip) return;

    let len = 0;
    if (filter) {
        if (filter.filteringValues)       len += filter.filteringValues.length;
        if (filter.filteringValuesInt)    len += filter.filteringValuesInt.length;
        if (filter.filteringValuesDouble) len += filter.filteringValuesDouble.length;
        if (filter.intBound)              len++;
        if (filter.doubleBound)           len++;
        if (filter.dateTimeBound)         len++;
    }

    if (len > 0) {
        chip.textContent = len;
        chip.classList.remove('hidden');
    } else {
        chip.classList.add('hidden');
    }
}

async function excludeCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    const index = columnHeader.dataset.index;
    const isNotCheckboxValue = checkbox.checked;
    const popup = checkbox.closest('.filterPopup');

    await sendFilterRequest("", isNotCheckboxValue, index, popup);
}

const enumMap = {
    "String": 0, "Int": 1, "Double": 2, "DateTime": 3, "Bool": 4, "ListString": 5
};

async function sendFilterRequest(value, isNotCheckboxValue, index, popup) {
    const fieldType = enumMap[popup.dataset.fieldType] || 0;
    const isPartialMatch = popup.querySelector('input[name="IsPartialMatch"]')?.checked || false;

    if (value !== "") {
        if (!filters[index]) {
            filters[index] = {
                isPartialMatch: isPartialMatch,
                fieldIndex: parseInt(index),
                fieldType: fieldType,
                filteringValues: []
            };
        } else {
            filters[index].fieldType = fieldType;
            filters[index].isPartialMatch = isPartialMatch;
        }

        if (!filters[index].filteringValues) filters[index].filteringValues = [];
        if (!filters[index].filteringValues.includes(value)) {
            filters[index].filteringValues.push(value);
        }
        filters[index].isNot = isNotCheckboxValue;
    }
    else if (filters[index]) {
        filters[index].isNot = isNotCheckboxValue;
        filters[index].isPartialMatch = isPartialMatch;
        filters[index].fieldType = fieldType;
        if (!filters[index].filteringValues || filters[index].filteringValues.length === 0) {
            filters[index] = undefined;
        }
    }

    updateFilterChipUI(index);
    if (!popup.classList.contains("executing")) await executeFilterRequest();
}

function updateTable(rows) {
    const tbody = document.querySelector("#dataTable tbody");
    let trs = tbody.querySelectorAll("tr");

    const dataRows = rows.slice(1); // skip header row if server includes it

    dataRows.forEach((row, i) => {
        let tr;

        if (trs[i]) {
            tr = trs[i];
        } else {
            tr = document.createElement("tr");
            tbody.appendChild(tr);
            row.forEach(() => {
                const td = document.createElement("td");
                tr.appendChild(td);
            });
        }

        const tds = tr.querySelectorAll("td");
        row.forEach((cell, j) => {
            tds[j].textContent = cell;
        });
    });

    // Remove surplus rows
    for (let i = dataRows.length; i < trs.length; i++) {
        trs[i].remove();
    }
}

function getContrastTextColor(color) {
    const temp = document.createElement("div");
    temp.style.color = color;
    document.body.appendChild(temp);

    const rgb = getComputedStyle(temp).color;
    document.body.removeChild(temp);

    const [r, g, b] = rgb.match(/\d+/g).map(Number);
    const brightness = (r * 299 + g * 587 + b * 114) / 1000;

    return brightness > 128 ? "black" : "white";
}

function reformatColor(color, canvas) {
    canvas.style.backgroundColor = color;

    const textColor = getContrastTextColor(color);
    const chart = Chart.getChart(canvas);

    chart.options.plugins.legend.labels.color = textColor;
    chart.options.plugins.title.color = textColor;

    chart.options.scales.x.ticks.color = textColor;
    chart.options.scales.y.ticks.color = textColor;
    if (chart.options.scales.z)
        chart.options.scales.z.ticks.color = textColor;

    if (chart.config.type === "matrix") {
        chart.data.datasets.forEach(ds => {
            ds.borderColor = textColor;
        });
    }

    chart.update();
}

async function autoChart() {
    const selPlot   = document.getElementById("selPlot");
    const noPlot    = document.getElementById("noPlot");
    const loader    = document.getElementById("loader");
    const chartGrid = document.getElementById("chartGrid");

    // ── Clear previous state ──────────────────────────────────────────────
    chartInstances.forEach(c => c.destroy());
    chartInstances.length = 0;

    // Destroy Muuri instance and empty the grid container
    if (muuriGrid) {
        muuriGrid.destroy();
        muuriGrid = null;
        console.log("[autoChart] Muuri grid destroyed.");
    }
    chartGrid.innerHTML = '';

    if (listIndexes.size === 0) {
        loader.hidden = true;
        selPlot.hidden = false;
        noPlot.hidden = true;
        return;
    }
    selPlot.hidden = true;
    noPlot.hidden  = true;
    loader.hidden  = false;

    // panelDivCopy is the inner content card — styling only, no layout role
    const panelDivTemplate = document.createElement("div");
    panelDivTemplate.classList.add("flex", "flex-col", "h-full");

    controller = new AbortController();
    try {
        const response = await fetch("/workspace", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify([...listIndexes].map(Number)),
            signal: controller.signal
        });
        const result = await response.json();

        loader.hidden = true;

        if (result.length === 0) {
            console.log("[autoChart] No charts returned.");
            selPlot.hidden = true;
            noPlot.hidden  = false;
            return;
        }

        // ── Init a fresh Muuri grid ───────────────────────────────────────
        muuriGrid = new Muuri(chartGrid, {
            items:       '.grid-item',
            layoutDuration: 300,
            layoutEasing:   'ease',
            dragEnabled:    true,
            dragHandle:     '.drag-handle',           // header bar is the drag handle
            dragStartPredicate: { distance: 5, delay: 0 },
        });
        console.log("[autoChart] Muuri grid initialised.");

        // ── Group samples by panelIndex ───────────────────────────────────
        const groupedResults = result.reduce((acc, sample) => {
            if (!acc[sample.panelIndex]) acc[sample.panelIndex] = [];
            acc[sample.panelIndex].push(sample);
            return acc;
        }, {});

        Object.keys(groupedResults).sort((a, b) => a - b).forEach(pIdx => {
            const panelSamples = groupedResults[pIdx];
            const panelDivCopy = panelDivTemplate.cloneNode();
            panelDivCopy.id = "panel" + pIdx;

            const wrappers = [];

            // ── Chart generation logic (UNCHANGED) ───────────────────────
            panelSamples.forEach((sample, sIdx) => {
                const wrapper = document.createElement("div");
                wrapper.className = "flex flex-col flex-1 relative";
                if (sIdx > 0) wrapper.style.display = 'none';

                wrapper.dataset.chartTitle = sample.title || `Chart ${sIdx + 1}`;

                const ctx = document.createElement("canvas");
                wrapper.appendChild(ctx);
                panelDivCopy.appendChild(wrapper);
                wrappers.push(wrapper);

                const isMatrix          = chartArray[sample.chartType] === 'matrix';
                const isMixed           = sample.chartType === 7;
                const isMixed_notStacked = sample.chartType === 9;

                const datasets = isMatrix
                    ? buildMatrixDataset(sample)
                    : [
                        ...sample.y.map(ds => ({
                            ...((isMixed || isMixed_notStacked) && {
                                type: chartArray[ds.chartType]
                            }),
                            label: ds.label,
                            data: ds.data,
                            yAxisID: 'y',
                        }))
                    ];

                if (!isMatrix && sample.z?.length > 0) {
                    datasets.push(
                        ...sample.z.map(ds => ({
                            ...((isMixed || isMixed_notStacked) && {
                                type: chartArray[ds.chartType]
                            }),
                            label: ds.label,
                            data: ds.data,
                            yAxisID: 'z',
                        }))
                    );
                }

                const cfg = {
                    ...(!(isMixed || isMixed_notStacked) && {
                        type: chartArray[sample.chartType]
                    }),

                    data: {
                        ...(isMatrix ? {} : { labels: sample.x }),
                        datasets
                    },

                    options: {
                        responsive: true,

                        plugins: {
                            legend: {
                                labels: { color: "white" }
                            },
                            title: {
                                display: true,
                                text: "My Chart",
                                color: "white"
                            }
                        },

                        scales: isMatrix
                            ? {
                                x: {
                                    type: "category",
                                    labels: sample.x,
                                    offset: true,
                                    ...axisStyle
                                },
                                y: {
                                    type: "category",
                                    labels: sample.y.map(r => r.label),
                                    offset: true,
                                    ...axisStyle
                                }
                            }
                            : isMixed
                                ? {
                                    x: stackedAxis,
                                    y: {
                                        position: "left",
                                        ...stackedAxis
                                    }
                                }
                                : {
                                    x: axisStyle,
                                    y: {
                                        position: "left",
                                        ...axisStyle
                                    }
                                }
                    }
                };

                if (isMatrix) {
                    cfg.options.plugins = cfg.options.plugins || {};
                    cfg.options.plugins.tooltip = cfg.options.plugins.tooltip || {};
                    cfg.options.plugins.tooltip.callbacks =
                        cfg.options.plugins.tooltip.callbacks || {};

                    cfg.options.plugins.tooltip.callbacks.title = (items) => {
                        const d = items[0].raw;
                        return `${d.y} / ${d.x}`;
                    };

                    cfg.options.plugins.tooltip.callbacks.label = (ctx) => {
                        const d = ctx.raw;
                        return `Value: ${d.v}`;
                    };
                }

                if (!isMatrix && sample.z?.length > 0) {
                    cfg.options.scales.z = {
                        position: 'right',
                        ...axisStyle,
                    };
                }

                if (sample.chartType === 6) {
                    cfg.options.indexAxis = 'y';
                }

                const chartInstance = new Chart(ctx, cfg);
                chartInstances.push(chartInstance);
            });
            // ── END chart generation logic ────────────────────────────────

            // Delegate DOM append + header wiring + grid registration
            appendChart(panelSamples, pIdx, wrappers, panelDivCopy);
        });

        // Refresh chart sizes after Muuri has laid out all items
        muuriGrid.on('layoutEnd', () => {
            chartInstances.forEach(c => c.resize());
        });
        muuriGrid.layout();
        console.log(`[autoChart] Layout triggered for ${Object.keys(groupedResults).length} panel(s).`);
    }
    catch (error) {
        if (error.name !== 'AbortError') {
            console.error("[autoChart] Fetch error:", error);
        }
    }
}

let _navChartInst = null;   // Chart.js instance for the mini overview
let _navDragState  = null;  // active drag: { type, startX, startLeft, startWidth }

/** Normalise a dataset's data array so the navigator always gets plain numbers. */
function _navNormalise(data) {
    return data.map(d =>
        d === null || d === undefined ? null
            : typeof d === 'object' && 'y' in d ? d.y
                : typeof d === 'number'           ? d
                    : null
    );
}

/** Returns the pixel width of the navigator track. */
function _navTrackWidth() {
    return document.getElementById('navTrack').getBoundingClientRect().width;
}

/**
 * Apply a new selection window (both % values 0-100) and update the
 * main chart's x.min / x.max accordingly.
 */
function _navUpdate(leftPct, widthPct, mainChart) {
    // Clamp
    widthPct = Math.max(4, widthPct);
    leftPct  = Math.max(0, leftPct);
    if (leftPct + widthPct > 100) {
        if (_navDragState?.type === 'left') leftPct  = 100 - widthPct;
        else                                widthPct = 100 - leftPct;
    }
    leftPct = Math.max(0, leftPct);

    // Reposition DOM elements
    const win = document.getElementById('navWindow');
    win.style.left  = `${leftPct}%`;
    win.style.width = `${widthPct}%`;
    document.getElementById('navMaskLeft').style.width  = `${leftPct}%`;
    document.getElementById('navMaskRight').style.width = `${100 - leftPct - widthPct}%`;

    // Drive the main chart's x-axis
    const labels = mainChart?.data?.labels;
    if (!labels?.length) return;

    const n   = labels.length;
    const lo  = Math.round(leftPct / 100 * n);
    const hi  = Math.min(n - 1, Math.round((leftPct + widthPct) / 100 * n) - 1);
    mainChart.options.scales.x.min = lo;
    mainChart.options.scales.x.max = Math.max(lo, hi);
    mainChart.update('none');
}

/**
 * Wire up mouse drag events for the navigator window and handles.
 * Must be called once per openZoom(); cleaned up by _navDestroy().
 */
function _navSetupDrag(mainChart) {
    const win         = document.getElementById('navWindow');
    const leftHandle  = document.getElementById('navHandleLeft');
    const rightHandle = document.getElementById('navHandleRight');
    const track       = document.getElementById('navTrack');

    function curPos() {
        return {
            leftPct:  parseFloat(win.style.left)  || 0,
            widthPct: parseFloat(win.style.width) || 100,
        };
    }

    function startDrag(type, e) {
        e.stopPropagation();
        e.preventDefault();
        const clientX = e.touches ? e.touches[0].clientX : e.clientX;
        const { leftPct, widthPct } = curPos();
        _navDragState = { type, startX: clientX, startLeft: leftPct, startWidth: widthPct };
        document.body.style.setProperty('user-select', 'none', 'important');
    }

    const onMove = (e) => {
        if (!_navDragState) return;
        const clientX = e.touches ? e.touches[0].clientX : e.clientX;
        const dx = (clientX - _navDragState.startX) / _navTrackWidth() * 100;
        const { startLeft, startWidth, type } = _navDragState;

        if      (type === 'move')  _navUpdate(startLeft + dx, startWidth,       mainChart);
        else if (type === 'left')  _navUpdate(startLeft + dx, startWidth - dx,  mainChart);
        else if (type === 'right') _navUpdate(startLeft,       startWidth + dx, mainChart);
    };

    const onUp = () => {
        _navDragState = null;
        document.body.style.removeProperty('user-select');
    };

    // Click on the masked areas to jump the window centre
    const onTrackClick = (e) => {
        if (_navDragState) return;
        const id = e.target?.id;
        if (id !== 'navMaskLeft' && id !== 'navMaskRight' && id !== 'navTrack') return;
        const rect    = track.getBoundingClientRect();
        const clickPct = (e.clientX - rect.left) / rect.width * 100;
        const { widthPct } = curPos();
        _navUpdate(clickPct - widthPct / 2, widthPct, mainChart);
    };

    leftHandle.addEventListener('mousedown',  (e) => startDrag('left',  e));
    rightHandle.addEventListener('mousedown', (e) => startDrag('right', e));
    win.addEventListener('mousedown', (e) => {
        if (e.target === leftHandle || leftHandle.contains(e.target))  return;
        if (e.target === rightHandle || rightHandle.contains(e.target)) return;
        startDrag('move', e);
    });
    track.addEventListener('click', onTrackClick);

    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup',   onUp);

    // Store cleanup so _navDestroy can remove global listeners
    _navChartInst._cleanupNav = () => {
        document.removeEventListener('mousemove', onMove);
        document.removeEventListener('mouseup',   onUp);
        track.removeEventListener('click', onTrackClick);
    };
}

/**
 * Create the navigator mini-chart and show the navigator bar.
 * Call this from openZoom() after the main chart has been resized.
 */
function _navInit(mainChart) {
    const container = document.getElementById('zoomNavigator');
    const canvas    = document.getElementById('navigatorCanvas');
    const labels    = mainChart?.data?.labels;

    // Navigator only makes sense for labelled (category) charts
    if (!labels?.length || mainChart.config.type === 'matrix') {
        container.classList.add('hidden');
        return;
    }

    if (_navChartInst) {
        _navChartInst._cleanupNav?.();
        _navChartInst.destroy();
        _navChartInst = null;
    }

    // Build simplified datasets — always line/area regardless of original type
    const navDatasets = mainChart.data.datasets.map(ds => ({
        type:            'line',
        label:           ds.label,
        data:            _navNormalise(Array.isArray(ds.data) ? ds.data : []),
        borderColor:     'rgba(233,150,122,0.75)',
        backgroundColor: 'rgba(233,150,122,0.12)',
        borderWidth:     1.5,
        pointRadius:     0,
        fill:            true,
        tension:         0.35,
    }));

    _navChartInst = new Chart(canvas, {
        type: 'line',
        data: { labels, datasets: navDatasets },
        options: {
            responsive:           true,
            maintainAspectRatio:  false,
            animation:            false,
            plugins: {
                legend:  { display: false },
                tooltip: { enabled: false },
            },
            scales: {
                x: { ticks: { display: false }, grid: { display: false }, border: { display: false } },
                y: { ticks: { display: false }, grid: { display: false }, border: { display: false } },
            },
        },
    });

    // Full-range selection on open
    _navUpdate(0, 100, mainChart);
    container.classList.remove('hidden');
    _navSetupDrag(mainChart);
}

/** Tear down the navigator and reset the main chart's x-axis constraints. */
function _navDestroy(mainChart) {
    if (_navChartInst) {
        _navChartInst._cleanupNav?.();
        _navChartInst.destroy();
        _navChartInst = null;
    }
    _navDragState = null;
    document.getElementById('zoomNavigator').classList.add('hidden');

    // Remove x-axis clamps so the chart renders fully when returned to the grid
    if (mainChart?.options?.scales?.x) {
        delete mainChart.options.scales.x.min;
        delete mainChart.options.scales.x.max;
    }
}

// ---------------------------------------------------------------------------

function openZoom(wrapper) {
    // Close any open menus first
    document.querySelectorAll('.plot-menu, .plot-colormap').forEach(m => m.classList.add('hidden'));

    const overlay = document.getElementById('zoomOverlay');
    const content = document.getElementById('zoomContent');

    _zoomedWrapper  = wrapper;
    _zoomedParent   = wrapper.parentElement;
    _zoomedSibling  = wrapper.nextElementSibling;
    _zoomedCssText  = wrapper.style.cssText;          // snapshot all inline styles

    document.getElementById('zoomTitle').textContent =
        wrapper.dataset.chartTitle || 'Chart';

    content.appendChild(wrapper);
    wrapper.style.cssText = 'display:flex; flex-direction:column; width:100%; height:100%;';

    overlay.classList.remove('hidden');
    document.addEventListener('keydown', _onEscClose);

    requestAnimationFrame(() => {
        const chart = Chart.getChart(wrapper.querySelector('canvas'));
        if (chart) {
            chart.resize();
            _navInit(chart);   // show the navigator now that the chart is live
        }
    });
}

function closeZoom() {
    if (!_zoomedWrapper) return;

    // Capture the chart reference before we move the wrapper back
    const chartBeforeMove = Chart.getChart(_zoomedWrapper.querySelector('canvas'));
    _navDestroy(chartBeforeMove);   // tear down navigator + reset x-axis clamps

    document.getElementById('zoomOverlay').classList.add('hidden');
    document.removeEventListener('keydown', _onEscClose);

    if (_zoomedSibling) {
        _zoomedParent.insertBefore(_zoomedWrapper, _zoomedSibling);
    } else {
        _zoomedParent.appendChild(_zoomedWrapper);
    }
    _zoomedWrapper.style.cssText = _zoomedCssText;

    const wrapper = _zoomedWrapper; // ← capture before nullifying

    _zoomedWrapper = _zoomedParent = _zoomedSibling = null;
    _zoomedCssText = '';

    requestAnimationFrame(() => {
        const chart = Chart.getChart(wrapper.querySelector('canvas'));
        chart?.resize();
        if (muuriGrid) muuriGrid.refreshItems().layout();
    });
}

function _onEscClose(e) {
    if (e.key === 'Escape') closeZoom();
}
// ---------------------------------------------------------------------------
// appendChart
// Responsible for everything that happens AFTER a panel's charts are created:
//   1. Build ONE panel-level header toolbar (always in the DOM, never hidden)
//   2. Wrap the panel content in a Muuri grid item shell
//   3. Register the item with muuriGrid
//
// KEY DESIGN: The drag-handle lives on the single panel header, NOT on the
// individual chart wrappers. Previously each wrapper had its own header with
// drag-handle, so hiding a wrapper also hid its header. Muuri's querySelector
// could then resolve to a hidden element and treat the item as immovable.
// With one persistent header the drag-handle is always present and visible.
// ---------------------------------------------------------------------------
function appendChart(panelSamples, pIdx, wrappers, panelDivCopy) {
    // ── 1. Build ONE panel-level header (always visible, owns drag-handle) ─
    const header = document.createElement("div");
    header.className = "drag-handle flex justify-between items-center p-2 border-b border-zinc-800 bg-zinc-900/30 relative z-50 select-none cursor-grab active:cursor-grabbing";

    // Drag grip icon (left side — visual affordance)
    const grip = document.createElement("div");
    grip.className = "text-zinc-600 hover:text-zinc-400 transition-colors pl-1 pointer-events-none";
    grip.innerHTML = `
        <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
            <circle cx="9"  cy="5"  r="1.5"/><circle cx="15" cy="5"  r="1.5"/>
            <circle cx="9"  cy="12" r="1.5"/><circle cx="15" cy="12" r="1.5"/>
            <circle cx="9"  cy="19" r="1.5"/><circle cx="15" cy="19" r="1.5"/>
        </svg>`;

    const container = document.createElement("div");
    container.className = "flex items-center justify-end gap-2";

    // ── Background color button ───────────────────────────────────────────
    const bgbtnWrapper = document.createElement("div");
    bgbtnWrapper.className = "relative flex flex-col items-end";

    const bgbtn = document.createElement("button");
    bgbtn.className = "hover:cursor-pointer text-sm tracking-wider px-2 py-1 rounded text-zinc-400 hover:text-white transition-all flex items-center gap-1.5 focus:outline-none";
    bgbtn.innerHTML = `
        Background color
        <svg class="w-2.5 h-2.5 transition-transform duration-200" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
        </svg>`;

    const colormap = document.createElement("div");
    colormap.className = "hidden absolute top-full right-0 mt-1 min-w-[150px] p-3 bg-zinc-900 border border-zinc-800 rounded shadow-2xl z-30 flex flex-col gap-2";
    colormap.innerHTML = `
        <input type="color" id="hiddenPicker-${pIdx}" class="w-full h-10 cursor-pointer rounded border-0 border-none bg-transparent" value="#000000">
        <input id="hexInput-${pIdx}" class="w-full bg-zinc-800 text-white px-3 py-2 rounded-lg outline-none text-sm text-center" placeholder="#000000">`;

    colormap.onclick = (e) => e.stopPropagation();

    const hiddenPicker = colormap.querySelector('input[type="color"]');
    const hexInput     = colormap.querySelector(`#hexInput-${pIdx}`);

    // Always apply color to whichever wrapper is currently visible
    const getActiveCanvas = () => {
        const active = wrappers.find(w => w.style.display !== 'none') ?? wrappers[0];
        return active.querySelector('canvas');
    };

    hiddenPicker.oninput = (e) => {
        const color = e.target.value;
        hexInput.value = color;
        reformatColor(color, getActiveCanvas());
    };
    hexInput.oninput = (e) => {
        const color = e.target.value;
        if (/^#[0-9A-F]{6}$/i.test(color)) {
            hiddenPicker.value = color;
            reformatColor(color, getActiveCanvas());
        }
    };

    colormap.classList.add('plot-colormap');
    bgbtn.classList.add('plot-bgbtn');

    bgbtn.onclick = (e) => {
        e.stopPropagation();
        const isHidden = colormap.classList.contains("hidden");
        document.querySelectorAll('.plot-menu, .plot-colormap').forEach(m => m.classList.add('hidden'));
        document.querySelectorAll('.plot-btn svg, .plot-bgbtn svg').forEach(s => s.classList.remove('rotate-180'));
        if (isHidden) {
            colormap.classList.remove("hidden");
            bgbtn.querySelector('svg').classList.add('rotate-180');
        }
    };

    bgbtnWrapper.appendChild(bgbtn);
    bgbtnWrapper.appendChild(colormap);

    // ── "Turn into" button (only when panel has multiple chart variants) ──
    if (panelSamples.length > 1) {
        const btnWrapper = document.createElement("div");
        btnWrapper.className = "relative flex flex-col items-end";

        const btn = document.createElement("button");
        btn.className = "hover:cursor-pointer text-sm tracking-wider px-2 py-1 rounded text-zinc-400 hover:text-white transition-all flex items-center gap-1.5";
        btn.innerHTML = `
            Turn into
            <svg class="w-2.5 h-2.5 transition-transform duration-200" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
            </svg>`;

        const menu = document.createElement("div");
        menu.className = "hidden absolute top-full right-0 mt-1 min-w-[120px] w-max max-w-[200px] bg-zinc-900 border border-zinc-800 rounded shadow-2xl py-1 z-30";

        panelSamples.forEach((targetSample, tIdx) => {
            const item = document.createElement("button");
            item.className = `w-full text-left px-3 py-1.5 text-sm transition-colors truncate ${tIdx === 0 ? 'text-[#E9967A] font-semibold bg-zinc-800/50' : 'text-zinc-400 hover:bg-zinc-800 hover:text-white'}`;
            item.innerText = targetSample.title || `Chart ${tIdx + 1}`;
            item.onclick = (e) => {
                e.stopPropagation();

                // Show the target wrapper, hide all others
                wrappers.forEach((w, i) => {
                    w.style.display = i === tIdx ? 'flex' : 'none';

                });

                // Sync active-item styles in the dropdown
                menu.querySelectorAll('button').forEach((b, i) => {
                    b.className = `w-full text-left px-3 py-1.5 text-sm transition-colors truncate ${i === tIdx ? 'text-[#E9967A] font-semibold bg-zinc-800/50' : 'text-zinc-400 hover:bg-zinc-800 hover:text-white'}`;
                });

                menu.classList.add("hidden");
                btn.querySelector('svg').classList.remove('rotate-180');

                // Resize the newly visible chart so it fills the panel correctly
                const chart = Chart.getChart(wrappers[tIdx].querySelector('canvas'));
                if (chart) setTimeout(() => chart.resize(), 0);
            };
            menu.appendChild(item);
        });

        menu.classList.add('plot-menu');
        btn.classList.add('plot-btn');

        btn.onclick = (e) => {
            e.stopPropagation();
            const isHidden = menu.classList.contains("hidden");
            document.querySelectorAll('.plot-menu, .plot-colormap').forEach(m => m.classList.add('hidden'));
            document.querySelectorAll('.plot-btn svg, .plot-bgbtn svg').forEach(s => s.classList.remove('rotate-180'));
            if (isHidden) {
                menu.classList.remove("hidden");
                btn.querySelector('svg').classList.add('rotate-180');
            }
        };

        btnWrapper.appendChild(btn);
        btnWrapper.appendChild(menu);
        container.appendChild(btnWrapper);
    }

    // --- ZOOM BUTTON ---
    const zoomBtn = document.createElement("button");
    zoomBtn.className = "hover:cursor-pointer px-2 py-1 rounded text-zinc-400 hover:text-white transition-all flex items-center";
    zoomBtn.title = "Fullscreen";
    zoomBtn.innerHTML = `
    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5
                 M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4"/>
    </svg>`;
    zoomBtn.onclick = (e) => {
        e.stopPropagation();
        const activeWrapper = wrappers.find(w => w.style.display !== 'none') ?? wrappers[0];
        openZoom(activeWrapper);
    };

    container.appendChild(zoomBtn);
    container.appendChild(bgbtnWrapper);
    header.appendChild(grip);
    header.appendChild(container);

    // Prepend the single header to the panel div (before all chart wrappers)
    panelDivCopy.prepend(header);

    // ── 2. Wrap in Muuri grid item shell ──────────────────────────────────
    // Structure: .grid-item > .grid-item-content > panelDivCopy
    // .grid-item      — Muuri positions this absolutely
    // .grid-item-content — provides padding, border, rounded corners
    const gridItem = document.createElement("div");
    gridItem.className = "grid-item";

    const gridItemContent = document.createElement("div");
    gridItemContent.className = "grid-item-content rounded-2xl border   ";
    gridItemContent.appendChild(panelDivCopy);
    gridItem.appendChild(gridItemContent);

    // ── 3. Register with Muuri ────────────────────────────────────────────
    // appendChildToGrid=false: we mount the DOM element ourselves so Muuri
    // doesn't double-append it. Then we call add() to let Muuri track it.
    document.getElementById("chartGrid").appendChild(gridItem);
    muuriGrid.add(gridItem, { layout: false }); // layout:false — batch; called once after all panels
    console.log(`[appendChart] Panel ${pIdx} added to grid (${panelSamples.length} variant(s)).`);
}

function showToast(message, isError = false) {
    const toast = document.createElement("div");
    toast.className = `fixed top-6 left-1/2 -translate-x-1/2 z-[100] px-6 py-3 rounded-full shadow-2xl backdrop-blur-xl border ${isError ? 'bg-red-500/10 border-red-500/20 text-red-400' : 'bg-green-500/10 border-green-500/20 text-green-400'} text-sm font-medium transition-all duration-300 transform scale-95 opacity-0 flex items-center gap-2`;
    toast.innerHTML = `
        ${isError
        ? '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path></svg>'
        : '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path></svg>'}
        <span>${message}</span>
    `;
    document.body.appendChild(toast);

    requestAnimationFrame(() => {
        toast.classList.remove('scale-95', 'opacity-0');
        toast.classList.add('scale-100', 'opacity-100');
    });

    setTimeout(() => {
        toast.classList.remove('scale-100', 'opacity-100');
        toast.classList.add('scale-95', 'opacity-0');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function gearBtnClicked(event, button) {
    event.stopPropagation();

    const popup   = button.parentElement.querySelector('.gearPopup');
    const wasOpen = !popup.classList.contains('hidden');

    document.querySelectorAll('.filterPopup').forEach(fm => fm.classList.add('hidden'));
    document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
    document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));

    if (!wasOpen) {
        // Reuse shared positionDropdown — same fix as filterPopup
        positionDropdown(popup, button);
        popup.classList.remove('hidden');
        button.querySelector('svg').classList.add('rotate-180');
        console.log(`[gearBtnClicked] Opened gear popup`);
    }
}

function toggleFieldTypeMenu(btn) {
    const popup    = btn.nextElementSibling;
    const isHidden = popup.classList.contains('hidden');

    document.querySelectorAll('.type-menu-popup').forEach(p => p.classList.add('hidden'));
    document.querySelectorAll('.type-menu-btn svg').forEach(s => s.classList.remove('rotate-180'));

    if (isHidden) {
        popup.classList.remove('hidden');
        btn.querySelector('svg').classList.add('rotate-180');
    }
}

async function changeFieldType(event, fieldIndex, newType) {
    event.stopPropagation();

    document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
    document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));

    try {
        const formData = new FormData();
        formData.append('fieldIndex', fieldIndex);
        formData.append('newType', newType);

        const response = await fetch('/workspace/changeFieldType', {
            method: 'POST',
            body: formData
        });
        const result = await response.json();

        if (result === "Field updated") {
            const typeMapping = {
                "string":   "String",
                "int":      "Int32",
                "double":   "Double",
                "datetime": "DateTime",
                "bool":     "Boolean",
                "list":     "List`1"
            };
            const span = document.getElementById(`fieldType-${fieldIndex}`);
            if (span) {
                span.textContent = typeMapping[newType] || newType;
                const header = span.closest('th');
                if (header) {
                    const buttonsContainer = header.querySelector('.type-menu-popup');
                    if (buttonsContainer) {
                        const buttonsHtml = Object.keys(typeMapping)
                            .filter(t => t !== newType)
                            .map(t => `<button onclick="changeFieldType(event, ${fieldIndex}, '${t}')" class="text-left px-3 py-1.5 text-xs text-zinc-300 hover:text-white hover:bg-white/10 rounded-lg transition-colors w-full tracking-wide">${t}</button>`)
                            .join('');
                        buttonsContainer.innerHTML = buttonsHtml;
                    }
                }
            }
            showToast(`Field type successfully changed to ${newType}.`);
            console.log(`[changeFieldType] Field ${fieldIndex} → ${newType}`);
        } else {
            showToast("Failed to update field type.", true);
            console.warn(`[changeFieldType] Server rejected update for field ${fieldIndex}`);
        }
    } catch (e) {
        showToast("Error updating field type.", true);
        console.error("[changeFieldType] Fetch error:", e);
    }
}

function fieldClicked(fieldindex) {
    if (controller) {
        controller.abort("New fetch!");
    }
    if (listIndexes.has(fieldindex)) listIndexes.delete(fieldindex);
    else listIndexes.add(fieldindex);
    autoChart();
}

async function filterIntSpecificKey(event, input) {
    if (event.key === "Enter") {
        event.preventDefault();
        const value = input.value;
        if (value.trim() === "") return;

        const columnHeader = input.closest("th");
        const index = columnHeader.dataset.index;
        const popup = input.closest(".filterPopup");
        const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]').checked;

        await sendIntSpecificFilterRequest(value, isNotCheckboxValue, index, input);
    }
}

async function sendIntSpecificFilterRequest(rawVal, isNot, index, input) {
    const popup     = input.closest(".filterPopup");
    const fieldType = enumMap[popup.dataset.fieldType];

    if (!filters[index]) {
        filters[index] = {
            fieldIndex: parseInt(index),
            fieldType,
            filteringValuesInt:    [],
            filteringValuesDouble: [],
            intBound:    null,
            doubleBound: null,
            isNot
        };
    }

    filters[index].intBound    = null;
    filters[index].doubleBound = null;
    filters[index].fieldType   = fieldType;

    let displayVal = null;
    if (fieldType === 1) { // Int
        const value = parseInt(rawVal);
        if (!isNaN(value)) {
            if (!filters[index].filteringValuesInt) filters[index].filteringValuesInt = [];
            if (!filters[index].filteringValuesInt.includes(value)) {
                filters[index].filteringValuesInt.push(value);
                displayVal = value;
            }
        }
    } else { // Double
        const value = parseFloat(rawVal);
        if (!isNaN(value)) {
            if (!filters[index].filteringValuesDouble) filters[index].filteringValuesDouble = [];
            if (!filters[index].filteringValuesDouble.includes(value)) {
                filters[index].filteringValuesDouble.push(value);
                displayVal = value;
            }
        }
    }

    filters[index].isNot = isNot;

    updateFilterChipUI(index);
    if (displayVal !== null) appendIntFilterValue(input, index, displayVal);

    await executeFilterRequest();
}

function appendIntFilterValue(input, index, value) {
    const container = input.previousElementSibling; // filterValues block

    // Clear any existing range chips before adding a specific-value chip
    Array.from(container.children).forEach(child => {
        if (child.textContent.includes(" to ")) child.remove();
    });

    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    chip.innerHTML = `<span class="truncate min-w-0">${value}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index]) {
            filters[index].filteringValuesInt    = (filters[index].filteringValuesInt    || []).filter(v => v !== value);
            filters[index].filteringValuesDouble = (filters[index].filteringValuesDouble || []).filter(v => v !== value);
            if (
                (!filters[index].filteringValuesInt    || filters[index].filteringValuesInt.length    === 0) &&
                (!filters[index].filteringValuesDouble || filters[index].filteringValuesDouble.length === 0) &&
                !filters[index].intBound && !filters[index].doubleBound
            ) {
                filters[index] = undefined;
            }
        }
        chip.remove();

        const popup = input.closest(".filterPopup");
        if (popup) {
            const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]').checked;
            if (filters[index]) filters[index].isNot = isNotCheckboxValue;
        }

        await executeFilterRequest();
    };

    container.appendChild(chip);
    input.value = "";
}

async function filterIntRangeKey(event, input) {
    if (event.key === "Enter") {
        event.preventDefault();
        const popup = input.closest(".filterPopup");
        const btn   = popup.querySelector("button");
        await applyIntRangeFilter(event, btn);
    }
}

async function applyIntRangeFilter(event, btn) {
    event.stopPropagation();
    const columnHeader = btn.closest("th");
    if (!columnHeader) return;
    const index     = columnHeader.dataset.index;
    const popup     = btn.closest(".filterPopup");
    const fieldType = enumMap[popup.dataset.fieldType];

    const minInput = popup.querySelector(".int-range-min").value;
    const maxInput = popup.querySelector(".int-range-max").value;
    const isNot    = popup.querySelector('input[name="IsNot"]').checked;

    if (minInput === "" && maxInput === "") return;

    if (!filters[index]) {
        filters[index] = {
            fieldIndex: parseInt(index),
            fieldType,
            filteringValuesInt:    null,
            filteringValuesDouble: null,
            isNot
        };
    }

    filters[index].fieldType           = fieldType;
    filters[index].filteringValuesInt  = null;
    filters[index].filteringValuesDouble = null;

    let minVal, maxVal;
    if (fieldType === 1) { // Int
        minVal = minInput === "" ? -2147483648          : parseInt(minInput);
        maxVal = maxInput === "" ? 2147483647           : parseInt(maxInput);
        filters[index].intBound = { leftBound: minVal, rightBound: maxVal };
    } else { // Double
        minVal = minInput === "" ? -1.7976931348623157E+308 : parseFloat(minInput);
        maxVal = maxInput === "" ?  1.7976931348623157E+308 : parseFloat(maxInput);
        filters[index].doubleBound = { leftBound: minVal, rightBound: maxVal };
    }
    filters[index].isNot = isNot;

    updateFilterChipUI(index);

    const container = popup.querySelector(".filterValues");
    container.innerHTML = "";
    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    chip.innerHTML = `<span class="truncate min-w-0">${minInput === "" ? "-inf" : minVal} to ${maxInput === "" ? "+inf" : maxVal}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index]) {
            filters[index].intBound    = null;
            filters[index].doubleBound = null;
        }
        if (!filters[index] || (
            (!filters[index].filteringValuesInt    || filters[index].filteringValuesInt.length    === 0) &&
            (!filters[index].filteringValuesDouble || filters[index].filteringValuesDouble.length === 0)
        )) {
            filters[index] = undefined;
        }
        chip.remove();

        popup.querySelector(".int-range-min").value = "";
        popup.querySelector(".int-range-max").value = "";

        await executeFilterRequest();
    };

    container.appendChild(chip);
    await executeFilterRequest();
}

async function applyDtRangeFilter(event, btn) {
    event.stopPropagation();
    const columnHeader = btn.closest("th");
    if (!columnHeader) return;
    const index     = columnHeader.dataset.index;
    const popup     = btn.closest(".filterPopup");
    const fieldType = enumMap[popup.dataset.fieldType];

    const minInput = popup.querySelector(".dt-range-min").value;
    const maxInput = popup.querySelector(".dt-range-max").value;
    const isNot    = popup.querySelector('input[name="IsNot"]').checked;

    if (minInput === "" && maxInput === "") return;

    const minDt = minInput === "" ? "0001-01-01T00:00:00" : minInput;
    const maxDt = maxInput === "" ? "9999-12-31T23:59:59" : maxInput;

    if (!filters[index]) {
        filters[index] = { fieldIndex: parseInt(index), fieldType, isNot, dateTimeBound: null };
    }

    filters[index].fieldType     = fieldType;
    filters[index].dateTimeBound = { leftBound: minDt, rightBound: maxDt };
    filters[index].isNot         = isNot;

    updateFilterChipUI(index);

    const container = popup.querySelector(".filterValues");
    container.innerHTML = "";
    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    const minDpy = minInput === "" ? "-inf" : new Date(minInput).toLocaleString();
    const maxDpy = maxInput === "" ? "+inf" : new Date(maxInput).toLocaleString();
    chip.innerHTML = `<span class="truncate min-w-0">${minDpy} to <br> ${maxDpy}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index]) filters[index].dateTimeBound = null;
        filters[index] = undefined;
        chip.remove();

        popup.querySelector(".dt-range-min").value = "";
        popup.querySelector(".dt-range-max").value = "";

        await executeFilterRequest();
    };

    container.appendChild(chip);
    await executeFilterRequest();
}

async function excludeDtCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    if (filters[index]) {
        filters[index].isNot = checkbox.checked;
        await executeFilterRequest();
    }
}

async function applyBoolFilter(event, btn, val) {
    event.stopPropagation();
    const columnHeader = btn.closest("th");
    if (!columnHeader) return;
    const index     = columnHeader.dataset.index;
    const popup     = btn.closest(".filterPopup");
    const fieldType = enumMap[popup.dataset.fieldType];
    const isNot     = popup.querySelector('input[name="IsNot"]')?.checked || false;

    if (!filters[index]) {
        filters[index] = { fieldIndex: parseInt(index), fieldType, filteringValues: [], isNot };
    }
    filters[index].fieldType = fieldType;

    // Strict toggle: overwrite to single value
    filters[index].filteringValues = [val];
    filters[index].isNot = isNot;

    const container = popup.querySelector(".filterValues");
    container.innerHTML = "";
    appendBoolFilterValue(popup, index, val);

    updateFilterChipUI(index);
    await executeFilterRequest();
}

function appendBoolFilterValue(popup, index, value) {
    const container = popup.querySelector(".filterValues");
    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    chip.innerHTML = `<span class="truncate min-w-0">${value}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index] && filters[index].filteringValues) {
            filters[index].filteringValues = filters[index].filteringValues.filter(v => v !== value);
            if (filters[index].filteringValues.length === 0) {
                filters[index] = undefined;
            }
        }
        chip.remove();
        await executeFilterRequest();
    };
    container.appendChild(chip);
}

async function excludeBoolCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    if (filters[index]) {
        filters[index].isNot = checkbox.checked;
        await executeFilterRequest();
    }
}

async function partialMatchStrCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    if (filters[index]) {
        filters[index].isPartialMatch = checkbox.checked;
        await executeFilterRequest();
    }
}

async function excludeIntCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    if (filters[index]) {
        filters[index].isNot = checkbox.checked;
        await executeFilterRequest();
    }
}

let executingFilterController = null;
async function executeFilterRequest() {
    if (executingFilterController) {
        executingFilterController.abort();
    }
    executingFilterController = new AbortController();
    controller = executingFilterController;
    const activeFilters = filters.filter(f => f !== undefined);
    console.log(`[executeFilterRequest] Sending ${activeFilters.length} active filter(s).`);
    try {
        const response = await fetch("/workspace/filter", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(activeFilters),
            signal: executingFilterController.signal
        });
        const result = await response.json();
        updateTable(result);
    } catch (e) {
        if (e.name !== 'AbortError') console.error("[executeFilterRequest] Fetch error:", e);
    }
}

document.addEventListener("DOMContentLoaded", () => {
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
                    // console.log(`[ResizeObserver] chartGrid width=${Math.round(w)}px → ${cols} col(s).`);
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