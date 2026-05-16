# AGENTS.md — LQ / ACPTDA

## Quick commands

```bash
# js typecheck (JSDoc+tsc, reads jsconfig.json)
npm run typecheck

# prettier format
npm run format
npm run format:check

# esbuild bundle (3 entry points → wwwroot/js/dist/)
npm run build          # minified
npm run build:watch    # no minify, sourcemaps
npm run build:dev      # one-shot, no minify

# tailwind v4 (run from LQ/wwwroot/)
npx tailwindcss -i style.css -o style_tw.css

# app
dotnet run --project LQ/LQ.csproj                   # listens http://0.0.0.0:5000

# migrations
dotnet ef migrations add <Name> --project LQ/LQ.csproj
dotnet ef database update --project LQ/LQ.csproj

# tests (MSTest, method-level parallel by default)
dotnet test LQ.Tests/LQ.Tests.csproj          # all tests
dotnet test --filter "<TestMethodName>"       # single test
```

## Architecture notes (not obvious from files)

### Legacy (pre-Phase 5)
- **No `_Layout.cshtml`** — each view is a self-contained full HTML page.
- **No `[Authorize]`** — all auth is manual session lookups in controllers.
- **No global error middleware** — controllers catch `Exception` broadly, return `Json(ex.Message)`.
- **Services inherit repository abstract classes** (not interfaces). `CSVService` inherits `TableRepository`. Avoid direct `LQDbContext` usage.
- **`TableRepositoryAbstract`** is an abstract class (suffix `Abstract`, not `I` prefix).
- **`Field.TypeName`** stores `Type.AssemblyQualifiedName`; `Field.Type` is a `[NotMapped]` getter via `Type.GetType()`.
- **DTOs** are immutable `record` types in `Models/DTOs/`.
- **Per-chart filters** (`ValueFilterDTO`) are independent of session-scoped table filters. Filter logic (OR for multiple values) must match in both `TableRepository.ApplyFilter()` and inline requests to `/workspace/chart`.
- **Filter variant selection** (buttons vs search) based on cardinality: buttons if ≤ limit, search otherwise. Limit threshold: `NUMERIC_BUTTONS_LIMIT` in frontend (currently 999999 = unlimited).

### Phase 1-5: Project Architecture
- **Project container** — New entity: `Project` (PK: ID, PublicToken Guid). CSVFile and Canvas are now children of Project (ProjectID required FK). One project per analyst/guest by default (backfilled migration).
- **Canvas decoupled from CSVFile** — Canvas now has independent PK (ID, identity) + PublicToken (Guid, unique). Dropped CSVFileID PK. Canvas.ProjectID is required. Canvas.Document persists independently.
- **Token-keyed routing** — Workspace now at `/workspace/{canvasToken}` (not session-based). Project detail at `/projects/{projectToken}`. Canvas preview PNG at `/projects/canvas/{token}/preview.png?v={ticks}`.
- **CSV picker in workspace** — Left-pane header dropdown to switch active CSV in a canvas (all CSVs in the project). Charts continue to work (csvFileToken baked into Document JSON).
- **Canvas snapshots** — Client-side PNG capture via html-to-image (CDN). Top-cropped 4:3 aspect, 2× DPR, ~480×360 logical. Stored in `Canvas.PreviewPng` (bytea). Captured 250ms after autosave completes. Best-effort (silent fail).
- **New ProjectService** — Inherits `TableRepository`. Methods: Project CRUD, Canvas CRUD, CSV rename/delete, ownership checks via `ResolveAccessibleProject()` / `ResolveAccessibleCanvas()`. Calls `TableRepository.GetCardPreview()` for CSV cell data.
- **New ProjectController** — Routes: `/projects`, `/projects/{token}`, `/projects/upload`, `/projects/canvases`, `/projects/csv/*`, `/projects/canvases/*`.
- **No Tailwind in project pages** — Detail.cshtml and Index.cshtml use custom CSS (handwritten `:root` variables, `.item-card`, `.badge-icon` with SVG). Workspace views still use Tailwind.
- **Badge icons** — CSV/Canvas type indicators now SVG icons (18×18px, stroke-only) instead of text badges. Table grid (CSV), document (Canvas). Positioned in item-info metadata section, left side.

## JS module architecture

### Core entry points (3 ES modules via `<script type="module">`)
- **`js/editor/index.js`** — Notion-like block editor (workspace right pane). IIFE, no exports.
- **`js/workspace/index.js`** — table panel + CSV picker + actions + DOM wiring. Exports functions to `window` for inline handlers.
- **`js/canvas-persist/index.js`** — canvas load/save orchestrator. Calls hydrate, starts autosave, schedules post-hydration snapshot.

### Canvas persistence (`js/canvas-persist/`) — Phase 5 update
- **`autosave.js`** — Debounced MutationObserver (1500ms) on #doc-canvas. On fire: `POST /workspace/canvas/{token}` with `{document: stringified JSON}`. On success: `scheduleSnapshot(250)` to delay PNG capture 250ms (let chart animations paint).
- **`captureAndUploadSnapshot()`** — Measures #doc-canvas clientWidth/scrollHeight. Source rect = min(scrollHeight, width * 3/4) (top-cropped to 4:3 aspect). Scales to ≤480×360 logical. Calls `html-to-image.toPng()` with `pixelRatio: 2` (960×720 actual PNG). Encodes to base64, `POST /workspace/canvas/{token}/snapshot` with `{pngBase64: string}`. Silent fail if library missing or network blip.
- **`hydrate.js`** — Replays `kind.hydrate()` for each block in saved Document JSON. After hydrate, triggers `scheduleSnapshot(900)` for initial thumbnail (if canvas has content).
- **`kinds.js`** — KINDS registry: text, divider, chart, textCard. Each kind has `serialize()` and `hydrate()` methods. Chart blocks carry explicit `csvFileToken` for cross-CSV support.
- **`state.js`** — `isHydrating()` flag. Autosave disabled during initial hydration to prevent double-save.

### Per-chart filter modules (sub-system)
- **`js/per-chart-filter/{api,convert,events,render,selection,state,toolbar}.js`** — independent inline filters on canvas.
- Filters are canvas blocks (like charts/text), persisted in Canvas.Document JSON.
- Filter scope (which charts it applies to) is persisted; selected values are NOT.
- Endpoint: POST `/workspace/chart` accepts `PerChartRequestDTO` with inline `ValueFilterDTO[]`.

### Chart.js integration
- **Chart.js is a UMD global** (`window.Chart`) loaded via `<script src="...chart.umd.min.js">`, never imported from npm.
- **`chartArray` index must match `ChartType` enum integer** — off-by-one breaks chart rendering.
- **`chart-refresh/index.js`** handles in-place chart updates when filters change (preserves dataset properties like `fill`, `stack`, `tension`).

### JavaScript tooling
- TypeScript via **JSDoc + `// @ts-check`** only (no `.ts` files). Config at `LQ/wwwroot/jsconfig.json`.
- Typecheck should be **0 errors under `strict: true`**.

## Per-chart filter gotchas

- **`selectedValues` NOT persisted** — only filter field + scope (`appliedTo`) persist across reload. Selected pills/chips reset.
- **Filter variant logic** — buttons if cardinality ≤ threshold, search otherwise. Threshold in `per-chart-filter/convert.js` (`NUMERIC_BUTTONS_LIMIT = 999999`).
- **Chart refresh on filter** — `chart-refresh/index.js:applySampleToInstance()` patches Chart.js in-place. Must preserve dataset properties (`fill`, `stack`, `tension`, `type`) by copying from template. New datasets inherit from first dataset's config.
- **Scatter/bubble colors** — explicitly set `borderColor` to palette color (not transparent) so Chart.js doesn't apply grey default. Keep `borderWidth: 1`.
- **Filter scope updates chart immediately** — toggle in right panel fires `replayFiltersFor()` without requiring "Apply" button.

## Tailwind v4 quirks

- **Zero-config** — no `tailwind.config.js`. Scans all project files automatically.
- **source** directive in `style.css`: `@import "tailwindcss" source("../Views/");`
- **JS-generated DOM classes** preserved from purge in `Views/tailwind_classes_in_js.html`.

## Database quirks

### Schema
- Schemas: `analyst` (identity), `app` (CSV data).
- **Phase 5 change**: New `Project` table (PK: ID, unique PublicToken). `CSVFile` and `Canvas` have required `ProjectID` FK to Project (cascade delete).
- **Canvas changes**: Now has identity PK (ID), unique PublicToken (for routing), Name, ProjectID (required), Document (jsonb), PreviewPng (bytea, nullable snapshot), LastUsedCSVFileID (nullable, hints CSV picker), UpdatedAt. Dropped CSVFileID as PK (was one-to-one with CSVFile).

### Data handling
- **`ValueStringFormatted`** is what SQL aggregates read (not `ValueString`).
- DateTime values stored as US-culture format (`M/D/YYYY H:MM:SS AM/PM`). .NET may insert **U+202F** before AM/PM — `ParseDT()` in `TableRepository` handles this via `REPLACE(col, chr(8239), ' ')`.
- `List<string>` fields store comma-joined in `ValueStringFormatted`; SQL uses `LATERAL UNNEST(string_to_array(..., ','))`.
- Row-filter injection: `ANY(ARRAY[...])` with integer-only values (safe).

### Snapshot storage
- **`Canvas.PreviewPng`** is bytea (binary BLOB). Phase 5 migration replaced old `PreviewJson` text column.
- Served via `GET /projects/canvas/{token}/preview.png?v={ticks}`. Ticks = `UpdatedAt.Ticks` for cache busting.
- When Canvas.Document is saved, snapshot is captured asynchronously and stored separately. No realtime push needed; next project detail view shows latest PNG.

## Tests

- **MSTest** framework (not xUnit/NUnit). `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`.
- **Two patterns**:
  - Real DB via `appsettings.json` (AnalystTest, ListDetectionTest, AutoChartTest) — requires running Postgres.
  - Testcontainers.PostgreSql (ChangeFieldTypeTest, CanvasPersistenceTest) — requires Docker.
- **`CanvasPersistenceTest`** (Phase 5) — Tests canvas Document JSON round-trip: serialize block kinds (text, chart, divider, textCard), hydrate from JSON, verify structure preserved. All 11 tests pass.
- **`TableRepositoryTest.CSV_FILE_ID = 34`** is hardcoded — must match a real CSVFile in local DB.
- Fixtures in `LQ.Tests/Fixtures/` copied to output dir.

## Git & config

- **`LQ/appsettings.json` is gitignored** — copy template from `LQ/appsettings.Development.json` or the CLAUDE.md example.
- **Two `node_modules/` trees**: root (`package.json`, dev tooling, gitignored) and `LQ/wwwroot/` (runtime deps, git-tracked because Razor serves chart.js from it).
- **`wwwroot/js/dist/` is gitignored** — regenerated by `npm run build`.
- No CI/CD (no `.github/` workflows).

## Existing instruction files

| File | Content |
|---|---|
| `CLAUDE.md` | Full project overview (Phase 1-5 architecture: Project entity, Canvas decoupling, snapshot system, new controllers/services, migrations, CSS styling, JS modules). See section 11 "Critical Files" for code layout. |
| `.claude/commands/autochart.md` | Chart system deep-dive (detectors, data contract, chartArray) |
| `SMOKE_TEST.md` | 11-section smoke test checklist covering per-chart filters, chart types, data integrity, persistence |
| `AGENTS.md` | This file — compact, agent-specific gotchas + Phase 1-5 updates |
