/**
 * A test that runs over its time limit is on a tracked list at the time it takes, and no other test may go over.
 *
 *   node tools/time-ratchet.mjs           check the gates' duration records against tools/time-ratchet.json (exit 0 clean, 1 breach, 2 tool error)
 *   node tools/time-ratchet.mjs --since <ms>  judge only the records written at or after that epoch time (a merge's own gates')
 *   node tools/time-ratchet.mjs --classes  the 15 classes of each record with the most total time, each with its reason and its cause
 *   node tools/time-ratchet.mjs --write  cut the list down to what the records now show (never adds a test, never raises one)
 *
 * The records are the files the gates write under `_fixtures/test-timing/`: a `.trx` for each dotnet gate (a Process half's name
 * has `process` in it), Vitest's `web-unit.json` and `tool-*.json` and the node reporter's `tool-tests.xml` and `tool-e2e.xml`. A test is over when it takes
 * longer than its record's limit (`LIMITS`, the owner's budget). A listed test may run up to the milliseconds on its entry, no
 * longer; a key is the test's key or its start, covering a suite's tests. An entry is `{ ms, why, row?, class? }`: `why` says what makes it
 * slow and why that cannot be cut (none fails), `row` the row that optimizes it, `class: true` a class slow in total. The list only shrinks.
 * Only a timing taken alone is judged (`tools/time-load.mjs` marks the rest: `reached`, and each merge gate that logs into the folder, stamp their records), in a run only its own records; each breach says its cause (`tools/time-why.mjs`).
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, writeAtomic } from './fsx.mjs';
import { judgeByOwner, loadWorld, unitsOf } from './time-reach.mjs';
import { mainFile, mainRecord, onMainOf, readMain, recordRun } from './time-main.mjs';
import { ALONE_HOLDS, foreignTrees, heldNow, isLoaded, readMarks } from './time-load.mjs';
import { causeOf, ratchetBreaches, readActs, sayBreach, sourceReader } from './time-why.mjs';

/** The owner's per-test budget, in milliseconds, by the kind of record. */
export const LIMITS = { unit: 30_000, process: 120_000, web: 10_000, tool: 120_000, toolUnit: 10_000 };

const VITEST_REPORTS = ['web-unit.json', 'tool-unit.json', 'tool-e2e.json'];

/** The node reporter's records: the tools' unit tier's and their end-to-end tier's. */
const JUNIT_REPORTS = ['tool-tests.xml', 'tool-e2e.xml'];

/** The suite a reached command's record (`reached-<suite>-<n>[-again].<ext>`, written when a class is measured again alone) is of, or null. */
const reachedSuite = (file) => /^reached-(.+?)-\d+(-again)?\.(trx|json|xml)$/.exec(file)?.[1] ?? null;

/** The limit for a record file by its name. */
export function limitFor(file) {
  const suite = reachedSuite(file);
  if (suite !== null) {
    if (file.endsWith('.trx')) return /process/i.test(suite) ? LIMITS.process : LIMITS.unit;
    return suite === 'web-unit' ? LIMITS.web : suite === 'tool-tests' || suite === 'tool-e2e' ? LIMITS.tool : LIMITS.toolUnit;
  }
  if (file === 'web-unit.json') return LIMITS.web;
  if (file === 'tool-unit.json') return LIMITS.toolUnit;
  if (JUNIT_REPORTS.includes(file) || file === 'tool-e2e.json') return LIMITS.tool;
  return /process/i.test(file) ? LIMITS.process : LIMITS.unit;
}

const under = (path, base) => (path.toLowerCase().startsWith(base.toLowerCase()) ? path.slice(base.length) : path);

const decode = (text) => text.replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&');
const attributes = (tag) => Object.fromEntries([...tag.matchAll(/([\w:-]+)="([^"]*)"/g)].map(([, name, value]) => [name, decode(value)]));
const slashes = (path) => path.replace(/\\/g, '/');

/** A trx duration, `hh:mm:ss.fffffff`, in milliseconds. */
function trxMs(duration) {
  const [, h, m, s] = /^(\d+):(\d+):(\d+(?:\.\d+)?)$/.exec(duration ?? '') ?? [];
  return h === undefined ? 0 : Math.round(((+h * 60 + +m) * 60 + +s) * 1000);
}

/** Each executed test of a trx as { key, ms }. */
export function readTrx(text) {
  const tests = [];
  for (const [tag] of text.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const at = attributes(tag);
    if (at.outcome === 'NotExecuted' || !at.testName) continue;
    tests.push({ key: at.testName, ms: trxMs(at.duration) });
  }
  return tests;
}

/** Each test of a Vitest json report as { key, ms }, the key `<file>::<full name>` with the file relative to `root`. */
export function readVitest(text, root) {
  const base = slashes(root).replace(/\/$/, '') + '/';
  const tests = [];
  for (const file of JSON.parse(text).testResults ?? []) {
    const relative = under(slashes(file.name ?? ''), base);
    for (const each of file.assertionResults ?? []) {
      if (each.status === 'pending' || each.status === 'skipped') continue;
      tests.push({ key: `${relative}::${each.fullName ?? each.title}`, ms: Math.round(each.duration ?? 0) });
    }
  }
  return tests;
}

/** Each test case of a node junit report as { key, ms }, the key `<file>::<suites> > <name>` with the file relative to `root`. */
export function readJunit(text, root) {
  const base = slashes(root).replace(/\/$/, '') + '/';
  const suites = [];
  const tests = [];
  for (const [tag] of text.matchAll(/<\/?testsuite\b[^>]*>|<testcase\b[^>]*>/g)) {
    if (tag.startsWith('</')) {
      suites.pop();
    } else if (tag.startsWith('<testsuite')) {
      if (!tag.endsWith('/>')) suites.push(attributes(tag).name);
    } else {
      const at = attributes(tag);
      const relative = under(slashes(at.file ?? ''), base);
      tests.push({ key: `${relative}::${[...suites, at.name].join(' > ')}`, ms: Math.round(Number(at.time ?? 0) * 1000) });
    }
  }
  return tests;
}

/** Each record file of `dir` (written at or after `since`, when given) with its tests as { key, ms }; a census record written after `CensusWarm.LateMark` waited on the warm-up, so is loaded. */
export function readRecords(dir, root, { since = null } = {}) {
  if (!existsSync(dir)) return [];
  const marks = readMarks(dir);
  const lateMark = join(dir, 'census-warm-late.json');
  const warmLate = existsSync(lateMark) ? statSync(lateMark).mtimeMs : null;
  const lateCensus = (file, at) => warmLate !== null && /^censuses-.*\.trx$/.test(file) && at >= warmLate;
  return readdirSync(dir).sort().map((file) => ({ file, at: statSync(join(dir, file)).mtimeMs })).filter(({ at }) => since === null || at >= since).map(({ file, at }) => {
    const text = () => readFileSync(join(dir, file), 'utf8');
    const reached = reachedSuite(file);
    const vitest = VITEST_REPORTS.includes(file) || (reached !== null && file.endsWith('.json'));
    const junit = JUNIT_REPORTS.includes(file) || ((reached === 'tool-tests' || reached === 'tool-e2e') && file.endsWith('.xml'));
    const tests = file.endsWith('.trx') ? readTrx(text()) : vitest ? readVitest(text(), root) : junit ? readJunit(text(), root) : [];
    return { file, at, tests, loaded: isLoaded(marks, file, at) || lateCensus(file, at) };
  });
}

/** Each test over its limit in `dir`'s records taken alone (key -> { ms, limit }), every key, `loaded`: over only under load, and `times`: each alone test's ms. */
export function scanRecords(dir, root, { since = null } = {}) {
  const latest = new Map();
  const seen = new Set();
  const underLoad = new Map();
  for (const { file, at, tests, loaded } of readRecords(dir, root, { since })) {
    const limit = limitFor(file);
    if (loaded) {
      for (const { key, ms } of tests) if (ms > limit && (underLoad.get(key)?.ms ?? -1) < ms) underLoad.set(key, { ms, limit });
      continue;
    }
    const slowest = new Map();
    for (const { key, ms } of tests) {
      seen.add(key);
      if ((slowest.get(key) ?? -1) < ms) slowest.set(key, ms);
    }
    for (const [key, ms] of slowest) {
      const kept = latest.get(key);
      if (!kept || kept.at < at || (kept.at === at && kept.ms < ms)) latest.set(key, { ms, limit, at });
    }
  }
  const over = {};
  for (const [key, { ms, limit }] of latest) if (ms > limit) over[key] = { ms, limit };
  const loaded = Object.fromEntries([...underLoad].filter(([key]) => !seen.has(key)));
  return { over, seen, loaded, times: Object.fromEntries([...latest].map(([key, { ms }]) => [key, ms])) };
}

/** The entry of `list` that covers `key`: its own, else the longest entry that starts it. */
export function entryFor(list, key) {
  if (list[key] !== undefined) return key;
  return Object.keys(list).filter((entry) => key.startsWith(entry)).sort((a, b) => b.length - a.length)[0];
}

const MIN_WHY = 25;

/** The reasons a list's entries lack: each entry says what makes its test slow and why that cannot be cut (a short or name-only sentence is none). */
export function reasonProblems(list) {
  const problems = [];
  for (const [key, entry] of Object.entries(list)) {
    const why = typeof entry?.why === 'string' ? entry.why.trim() : '';
    const name = (key.split('::').pop() ?? key).split('.').pop() ?? key;
    const squash = (text) => text.toLowerCase().replace(/[^a-z0-9]/g, '');
    if (typeof entry?.ms !== 'number') problems.push(`${key} is listed with no { ms, why }: give its limit in milliseconds and why it is slow`);
    else if (why.length < MIN_WHY) problems.push(`${key} is listed with no why: say what makes it slow and why that cannot be cut, or optimize it`);
    else if (squash(why).includes(squash(name)) && squash(why).length < squash(name).length + MIN_WHY) {
      problems.push(`${key}'s why only restates the test's name: say what makes it slow`);
    }
  }
  return problems;
}

/** The breaches of `list` (key -> { ms, why, row }) against `found` (key -> { ms, limit }). */
export function checkTimes(found, list) {
  const problems = [];
  for (const [key, { ms, limit }] of Object.entries(found)) {
    const entry = entryFor(list, key);
    if (entry === undefined) {
      problems.push(`${key} took ${ms} ms, over its ${limit} ms limit: make it faster or smaller; the list takes no new test`);
    } else if (ms > list[entry].ms) {
      problems.push(`${key} took ${ms} ms, longer than its listed ${list[entry].ms} ms: a listed test does not get slower`);
    }
  }
  return problems;
}

/** One line for each test over its limit: its reason (and the row that optimizes it), or that it has none. */
export function reasonLines(found, list) {
  return Object.entries(found).map(([key, { ms }]) => {
    const entry = entryFor(list, key);
    const { why, row } = entry === undefined ? {} : list[entry];
    return `${key} took ${ms} ms: ${why ? `${why}${row ? ` (${row})` : ''}` : 'no reason: optimize it or say why'}`;
  });
}

/** A test's class: its key without the case's arguments and its last name part, or the file of a `file::name` key. */
const classOf = (key) => (key.includes('::') ? key.split('::')[0] : key.split('(')[0].replace(/\.[^.]*$/, ''));

const ACT_NAMES = ['process', 'git', 'disk', 'network', 'wait'];

function classActs(keys, acts) {
  const found = keys.map((key) => acts(key)).filter(Boolean);
  if (!found.length) return null;
  return Object.fromEntries(ACT_NAMES.map((name) => [name, found.reduce((sum, each) => sum + (each[name] ?? 0), 0)]));
}

/** The `count` slowest classes as { name, ms, count, reason, row }; `read` ({ source, acts, record }) adds each one's cause and evidence. */
export function slowestClasses(tests, count, list = {}, read = null) {
  const classes = new Map();
  for (const { key, ms } of tests) {
    const name = classOf(key);
    const at = classes.get(name) ?? { name, ms: 0, count: 0, keys: [] };
    classes.set(name, { ...at, ms: at.ms + ms, count: at.count + 1, keys: [...at.keys, key] });
  }
  return [...classes.values()].sort((a, b) => b.ms - a.ms).slice(0, count).map(({ keys, ...each }) => {
    const entry = entryFor(list, each.name) ?? Object.keys(list).find((key) => key.startsWith(`${each.name}.`) || key.startsWith(`${each.name}::`));
    const reading = read ? causeOf({ ...(read.source(keys[0]) ?? {}), record: read.record ?? '', acts: read.acts ? classActs(keys, read.acts) : null }) : null;
    return { ...each, reason: entry === undefined ? '' : list[entry].why, row: entry === undefined ? '' : (list[entry].row ?? ''), ...(reading ? { cause: reading.cause, evidence: reading.evidence } : {}) };
  });
}

export const classLine = (c) => `  ${(c.ms / 1000).toFixed(0)} s  ${c.count}  ${c.name}  ${c.reason || 'no reason: optimize it or say why'}${c.row ? ` (${c.row})` : ''}${c.cause ? `  [${c.cause}: ${c.evidence}]` : ''}`;

/** The list cut to the records: an entry lowered to its slowest test, gone once none is over; a `class: true` entry stays with its class. */
export function tighten(found, list, seen = new Set()) {
  const slowest = {};
  for (const [key, { ms }] of Object.entries(found)) {
    const entry = entryFor(list, key);
    if (entry !== undefined) slowest[entry] = Math.max(slowest[entry] ?? 0, ms);
  }
  const next = {};
  for (const entry of Object.keys(list).sort()) {
    if (list[entry].class !== true && slowest[entry] === undefined && [...seen].some((key) => entryFor(list, key) === entry)) continue;
    next[entry] = { ...list[entry], ms: Math.min(list[entry].ms, slowest[entry] ?? list[entry].ms) };
  }
  return next;
}

/** The census classes `units` run again alone, once the merge's other gates have ended, so each writes a newer record of its own time. */
export function measureAlone(root, units) {
  console.log(`time-ratchet: measured again alone: ${units.join(', ')}`);
  spawnSync(process.execPath, [join(root, 'tools', 'reached-checks.mjs'), '--measure', '--classes', units.join(',')], { cwd: root, stdio: 'inherit', windowsHide: true });
}

export const GATE_START = 'VERIFY_CHECKS_STARTED_AT';

/** `npm run verify:checks` hands its steps its start (`GATE_START`, epoch ms) unless an enclosing run handed one. */
export function withGateStart(env, at) {
  const held = Number(env[GATE_START]);
  return Number.isFinite(held) && held > 0 ? env : { ...env, [GATE_START]: String(at) };
}

/** When this run began: the later of `run-start.json` (the merge) and the gate start in `env`, or null; older records are never a red. */
export function runStart(dir, env = process.env) {
  const starts = [];
  try {
    starts.push(Number(JSON.parse(readFileSync(join(dir, 'run-start.json'), 'utf8')).startedAt));
  } catch {
    starts.length = 0;
  }
  starts.push(Number(env[GATE_START] || NaN));
  const known = starts.filter((at) => Number.isFinite(at));
  return known.length ? Math.max(...known) : null;
}

/** A scan's tests timed only under load, measured again alone when `free()`, else left unmeasured; they fail nothing for time. */
export async function settleLoaded({ scan, free, measure, world }) {
  let scanned = scan();
  if (Object.keys(scanned.loaded).length && free()) {
    const units = unitsOf(scanned.loaded, await world());
    if (units.length) {
      measure(units);
      scanned = scan();
    }
  }
  return { scanned, unmeasured: Object.keys(scanned.loaded) };
}

async function main() {
  const at = process.argv.indexOf('--since');
  const explicit = at >= 0 ? Number(process.argv[at + 1]) : null;
  if (explicit === null && process.env.MERGE_GATES_RUN === '1') {
    console.log("time-ratchet: stands aside inside a merge: the merge tool runs it once after every gate, on the records its own gates wrote");
    return 0;
  }
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const listFile = join(root, 'tools', 'time-ratchet.json');
  const dir = join(root, '_fixtures', 'test-timing');
  const since = explicit ?? runStart(dir);
  try {
    const list = JSON.parse(readFileSync(listFile, 'utf8'));
    const changed = process.env.DAORIS_RATCHET_CHANGED ? JSON.parse(process.env.DAORIS_RATCHET_CHANGED) : null;
    const scan = () => scanRecords(dir, root, { since });
    const { scanned: settled, unmeasured } = await settleLoaded({ scan, free: () => heldNow() + foreignTrees(root) <= ALONE_HOLDS, measure: (units) => measureAlone(root, units), world: () => loadWorld(root) });
    for (const key of unmeasured) console.log(`time-ratchet: unmeasured, under load: ${key} was timed only while other heavy runs were under way, so it is not judged for time; it is measured again alone when the machine is free`);
    const main = changed ? readMain(mainFile(root)) : {};
    const onMain = onMainOf(root);
    const mainMs = (key) => mainRecord(main, key, onMain);
    const judged = changed ? await judgeByOwner({ scan, changed, mainMs, world: () => loadWorld(root), measure: (units) => measureAlone(root, units) }) : null;
    const { over: all, seen } = judged ? { over: {}, seen: judged.seen } : scan();
    const found = judged ? judged.mine : all;
    const inherited = judged ? judged.inherited : {};
    if (process.argv.includes('--write')) {
      const next = tighten(found, list, seen);
      writeAtomic(listFile, JSON.stringify(next, null, 2) + '\n');
      console.log(`time-ratchet: wrote ${Object.keys(next).length} listed tests to tools/time-ratchet.json`);
      return 0;
    }
    if (process.argv.includes('--classes')) {
      for (const { file, tests } of readRecords(dir, root, { since })) {
        console.log(`${file}:`);
        for (const c of slowestClasses(tests, 15, list, { source: sourceReader(root), acts: readActs(dir), record: file })) console.log(classLine(c));
      }
      return 0;
    }
    const problems = [...reasonProblems(list).map((line) => `${line}`), ...checkTimes(found, list)];
    for (const line of reasonLines(found, list)) console.log(`time-ratchet: over budget: ${line}`);
    const loaded = Object.fromEntries(unmeasured.map((key) => [key, settled.loaded[key]]));
    const records = () => new Map(readRecords(dir, root, { since }).flatMap(({ file, tests }) => tests.map(({ key }) => [key, file])));
    const breaches = ratchetBreaches({ found, inherited, loaded, listedAt: (key) => list[entryFor(list, key)]?.ms, records, source: sourceReader(root), acts: readActs(dir) });
    for (const each of breaches.filter((breach) => breach.inherited)) {
      console.log(`time-ratchet: inherited from main, a known red that fails nothing: ${each.name} took ${each.ms} ms, over its ${each.limit} ms limit; cause ${each.cause}, owned by ${each.fix}`);
    }
    for (const breach of breaches) console.log(sayBreach(breach));
    if (changed) recordRun(root, { times: scan().times, changed });
    for (const p of problems) console.error(`time-ratchet: ${p}`);
    if (problems.length) return 1;
    const count = Object.keys(inherited).length;
    console.log(`time-ratchet: clean (${Object.keys(found).length} tests of the merge's over their limits, all listed and none slower${count ? `; ${count} inherited from main, each with its cause and row` : ''})`);
    return 0;
  } catch (e) {
    console.error(`time-ratchet: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) main().then((code) => { process.exitCode = code; });
