let chartInstances = [];
const listIndexes = new Set();
const chartArray = ['pie', 'bar', 'line', 'bar', 'scatter', 'polarArea', 'bar', 'mixed', 'matrix', 'mixedBar'];
let controller;

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
                        datasets,
                    },
                    options: {
                        responsive: true,
                        scales: isMatrix
                            ? {
                                x: {
                                    type: 'category',
                                    labels: sample.x,
                                    offset: true
                                },
                                y: {
                                    type: 'category',
                                    labels: sample.y.map(r => r.label),
                                    offset: true
                                }
                            }
                            : isMixed
                                ? {
                                    x: { stacked: true },
                                    y: { position: 'left', stacked: true }
                                }
                                : {
                                    y: { position: 'left' }
                                },
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
                    cfg.options.scales.z = { position: 'right' };
                }

                if (sample.chartType === 6) {
                    cfg.options.indexAxis = 'y';
                }

                const chartInstance = new Chart(ctx, cfg);
                chartInstances.push(chartInstance);
            });

            if (panelSamples.length > 1) {
                wrappers.forEach((wrapper, sIdx) => {
                    const header = document.createElement("div");
                    header.className = "flex justify-end p-2 border-b border-zinc-800 bg-zinc-900/30";

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
                    bgbtn.className = "hover:cursor-pointer text-sm tracking-wider px-2 py-1 rounded text-zinc-400 hover:text-white transition-all flex items-center gap-1.5";
                    bgbtn.innerHTML = `
                        Background color
                        <svg class="w-2.5 h-2.5 transition-transform duration-200" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"></path>
                        </svg>
                    `;
                    const colormap = document.createElement("div");
                    colormap.className = "hidden absolute top-full right-0 mt-1 min-w-[120px] w-max max-w-[200px] bg-zinc-900 border border-zinc-800 rounded shadow-2xl py-1 z-30";
                    colormap.innerHTML = `
                        <img style="margin-right:2px;" src="img_colormap.gif" alt="colormap">
<!--                        usemap="#colormap"-->
                    `;

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
                    }

                    btnWrapper.appendChild(btn);
                    btnWrapper.appendChild(menu);
                    bgbtnWrapper.appendChild(bgbtn);
                    bgbtnWrapper.appendChild(colormap);

                    container.appendChild(btnWrapper);
                    container.appendChild(bgbtnWrapper);
                    header.appendChild(container);
                    wrapper.prepend(header);
                });
            }

        });
    }
    catch (error) {
        if (error.name !== 'AbortError') {
            console.log(error);
        }
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
    });

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
