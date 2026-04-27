// ── Notion-like UX layer for #doc-canvas ─────────────────────────────────────
(function () {
  'use strict';

  const canvas = document.getElementById('doc-canvas');
  if (!canvas) return;

  // ── Block type registry ────────────────────────────────────────────────────

  const BLOCK_TYPES = [
    {
      id: 'paragraph', label: 'Text', icon: '¶',
      desc: 'Plain text paragraph',
      keys: ['text', 'paragraph', 'p', 'plain'],
    },
    {
      id: 'h1', label: 'Heading 1', icon: 'H1',
      desc: 'Large section heading',
      keys: ['h1', 'heading1', 'heading 1', 'title', 'large'],
    },
    {
      id: 'h2', label: 'Heading 2', icon: 'H2',
      desc: 'Medium section heading',
      keys: ['h2', 'heading2', 'heading 2', 'medium'],
    },
    {
      id: 'h3', label: 'Heading 3', icon: 'H3',
      desc: 'Small section heading',
      keys: ['h3', 'heading3', 'heading 3', 'small'],
    },
    {
      id: 'callout', label: 'Callout', icon: '»',
      desc: 'Highlighted note or quote',
      keys: ['callout', 'note', 'tip', 'quote', 'blockquote', 'highlight'],
    },
    {
      id: 'code', label: 'Code', icon: '</>',
      desc: 'Monospace code block',
      keys: ['code', 'pre', 'snippet', 'mono', 'technical'],
    },
    {
      id: 'divider', label: 'Divider', icon: '—',
      desc: 'Horizontal separator',
      keys: ['divider', 'separator', 'hr', 'line', 'rule'],
    },
  ];

  const PLACEHOLDER_MAP = {
    h1: 'Heading 1',
    h2: 'Heading 2',
    h3: 'Heading 3',
    callout: 'Type a note…',
    code: 'Code…',
    paragraph: "Start writing, or press '/' for commands…",
  };

  // ── Block creation ─────────────────────────────────────────────────────────

  function makeBlock(type, content = '') {
    if (type === 'divider') {
      const wrap = document.createElement('div');
      wrap.className = 'doc-divider';
      wrap.dataset.blockType = 'divider';
      const hr = document.createElement('hr');
      wrap.appendChild(hr);
      animateIn(wrap);
      return wrap;
    }

    const el = document.createElement('div');
    el.className = 'doc-text';
    el.contentEditable = 'true';
    el.spellcheck = false;
    el.dataset.blockType = type || 'paragraph';
    el.dataset.placeholder = PLACEHOLDER_MAP[type] || PLACEHOLDER_MAP.paragraph;

    if (content) el.textContent = content;

    animateIn(el);
    setupBlockListeners(el);
    return el;
  }

  function animateIn(el) {
    el.style.animation = 'none';
    requestAnimationFrame(() => {
      el.style.animation = 'blockIn 120ms cubic-bezier(0.16,1,0.3,1)';
    });
  }

  function insertAfterEl(newEl, refEl) {
    const next = refEl.nextSibling;
    if (next) canvas.insertBefore(newEl, next);
    else canvas.appendChild(newEl);
  }

  function focusBlockEnd(el) {
    if (!el || el.contentEditable !== 'true') return;
    el.focus();
    const sel = window.getSelection();
    const range = document.createRange();
    if (el.lastChild) {
      range.selectNodeContents(el);
      range.collapse(false);
    } else {
      range.setStart(el, 0);
      range.collapse(true);
    }
    sel.removeAllRanges();
    sel.addRange(range);
  }

  function focusBlockStart(el) {
    if (!el || el.contentEditable !== 'true') return;
    el.focus();
    const sel = window.getSelection();
    const range = document.createRange();
    range.setStart(el, 0);
    range.collapse(true);
    sel.removeAllRanges();
    sel.addRange(range);
  }

  function getAllBlocks() {
    return [...canvas.children].filter(c =>
      c.classList.contains('doc-text') ||
      c.classList.contains('doc-chart-section') ||
      c.classList.contains('doc-divider')
    );
  }

  function getAdjacentBlock(block, dir) {
    const blocks = getAllBlocks();
    const idx = blocks.indexOf(block);
    if (idx === -1) return null;
    return blocks[idx + dir] || null;
  }

  // ── Morph block type ───────────────────────────────────────────────────────

  function morphBlock(block, type) {
    if (!block || block.contentEditable !== 'true') return;

    if (type === 'divider') {
      const divider = makeBlock('divider');
      canvas.replaceChild(divider, block);
      const p = makeBlock('paragraph');
      insertAfterEl(p, divider);
      focusBlockStart(p);
      return;
    }

    block.dataset.blockType = type;
    block.dataset.placeholder = PLACEHOLDER_MAP[type] || PLACEHOLDER_MAP.paragraph;
    focusBlockEnd(block);
  }

  // ── Page title ─────────────────────────────────────────────────────────────

  function initTitle() {
    const titleEl = document.createElement('div');
    titleEl.id = 'doc-title';
    titleEl.contentEditable = 'true';
    titleEl.spellcheck = false;

    canvas.insertBefore(titleEl, canvas.firstChild);

    titleEl.addEventListener('keydown', e => {
      if (e.key === 'Enter') {
        e.preventDefault();
        const first = canvas.querySelector('.doc-text');
        if (first) focusBlockStart(first);
        else {
          const p = makeBlock('paragraph');
          insertAfterEl(p, titleEl);
          focusBlockStart(p);
        }
      }
    });
  }

  // ── Slash command menu ─────────────────────────────────────────────────────

  const slash = {
    open: false,
    query: '',
    activeIndex: 0,
    triggerBlock: null,
    menu: null,
  };

  function initSlashMenu() {
    const menu = document.createElement('div');
    menu.id = 'doc-slash-menu';
    document.body.appendChild(menu);
    slash.menu = menu;

    document.addEventListener('mousedown', e => {
      if (slash.open && !menu.contains(e.target)) closeSlashMenu();
    });
  }

  function openSlashMenu(block) {
    slash.open = true;
    slash.query = '';
    slash.activeIndex = 0;
    slash.triggerBlock = block;
    renderSlashMenu();
    positionSlashMenu();
    slash.menu.classList.add('open');
  }

  function closeSlashMenu() {
    slash.open = false;
    slash.menu.classList.remove('open');
    slash.query = '';
    slash.triggerBlock = null;
  }

  function renderSlashMenu() {
    const q = slash.query.toLowerCase();
    const filtered = q
      ? BLOCK_TYPES.filter(bt =>
          bt.label.toLowerCase().includes(q) ||
          bt.keys.some(k => k.includes(q))
        )
      : BLOCK_TYPES;

    const menu = slash.menu;

    if (filtered.length === 0) {
      menu.innerHTML = '<div class="slash-empty">No results for "' + slash.query + '"</div>';
      return;
    }

    if (slash.activeIndex >= filtered.length) slash.activeIndex = filtered.length - 1;

    menu.innerHTML = filtered.map((bt, i) => `
      <div class="slash-item ${i === slash.activeIndex ? 'active' : ''}" data-type="${bt.id}">
        <div class="slash-icon">${bt.icon}</div>
        <div class="slash-text">
          <div class="slash-label">${bt.label}</div>
          <div class="slash-desc">${bt.desc}</div>
        </div>
      </div>
    `).join('');

    menu.querySelectorAll('.slash-item').forEach(item => {
      item.addEventListener('mousedown', e => {
        e.preventDefault();
        pickSlashItem(item.dataset.type);
      });
    });
  }

  function positionSlashMenu() {
    const menu = slash.menu;
    const sel = window.getSelection();
    let rect;

    if (sel && sel.rangeCount) {
      rect = sel.getRangeAt(0).getBoundingClientRect();
    }

    if (!rect || rect.width === 0) {
      rect = (slash.triggerBlock || canvas).getBoundingClientRect();
    }

    const mW = 272, mH = 320;
    const vW = window.innerWidth, vH = window.innerHeight;

    let top = rect.bottom + 6;
    let left = rect.left;

    if (top + mH > vH - 12) top = rect.top - mH - 6;
    if (top < 8) top = 8;
    if (left + mW > vW - 8) left = vW - mW - 8;
    if (left < 8) left = 8;

    menu.style.top = `${top}px`;
    menu.style.left = `${left}px`;
  }

  function pickSlashItem(type) {
    const block = slash.triggerBlock;
    if (!block) { closeSlashMenu(); return; }

    // Strip the slash + query from the block content
    const text = block.textContent;
    const slashIdx = text.lastIndexOf('/');
    if (slashIdx !== -1) {
      block.textContent = text.substring(0, slashIdx);
    }

    closeSlashMenu();

    const isEmpty = block.textContent.trim() === '';

    if (type === 'divider') {
      const divider = makeBlock('divider');
      if (isEmpty) {
        canvas.replaceChild(divider, block);
      } else {
        insertAfterEl(divider, block);
      }
      const p = makeBlock('paragraph');
      insertAfterEl(p, divider);
      focusBlockStart(p);
      return;
    }

    if (isEmpty) {
      morphBlock(block, type);
    } else {
      const newBlock = makeBlock(type);
      insertAfterEl(newBlock, block);
      focusBlockStart(newBlock);
    }
  }

  // ── Block handle ──────────────────────────────────────────────────────────

  const handle = {
    el: null,
    contextMenu: null,
    turnMenu: null,
    currentBlock: null,
    showTimer: null,
    isDragging: false,
    dragBlock: null,
    dragPlaceholder: null,
  };

  function initBlockHandle() {
    // ── Grip button ──
    const el = document.createElement('div');
    el.id = 'doc-handle';
    el.innerHTML = `<svg width="14" height="14" viewBox="0 0 14 14" fill="currentColor">
      <circle cx="4.5" cy="2.5" r="1.4"/><circle cx="9.5" cy="2.5" r="1.4"/>
      <circle cx="4.5" cy="7"   r="1.4"/><circle cx="9.5" cy="7"   r="1.4"/>
      <circle cx="4.5" cy="11.5" r="1.4"/><circle cx="9.5" cy="11.5" r="1.4"/>
    </svg>`;
    document.body.appendChild(el);
    handle.el = el;

    // ── Context menu ──
    const cm = document.createElement('div');
    cm.id = 'doc-handle-menu';
    cm.innerHTML = `
      <div class="hm-item" data-action="duplicate">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"/></svg>
        Duplicate
      </div>
      <div class="hm-item hm-turn" data-action="turnInto">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3l4 4-4 4"/><path d="M3 7h18"/><path d="M7 21l-4-4 4-4"/><path d="M21 17H3"/></svg>
        Turn into
        <svg class="hm-chevron" width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M9 18l6-6-6-6"/></svg>
      </div>
      <div class="hm-sep"></div>
      <div class="hm-item hm-danger" data-action="delete">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14H6L5 6"/><path d="M9 6V4h6v2"/></svg>
        Delete
      </div>
    `;
    document.body.appendChild(cm);
    handle.contextMenu = cm;

    // ── Turn-into submenu ──
    const tm = document.createElement('div');
    tm.id = 'doc-turn-menu';
    tm.innerHTML = BLOCK_TYPES.filter(bt => bt.id !== 'divider').map(bt =>
      `<div class="hm-item" data-type="${bt.id}">
        <span class="hm-turn-icon">${bt.icon}</span>${bt.label}
      </div>`
    ).join('');
    document.body.appendChild(tm);
    handle.turnMenu = tm;

    // ── Handle: show on canvas hover ──
    canvas.addEventListener('mousemove', e => {
      const block = e.target.closest('.doc-text, .doc-chart-section, .doc-divider');
      if (!block || block.id === 'docDropCursor' || block.id === 'doc-title'
          || block.closest('.text-card')) {
        scheduleHide();
        return;
      }
      handle.currentBlock = block;
      clearTimeout(handle.showTimer);
      handle.showTimer = setTimeout(() => showHandle(block), 200);
    });

    canvas.addEventListener('mouseleave', () => scheduleHide());
    el.addEventListener('mouseenter', () => clearTimeout(handle.showTimer));
    el.addEventListener('mouseleave', e => {
      if (!cm.contains(e.relatedTarget)) scheduleHide();
    });

    // ── Handle click → context menu ──
    el.addEventListener('mousedown', e => {
      e.preventDefault();
      e.stopPropagation();
      if (cm.classList.contains('open')) {
        closeContextMenu();
      } else {
        openContextMenu();
      }
    });

    // ── Context menu actions ──
    cm.addEventListener('mousedown', e => {
      e.preventDefault();
      const item = e.target.closest('[data-action]');
      if (!item) return;
      const action = item.dataset.action;
      const block = handle.currentBlock;
      if (!block) return;

      if (action === 'delete') {
        closeContextMenu();
        block.remove();
      } else if (action === 'duplicate') {
        closeContextMenu();
        const clone = block.cloneNode(true);
        clone._editorSetup = false;
        animateIn(clone);
        insertAfterEl(clone, block);
        if (clone.contentEditable === 'true') {
          setupBlockListeners(clone);
          focusBlockEnd(clone);
        }
      } else if (action === 'turnInto') {
        const cmRect = cm.getBoundingClientRect();
        tm.style.top = `${cmRect.top}px`;
        tm.style.left = `${cmRect.right + 4}px`;
        tm.classList.add('open');
      }
    });

    // ── Turn-into submenu clicks ──
    tm.addEventListener('mousedown', e => {
      e.preventDefault();
      const item = e.target.closest('[data-type]');
      if (!item) return;
      const block = handle.currentBlock;
      closeTurnMenu();
      closeContextMenu();
      if (block) morphBlock(block, item.dataset.type);
    });

    // ── Close on outside click ──
    document.addEventListener('mousedown', e => {
      if (!cm.contains(e.target) && !el.contains(e.target) && !tm.contains(e.target)) {
        closeContextMenu();
      }
    });

    // ── Drag reorder ──
    el.draggable = true;

    el.addEventListener('dragstart', e => {
      const block = handle.currentBlock;
      if (!block) { e.preventDefault(); return; }

      handle.isDragging = true;
      handle.dragBlock = block;
      block.style.opacity = '0.35';

      const ghost = document.createElement('div');
      ghost.style.cssText = 'position:fixed;top:-999px;background:#18181B;color:#fff;padding:5px 12px;border-radius:6px;font-size:12px;font-family:"DM Sans",sans-serif;white-space:nowrap;';
      ghost.textContent = (block.textContent || 'Block').substring(0, 32);
      document.body.appendChild(ghost);
      e.dataTransfer.setDragImage(ghost, 0, 0);
      requestAnimationFrame(() => ghost.remove());
      e.dataTransfer.effectAllowed = 'move';
      e.dataTransfer.setData('text/block-reorder', '1');
    });

    el.addEventListener('dragend', () => {
      if (handle.dragBlock) handle.dragBlock.style.opacity = '';
      removeDragPlaceholder();
      handle.isDragging = false;
      handle.dragBlock = null;
    });

    canvas.addEventListener('dragover', e => {
      if (!handle.isDragging) return;
      e.preventDefault();
      e.stopPropagation();

      const blocks = getAllBlocks().filter(b => b !== handle.dragBlock);
      removeDragPlaceholder();

      const ph = document.createElement('div');
      ph.className = 'doc-drag-ph';
      handle.dragPlaceholder = ph;

      let insertBeforeEl = null;
      for (const b of blocks) {
        const r = b.getBoundingClientRect();
        if (e.clientY < r.top + r.height / 2) { insertBeforeEl = b; break; }
      }

      if (insertBeforeEl) canvas.insertBefore(ph, insertBeforeEl);
      else {
        // Insert before the drop cursor if it exists
        const cur = document.getElementById('docDropCursor');
        if (cur) canvas.insertBefore(ph, cur);
        else canvas.appendChild(ph);
      }
    });

    canvas.addEventListener('drop', e => {
      if (!handle.isDragging || !handle.dragBlock) return;
      e.preventDefault();
      e.stopPropagation();

      const ph = canvas.querySelector('.doc-drag-ph');
      if (ph) {
        canvas.insertBefore(handle.dragBlock, ph);
        ph.remove();
      }
      handle.isDragging = false;
    });
  }

  function showHandle(block) {
    const el = handle.el;
    const blockRect = block.getBoundingClientRect();

    el.style.top = `${blockRect.top + blockRect.height / 2 - 12}px`;
    el.style.left = `${blockRect.left - 30}px`;
    el.classList.add('visible');
    requestAnimationFrame(() => el.classList.add('show'));
  }

  let hideTimer = null;
  function scheduleHide() {
    clearTimeout(hideTimer);
    hideTimer = setTimeout(() => {
      if (!handle.contextMenu.classList.contains('open') && !handle.el.matches(':hover')) {
        hideHandle();
      }
    }, 100);
  }

  function hideHandle() {
    clearTimeout(handle.showTimer);
    handle.el.classList.remove('show');
    setTimeout(() => {
      if (!handle.el.classList.contains('show')) handle.el.classList.remove('visible');
    }, 150);
  }

  function openContextMenu() {
    const elRect = handle.el.getBoundingClientRect();
    const cm = handle.contextMenu;
    cm.style.top = `${elRect.top}px`;
    cm.style.left = `${elRect.right + 6}px`;
    cm.classList.add('open');
  }

  function closeContextMenu() {
    handle.contextMenu.classList.remove('open');
    closeTurnMenu();
  }

  function closeTurnMenu() {
    handle.turnMenu.classList.remove('open');
  }

  function removeDragPlaceholder() {
    canvas.querySelectorAll('.doc-drag-ph').forEach(p => p.remove());
    handle.dragPlaceholder = null;
  }

  // ── Inline formatting toolbar ──────────────────────────────────────────────

  function initInlineToolbar() {
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

    tb.addEventListener('mousedown', e => {
      e.preventDefault();
      const btn = e.target.closest('[data-cmd],[data-action]');
      if (!btn) return;

      const cmd = btn.dataset.cmd;
      const action = btn.dataset.action;

      if (action === 'comment') return; // future

      if (cmd === 'code') {
        const sel = window.getSelection();
        if (sel && !sel.isCollapsed) {
          const selectedText = sel.toString();
          document.execCommand('insertHTML', false,
            `<code style="background:rgba(135,131,120,0.15);padding:1px 5px;border-radius:3px;font-family:monospace;font-size:0.875em;color:#E9967A">${selectedText}</code>`
          );
        }
      } else if (cmd === 'createLink') {
        const url = prompt('URL:');
        if (url) document.execCommand('createLink', false, url);
      } else {
        document.execCommand(cmd, false, null);
      }
    });

    document.addEventListener('selectionchange', () => {
      const sel = window.getSelection();
      if (!sel || sel.isCollapsed || !sel.toString().trim()) {
        tb.classList.remove('open');
        return;
      }

      const anchor = sel.anchorNode?.parentElement?.closest('.doc-text');
      if (!anchor) { tb.classList.remove('open'); return; }

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

  // ── Block keyboard + input handlers ───────────────────────────────────────

  function setupBlockListeners(el) {
    if (el._editorSetup) return;
    el._editorSetup = true;
    el.addEventListener('keydown', onBlockKeydown);
    el.addEventListener('input', onBlockInput);
  }

  function onBlockKeydown(e) {
    const block = e.currentTarget;

    // ── Slash menu navigation ──
    if (slash.open) {
      const q = slash.query.toLowerCase();
      const filtered = q
        ? BLOCK_TYPES.filter(bt => bt.label.toLowerCase().includes(q) || bt.keys.some(k => k.includes(q)))
        : BLOCK_TYPES;

      if (e.key === 'ArrowDown') {
        e.preventDefault();
        slash.activeIndex = Math.min(slash.activeIndex + 1, filtered.length - 1);
        renderSlashMenu();
        return;
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault();
        slash.activeIndex = Math.max(slash.activeIndex - 1, 0);
        renderSlashMenu();
        return;
      }
      if (e.key === 'Enter') {
        e.preventDefault();
        if (filtered[slash.activeIndex]) pickSlashItem(filtered[slash.activeIndex].id);
        return;
      }
      if (e.key === 'Escape') { closeSlashMenu(); return; }
      if (e.key === 'Backspace') {
        if (slash.query.length === 0) closeSlashMenu();
        // Allow default backspace to delete char; input handler updates query
        return;
      }
      return; // for other keys while menu open, let them through
    }

    // ── Escape → block selection mode ──
    if (e.key === 'Escape') {
      e.preventDefault();
      enterSelectionMode(block);
      return;
    }

    // ── Enter → new paragraph below ──
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      const newBlock = makeBlock('paragraph');
      insertAfterEl(newBlock, block);
      focusBlockStart(newBlock);
      return;
    }

    // ── Backspace on empty block → remove ──
    if (e.key === 'Backspace') {
      const isEmpty = block.textContent === '' && !block.innerHTML.replace(/<br\s*\/?>/gi, '').trim();
      if (isEmpty) {
        e.preventDefault();
        const prev = getAdjacentBlock(block, -1);
        block.remove();
        if (prev && prev.contentEditable === 'true') focusBlockEnd(prev);
        return;
      }
    }

    // ── Slash: open command menu ──
    if (e.key === '/') {
      // Let the char be typed, then open
      setTimeout(() => {
        if (!slash.open) openSlashMenu(block);
      }, 0);
    }

    // ── Tab → focus next block ──
    if (e.key === 'Tab') {
      e.preventDefault();
      const next = getAdjacentBlock(block, e.shiftKey ? -1 : 1);
      if (next && next.contentEditable === 'true') focusBlockStart(next);
    }
  }

  function onBlockInput(e) {
    const block = e.currentTarget;

    if (slash.open && slash.triggerBlock === block) {
      const text = block.textContent;
      const idx = text.lastIndexOf('/');
      if (idx === -1) {
        closeSlashMenu();
      } else {
        slash.query = text.substring(idx + 1);
        renderSlashMenu();
        positionSlashMenu();
      }
      return;
    }

    checkMarkdown(block);
  }

  // ── Markdown shortcuts ─────────────────────────────────────────────────────

  const MD_PATTERNS = [
    { re: /^# $/,   type: 'h1' },
    { re: /^## $/,  type: 'h2' },
    { re: /^### $/, type: 'h3' },
    { re: /^> $/,   type: 'callout' },
    { re: /^```$/,  type: 'code' },
    { re: /^---$/,  type: 'divider' },
  ];

  function checkMarkdown(block) {
    const text = block.textContent;

    for (const p of MD_PATTERNS) {
      if (p.re.test(text)) {
        block.textContent = '';

        if (p.type === 'divider') {
          const divider = makeBlock('divider');
          canvas.replaceChild(divider, block);
          const newBlock = makeBlock('paragraph');
          insertAfterEl(newBlock, divider);
          focusBlockStart(newBlock);
        } else {
          block.dataset.blockType = p.type;
          block.dataset.placeholder = PLACEHOLDER_MAP[p.type] || PLACEHOLDER_MAP.paragraph;
        }
        return;
      }
    }
  }

  // ── Block selection mode ───────────────────────────────────────────────────

  let selectedBlock = null;

  function enterSelectionMode(block) {
    clearSelection();
    selectedBlock = block;
    block.classList.add('block-selected');
    block.blur();
    document.addEventListener('keydown', onSelectionKeydown);
    document.addEventListener('mousedown', onSelectionMousedown);
  }

  function clearSelection() {
    if (selectedBlock) {
      selectedBlock.classList.remove('block-selected');
      selectedBlock = null;
    }
    document.removeEventListener('keydown', onSelectionKeydown);
    document.removeEventListener('mousedown', onSelectionMousedown);
  }

  function onSelectionMousedown(e) {
    // Shift+click on a block → multi-select (visual only for now)
    const clicked = e.target.closest('.doc-text, .doc-chart-section, .doc-divider');
    if (!clicked) { clearSelection(); return; }
    if (e.shiftKey && selectedBlock) {
      clicked.classList.toggle('block-selected');
    } else {
      clearSelection();
    }
  }

  function onSelectionKeydown(e) {
    if (!selectedBlock) { clearSelection(); return; }

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const next = getAdjacentBlock(selectedBlock, 1);
      if (next) enterSelectionMode(next);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const prev = getAdjacentBlock(selectedBlock, -1);
      if (prev) enterSelectionMode(prev);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const block = selectedBlock;
      clearSelection();
      if (block.contentEditable === 'true') focusBlockEnd(block);
    } else if (e.key === 'Backspace' || e.key === 'Delete') {
      e.preventDefault();
      const next = getAdjacentBlock(selectedBlock, 1) || getAdjacentBlock(selectedBlock, -1);
      selectedBlock.remove();
      clearSelection();
      if (next && next.contentEditable === 'true') focusBlockEnd(next);
    } else if (e.key === 'Escape') {
      clearSelection();
    }
  }

  // ── Click-anywhere-to-edit ─────────────────────────────────────────────────

  canvas.addEventListener('click', e => {
    if (e.target === canvas) {
      clearSelection();
      const blocks = getAllBlocks();
      const last = blocks.filter(b => b.contentEditable === 'true').pop();
      if (last) {
        focusBlockEnd(last);
      } else {
        const p = makeBlock('paragraph');
        const cur = document.getElementById('docDropCursor');
        if (cur) canvas.insertBefore(p, cur);
        else canvas.appendChild(p);
        focusBlockStart(p);
      }
    }
  });

  // Clear selection when refocusing a block
  canvas.addEventListener('focusin', e => {
    if (e.target.classList.contains('doc-text')) clearSelection();
  });

  // ── Wire up existing blocks + observe new ones ─────────────────────────────

  function initExistingBlocks() {
    canvas.querySelectorAll('.doc-text').forEach(block => {
      if (!block.dataset.blockType) {
        block.dataset.blockType = 'paragraph';
      }
      block.dataset.placeholder = PLACEHOLDER_MAP[block.dataset.blockType] || PLACEHOLDER_MAP.paragraph;
      setupBlockListeners(block);
    });
  }

  const observer = new MutationObserver(mutations => {
    for (const m of mutations) {
      for (const node of m.addedNodes) {
        if (node.nodeType !== 1) continue;
        if (node.classList.contains('doc-text') && !node._editorSetup) {
          setupBlockListeners(node);
        }
        node.querySelectorAll?.('.doc-text').forEach(el => {
          if (!el._editorSetup) setupBlockListeners(el);
        });
      }
    }
  });

  observer.observe(canvas, { childList: true, subtree: true });

  // ── Boot ──────────────────────────────────────────────────────────────────

  initTitle();
  initSlashMenu();
  initBlockHandle();
  initInlineToolbar();
  initExistingBlocks();

})();
