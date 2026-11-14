import { test } from 'vitest';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { groupIsHeavy, inPlace, WHOLE_SUITE_CLASSES, type Admission } from './reached-admit.ts';
import { PassedClasses, unitId } from './class-reuse.ts';
import { runPlan } from './reached-run.ts';
import type { ReachedGroup, ReachedPlan, ReachedUnit } from './reached.ts';

const machine = await import(
  // @ts-expect-error — untyped workspace tooling
  '../machine-admission.mjs') as {
  createAdmission: (options: Record<string, unknown>) => Admission;
  admissionFile: (home: string) => string;
  readState: (file: string) => { holds: unknown[]; waits: unknown[] };
};
const { admissionFile, createAdmission, readState } = machine;

const unit = (suite: string, name: string): ReachedUnit => ({ suite, kind: 'dotnet', unit: name, name, file: `${name}.cs` });
const group = (suite: string, count: number, settings: string | null = null): ReachedGroup => ({ runner: 'dotnet', suite, settings, units: Array.from({ length: count }, (_, i) => unit(suite, `Class${i}`)) });
const NONE: ReadonlySet<string> = new Set();
const wait = (ms: number) => new Promise((done) => { setTimeout(done, ms); });

test('a group is heavy by its tier or suite or size, never by a class name', () => {
  assert.equal(groupIsHeavy(group('driver', 1, 'process.runsettings'), NONE), true, 'the Process settings');
  assert.equal(groupIsHeavy(group('modules-process', 1), new Set(['modules-process'])), true, 'a Process suite');
  assert.equal(groupIsHeavy(group('driver-rehearsals', 1), NONE), true, 'a rehearsal');
  assert.equal(groupIsHeavy(group('driver', WHOLE_SUITE_CLASSES), NONE), true, 'a whole suite');
  assert.equal(groupIsHeavy(group('driver', 1), NONE), false, 'one fast unit-tier class');
  const named = group('driver', 1);
  named.units[0] = unit('driver', 'ProcessRehearsalCensusTests');
  assert.equal(groupIsHeavy(named, NONE), false);
});

/** An admission over a fixture home, capacity one, held by a fake holder (a live process, as the seam sees it). */
function fixture() {
  const { dir, remove } = fixtureFolder('reached-admit');
  const said: string[] = [];
  const admission = createAdmission({
    daorisHome: dir, kind: 'session', capacity: 1, say: (line: string) => said.push(line), pollMs: 5, beatMs: 1_000_000, env: {}, alive: () => true,
  });
  const file = admissionFile(dir) as string;
  const now = new Date().toISOString();
  const fakeHolder = { id: 'other-1', kind: 'session', pid: process.pid, what: 'another builder run', since: now, beat: now };
  const setHolds = (holds: unknown[]) => {
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, JSON.stringify({ schema: 1, holds, waits: [] }));
  };
  return { dir, admission, said, file, setHolds, fakeHolder, remove };
}

test('a heavy group waits while the capacity is full and runs when it is freed; a light one does not wait', async () => {
  const f = fixture();
  try {
    f.setHolds([f.fakeHolder]);
    const processSuites = new Set(['driver-process']);
    let ran = false;
    const heavy = inPlace(group('driver-process', 1, 'process.runsettings'), { admission: f.admission, processSuites }, async () => { ran = true; });
    const light = await inPlace(group('driver', 1), { admission: f.admission, processSuites }, async () => 'at once');
    assert.equal(light, 'at once', 'the light group ran beside the full capacity');
    await wait(80);
    assert.equal(ran, false, 'the heavy group waits behind the holder');
    assert.match(f.said[0] ?? '', /waiting for the machine's heavy-run admission before reached checks, driver-process/);
    assert.match(f.said[0] ?? '', /another builder run/);
    f.setHolds([]);
    await heavy;
    assert.equal(ran, true);
    assert.deepEqual(readState(f.file), { holds: [], waits: [] }, 'its place is let go as it ends');
  } finally {
    f.remove();
  }
});

test('a place is let go when the group throws, and a plan runs its heavy group in the place', async () => {
  const f = fixture();
  try {
    await assert.rejects(inPlace(group('driver', WHOLE_SUITE_CLASSES), { admission: f.admission, processSuites: NONE }, async () => { throw new Error('boom'); }), /boom/);
    assert.deepEqual(readState(f.file), { holds: [], waits: [] });
    const heavy = group('driver', WHOLE_SUITE_CLASSES);
    const plan: ReachedPlan = { groups: [heavy], left: {}, reached: heavy.units.length, replayed: {} };
    const keys = new Map(heavy.units.map((each) => [unitId(each), 'k']));
    const during: number[] = [];
    await runPlan({
      root: f.dir, plan, keys, store: new PassedClasses(`${f.file}.store`), say: () => undefined, mainRedOf: () => false,
      exec: () => { during.push(readState(f.file).holds.length); return 1; },
      inPlace: (each, fn) => inPlace(each, { admission: f.admission, processSuites: NONE }, fn),
    });
    assert.deepEqual(during, [1, 1], 'the run and its run again alone both ran holding the place');
    assert.equal(readState(f.file).holds.length, 0);
  } finally {
    f.remove();
  }
});
