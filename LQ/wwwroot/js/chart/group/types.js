// @ts-check
/**
 * @file Chart-type registry — the `chartArray` index→engine-type-name map plus the
 * 13 SVG mini-icons used by the "Turn into" deck and zoom-panel.
 *
 * Order in `chartArray` mirrors the C# `ChartType` enum (server sends an integer index;
 * client looks up the name to construct Chart.js datasets and to label icons).
 */

/**
 * Chart.js type name per `ChartType` enum index. Used by build-panel.js to choose the
 * right Chart.js controller, and by the zoom panel to label series.
 *
 * Note: 7 ('mixed'), 9 ('mixedBar'), 11 ('line' / area), 12 ('radar') are special-cased
 * in build-panel.js because Chart.js needs additional configuration for them.
 */
export const chartArray = [
  'pie',
  'bar',
  'line',
  'bar',
  'scatter',
  'polarArea',
  'bar',
  'mixed',
  'matrix',
  'mixedBar',
  'bubble',
  'line',
  'radar',
];

// SVG mini-icons (16×16 viewBox, currentColor). One per line for legibility.
// prettier-ignore
const _ICONS = {
    pie:      `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path d="M8 8L8 1.5A6.5 6.5 0 0 1 11.8 13.3Z" opacity=".9"/><path d="M8 8L11.8 13.3A6.5 6.5 0 0 1 1.5 8Z" opacity=".6"/><path d="M8 8L1.5 8A6.5 6.5 0 0 1 8 1.5Z" opacity=".35"/><circle cx="8" cy="8" r="6.5" fill="none" stroke="currentColor" stroke-width=".8" opacity=".3"/></svg>`,
    bar:      `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><rect x="1.5" y="8"   width="3" height="6"    rx=".6" opacity=".4"/><rect x="6.5" y="4.5" width="3" height="9.5"  rx=".6" opacity=".65"/><rect x="11.5" y="1.5" width="3" height="12.5" rx=".6" opacity=".9"/></svg>`,
    line:     `<svg viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg"><path d="M1.5 12.5C3.5 10 4 8.5 5.5 8C7.5 6.5 8.5 5.8 10 5.5C11.5 5.2 12.5 3.5 14 2.5L14 14L1.5 14Z" fill="currentColor" opacity=".12"/><path d="M1.5 12.5C3.5 10 4 8.5 5.5 8C7.5 6.5 8.5 5.8 10 5.5C11.5 5.2 12.5 3.5 14 2.5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/><circle cx="1.5" cy="12.5" r="1.3" fill="currentColor"/><circle cx="5.5" cy="8" r="1.3" fill="currentColor"/><circle cx="10" cy="5.5" r="1.3" fill="currentColor"/><circle cx="14" cy="2.5" r="1.3" fill="currentColor"/></svg>`,
    histogram:`<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><rect x=".75"  y="11" width="2.5" height="3"  rx=".3" opacity=".4"/><rect x="3.75" y="7"  width="2.5" height="7"  rx=".3" opacity=".65"/><rect x="6.75" y="3"  width="2.5" height="11" rx=".3" opacity=".9"/><rect x="9.75" y="7"  width="2.5" height="7"  rx=".3" opacity=".65"/><rect x="12.75" y="11" width="2.5" height="3" rx=".3" opacity=".4"/></svg>`,
    scatter:  `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><circle cx="2"    cy="12.5" r="1.4" opacity=".5"/><circle cx="4.5"  cy="9"    r="1.4" opacity=".75"/><circle cx="6.5"  cy="11"   r="1.4" opacity=".55"/><circle cx="9"    cy="6.5"  r="1.4" opacity=".85"/><circle cx="11.5" cy="8"    r="1.4" opacity=".65"/><circle cx="13.5" cy="4"    r="1.4" opacity=".9"/></svg>`,
    polar:    `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><circle cx="8" cy="8" r="6.5" fill="none" stroke="currentColor" stroke-width=".8" opacity=".2"/><path d="M8 8L8 1.5A6.5 6.5 0 0 1 14.2 6Z" opacity=".95"/><path d="M8 8L14.2 6A6.5 6.5 0 0 1 11.8 13.3Z" opacity=".28"/><path d="M8 8L11.8 13.3A6.5 6.5 0 0 1 4.2 13.3Z" opacity=".72"/><path d="M8 8L4.2 13.3A6.5 6.5 0 0 1 1.8 6Z" opacity=".18"/><path d="M8 8L1.8 6A6.5 6.5 0 0 1 8 1.5Z" opacity=".52"/></svg>`,
    sidebar:  `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><rect x="2" y="1.5" width=".8" height="13" rx=".4" opacity=".22"/><rect x="3" y="2.5"  width="11.5" height="3" rx=".6" opacity=".9"/><rect x="3" y="6.5"  width="8.5"  height="3" rx=".6" opacity=".6"/><rect x="3" y="10.5" width="5.5"  height="3" rx=".6" opacity=".38"/></svg>`,
    mixed:    `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><rect x="1.5" y="10"  width="3" height="4"   rx=".4" opacity=".38"/><rect x="6.5" y="8.5" width="3" height="5.5" rx=".4" opacity=".38"/><rect x="11.5" y="7"  width="3" height="7"   rx=".4" opacity=".38"/><rect x="1.5" y="7"   width="3" height="3"   rx=".4" opacity=".82"/><rect x="6.5" y="5"   width="3" height="3.5" rx=".4" opacity=".82"/><rect x="11.5" y="3"  width="3" height="4"   rx=".4" opacity=".82"/><polyline points="3,7 8,5 13,3" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>`,
    matrix:   `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><rect x="1.5" y="1.5" width="3.5" height="3.5" rx=".4" opacity=".9"/><rect x="6.2" y="1.5" width="3.5" height="3.5" rx=".4" opacity=".45"/><rect x="11"  y="1.5" width="3.5" height="3.5" rx=".4" opacity=".12"/><rect x="1.5" y="6.2" width="3.5" height="3.5" rx=".4" opacity=".5"/><rect x="6.2" y="6.2" width="3.5" height="3.5" rx=".4" opacity=".82"/><rect x="11"  y="6.2" width="3.5" height="3.5" rx=".4" opacity=".35"/><rect x="1.5" y="11"  width="3.5" height="3.5" rx=".4" opacity=".18"/><rect x="6.2" y="11"  width="3.5" height="3.5" rx=".4" opacity=".6"/><rect x="11"  y="11"  width="3.5" height="3.5" rx=".4" opacity=".95"/></svg>`,
    mixedbar: `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><rect x="1.5" y="8"   width="3" height="6"   rx=".5" opacity=".65"/><rect x="6.5" y="5"   width="3" height="9"   rx=".5" opacity=".65"/><rect x="11.5" y="7"  width="3" height="7"   rx=".5" opacity=".65"/><circle cx="3" cy="4" r="1.2" opacity=".9"/><circle cx="8" cy="2" r="1.2" opacity=".9"/><circle cx="13" cy="5.5" r="1.2" opacity=".9"/><polyline points="3,4 8,2 13,5.5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>`,
    bubble:   `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><circle cx="3.5"  cy="12"   r="1.8" opacity=".55"/><circle cx="8.5"  cy="6.5"  r="3.5" opacity=".7"/><circle cx="13.5" cy="11"   r="2.5" opacity=".4"/></svg>`,
    area:     `<svg viewBox="0 0 16 16" xmlns="http://www.w3.org/2000/svg"><path d="M1.5 12.5C3.5 10 4 8.5 5.5 8C7.5 6.5 8.5 5.8 10 5.5C11.5 5.2 12.5 3.5 14 2.5L14 14L1.5 14Z" fill="currentColor" opacity=".55"/><path d="M1.5 12.5C3.5 10 4 8.5 5.5 8C7.5 6.5 8.5 5.8 10 5.5C11.5 5.2 12.5 3.5 14 2.5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>`,
    radar:    `<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><line x1="8" y1="8" x2="8" y2="2" stroke="currentColor" stroke-width=".7" opacity=".25"/><line x1="8" y1="8" x2="13.7" y2="6.1" stroke="currentColor" stroke-width=".7" opacity=".25"/><line x1="8" y1="8" x2="11.5" y2="12.9" stroke="currentColor" stroke-width=".7" opacity=".25"/><line x1="8" y1="8" x2="4.5" y2="12.9" stroke="currentColor" stroke-width=".7" opacity=".25"/><line x1="8" y1="8" x2="2.3" y2="6.1" stroke="currentColor" stroke-width=".7" opacity=".25"/><polygon points="8,2 13.7,6.1 11.5,12.9 4.5,12.9 2.3,6.1" fill="none" stroke="currentColor" stroke-width=".7" opacity=".18"/><polygon points="8,4 11.8,6.8 10.4,11.2 5.6,11.2 4.2,6.8" opacity=".25"/><polygon points="8,4 11.8,6.8 10.4,11.2 5.6,11.2 4.2,6.8" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round" opacity=".9"/></svg>`,
};

// Row 0: Pie · Bar · Line · Histogram   (matches enum 0–3)
// Row 1: Scatter · Polar · Side Bar · Mixed  (enum 4–7)
// Row 2: Matrix · Mixed Bar · Bubble · Area (enum 8–11)
// Row 3: Radar  (enum 12)
export const CHART_META = [
  { label: 'Pie', svg: _ICONS.pie },
  { label: 'Bar', svg: _ICONS.bar },
  { label: 'Line', svg: _ICONS.line },
  { label: 'Histogram', svg: _ICONS.histogram },
  { label: 'Scatter', svg: _ICONS.scatter },
  { label: 'Polar', svg: _ICONS.polar },
  { label: 'Side Bar', svg: _ICONS.sidebar },
  { label: 'Mixed', svg: _ICONS.mixed },
  { label: 'Matrix', svg: _ICONS.matrix },
  { label: 'Mixed Bar', svg: _ICONS.mixedbar },
  { label: 'Bubble', svg: _ICONS.bubble },
  { label: 'Area', svg: _ICONS.area },
  { label: 'Radar', svg: _ICONS.radar },
];
