/**
 * A test that runs over its time limit is on a tracked list at the time it takes, and no other test may go over.
 *
 *   node tools/time-ratchet.mjs           check the gates' duration records against tools/time-ratchet.json (exit 0 clean, 1 breach, 2 tool error)
 *   node tools/time-ratchet.mjs --since <ms>  judge only the records written at or after that epoch time (a merge's own gates')
 *   node tools/time-ratchet.mjs --write  cut the list down to what the records now show (never adds a test, never raises one)
 *
 * The records are the files the gates write under `_fixtures/test-timing/`: a `.trx` for each dotnet gate (a Process half's name
 * has `process` in it), `web-unit.json`, `tool-unit.json` and `tool-e2e.json` from Vitest and `tool-tests.xml` from the node
 * reporter. A test is over when it takes
 * longer than its record's limit (`LIMITS`, the owner's budget). A listed test may run up to the milliseconds on its entry, no
 * longer; an entry's key is the test's key or the start of it, so one entry may cover a suite's tests. With no records
 * (a fresh checkout, no gate run yet) nothing is checked. The list only shrinks: a test that got faster is cut down by `--write`,
 * and a new slow test is fixed, never added.
 */
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain, writeAtomic } from './fsx.mjs';

/** The owner's per-test budget, in milliseconds, by the kind of record. */
export const LIMITS = { unit: 30_000, process: 120_000, web: 10_000, tool: 120_000, toolUnit: 10_000 };

/** The Vitest reports: the page's and the merge tool's two projects. */
const VITEST_REPORTS = ['web-unit.json', 'tool-unit.json', 'tool-e2e.json'];

/** The limit for a record file by its name. */
export function limitFor(file) {
  if (file === 'web-unit.json') return LIMITS.web;
  if (file === 'tool-unit.json') return LIMITS.toolUnit;
  if (file === 'tool-tests.xml' || file === 'tool-e2e.json') return LIMITS.tool;
  return /process/i.test(file) ? LIMITS.process : LIMITS.unit;
}

/** `path` relative to `base` (ending in a slash) where it lies under it, compared without case: a drive letter is spelt either way. */
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

/** Every test over its limit in the records of `dir`, as key -> { ms, limit } (the slowest run of a key wins), and every key the records hold. */
export function scanRecords(dir, root, { since = null } = {}) {
  const over = {};
  const seen = new Set();
  if (!existsSync(dir)) return { over, seen };
  for (const file of readdirSync(dir).sort()) {
    if (since !== null && statSync(join(dir, file)).mtimeMs < since) continue;
    const text = () => readFileSync(join(dir, file), 'utf8');
    const tests = file.endsWith('.trx') ? readTrx(text()) : VITEST_REPORTS.includes(file) ? readVitest(text(), root) : file === 'tool-tests.xml' ? readJunit(text(), root) : [];
    const limit = limitFor(file);
    for (const { key, ms } of tests) {
      seen.add(key);
      if (ms > limit && (over[key]?.ms ?? 0) < ms) over[key] = { ms, limit };
    }
  }
  return { over, seen };
}

/** The entry of `list` that covers `key`: its own, else the longest entry that starts it. */
export function entryFor(list, key) {
  if (list[key] !== undefined) return key;
  return Object.keys(list).filter((entry) => key.startsWith(entry)).sort((a, b) => b.length - a.length)[0];
}

/** The breaches of `list` (key -> ms) against `found` (key -> { ms, limit }). */
export function checkTimes(found, list) {
  const problems = [];
  for (const [key, { ms, limit }] of Object.entries(found)) {
    const entry = entryFor(list, key);
    if (entry === undefined) {
      problems.push(`${key} took ${ms} ms, over its ${limit} ms limit: make it faster or smaller; the list takes no new test`);
    } else if (ms > list[entry]) {
      problems.push(`${key} took ${ms} ms, longer than its listed ${list[entry]} ms: a listed test does not get slower`);
    }
  }
  return problems;
}

/** The list cut down to the records: an entry is lowered to the slowest test it covers, and leaves when the records hold a test it covers and none is over a limit. */
export function tighten(found, list, seen = new Set()) {
  const slowest = {};
  for (const [key, { ms }] of Object.entries(found)) {
    const entry = entryFor(list, key);
    if (entry !== undefined) slowest[entry] = Math.max(slowest[entry] ?? 0, ms);
  }
  const next = {};
  for (const entry of Object.keys(list).sort()) {
    if (slowest[entry] === undefined && [...seen].some((key) => entryFor(list, key) === entry)) continue;
    next[entry] = Math.min(list[entry], slowest[entry] ?? list[entry]);
  }
  return next;
}

function main() {
  const at = process.argv.indexOf('--since');
  const since = at >= 0 ? Number(process.argv[at + 1]) : null;
  if (since === null && process.env.MERGE_GATES_RUN === '1') {
    console.log("time-ratchet: stands aside inside a merge: the merge tool runs it once after every gate, on the records its own gates wrote");
    return 0;
  }
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  const listFile = join(root, 'tools', 'time-ratchet.json');
  const dir = join(root, '_fixtures', 'test-timing');
  try {
    const list = JSON.parse(readFileSync(listFile, 'utf8'));
    const { over: found, seen } = scanRecords(dir, root, { since });
    if (process.argv.includes('--write')) {
      const next = tighten(found, list, seen);
      writeAtomic(listFile, JSON.stringify(next, null, 2) + '\n');
      console.log(`time-ratchet: wrote ${Object.keys(next).length} listed tests to tools/time-ratchet.json`);
      return 0;
    }
    const problems = checkTimes(found, list);
    for (const p of problems) console.error(`time-ratchet: ${p}`);
    if (problems.length) return 1;
    console.log(`time-ratchet: clean (${Object.keys(found).length} tests over their limits, all listed and none slower)`);
    return 0;
  } catch (e) {
    console.error(`time-ratchet: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
