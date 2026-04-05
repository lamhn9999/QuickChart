import { appState, positionDropdown, updateTable } from './workspace.js';

// ── Filter globals ────────────────────────────────────────────────────────────
let filters = [];
let executingFilterController = null;

export const enumMap = {
    "String": 0, "Int": 1, "Double": 2, "DateTime": 3, "Bool": 4, "ListString": 5
};

// ── Filter popup UI ───────────────────────────────────────────────────────────

export function filterClicked(event, filterbtn) {
    event.stopPropagation();

    const popup   = filterbtn.parentElement.querySelector(".filterPopup");
    const wasOpen = !popup.classList.contains("hidden");

    // Close all open dropdowns first
    document.querySelectorAll('.filterPopup').forEach(fm => fm.classList.add('hidden'));
    document.querySelectorAll('.gearPopup').forEach(m => m.classList.add('hidden'));
    document.querySelectorAll('.gear-btn svg').forEach(s => s.classList.remove('rotate-180'));

    if (!wasOpen) {
        // Position using fixed coords before revealing — escapes overflow:auto clipping
        positionDropdown(popup, filterbtn);
        popup.classList.remove('hidden');
        console.log(`[filterClicked] Opened filter popup for field index=${filterbtn.closest('th')?.dataset?.index}`);
    }

    // Not all filter variants have a text input (e.g. Bool only has buttons)
    popup.querySelector("input")?.focus();
}

export function filterPopupClicked(event, filterpopup) {
    event.stopPropagation();
    filterpopup.querySelector("input")?.focus();
}

function updateFilterChipUI(index) {
    const th = document.querySelector(`th[data-index="${index}"]`);
    if (!th) return;
    const filter = filters[index];
    const chip = th.querySelector('.filter-chip');
    if (!chip) return;

    let len = 0;
    if (filter) {
        if (filter.filteringValues)       len += filter.filteringValues.length;
        if (filter.filteringValuesInt)    len += filter.filteringValuesInt.length;
        if (filter.filteringValuesDouble) len += filter.filteringValuesDouble.length;
        if (filter.intBound)              len++;
        if (filter.doubleBound)           len++;
        if (filter.dateTimeBound)         len++;
    }

    if (len > 0) {
        chip.textContent = len;
        chip.classList.remove('hidden');
    } else {
        chip.classList.add('hidden');
    }
}

// ── String filter ─────────────────────────────────────────────────────────────

export async function filterInputKey(event, input) {
    if (event.key === "Enter") {
        event.preventDefault();

        const value = input.value;
        const columnHeader = input.closest("th");
        const index = columnHeader.dataset.index;
        const popup = input.closest(".filterPopup");
        const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]')?.checked || false;

        await sendFilterRequest(value.trim(), isNotCheckboxValue, index, popup);
        appendFilterValue(input, index);
    }
}

function appendFilterValue(input, index) {
    const container = input.previousElementSibling; // chip container above input
    const value = input.value.trim();
    if (!value) return;

    if (Array.from(container.children).some(c => c.textContent.replace('✕', '').trim() === value)) {
        input.value = "";
        return;
    }

    const popup = input.closest(".filterPopup");

    const chip = document.createElement("div");
    chip.className =
        "filterValue rounded-xl bg-[darksalmon] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";

    const text = document.createElement("span");
    text.textContent = value;
    text.className = "truncate min-w-0";

    const removeBtn = document.createElement("button");
    removeBtn.textContent = "✕";
    removeBtn.className = "ml-1 font-bold hover:cursor-pointer";

    removeBtn.onclick = async (e) => {
        e.stopPropagation();

        if (filters[index] && filters[index].filteringValues) {
            filters[index].filteringValues =
                filters[index].filteringValues.filter(v => v !== value);

            if (filters[index].filteringValues.length === 0) {
                filters[index] = undefined;
            }
        }

        chip.remove();
        const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]')?.checked || false;
        await sendFilterRequest("", isNotCheckboxValue, index, popup);
    };

    chip.onclick = () => {
        input.value = value;
        input.focus();
    };

    chip.appendChild(text);
    chip.appendChild(removeBtn);
    container.appendChild(chip);

    input.value = "";
}

export async function excludeCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    const index = columnHeader.dataset.index;
    const isNotCheckboxValue = checkbox.checked;
    const popup = checkbox.closest('.filterPopup');

    await sendFilterRequest("", isNotCheckboxValue, index, popup);
}

async function sendFilterRequest(value, isNotCheckboxValue, index, popup) {
    const fieldType = enumMap[popup.dataset.fieldType] || 0;
    const isPartialMatch = popup.querySelector('input[name="IsPartialMatch"]')?.checked || false;

    if (value !== "") {
        if (!filters[index]) {
            filters[index] = {
                isPartialMatch: isPartialMatch,
                fieldIndex: parseInt(index),
                fieldType: fieldType,
                filteringValues: []
            };
        } else {
            filters[index].fieldType = fieldType;
            filters[index].isPartialMatch = isPartialMatch;
        }

        if (!filters[index].filteringValues) filters[index].filteringValues = [];
        if (!filters[index].filteringValues.includes(value)) {
            filters[index].filteringValues.push(value);
        }
        filters[index].isNot = isNotCheckboxValue;
    }
    else if (filters[index]) {
        filters[index].isNot = isNotCheckboxValue;
        filters[index].isPartialMatch = isPartialMatch;
        filters[index].fieldType = fieldType;
        if (!filters[index].filteringValues || filters[index].filteringValues.length === 0) {
            filters[index] = undefined;
        }
    }

    updateFilterChipUI(index);
    if (!popup.classList.contains("executing")) await executeFilterRequest();
}

export async function partialMatchStrCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    if (filters[index]) {
        filters[index].isPartialMatch = checkbox.checked;
        await executeFilterRequest();
    }
}

// ── Int / Double filter ───────────────────────────────────────────────────────

export async function filterIntSpecificKey(event, input) {
    if (event.key === "Enter") {
        event.preventDefault();
        const value = input.value;
        if (value.trim() === "") return;

        const columnHeader = input.closest("th");
        const index = columnHeader.dataset.index;
        const popup = input.closest(".filterPopup");
        const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]').checked;

        await sendIntSpecificFilterRequest(value, isNotCheckboxValue, index, input);
    }
}

async function sendIntSpecificFilterRequest(rawVal, isNot, index, input) {
    const popup     = input.closest(".filterPopup");
    const fieldType = enumMap[popup.dataset.fieldType];

    if (!filters[index]) {
        filters[index] = {
            fieldIndex: parseInt(index),
            fieldType,
            filteringValuesInt:    [],
            filteringValuesDouble: [],
            intBound:    null,
            doubleBound: null,
            isNot
        };
    }

    filters[index].intBound    = null;
    filters[index].doubleBound = null;
    filters[index].fieldType   = fieldType;

    let displayVal = null;
    if (fieldType === 1) { // Int
        const value = parseInt(rawVal);
        if (!isNaN(value)) {
            if (!filters[index].filteringValuesInt) filters[index].filteringValuesInt = [];
            if (!filters[index].filteringValuesInt.includes(value)) {
                filters[index].filteringValuesInt.push(value);
                displayVal = value;
            }
        }
    } else { // Double
        const value = parseFloat(rawVal);
        if (!isNaN(value)) {
            if (!filters[index].filteringValuesDouble) filters[index].filteringValuesDouble = [];
            if (!filters[index].filteringValuesDouble.includes(value)) {
                filters[index].filteringValuesDouble.push(value);
                displayVal = value;
            }
        }
    }

    filters[index].isNot = isNot;

    updateFilterChipUI(index);
    if (displayVal !== null) appendIntFilterValue(input, index, displayVal);

    await executeFilterRequest();
}

function appendIntFilterValue(input, index, value) {
    const container = input.previousElementSibling; // filterValues block

    // Clear any existing range chips before adding a specific-value chip
    Array.from(container.children).forEach(child => {
        if (child.textContent.includes(" to ")) child.remove();
    });

    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    chip.innerHTML = `<span class="truncate min-w-0">${value}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index]) {
            filters[index].filteringValuesInt    = (filters[index].filteringValuesInt    || []).filter(v => v !== value);
            filters[index].filteringValuesDouble = (filters[index].filteringValuesDouble || []).filter(v => v !== value);
            if (
                (!filters[index].filteringValuesInt    || filters[index].filteringValuesInt.length    === 0) &&
                (!filters[index].filteringValuesDouble || filters[index].filteringValuesDouble.length === 0) &&
                !filters[index].intBound && !filters[index].doubleBound
            ) {
                filters[index] = undefined;
            }
        }
        chip.remove();

        const popup = input.closest(".filterPopup");
        if (popup) {
            const isNotCheckboxValue = popup.querySelector('input[name="IsNot"]').checked;
            if (filters[index]) filters[index].isNot = isNotCheckboxValue;
        }

        await executeFilterRequest();
    };

    container.appendChild(chip);
    input.value = "";
}

export async function filterIntRangeKey(event, input) {
    if (event.key === "Enter") {
        event.preventDefault();
        const popup = input.closest(".filterPopup");
        const btn   = popup.querySelector("button");
        await applyIntRangeFilter(event, btn);
    }
}

export async function applyIntRangeFilter(event, btn) {
    event.stopPropagation();
    const columnHeader = btn.closest("th");
    if (!columnHeader) return;
    const index     = columnHeader.dataset.index;
    const popup     = btn.closest(".filterPopup");
    const fieldType = enumMap[popup.dataset.fieldType];

    const minInput = popup.querySelector(".int-range-min").value;
    const maxInput = popup.querySelector(".int-range-max").value;
    const isNot    = popup.querySelector('input[name="IsNot"]').checked;

    if (minInput === "" && maxInput === "") return;

    if (!filters[index]) {
        filters[index] = {
            fieldIndex: parseInt(index),
            fieldType,
            filteringValuesInt:    null,
            filteringValuesDouble: null,
            isNot
        };
    }

    filters[index].fieldType           = fieldType;
    filters[index].filteringValuesInt  = null;
    filters[index].filteringValuesDouble = null;

    let minVal, maxVal;
    if (fieldType === 1) { // Int
        minVal = minInput === "" ? -2147483648          : parseInt(minInput);
        maxVal = maxInput === "" ? 2147483647           : parseInt(maxInput);
        filters[index].intBound = { leftBound: minVal, rightBound: maxVal };
    } else { // Double
        minVal = minInput === "" ? -1.7976931348623157E+308 : parseFloat(minInput);
        maxVal = maxInput === "" ?  1.7976931348623157E+308 : parseFloat(maxInput);
        filters[index].doubleBound = { leftBound: minVal, rightBound: maxVal };
    }
    filters[index].isNot = isNot;

    updateFilterChipUI(index);

    const container = popup.querySelector(".filterValues");
    container.innerHTML = "";
    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    chip.innerHTML = `<span class="truncate min-w-0">${minInput === "" ? "-inf" : minVal} to ${maxInput === "" ? "+inf" : maxVal}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index]) {
            filters[index].intBound    = null;
            filters[index].doubleBound = null;
        }
        if (!filters[index] || (
            (!filters[index].filteringValuesInt    || filters[index].filteringValuesInt.length    === 0) &&
            (!filters[index].filteringValuesDouble || filters[index].filteringValuesDouble.length === 0)
        )) {
            filters[index] = undefined;
        }
        chip.remove();

        popup.querySelector(".int-range-min").value = "";
        popup.querySelector(".int-range-max").value = "";

        await executeFilterRequest();
    };

    container.appendChild(chip);
    await executeFilterRequest();
}

export async function excludeIntCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    if (filters[index]) {
        filters[index].isNot = checkbox.checked;
        await executeFilterRequest();
    }
}

// ── DateTime filter ───────────────────────────────────────────────────────────

export async function applyDtRangeFilter(event, btn) {
    event.stopPropagation();
    const columnHeader = btn.closest("th");
    if (!columnHeader) return;
    const index     = columnHeader.dataset.index;
    const popup     = btn.closest(".filterPopup");
    const fieldType = enumMap[popup.dataset.fieldType];

    const minInput = popup.querySelector(".dt-range-min").value;
    const maxInput = popup.querySelector(".dt-range-max").value;
    const isNot    = popup.querySelector('input[name="IsNot"]').checked;

    if (minInput === "" && maxInput === "") return;

    const minDt = minInput === "" ? "0001-01-01T00:00:00" : minInput;
    const maxDt = maxInput === "" ? "9999-12-31T23:59:59" : maxInput;

    if (!filters[index]) {
        filters[index] = { fieldIndex: parseInt(index), fieldType, isNot, dateTimeBound: null };
    }

    filters[index].fieldType     = fieldType;
    filters[index].dateTimeBound = { leftBound: minDt, rightBound: maxDt };
    filters[index].isNot         = isNot;

    updateFilterChipUI(index);

    const container = popup.querySelector(".filterValues");
    container.innerHTML = "";
    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    const minDpy = minInput === "" ? "-inf" : new Date(minInput).toLocaleString();
    const maxDpy = maxInput === "" ? "+inf" : new Date(maxInput).toLocaleString();
    chip.innerHTML = `<span class="truncate min-w-0">${minDpy} to <br> ${maxDpy}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index]) filters[index].dateTimeBound = null;
        filters[index] = undefined;
        chip.remove();

        popup.querySelector(".dt-range-min").value = "";
        popup.querySelector(".dt-range-max").value = "";

        await executeFilterRequest();
    };

    container.appendChild(chip);
    await executeFilterRequest();
}

export async function excludeDtCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    if (filters[index]) {
        filters[index].isNot = checkbox.checked;
        await executeFilterRequest();
    }
}

// ── Bool filter ───────────────────────────────────────────────────────────────

export async function applyBoolFilter(event, btn, val) {
    event.stopPropagation();
    const columnHeader = btn.closest("th");
    if (!columnHeader) return;
    const index     = columnHeader.dataset.index;
    const popup     = btn.closest(".filterPopup");
    const fieldType = enumMap[popup.dataset.fieldType];
    const isNot     = popup.querySelector('input[name="IsNot"]')?.checked || false;

    if (!filters[index]) {
        filters[index] = { fieldIndex: parseInt(index), fieldType, filteringValues: [], isNot };
    }
    filters[index].fieldType = fieldType;

    // Strict toggle: overwrite to single value
    filters[index].filteringValues = [val];
    filters[index].isNot = isNot;

    const container = popup.querySelector(".filterValues");
    container.innerHTML = "";
    appendBoolFilterValue(popup, index, val);

    updateFilterChipUI(index);
    await executeFilterRequest();
}

function appendBoolFilterValue(popup, index, value) {
    const container = popup.querySelector(".filterValues");
    const chip = document.createElement("div");
    chip.className = "filterValue rounded-xl bg-[#E9967A] text-black px-2 py-1 text-xs flex items-center gap-1 max-w-full";
    chip.innerHTML = `<span class="truncate min-w-0">${value}</span><button class="ml-1 font-bold hover:cursor-pointer">✕</button>`;

    chip.querySelector("button").onclick = async (e) => {
        e.stopPropagation();
        if (filters[index] && filters[index].filteringValues) {
            filters[index].filteringValues = filters[index].filteringValues.filter(v => v !== value);
            if (filters[index].filteringValues.length === 0) {
                filters[index] = undefined;
            }
        }
        chip.remove();
        await executeFilterRequest();
    };
    container.appendChild(chip);
}

export async function excludeBoolCheckboxClicked(event, checkbox) {
    event.stopPropagation();
    const columnHeader = checkbox.closest("th");
    if (!columnHeader) return;
    const index = columnHeader.dataset.index;
    if (filters[index]) {
        filters[index].isNot = checkbox.checked;
        await executeFilterRequest();
    }
}

// ── Execute filter request ────────────────────────────────────────────────────

async function executeFilterRequest() {
    if (executingFilterController) {
        executingFilterController.abort();
    }
    executingFilterController = new AbortController();
    appState.controller = executingFilterController;
    const activeFilters = filters.filter(f => f !== undefined);
    console.log(`[executeFilterRequest] Sending ${activeFilters.length} active filter(s).`);
    try {
        const response = await fetch("/workspace/filter", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(activeFilters),
            signal: executingFilterController.signal
        });
        const result = await response.json();
        updateTable(result);
    } catch (e) {
        if (e.name !== 'AbortError') console.error("[executeFilterRequest] Fetch error:", e);
    }
}
