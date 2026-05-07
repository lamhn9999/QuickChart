// @ts-check
/**
 * @file Background-colour helpers for chart cards.
 *
 * `getContrastTextColor(color)` resolves any CSS colour string by handing it to the browser
 * (via a hidden `<div>`) and computing a black/white contrast based on YIQ brightness.
 * `reformatColor(color, canvas)` paints the card background and re-themes the chart.
 */

import { applyChartTheme } from './theme.js';

/**
 * @param {string} color any CSS colour string
 * @returns {'black' | 'white'}
 */
export function getContrastTextColor(color) {
  const temp = document.createElement('div');
  temp.style.color = color;
  document.body.appendChild(temp);

  const rgb = getComputedStyle(temp).color;
  document.body.removeChild(temp);

  const match = rgb.match(/\d+/g);
  if (!match) return 'black';
  const [r, g, b] = match.map(Number);
  const brightness = (r * 299 + g * 587 + b * 114) / 1000;

  return brightness > 128 ? 'black' : 'white';
}

/**
 * Repaint the chart-card background and reapply the matching theme to the chart inside.
 * @param {string} color
 * @param {HTMLCanvasElement} canvas
 */
export function reformatColor(color, canvas) {
  canvas.style.backgroundColor = color;

  const textColor = getContrastTextColor(color);
  // @ts-ignore — Chart is a runtime global from chart.umd.min.js
  const chart = Chart.getChart(canvas);
  if (!chart) return;
  const isDark = textColor === 'white';
  applyChartTheme(chart, isDark ? 'dark' : 'light');
}
