import { test } from 'vitest';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { fastPlan, scopedKind } from './fast-plan.ts';
import { readPlan } from './plan.ts';
import { repoBudgetSource } from './repo-home.ts';
import { selectGates } from './reach.ts';
import type { ScopedKind, ScopedSet } from './fast-plan.ts';
// @ts-expect-error — untyped workspace tooling, read through the seam its own tests use
import * as untyped from '../merge-queue.mjs';

const { scopedSet } = untyped as {
  scopedSet: (options: { gates: { name: string; kind: ScopedKind | undefined }[]; reached: Set<string>; knownRed: Set<string> }) => ScopedSet;
};
const ROOT = fileURLToPath(new URL('../..', import.meta.url));

/** The queue's scoped set over this repository's own plan for `changed`, every Process, git and rehearsal gate holding a known red. */
function chosen(changed: string[]) {
  const plan = readPlan(ROOT, repoBudgetSource());
  const kinds = new Map(plan.map((gate) => [gate.name, scopedKind(gate)]));
  const selection = selectGates(plan, changed, { gated: true });
  const reached = new Set(selection.gates.filter((entry) => entry.run || entry.reached).map((entry) => entry.gate.name));
  const knownRed = new Set(plan.filter((gate) => ['process', 'git', 'rehearsal'].includes(kinds.get(gate.name)!)).map((gate) => gate.name));
  const set = scopedSet({ gates: plan.map((gate) => ({ name: gate.name, kind: kinds.get(gate.name) })), reached, knownRed });
  return { plan, kinds, set, fast: fastPlan(set, (name) => kinds.get(name)!) };
}

const names = (entries: { name: string }[]) => entries.map((entry) => entry.name);

test('a branch that changes the gates file reaches every gate, and its preflight still defers each Process and git half, rehearsal and browser spec', () => {
  const { plan, kinds, set, fast } = chosen(['daoris.gates.json']);
  const heavy = plan.filter((gate) => ['process', 'git', 'rehearsal', 'browser'].includes(kinds.get(gate.name)!)).map((gate) => gate.name);
  for (const name of ['driver-process', 'driver-process-git', 'modules-process', 'service-process', 'driver-rehearsals', 'tool-e2e', 'web', 'deployment']) {
    assert.ok(heavy.includes(name), `${name} is a gate the fast tier defers`);
  }
  assert.ok(names(set.run).includes('driver-process'), "the queue's own set runs a half that holds a known red");
  assert.deepEqual(names(fast.run).filter((name) => heavy.includes(name)), [], 'the preflight runs none of them');
  assert.deepEqual(heavy.filter((name) => !names(fast.deferred).includes(name)), [], 'each is named deferred');
  assert.match(fast.deferred.find((entry) => entry.name === 'driver-process')!.why, /^a Process half: the merge's fast tier defers it to the CI run on main, and a preflight never runs one/);
  assert.match(fast.deferred.find((entry) => entry.name === 'tool-e2e')!.why, /deferred to the background run/);
  for (const name of ['universal', 'code-map', 'test-map', 'cli', 'reached', 'driver', 'service']) assert.ok(names(fast.run).includes(name), `${name} runs, as the merge runs it`);
});

test('the gates a preflight runs are the fast gates its merge runs, the kept ones kept', () => {
  const { set, fast } = chosen(['tools/merge/fast-plan.ts']);
  assert.deepEqual(names(fast.run), names(set.run).filter((name) => !/-process|rehearsals/.test(name)));
  assert.deepEqual(fast.kept, set.kept);
});

test('a gate is the kind of run its command makes it, whatever its name', () => {
  assert.equal(scopedKind({ name: 'universal', run: 'node x' }), 'cheap');
  assert.equal(scopedKind({ name: 'renamed', run: 'dotnet test src/A --settings src/Daoris.Desktop/git.process.runsettings' }), 'git');
  assert.equal(scopedKind({ name: 'renamed', run: 'dotnet test src/A --settings src/Daoris.Desktop/process.runsettings' }), 'process');
  assert.equal(scopedKind({ name: 'renamed', run: 'npm run tooltests:e2e' }), 'rehearsal');
  assert.equal(scopedKind({ name: 'renamed', run: 'npm run rehearse:deploy' }), 'rehearsal');
  assert.equal(scopedKind({ name: 'web', run: 'npm run test:web:e2e' }), 'browser');
  assert.equal(scopedKind({ name: 'renamed', run: 'dotnet test src/Daoris.Service --filter Category!=Process' }), 'fast');
  assert.equal(scopedKind({ name: 'renamed', run: 'npm run tooltests' }), 'node');
});
