/**
 * Main's alone records of each test (RATCHETOWN1, D346), which the time ratchet reads to tell a test a merge made slower from one
 * that was slow before it: `local/time-main.json` in the checkout every worktree shares, machine-local, written by each ratchet run
 * that judges a merge or a commit and read by the next.
 *
 *   { "schema": 1, "tests": { "<key>": [{ "ms": 8123, "commit": "<sha>", "at": "<iso>" }] } }   newest first, `KEEP` a test
 *
 * An entry is the time a test took alone (`scanRecords`'s judged records) and the commit it ran on: an open merge's branch tip
 * (`MERGE_HEAD`), else a clean tree's `HEAD`. It is main's once that commit is in main's history, so a merge that never lands sets no
 * record of main's, and one that lands does, whichever run measured it. Main's record of a test is the slowest of its entries on
 * main: a test whose alone times swing (one tool case ran 10 to 31 s alone in a week) is not called slower by its own noise.
 */
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { writeAtomic } from './fsx.mjs';
import { verdictsHome } from './merge/home.ts';

export const TIME_MAIN = 'local/time-main.json';
/** How many entries a test keeps, newest first. */
export const KEEP = 5;

/** The store's file for the tree at `root`: in the checkout its worktrees share. */
export const mainFile = (root) => join(verdictsHome(root), TIME_MAIN);

/** The tests the store keeps; a missing or unreadable store keeps none. */
export function readMain(file) {
  try {
    const tests = JSON.parse(readFileSync(file, 'utf8'))?.tests;
    return tests && typeof tests === 'object' && !Array.isArray(tests) ? tests : {};
  } catch {
    return {};
  }
}

/** `tests` with a run's alone `times` (key -> ms) on `commit` first under each key, replacing that commit's entry, `KEEP` kept. */
export function withRun(tests, times, commit, at) {
  const next = { ...tests };
  for (const [key, ms] of Object.entries(times)) {
    const kept = (Array.isArray(next[key]) ? next[key] : []).filter((each) => each?.commit !== commit);
    next[key] = [{ ms, commit, at }, ...kept].slice(0, KEEP);
  }
  return next;
}

/** Main's alone record of `key`: the slowest of its entries whose commit `onMain` holds, or null where none is. */
export function mainRecord(tests, key, onMain) {
  const times = (Array.isArray(tests[key]) ? tests[key] : []).filter((each) => typeof each?.ms === 'number' && onMain(each.commit)).map((each) => each.ms);
  return times.length ? Math.max(...times) : null;
}

const git = (root, args) => spawnSync('git', ['--no-optional-locks', ...args], { cwd: root, encoding: 'utf8', windowsHide: true });

/** Whether a commit is in the history of the tree's `HEAD` (main's tip in a merge's tree), asked of git once a commit. */
export function onMainOf(root) {
  const known = new Map();
  return (commit) => {
    if (typeof commit !== 'string' || !/^[0-9a-f]{7,40}$/.test(commit)) return false;
    if (!known.has(commit)) known.set(commit, git(root, ['merge-base', '--is-ancestor', commit, 'HEAD']).status === 0);
    return known.get(commit);
  };
}

/**
 * The commit a run's records are of: an open merge's branch tip (`MERGE_HEAD`); a tree with nothing changed, `HEAD`; else none. A
 * folder that is not its tree's top (a test's fixture inside a checkout) has none.
 */
export function commitOf(root, changed) {
  const top = git(root, ['rev-parse', '--show-toplevel']);
  const same = (path) => resolve(path).replace(/\\/g, '/').toLowerCase();
  if (top.status !== 0 || same(top.stdout.trim()) !== same(root)) return null;
  const merging = git(root, ['rev-parse', '-q', '--verify', 'MERGE_HEAD']);
  if (merging.status === 0 && merging.stdout.trim()) return merging.stdout.trim();
  if (changed.length) return null;
  const head = git(root, ['rev-parse', 'HEAD']);
  return head.status === 0 ? head.stdout.trim() || null : null;
}

/** The run's alone times written to the store, where the commit they are of is known; what went wrong said, never thrown. */
export function recordRun(root, { times, changed, at = new Date().toISOString(), say = console.log }) {
  try {
    const commit = commitOf(root, changed);
    if (!commit || !Object.keys(times).length) return;
    const file = mainFile(root);
    writeAtomic(file, `${JSON.stringify({ schema: 1, tests: withRun(readMain(file), times, commit, at) })}\n`);
  } catch (error) {
    say(`time-ratchet: main's alone records could not be written (${error?.message ?? error}); the next merge reads the ones before`);
  }
}
