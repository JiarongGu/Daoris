/**
 * The one filesystem helper the tooling shares. It existed five times — both rehearsals, the package
 * stager, and the web e2e host — each copy carrying the same one-line justification.
 */
import { copyFileSync, mkdirSync, readdirSync, realpathSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Whether the module at `url` is the script node was asked to run — the runner guard every tool whose
 * helpers are also imported needs, in one place (REV3; it existed in three forms).
 *
 * Compared as REAL paths. Node resolves the main module's links, so through a junction or a symlink
 * `import.meta.url` is the real path and `process.argv[1]` the one typed: the guard read false, and a
 * gate ran nothing and exited 0.
 */
export function isMain(url) {
  const invoked = process.argv[1];
  if (!invoked) return false;
  try {
    return realpathSync(invoked) === realpathSync(fileURLToPath(url));
  } catch {
    return false;
  }
}

/** Recursive copy. Deliberately not fs.cpSync — it has crashed on this platform. */
export function copyTree(from, to) {
  mkdirSync(to, { recursive: true });
  for (const entry of readdirSync(from, { withFileTypes: true })) {
    const source = join(from, entry.name);
    const target = join(to, entry.name);
    if (entry.isDirectory()) copyTree(source, target);
    else copyFileSync(source, target);
  }
}
