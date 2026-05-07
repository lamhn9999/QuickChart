// @ts-check
/**
 * @file Number formatting + the histogram canvas drawing primitive used by sparklines.
 */

/**
 * Compact-number formatter — 1.5k, 1.2M, 1.0B, or up to 2 decimals otherwise.
 * @param {number} n
 */
export function fmtNum(n) {
  const a = Math.abs(n);
  if (a >= 1e9) return (n / 1e9).toFixed(1) + 'B';
  if (a >= 1e6) return (n / 1e6).toFixed(1) + 'M';
  if (a >= 1e3) return (n / 1e3).toFixed(1) + 'k';
  return n.toLocaleString(undefined, { maximumFractionDigits: 2 });
}

/**
 * Render a 14-bin histogram into a 240×28 canvas. Returns `[min, max]` for caller display.
 * If all values equal, draws a single band.
 *
 * @param {HTMLCanvasElement} canvas
 * @param {number[]} nums
 * @returns {[number, number]}
 */
export function drawHistogram(canvas, nums) {
  const BINS = 14;
  const W = 240;
  const H = 28;
  canvas.width = W;
  canvas.height = H;
  const ctx = /** @type {CanvasRenderingContext2D} */ (canvas.getContext('2d'));
  ctx.clearRect(0, 0, W, H);

  const min = Math.min(...nums);
  const max = Math.max(...nums);
  if (min === max) {
    ctx.fillStyle = 'rgba(233,150,122,0.6)';
    ctx.fillRect(W * 0.1, 2, W * 0.8, H - 4);
    return [min, max];
  }

  const binSize = (max - min) / BINS;
  const counts = new Array(BINS).fill(0);
  nums.forEach((n) => {
    counts[Math.min(Math.floor((n - min) / binSize), BINS - 1)]++;
  });
  const maxC = Math.max(...counts);
  const gap = 1;
  const barW = (W - gap * (BINS + 1)) / BINS;

  counts.forEach((c, i) => {
    const h = (c / maxC) * (H - 4);
    ctx.fillStyle = 'rgba(233,150,122,0.65)';
    ctx.fillRect(
      Math.round(gap + i * (barW + gap)),
      Math.round(H - h - 2),
      Math.round(barW),
      Math.round(h)
    );
  });
  return [min, max];
}
