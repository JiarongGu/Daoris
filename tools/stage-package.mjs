#!/usr/bin/env node
/**
 * Stage the workspace-root files that must ship INSIDE the CLI package — and clean them up again.
 *
 * Three files live at the root because they describe the project rather than the
 * CLI — the canon (data the service and its clients will read too), the licence,
 * and the README. npm cannot reach outside a package directory: `files` is
 * package-relative, and README/LICENSE are only picked up from the package root.
 * Left alone, the restructure would have shipped a package with no licence text
 * and no readme, and D11's "the canon ships inside the package" would have
 * silently stopped being true.
 *
 * The root copies are the source of truth; these are gitignored and rebuilt at
 * pack time. Nothing edits them, and `upstream` writes to the root canon through
 * the same resolution the CLI uses.
 *
 * Run automatically by the CLI package's `prepack`, and with `--clean` by `postpack`.
 *
 * The cleanup is not tidiness. `resolveCanonRoot` decides by `node_modules`, so a leftover canon can
 * no longer shadow the real tree in a dev checkout — but everything staged is gitignored build
 * output, invisible to `git status`, and `prepack` also runs `npm run build`, whose `dist/` the bin
 * PREFERS over the sources. A stale `dist/` outliving the pack is exactly how DRV5's remote landing
 * ran month-old code through every bin-driven gate while `node --test` stayed green (FIX-LOG
 * 2026-09-20) — so `postpack` removes everything a pack creates, dist/ included, and the dev loop
 * goes back to needing no build at all.
 */
import { copyFileSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { copyTree } from './fsx.mjs';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const pkgRoot = join(repoRoot, 'src', 'Daoris.Cli');

const STAGED = ['canon', 'LICENSE', 'README.md'];

if (process.argv.includes('--clean')) {
  for (const name of [...STAGED, 'dist']) {
    rmSync(join(pkgRoot, name), { recursive: true, force: true });
  }
  console.error('stage-package: removed the staged files and dist/ — the dev loop needs no build');
  process.exit(0);
}

rmSync(join(pkgRoot, 'canon'), { recursive: true, force: true });
copyTree(join(repoRoot, 'canon'), join(pkgRoot, 'canon'));
for (const file of ['LICENSE', 'README.md']) {
  copyFileSync(join(repoRoot, file), join(pkgRoot, file));
}
// stderr, not stdout: this runs as `prepack`, and `npm pack --json` emits the
// file manifest on stdout — anything else written there is parsed as JSON.
console.error('stage-package: staged canon/, LICENSE and README.md into src/Daoris.Cli');
