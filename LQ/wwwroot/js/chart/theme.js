// @ts-check
/**
 * @file Chart.js theme tokens + axis styles + theme switcher.
 *
 * Cards have a dark background (legend/title need light text), the zoom overlay has a light
 * background (need dark text). `_applyChartTheme(chart, 'dark' | 'light')` flips legend, title,
 * tick, grid, angleLine, and pointLabel colours in one shot.
 */

export const DARK_TEXT = 'rgba(255,255,255,0.65)';
export const DARK_TITLE = 'rgba(255,255,255,0.92)';
export const DARK_GRID = 'rgba(255,255,255,0.06)';
export const LIGHT_TEXT = '#52525b';
export const LIGHT_TITLE = '#18181b';
export const LIGHT_GRID = 'rgba(0,0,0,0.08)';

/** Default tick + grid styling for x/y axes. Used by every numeric/categorical scale. */
export const axisStyle = {
  ticks: { color: LIGHT_TEXT, font: { size: 11 } },
  grid: { color: LIGHT_GRID },
};

/** Same as axisStyle but `stacked: true`. */
export const stackedAxis = {
  ...axisStyle,
  stacked: true,
};

/**
 * Switch a chart's text / grid colours to match its container background.
 * @param {any} chart    Chart.js instance
 * @param {'dark' | 'light'} theme  'dark' for card containers, 'light' for the zoom overlay
 */
export function applyChartTheme(chart, theme) {
  const isDark = theme === 'dark';
  const text = isDark ? DARK_TEXT : LIGHT_TEXT;
  const title = isDark ? DARK_TITLE : LIGHT_TITLE;
  const grid = isDark ? DARK_GRID : LIGHT_GRID;

  if (chart.options.plugins?.legend?.labels) chart.options.plugins.legend.labels.color = text;
  if (chart.options.plugins?.title) chart.options.plugins.title.color = title;

  ['x', 'y', 'z', 'r'].forEach((axis) => {
    const s = chart.options.scales?.[axis];
    if (!s) return;
    if (s.ticks) s.ticks.color = text;
    if (s.grid) s.grid.color = grid;
    if (s.angleLines) s.angleLines.color = grid;
    if (s.pointLabels) s.pointLabels.color = text;
  });

  // Matrix: border colour as text
  if (chart.config.type === 'matrix') {
    chart.data.datasets.forEach((/** @type {any} */ ds) => {
      ds.borderColor = text;
    });
  }

  chart.update('none');
}
