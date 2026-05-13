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

- **No `_Layout.cshtml`** — each view is a self-contained full HTML page.
- **No `[Authorize]`** — all auth is manual session lookups in controllers.
- **No global error middleware** — controllers catch `Exception` broadly, return `Json(ex.Message)`.
- **Services inherit repository abstract classes** (not interfaces). `CSVService` inherits `TableRepository`. Avoid direct `LQDbContext` usage.
- **`TableRepositoryAbstract`** is an abstract class (suffix `Abstract`, not `I` prefix).
- **`Field.TypeName`** stores `Type.AssemblyQualifiedName`; `Field.Type` is a `[NotMapped]` getter via `Type.GetType()`.
- **DTOs** are immutable `record` types in `Models/DTOs/`.
- **Per-chart filters** (`ValueFilterDTO`) are independent of session-scoped table filters. Filter logic (OR for multiple values) must match in both `TableRepository.ApplyFilter()` and inline requests to `/workspace/chart`.
- **Filter variant selection** (buttons vs search) based on cardinality: buttons if ≤ limit, search otherwise. Limit threshold: `NUMERIC_BUTTONS_LIMIT` in frontend (currently 999999 = unlimited).

## JS module architecture

- **3 ES module entry points** (loaded via `<script type="module">`):
  - `js/editor/index.js` — Notion-like block editor (workspace right pane)
  - `js/workspace/index.js` — table panel + actions + DOM wiring
  - `js/canvas-persist/index.js` — canvas load/save
- **Per-chart filter modules** (sub-system):
  - `js/per-chart-filter/{api,convert,events,render,selection,state,toolbar}.js` — independent inline filters on canvas
  - Filters are canvas blocks (like charts/text), persisted in Canvas.Document JSON
  - Filter scope (which charts it applies to) is persisted; selected values are NOT
  - Endpoint: POST `/workspace/chart` accepts `PerChartRequestDTO` with inline `ValueFilterDTO[]`
- **Chart.js is a UMD global** (`window.Chart`) loaded via `<script src="...chart.umd.min.js">`, never imported from npm.
- **`chartArray` index must match `ChartType` enum integer** — off-by-one breaks chart rendering.
- **`chart-refresh/index.js`** handles in-place chart updates when filters change (preserves dataset properties like `fill`, `stack`, `tension`).
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

- Schemas: `analyst` (identity), `app` (CSV data).
- **`ValueStringFormatted`** is what SQL aggregates read (not `ValueString`).
- DateTime values stored as US-culture format (`M/D/YYYY H:MM:SS AM/PM`). .NET may insert **U+202F** before AM/PM — `ParseDT()` in `TableRepository` handles this via `REPLACE(col, chr(8239), ' ')`.
- `List<string>` fields store comma-joined in `ValueStringFormatted`; SQL uses `LATERAL UNNEST(string_to_array(..., ','))`.
- Row-filter injection: `ANY(ARRAY[...])` with integer-only values (safe).

## Tests

- **MSTest** framework (not xUnit/NUnit). `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`.
- **Two patterns**:
  - Real DB via `appsettings.json` (AnalystTest, TableRepositoryTest, ListDetectionTest, AutoChartTest) — requires running Postgres.
  - Testcontainers.PostgreSql (ChangeFieldTypeTest, CanvasPersistenceTest) — requires Docker.
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
| `CLAUDE.md` | Full project overview (comprehensive, keep as reference) |
| `.claude/commands/autochart.md` | Chart system deep-dive (detectors, data contract, chartArray) |
| `SMOKE_TEST.md` | 11-section smoke test checklist covering per-chart filters, chart types, data integrity, persistence |
| `AGENTS.md` | This file — compact, agent-specific gotchas |
