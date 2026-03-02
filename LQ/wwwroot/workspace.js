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

    if (listIndexes.size === 0) {
        loader.hidden = true;
        selPlot.hidden = false;
        noPlot.hidden = true;
        return;
    }
    selPlot.hidden = true;
    noPlot.hidden = true;
    loader.hidden = false;

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

        chartInstances.forEach(c => c.destroy());
        chartInstances.length = 0;
        loader.hidden = true;

        result.forEach(sample => {
            const ctx = document.createElement("canvas");
            panel.appendChild(ctx);

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

        if (chartInstances.length === 0) {
            console.log("No charts found.");
            selPlot.hidden = true;
            noPlot.hidden = false;
        }
    }
    catch (error) {
        console.log(error);
    }
}

function fieldClicked(fieldindex) {
    const panel = document.getElementById("plotPanel");
    if (controller) {
        controller.abort("New fetch!");
    }
    panel.querySelectorAll("canvas").forEach(c => c.remove());
    if (listIndexes.has(fieldindex)) listIndexes.delete(fieldindex);
    else listIndexes.add(fieldindex);
    console.log(listIndexes);
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
