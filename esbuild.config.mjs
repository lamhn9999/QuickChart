// @ts-check
/**
 * esbuild bolt-on for the wwwroot/js bundle.
 *
 * Why this exists (Phase 10 of /home/lam/.claude/plans/analyze-wwwroot-js-how-would-snug-donut.md):
 *   Phases 0–9 carved the old 6-file pile into ~67 ESM modules served raw. That's
 *   excellent for development (every save reloads instantly, no compile) but in production
 *   3 entry points × 67 files = ~67 HTTP requests just for our JS. This config bundles
 *   each entry point into one minified file under wwwroot/js/dist/.
 *
 * Entry points (the only files Razor loads via <script type="module">):
 *   - js/editor/index.js          — Notion-like block editor (workspace right pane)
 *   - js/workspace/index.js       — table panel + delegated actions + DOM wiring
 *   - js/canvas-persist/index.js  — load/save the canvas state on the workspace view
 *
 * Production switch (NOT auto-applied): edit Workspace.cshtml's three <script> tags from
 *   src="~/js/editor/index.js?v=4"
 * to
 *   src="~/js/dist/editor.min.js"
 * Recommend gating that switch on `app.Environment.IsProduction()` via a Razor helper or
 * `@(env.IsProduction() ? "..." : "...")` ternary — see SMOKE_TEST.md.
 *
 * Externals: nothing. The local code never imports `chart.js` from npm; Chart.js is
 * loaded as a UMD global (`window.Chart`) via a separate <script src="...chart.umd.min.js">.
 *
 * CLI:
 *   node esbuild.config.mjs               # one-shot production build (minified)
 *   node esbuild.config.mjs --watch       # dev mode (no minify, sourcemaps, rebuild on change)
 *   node esbuild.config.mjs --no-minify   # one-shot, no minify (debug)
 */

import { build, context } from 'esbuild';
import { mkdirSync, rmSync } from 'node:fs';
import { resolve } from 'node:path';

const ROOT = resolve(import.meta.dirname);
const SRC = resolve(ROOT, 'LQ/wwwroot/js');
const OUT = resolve(SRC, 'dist');

/** @type {{ name: string, entry: string }[]} */
const BUNDLES = [
  { name: 'editor', entry: resolve(SRC, 'editor/index.js') },
  { name: 'workspace', entry: resolve(SRC, 'workspace/index.js') },
  { name: 'canvas-persist', entry: resolve(SRC, 'canvas-persist/index.js') },
];

const watch = process.argv.includes('--watch');
const minify = !watch && !process.argv.includes('--no-minify');

/**
 * Build options shared by every entry. Per-entry `outfile` is added inside the loops.
 * @returns {import('esbuild').BuildOptions}
 */
function baseOptions() {
  return {
    bundle: true,
    format: 'esm', // keep ESM so Razor's <script type="module"> stays valid
    target: 'es2022',
    splitting: false, // 3 standalone bundles is simpler than shared chunks for this app
    minify,
    sourcemap: true,
    legalComments: 'none',
    treeShaking: true,
    logLevel: 'info',
    // No externals: local code only imports project-relative paths; Chart.js is `window.Chart`.
  };
}

async function buildAll() {
  rmSync(OUT, { recursive: true, force: true });
  mkdirSync(OUT, { recursive: true });
  await Promise.all(
    BUNDLES.map(({ name, entry }) =>
      build({
        ...baseOptions(),
        entryPoints: [entry],
        outfile: resolve(OUT, `${name}.min.js`),
      })
    )
  );
  console.log(`[esbuild] built ${BUNDLES.length} bundles → ${OUT}`);
}

async function watchAll() {
  mkdirSync(OUT, { recursive: true });
  for (const { name, entry } of BUNDLES) {
    const ctx = await context({
      ...baseOptions(),
      entryPoints: [entry],
      outfile: resolve(OUT, `${name}.min.js`),
    });
    await ctx.watch();
  }
  console.log(`[esbuild] watching ${BUNDLES.length} entry points… (Ctrl+C to stop)`);
}

if (watch) await watchAll();
else await buildAll();
