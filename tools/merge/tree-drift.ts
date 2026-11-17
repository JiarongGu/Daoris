/**
 * The paths two trees differ by, asked of git once per pair and kept. A judgement of the stored verdicts compares the checkout's
 * tree with the tree of every verdict, hundreds of trees, and a git start for each costs minutes. Every pair a judgement lacks is
 * asked in one `git diff-tree --stdin` a batch, and since a tree never changes, the answer of a pair of
 * tree ids holds for ever: it is kept under the verdict store's checkout (`DRIFT_CACHE`), a file per judged tree (`to`), each
 * pair by its other tree (`from`), the paths in one table. The newest few judged trees stay, as the census cache keeps its own.
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { git as gitIn, isIgnored } from './git.ts';
import { zPaths } from './paths.ts';

export const DRIFT_CACHE = 'local/scratch/tree-drift';
export const DRIFT_SCHEMA = 1;
/** How many pairs one git start is asked: a pair a month apart differs by every path, about half a megabyte of names. */
export const DRIFT_BATCH = 64;
const KEEP = 8;

/** The tool's git in `root`: its status and its output, never refusing. */
export type DriftGit = (args: string[], input?: string) => { status: number | null; out: string };
type Kept = { paths: string[]; pairs: Record<string, number[]> };

/** The file the paths every tree differs from the judged tree `to` by are kept in. */
export const driftFile = (to: string, schema = DRIFT_SCHEMA): string => `drift-${schema}-${to}.json`;

const HEADER = /^([0-9a-f]{40,64} [0-9a-f]{40,64})\n/;

/**
 * The answers of `git diff-tree --stdin -r -z --name-only --always`, by the pair each echoes (`from to`): the pair's line, then
 * its paths each ended by a NUL, in git's order, each read as `zPaths` reads one pair's. A pair git cannot read (a tree it lacks) is
 * passed over without a word, so it is absent.
 */
export function readPairs(out: string): Map<string, string[]> {
  const pairs = new Map<string, string[]>();
  let current: string[] | null = null;
  for (let piece of out.split('\0')) {
    for (let header = HEADER.exec(piece); header; header = HEADER.exec(piece)) {
      current = [];
      pairs.set(header[1]!, current);
      piece = piece.slice(header[0].length);
    }
    if (piece.trim() && current) current.push(piece.trim());
  }
  return pairs;
}

const isId = (tree: string): boolean => /^[0-9a-f]{40,64}$/.test(tree);

/**
 * The paths each tree differs from `to` by. `ask` takes every tree a judgement will compare at once: the pairs kept in `dir` are
 * read, the rest asked of git a batch a start, and what git answered kept, where `writable` says the folder may be written (it
 * is ignored, so a kept file changes no tree). `drift` answers one pair, asking it alone where `ask` did not: `[]` for the
 * tree itself, null where there is no tree or git lacks it, as `git diff` of the pair fails.
 */
export function treeDrifts({ to, git, dir, writable }: { to: string | null; git: DriftGit; dir: string; writable: () => boolean }): {
  ask: (froms: readonly (string | null | undefined)[]) => void; drift: (from: string | null | undefined) => string[] | null;
} {
  const known = new Map<string, string[] | null>();
  const file = to ? join(dir, driftFile(to)) : null;
  let read = false;
  const readKept = (): void => {
    read = true;
    try {
      const kept = file && existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) as Kept : null;
      for (const [from, indices] of Object.entries(kept?.pairs ?? {})) known.set(from, indices.map((index) => kept!.paths[index]!));
    } catch {
      // A file that does not read is asked again, and written over.
    }
  };
  const ask = (froms: readonly (string | null | undefined)[]): void => {
    if (!to) return;
    if (!read) readKept();
    const wanted = [...new Set(froms)].filter((from): from is string => Boolean(from) && from !== to && !known.has(from!));
    if (!wanted.length) return;
    // Of more than one, what each is, in one start: a tree id is asked in the batch, which echoes it; a tree git lacks is none.
    const types = wanted.length < 2 ? new Map<string, string>() : new Map(git(['cat-file', '--batch-check'], wanted.filter(isId).map((from) => `${from}\n`).join('')).out
      .split('\n').map((line) => line.trim().split(' ')).map(([name, type]) => [name!, type!]));
    const trees = wanted.filter((from) => types.get(from) === 'tree');
    const answered = new Map<string, string[]>();
    for (let at = 0; at < trees.length; at += DRIFT_BATCH) {
      const batch = trees.slice(at, at + DRIFT_BATCH);
      const pairs = readPairs(git(['--no-optional-locks', 'diff-tree', '--stdin', '-r', '-z', '--name-only', '--no-renames', '--always'], batch.map((from) => `${from} ${to}\n`).join('')).out);
      for (const from of batch) if (pairs.has(`${from} ${to}`)) answered.set(from, pairs.get(`${from} ${to}`)!);
    }
    // Anything else, one alone, a commit or a name git cannot read as an id, is asked as `git diff` of the pair asks it.
    for (const from of wanted.filter((each) => !answered.has(each))) {
      const ran = types.get(from) === 'missing' ? null : git(['--no-optional-locks', 'diff', '-z', '--name-only', '--no-renames', from, to]);
      if (ran?.status === 0 && isId(from)) answered.set(from, zPaths(ran.out));
      else known.set(from, ran?.status === 0 ? zPaths(ran.out) : null);
    }
    for (const [from, paths] of answered) known.set(from, paths);
    if (answered.size) keep();
  };
  const keep = (): void => {
    try {
      if (!file || !writable()) return;
      const table = new Map<string, number>();
      const pairs: Record<string, number[]> = {};
      for (const [from, paths] of known) {
        if (!paths) continue;
        pairs[from] = paths.map((path) => {
          if (!table.has(path)) table.set(path, table.size);
          return table.get(path)!;
        });
      }
      mkdirSync(dir, { recursive: true });
      const partial = `${file}.${process.pid}.partial`;
      writeFileSync(partial, JSON.stringify({ paths: [...table.keys()], pairs }));
      renameSync(partial, file);
      prune(dir);
    } catch {
      // Not kept: the next judgement of this tree asks git again.
    }
  };
  const drift = (from: string | null | undefined): string[] | null => {
    if (!from || !to) return null;
    if (from === to) return [];
    if (!known.has(from)) ask([from]);
    return known.get(from) ?? null;
  };
  return { ask, drift };
}

/**
 * The drifts from `to` as the merge tool reads them: git asked in the checkout `root`, the answers kept under `home`, the verdict
 * store's checkout, which every worktree shares, once git says it ignores the folder there.
 */
export function driftsAt(root: string, home: string, to: string | null): ReturnType<typeof treeDrifts> {
  const dir = join(home, DRIFT_CACHE);
  const writable = (): boolean => existsSync(dir) || (Boolean(to) && isIgnored(home, `${DRIFT_CACHE}/${driftFile(to!)}`));
  return treeDrifts({ to, dir, writable, git: (args, input) => gitIn(root, args, { allowFail: true, ...(input === undefined ? {} : { input }) }) });
}

/** Only the newest `KEEP` judged trees' files stay. */
function prune(dir: string): void {
  const kept = readdirSync(dir).filter((name) => name.startsWith('drift-') && name.endsWith('.json'))
    .map((name) => ({ name, at: statSync(join(dir, name)).mtimeMs })).sort((a, b) => b.at - a.at);
  for (const old of kept.slice(KEEP)) rmSync(join(dir, old.name), { force: true });
}
