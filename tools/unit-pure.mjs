/**
 * The unit-purity census: a unit test starts no process, reads git through no helper that starts it and waits on no wall clock,
 * because each of those costs what the machine's load makes it cost, not what the code does, and a loaded run fails on it.
 *
 *   node tools/unit-pure.mjs             check the tree against tools/unit-pure.json (exit 0 clean, 1 a breach, 2 tool error)
 *   node tools/unit-pure.mjs --shrink    lower the list to what the tree reads now, never raising a count (the commit hook's writer)
 *   node tools/unit-pure.mjs --write     write the list from the tree, each file with the row that owns cutting it (`--row <ROW>`)
 *   node tools/unit-pure.mjs --measure   the tree's unit tests that do one, grouped by folder and act, the largest first
 *
 * A unit test is a test file of a declared set (`tools/test-sets.mjs`) that no real dependency owns: a set run by the `tool-e2e` gate
 * or by Playwright is where a real process, a real git and a real wait belong, so the same text there is not read. The list names each
 * file that does one now, with its count per act and the row that owns cutting it; a count only falls, a file the list lacks has none.
 * A .NET unit test class is held by the layers census (`tools/layers-census.mjs`: process starts, git's sites, the clock), by the
 * `Process` trait that moves it out of the unit halves, and by the time ratchet, which classes a slow one by what it did
 * (`tools/time-why.mjs`).
 */
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, writeAtomic } from './fsx.mjs';
import { declaredSets, filesBySet, listFiles, setOf } from './test-sets.mjs';
import { ACTS, readActs } from './unit-pure/reads.mjs';

export { readActs };

export const LIST_FILE = 'tools/unit-pure.json';
const ROW = /^[A-Z][A-Z0-9]*\d[a-z]?$/;
const TEST_FILE = /\.(?:test\.(?:mjs|ts|tsx))$/;

const SAYS = {
  process: 'starts a process',
  git: 'reads git through a helper that starts it',
  clock: 'waits on the wall clock',
};

/** Whether a real dependency owns a set: its gate is the end-to-end one, or its runner a browser's. */
export const isReal = (set) => set.gate === 'tool-e2e' || set.runner === 'playwright';

const readList = (root) => {
  const file = join(root, ...LIST_FILE.split('/'));
  return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : { files: {} };
};

/** Each unit test file of the tree at `root` with what it does: `Map` file to `{ process, git, clock }` of sites, a file that does none left out. */
function scanUnits(root) {
  const sets = declaredSets(root);
  const held = filesBySet(sets, listFiles(root, sets));
  const found = new Map();
  for (const set of sets.filter((each) => !each.of && !isReal(each))) {
    for (const file of held.get(set.name).filter((each) => TEST_FILE.test(each))) {
      const acts = readActs(readFileSync(join(root, file), 'utf8'));
      if (ACTS.some((act) => acts[act].length)) found.set(file, acts);
    }
  }
  return found;
}

/** The entries of the list's `files`, each a file with its counts per act and its row. */
const entriesOf = (list) => Object.entries(list.files ?? {}).sort(([a], [b]) => (a < b ? -1 : 1));

const lines = (sites) => `line${sites.length === 1 ? '' : 's'} ${sites.map((site) => site.line).join(', ')}`;

/** The census over the tree at `root`: `{ problems, count }`, one problem per file over its allowance, per count that fell and per entry at fault. */
export function checkUnitPure(root) {
  const list = readList(root);
  const found = scanUnits(root);
  const tasks = existsSync(join(root, 'TASKS.md')) ? readFileSync(join(root, 'TASKS.md'), 'utf8') : null;
  const problems = [];
  for (const [file, acts] of [...found].sort(([a], [b]) => (a < b ? -1 : 1))) {
    for (const act of ACTS) {
      const sites = acts[act];
      const allowed = list.files?.[file]?.[act] ?? 0;
      if (sites.length > allowed) {
        problems.push(`${file}:${sites[0].line}: ${SAYS[act]} ${sites.length} times (${lines(sites)}), over its allowance of ${allowed} in a unit test, whose time is the machine's load: move the case to a real-dependency set (a \`.e2e.ts\` of tool-e2e, a Playwright spec), or hand the code under test a seam of its own`);
      }
    }
  }
  for (const [file, entry] of entriesOf(list)) {
    const acts = found.get(file);
    if (!acts) {
      problems.push(`${file}: is no longer a unit test that does any of it: remove its entry (node tools/unit-pure.mjs --shrink)`);
      continue;
    }
    for (const act of ACTS) {
      if ((entry[act] ?? 0) > acts[act].length) problems.push(`${file}: ${act} fell to ${acts[act].length} from its allowance of ${entry[act]}: lower the list (node tools/unit-pure.mjs --shrink)`);
    }
    if (!entry.row) problems.push(`${LIST_FILE}: ${file} names no row (the TASKS.md row that owns cutting it)`);
    else if (!ROW.test(entry.row)) problems.push(`${LIST_FILE}: ${file}'s row ${entry.row} is no row id`);
    else if (tasks !== null && !tasks.includes(`**${entry.row} `)) problems.push(`${LIST_FILE}: ${file}'s row ${entry.row} is not in TASKS.md: a finished row leaves no entry behind, cut the file or name the row that owns it`);
  }
  return { problems, count: found.size };
}

/** The list lowered to what the tree reads and written back where a count fell: the lines that say each, none where nothing fell. */
export function shrinkUnitPure(root) {
  const list = readList(root);
  const found = scanUnits(root);
  const lowered = [];
  const files = {};
  for (const [file, entry] of entriesOf(list)) {
    const acts = found.get(file);
    const kept = { ...entry };
    for (const act of ACTS) {
      if (entry[act] === undefined) continue;
      const reads = acts?.[act].length ?? 0;
      if (reads >= entry[act]) continue;
      if (reads === 0) {
        delete kept[act];
        if (acts) lowered.push(`${file} ${act}: ${entry[act]} to none`);
      } else {
        kept[act] = reads;
        lowered.push(`${file} ${act}: ${entry[act]} to ${reads}`);
      }
    }
    if (ACTS.some((act) => kept[act] !== undefined)) files[file] = kept;
    else lowered.push(`${file}: no longer any of it`);
  }
  lowered.sort();
  if (lowered.length) writeAtomic(join(root, ...LIST_FILE.split('/')), `${JSON.stringify({ ...list, files }, null, 2)}\n`);
  return lowered;
}

/** Each file and act the tree's unit tests do, `{ file, act, count, lines }`, the largest first. */
export function unitPureFinds(root) {
  const finds = [];
  for (const [file, acts] of scanUnits(root)) {
    for (const act of ACTS) if (acts[act].length) finds.push({ file, act, count: acts[act].length, lines: acts[act].map((site) => site.line) });
  }
  return finds.sort((a, b) => b.count - a.count || (a.file < b.file ? -1 : 1) || ACTS.indexOf(a.act) - ACTS.indexOf(b.act));
}

/** The finds grouped by folder and act, the largest first, with the totals. */
export function measureUnitPure(root) {
  const groups = new Map();
  for (const find of unitPureFinds(root)) {
    const key = `${posix.dirname(find.file)}\0${find.act}`;
    const group = groups.get(key) ?? { domain: posix.dirname(find.file), act: find.act, files: 0, sites: 0 };
    group.files += 1;
    group.sites += find.count;
    groups.set(key, group);
  }
  const ordered = [...groups.values()].sort((a, b) => b.sites - a.sites || (a.domain < b.domain ? -1 : a.domain > b.domain ? 1 : 0) || ACTS.indexOf(a.act) - ACTS.indexOf(b.act));
  return { groups: ordered, files: new Set(unitPureFinds(root).map((find) => find.file)).size, sites: ordered.reduce((sum, group) => sum + group.sites, 0) };
}

/** The list written from the tree, each file with `row` (an entry already named keeps its row). */
export function writeUnitPure(root, row) {
  const before = readList(root).files ?? {};
  const files = {};
  for (const [file, acts] of [...scanUnits(root)].sort(([a], [b]) => (a < b ? -1 : 1))) {
    files[file] = { ...Object.fromEntries(ACTS.filter((act) => acts[act].length).map((act) => [act, acts[act].length])), row: before[file]?.row ?? row };
  }
  writeAtomic(join(root, ...LIST_FILE.split('/')), `${JSON.stringify({ files }, null, 2)}\n`);
  return Object.keys(files).length;
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const argv = process.argv.slice(2);
  try {
    if (argv.includes('--measure')) {
      const { groups, files, sites } = measureUnitPure(root);
      console.log(`unit-pure: ${files} unit test files do ${sites} of the three (a process, git through a helper, a wall-clock wait), by folder and act, the largest first`);
      for (const group of groups) console.log(`  ${String(group.sites).padStart(4)} sites in ${String(group.files).padStart(3)} files  ${group.act.padEnd(7)} ${group.domain}`);
      return 0;
    }
    if (argv.includes('--write')) {
      const row = argv.includes('--row') ? argv[argv.indexOf('--row') + 1] : null;
      if (!row || !ROW.test(row)) throw new Error('--write names the row that owns cutting the files: --row <ROW>');
      console.log(`unit-pure: wrote ${LIST_FILE.replace(/\\/g, '/')} with ${writeUnitPure(root, row)} files`);
      return 0;
    }
    if (argv.includes('--shrink')) {
      const lowered = shrinkUnitPure(root);
      console.log(lowered.length ? `unit-pure: lowered ${lowered.length} allowance${lowered.length === 1 ? '' : 's'} of ${LIST_FILE.replace(/\\/g, '/')}` : 'unit-pure: no allowance fell, so the list is as it was');
      for (const line of lowered) console.log(`  ${line}`);
      return 0;
    }
    const { problems, count } = checkUnitPure(root);
    for (const problem of problems) console.error(`unit-pure: ${problem}`);
    if (problems.length) return 1;
    console.log(`unit-pure: clean (${count} unit test files start, read git or wait, each on the list at no less than it does)`);
    return 0;
  } catch (error) {
    console.error(`unit-pure: ${error.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
