// @ts-check
/**
 * @file `#doc-canvas` lookup, sizing helpers, page-size presets, and HTML export.
 *
 * - `docCanvas()` / `getNextCardPosition()` / `updateCanvasSize()` are the small primitives
 *   used by drag/resize handlers and createTextCard.
 * - `setCanvasSize` / `setCanvasBg` are user-facing options-panel actions.
 * - `exportCanvas` serialises every chart card + text card into a self-contained HTML file
 *   with a CDN-loaded Chart.js, then triggers a download.
 */

/** @returns {HTMLElement | null} */
export function docCanvas() {
  return /** @type {HTMLElement | null} */ (document.getElementById('doc-canvas'));
}

/**
 * Cascade-stagger position for a brand-new card. Cycles every 10 cards so they don't drift
 * off-canvas on prolonged use.
 */
export function getNextCardPosition() {
  const canvas = docCanvas();
  if (!canvas) return { x: 20, y: 20 };
  const n = canvas.querySelectorAll(':scope > .canvas-card').length;
  const step = n % 10;
  return { x: 20 + step * 28, y: 20 + step * 28 };
}

/**
 * Recompute `min-height` / `min-width` of the doc canvas so it always grows to enclose every
 * card. Called after any drag/resize/insert.
 */
export function updateCanvasSize() {
  const canvas = docCanvas();
  if (!canvas) return;
  let maxH = 600;
  let maxW = 800;
  canvas.querySelectorAll(':scope > .canvas-card').forEach((c) => {
    const el = /** @type {HTMLElement} */ (c);
    maxH = Math.max(maxH, el.offsetTop + el.offsetHeight);
    maxW = Math.max(maxW, el.offsetLeft + el.offsetWidth);
  });
  canvas.style.minHeight = `${maxH + 80}px`;
  canvas.style.minWidth = `${maxW + 80}px`;
}

// ── HTML export ──────────────────────────────────────────────────────────────

/**
 * Walk every `.canvas-card` and `.text-card`, serialise each chart's config (functions
 * stripped) into an inline `<script>`, and trigger a download of a self-contained HTML.
 */
export function exportCanvas() {
  const canvas = docCanvas();
  if (!canvas) return;
  const cards = Array.from(canvas.querySelectorAll(':scope > .canvas-card'));
  if (cards.length === 0) {
    alert('No charts to export.');
    return;
  }

  let maxR = 80;
  let maxB = 80;
  cards.forEach((c) => {
    const el = /** @type {HTMLElement} */ (c);
    maxR = Math.max(maxR, el.offsetLeft + el.offsetWidth + 40);
    maxB = Math.max(maxB, el.offsetTop + el.offsetHeight + 40);
  });

  /** @type {string[]} */
  const scriptLines = [];
  let chartIdx = 0;
  let cardHTMLs = '';

  cards.forEach((card) => {
    const el = /** @type {HTMLElement} */ (card);
    const cvs = /** @type {HTMLCanvasElement | null} */ (card.querySelector('canvas'));
    const bg = cvs?.style.backgroundColor || '#ffffff';
    const cardStyle =
      `position:absolute;left:${el.offsetLeft}px;top:${el.offsetTop}px;` +
      `width:${el.offsetWidth}px;background:${bg};border-radius:14px;` +
      `box-shadow:0 1px 3px rgba(0,0,0,.08),0 4px 16px rgba(0,0,0,.06);overflow:hidden`;

    let panelsHTML = '';
    card
      .querySelectorAll('.canvas-card-panels > .flex.flex-col:not(.drag-handle)')
      .forEach((panel) => {
        let wrappersHTML = '';
        panel.querySelectorAll('[data-chart-title]').forEach((wrapper) => {
          const w = /** @type {HTMLElement} */ (wrapper);
          if (w.style.display === 'none') return;
          const cv = /** @type {HTMLCanvasElement | null} */ (w.querySelector('canvas'));
          if (!cv) return;
          // @ts-ignore — Chart is a runtime global
          const inst = Chart.getChart(cv);
          if (!inst) return;
          const id = `ec${chartIdx++}`;
          const W = cv.offsetWidth || 340;
          const H = cv.offsetHeight || 220;
          wrappersHTML += `<canvas id="${id}" width="${W}" height="${H}" style="display:block;width:100%;height:${H}px"></canvas>`;
          try {
            const cfg = JSON.parse(
              JSON.stringify(inst.config, (_k, v) => (typeof v === 'function' ? undefined : v))
            );
            if (cfg.options) {
              cfg.options.animation = false;
              cfg.options.responsive = false;
              cfg.options.maintainAspectRatio = false;
            }
            scriptLines.push(
              `(function(){var c=${JSON.stringify(cfg)};new Chart(document.getElementById('${id}'),c);})();`
            );
          } catch {
            /* skip broken chart */
          }
        });
        if (wrappersHTML) panelsHTML += `<div style="flex:1;min-width:0">${wrappersHTML}</div>`;
      });

    if (panelsHTML)
      cardHTMLs += `<div style="${cardStyle}"><div style="display:flex;flex-direction:row;height:100%">${panelsHTML}</div></div>\n`;
  });

  // Text cards
  canvas.querySelectorAll(':scope > .text-card').forEach((card) => {
    const el = /** @type {HTMLElement} */ (card);
    const body = card.querySelector('.tc-content');
    if (!body) return;
    const textStyle =
      `position:absolute;left:${el.offsetLeft}px;top:${el.offsetTop}px;` +
      `width:${el.offsetWidth}px;min-height:${el.offsetHeight}px;` +
      `font-family:system-ui,sans-serif;font-size:14px;line-height:1.65;color:#27272A;padding:2px 4px`;
    cardHTMLs += `<div style="${textStyle}">${body.innerHTML}</div>\n`;
  });

  const canvasBg = canvas.style.background || '#ffffff';
  const textBlockCSS =
    `[data-block-type="h1"]{font-size:2em;font-weight:800;line-height:1.18;color:#18181B}` +
    `[data-block-type="h2"]{font-size:1.45em;font-weight:700;line-height:1.28;color:#18181B}` +
    `[data-block-type="h3"]{font-size:1.15em;font-weight:600;line-height:1.35;color:#27272A}`;
  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>Dashboard</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.5.1/dist/chart.umd.min.js"><\/script>
  <script src="https://cdn.jsdelivr.net/npm/chartjs-chart-matrix@1.3.0"><\/script>
  <style>*{margin:0;padding:0;box-sizing:border-box}body{background:#EFEFEF;padding:48px;font-family:system-ui,sans-serif}#_cv{position:relative;width:${maxR}px;min-height:${maxB}px;background:${canvasBg};border-radius:8px;margin:0 auto;box-shadow:0 8px 48px rgba(0,0,0,.5)}${textBlockCSS}</style>
</head>
<body>
  <div id="_cv">
${cardHTMLs}
  </div>
  <script>
${scriptLines.join('\n')}
  <\/script>
</body>
</html>`;

  const blob = new Blob([html], { type: 'text/html' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = 'dashboard.html';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(a.href), 2000);
}

// ── Canvas size presets + background colour ──────────────────────────────────

const CANVAS_SIZES = {
  wide: { w: 1280, h: 720 },
  a4: { w: 794, h: 1123 },
};

/**
 * Apply one of the preset page sizes (wide / a4) to the doc canvas + toggle the active class
 * on the matching toolbar button.
 * @param {keyof typeof CANVAS_SIZES} key
 */
export function setCanvasSize(key) {
  const { w, h } = CANVAS_SIZES[key] ?? CANVAS_SIZES.wide;
  const canvas = docCanvas();
  if (!canvas) return;
  canvas.style.width = `${w}px`;
  canvas.style.height = `${h}px`;
  canvas.style.minWidth = `${w}px`;
  canvas.style.minHeight = `${h}px`;
  document.querySelectorAll('.rp-size-btn').forEach((b) => {
    b.classList.toggle('active', b.id === `btn-size-${key}`);
  });
}

/**
 * Set the doc canvas background colour and update the toolbar swatch indicator.
 * @param {string} hex `#rgb` or `#rrggbb`
 */
export function setCanvasBg(hex) {
  if (!/^#[0-9a-fA-F]{3,6}$/.test(hex)) return;
  const canvas = docCanvas();
  if (!canvas) return;
  canvas.style.background = hex;
  const swatch = document.getElementById('canvas-bg-swatch');
  if (swatch) swatch.style.background = hex;
}
