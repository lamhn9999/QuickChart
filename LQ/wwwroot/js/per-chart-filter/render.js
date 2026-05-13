// @ts-check
/**
 * @file Filter-card DOM construction.
 *
 * Pure-rendering helpers — no fetch calls, no registry mutations. The two
 * exported builders return a complete `.filter-card` element ready to be
 * placed inside a `.canvas-card` wrapper. Both variants share the same
 * header and footer; only the body differs.
 *
 * Action wiring is delegation-based via `data-action` attributes that
 * `workspace/index.js` dispatches into per-chart-filter/events.js. Keeping
 * the markup declarative means hydrate-on-reload doesn't need to re-bind
 * a single listener per pill.
 */

/** @typedef {import('./state.js').FilterRecord} FilterRecord */

const FILTER_ICON_SVG = `
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
       stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round">
    <path d="M3 4h18l-7 9v6l-4 2v-8z"/>
  </svg>`;

const CLOSE_ICON_SVG = `
  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor"
       stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
    <path d="M6 6l12 12M6 18L18 6"/>
  </svg>`;

const SEARCH_ICON_SVG = `
  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor"
       stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round">
    <circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3"/>
  </svg>`;

/**
 * Build the static header + footer scaffold shared by both variants.
 * @param {FilterRecord} rec
 * @returns {HTMLElement}
 */
function buildShell(rec) {
  const root = document.createElement('div');
  root.className = 'filter-card';
  root.dataset.filterGroupId = String(rec.groupId);

  const header = document.createElement('div');
  header.className = 'filter-card-header';
  header.innerHTML = `
    <span class="filter-card-name">${escapeHtml(rec.fieldName)}</span>
    <span class="filter-card-mode" data-filter-mode-label></span>
  `;
  root.appendChild(header);

  const body = document.createElement('div');
  body.className = 'filter-card-body';
  body.dataset.filterBody = '';
  root.appendChild(body);

  const footer = document.createElement('div');
  footer.className = 'filter-card-footer';
  footer.innerHTML = `
    <span class="filter-card-applied">
      <span data-filter-applied-count>${rec.appliedTo.size}</span>
      ${rec.appliedTo.size === 1 ? 'chart' : 'charts'}
    </span>
    <button type="button" class="filter-card-clear"
            data-action="filter:clear">Clear</button>
  `;
  root.appendChild(footer);

  return root;
}

/**
 * Buttons-variant body. One pill per available value.
 * @param {FilterRecord} rec
 */
export function renderButtonsBody(rec) {
  const root = buildShell(rec);
  const body = /** @type {HTMLElement} */ (root.querySelector('[data-filter-body]'));
  body.classList.add('filter-buttons');

  rec.availableValues.forEach((value) => {
    const v = value ?? '';
    const pill = document.createElement('button');
    pill.type = 'button';
    pill.className = 'filter-pill';
    pill.dataset.action = 'filter:pill';
    pill.dataset.value = v;
    pill.textContent = v === '' ? '(blank)' : v;
    if (rec.selectedValues.has(v)) pill.classList.add('active');
    body.appendChild(pill);
  });

  setModeLabel(root, rec);
  return root;
}

/**
 * Search-variant body. Text input + chip list. Initial chips come from
 * any pre-existing selectedValues (e.g. on hydrate from canvas persistence).
 * @param {FilterRecord} rec
 */
export function renderSearchBody(rec) {
  const root = buildShell(rec);
  const body = /** @type {HTMLElement} */ (root.querySelector('[data-filter-body]'));
  body.classList.add('filter-search');

  body.innerHTML = `
    <div class="filter-search-row">
      <span class="filter-search-icon">${SEARCH_ICON_SVG}</span>
      <input type="text" class="filter-search-input"
             placeholder="Search values…"
             data-on-keydown="filter:search-enter"
             autocomplete="off" />
    </div>
    <div class="filter-search-chips" data-filter-chips></div>
  `;

  const chipsEl = /** @type {HTMLElement} */ (body.querySelector('[data-filter-chips]'));
  rec.selectedValues.forEach((v) => chipsEl.appendChild(buildChip(v)));

  setModeLabel(root, rec);
  return root;
}

/**
 * Build a single removable chip for the search variant.
 * @param {string} value
 */
export function buildChip(value) {
  const chip = document.createElement('span');
  chip.className = 'filter-chip';
  chip.dataset.value = value;
  chip.innerHTML = `
    <span class="filter-chip-text">${escapeHtml(value === '' ? '(blank)' : value)}</span>
    <button type="button" class="filter-chip-x"
            data-action="filter:chip-remove" title="Remove">${CLOSE_ICON_SVG}</button>
  `;
  return chip;
}

/**
 * Refresh the small footer line that shows how many charts are wired up
 * and (for buttons variant) the multi-select hint. Cheap enough to call
 * after every selection change.
 * @param {HTMLElement} root
 * @param {FilterRecord} rec
 */
export function setModeLabel(root, rec) {
  const label = /** @type {HTMLElement | null} */ (root.querySelector('[data-filter-mode-label]'));
  if (label) {
    if (rec.variant === 'buttons') {
      label.textContent = rec.selectedValues.size > 1 ? 'multi' : '';
    } else {
      label.textContent = '';
    }
  }
  const countEl = /** @type {HTMLElement | null} */ (root.querySelector('[data-filter-applied-count]'));
  if (countEl) countEl.textContent = String(rec.appliedTo.size);
  const appliedEl = /** @type {HTMLElement | null} */ (root.querySelector('.filter-card-applied'));
  if (appliedEl) {
    const noun = rec.appliedTo.size === 1 ? 'chart' : 'charts';
    appliedEl.lastChild && (appliedEl.lastChild.nodeValue = ` ${noun}`);
  }
}

/** Tiny HTML escaper — values can contain quotes / brackets from the CSV. */
function escapeHtml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
