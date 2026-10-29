import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { makeFixture, PATIENCE } from '../test/_fixture.ts';

/**
 * HEAVYQUEUE1: the queue's heavy gates and a driven session's heavy runs share one admission in the Daoris home, so a session's
 * focused run beside a candidate no longer doubles the candidate's gate times: the gate waits for the session's place, says so, and is
 * never failed or timed out for the wait. The admission's own table is `tools/machine-admission.test.mjs`; this holds the merge tool's
 * use of it (`runGates`' `admission`, `admitted`, `machineAdmission`). Run by the `tool-tests` gate.
 */

interface Gate { name: string; run: string; kind: string }
interface GateResult { gate: Gate; code: number; log: string; verdict: string; note: string; ms: number }
interface HeavyAdmission {
  take: (request?: { what?: string; session?: string; halted?: () => boolean }) => Promise<{ release: () => void; id: string; inside: boolean } | null>;
}

const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/merge-branch.mjs') as {
  runGates: (root: string, gates: Gate[], dir: string, options: Record<string, unknown>) => Promise<GateResult[]>;
  machineAdmission: (env: Record<string, string>) => HeavyAdmission | null;
  takesAPlace: (gate: { name: string; run: string; memoryGb?: number }) => boolean;
};
interface WholeAdmission {
  takeWhole: (request?: { what?: string; halted?: () => boolean }) => Promise<{ held: boolean; why?: string; ids?: string[]; end?: () => { conclusive: boolean; doubts: { at: string; why: string }[] } }>;
}
const machine = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/machine-admission.mjs') as {
  createAdmission: (options: Record<string, unknown>) => HeavyAdmission & WholeAdmission;
  admissionFile: (home: string) => string;
  readState: (file: string) => { holds: { what: string; kind: string; session?: string }[]; waits: { what: string }[] };
};

const planned = (name: string, run: string): Gate => ({ name, run, kind: 'suite' });

/** A promise resolved from outside. */
function signal(): { promise: Promise<void>; resolve: () => void } {
  let resolve!: () => void;
  const promise = new Promise<void>((done) => { resolve = done; });
  return { promise, resolve };
}

/** What a fake gate waits for, failing the test rather than hanging it when that never comes. */
async function within(promise: Promise<unknown>, what: string, ms = PATIENCE): Promise<void> {
  let timer: ReturnType<typeof setTimeout> | undefined;
  const late = new Promise<never>((_, fail) => { timer = setTimeout(() => fail(new Error(`never happened: ${what}`)), ms); });
  try {
    await Promise.race([promise, late]);
  } finally {
    clearTimeout(timer);
  }
}

/** A fake gate runner: each gate notes its start and its end, and ends when what `until` names for it has happened. */
function gateRun(names: string[]) {
  const events: string[] = [];
  const started = Object.fromEntries(names.map((name) => [name, signal()]));
  const until: Record<string, () => Promise<unknown>> = {};
  const run = async (_root: string, gate: Gate, at: string): Promise<GateResult> => {
    events.push(`start ${gate.name}`);
    started[gate.name]!.resolve();
    const wait = until[gate.name];
    if (wait) await within(wait(), `what ${gate.name} waits for`);
    events.push(`end ${gate.name}`);
    return { gate, code: 0, log: join(at, `${gate.name}.log`), verdict: 'PASS', note: '', ms: 1000 };
  };
  return { events, started, until, run };
}

const pause = (ms: number) => new Promise((done) => { setTimeout(done, ms); });
let homes = 0;
const newHome = () => makeFixture(`heavy-admission-${(homes += 1)}`).root;
/** An admission of one process over a home, of a capacity of one, looking again every few milliseconds. */
const over = (home: string, kind: string, said: string[] = []) => machine.createAdmission({
  daorisHome: home, kind, capacity: 1, env: {}, say: (line: string) => said.push(line), pollMs: 5, beatMs: 1_000_000,
});

describe('HEAVYQUEUE1: a heavy gate waits for the machine admission', () => {
  test('a driver gate waits for a session focused run, says what for, and runs when the session lets go', async () => {
    const home = newHome();
    const session = await over(home, 'session').take({ what: 'dotnet test --filter Foo', session: 's-1' });
    assert.ok(session);
    const said: string[] = [];
    const gate = gateRun(['driver']);
    const run = tool.runGates('root', [planned('driver', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process')], 'logs', {
      run: gate.run, print: (line: string) => said.push(line), admission: over(home, 'queue', said),
    });
    await pause(150);
    assert.deepEqual(gate.events, [], 'the gate has not started while the session holds the only place');
    assert.match(said.join('\n'), /waiting for the machine's heavy-run admission before driver gate: the machine runs 1 of 1 heavy runs: session s-1's dotnet test --filter Foo/);
    assert.match(said.join('\n'), /never times out/);
    assert.deepEqual(machine.readState(machine.admissionFile(home)).waits.map((each) => each.what), ['driver gate']);
    session.release();
    const results = await run;
    assert.deepEqual(gate.events, ['start driver', 'end driver']);
    assert.deepEqual(results.map((result) => [result.gate.name, result.verdict]), [['driver', 'PASS']], 'the wait is no failure of the gate');
    assert.deepEqual(machine.readState(machine.admissionFile(home)), { holds: [], waits: [] }, 'its place is let go as the gate ends');
  });

  test('a gate holds its place for its own run alone: the next waits until it ends, and a light gate takes none', async () => {
    const home = newHome();
    const gate = gateRun(['u', 'x', 'y']);
    const release = signal();
    gate.until['x'] = () => release.promise;
    // x and y are heavy and the machine admits one; u is light and runs beside them without a place.
    const run = tool.runGates('root', [planned('u', 'node u.mjs'), planned('x', 'dotnet test src/A/x'), planned('y', 'dotnet test src/B/y')], 'logs', {
      run: gate.run, print: () => {}, admission: over(home, 'queue'),
    });
    await within(gate.started['x']!.promise, 'x to start');
    await within(gate.started['u']!.promise, 'u to start');
    await pause(100);
    assert.deepEqual(machine.readState(machine.admissionFile(home)).holds.map((each) => each.what), ['x gate'], 'only a heavy gate holds a place');
    assert.ok(!gate.events.includes('start y'), 'y waits for the place x holds');
    release.resolve();
    await run;
    assert.ok(gate.events.indexOf('start y') > gate.events.indexOf('end x'), gate.events.join(', '));
  });

  test('gates run in turn take a place each and let it go', async () => {
    const home = newHome();
    const gate = gateRun(['x', 'y']);
    await tool.runGates('root', [planned('x', 'dotnet test src/A/x'), planned('y', 'dotnet test src/A/y')], 'logs', {
      run: gate.run, print: () => {}, admission: over(home, 'queue'), oneAtATime: true,
    });
    assert.deepEqual(gate.events, ['start x', 'end x', 'start y', 'end y']);
    assert.deepEqual(machine.readState(machine.admissionFile(home)), { holds: [], waits: [] });
  });

  test('a wait the run no longer needs starts no gate and holds nothing', async () => {
    const home = newHome();
    const session = await over(home, 'session').take({ what: 'a run', session: 's-2' });
    const gate = gateRun(['driver']);
    let stop = false;
    const run = tool.runGates('root', [planned('driver', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests')], 'logs', {
      run: gate.run, print: () => {}, admission: over(home, 'queue'), halt: () => stop, keepGoing: true,
    });
    await pause(80);
    stop = true;
    const results = await run;
    assert.deepEqual(results, []);
    assert.deepEqual(gate.events, []);
    assert.deepEqual(machine.readState(machine.admissionFile(home)).waits, []);
    session?.release();
  });

  test('the suites, the Process halves, the rehearsals and the tool tests take a place; a light gate and a gate declaring no memory take none', () => {
    const gate = (run: string, memoryGb?: number) => ({ name: 'g', run, ...(memoryGb === undefined ? {} : { memoryGb }) });
    assert.equal(tool.takesAPlace(gate('dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process')), true);
    assert.equal(tool.takesAPlace(gate('dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/process.runsettings')), true);
    assert.equal(tool.takesAPlace(gate('npm run verify')), true);
    assert.equal(tool.takesAPlace(gate('npm run tooltests')), true, 'the tool tests load the CPU as a suite does');
    assert.equal(tool.takesAPlace(gate('node tools/orient-index.mjs --check')), false);
    assert.equal(tool.takesAPlace(gate('npm run typecheck:web')), false);
    assert.equal(tool.takesAPlace(gate('dotnet test src/Daoris.Service', 0)), false, 'a declared need of none is none');
    assert.equal(tool.takesAPlace(gate('npm run tooltests', 0)), false);
  });

  test('INTEGRATE1b: while a confirm run holds the whole admission a heavy gate and a session run both wait, and run once it lets go', async () => {
    const home = newHome();
    // A capacity of two: the whole admission is two ordinary entries, which today's take counts as it counts any.
    const whole = machine.createAdmission({ daorisHome: home, kind: 'queue', capacity: 2, env: {}, say: () => {}, pollMs: 5, beatMs: 1_000_000 });
    const confirm = await whole.takeWhole({ what: 'confirm run of driver' });
    assert.ok(confirm.held, confirm.why);
    const gate = gateRun(['driver']);
    const run = tool.runGates('root', [planned('driver', 'dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process')], 'logs', {
      run: gate.run, print: () => {}, admission: machine.createAdmission({ daorisHome: home, kind: 'queue', capacity: 2, env: {}, say: () => {}, pollMs: 5, beatMs: 1_000_000 }),
    });
    let sessionHeld = false;
    const session = machine.createAdmission({ daorisHome: home, kind: 'session', capacity: 2, env: {}, say: () => {}, pollMs: 5, beatMs: 1_000_000 })
      .take({ what: 'dotnet test --filter Foo', session: 's-1' }).then((hold) => { sessionHeld = true; return hold; });
    await pause(150);
    assert.deepEqual(gate.events, [], 'the gate waits while the confirm run holds every place');
    assert.equal(sessionHeld, false, 'so does a driven session');
    assert.deepEqual(machine.readState(machine.admissionFile(home)).holds.map((each) => each.what), ['confirm run of driver (place 1 of 2)', 'confirm run of driver (place 2 of 2)']);
    const evidence = confirm.end!();
    assert.equal(evidence.conclusive, true, JSON.stringify(evidence.doubts));
    await run;
    assert.deepEqual(gate.events, ['start driver', 'end driver']);
    (await session)?.release();
    assert.deepEqual(machine.readState(machine.admissionFile(home)), { holds: [], waits: [] });
  });

  test('the admission is off for the tool own tests, on for a home, and needs no home to be asked', () => {
    assert.equal(tool.machineAdmission({ MERGE_BRANCH_MEMORY: 'off', DAORIS_HOME: 'x' }), null);
    assert.equal(tool.machineAdmission({ MERGE_BRANCH_ADMISSION: 'off', DAORIS_HOME: 'x' }), null);
    assert.ok(tool.machineAdmission({ MERGE_BRANCH_MEMORY: 'off', MERGE_BRANCH_ADMISSION: 'on', DAORIS_HOME: 'x' }));
    assert.ok(tool.machineAdmission({}), 'with no home it is an admission that shares nothing');
  });
});
