/**
 * How many heavy runs are under way as a test timing is taken, so the time ratchet judges only timings taken with the machine
 * to themselves (a timing taken under load is the load's, not the test's).
 *
 * The count is the machine's heavy-run admission (`tools/machine-admission.mjs`): the places held, the run's own included, plus
 * one place for each other checkout or worktree with a test run under way (`testhost`, `vstest`, `dotnet test`, node `--test` or
 * vitest) that took no admission place; the sampler adds them, so `holds` stays one number. A process belongs to the tree its
 * command line names (a worktree under `.claude/worktrees/`, `local/gate-tree`, `local/heavy-tree`, else the main checkout); the
 * run's own tree counts nothing, and a failed process read counts nothing (Windows only, one read at most every 15 s). A
 * record file is marked in `_fixtures/test-timing/load-marks.json` as `{ "<file>": { holds, at } }`, `holds` the most places held
 * while its run lasted and `at` the record's write time at the mark (a record written again after is unmarked). A record is
 * under load when more than `ALONE_HOLDS` places were held; one with no mark is judged as alone, as every record written before
 * this mark is. With no Daoris home nothing is shared and nothing counts. `reached` and the merge's gate step write the marks.
 */
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { writeAtomic } from './fsx.mjs';
import { lookAt } from './machine-admission.mjs';
import { readProcesses as readTable } from './machine-read.mjs';

/** The most places a run may itself hold and still be alone: its own. */
export const ALONE_HOLDS = 1;

const MARKS = 'load-marks.json';

/** The heavy runs under way on the machine now: the live places held (0 with no Daoris home or no admission file). */
export function heldNow(home = process.env.DAORIS_HOME, seam = {}) {
  if (!home) return 0;
  try {
    return lookAt(home, seam).holds.length;
  } catch {
    return 0;
  }
}

/** The marks of `dir`, file -> { holds, at } (none when the file is absent or unreadable). */
export function readMarks(dir) {
  try {
    const marks = JSON.parse(readFileSync(join(dir, MARKS), 'utf8'));
    return marks !== null && typeof marks === 'object' ? marks : {};
  } catch {
    return {};
  }
}

/** Whether the record `file` written at `at` is marked as taken under load, by `marks`. */
export function isLoaded(marks, file, at) {
  const mark = marks[file];
  return mark !== undefined && mark.at >= at && mark.holds > ALONE_HOLDS;
}

/** Mark every record of `dir` written at or after `since` with `holds`, the most heavy runs under way during the run that wrote it. */
export function stampLoad(dir, holds, since) {
  if (!existsSync(dir)) return [];
  const marks = readMarks(dir);
  const stamped = [];
  for (const file of readdirSync(dir)) {
    if (file === MARKS) continue;
    const at = statSync(join(dir, file)).mtimeMs;
    if (at < since) continue;
    marks[file] = { holds, at };
    stamped.push(file);
  }
  if (stamped.length) writeAtomic(join(dir, MARKS), JSON.stringify(marks, null, 2) + '\n');
  return stamped;
}

/** The checkout the tools sit in: the run's own root. */
const OWN_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');

export const FOREIGN_EVERY = 15_000;
const FOREIGN_TIMEOUT = 5_000;
const MAY_BE_TESTS = /testhost|vstest|dotnet|node/i;

/** The processes that may be test runs, `[{ name, commandLine }]` (empty off Windows, or when the read fails). */
export function readProcesses() {
  if (process.platform !== 'win32') return [];
  try {
    return readTable({ timeoutMs: FOREIGN_TIMEOUT }).filter((row) => MAY_BE_TESTS.test(row.name)).map((row) => ({ name: row.name, commandLine: row.command }));
  } catch {
    return [];
  }
}

/** Whether a process is a test run: a test host, vstest, `dotnet test`, or node running `--test` or vitest. */
export function isTestProcess({ name, commandLine }) {
  const exe = name.toLowerCase();
  if (/^(testhost|vstest\.console)/.test(exe)) return true;
  if (/^dotnet/.test(exe)) return /\bvstest\b|\bdotnet(\.exe)?"?\s+test\b|\btesthost\b/i.test(commandLine);
  if (/^node/.test(exe)) return /\s--test\b|vitest/i.test(commandLine);
  return false;
}

/** The checkout root of a path: the worktree, the gate or heavy tree it is in, else `main`; null when outside `main`. */
export function treeOf(path, main) {
  const under = (p, base) => p === base || p.startsWith(base + '/');
  const norm = (p) => p.replace(/\\/g, '/').replace(/\/+$/, '').toLowerCase();
  const p = norm(path);
  const base = norm(main);
  if (!under(p, base)) return null;
  const rel = p.slice(base.length + 1).split('/');
  if (rel[0] === '.claude' && rel[1] === 'worktrees' && rel[2]) return rel.slice(0, 3).join('/');
  if (rel[0] === 'local' && (rel[1] === 'gate-tree' || rel[1] === 'heavy-tree')) return rel.slice(0, 2).join('/');
  return '';
}

/** The main checkout of `root`: itself, or the checkout above the worktree it is. */
function mainOf(root) {
  const flat = root.replace(/\\/g, '/');
  const at = flat.toLowerCase().indexOf('/.claude/worktrees/');
  return at >= 0 ? flat.slice(0, at) : flat;
}

let foreignRead = { at: -Infinity, count: 0 };

/**
 * The trees other than the run's own (`root`) with a test run under way: each counts one place however many processes it runs.
 * `seam.processes` reads the processes and `seam.now` the clock; a read is taken at most every 15 s, and one that fails counts 0.
 */
export function foreignTrees(root = OWN_ROOT, seam = {}) {
  const now = (seam.now ?? Date.now)();
  if (now - foreignRead.at < FOREIGN_EVERY && !seam.processes) return foreignRead.count;
  let count = 0;
  try {
    const main = mainOf(root);
    const own = treeOf(root, main);
    const pattern = new RegExp(main.replace(/[.*+?^${}()|[\]\\]/g, '\\$&').replace(/\//g, '[\\\\/]') + '[^\\s"\']*', 'gi');
    const trees = new Set();
    for (const proc of (seam.processes ?? readProcesses)()) {
      if (!isTestProcess(proc)) continue;
      for (const path of proc.commandLine.match(pattern) ?? []) {
        const tree = treeOf(path, main);
        if (tree !== null && tree !== own) trees.add(tree);
      }
    }
    count = trees.size;
  } catch {
    count = 0;
  }
  if (!seam.processes) foreignRead = { at: now, count };
  return count;
}

/** A sampler of the load while a run lasts: `peak()` is the most places held at any sample, and `stop()` ends the sampling. */
export function loadSampler({ every = 5_000, count = () => heldNow() + foreignTrees() } = {}) {
  let peak = count();
  const timer = setInterval(() => { peak = Math.max(peak, count()); }, every);
  timer.unref();
  return { peak: () => { peak = Math.max(peak, count()); return peak; }, stop: () => clearInterval(timer) };
}
