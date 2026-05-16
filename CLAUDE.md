# CLAUDE.md — LQ / ACPTDA

## 1. Project Overview

This is a data-storytelling web application that lets users upload CSV files and instantly generate rich, auto-detected charts without writing any code. The product vision is "Notion meets Tableau": a split-pane workspace where the left side shows the raw data table and the right side is a Notion-like document canvas where users drop column headers to summon charts, then annotate them with text, headings, and callouts. It supports both authenticated analysts and anonymous guests (cookie-based, 7-day ephemeral session).

---

## 2. Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| Runtime | .NET 9 / ASP.NET Core MVC | `net9.0`, nullable enabled, implicit usings |
| Language | C# 13 | |
| ORM | EF Core 9 (`Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4) | Code-first, seven applied migrations |
| Bulk insert | `EFCore.BulkExtensions` 9.0.2 | Used for initial CSV value insert and bulk-update list fields |
| Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 9.0.11 | `IdentityUser` subclass `Analyst`; session-based, no JWT |
| API docs | `Swashbuckle.AspNetCore` 10.1.0 | Swagger UI at `/swagger` |
| Database | PostgreSQL (Npgsql) | Schemas: `analyst`, `app` |
| CSS | Tailwind CSS v4 CLI build | `@tailwindcss/cli` in `LQ/wwwroot/package.json`; compiled to `wwwroot/style_tw.css` |
| Canvas snapshots | `html-to-image` (CDN) | Client-side DOM-to-PNG capture via jsdelivr; 4:3 top-crop aspect, 2× DPR, ~480×360 logical |
| Charting | Chart.js 4.5.1 (local, `node_modules/chart.js/dist/chart.umd.min.js`) | |
| Matrix chart | `chartjs-chart-matrix` 1.3.0 | Loaded from jsDelivr CDN |
| Animation | `web-animations-js` 2.3.2 | Local `node_modules` |
| Fonts | Syne + DM Sans | Google Fonts CDN |

---

## 3. Architecture

### MVC Request Lifecycle

Four controllers; no `[Authorize]` attributes — all auth checks are manual session lookups:

| Controller | Route prefix | Domain |
|---|---|---|
| `HomeController` | `/` | Legacy: now minimal; redirects `/` → `/projects` |
| `ProjectController` | `/projects` | Project CRUD, canvas CRUD, CSV upload/rename/delete, card preview endpoints |
| `WorkspaceController` | `/workspace` | Workspace entry via canvas token, AutoChart, filter, field-type change, sparkline, canvas snapshot save |
| `AuthController` | `/auth` | Sign-in, register, sign-out |

### Data Flow

```
1. Project home
   GET /projects  →  ProjectController.Index()
     → Lists all projects for analyst or guest (paginated)
     → Renders project cards with 4-up canvas/CSV thumbnails

2. Project detail
   GET /projects/{token}  →  ProjectController.Detail({token})
     → Loads Project + all CSVFiles + all Canvases
     → For each CSV card: calls TableRepository.GetCardPreview() for first 5×4 cell data
     → For each Canvas card: serves <img src="/projects/canvas/{token}/preview.png?v={ticks}">
     → Renders item-grid with filter/sort controls

3. Canvas upload to project
   POST /projects/{token}/upload  →  ProjectController.UploadFile({token})
     → Validates CSV file, parses, detects types, bulk-inserts
     → File is assigned ProjectID (no CSVFileID → Canvas mapping yet)
     → Returns JSON; client reloads /projects/{token}

4. Canvas creation in project
   POST /projects/{token}/canvases  →  ProjectController.CreateCanvas({token})
     → Creates new Canvas with ProjectID, auto-assigned PublicToken
     → Returns JSON; client reloads

5. Enter workspace
   GET /workspace/{canvasToken}  →  WorkspaceController.EnterWorkspace({canvasToken})
     → Resolves Canvas + Project via token (no session lookup)
     → Loads Canvas.Document JSON for hydration
     → CSVService.GetTableDisplayLimited() for default/last-used CSV
     → Returns WorkspaceSpecific view with CSV picker dropdown
     → Left pane: #dataTable with CSV picker at top + field list
     → Right pane: #doc-canvas with persisted Document JSON hydrated

6. Canvas save + snapshot
   User edits canvas (add/remove blocks, change text)
     → MutationObserver triggers debounced autosave (1500ms)
     → fetch POST /workspace/canvas/{canvasToken}  (body: {document: JSON.stringify(...)})
     → WorkspaceController.SaveCanvasSnapshot({canvasToken})
       → Stores Canvas.Document JSON
       → Schedules snapshot capture (250ms delay for chart paint)
     → Client: fetch POST /workspace/canvas/{canvasToken}/snapshot  (body: {pngBase64: "..."})
       → WorkspaceController.SaveCanvasSnapshot() stores in Canvas.PreviewPng (bytea)
     → Next visit to /projects/{token} shows updated PNG in canvas card

7. Chart creation
   User drags CSV column header → canvas
     → chart.js:createChartGroup() posts to /workspace/chart (if per-chart filters) or /workspace (if table-wide)
     → AutoChartService detects chart type, returns JSON with ChartType, X/Y/Z labels
     → Chart.js renders inside #doc-canvas
     → Canvas auto-saves (MutationObserver sees new .doc-chart-section)

8. CSV picker in workspace
   User clicks CSV dropdown in left-pane header
     → Selects different CSV from project
     → fetch GET /workspace/{canvasToken}/table?csvToken={token}
     → WorkspaceController.SwitchTableCsv() updates session LastUsedCSVFileID
     → Returns new TableDisplayDTO, JS re-populates #dataTable
     → Chart blocks continue to work (csvFileToken is baked into Document JSON)
```

### Two workspace sections

| Section | HTML element | JS files |
|---|---|---|
| Table / left | `#tablePanel` → `#dataTable` | `main.js`, `filter.js`, `workspace.js` |
| Document canvas / right | `#plotPanel` → `#doc-canvas` | `chart.js`, `editor.js`, `main.js` |

### How backend infers chart type

`AutoChartService.CreateChartAsync()` in `Services/Implementations/AutoChartService.cs`:

1. Load fields for the requested indexes from the DB.
2. Materialise a `rowFilter` (array of surviving row indexes) if filters are active.
3. Classify fields: `categorical`, `numeric`, `identifier` lists.
4. Scale-group numeric fields by median/IQR so compatible fields share a y-axis.
5. Prefetch cardinality (`CountDistinct`) for categorical fields.
6. Iterate field combinations (1-field, 2-field, 3-field) and call typed detect methods:
   - `DetectPieChartAsync`, `DetectSideBarAsync`, `DetectParetoChartAsync` — single categorical
   - `DetectHistogramAsync` — single numeric
   - `DetectBarChartAsync`, `DetectRankedBarAsync`, `DetectBoxPlotAsync` — cat × numeric
   - `DetectTimeSeriesLineAsync`, `DetectTimeSeriesEnhancedAsync`, `DetectCumulativeSumAsync`, `DetectGrowthRateAsync` — date × numeric
   - `DetectMatrixAsync`, `DetectMatrixMeanAsync` — cat × cat
   - `DetectScatter`, `DetectScatterRegression`, `DetectBubbleChart`, `DetectCorrelationMatrix` — numerics (raw row sample)
7. Returns a deduplicated `List<Chart>` with `ChartType` enum (0–10), `X` labels, `Y`/`Z` label-data pairs.

### Razor ↔ JS interaction patterns

- **Strongly-typed model** → View: `@model TableDisplayDTO` passed directly.
- **Inline event handlers** with C# values: `onclick="fieldClicked(@f.Index)"`, `onclick="changeFieldType(event, @f.Index, '@t')"`.
- **`data-*` attributes**: `th[data-index]`, `div[data-spark-col]`, `div[data-spark-type]`, `div[data-field-type]` — JS reads these to avoid round-trips.
- **Session for all cross-request state**: AnalystDTO, CSVFileDTO, TableDTO, ListValueFilterDTO — the workspace has no own state URL.
- **Fetch calls** (not form posts) for: AutoChart, FilterTable, Sparkline, ChangeFieldType.
- **ViewBag**: only `ViewBag.Username` is used (for the nav greeting).

---

## 4. Database Schema

### Schema `analyst`

| Table | Columns | Notes |
|---|---|---|
| `Analyst` | All ASP.NET Identity columns | `IdentityUser` subclass; `UserName`, `Email`, `PasswordHash`, lockout fields |

### Schema `app`

| Table | Key | Notable columns | Relationships |
|---|---|---|---|
| `Project` | `ID` (serial) | `PublicToken` (Guid, unique, external), `Name`, `AnalystID` (nullable FK → Analyst), `GuestToken` (nullable, indexed), `CreatedTime`, `ModifiedTime` | Cascade delete from Analyst; indexes on (AnalystID) and (GuestToken). Container for CSVFiles and Canvases. |
| `CSVFile` | `ID` (serial) | `PublicToken` (Guid, unique), `Name`, `CreatedTime`, `ModifiedTime`, `ProjectID` (FK → Project, required), `AnalystID` (nullable FK → Analyst, denormalized for legacy routes), `GuestToken` (nullable), `Status` (enum), `ProcessingPayload` (jsonb) | FK → Project (cascade delete); composite indexes on (ProjectID, Status) for project-scoped queries. |
| `Canvas` | `ID` (serial) | `PublicToken` (Guid, unique, external), `Name`, `ProjectID` (FK → Project, required), `Document` (jsonb), `PreviewPng` (bytea, nullable, cached snapshot), `LastUsedCSVFileID` (nullable, hints CSV picker), `UpdatedAt` (timestamp) | FK → Project (cascade delete); token-keyed routes for `/workspace/{token}`, `/projects/canvas/{token}/preview.png`. |
| `Field` | `ID` (serial) | `Name`, `Index`, `TypeName` (C# AssemblyQualifiedName), `Categories` (serialised), `SparklineJson` (nullable, camelCase JSON), `SparklineTypeName` (nullable) | FK → CSVFile; unchanged from prior schema. |
| `Value` | **(FieldID, RowIndex)** composite PK | `ValueString`, `ValueStringFormatted`, `Existance` | FK → Field; unchanged from prior schema. |

**Key invariants:**
- `ValueStringFormatted` is what all SQL aggregates read. It is set during upload type detection. For DateTime fields it is a .NET `DateTime.ToString()` US-culture string (`M/D/YYYY H:MM:SS AM/PM`).
- All aggregate SQL in `TableRepository` uses a hand-rolled `ParseDT()` helper (`MAKE_TIMESTAMP` via `SPLIT_PART`) rather than `TO_TIMESTAMP` to handle Unicode space variants (U+202F, U+00A0) that .NET inserts before AM/PM.
- `List<string>` fields store comma-joined items in `ValueStringFormatted`; SQL queries use `LATERAL UNNEST(string_to_array(..., ','))`.

### Raw SQL

All aggregate queries in `TableRepository` are hand-written via `_lqDbContext.Database.SqlQueryRaw<T>()`. LINQ is used only for loading full row sets. Key SQL features used:
- `PERCENTILE_CONT` (median, IQR, Q1/Q3)
- `WIDTH_BUCKET` (histograms)
- `DATE_TRUNC` (time series)
- `LATERAL UNNEST` (list fields)
- `MAKE_TIMESTAMP` via `SPLIT_PART` (DateTime parsing)
- `ANY(ARRAY[...])` (row-filter injection — safe, integer-only)

---

## 5. Implemented Features ✅

### Projects (Phase 1-5)
- **Project CRUD** — Create, read, rename, delete projects. Controller: `ProjectController`, Service: `ProjectService`
- **Project home** — Lists all projects for analyst or guest with 4-up thumbnail grid. View: `Views/Project/Index.cshtml`
- **Project detail** — Grid of CSVs and Canvases in a project; filtering/sorting. View: `Views/Project/Detail.cshtml`
- **CSV card preview** — Server-renders first 5×4 cells with fade-out gradients and row/col counter. Service: `TableRepository.GetCardPreview()`
- **Canvas card preview** — Client-side PNG snapshots (bytea in DB); displayed with `object-fit: cover`, `object-position: top center`. Storage: `Canvas.PreviewPng`
- **CSV/Canvas icon badges** — Line-based SVG icons (table grid for CSV, document for Canvas) in item-info metadata section. View: inline `<svg>` in `_render canvas/csv icon`

### Legacy / Deprecated
- **CSV upload (authenticated)** — Validates CSV (consistent column count, ≥2 rows, ≥1 comma), parses, detects field types, bulk-inserts. Controller: `HomeController.Upload()` [DEPRECATED: use `ProjectController.UploadFile()` now]
- **CSV upload (guest)** — Same but with cookie-based guest token; uses `InterpretSinglePass()` (parallel type detection). Controller: `HomeController.GuestUpload()` [DEPRECATED]
- **Guest cleanup** — Background service purges guest CSVFiles older than 7 days every 6 hours. Service: `GuestCleanupService`
- **File list (legacy)** — Shows all files for current user or guest token. Controller: `HomeController.Show()` [DEPRECATED: use `ProjectController.Index()` → `/projects`]
- **File rename (legacy)** — Authenticated only. Controller: `HomeController.RenameFile()` [DEPRECATED: use `ProjectController.RenameCSV()` or `.RenameCanvas()`]
- **File delete (legacy)** — Authenticated and guest. Controller: `HomeController.Remove()` / `GuestRemove()` [DEPRECATED: use `ProjectController.DeleteCSV()` or `.DeleteCanvas()`]
- **Field type detection** — auto-detects DateTime (30+ format patterns), int, double, bool, List\<string\>, string. Service: `CSVService.DetectType()`
- **Category detection** — Categorical / Numeric / Identifier classification by uniqueness ratio. Service: `CSVService.DetectCategory()`
- **Field type change** — Gear icon → "Change Field Type" submenu. Re-validates existing values, reformats list fields. Controller: `WorkspaceController.ChangeFieldType()`, JS: `workspace.js:changeFieldType()`
- **Column filtering** — Numeric range/exact, string search (partial/exact/exclude), DateTime range, Bool toggle, List search. Controller: `WorkspaceController.FilterTable()`, JS: `filter.js`, View: filter popup HTML in both workspace views
- **Chart refresh on filter** — Applying table filters automatically updates all canvas charts by re-fetching data from the AutoChart endpoint and patching Chart.js instances in-place. No destroy/recreate flash; filters are session-persisted. Debounced 150ms to batch rapid filter clicks. Controller: `WorkspaceController.FilterTable()` emits `onFilterApplied` signal. JS: `filter/api.js` → `filter/reactor.js` → `chart-refresh/index.js`. Note: Box plots are skipped for in-place refresh (BoxPlot plugin captures boxData in closure; only raw data updates work).
- **AutoChart** — Drag column headers onto canvas → fetches chart recommendations. Controller: `WorkspaceController.AutoChart()`, Service: `AutoChartService`, JS: `chart.js:createChartGroup()`
- **Chart drop onto existing chart** — Header drop onto an existing chart card replaces it. JS: `chart.js:replaceChartGroup()`, `main.js:_chartDropOnCard()`
- **Sparklines** — Per-column mini charts in header cells. Controller: `WorkspaceController.SparklineChart()`, JS: inline `<script>` in both workspace views. Results are cached in `Field.SparklineJson` (camelCase JSON) / `Field.SparklineTypeName`; cache is invalidated when the field type changes. `SparklineCancelService` (singleton) cancels in-flight computation for a field when its type is changed mid-request.
- **Zoom overlay** — Click chart → full-screen overlay with option pane. JS: `chart.js:openZoom()`, `closeZoom()`
- **Zoom navigator** — Time series get a range-selector mini chart below the zoom view. JS: `chart.js` navigator section
- **Table panel collapse** — Toggle between full table and field-card mode. JS: inline `onclick` in workspace views
- **Panel resize** — Drag the `#drag` divider to resize left/right panels. JS: `main.js` resizer section
- **Document canvas (Notion-like editor)** — Block-based editor with paragraph, h1, h2, h3, callout, code, divider blocks. Slash command menu (`/h1`, `/code`, etc.), inline formatting toolbar (bold/italic/underline/strikethrough/code/link), block drag-reorder, block handle context menu (Duplicate, Turn into, Delete). JS: `editor.js` (loaded only in `WorkspaceSpecific.cshtml`)
- **Sign in / Sign out** — Session-based. Controller: `AuthController.SignIn()`, `AuthController.SignOut()`
- **Register** — `AuthController.Register()` with password policy enforcement
- **Chart type panel** — In zoom mode, option pane shows chart-type selector with SVG icons. JS: `chart.js` zoom option pane
- **Background color** — Per-chart card background color picker. JS: `chart.js` (colormap UI in hidden Tailwind section of workspace views)
- **Per-chart filters** — Independent filter blocks on canvas for narrowing individual charts without affecting table-wide state. Convert chart to filter via "Turn into" menu or create new filter block. Filter scope is configurable (multi-chart via right panel checkboxes). Filter state persists in canvas; selected values are NOT persisted (resets on reload). Controller: `WorkspaceController.ChartWithInlineFilters()` (POST `/workspace/chart`), JS: `per-chart-filter/{api,convert,events,render,selection,state,toolbar}.js`, Backend: `PerChartRequestDTO`, `ValueFilterDTO` marshalling, `DetectChartWithFiltersAsync()`. Variant selection (buttons vs search) now unlimited (threshold 999999).

---

## 6. Partially Implemented / In Progress ⚠️

- **`AuthController.SignIn()` sign-in result** ⚠️ — Calls `CheckPasswordSignIn()` to validate but after `Register()` also calls `CheckPasswordSignIn()` without actually signing a cookie (ASP.NET Identity cookie auth is not configured — `AddAuthentication()` with no scheme and no cookie handler). Session is set manually; the `SignInManager` call is a no-op for cookie issuance.
- **`Value.Existance` column** ⚠️ — Persisted with default `true` but never queried or toggled. Appears to be scaffolding for a soft-delete / "exclude row" feature.
- **Multi-select block selection** ⚠️ — `editor.js` registers `Shift+click` on blocks (`onSelectionMousedown`) and toggles `block-selected` visually, but no action operates on the multi-selection (copy, delete, etc.).
- **Inline toolbar "Comment" button** ⚠️ — `editor.js` has `if (action === 'comment') return; // future`.
- **`listIndexes` export in `workspace.js`** ⚠️ — Exported but never populated or read in any imported module.
- **Guest cannot rename files** — `HomeController.RenameFile()` returns `"Cannot perform action: analyst not signed in"` for guests; the home view for guests (`Home.cshtml`) shows no rename UI.
- **`zoomOptionPane` chart-type panel in `Workspace.cshtml`** ⚠️ — The pane HTML is present in the guest view (`Workspace.cshtml`) but `editor.js` (which wires up the pane in WorkspaceSpecific) is not loaded for guests. The zoom option pane wiring lives in `chart.js` so it should work for guests too, but not verified.

---

## 7. Future Goals / Not Yet Implemented 🔲

- **PDF export** 🔲 — No export endpoint or library found.
- **Repository export** 🔲 — No self-contained HTML+JS+CSS bundle generation.
- **Notion-like multi-column layout** 🔲 — Editor blocks are single-column only; no column/grid block type.
- **Vega-Lite or D3 integration** 🔲 — Only Chart.js is used; no alternate rendering engine.
- **OAuth / social login** 🔲 — Only username+password Identity; no external provider configuration.
- **Subscription / plan tiers** 🔲 — No plan field on `Analyst`, no Stripe hooks, no feature flags.
- **Usage tracking / analytics** 🔲 — Only request-level `ILogger` calls; no per-user event tracking.
- **Rate limiting** 🔲 — No middleware; upload limit is a `RequestSizeLimit(209715200)` attribute (200 MB).
- **Email confirmation** 🔲 — `RequireUniqueEmail = true` in Identity config but no confirmation flow.
- **Canvas persistence (charts + text blocks)** ✅ — `#doc-canvas` content is serialised to JSON and stored in `Canvas.Document` (jsonb). On workspace load the Razor view injects the saved JSON into `<script id="saved-canvas">` and `canvas-persist/index.js` hydrates it. A debounced `MutationObserver` auto-saves changes via `POST /workspace/canvas/{canvasToken}`; `beforeunload` flushes via `sendBeacon`. JS modules: `canvas-persist/{index,hydrate,autosave,serialize,kinds,state}.js`.
- **Canvas snapshots** ✅ — Client-side PNG rasterization via `html-to-image` CDN after autosave settles (250ms). Captures #doc-canvas at top-cropped 4:3 aspect, 2× DPR (960×720 actual PNG), downscaled to 480×360 logical. Stored as `Canvas.PreviewPng` (bytea). Endpoint: `POST /workspace/canvas/{canvasToken}/snapshot` and `GET /projects/canvas/{canvasToken}/preview.png?v={ticks}`. JS: `canvas-persist/autosave.js:captureAndUploadSnapshot()`.

---

## 8. Conventions

### C\#
- **LQDbContext**: Only used in TableRepository.cs and AnalystRepository, nowhere else. Only allow inheritance of Repositories from Services.
- **Naming**: PascalCase everywhere; no `I`-prefixed fields except interfaces (`ICSVService`). Repository abstract is `TableRepositoryAbstract` (suffix, not prefix).
- **ViewModels / DTOs**: Immutable `record` types in `Models/DTOs/`. Named `*DTO` or `*DisplayDTO`. New: `ProjectDetailDTO` (Project + CSVFiles + Canvases for detail view).
- **Services**: Injected via constructor; `ICSVService` / `IAutoChartService` / `IAnalystService` / `IProjectService` interfaces; concrete in `Services/Implementations/`. No direct use of LQDbContext, inherit from TableRepository or AnalystRepository to use. `ProjectService` inherits `TableRepository` for card preview queries.
- **Repository pattern**: `TableRepositoryAbstract` (abstract class, not interface) → `TableRepository` (concrete). `CSVService` and `ProjectService` inherit `TableRepository` directly. New methods: `UpdateCanvasPreviewPng()`, `GetCanvasPreviewPng()`, `GetCardPreview(csvFileId, rowLimit, colLimit)`.
- **Error handling**: Most action methods catch `Exception` and return `Json(ex.Message)` or `StatusCode(500)`. No global exception middleware.
- **Static factory methods**: `Analyst.GiveBirth(name)` pattern for domain validation.
- **Field type storage**: `Field.TypeName` stores `Type.AssemblyQualifiedName`; `Field.Type` is a computed `[NotMapped]` property that resolves it via `System.Type.GetType()`.

### Razor Views
- No `_Layout.cshtml` — each view is a self-contained full HTML page.
- Model binding: strongly-typed `@model` for all views (`ProjectDetailDTO`, `TableDisplayDTO`, etc.).
- `ViewBag.Username` is the only ViewBag use.
- New Phase 5 views:
  - `Views/Project/Index.cshtml` — Project list (sticky nav, H1 title, "+ New project" button, grid of projects with stagger-fade-in animation).
  - `Views/Project/Detail.cshtml` — Project detail (project header with title, upload/canvas buttons, filter bar, item-grid with CSV/Canvas cards, menu for rename/delete).
  - Inline SVG icons: table grid (CSV) and document (Canvas) in item-info as `.badge-icon` with `currentColor` stroke, 18×18px container.
- Type icons and type labels are generated inline in the view via C# `switch` expressions on `f.TypeName`.
- `@Html.Raw()` used to inject inline SVG paths from C# variables (now includes CSV/Canvas icons).
- `Views/tailwind_classes_in_js.html` — a dummy HTML file with all Tailwind classes used by JavaScript-generated DOM (prevents the Tailwind CLI scanner from purging them).

### JavaScript
- **Module system**: ES modules (`import`/`export`) for `workspace.js`, `chart.js`, `filter.js`. `editor.js` is a self-executing IIFE (no exports).
- **`main.js`** is the entry point; it `import`s from other modules and exposes selected functions to `window` for inline HTML handlers (`Object.assign(window, { fieldClicked, ... })`).
- **Project detail (Detail.cshtml)**: Inline script for filter/sort, card menu toggle, rename/delete, create canvas, project title edit, upload progress toast.
- **Canvas persistence** (`wwwroot/js/canvas-persist/`):
  - `index.js`: DOMContentLoaded orchestrator; hydrates saved canvas on load, starts autosave, schedules post-hydration snapshot.
  - `autosave.js`: Debounced MutationObserver on #doc-canvas (1500ms), auto-saves via `POST /workspace/canvas/{token}`. Captures PNG snapshot 250ms after save: measures #doc-canvas size, top-crops to 4:3 aspect, scales to 480×360 logical, renders at 2× DPR via html-to-image, uploads via `POST /workspace/canvas/{token}/snapshot`.
  - `hydrate.js`: Replays kind.hydrate() for each block in saved Document JSON.
  - `serialize.js`: Walks #doc-canvas DOM, serializes to JSON with title, blocks array.
  - `kinds.js`: KINDS registry (text, divider, chart, textCard) + block-specific serialize/hydrate logic.
  - `state.js`: `isHydrating()` flag to skip autosave during initial load.
- **CSV picker** (`wwwroot/js/workspace/csv-picker.js`): Dropdown in left-pane header to switch CSV; fetches new table data via `GET /workspace/{canvasToken}/table?csvToken={token}`.
- **DOM reads**: JS reads field indexes from `th[data-index]`, spark type from `[data-spark-type]`, filter type from `[data-field-type]` attributes. No hidden inputs or inline JSON blobs.
- **Fetch calls**: All use `fetch()` without a wrapper. JSON body for AutoChart; `FormData` for ChangeFieldType; query params for legacy Remove/Rename.
- **Chart data**: Returned from `/workspace` (POST) or `/workspace/chart` (per-chart) as a JSON-serialised `List<Chart>`. The `ChartType` integer maps to `chartArray` index in `chart.js`: `['pie','bar','line','bar','scatter','polarArea','bar','mixed','matrix','mixedBar','bubble']`.
- **Chart rendering**: `chart.js:createChartGroup(indexes)` posts to the server, receives chart array, creates a `doc-chart-section` wrapper in `#doc-canvas`, and instantiates Chart.js charts inside it.

### CSS / Styling
- **Custom CSS in Detail.cshtml**: No Tailwind; handwritten CSS for Phase 5 project pages.
- **Color variables** (`:root`): `--bg`, `--bg-card`, `--nav-bg`, `--text`, `--text-muted`, `--text-light`, `--accent` (salmon #E9967A), `--border`, `--preview-bg` (off-white #FBFBFD), `--silhouette` (faint grey), `--badge-csv`, `--badge-canvas`, `--danger`.
- **Fonts**: Syne (display, 600-800wt), DM Sans (body, 300-700wt); Google Fonts CDN.
- **Grid**: 4 cols on wide, 3 on medium, 2 on tablet, 1 on mobile; 1.25rem gap.
- **Item cards**: 4:3 preview aspect, flex column layout, item-body with item-info (icon + text) and item-menu.
- **Item-info layout**: flex row with badge-icon (18×18px, color: var(--badge-csv) for CSV, var(--text-muted) for Canvas) and item-info-text (flex column with name + edit date).
- **Badge icons**: SVG with `fill="none"`, `stroke="currentColor"`, `stroke-width="1.8"`. CSV: table grid (box + dividing lines). Canvas: document (rounded box + ruled lines).
- **CSV card preview**: Real data table (first 5×4 cells) with fade-out gradients on right and bottom, "X cols · Y rows" stat in corner.
- **Canvas card preview**: PNG image (`Canvas.PreviewPng`) with `object-fit: cover`, `object-position: top center`, `image-rendering: -webkit-optimize-contrast` for crisp strokes.
- **Animations**: Cards stagger-fade-in on page load (240ms cubic-bezier, 30ms stagger); filter pill switches 180ms color transition; hover state 120ms border-color only (no scale/shadow).
- **No Tailwind in project pages** — Detail.cshtml and Index.cshtml use custom CSS (handwritten; not Tailwind classes). Workspace views reuse existing Tailwind + custom editor CSS.

### EF Core
- **Code-first** with manual migrations.
- Seven migrations in order:
  1. `20260110022414_InitialCreate` — CSVFile, Field, Value tables
  2. `20260421042659_GuestUploadSupport` — AnalystID nullable, GuestToken indexed
  3. `20260429091345_UploadStatusTracking` — PublicToken (Guid), Status (enum), ProcessingPayload (jsonb), composite indexes
  4. `20260502133005_AddCanvasPersistence` — Canvas table (CSVFileID PK, Document jsonb, UpdatedAt)
  5. `20260508035731_AddSparklineJsonToField` — SparklineJson, SparklineTypeName columns on Field
  6. `20260516120905_FixPreviewJsonColumnType` — Renames Canvas.PreviewJson from text to jsonb (intermediate step during Phase 5)
  7. `20260516143725_ReplacePreviewJsonWithPreviewPng` — Adds Project table (PK: ID, PublicToken, AnalystID, GuestToken, Name, timestamps). Adds ProjectID (required FK) to CSVFile and Canvas. Drops CSVFileID from Canvas PK, adds Canvas.ID (identity) + Canvas.PublicToken (Guid unique) + Canvas.Name. Replaces Canvas.PreviewJson with Canvas.PreviewPng (bytea). Adds Canvas.LastUsedCSVFileID (nullable). Backfills all CSVFile/Canvas with default Project per analyst/guest.
- Direct `LQDbContext` injection; no Unit-of-Work wrapper beyond the DbContext.
- Aggregate queries bypass LINQ entirely; only EF Core for inserts/updates/deletes and simple lookups.

### PostgreSQL / Npgsql
- **Schemas**: `analyst` (identity) and `app` (CSV data).
- `PERCENTILE_CONT`, `WIDTH_BUCKET`, `DATE_TRUNC`, `LATERAL UNNEST` used in raw SQL.
- **DateTime quirk**: .NET serialises DateTimes with U+202F (narrow no-break space) before AM/PM on some locales. The `ParseDT()` helper in `TableRepository` normalises these with `REPLACE(col, chr(8239), ' ')`.
- No JSONB or array column types; categories are stored as a serialised list inside EF Core's value converter.
- Row-filter injection uses `ANY(ARRAY[...])` with integer-only values (no SQL injection risk).

---

## 9. Development Setup

### Prerequisites
- .NET 9 SDK
- PostgreSQL running on `localhost:5432`
- Node.js (for Tailwind CLI)

### Connection string

Edit `LQ/appsettings.json` (or use user secrets / environment variables):
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=LQDb;Username=;Password="
}
```

### Restore and run

```bash
# Restore NuGet packages
dotnet restore LQ/LQ.csproj

# Apply EF Core migrations (creates schemas + tables)
dotnet ef database update --project LQ/LQ.csproj

# Run the app (listens on http://0.0.0.0:5000)
dotnet run --project LQ/LQ.csproj
```

### Build Tailwind CSS

```bash
cd LQ/wwwroot
npm install                          # install Tailwind CLI + chart.js + muuri etc.
npx tailwindcss -i style.css -o style_tw.css --watch
# or one-shot:
npx tailwindcss -i style.css -o style_tw.css
```

Tailwind scans all project files (including `LQ/Views/tailwind_classes_in_js.html`) to determine which classes to include.

### EF Core migration commands

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project LQ/LQ.csproj

# Apply pending migrations
dotnet ef database update --project LQ/LQ.csproj
```

### Swagger

Available at `http://localhost:5000/swagger` when running.

---

## 10. Considerations

| Signal | Status |
|---|---|
| ASP.NET Core Identity | ✅ Fully scaffolded (`Analyst : IdentityUser`, password policy, lockout) |
| Guest / anonymous mode | ✅ Cookie-based `lq_guest` token; 7-day TTL; background cleanup |
| User plan / tier fields | ❌ None on `Analyst` |
| Feature flags | ❌ None |
| Usage tracking | ❌ Only `ILogger` calls at `Information` level |
| Stripe / payment hooks | ❌ None |
| Rate limiting middleware | ❌ None (`RequestSizeLimit` attribute only) |
| Email confirmation flow | ❌ `RequireUniqueEmail = true` but no confirm/reset endpoint |
| OAuth / social login | ❌ Not configured |

The codebase is at the "working prototype" stage

---

## 11. Critical Files (Phase 1-5 Project Architecture)

### Models
- `LQ/Models/TableModels/Project.cs` — New entity: `ID` (PK), `PublicToken` (Guid, unique), `Name`, `AnalystID` (nullable FK), `GuestToken` (nullable), `CreatedTime`, `ModifiedTime`. Relationships: `ICollection<CSVFile>`, `ICollection<Canvas>`.
- `LQ/Models/TableModels/Canvas.cs` — Updated: `ID` (identity PK, was CSVFileID), `PublicToken` (Guid, unique), `Name`, `ProjectID` (required FK), `Document` (jsonb), `PreviewPng` (bytea, nullable), `LastUsedCSVFileID` (nullable), `UpdatedAt`.
- `LQ/Models/TableModels/CSVFile.cs` — Updated: Added `ProjectID` (required FK → Project), kept AnalystID (denormalized for legacy routes).
- `LQ/Models/DTOs/ProjectDetailDTO.cs` — New DTO: `Token`, `Name`, `ModifiedTime`, `CSVFiles` (ProjectChildDTO list), `Canvases` (ProjectChildDTO list).
- `LQ/Models/DTOs/ProjectChildDTO.cs` — New DTO: `Token`, `Kind` (ProjectChildKind enum: CSV or Canvas), `Name`, `ModifiedTime`, `Status`, `HasPreview`, `PreviewHeaders[][]`, `PreviewRows[][]`, `FieldCount`, `RowCount`.

### Controllers
- `LQ/Controllers/ProjectController.cs` — New: `Index()`, `Detail()`, `Create()`, `Rename()`, `Delete()`, `UploadFile()`, `CreateCanvas()`, `RenameCSV()`, `DeleteCSV()`, `RenameCanvas()`, `DeleteCanvas()`, `CanvasPreviewPng()` (GET preview image).
- `LQ/Controllers/WorkspaceController.cs` — Updated: `EnterWorkspace(canvasToken)` (was session-keyed), `SaveCanvasSnapshot(canvasToken)`, `SaveCanvasSnapshotPng(canvasToken)` (receives PNG base64), removed legacy session-keyed routes.
- `LQ/Controllers/HomeController.cs` — Stripped to minimal: `Show()` redirects to `/projects`, `GuestUpload()` (legacy, redirects to `/projects`).

### Services
- `LQ/Services/Implementations/ProjectService.cs` — New: inherits `TableRepository`. Methods: CRUD for Project/Canvas; `ResolveAccessibleProject()`, `ResolveAccessibleCanvas()` (with analyst/guest checks); calls `TableRepository.GetCardPreview()` for CSV cell data.
- `LQ/Services/Interfaces/IProjectService.cs` — New interface for ProjectService.
- `LQ/Services/Implementations/CSVService.cs` — Unchanged (inherits TableRepository); still handles upload, type detection, field categorization.
- `LQ/Services/Implementations/AutoChartService.cs` — Unchanged; chart type detection still works with explicit `csvFileToken` resolution.

### Repository
- `LQ/Data/Repositories/TableRepository.cs` — New methods:
  - `UpdateCanvasPreviewPng(canvasId, pngBytes)` — stores bytea in Canvas.PreviewPng.
  - `GetCanvasPreviewPng(canvasId)` — retrieves bytea from Canvas.PreviewPng.
  - `GetCardPreview(csvFileId, rowLimit, colLimit)` — returns string[][] (first N rows, M cols) for CSV card preview.
  - `ResolveCanvasToken(token)` → Canvas — token-keyed lookup.
  - `ResolveProjectToken(token)` → Project — token-keyed lookup.

### Database
- `LQ/Data/Database/LQDbContext.cs` — Updated: Added `DbSet<Project>`, updated `Canvas` key + relationships, added `ProjectID` FK to CSVFile.
- `LQ/Migrations/20260516143725_ReplacePreviewJsonWithPreviewPng.cs` — Creates Project table, adds ProjectID to CSVFile/Canvas, adds Canvas.ID/PublicToken/Name, drops Canvas.CSVFileID key, replaces PreviewJson with PreviewPng (bytea).

### Views
- `LQ/Views/Project/Index.cshtml` — New: Project list page. Sticky nav, H1 title, "+ New project" button, project grid (4 cols wide, responsive). Project cards: thumbnail grid with 4-up recent CSV/Canvas previews, project name, metadata, stagger-fade-in animation.
- `LQ/Views/Project/Detail.cshtml` — New: Project detail page. Header with project name (editable), buttons (Upload CSV, New canvas, Delete project). Filter bar (All/CSVs/Canvases, sort dropdown). Item-grid: CSV and Canvas cards mixed. Each card: preview (CSV table or Canvas PNG), item-info (CSV/Canvas icon + name), metadata (edit date), menu (Rename, Delete). All inline CSS; no Tailwind.

### JavaScript
- `LQ/wwwroot/js/canvas-persist/autosave.js` — Rewritten: MutationObserver triggers POST `/workspace/canvas/{token}`. After success, schedules `captureAndUploadSnapshot()` (250ms delay): measures #doc-canvas, top-crops to 4:3 aspect (srcH = min(scrollHeight, clientWidth * 3/4)), scales to ≤480×360, renders via html-to-image at pixelRatio: 2 (960×720 actual PNG), uploads PNG base64 via POST `/workspace/canvas/{token}/snapshot`.
- `LQ/wwwroot/js/canvas-persist/index.js` — Added: `scheduleSnapshot(900)` call after hydration if canvas has content (ensures snapshot fires even for unedited canvases).
- `LQ/wwwroot/js/workspace/csv-picker.js` — New: Dropdown in left-pane header; on change, fetches `GET /workspace/{token}/table?csvToken={token}`, updates #dataTable.

### Styling
- `LQ/Views/Project/Detail.cshtml` — Contains all CSS for project pages (no Tailwind). Custom `:root` variables, `.item-card`, `.item-preview`, `.badge-icon`, `.item-info`, `.item-body`, `.item-name`, hover states, animations, responsive grid.
- `LQ/wwwroot/style_tw.css` — Unchanged (Tailwind for workspace views only).
