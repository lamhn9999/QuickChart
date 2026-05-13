# CLAUDE.md — LQ / ACPTDA

## 1. Project Overview

This is a data-storytelling web application that lets users upload CSV files and instantly generate rich, auto-detected charts without writing any code. The product vision is "Notion meets Tableau": a split-pane workspace where the left side shows the raw data table and the right side is a Notion-like document canvas where users drop column headers to summon charts, then annotate them with text, headings, and callouts. It supports both authenticated analysts and anonymous guests (cookie-based, 7-day ephemeral session).

---

## 2. Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| Runtime | .NET 9 / ASP.NET Core MVC | `net9.0`, nullable enabled, implicit usings |
| Language | C# 13 | |
| ORM | EF Core 9 (`Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4) | Code-first, five applied migrations |
| Bulk insert | `EFCore.BulkExtensions` 9.0.2 | Used for initial CSV value insert and bulk-update list fields |
| Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 9.0.11 | `IdentityUser` subclass `Analyst`; session-based, no JWT |
| API docs | `Swashbuckle.AspNetCore` 10.1.0 | Swagger UI at `/swagger` |
| Database | PostgreSQL (Npgsql) | Schemas: `analyst`, `app` |
| CSS | Tailwind CSS v4 CLI build | `@tailwindcss/cli` in `LQ/wwwroot/package.json`; compiled to `wwwroot/style_tw.css` |
| Charting | Chart.js 4.5.1 (local, `node_modules/chart.js/dist/chart.umd.min.js`) | |
| Matrix chart | `chartjs-chart-matrix` 1.3.0 | Loaded from jsDelivr CDN |
| Animation | `web-animations-js` 2.3.2 | Local `node_modules` |
| Fonts | Syne + DM Sans | Google Fonts CDN |

---

## 3. Architecture

### MVC Request Lifecycle

Three controllers; no `[Authorize]` attributes — all auth checks are manual session lookups:

| Controller | Route prefix | Domain |
|---|---|---|
| `HomeController` | `/` | Home page, CSV upload/delete/rename, file selection, guest support |
| `WorkspaceController` | `/workspace` | Workspace entry, AutoChart, filter, field-type change, sparkline |
| `AuthController` | `/auth` | Sign-in, register, sign-out |

### Data Flow

```
1. Upload
   POST /  ← IFormFile
   HomeController.Upload() → CSVService.ProcessUploaded()
     → CSVService.Interpret()   — parse CSV, detect types (int/double/DateTime/bool/List<string>/string)
     → CSVService.DetectType()  — try date formats, numeric, bool, list heuristics
     → CSVService.DetectCategory() — Categorical / Numeric / Identifier classification
     → TableRepository.Insert()  — EFCore insert CSVFile+Fields, BulkInsert Values

2. Open file
   POST /table?filename=...  →  HomeController.Select()
     → stores CSVFileDTO in session

3. Enter workspace
   GET /workspace  →  WorkspaceController.EnterWorkspace()
     → CSVService.GetTableDisplayLimited(file, 200 rows)
     → returns TableDisplayDTO → View("WorkspaceSpecific", dto)
     → View serialises field metadata directly into HTML th[data-index] attributes

4. Chart creation (browser side)
   User clicks/drags column header → JS collects field indexes in pendingIndexes (Set)
   Drop on canvas → main.js → chart.js:createChartGroup(indexes)
     → fetch POST /workspace  (body: JSON array of field indexes)
     → WorkspaceController.AutoChart() → AutoChartService.DetectChartAsync()
       → SQL aggregates via TableRepository.SqlQueryRaw(...)
       → returns List<Chart> JSON
     → chart.js renders Chart.js instances inside #doc-canvas

5. Filtering
   User configures filter popup → filter.js → fetch POST /workspace/filter
     → WorkspaceController.FilterTable() → stores filters in session
     → `onFilterApplied` event fires
     → `filter/reactor.js` debounces call to `refreshAllCharts()`
     → For each chart group, re-fetches from POST /workspace with same field indexes
     → Updates Chart.js instances in-place (labels + data), calls `chart.update('none')`
     → subsequent AutoChart calls also use filters

6. Sparklines
   On page load each th[data-spark-col] hits GET /workspace/sparkline/{fieldIndex}
     → returns single-field Chart for mini canvas rendering
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
| `CSVFile` | `ID` (serial) | `PublicToken` (Guid, unique, external facing ID), `Name`, `CreatedTime`, `ModifiedTime`, `AnalystID` (nullable FK → Analyst), `GuestToken` (nullable, indexed), `Status` (UploadStatus enum: 0=Failed, 1=Completed, 2=Processing), `ProcessingPayload` (jsonb, nullable, error/metadata on fail) | Cascade delete from Analyst; composite indexes on (AnalystID, Status) and (GuestToken, Status) for queries |
| `Canvas` | `CSVFileID` (serial, PK) | `Document` (jsonb), `UpdatedAt` (timestamp) | FK → CSVFile (cascade delete); one-to-one, stores persisted chart+text blocks |
| `Field` | `ID` (serial) | `Name`, `Index`, `TypeName` (C# AssemblyQualifiedName), `Categories` (serialised), `SparklineJson` (nullable, camelCase JSON), `SparklineTypeName` (nullable, mirrors `TypeName` at cache-write time) | FK → CSVFile |
| `Value` | **(FieldID, RowIndex)** composite PK | `ValueString` (raw from CSV), `ValueStringFormatted` (normalised for queries), `Existance` (bool, default true) | FK → Field |

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

- **CSV upload (authenticated)** — Validates CSV (consistent column count, ≥2 rows, ≥1 comma), parses, detects field types, bulk-inserts. Controller: `HomeController.Upload()`, Service: `CSVService.ProcessUploaded()`
- **CSV upload (guest)** — Same but with cookie-based guest token; uses `InterpretSinglePass()` (parallel type detection). Controller: `HomeController.GuestUpload()`, Service: `CSVService.ProcessUploadedGuest()`
- **Guest cleanup** — Background service purges guest CSVFiles older than 7 days every 6 hours. Service: `GuestCleanupService`
- **File list** — Shows all files for current user or guest token. Controller: `HomeController.Show()`, Views: `Views/Home/NewHomeSpecific.cshtml`, `Views/Home/Home.cshtml`
- **File rename** — Authenticated only. Controller: `HomeController.RenameFile()`, JS: `Views/Home/NewHomeSpecific.cshtml` inline script
- **File delete** — Authenticated (`HomeController.Remove()`) and guest (`HomeController.GuestRemove()`), JS: home view inline script
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
- **Canvas persistence (charts + text blocks)** ✅ — `#doc-canvas` content is serialised to JSON and stored in `app.CanvasDocument` (per-CSVFile). On workspace load the Razor view injects the saved JSON into `<script id="saved-canvas">` and `canvas-persist/index.js` hydrates it. A debounced `MutationObserver` auto-saves changes; `beforeunload` flushes via `sendBeacon`. Endpoints: `GET /workspace/canvas`, `POST /workspace/canvas`, `POST /workspace/canvas/append-chart`. JS modules: `canvas-persist/{index,hydrate,autosave,serialize,kinds,state}.js`.

---

## 8. Conventions

### C\#
- **LQDbContext**: Only used in TableRepository.cs and AnalystRepository, nowhere else. Only allow inheritance of Repositories from Services.
- **Naming**: PascalCase everywhere; no `I`-prefixed fields except interfaces (`ICSVService`). Repository abstract is `TableRepositoryAbstract` (suffix, not prefix).
- **ViewModels / DTOs**: Immutable `record` types in `Models/DTOs/`. Named `*DTO` or `*DisplayDTO`.
- **Services**: Injected via constructor; `ICSVService` / `IAutoChartService` / `IAnalystService` interfaces; concrete in `Services/Implementations/`. No direct use of LQDbContext, inherit from TableRepository or AnalystRepository to use.
- **Repository pattern**: `TableRepositoryAbstract` (abstract class, not interface) → `TableRepository` (concrete). `CSVService` inherits `TableRepository` directly.
- **Error handling**: Most action methods catch `Exception` and return `Json(ex.Message)` or `StatusCode(500)`. No global exception middleware.
- **Static factory methods**: `Analyst.GiveBirth(name)` pattern for domain validation.
- **Field type storage**: `Field.TypeName` stores `Type.AssemblyQualifiedName`; `Field.Type` is a computed `[NotMapped]` property that resolves it via `System.Type.GetType()`.

### Razor Views
- No `_Layout.cshtml` — each view is a self-contained full HTML page.
- Model binding: strongly-typed `@model` for all views (`TableDisplayDTO`, `List<CSVFile>`).
- `ViewBag.Username` is the only ViewBag use.
- Type icons and type labels are generated inline in the view via C# `switch` expressions on `f.TypeName`.
- `@Html.Raw()` used to inject inline SVG paths from C# variables.
- `Views/tailwind_classes_in_js.html` — a dummy HTML file with all Tailwind classes used by JavaScript-generated DOM (prevents the Tailwind CLI scanner from purging them).

### JavaScript
- **Module system**: ES modules (`import`/`export`) for `workspace.js`, `chart.js`, `filter.js`. `editor.js` is a self-executing IIFE (no exports).
- **`main.js`** is the entry point; it `import`s from other modules and exposes selected functions to `window` for inline HTML handlers (`Object.assign(window, { fieldClicked, ... })`).
- **DOM reads**: JS reads field indexes from `th[data-index]`, spark type from `[data-spark-type]`, filter type from `[data-field-type]` attributes. No hidden inputs or inline JSON blobs.
- **Fetch calls**: All use `fetch()` without a wrapper. JSON body for AutoChart; `FormData` for ChangeFieldType; query params for Remove/Rename.
- **Chart data**: Returned from `/workspace` (POST) as a JSON-serialised `List<Chart>`. The `ChartType` integer maps to `chartArray` index in `chart.js`: `['pie','bar','line','bar','scatter','polarArea','bar','mixed','matrix','mixedBar','bubble']`.
- **Chart rendering**: `chart.js:createChartGroup(indexes)` posts to the server, receives chart array, creates a `doc-chart-section` wrapper in `#doc-canvas`, and instantiates Chart.js charts inside it.

### Tailwind
- **Version 4 CLI build** (not CDN, not PostCSS). Run via `npx tailwindcss` in `LQ/wwwroot/`.
- Input: `LQ/wwwroot/style.css`. Output: `LQ/wwwroot/style_tw.css`.
- `tailwind.config.js` does not exist (Tailwind v4 is zero-config by default; it scans all files in the project).
- Inline JS-generated classes that would be purged are preserved in `LQ/Views/tailwind_classes_in_js.html`.

### EF Core
- **Code-first** with manual migrations.
- Five migrations in order:
  1. `20260110022414_InitialCreate` — CSVFile, Field, Value tables
  2. `20260421042659_GuestUploadSupport` — AnalystID nullable, GuestToken indexed
  3. `20260429091345_UploadStatusTracking` — PublicToken (Guid), Status (enum), ProcessingPayload (jsonb), composite indexes
  4. `20260502133005_AddCanvasPersistence` — Canvas table (CSVFileID PK, Document jsonb, UpdatedAt)
  5. `20260508035731_AddSparklineJsonToField` — SparklineJson, SparklineTypeName columns on Field
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
