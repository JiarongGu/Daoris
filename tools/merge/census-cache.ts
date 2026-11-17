/**
 * The census of test units kept by the tree it reads, so a second reading of the same tree (the reached run after the
 * test-map gate, a reached run again, the full set's replay) reads it in a second instead of the census's tens. The key is the
 * checkout's content tree (`stagedTree`: every file it holds, committed or not), so any change to a file, the census's own code
 * included, is a new key; `CENSUS_SCHEMA` changes with what a cached census holds. The newest few are kept under `_fixtures/`.
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

export const CENSUS_CACHE = '_fixtures/reached-census';
export const CENSUS_SCHEMA = 1;
const KEEP = 3;

/** The file a census of the content tree `tree` is kept in. */
export const censusKey = (tree: string, schema = CENSUS_SCHEMA): string => `census-${schema}-${tree}.json`;

/**
 * The census of the tree `tree` from the cache under `dir`, or computed (`compute`) and kept there. `keep` names the fields kept
 * (the rest, readers, `revive` gives back); a tree git could not read (`tree` null) is computed and never kept.
 */
export function cachedCensus<M extends Record<string, unknown>>({ dir, tree, compute, keep, revive }: {
  dir: string; tree: string | null; compute: () => M; keep: readonly (keyof M & string)[]; revive: (saved: Partial<M>) => M;
}): { map: M; hit: boolean } {
  if (!tree) return { map: compute(), hit: false };
  const file = join(dir, censusKey(tree));
  if (existsSync(file)) {
    try {
      return { map: revive(JSON.parse(readFileSync(file, 'utf8')) as Partial<M>), hit: true };
    } catch {
      rmSync(file, { force: true });
    }
  }
  const map = compute();
  try {
    mkdirSync(dir, { recursive: true });
    const partial = join(dir, `${censusKey(tree)}.${process.pid}.partial`);
    writeFileSync(partial, JSON.stringify(Object.fromEntries(keep.map((field) => [field, map[field]]))));
    renameSync(partial, file);
    prune(dir, 'census-');
  } catch {
    // A census not kept is read again next time; the run goes on with the one it read.
  }
  return { map, hit: false };
}

/**
 * What the plan derives from the census of `tree` and keeps beside it (`merge/reached-reuse.ts`'s placements of each file a .NET
 * suite's classes may read): read once per tree like the census, written back where the run added to it.
 */
export function treeSide(dir: string, tree: string | null): { value: Record<string, unknown>; save: (value: Record<string, unknown>) => void } {
  const file = tree ? join(dir, `reuse-${CENSUS_SCHEMA}-${tree}.json`) : null;
  let value: Record<string, unknown> = {};
  try {
    if (file && existsSync(file)) value = JSON.parse(readFileSync(file, 'utf8')) as Record<string, unknown>;
  } catch {
    value = {};
  }
  return {
    value,
    save: (next) => {
      if (!file) return;
      try {
        mkdirSync(dir, { recursive: true });
        writeFileSync(`${file}.${process.pid}.partial`, JSON.stringify(next));
        renameSync(`${file}.${process.pid}.partial`, file);
        prune(dir, 'reuse-');
      } catch {
        // Not kept: the next run on this tree derives it again.
      }
    },
  };
}

/** Only the newest `KEEP` files of a kind stay. */
function prune(dir: string, prefix: string): void {
  const kept = readdirSync(dir).filter((name) => name.startsWith(prefix) && name.endsWith('.json'))
    .map((name) => ({ name, at: statSync(join(dir, name)).mtimeMs })).sort((a, b) => b.at - a.at);
  for (const old of kept.slice(KEEP)) rmSync(join(dir, old.name), { force: true });
}
