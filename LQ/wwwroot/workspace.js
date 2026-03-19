let chartInstances = [];
const listIndexes = new Set();
const chartArray = ['pie', 'bar', 'line', 'bar', 'scatter', 'polarArea', 'bar', 'mixed', 'matrix', 'mixedBar'];
let controller;

let filters = [];

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

function filterClicked(event, filterbtn) {
    event.stopPropagation();

    const popup = filterbtn.parentElement.querySelector(".filterPopup");
    const wasOpen = !popup.classList.contains("hidden");

    document.querySelectorAll('.filterPopup').forEach(fm => fm.classList.add('hidden'));
    document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
    document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));

    if (!wasOpen) {
        popup.classList.remove('hidden');
    }

    const input = popup.querySelector("input");
    input.focus();
}

function filterPopupClicked(event, filterpopup) {
    event.stopPropagation();

    const input = filterpopup.querySelector("input");
    if (input) {
        input.focus();
    }
}
async function filterInputKey(event, input) {
    if (event.key === "Enter") {
        event.preventDefault();

        const value = input.value;
        const columnHeader = input.closest("th");
        const index = columnHeader.dataset.index;
        const isNotCheckboxValue = input.nextElementSibling.querySelector("input").checked;

        await sendFilterRequest(value.trim(), isNotCheckboxValue, index);
        appendFilterValue(input, index);
    }
}
function appendFilterValue(input, index) {
    const container = input.previousElementSibling; // chip container above input
    const value = input.value.trim();
    if (!value) return;

    const chip = document.createElement("div");
    chip.className =
        "filterValue rounded-xl bg-[darksalmon] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";

    const text = document.createElement("span");
    text.textContent = value;
    text.className = "truncate min-w-0";   // important

    const removeBtn = document.createElement("button");
    removeBtn.textContent = "✕";
    removeBtn.className = "ml-1 font-bold hover:cursor-pointer";

    removeBtn.onclick = async (e) => {
        e.stopPropagation();

        if (filters[index].filteringValues) {
            filters[index].filteringValues =
                filters[index].filteringValues.filter(v => v !== value);

            if (filters[index].filteringValues.length === 0) {
                filters[index] = undefined;
            }
        }

        chip.remove();
        const isNotCheckboxValue = input.nextElementSibling.querySelector("input").checked;
        await sendFilterRequest("", isNotCheckboxValue, index);
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

    if (filter && filter.filteringValues && filter.filteringValues.length > 0) {
        chip.textContent = filter.filteringValues.length;
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

    await sendFilterRequest("", isNotCheckboxValue, index);
}

async function sendFilterRequest(value, isNotCheckboxValue, index) {
    if (value !== "") {
        if (!filters[index]) {
            filters[index] = {
                isPartialMatch: true,
                fieldIndex: index,
                filteringValues: []
            };
        }
        if (!filters[index].filteringValues.includes(value)) {
            filters[index].filteringValues.push(value);
        }
        filters[index].isNot = isNotCheckboxValue;
    }
    else if (filters[index]) {
        filters[index].isNot = isNotCheckboxValue;
        if (filters[index].filteringValues.length === 0) {
            filters[index] = undefined;
        }
    }

    updateFilterChipUI(index);

    controller = new AbortController();
    const response = await fetch("/workspace/filter", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(filters.filter(f => f !== undefined)),
        signal: controller.signal
    });
    const result = await response.json();

    updateTable(result);
}

function updateTable(rows) {
    const tbody = document.querySelector("#dataTable tbody");
    let trs = tbody.querySelectorAll("tr");

    const dataRows = rows.slice(1); // skip header if server includes it

    dataRows.forEach((row, i) => {

        let tr;

        if (trs[i]) {
            tr = trs[i];
        }
        else {
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
    const selPlot = document.getElementById("selPlot");
    const noPlot = document.getElementById("noPlot");
    const loader = document.getElementById("loader");
    const panel = document.getElementById("plotPanel");

    // Clear previous charts and panels
    chartInstances.forEach(c => c.destroy());
    chartInstances.length = 0;
    panel.querySelectorAll('[id^="panel"]').forEach(el => el.remove());

    if (listIndexes.size === 0) {
        loader.hidden = true;
        selPlot.hidden = false;
        noPlot.hidden = true;
        return;
    }
    selPlot.hidden = true;
    noPlot.hidden = true;
    loader.hidden = false;

    const panelDivTemplate = document.createElement("div");
    panelDivTemplate.classList.add("flex", "flex-col", "border-2", "border-white", "mb-4");

    controller = new AbortController();
    try {
        const response = await fetch("/workspace", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify([...listIndexes].map(Number)),
            signal: controller.signal
        });
        const result = await response.json();

        loader.hidden = true;

        if (result.length === 0) {
            console.log("No charts found.");
            selPlot.hidden = true;
            noPlot.hidden = false;
            return;
        }

        // Group samples by panelIndex
        const groupedResults = result.reduce((acc, sample) => {
            if (!acc[sample.panelIndex]) acc[sample.panelIndex] = [];
            acc[sample.panelIndex].push(sample);
            return acc;
        }, {});

        Object.keys(groupedResults).sort((a, b) => a - b).forEach(pIdx => {
            const panelSamples = groupedResults[pIdx];
            const panelDivCopy = panelDivTemplate.cloneNode();
            panelDivCopy.id = "panel" + pIdx;
            panel.appendChild(panelDivCopy);

            const wrappers = [];

            panelSamples.forEach((sample, sIdx) => {
                const wrapper = document.createElement("div");
                wrapper.className = "flex flex-col flex-1 relative";
                if (sIdx > 0) wrapper.style.display = 'none';

                const ctx = document.createElement("canvas");
                wrapper.appendChild(ctx);
                panelDivCopy.appendChild(wrapper);
                wrappers.push(wrapper);

                const isMatrix = chartArray[sample.chartType] === 'matrix';
                const isMixed = sample.chartType === 7;
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

            wrappers.forEach((wrapper, sIdx) => {
                const header = document.createElement("div");
                header.className = "flex justify-end p-2 border-b border-zinc-800 bg-zinc-900/30 relative z-50";

                const container = document.createElement("div");
                container.className = "flex items-center justify-end gap-2";

                const btnWrapper = document.createElement("div");
                btnWrapper.className = "relative flex flex-col items-end";

                const bgbtnWrapper = document.createElement("div");
                bgbtnWrapper.className = "relative flex flex-col items-end";

                // Turn into button
                const btn = document.createElement("button");
                btn.className = "hover:cursor-pointer text-sm tracking-wider px-2 py-1 rounded text-zinc-400 hover:text-white transition-all flex items-center gap-1.5";
                btn.innerHTML = `
                        Turn into
                        <svg class="w-2.5 h-2.5 transition-transform duration-200" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
                        </svg>
                    `;
                const menu = document.createElement("div");
                menu.className = "hidden absolute top-full right-0 mt-1 min-w-[120px] w-max max-w-[200px] bg-zinc-900 border border-zinc-800 rounded shadow-2xl py-1 z-30";
                panelSamples.forEach((targetSample, tIdx) => {
                    const item = document.createElement("button");
                    item.className = `w-full text-left px-3 py-1.5 text-sm transition-colors truncate ${tIdx === sIdx ? 'text-[#E9967A] font-semibold bg-zinc-800/50' : 'text-zinc-400 hover:bg-zinc-800 hover:text-white'}`;
                    item.innerText = targetSample.title || `Chart ${tIdx + 1}`;
                    item.onclick = (e) => {
                        e.stopPropagation();
                        wrapper.style.display = 'none';
                        wrappers[tIdx].style.display = 'flex';
                        menu.classList.add("hidden");
                    };
                    menu.appendChild(item);
                });

                // Background color button
                const bgbtn = document.createElement("button");
                bgbtn.className = "hover:cursor-pointer text-sm tracking-wider px-2 py-1 rounded text-zinc-400 hover:text-white transition-all flex items-center gap-1.5 focus:outline-none";
                bgbtn.innerHTML = `
                        Background color
                        <svg class="w-2.5 h-2.5 transition-transform duration-200" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
                        </svg>
                    `;

                const colormap = document.createElement("div");
                colormap.className = "hidden absolute top-full right-0 mt-1 min-w-[150px] p-3 bg-zinc-900 border border-zinc-800 rounded shadow-2xl z-30 flex flex-col gap-2";
                colormap.innerHTML = `
                        <input type="color" id="hiddenPicker-${pIdx}-${sIdx}" class="w-full h-10 cursor-pointer rounded border-0 border-none bg-transparent" value="#000000">
                        <input id="hexInput-${pIdx}-${sIdx}" class="w-full bg-zinc-800 text-white px-3 py-2 rounded-lg outline-none text-sm text-center" placeholder="#000000">
                    `;

                colormap.onclick = (e) => e.stopPropagation();

                const hiddenPicker = colormap.querySelector('input[type="color"]');
                const hexInput = colormap.querySelector(`#hexInput-${pIdx}-${sIdx}`);

                hiddenPicker.oninput = (e) => {
                    const color = e.target.value;
                    hexInput.value = color;
                    // wrapper.querySelector('canvas').style.backgroundColor = color;
                    reformatColor(color, wrapper.querySelector('canvas'));
                };

                hexInput.oninput = (e) => {
                    const color = e.target.value;
                    if (/^#[0-9A-F]{6}$/i.test(color)) {
                        hiddenPicker.value = color;
                        // wrapper.querySelector('canvas').style.backgroundColor = color;
                        reformatColor(color, wrapper.querySelector('canvas'));
                    }
                };

                menu.classList.add('plot-menu');
                btn.classList.add('plot-btn');
                bgbtn.classList.add('plot-bgbtn');
                colormap.classList.add('plot-colormap');

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

                if (panelSamples.length > 1) {
                    btnWrapper.appendChild(btn);
                    btnWrapper.appendChild(menu);
                    container.appendChild(btnWrapper);
                }

                bgbtnWrapper.appendChild(bgbtn);
                bgbtnWrapper.appendChild(colormap);

                container.appendChild(bgbtnWrapper);
                header.appendChild(container);
                wrapper.prepend(header);
            });

        });
    }
    catch (error) {
        if (error.name !== 'AbortError') {
            console.log(error);
        }
    }
}

function showToast(message, isError = false) {
    const toast = document.createElement("div");
    toast.className = `fixed top-6 left-1/2 -translate-x-1/2 z-[100] px-6 py-3 rounded-full shadow-2xl backdrop-blur-xl border ${isError ? 'bg-red-500/10 border-red-500/20 text-red-400' : 'bg-green-500/10 border-green-500/20 text-green-400'} text-sm font-medium transition-all duration-300 transform scale-95 opacity-0 flex items-center gap-2`;
    toast.innerHTML = `
        ${isError ?
            '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path></svg>' :
            '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path></svg>'}
        <span>${message}</span>
    `;
    document.body.appendChild(toast);

    // Animate in
    requestAnimationFrame(() => {
        toast.classList.remove('scale-95', 'opacity-0');
        toast.classList.add('scale-100', 'opacity-100');
    });

    // Animate out and remove
    setTimeout(() => {
        toast.classList.remove('scale-100', 'opacity-100');
        toast.classList.add('scale-95', 'opacity-0');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}


function gearBtnClicked(event, button) {
    event.stopPropagation();

    const popup = button.parentElement.querySelector('.gearPopup');
    const wasOpen = !popup.classList.contains('hidden');

    document.querySelectorAll('.filterPopup').forEach(fm => fm.classList.add('hidden'));
    document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
    document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));

    if (!wasOpen) {
        popup.classList.remove('hidden');
        button.querySelector('svg').classList.add('rotate-180');

        const rect = button.getBoundingClientRect();
        popup.style.position = 'fixed'; // Ensure it's fixed for accurate positioning
        popup.style.top = `${rect.bottom + 8}px`;
        popup.style.left = `${rect.left}px`;
    }
}

function toggleFieldTypeMenu(btn) {
    const popup = btn.nextElementSibling;
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

    // Hide all popups
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
                "string": "String",
                "int": "Int32",
                "double": "Double",
                "datetime": "DateTime",
                "bool": "Boolean",
                "list": "List`1"
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
        } else {
            showToast("Failed to update field type.", true);
        }
    } catch (e) {
        showToast("Error updating field type.", true);
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
        const value = parseInt(input.value);
        if (isNaN(value)) return;

        const columnHeader = input.closest("th");
        const index = columnHeader.dataset.index;
        const popup = input.closest(".filterPopup");
        const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]').checked;

        await sendIntSpecificFilterRequest(value, isNotCheckboxValue, index, input);
    }
}

async function sendIntSpecificFilterRequest(value, isNot, index, input) {
    if (!filters[index]) {
        filters[index] = { fieldIndex: parseInt(index), filteringValuesInt: [], intBound: null, isNot: isNot };
    }

    // Clear range if we're using specific values
    filters[index].intBound = null;
    if (!filters[index].filteringValuesInt) filters[index].filteringValuesInt = [];

    if (value !== null && !filters[index].filteringValuesInt.includes(value)) {
        filters[index].filteringValuesInt.push(value);
    }
    filters[index].isNot = isNot;

    updateFilterChipUI(index);
    if (value !== null) appendIntFilterValue(input, index, value);

    await executeFilterRequest();
}

function appendIntFilterValue(input, index, value) {
    const container = input.previousElementSibling; // filterValues block

    // Clear any existing range chips
    Array.from(container.children).forEach(child => {
        if (child.textContent.includes(" to ")) child.remove();
    });

    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    chip.innerHTML = `<span class="truncate min-w-0">${value}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index]) {
            filters[index].filteringValuesInt = (filters[index].filteringValuesInt || []).filter(v => v !== value);
            if (filters[index].filteringValuesInt.length === 0 && !filters[index].intBound) {
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
        const btn = popup.querySelector("button");
        await applyIntRangeFilter(event, btn);
    }
}

async function applyIntRangeFilter(event, btn) {
    event.stopPropagation();
    const columnHeader = btn.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    const popup = btn.closest(".filterPopup");

    const minInput = popup.querySelector(".int-range-min").value;
    const maxInput = popup.querySelector(".int-range-max").value;
    const isNot = popup.querySelector('input[name="IsNot"]').checked;

    if (minInput === "" && maxInput === "") return; // Do nothing if both empty

    const minVal = minInput === "" ? -2147483648 : parseInt(minInput);
    const maxVal = maxInput === "" ? 2147483647 : parseInt(maxInput);

    if (!filters[index]) {
        filters[index] = { fieldIndex: parseInt(index), filteringValuesInt: [], isNot: isNot };
    }

    filters[index].filteringValuesInt = null;
    filters[index].intBound = { leftBound: minVal, rightBound: maxVal };
    filters[index].isNot = isNot;

    updateFilterChipUI(index);

    const container = popup.querySelector(".filterValues");
    container.innerHTML = ""; // Clear specific value chips
    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    chip.innerHTML = `<span class="truncate min-w-0">${minInput === "" ? "-inf" : minVal} to ${maxInput === "" ? "+inf" : maxVal}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index]) filters[index].intBound = null;
        if (!filters[index] || (!filters[index].filteringValuesInt || filters[index].filteringValuesInt.length === 0)) {
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

async function executeFilterRequest() {
    controller = new AbortController();
    console.log(filters)
    const activeFilters = filters.filter(f => f !== undefined);
    const response = await fetch("/workspace/filter", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(activeFilters),
        signal: controller.signal
    });
    const result = await response.json();
    updateTable(result);
}

document.addEventListener("DOMContentLoaded", () => {
    const resizer = document.getElementById('drag');
    const left = document.getElementById('tablePanel');
    const table = document.getElementById("dataTable");

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
        });

        document.addEventListener('mouseup', () => {
            isDragging = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
        });
    }
});
