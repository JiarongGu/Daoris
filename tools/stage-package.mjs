#!/usr/bin/env node
/**
 * Stage the workspace-root files that must ship INSIDE the CLI package.
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
 * Run automatically by the CLI package's `prepack`, and REMOVED again by `postpack`.
 *
 * That cleanup is not tidiness. `resolveCanonRoot` prefers a canon beside the package, because that is
 * where the published one lives — so a staged copy left behind silently shadows the real tree, and
 * every later development command reads a stale canon. It is gitignored, so nothing shows it. This
 * project's own pathology, in its own build: two copies, one quietly winning.
 */
import { copyFileSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const pkgRoot = join(repoRoot, 'src', 'Daoris.Cli');

/** Deliberately not fs.cpSync — it has crashed on this platform. */
function copyTree(source, dest) {
  mkdirSync(dest, { recursive: true });
  for (const entry of readdirSync(source, { withFileTypes: true })) {
    const a = join(source, entry.name);
    const b = join(dest, entry.name);
    if (entry.isDirectory()) copyTree(a, b);
    else copyFileSync(a, b);
  }
}

rmSync(join(pkgRoot, 'canon'), { recursive: true, force: true });
copyTree(join(repoRoot, 'canon'), join(pkgRoot, 'canon'));
for (const file of ['LICENSE', 'README.md']) {
  copyFileSync(join(repoRoot, file), join(pkgRoot, file));
}
// stderr, not stdout: this runs as `prepack`, and `npm pack --json` emits the
// file manifest on stdout — anything else written there is parsed as JSON.
console.error('stage-package: staged canon/, LICENSE and README.md into src/Daoris.Cli');
