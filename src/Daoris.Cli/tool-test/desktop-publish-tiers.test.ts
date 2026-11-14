import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../test/_fixture.ts';
// @ts-expect-error — untyped workspace tooling
import { ungatedRefusal } from '../../../tools/desktop-publish.mjs';

// test-map: strings name paths as data

const commit = '0123456789abcdef0123456789abcdef01234567';
const run = (state: string, extra: Record<string, unknown> = {}) => ({ id: `heavy-0123456-${state}`, state, commit, base: 'f'.repeat(40), branches: ['feature'], at: '2026-11-14T10:00:00.000Z', ...extra });

function home(name: string, runs: object[], findings: object[] = []): { root: string; to: string; cleanup: () => void } {
  const fx = makeFixture(name);
  mkdirSync(join(fx.root, 'local'), { recursive: true });
  writeFileSync(join(fx.root, 'local', 'heavy-runs.jsonl'), runs.map((one) => JSON.stringify(one)).join('\n'));
  writeFileSync(join(fx.root, 'local', 'ci-findings.json'), JSON.stringify({ findings }));
  return { root: fx.root, to: join(fx.root, 'install'), cleanup: fx.cleanup };
}

test('the stage takes a commit whose heavy run is green, and says which tier it rests on', () => {
  const fx = home('tiers-green', [run('running'), { ...run('running'), state: 'green', ended: '2026-11-14T10:20:00.000Z', classes: { ran: 4, replayed: 1, flaked: [], red: [], mainRed: [] } }]);
  const taken = ungatedRefusal(fx.to, fx.root, { home: fx.root, commit });
  assert.equal(taken.refusal, null, taken.refusal ?? '');
  assert.match(taken.note!, /heavy run heavy-0123456-\w+ is green on 0123456/);
  fx.cleanup();
});

test('the stage refuses a commit with no landing run, one still running, one that errored, and one with a red no finding holds, naming each', () => {
  const none = home('tiers-none', []);
  const refusedNone = ungatedRefusal(none.to, none.root, { home: none.root, commit });
  assert.match(refusedNone.refusal!, /no heavy run is recorded for 0123456/);
  assert.match(refusedNone.refusal!, /the fast tier passes at a merge and the heavy run starts when it lands/);
  assert.match(refusedNone.refusal!, /--force-ungated/);
  none.cleanup();

  const running = home('tiers-running', [run('running')]);
  assert.match(ungatedRefusal(running.to, running.root, { home: running.root, commit }).refusal!, /heavy run heavy-0123456-running is still running/);
  running.cleanup();

  const errored = home('tiers-error', [run('error', { note: 'the run left 40 reached classes unrun' })]);
  assert.match(ungatedRefusal(errored.to, errored.root, { home: errored.root, commit }).refusal!, /ended in error: the run left 40 reached classes unrun/);
  errored.cleanup();

  const red = home('tiers-red', [run('red', { classes: { ran: 3, replayed: 0, flaked: [], red: ['driver-process|A.BTests'], mainRed: [] } })]);
  const refusedRed = ungatedRefusal(red.to, red.root, { home: red.root, commit });
  assert.match(refusedRed.refusal!, /red on driver-process\|A\.BTests, which no finding holds as main's red/);
  red.cleanup();

  const another = home('tiers-other-commit', [{ ...run('green'), commit: 'b'.repeat(40) }]);
  assert.match(ungatedRefusal(another.to, another.root, { home: another.root, commit }).refusal!, /no heavy run is recorded for 0123456/);
  another.cleanup();
});

test('a red whose every class is a finding already filed as main\'s red is accepted, saying so; the explicit override still publishes', () => {
  const filed = [{ gate: 'driver-process', unit: 'A.BTests', state: 'red' }];
  const fx = home('tiers-filed', [run('red', { classes: { ran: 3, replayed: 0, flaked: [], red: ['driver-process|A.BTests'], mainRed: [] } })], filed);
  const taken = ungatedRefusal(fx.to, fx.root, { home: fx.root, commit });
  assert.equal(taken.refusal, null, taken.refusal ?? '');
  assert.match(taken.note!, /heavy run .* is red on driver-process\|A\.BTests, each filed as main's red/);
  fx.cleanup();

  const none = home('tiers-forced', []);
  const forced = ungatedRefusal(none.to, none.root, { home: none.root, commit, force: true });
  assert.equal(forced.refusal, null);
  assert.match(forced.note!, /ungated \(--force-ungated\).*no heavy run is recorded/s);
  none.cleanup();
  rmSync(join(none.root, 'local'), { recursive: true, force: true });
});

test('the deployment rehearsal\'s scratch install is not asked', () => {
  const fx = home('tiers-fixtures', []);
  assert.deepEqual(ungatedRefusal(join(fx.root, '_fixtures', 'deployment-rehearsal', 'install'), fx.root, { home: fx.root, commit }), { refusal: null, note: null });
  fx.cleanup();
});
