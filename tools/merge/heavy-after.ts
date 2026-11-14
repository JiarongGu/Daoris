/**
 * The heavy tier's run after a merge lands. A merge defers the real-process, real-git and rehearsal classes its paths reach
 * (`heavy-tier.ts`); when the merge commits, `startHeavy` starts `tools/heavy-run.mjs` in the background on main's new tip, which runs
 * them through `tools/reached-checks.mjs --tier heavy` (the machine's admission, each pass recorded) and settles here. A red is filed
 * as a finding naming the merge commit and the classes, the same record the merge reads as main's red, and said on one line the
 * steward's monitor reads (`HEAVY_PREFIX`). The run never holds a merge: the next merge reads its red as main's.
 */
import { spawn } from 'node:child_process';
import { appendFileSync, closeSync, existsSync, mkdirSync, openSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import type { ClassOutcome } from './reached-classify.ts';

export const HEAVY_RUNS = 'local/heavy-runs.jsonl';
export const HEAVY_TREE = 'local/heavy-tree';
export const HEAVY_RUNNER = 'tools/heavy-run.mjs';
/** What every line the run says begins with, so a monitor that reads the log or the output finds it. */
export const HEAVY_PREFIX = 'heavy tier:';

export type HeavyState = 'running' | 'green' | 'red' | 'error';

/** One run's record: written as `running` when the merge lands and again, settled, when the run ends. */
export type HeavyRun = {
  id: string; state: HeavyState; commit: string; base: string; branches: string[]; at: string; ended?: string;
  classes?: { ran: number; replayed: number; flaked: string[]; red: string[]; mainRed: string[] }; note?: string;
};

/** A failure the run files into the CI findings, in the shape `fileSighting` takes. */
export type HeavySighting = {
  gate: string; unit: string; test: string; fingerprint: { test: string; exception: string; at: null; message: string };
  signature: { test: string; exception: string; message: string }; message: string; lines: string[]; commit: string; at: string; kind: 'heavy';
};

const sha = (commit: string): string => commit.slice(0, 7);

export const heavyId = (commit: string, at: string): string => `heavy-${sha(commit)}-${at.replace(/[^0-9]/g, '').slice(0, 14)}`;

/** The arguments the runner is started with. */
export const runnerArgs = (run: HeavyRun): string[] => [HEAVY_RUNNER, '--id', run.id, '--commit', run.commit, '--base', run.base, '--branches', run.branches.join(',')];

export type StartSeams = {
  /** Starts the runner detached with its output in `log`, answering its pid, or null where it could not start. */
  start: (args: string[], log: string) => number | null;
  append: (record: HeavyRun) => void;
  now: () => string;
};

/**
 * The run after a landing, started: its `running` record appended, its runner started in the background. Answers the record and the
 * line to say. Never throws for a runner that could not start: the record says `error`, loudly, and the merge is not held.
 */
export function startHeavy(input: { commit: string; base: string; branches: string[]; log: string }, seams: StartSeams): { run: HeavyRun; lines: string[] } {
  const at = seams.now();
  const run: HeavyRun = { id: heavyId(input.commit, at), state: 'running', commit: input.commit, base: input.base, branches: input.branches, at };
  const pid = seams.start(runnerArgs(run), input.log);
  if (pid === null) {
    const failed: HeavyRun = { ...run, state: 'error', ended: at, note: 'the runner could not be started' };
    seams.append(failed);
    return { run: failed, lines: [`${HEAVY_PREFIX} ${sha(input.commit)} (${input.branches.join(', ')}): the run could not be started; the heavy classes are owed to the CI beside`] };
  }
  seams.append(run);
  return { run, lines: [`${HEAVY_PREFIX} ${run.id} started in the background on ${sha(input.commit)} (${input.branches.join(', ')}), pid ${pid}; log ${input.log}`] };
}

/** The runner started as a detached process of this tool's own node, its output in `log`; used by the merge's landing. */
export function startDetached(home: string, args: string[], log: string): number | null {
  try {
    mkdirSync(dirname(join(home, log)), { recursive: true });
    const out = openSync(join(home, log), 'a');
    const child = spawn(process.execPath, args, { cwd: home, detached: true, stdio: ['ignore', out, out], windowsHide: true });
    closeSync(out);
    child.on('error', () => undefined);
    child.unref();
    return child.pid ?? null;
  } catch {
    return null;
  }
}

/** The run's runner exists in this repository: a scratch repository the tool's own tests merge in holds none, and starts no run. */
export const hasRunner = (home: string): boolean => existsSync(join(home, HEAVY_RUNNER));

/** The landing's own start of the run on the tip just promoted; the lines to say, none where the repository holds no runner (a scratch one). */
export function startAfterLanding(home: string, candidate: { tip: string; base: string; members: { branch: string }[] }): string[] {
  if (!hasRunner(home)) return [];
  return startHeavy(
    { commit: candidate.tip, base: candidate.base, branches: candidate.members.map((each) => each.branch), log: `local/scratch/heavy-${candidate.tip.slice(0, 7)}.log` },
    { start: (args, file) => startDetached(home, args, file), append: (record) => appendHeavy(home, record), now: () => new Date().toISOString() },
  ).lines;
}

/**
 * A run settled by what `reached-checks --tier heavy` ended with. `outcome` null is a run that ended with no counts (`code` its exit): an
 * error, said and kept, its classes owed to the CI beside. A red names the merge commit and every class that failed again alone; a class
 * main already stood red is counted but not filed again.
 */
export function settleHeavy(run: HeavyRun, outcome: ClassOutcome | null, code: number | null, now: string): { run: HeavyRun; sightings: HeavySighting[]; lines: string[] } {
  const where = `${sha(run.commit)} (${run.branches.join(', ')})`;
  if (!outcome) {
    const note = `the run ended with no class counts (exit ${code ?? 'none'})`;
    return { run: { ...run, state: 'error', ended: now, note }, sightings: [], lines: [`${HEAVY_PREFIX} ERROR ${where}: ${note}; the heavy classes are owed to the CI beside`] };
  }
  const classes = { ran: outcome.ran, replayed: outcome.replayed, flaked: outcome.flaked, red: outcome.red, mainRed: outcome.mainRed };
  if (outcome.red.length === 0) {
    const flaked = outcome.flaked.length ? `; ${outcome.flaked.length} failed and passed alone: ${outcome.flaked.join(', ')}` : '';
    return { run: { ...run, state: 'green', ended: now, classes }, sightings: [], lines: [`${HEAVY_PREFIX} GREEN ${where}: ${outcome.ran} classes ran, ${outcome.replayed} replayed${flaked}`] };
  }
  const sightings = outcome.red.map((id): HeavySighting => {
    const [gate = '', unit = ''] = id.split('|');
    const message = `failed in the heavy tier run after merge ${sha(run.commit)} (${run.branches.join(', ')}), then again alone`;
    return {
      gate, unit, test: unit, fingerprint: { test: unit, exception: '', at: null, message }, signature: { test: unit, exception: '', message },
      message, lines: [`${unit}: ${message}`], commit: run.commit, at: now, kind: 'heavy',
    };
  });
  return {
    run: { ...run, state: 'red', ended: now, classes }, sightings,
    lines: [`${HEAVY_PREFIX} RED ${where}: ${outcome.red.length} class(es) failed: ${outcome.red.join(', ')}; filed as findings, main's red for the next merge`],
  };
}

export function appendHeavy(home: string, record: HeavyRun): void {
  mkdirSync(dirname(join(home, HEAVY_RUNS)), { recursive: true });
  appendFileSync(join(home, HEAVY_RUNS), `${JSON.stringify(record)}\n`);
}

/** Each run's newest record, oldest run first: a run's `running` line is superseded by the line that settles it. */
export function readHeavy(home: string): HeavyRun[] {
  const file = join(home, HEAVY_RUNS);
  if (!existsSync(file)) return [];
  const byId = new Map<string, HeavyRun>();
  for (const line of readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean)) {
    try {
      const run = JSON.parse(line) as HeavyRun;
      if (typeof run.id === 'string' && typeof run.commit === 'string') byId.set(run.id, run);
    } catch {
      continue;
    }
  }
  return [...byId.values()];
}

/** The heavy tier's standing as the next merge's plan says it: a run still going, and each run of the last day that ended red or in error. */
export function heavyStanding(runs: readonly HeavyRun[], now: number = Date.now()): string[] {
  const day = 24 * 60 * 60 * 1000;
  const lines: string[] = [];
  for (const run of runs) {
    const where = `${sha(run.commit)} (${run.branches.join(', ')})`;
    if (run.state === 'running') lines.push(`${HEAVY_PREFIX} running on ${where} since ${run.at}: its reds will be main's`);
    else if (run.state === 'red' && now - Date.parse(run.ended ?? run.at) < day) lines.push(`${HEAVY_PREFIX} main is RED from ${where}: ${(run.classes?.red ?? []).join(', ')}`);
    else if (run.state === 'error' && now - Date.parse(run.ended ?? run.at) < day) lines.push(`${HEAVY_PREFIX} ERROR on ${where}: ${run.note ?? 'ended with no counts'}`);
  }
  return lines;
}
