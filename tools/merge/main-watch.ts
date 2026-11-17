/**
 * The watch of main: after each landing the cheap suites (the CLI's verify and tests, the tool tests, the desktop's
 * censuses: `WATCH_GATES`) run on main's new tip in the background, in a tree of their own (`MAIN_WATCH_TREE`), every one to its
 * end, by `node tools/merge-branch.mjs --main-watch <commit>` (`startWatch`, which the landing's after-steps call). A red writes the
 * brief (`red-brief.ts`) and holds the merge queue's next merge (`watchHold`) until it is fixed (a watch of a later tip is green)
 * or named known (`--queue --known` files each of its failures as a known red, or the CI already holds them). The doctrine says a
 * red main comes first; this makes it seen before the next merge builds on it.
 *
 * The record, `local/main-watch.jsonl`: a `running` line as a watch starts, and the line that settles it as it ends.
 */
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

export const MAIN_WATCH = 'local/main-watch.jsonl';
export const MAIN_WATCH_TREE = 'local/main-watch-tree';
export const MAIN_WATCH_LOCK = 'local/main-watch.lock';
export const WATCH_RUNNER = 'tools/merge-branch.mjs';
/** What every line the watch says begins with, so a monitor reading the runner's log finds it. */
export const WATCH_PREFIX = 'main watch:';
/** The cheap suites, by the gate names `daoris.gates.json` declares them under. */
export const WATCH_GATES: readonly string[] = Object.freeze(['cli', 'cli-tests', 'tool-tests', 'censuses']);

export type WatchFailure = { gate: string; unit: string; test: string; key: string; message: string; signature?: unknown };
export type WatchRun = {
  commit: string; state: 'running' | 'green' | 'red' | 'error'; at: string; ended?: string; pid?: number; brief?: string | null;
  failures?: WatchFailure[]; note?: string;
};

const sha = (commit: string): string => commit.slice(0, 7);

/** The plan's cheap suites, in the plan's order. */
export const watchGates = <T extends { name: string }>(plan: readonly T[]): T[] => plan.filter((gate) => WATCH_GATES.includes(gate.name));

export function appendWatch(home: string, run: WatchRun): void {
  mkdirSync(dirname(join(home, MAIN_WATCH)), { recursive: true });
  appendFileSync(join(home, MAIN_WATCH), `${JSON.stringify(run)}\n`);
}

/** Each watch's newest line, oldest watch first: a watch is its commit and its start. */
export function readWatch(home: string): WatchRun[] {
  const file = join(home, MAIN_WATCH);
  if (!existsSync(file)) return [];
  const byRun = new Map<string, WatchRun>();
  for (const line of readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean)) {
    try {
      const run = JSON.parse(line) as WatchRun;
      if (typeof run.commit === 'string' && typeof run.at === 'string') byRun.set(`${run.commit}@${run.at}`, run);
    } catch {
      continue;
    }
  }
  return [...byRun.values()];
}

/**
 * Whether main's red holds the queue's next merge, and why, naming the brief. The newest settled watch decides: green, or red with
 * every failure `known`, holds nothing. A red holds; where main has moved past it, `watchOn` is the tip to watch next, unless a
 * watch whose process lives runs there already or one has settled there.
 */
export function watchHold(runs: readonly WatchRun[], { mainTip, known, alive }: { mainTip: string; known: (failure: WatchFailure) => boolean; alive: (pid: number) => boolean }): { hold: boolean; why: string; watchOn: string | null } {
  const settled = runs.filter((run) => run.state === 'green' || run.state === 'red');
  const last = settled.at(-1);
  if (!last || last.state === 'green') return { hold: false, why: '', watchOn: null };
  const open = (last.failures ?? []).filter((failure) => !known(failure));
  if (!open.length) return { hold: false, why: '', watchOn: null };
  const gates = [...new Set(open.map((failure) => failure.gate))].join(', ');
  const why = `main is red at ${sha(last.commit)} (${gates}): the next merge waits until it is fixed or named known (--queue --known); the brief: ${last.brief ?? 'none written'}`;
  const watched = last.commit === mainTip || runs.some((run) => run.commit === mainTip && (run.state !== 'running' || (run.pid !== undefined && alive(run.pid))));
  return { hold: true, why, watchOn: watched ? null : mainTip };
}

/** The newest settled watch where it is red: what `--queue --known` names known; null where it is green or none settled. */
export function lastRed(runs: readonly WatchRun[]): WatchRun | null {
  const last = runs.filter((run) => run.state === 'green' || run.state === 'red').at(-1);
  return last?.state === 'red' ? last : null;
}

/** Each failure of a red watch as the known-red record files a sighting of it (`fileSighting`'s input), at `at`. */
export const sightingsOf = (red: WatchRun, at: string) => (red.failures ?? []).map((each) => ({
  gate: each.gate, unit: each.unit, test: each.test, fingerprint: { test: each.test, exception: '', at: null, message: each.message },
  signature: each.signature, message: each.message, lines: [each.message], commit: red.commit, at, kind: 'watch',
}));

/** The watch started in the background on `commit` through `start` (a detached node, its output in the log); answers the line to say. */
export function startWatch(commit: string, { start }: { start: (args: string[], log: string) => number | null }): string[] {
  const log = `local/scratch/main-watch-${sha(commit)}.log`;
  const pid = start([WATCH_RUNNER, '--main-watch', commit], log);
  return [pid === null
    ? `${WATCH_PREFIX} could not start on ${sha(commit)}: the cheap suites are owed to the CI beside`
    : `${WATCH_PREFIX} started on ${sha(commit)} in the background, pid ${pid}; log ${log}`];
}
