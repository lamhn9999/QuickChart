// @ts-check
/**
 * @file Navigator mini-chart for the zoom overlay.
 *
 * On openZoom() of a category chart, `navInit(mainChart)` builds a small line/area chart in
 * the navigator track DOM (`#zoomNavigator`, `#navigatorCanvas`, `#navWindow`, `#navTrack`,
 * `#navMaskLeft`, `#navMaskRight`, `#navHandleLeft`, `#navHandleRight`) and wires drag/click
 * to drive the main chart's `x.min`/`x.max` so the user can pan/zoom horizontally.
 *
 * Skipped for non-labelled charts (matrix, scatter without labels) — the navigator container
 * stays hidden in that case.
 *
 * `navDestroy(mainChart)` removes everything and clears the x-axis clamps so the chart goes
 * back to full-range when the user closes zoom.
 */

/** @type {any | null} Chart.js instance for the mini overview */
let _navChartInst = null;
/** @type {{ type: 'left' | 'right' | 'move', startX: number, startLeft: number, startWidth: number } | null} */
let _navDragState = null;

/** Normalise a dataset's data array so the navigator always gets plain numbers. */
function navNormalise(/** @type {any[]} */ data) {
  return data.map((d) =>
    d === null || d === undefined
      ? null
      : typeof d === 'object' && 'y' in d
        ? d.y
        : typeof d === 'number'
          ? d
          : null
  );
}

/** Returns the pixel width of the navigator track. */
function navTrackWidth() {
  const t = document.getElementById('navTrack');
  return t ? t.getBoundingClientRect().width : 0;
}

/**
 * Apply a new selection window (both % values 0-100) and update the main chart's
 * x.min / x.max accordingly.
 */
function navUpdate(
  /** @type {number} */ leftPct,
  /** @type {number} */ widthPct,
  /** @type {any} */ mainChart
) {
  // Clamp
  widthPct = Math.max(4, widthPct);
  leftPct = Math.max(0, leftPct);
  if (leftPct + widthPct > 100) {
    if (_navDragState?.type === 'left') leftPct = 100 - widthPct;
    else widthPct = 100 - leftPct;
  }
  leftPct = Math.max(0, leftPct);

  // Reposition DOM elements
  const win = /** @type {HTMLElement | null} */ (document.getElementById('navWindow'));
  const maskL = /** @type {HTMLElement | null} */ (document.getElementById('navMaskLeft'));
  const maskR = /** @type {HTMLElement | null} */ (document.getElementById('navMaskRight'));
  if (!win || !maskL || !maskR) return;
  win.style.left = `${leftPct}%`;
  win.style.width = `${widthPct}%`;
  maskL.style.width = `${leftPct}%`;
  maskR.style.width = `${100 - leftPct - widthPct}%`;

  // Drive the main chart's x-axis
  const labels = mainChart?.data?.labels;
  if (!labels?.length) return;

  const n = labels.length;
  const lo = Math.round((leftPct / 100) * n);
  const hi = Math.min(n - 1, Math.round(((leftPct + widthPct) / 100) * n) - 1);
  mainChart.options.scales.x.min = lo;
  mainChart.options.scales.x.max = Math.max(lo, hi);
  mainChart.update('none');
}

/**
 * Wire up mouse drag events for the navigator window and handles.
 * Must be called once per openZoom(); cleaned up by navDestroy().
 */
function navSetupDrag(/** @type {any} */ mainChart) {
  const win = /** @type {HTMLElement | null} */ (document.getElementById('navWindow'));
  const leftHandle = /** @type {HTMLElement | null} */ (document.getElementById('navHandleLeft'));
  const rightHandle = /** @type {HTMLElement | null} */ (document.getElementById('navHandleRight'));
  const track = /** @type {HTMLElement | null} */ (document.getElementById('navTrack'));
  if (!win || !leftHandle || !rightHandle || !track) return;
  // Capture as non-null locals so closures below don't re-trigger strict-null checks.
  const winEl = win;

  function curPos() {
    return {
      leftPct: parseFloat(winEl.style.left) || 0,
      widthPct: parseFloat(winEl.style.width) || 100,
    };
  }

  function startDrag(/** @type {'left'|'right'|'move'} */ type, /** @type {any} */ e) {
    e.stopPropagation();
    e.preventDefault();
    const clientX = e.touches ? e.touches[0].clientX : e.clientX;
    const { leftPct, widthPct } = curPos();
    _navDragState = { type, startX: clientX, startLeft: leftPct, startWidth: widthPct };
    document.body.style.setProperty('user-select', 'none', 'important');
  }

  const onMove = (/** @type {any} */ e) => {
    if (!_navDragState) return;
    const clientX = e.touches ? e.touches[0].clientX : e.clientX;
    const dx = ((clientX - _navDragState.startX) / navTrackWidth()) * 100;
    const { startLeft, startWidth, type } = _navDragState;

    if (type === 'move') navUpdate(startLeft + dx, startWidth, mainChart);
    else if (type === 'left') navUpdate(startLeft + dx, startWidth - dx, mainChart);
    else if (type === 'right') navUpdate(startLeft, startWidth + dx, mainChart);
  };

  const onUp = () => {
    _navDragState = null;
    document.body.style.removeProperty('user-select');
  };

  // Click on the masked areas to jump the window centre
  const onTrackClick = (/** @type {any} */ e) => {
    if (_navDragState) return;
    const id = e.target?.id;
    if (id !== 'navMaskLeft' && id !== 'navMaskRight' && id !== 'navTrack') return;
    const rect = track.getBoundingClientRect();
    const clickPct = ((e.clientX - rect.left) / rect.width) * 100;
    const { widthPct } = curPos();
    navUpdate(clickPct - widthPct / 2, widthPct, mainChart);
  };

  leftHandle.addEventListener('mousedown', (e) => startDrag('left', e));
  rightHandle.addEventListener('mousedown', (e) => startDrag('right', e));
  win.addEventListener('mousedown', (e) => {
    const t = /** @type {Node} */ (e.target);
    if (t === leftHandle || leftHandle.contains(t)) return;
    if (t === rightHandle || rightHandle.contains(t)) return;
    startDrag('move', e);
  });
  track.addEventListener('click', onTrackClick);

  document.addEventListener('mousemove', onMove);
  document.addEventListener('mouseup', onUp);

  // Store cleanup so navDestroy can remove global listeners
  _navChartInst._cleanupNav = () => {
    document.removeEventListener('mousemove', onMove);
    document.removeEventListener('mouseup', onUp);
    track.removeEventListener('click', onTrackClick);
  };
}

/**
 * Create the navigator mini-chart and show the navigator bar.
 * Call this from openZoom() after the main chart has been resized.
 * @param {any} mainChart
 */
export function navInit(mainChart) {
  const container = document.getElementById('zoomNavigator');
  const canvas = document.getElementById('navigatorCanvas');
  const labels = mainChart?.data?.labels;

  // Navigator only makes sense for labelled (category) charts
  if (!container || !canvas || !labels?.length || mainChart.config.type === 'matrix') {
    container?.classList.add('hidden');
    return;
  }

  if (_navChartInst) {
    _navChartInst._cleanupNav?.();
    _navChartInst.destroy();
    _navChartInst = null;
  }

  // Build simplified datasets — always line/area regardless of original type
  const navDatasets = mainChart.data.datasets.map((/** @type {any} */ ds) => ({
    type: 'line',
    label: ds.label,
    data: navNormalise(Array.isArray(ds.data) ? ds.data : []),
    borderColor: 'rgba(233,150,122,0.75)',
    backgroundColor: 'rgba(233,150,122,0.12)',
    borderWidth: 1.5,
    pointRadius: 0,
    fill: true,
    tension: 0.35,
  }));

  // @ts-ignore — Chart is a runtime global
  _navChartInst = new Chart(canvas, {
    type: 'line',
    data: { labels, datasets: navDatasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: {
        legend: { display: false },
        tooltip: { enabled: false },
      },
      scales: {
        x: { ticks: { display: false }, grid: { display: false }, border: { display: false } },
        y: { ticks: { display: false }, grid: { display: false }, border: { display: false } },
      },
    },
  });

  // Full-range selection on open
  navUpdate(0, 100, mainChart);
  container.classList.remove('hidden');
  navSetupDrag(mainChart);
}

/**
 * Tear down the navigator and reset the main chart's x-axis constraints.
 * @param {any} mainChart
 */
export function navDestroy(mainChart) {
  if (_navChartInst) {
    _navChartInst._cleanupNav?.();
    _navChartInst.destroy();
    _navChartInst = null;
  }
  _navDragState = null;
  document.getElementById('zoomNavigator')?.classList.add('hidden');

  // Remove x-axis clamps so the chart renders fully when returned to the grid
  if (mainChart?.options?.scales?.x) {
    delete mainChart.options.scales.x.min;
    delete mainChart.options.scales.x.max;
  }
}
