#!/usr/bin/env node
/**
 * The test sets `daoris.gates.json` declares in `tests`: each a runner's set of test files given by patterns, so a new test file
 * under a pattern is in its set, its gate's run and the merge's reach with no list to edit.
 *
 *   node tools/test-sets.mjs run [--gate <gate>]… [<set>]…   run the sets named and every set of each gate, in the declaration's order
 *   node tools/test-sets.mjs list [<set>]…                   each set's files
 *   node tools/test-sets.mjs reach <path>…                   the sets each changed path reaches, before the census narrows each to its units
 *
 * A file is in the first set whose `files` hold it. `gate` names the gate that runs a set, `suite` the census's name for its units
 * (`tools/test-map.mjs`; the gate's where none), `cwd` where its runner starts (the root where none), `args` the runner's own, and
 * `last` puts a set after the rest of its gate's. A `bounded` set's units are reached only by a path its gate's `paths` hold; any
 * other set's by the census's reach of each unit. A Vitest set with a `project` runs that project of its `config`, which reads its
 * `files` from here; one that is `of` another set runs its own files, a pick that set also runs; any other runs its config whole.
 * `tools/tool-test-census.mjs` holds every test file to a set and every set to a file.
 *
 * Exit codes: 0 every set passed · the first failing runner's code · 2 a tool error.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { globToRegExp } from './merge/lanes-file.ts';

export const ROOT = dirname(dirname(fileURLToPath(import.meta.url)));
const SKIPPED = new Set(['node_modules', '.git', 'bin', 'obj', 'dist']);
const VITEST = 'node_modules/vitest/vitest.mjs';

/** The declared sets of a parsed `daoris.gates.json`, each with its defaults; a malformed set is refused by name. */
export function testSets(declaration) {
  const sets = declaration?.tests?.sets ?? [];
  const seen = new Set();
  return sets.map((set) => {
    if (!set.runner) throw new Error(`test set ${set.name} names no runner`);
    if (!Array.isArray(set.files) || set.files.length === 0) throw new Error(`test set ${set.name} names no files`);
    if (seen.has(set.name)) throw new Error(`two sets are named ${set.name}`);
    seen.add(set.name);
    return Object.freeze({ ...set, suite: set.suite ?? set.gate, cwd: set.cwd ?? '.', args: set.args ?? [], whole: set.whole ?? [], patterns: set.files.map(globToRegExp) });
  });
}

/** The sets this checkout declares. */
export const declaredSets = (root = ROOT) => testSets(JSON.parse(readFileSync(join(root, 'daoris.gates.json'), 'utf8')));

const holds = (set, file) => set.patterns.some((pattern) => pattern.test(file));

/** The set a file is in: the first whose patterns hold it, a pick of another set taking none; or null. */
export const setOf = (sets, file) => sets.find((set) => !set.of && holds(set, file)) ?? null;

/** Each set's files among `files`, sorted: a file in its first set, and a pick's own among those its set holds. */
export function filesBySet(sets, files) {
  const held = new Map(sets.map((set) => [set.name, []]));
  for (const file of files) {
    const set = setOf(sets, file);
    if (set) held.get(set.name).push(file);
    for (const pick of sets.filter((each) => each.of && holds(each, file))) held.get(pick.name).push(file);
  }
  for (const list of held.values()) list.sort();
  return held;
}

/** Whether declared `paths` hold `path`: an entry ending in `/` is a folder, one with `*` a glob, any other a file. */
const pathsHold = (paths, path) => paths.some((entry) => (entry.endsWith('/') ? path.startsWith(entry) : entry.includes('*') ? globToRegExp(entry).test(path) : path === entry));

/** The paths that bound a set's reach (its gate's `paths`, for a `bounded` set), or null where nothing bounds it. */
export function scopeOf(set, gates) {
  if (!set.bounded) return null;
  const paths = gates.find((gate) => gate.name === set.gate)?.paths;
  return Array.isArray(paths) ? paths : null;
}

/** The sets a changed path reaches: every set but a bounded one whose scope does not hold it. The census narrows each to its units. */
export const setsReaching = (sets, gates, path) => sets.filter((set) => {
  const scope = scopeOf(set, gates);
  return !scope || pathsHold(scope, path);
});

/** The sets a run asks for, in the declaration's order, a `last` set after the rest; an unknown name or gate is refused. */
export function selectSets(sets, { names = [], gates = [] }) {
  for (const name of names) if (!sets.some((set) => set.name === name)) throw new Error(`no test set is named ${name}`);
  for (const gate of gates) if (!sets.some((set) => set.gate === gate)) throw new Error(`no test set is run by gate ${gate}`);
  const chosen = sets.filter((set) => names.includes(set.name) || gates.includes(set.gate));
  return [...chosen.filter((set) => !set.last), ...chosen.filter((set) => set.last)];
}

/** The command that runs a set over its `files` (repository-relative), from the set's folder. */
export function setCommand(set, files) {
  const from = (file) => (set.cwd === '.' ? file : posix.relative(set.cwd, file));
  if (set.runner === 'node') return { label: set.name, cwd: set.cwd, command: 'node', args: ['--test', ...set.args, ...files.map(from)] };
  if (set.runner === 'vitest') {
    const selection = set.project
      ? ['--config', from(set.config), '--project', set.project]
      : set.of ? files.map(from) : [];
    return { label: set.name, cwd: set.cwd, command: 'node', args: [VITEST, 'run', ...selection, ...set.args] };
  }
  throw new Error(`test set ${set.name} runs on ${set.runner}, which its gate's own script starts`);
}

/** The folder a pattern's files lie under: its segments before the first with a wildcard. */
const baseOf = (pattern) => {
  const segments = pattern.split('/');
  const at = segments.findIndex((segment) => /[*?{[]/.test(segment));
  return (at < 0 ? segments.slice(0, -1) : segments.slice(0, at)).join('/');
};

/** The files on disk under `root` that the sets' patterns can hold, repository-relative: a new file is seen before it is committed. */
export function listFiles(root, sets) {
  const out = new Set();
  const walk = (dir) => {
    if (!existsSync(join(root, dir))) return;
    for (const entry of readdirSync(join(root, dir), { withFileTypes: true })) {
      const path = dir ? `${dir}/${entry.name}` : entry.name;
      if (entry.isDirectory()) {
        if (!SKIPPED.has(entry.name)) walk(path);
      } else out.add(path);
    }
  };
  const bases = [...new Set(sets.flatMap((set) => set.files.map(baseOf)))];
  for (const base of bases.filter((base) => !bases.some((other) => other !== base && base.startsWith(`${other}/`)))) walk(base);
  return [...out].sort();
}

/** Runs the selected sets one after another, as `&&` would: the first that fails ends the run with its code. */
export function runSets(root, selected, { sets = declaredSets(root), run = spawnSync } = {}) {
  const held = filesBySet(sets, listFiles(root, sets));
  mkdirSync(join(root, '_fixtures', 'test-timing'), { recursive: true });
  for (const set of selected) {
    const files = held.get(set.name);
    if (files.length === 0) throw new Error(`test set ${set.name} holds no test file`);
    const command = setCommand(set, files);
    const own = join(root, command.cwd, VITEST);
    const args = command.args[0] === VITEST && !existsSync(own) ? [join(root, VITEST), ...command.args.slice(1)] : command.args;
    console.log(`test-sets: ${set.name} (${set.runner}, ${files.length} files) in ${command.cwd}`);
    const ran = run(command.command, args, { cwd: join(root, command.cwd), stdio: 'inherit', windowsHide: true });
    if (ran.error) throw ran.error;
    if (ran.status !== 0) return ran.status ?? 1;
  }
  return 0;
}

function main(argv) {
  const [verb, ...rest] = argv;
  const gates = rest.flatMap((word, at) => (rest[at - 1] === '--gate' ? [word] : []));
  const names = rest.filter((word, at) => word !== '--gate' && rest[at - 1] !== '--gate');
  const sets = declaredSets();
  if (verb === 'reach') {
    const gateList = JSON.parse(readFileSync(join(ROOT, 'daoris.gates.json'), 'utf8')).gates ?? [];
    for (const path of names) console.log(`${path}: ${setsReaching(sets, gateList, path).map((set) => `${set.name}${set.bounded ? ' (within its gate\'s paths)' : ''}`).join(', ')}`);
    return 0;
  }
  if (verb === 'list') {
    const held = filesBySet(sets, listFiles(ROOT, sets));
    for (const set of names.length ? selectSets(sets, { names }) : sets) console.log(`${set.name} (${set.gate}, ${set.runner}): ${held.get(set.name).join(' ')}`);
    return 0;
  }
  if (verb !== 'run' || (!gates.length && !names.length)) throw new Error('usage: node tools/test-sets.mjs run [--gate <gate>]... [<set>]... | list [<set>]... | reach <path>...');
  return runSets(ROOT, selectSets(sets, { names, gates }), { sets });
}

if (isMain(import.meta.url)) {
  try {
    process.exitCode = main(process.argv.slice(2));
  } catch (error) {
    console.error(`test-sets: ${error.message}`);
    process.exitCode = 2;
  }
}
