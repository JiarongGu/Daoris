/**
 * The one filesystem helper the tooling shares. It existed five times — both rehearsals, the package
 * stager, and the web e2e host — each copy carrying the same one-line justification.
 */
import { copyFileSync, existsSync, mkdirSync, readFileSync, readdirSync, realpathSync, renameSync } from 'node:fs';
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

/** The refusals that mean something still holds a file in the way, and give way once it lets go. */
const HELD = new Set(['EPERM', 'EACCES', 'EBUSY']);

/**
 * A rename tried again while a held file refuses it (UPDATE1's stage). On Windows a folder of executables written a
 * moment ago cannot be renamed while something still holds a file in it, the virus scanner most often, and the refusal
 * is EPERM; it gives way within seconds. Any other failure throws at once, and the last refusal throws after the tries.
 */
export function renameHeld(from, to, { tries = 50, waitMs = 200, rename = renameSync } = {}) {
  for (let attempt = 1; ; attempt++) {
    try {
      rename(from, to);
      return;
    } catch (error) {
      if (attempt >= tries || !HELD.has(error?.code)) throw error;
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, waitMs);
    }
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

/** Every file under `root`, as `/`-separated paths relative to it. */
function filesUnder(root, at = '') {
  if (!existsSync(join(root, at))) return [];
  return readdirSync(join(root, at), { withFileTypes: true }).flatMap((entry) => {
    const path = at ? `${at}/${entry.name}` : entry.name;
    return entry.isDirectory() ? filesUnder(root, path) : [path];
  });
}

/**
 * The files two trees disagree on, sorted: different bytes, or present on one side only. How a gate
 * asks "would this change anything?" of a copy, so that asking never repairs the tree being judged.
 */
export function treeDiff(left, right) {
  const leftFiles = new Set(filesUnder(left));
  const rightFiles = new Set(filesUnder(right));
  return [...new Set([...leftFiles, ...rightFiles])].sort().filter((path) =>
    !leftFiles.has(path) || !rightFiles.has(path)
      || !readFileSync(join(left, path)).equals(readFileSync(join(right, path))));
}
