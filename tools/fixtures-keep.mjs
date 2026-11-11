#!/usr/bin/env node
/**
 * The keep rule for a checkout's `_fixtures/`: an entry no run needs any more is removed, and every other is kept. A test removes
 * its own scratch (`GitSeed.Remove`); what this clears is what a killed host, a failed rehearsal or a retired probe left.
 *
 *   node tools/fixtures-keep.mjs [--root <checkout>] [--hours <n>]           say what the rule would remove, removing nothing
 *   node tools/fixtures-keep.mjs --apply [--root <checkout>] [--hours <n>]   remove it
 *
 * An entry stays while it or anything directly in it changed within the window, 24 hours by default (the longest run, a CI of
 * every gate, takes about eight); while it is a cache a run keeps on purpose (KEPT); and always when it is a link of any kind, which
 * is never followed. As the repository's cleanup procedure asks (docs/development.md), the folder must be one git ignores and each
 * target must resolve inside it. Only the folder's direct entries are read, so a plan of a folder of 400,000 files is quick.
 *
 * Exit codes: 0 planned or removed, a held entry said and left · 2 a tool error (usage, a folder git does not ignore).
 */
import { lstatSync, readdirSync, realpathSync, rmSync } from 'node:fs';
import { dirname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execGit, isMain } from './fsx.mjs';

/**
 * The entries a run keeps across runs on purpose: the pinned git the tests fetch and verify once, the rehearsals' incremental
 * builds, and the seeded repositories, whose folders `GitSeed` sweeps itself by whether their host still runs.
 */
export const KEPT = Object.freeze(['pinned-mingit', 'rehearsal-builds', 'seeds']);

const HOUR = 3600_000;

/** When `path` or anything directly in it last changed, in ms. */
function newest(path, stat) {
  let at = stat.mtimeMs;
  if (!stat.isDirectory()) return at;
  for (const child of readdirSync(path)) {
    try {
      at = Math.max(at, lstatSync(join(path, child)).mtimeMs);
    } catch {
      // A child that goes while the folder is read is one a run is writing now.
      return Date.now();
    }
  }
  return at;
}

/** The entries of `root`'s `_fixtures/` the rule would remove: `{ name, path, newest }`, written before the window began. */
export function stale(root, { now = Date.now(), hours = 24 } = {}) {
  const fixtures = join(root, '_fixtures');
  const since = now - hours * HOUR;
  const out = [];
  for (const name of readdirSync(fixtures)) {
    if (KEPT.includes(name)) continue;
    const path = join(fixtures, name);
    // An entry its run removes while the folder is read is no one's to remove.
    const stat = lstatSync(path, { throwIfNoEntry: false });
    if (!stat || stat.isSymbolicLink()) continue;
    const at = newest(path, stat);
    if (at < since) out.push({ name, path, newest: at });
  }
  return out;
}

/**
 * Remove each of `entries`, each resolved inside `root`'s `_fixtures/` first and a link refused, the walk never following one. An
 * entry something still holds is left and said: a later run's rule takes it. `remove` is a test's.
 */
export function removeStale(root, entries, { remove = (path) => rmSync(path, { recursive: true, force: true, maxRetries: 4 }) } = {}) {
  const fixtures = realpathSync(join(root, '_fixtures'));
  const removed = [];
  const held = [];
  for (const entry of entries) {
    const target = resolve(entry.path);
    if (dirname(target) !== resolve(root, '_fixtures') || !realpathSync(target).startsWith(fixtures + sep)) {
      throw new Error(`fixtures-keep: ${entry.path} resolves outside ${fixtures}`);
    }
    if (lstatSync(target).isSymbolicLink()) throw new Error(`fixtures-keep: ${entry.path} is a link, and a link is never removed`);
    try {
      remove(target);
      removed.push(entry.name);
    } catch (error) {
      held.push({ name: entry.name, why: error.code ?? error.message });
    }
  }
  return { removed, held };
}

function main(argv) {
  const apply = argv.includes('--apply');
  const flag = (name) => (argv.includes(name) ? argv[argv.indexOf(name) + 1] : undefined);
  const root = resolve(flag('--root') ?? dirname(dirname(fileURLToPath(import.meta.url))));
  const hours = Number(flag('--hours') ?? 24);
  const known = new Set(['--apply', '--root', flag('--root'), '--hours', flag('--hours')]);
  if (!Number.isFinite(hours) || hours < 1 || argv.some((each) => !known.has(each))) {
    console.error('usage: node tools/fixtures-keep.mjs [--apply] [--root <checkout>] [--hours <n of at least 1>]');
    return 2;
  }
  try {
    execGit(['check-ignore', '-q', '_fixtures/'], { cwd: root });
  } catch {
    console.error(`fixtures-keep: git does not ignore ${join(root, '_fixtures')}, so nothing in it is taken for scratch`);
    return 2;
  }
  const entries = stale(root, { hours });
  const day = (ms) => new Date(ms).toISOString().slice(0, 10);
  for (const entry of entries) console.log(`${apply ? 'removing' : 'would remove'} _fixtures/${entry.name} (last written ${day(entry.newest)})`);
  if (!apply) {
    console.log(`fixtures-keep: ${entries.length} entries older than ${hours} hours; --apply removes them`);
    return 0;
  }
  const { removed, held } = removeStale(root, entries);
  for (const each of held) console.log(`left _fixtures/${each.name}: something still holds it (${each.why}); a later run takes it`);
  console.log(`fixtures-keep: removed ${removed.length}, left ${held.length}`);
  return 0;
}

if (isMain(import.meta.url)) process.exit(main(process.argv.slice(2)));
