// ── Shared state ──────────────────────────────────────────────────────────────
export const listIndexes    = new Set();
export const pendingIndexes = new Set();  // selected but not yet charted
export const appState = { controller: null };

// ── Navigation ────────────────────────────────────────────────────────────────

export function backHome() {
    window.location.href = '/';
}

// ── Dropdown positioning ──────────────────────────────────────────────────────

export function positionDropdown(popup, anchorEl) {
    const MARGIN = 8; // min gap from viewport edges (px)
    const GAP    = 4; // gap between anchor bottom and popup top (px)

    popup.style.visibility = 'hidden';
    popup.style.position   = 'fixed';
    popup.style.top        = '0px';
    popup.style.left       = '0px';
    popup.style.transform  = 'none'; // neutralise any Tailwind translate classes
    popup.classList.remove('hidden');

    const pw = popup.offsetWidth;
    const ph = popup.offsetHeight;

    popup.classList.add('hidden');
    popup.style.visibility = '';

    const rect = anchorEl.getBoundingClientRect();
    const vw   = window.innerWidth;
    const vh   = window.innerHeight;

    let top  = rect.bottom + GAP;
    let left = rect.left;

    if (left < MARGIN)            left = MARGIN;
    if (left + pw > vw - MARGIN)  left = vw - pw - MARGIN;

    // Flip above anchor if not enough space below
    if (top + ph > vh - MARGIN) {
        top = rect.top - ph - GAP;
    }
    // Last resort: pin to top margin
    if (top < MARGIN) top = MARGIN;

    popup.style.top    = `${top}px`;
    popup.style.left   = `${left}px`;
    popup.style.zIndex = '9999';
}

// ── Toast notifications ───────────────────────────────────────────────────────

export function showToast(message, isError = false) {
    const toast = document.createElement("div");
    toast.className = `fixed top-6 left-1/2 -translate-x-1/2 z-[100] px-6 py-3 rounded-full shadow-2xl backdrop-blur-xl border ${isError ? 'bg-red-500/10 border-red-500/20 text-red-400' : 'bg-green-500/10 border-green-500/20 text-green-400'} text-sm font-medium transition-all duration-300 transform scale-95 opacity-0 flex items-center gap-2`;
    toast.innerHTML = `
        ${isError
        ? '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path></svg>'
        : '<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path></svg>'}
        <span>${message}</span>
    `;
    document.body.appendChild(toast);

    requestAnimationFrame(() => {
        toast.classList.remove('scale-95', 'opacity-0');
        toast.classList.add('scale-100', 'opacity-100');
    });

    setTimeout(() => {
        toast.classList.remove('scale-100', 'opacity-100');
        toast.classList.add('scale-95', 'opacity-0');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// ── Table update ──────────────────────────────────────────────────────────────

export function updateTable(dto) {
    const tbody = document.querySelector("#dataTable tbody");
    let trs = tbody.querySelectorAll("tr");

    const dataRows = dto.values;

    dataRows.forEach((row, i) => {
        let tr;

        if (trs[i]) {
            tr = trs[i];
        } else {
            tr = document.createElement("tr");
            tbody.appendChild(tr);
            row.forEach(() => {
                const td = document.createElement("td");
                tr.appendChild(td);
            });
        }

        const tds = tr.querySelectorAll("td");
        row.forEach((cell, j) => {
            tds[j].textContent = cell;
        });
    });

    // Remove surplus rows
    for (let i = dataRows.length; i < trs.length; i++) {
        trs[i].remove();
    }
}

// ── Field type management ─────────────────────────────────────────────────────

export function gearBtnClicked(event, button) {
    event.stopPropagation();

    const popup   = button.parentElement.querySelector('.gearPopup');
    const wasOpen = !popup.classList.contains('hidden');

    document.querySelectorAll('.filterPopup').forEach(fm => fm.classList.add('hidden'));
    document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
    document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));

    if (!wasOpen) {
        positionDropdown(popup, button);
        popup.classList.remove('hidden');
        button.querySelector('svg').classList.add('rotate-180');
        console.log(`[gearBtnClicked] Opened gear popup`);
    }
}

export function toggleFieldTypeMenu(btn) {
    const popup    = btn.nextElementSibling;
    const isHidden = popup.classList.contains('hidden');

    document.querySelectorAll('.type-menu-popup').forEach(p => p.classList.add('hidden'));
    document.querySelectorAll('.type-menu-btn svg').forEach(s => s.classList.remove('rotate-180'));

    if (isHidden) {
        popup.classList.remove('hidden');
        btn.querySelector('svg').classList.add('rotate-180');
    }
}

export async function changeFieldType(event, fieldIndex, newType) {
    event.stopPropagation();

    document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
    document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));

    try {
        const formData = new FormData();
        formData.append('fieldIndex', fieldIndex);
        formData.append('newType', newType);

        const response = await fetch('/workspace/changeFieldType', {
            method: 'POST',
            body: formData
        });
        const result = await response.json();

        if (result === "Field updated") {
            const typeMapping = {
                "string":   "String",
                "int":      "Int32",
                "double":   "Double",
                "datetime": "DateTime",
                "bool":     "Boolean",
                "list":     "List`1"
            };
            const span = document.getElementById(`fieldType-${fieldIndex}`);
            if (span) {
                span.textContent = typeMapping[newType] || newType;
                const header = span.closest('th');
                if (header) {
                    const buttonsContainer = header.querySelector('.type-menu-popup');
                    if (buttonsContainer) {
                        const buttonsHtml = Object.keys(typeMapping)
                            .filter(t => t !== newType)
                            .map(t => `<button onclick="changeFieldType(event, ${fieldIndex}, '${t}')" class="text-left px-3 py-1.5 text-xs text-zinc-300 hover:text-white hover:bg-white/10 rounded-lg transition-colors w-full tracking-wide">${t}</button>`)
                            .join('');
                        buttonsContainer.innerHTML = buttonsHtml;
                    }
                }
            }
            showToast(`Field type successfully changed to ${newType}.`);
            console.log(`[changeFieldType] Field ${fieldIndex} → ${newType}`);
        } else {
            showToast("Failed to update field type.", true);
            console.warn(`[changeFieldType] Server rejected update for field ${fieldIndex}`);
        }
    } catch (e) {
        showToast("Error updating field type.", true);
        console.error("[changeFieldType] Fetch error:", e);
    }
}
