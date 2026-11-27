import { test } from 'vitest';
import assert from 'node:assert/strict';
// @ts-expect-error — untyped workspace tooling, read through the seam its own tests use
import * as untyped from '../merge-branch.mjs';
import { PREFLIGHT_BOUND_MINUTES, planLines, preflightRun, type Timers } from './preflight.ts';
import type { ScopedSet } from './fast-plan.ts';

type Gate = { name: string; run: string; kind: string; memoryGb?: number };
type Result = { gate: Gate; code: number | null; verdict: string; note: string; ms: number; log: string };
const tool = untyped as { runGates: (root: string, gates: Gate[], dir: string, options: Record<string, unknown>) => Promise<Result[]> };

const SET: ScopedSet = {
  run: ['passes', 'fails', 'long', 'heavy', 'queued'].map((name) => ({ name, why: 'reached' })),
  kept: [{ name: 'code-map', why: 'its pass stands on this tree' }],
  deferred: [{ name: 'driver-process', why: "a Process half: the merge's fast tier defers it" }],
};

/** A clock and timers held in the test: `fire` runs what is due by the clock, `tick(ms)` moves the clock and fires. */
function heldTime(start = 1_000_000) {
  let clock = start;
  const due: Array<{ at: number; every: number | null; fn: () => void }> = [];
  const timers: Timers = {
    every: (fn, ms) => {
      const entry = { at: clock + ms, every: ms, fn };
      due.push(entry);
      return () => { due.splice(due.indexOf(entry), 1); };
    },
    after: (fn, ms) => {
      const entry = { at: clock + ms, every: null, fn };
      due.push(entry);
      return () => { due.splice(due.indexOf(entry), 1); };
    },
  };
  const tick = (ms: number) => {
    clock += ms;
    for (const entry of [...due].sort((a, b) => a.at - b.at)) {
      if (entry.at > clock || !due.includes(entry)) continue;
      if (entry.every) entry.at += entry.every;
      else due.splice(due.indexOf(entry), 1);
      entry.fn();
    }
  };
  return { now: () => clock, timers, tick, pending: () => due.length };
}

const until = async (done: () => boolean) => {
  for (let i = 0; i < 400 && !done(); i += 1) await new Promise((next) => { setTimeout(next, 2); });
  assert.ok(done(), 'the run reached the state it waits for');
};

test('a preflight says each minute what it waits on, and at its bound ends what runs, starts nothing more and says what never started and why', async () => {
  const time = heldTime();
  const said: string[] = [];
  const run = preflightRun({
    branch: 'feature/x', gates: names(SET.run), set: SET, boundMinutes: 3, started: time.now(), now: time.now, say: (line) => said.push(line), timers: time.timers,
    holders: () => "the machine runs 3 of 3 heavy runs: the merge queue's driver-process gate (12m 0s)",
  });
  // The machine's admission never has a place for `heavy`: it asks until the run stops it.
  const admission = {
    take: ({ halted }: { what: string; halted?: () => boolean }) => new Promise((done) => {
      const look = setInterval(() => { if (halted?.()) { clearInterval(look); done(null); } }, 2);
    }),
  };
  const hooks = run.hooks(admission);
  const started: string[] = [];
  const gates: Gate[] = [
    { name: 'passes', run: 'node passes.mjs', kind: 'check' }, { name: 'fails', run: 'node fails.mjs', kind: 'check' },
    { name: 'long', run: 'node long.mjs', kind: 'check' }, { name: 'heavy', run: 'node heavy.mjs', kind: 'suite', memoryGb: 1 },
    { name: 'queued', run: 'node queued.mjs', kind: 'check' },
  ];
  run.start();
  const ran = tool.runGates('root', gates, 'logs', {
    keepGoing: true, scripts: {}, print: () => {}, width: 2, admission: hooks.admission, halt: hooks.halt,
    run: async (_root: string, gate: Gate) => {
      hooks.onStart(gate);
      started.push(gate.name);
      let result: Result;
      if (gate.name === 'long') {
        await until(() => Boolean(hooks.trees.ended));
        result = { gate, code: null, verdict: 'ENDED', note: `ended, not failed: ${hooks.trees.ended}`, ms: 3 * 60_000, log: 'logs/long.log' };
      } else {
        const fails = gate.name === 'fails';
        result = { gate, code: fails ? 1 : 0, verdict: fails ? 'FAIL' : 'PASS', note: '', ms: 1, log: `logs/${gate.name}.log` };
      }
      hooks.onResult(result);
      return result;
    },
  });
  await until(() => started.includes('long'));
  await new Promise((next) => { setTimeout(next, 20); });

  time.tick(60_000);
  assert.equal(said.length, 1, said.join('\n'));
  assert.equal(said[0], "  preflight, 1 min of its 3 min bound: running long (1 min); waiting for the machine's heavy-run admission: heavy (1 min); "
    + "the machine runs 3 of 3 heavy runs: the merge queue's driver-process gate (12m 0s); waiting their turn: queued");
  time.tick(60_000);
  assert.equal(said.length, 2, 'one line a minute');

  time.tick(60_000);
  assert.match(said.at(-1)!, /^ {2}preflight: its bound of 3 min is reached: ending long, and starting nothing more$/);
  assert.equal(hooks.trees.ended, "the preflight's bound of 3 min");
  const results = await ran;
  run.stop();
  assert.equal(time.pending(), 0, 'the bound and the minute line are cleared');
  assert.deepEqual(started, ['passes', 'fails', 'long'], 'nothing starts once the bound is reached');

  const { lines, code, failed } = run.summary(results);
  assert.equal(code, 1);
  assert.deepEqual(failed.map((result) => result.gate.name), ['fails']);
  assert.deepEqual(lines, [
    'merge-branch: the preflight of feature/x stopped at its bound of 3 min, unfinished and no pass: 1 passed, 1 failed, 1 stopped while running, 2 never started',
    '  passed: passes',
    '  failed: fails',
    '  stopped while running: long (3 min in)',
    "  never started: heavy, waiting for the machine's heavy-run admission; the machine runs 3 of 3 heavy runs: the merge queue's driver-process gate (12m 0s); queued, waiting its turn behind long",
    "  deferred to the CI run on main, as the merge's fast tier defers them: driver-process",
    "  Fix the branch's reds in the brief; then run it again with a longer --bound, or when the machine is freer, for the gates it did not finish.",
  ]);
});

test('a preflight that ends within its bound says its time, and names what it deferred', () => {
  const time = heldTime();
  const said: string[] = [];
  const run = preflightRun({ branch: 'feature/x', gates: ['passes', 'fails'], set: SET, started: time.now(), now: time.now, say: (line) => said.push(line), timers: time.timers });
  const result = (name: string, verdict: string) => ({ gate: { name }, verdict, ms: 1 });
  const hooks = run.hooks(null);
  assert.equal(hooks.admission, null, 'with the admission off, none is watched');
  run.start();
  time.tick(60_000);
  for (const name of ['passes', 'fails']) {
    hooks.onStart({ name });
    hooks.onResult(result(name, 'PASS'));
  }
  time.tick(6 * 60_000);
  run.stop();
  assert.equal(said[0], '  preflight, 1 min of its 25 min bound: waiting their turn: passes, fails');
  assert.equal(said.at(-1), '  preflight, 7 min of its 25 min bound: every gate has run once; finishing (a gate run again alone, once the machine is free, or the brief)');
  assert.deepEqual(run.summary([result('passes', 'PASS'), result('fails', 'PASS')]), {
    code: 0, failed: [],
    lines: [
      "merge-branch: the preflight of feature/x passed in 7 min: 2 of its merge's fast plan ran green here.",
      "  deferred to the CI run on main, as the merge's fast tier defers them: driver-process",
    ],
  });
  const red = run.summary([result('passes', 'PASS'), result('fails', 'FAIL')]);
  assert.equal(red.code, 1);
  assert.match(red.lines[0]!, /^merge-branch: the preflight of feature\/x failed 1 of 2 gates in 7 min: fails\. Fix the branch's reds/);
  assert.equal(PREFLIGHT_BOUND_MINUTES, 25);
});

test('the plan is said before it runs: the gates, the kept, each deferred with why, and the bound', () => {
  assert.deepEqual(planLines(SET, 25), [
    "the merge's fast plan: passes, fails, long, heavy, queued; kept, their pass standing on this tree: code-map",
    "  deferred: driver-process, a Process half: the merge's fast tier defers it",
    '  its bound: 25 min (--bound <minutes>); each minute it says what it waits on, and at the bound it ends what it started and says what ran',
  ]);
});

function names(entries: { name: string }[]): string[] {
  return entries.map((entry) => entry.name);
}

test('a preflight counts the time each gate waited for its place and for memory, so the record says wait apart from run', async () => {
  const time = heldTime();
  const run = preflightRun({ branch: 'feature/x', gates: ['early', 'late'], set: SET, started: time.now(), now: time.now, say: () => {}, timers: time.timers });
  let release: (hold: unknown) => void = () => {};
  const hooks = run.hooks({ take: () => new Promise((done) => { release = done; }) });
  const asking = hooks.admission!.take({ what: 'late gate' });
  time.tick(90_000);
  release({ id: 'place' });
  await asking;
  time.tick(30_000);
  hooks.onStart({ name: 'late' });
  hooks.onStart({ name: 'early' });
  time.tick(5_000);
  hooks.onResult({ gate: { name: 'early' }, verdict: 'PASS', ms: 5_000 });
  assert.deepEqual(run.waits(), { late: 120_000 }, 'its place and the memory after it, from the ask to the start; a gate that never waited has none');
});
