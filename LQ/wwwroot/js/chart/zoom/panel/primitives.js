// @ts-check
/**
 * @file Tiny DOM primitives shared across every section of the zoom-panel.
 *
 * - `css(el, s)`  — Apply a `style.cssText` block in one shot. Used everywhere because the
 *   panel is dense imperative DOM. Future polish: replace with `h(tag, {style:{…}})` from
 *   core/dom.js, but for now this preserves the existing chart.js code shape verbatim.
 * - `sectionLabel(text)` — small all-caps section header.
 * - `accordion(title, buildFn, startOpen)` — collapsible card; `buildFn(body)` populates
 *   the body lazily. Returns the wrapper element.
 * - `mkInput(placeholder, onChange)` — small light-themed text input.
 * - `mkSelect()` — small light-themed select; caller fills options.
 */

/**
 * @template {HTMLElement} T
 * @param {T} el
 * @param {string} s
 * @returns {T}
 */
export function css(el, s) {
  el.style.cssText = s;
  return el;
}

/**
 * @param {string} text
 */
export function sectionLabel(text) {
  const d = document.createElement('div');
  css(
    d,
    'font-size:8.5px;font-weight:700;letter-spacing:0.1em;text-transform:uppercase;color:#a1a1aa;padding:12px 4px 5px;margin-top:2px'
  );
  d.textContent = text;
  return d;
}

/**
 * @param {string} title
 * @param {(body: HTMLElement) => void} buildFn
 * @param {boolean} [startOpen]
 */
export function accordion(title, buildFn, startOpen = false) {
  const aWrap = document.createElement('div');
  css(aWrap, 'border:1px solid #e4e4e7;border-radius:8px;overflow:hidden;background:#fff');
  let _open = startOpen;

  const sumBtn = document.createElement('button');
  css(
    sumBtn,
    `width:100%;padding:8px 10px;display:flex;align-items:center;justify-content:space-between;background:${_open ? '#f4f4f5' : '#fff'};border:none;cursor:pointer;color:${_open ? '#18181b' : '#71717a'};font-size:10.5px;font-weight:500;text-align:left;font-family:inherit;transition:background 0.1s,color 0.1s`
  );
  const chevron = document.createElement('span');
  css(
    chevron,
    `display:inline-flex;flex-shrink:0;transition:transform 0.15s;color:#d4d4d8;transform:${startOpen ? 'rotate(180deg)' : ''}`
  );
  chevron.innerHTML = `<svg width="8" height="8" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><path d="M6 9l6 6 6-6"/></svg>`;
  sumBtn.append(document.createTextNode(title), chevron);
  sumBtn.addEventListener('mouseenter', () => {
    sumBtn.style.background = '#f4f4f5';
    sumBtn.style.color = '#18181b';
  });
  sumBtn.addEventListener('mouseleave', () => {
    sumBtn.style.background = _open ? '#f4f4f5' : '#fff';
    sumBtn.style.color = _open ? '#18181b' : '#71717a';
  });

  const body = document.createElement('div');
  css(
    body,
    `padding:8px 10px 10px;border-top:1px solid #f4f4f5;display:${startOpen ? 'flex' : 'none'};flex-direction:column;gap:6px`
  );
  buildFn(body);

  sumBtn.addEventListener('click', () => {
    _open = !_open;
    body.style.display = _open ? 'flex' : 'none';
    chevron.style.transform = _open ? 'rotate(180deg)' : '';
    sumBtn.style.color = _open ? '#18181b' : '#71717a';
    sumBtn.style.background = _open ? '#f4f4f5' : '#fff';
  });
  aWrap.append(sumBtn, body);
  return aWrap;
}

/**
 * @param {string} placeholder
 * @param {(e: Event) => void} onChange
 */
export function mkInput(placeholder, onChange) {
  const inp = document.createElement('input');
  inp.type = 'text';
  inp.placeholder = placeholder;
  css(
    inp,
    'flex:1;min-width:0;background:#fff;border:1px solid #e4e4e7;border-radius:5px;padding:4px 7px;font-size:10px;color:#3f3f46;outline:none;transition:border-color 0.1s;font-family:inherit'
  );
  inp.addEventListener('focus', () => (inp.style.borderColor = 'rgba(233,150,122,0.6)'));
  inp.addEventListener('blur', () => (inp.style.borderColor = '#e4e4e7'));
  inp.addEventListener('input', onChange);
  return inp;
}

export function mkSelect() {
  const s = document.createElement('select');
  css(
    s,
    'flex:1;min-width:0;background:#fff;border:1px solid #e4e4e7;border-radius:5px;padding:4px 6px;font-size:10px;color:#3f3f46;outline:none;cursor:pointer;transition:border-color 0.1s;font-family:inherit'
  );
  s.addEventListener('focus', () => (s.style.borderColor = 'rgba(233,150,122,0.5)'));
  s.addEventListener('blur', () => (s.style.borderColor = '#e4e4e7'));
  return s;
}
