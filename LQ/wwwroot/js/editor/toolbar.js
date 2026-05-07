// @ts-check
/**
 * @file Floating inline-format toolbar — appears above the current text selection inside
 * any `.doc-text` or `.tc-content` block. Bold / italic / underline / strikethrough /
 * inline-code / link.
 *
 * The Comment button is registered but the action is deferred — `data-action="comment"`
 * resolves to `return; // future` in the click handler.
 */

/** Mount the toolbar element + its global selectionchange listener. */
export function initInlineToolbar() {
  const tb = document.createElement('div');
  tb.id = 'doc-inline-toolbar';
  tb.innerHTML = `
      <button class="tb-btn" data-cmd="bold"><b>B</b></button>
      <button class="tb-btn tb-italic" data-cmd="italic"><i>I</i></button>
      <button class="tb-btn tb-under" data-cmd="underline"><u>U</u></button>
      <button class="tb-btn tb-strike" data-cmd="strikeThrough"><s>S</s></button>
      <div class="tb-sep"></div>
      <button class="tb-btn tb-mono" data-cmd="code" title="Inline code">⌥</button>
      <div class="tb-sep"></div>
      <button class="tb-btn" data-cmd="createLink">Link</button>
      <button class="tb-btn tb-comment" data-action="comment">Comment</button>
    `;
  document.body.appendChild(tb);

  tb.addEventListener('mousedown', (e) => {
    e.preventDefault();
    const t = /** @type {Element | null} */ (e.target);
    const btn = /** @type {HTMLElement | null} */ (t?.closest('[data-cmd],[data-action]'));
    if (!btn) return;

    const cmd = btn.dataset.cmd;
    const action = btn.dataset.action;

    if (action === 'comment') return; // future

    if (cmd === 'code') {
      const sel = window.getSelection();
      if (sel && !sel.isCollapsed) {
        const selectedText = sel.toString();
        document.execCommand(
          'insertHTML',
          false,
          `<code style="background:rgba(135,131,120,0.15);padding:1px 5px;border-radius:3px;font-family:monospace;font-size:0.875em;color:#E9967A">${selectedText}</code>`
        );
      }
    } else if (cmd === 'createLink') {
      const url = prompt('URL:');
      if (url) document.execCommand('createLink', false, url);
    } else if (cmd) {
      document.execCommand(cmd, false);
    }
  });

  document.addEventListener('selectionchange', () => {
    const sel = window.getSelection();
    if (!sel || sel.isCollapsed || !sel.toString().trim()) {
      tb.classList.remove('open');
      return;
    }

    const anchor = /** @type {Element | null} */ (sel.anchorNode?.parentElement)?.closest(
      '.doc-text, .tc-content'
    );
    if (!anchor) {
      tb.classList.remove('open');
      return;
    }

    const range = sel.getRangeAt(0);
    const rect = range.getBoundingClientRect();
    if (!rect || rect.width === 0) return;

    const tbW = 300;
    let top = rect.top - 46;
    let left = rect.left + rect.width / 2 - tbW / 2;

    if (top < 8) top = rect.bottom + 8;
    if (left < 8) left = 8;
    if (left + tbW > window.innerWidth - 8) left = window.innerWidth - tbW - 8;

    tb.style.top = `${top}px`;
    tb.style.left = `${left}px`;
    tb.classList.add('open');
  });
}
