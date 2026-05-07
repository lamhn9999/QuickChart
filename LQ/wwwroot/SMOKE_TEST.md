# Workspace JS smoke test

Run this checklist after every refactor phase from `~/.claude/plans/analyze-wwwroot-js-how-would-snug-donut.md`.
Each phase ships only when this passes end-to-end.

## How to run

```bash
# typecheck (must not regress past baseline; see below)
cd <repo-root>
npm run typecheck

# tailwind (in another shell, optional --watch)
cd <repo-root>/LQ/wwwroot
npx tailwindcss -i style.css -o style_tw.css

# app
cd <repo-root>
dotnet run --project LQ/LQ.csproj
# → http://localhost:5000
```

Replace `<repo-root>` with the path to the cloned LQ repository.

Use a sample CSV from `LQ/Data/SampleCSVs/` that has at least one column of each type:
`int`, `double`, `datetime`, `bool`, `string`, `list<string>`.

## Typecheck baseline

Phase-0 baseline: **83 errors** across 6 files (recorded in `.typecheck-baseline.log`).

| File | Baseline errors |
|---|---|
| `chart.js` | 52 |
| `editor.js` | 22 |
| `main.js` | 5 |
| `canvas-persist.js` | 2 |
| `workspace.js` | 1 |
| `filter.js` | 1 |

A phase is acceptable if **error count ≤ baseline AND no new file appears**. Phase 9 drives this to 0 under `strict: true`.

## Smoke-test scenarios (run in order)

### Auth + upload
1. **[Authenticated]** Sign in (or register) → `/` shows file list.
2. Upload a CSV from `LQ/Data/SampleCSVs/` → file appears in list.
3. Click the file → workspace loads.

### Table panel + sparklines
4. Header types are correct (numeric / categorical / identifier badges + type icons).
5. Every column header shows a sparkline mini-chart (numeric, bool, categorical, string variants render).
6. Sort table by clicking any header.
7. Toggle table panel collapse mode → cards view.

### Header pending + AutoChart drop
8. Click a header → it goes "pending" (highlighted, badge shows `1 selected`).
9. Click a second header → badge says `2 selected`.
10. Drag any pending header onto plot panel → chart card appears at drop position.
11. Drag a third header onto an *existing* card → group is **replaced** (not duplicated).

### Chart card UX
12. Drag a chart card around the canvas → snap guides appear when edges align with neighbours.
13. Resize a chart card from each handle (corners + edges) → Chart.js resizes cleanly.
14. Open card menu → **Duplicate**, **Delete**, **Background colour**, **Bring to front** all work.
15. Hover-reveal drag handle (the recent feature in commit 124d990) appears next to card.

### Chart-type panel + zoom
16. Click chart card → zoom overlay opens, options pane on right.
17. In options pane: toggle a series, change a colour, change axis labels, change title.
18. Open zoom on **every chart type** in `chartArray` (12 types: pie, bar, line, scatter, polarArea, mixed, matrix, mixedBar, bubble, …). Close with Esc; close with X.
19. For time-series: navigator mini-chart appears below; drag the navigator window.

### Filtering
20. Filter a string column: chip add/remove, partial-match toggle, exclude toggle.
21. Filter a numeric column: specific value, range, exclude. Verify table updates.
22. Filter a datetime column: range mode, exclude.
23. Filter a bool column: toggle.
24. Filter a `list<string>` column: search.
25. Active filters survive across chart drops (drop a header → chart respects current filters).

### Field-type / category UI
26. Gear icon on header → "Change field type" submenu → switch a string column to bool. Verify it re-validates.
27. For a `list<string>` column: open Categories submenu → toggle individual categories.

### Editor + canvas-persist
28. Add a text card via toolbar → type into it → resize.
29. `/` slash command on a new line: insert each block type (paragraph, h1, h2, h3, callout, code, divider).
30. Inline format toolbar (select text): bold / italic / underline / strikethrough / code / link.
31. Markdown shortcut auto-convert: type `# `, `## `, `### `, `> `, ` ``` `, `---`.
32. Drag a block with the handle → reorder.
33. Refresh page → canvas hydrates: every chart, every block, every text card at the correct position with correct content.

### Export
34. Export canvas to HTML → file downloads → open in browser → all charts render in the export.

### Guest flow
35. Sign out (or open a fresh incognito) → upload a CSV as guest → workspace works.
36. Verify guest cookie session persists across reload.
37. Repeat scenarios 4–11 as guest.

## Acceptance per phase

Each phase must keep **all 37 scenarios green** *and* `npm run typecheck` ≤ 83 errors. New ESLint or Prettier warnings: zero. New `console.error`s during the smoke run: zero (open DevTools console while testing).

After phase 8: zero inline `onclick=` handlers in `LQ/Views/Workspace/Workspace.cshtml` and `WorkspaceSpecific.cshtml`.

After phase 9: typecheck = 0 errors under `strict: true`.

After phase 6: `LQ/wwwroot/js/chart.js` no longer exists; no file in `LQ/wwwroot/js/` exceeds 400 LoC.

## Phase 10 — esbuild bundle (optional)

Three entry points → three bundled files under `LQ/wwwroot/js/dist/`. The build does **not** auto-rewrite Razor; dev mode keeps loading raw ESM, prod swaps to bundles via the steps below.

### Build commands

```bash
cd /home/lam/LQ
npm run build         # one-shot, minified, sourcemapped — for production
npm run build:watch   # rebuild on change, no minify — for testing the bundle in dev
npm run build:dev     # one-shot, no minify, sourcemapped — for debugging the bundle
```

Output sizes (approximate, current):

| Bundle | Minified | Sourcemap |
|---|---|---|
| `dist/editor.min.js` | ~21 KB | ~89 KB |
| `dist/canvas-persist.min.js` | ~65 KB | ~238 KB |
| `dist/workspace.min.js` | ~90 KB | ~325 KB |

Total minified: ~176 KB (vs ~448 KB raw across 67 files).

### Switching Razor to use the bundles

`LQ/Views/Workspace/Workspace.cshtml` near the bottom:

```diff
+ @inject Microsoft.AspNetCore.Hosting.IWebHostEnvironment Env
+ @{ var bundled = Env.IsProduction(); }
- <script type="module" src="~/js/editor/index.js?v=4"></script>
- <script type="module" src="~/js/workspace/index.js?v=4"></script>
- <script type="module" src="~/js/canvas-persist/index.js?v=4"></script>
+ <script type="module" src="@(bundled ? "~/js/dist/editor.min.js" : "~/js/editor/index.js?v=4")"></script>
+ <script type="module" src="@(bundled ? "~/js/dist/workspace.min.js" : "~/js/workspace/index.js?v=4")"></script>
+ <script type="module" src="@(bundled ? "~/js/dist/canvas-persist.min.js" : "~/js/canvas-persist/index.js?v=4")"></script>
```

This keeps `dotnet run` (Development) using raw ESM for fast iteration and uses the bundle in Production. Build the bundle as a CI step before deploy:

```bash
npm ci && npm run build && dotnet publish ...
```

### Acceptance

After phase 10:
- `npm run build` exits 0.
- `LQ/wwwroot/js/dist/{editor,workspace,canvas-persist}.min.js` exist and pass `node --check`.
- Smoke scenarios 1–37 all pass with the bundled output (test by either flipping the Razor `bundled` flag or temporarily rewriting the three `<script>` tags).
- `LQ/wwwroot/js/dist/` is gitignored.

## Phase 11 — Delegation bleed-through fixes

Five bugs (two reported, three found in audit) caused by `target.closest()` walks
matching popup ancestors and by `filterPopupClicked` blindly re-focusing the first
input on every click. Fix:

- `core/menus.js` exports `POPUP_SELECTORS` derived from `MENU_KINDS`.
- `wireColumnHighlight` and `'header:click'` early-return when `target.closest(POPUP_SELECTORS)`.
- `filterPopupClicked` deleted entirely; `data-action="filter:popup-click"` removed
  from all 5 filter popup divs.

### Smoke scenarios — run all of these in addition to 1–37

**Bug A (column-highlight bleed)**:
- 38. Open a numeric filter popup → click the min input → only the input is focused.
  Table column has **no** `.col-selected` highlight, **no** `.header-pending` badge.

**Bug B (focus-stealing)**:
- 39. In numeric range filter, click min → max → min → focus follows clicks exactly.

**Bug C (gear submenu pending bleed)**:
- 40. Click gear icon → click "Change Field Type" → submenu opens.
- 41. Pick a new type → POST fires, label updates, **column has no `.header-pending` badge**.

**Bug D (category submenu pending bleed)**:
- 42. Click gear → "Categories" → toggle a category → POST fires; **column not pending**.

**Bug E (focus-steal on popup buttons)**:
- 43. Click "Apply Range" button → button activates; focus does NOT jump to first input.
- 44. Click "Exclude results" checkbox → checkbox toggles; focus does NOT jump.

### Regression checks

- 45. Click a th's blank area (NOT a popup) → column toggles `.header-pending` (this should still work).
- 46. Drag a header onto plot panel → still works.
- 47. Click outside any open popup → all popups close (closer still active).

### Acceptance

After phase 11:
- All scenarios 1–47 pass.
- `npm run typecheck` = 0 errors under `strict: true`.
- No `filterPopupClicked` symbol exists anywhere in `LQ/wwwroot/js/`.
- No `filter:popup-click` action attribute exists in `LQ/Views/`.
