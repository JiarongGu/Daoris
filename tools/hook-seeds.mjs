/**
 * Seeds for the tests that run real git with the commit and checkout hooks installed (`commit-checks.test.mjs`, `index-hook.test.mjs`):
 * a repository built once for each shape and copied for each case. On this machine `git init`, a base commit and the hook's install
 * cost most of a case, and a case's own subject (one hooked commit or merge) the rest, so what a case shares is built once.
 *
 *   const kit = seeds(scratchFolder);
 *   const dir = kit.copy(JSON.stringify(files), (seed) => { ...init, write, commit, install in `seed`... });
 *
 * A seed is built where a case first asks its key, in the kit's own folder, and every case works in a copy of its own; a seed is
 * never handed out. The copy is the plain recursive one (`copyTree`), since git writes its objects read-only and the hooks and the
 * driver's configuration name tools by absolute path, which a copy keeps.
 */
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { copyTree } from './fsx.mjs';

let kits = 0;

/** A kit under `scratch`: `copy(key, build)` is a folder holding what `build(seedFolder)` made, built once for each key. */
export function seeds(scratch) {
  const home = join(scratch, `kit${kits += 1}`);
  const built = new Map();
  let count = 0;
  return {
    /** How many folders this kit has handed out. */
    get count() {
      return count;
    },
    copy(key, build) {
      let seed = built.get(key);
      if (seed === undefined) {
        seed = join(home, `seed${built.size + 1}`);
        mkdirSync(seed, { recursive: true });
        build(seed);
        built.set(key, seed);
      }
      const dir = join(home, `r${count += 1}`);
      copyTree(seed, dir);
      return dir;
    },
  };
}
