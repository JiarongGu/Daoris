import { test } from 'vitest';
import assert from 'node:assert/strict';
import { attribute, findingReds, gateOutcome, mainRedLines, verdictReds, type Failure, type MainRed } from './main-red.ts';

const fail = (gate: string, unit: string, key: string): Failure => ({ gate, unit, key });
const red = (gate: string, key: string): MainRed => ({ gate, key, from: 'finding', at: '2026-11-12T08:00:00Z', commit: 'abc1234' });

test('a failure main already has under the same signature is main\'s, and one it has not is the branch\'s', () => {
  const split = attribute([fail('reached', 'XTests', 'k1'), fail('reached', 'YTests', 'k2')], [red('reached', 'k1')]);
  assert.deepEqual(split.main.map((each) => each.unit), ['XTests']);
  assert.deepEqual(split.branch.map((each) => each.unit), ['YTests']);
});

test('the same signature on another gate is not main\'s red', () => {
  assert.equal(attribute([fail('cli', 'a', 'k1')], [red('reached', 'k1')]).main.length, 0);
});

test('a gate fails the merge only when a failure of it is the branch\'s; none read is the branch\'s too', () => {
  assert.equal(gateOutcome([fail('reached', 'X', 'k1')], [red('reached', 'k1')]), 'main');
  assert.equal(gateOutcome([fail('reached', 'X', 'k1'), fail('reached', 'Y', 'k2')], [red('reached', 'k1')]), 'branch');
  assert.equal(gateOutcome([], [red('reached', 'k1')]), 'branch');
});

test('main\'s standing comes from its open findings and from its failing verdicts on its own tree, unless a later pass cleared them', () => {
  const findings = [
    { state: 'red', gate: 'reached', test: 'XTests.A', key: 'k1', expired: false, lastSeen: { commit: 'abc1234' }, at: '2026-11-12T08:00:00Z' },
    { state: 'red', gate: 'cli', test: 'T', key: 'k9', expired: true, lastSeen: null, at: '2026-11-01T08:00:00Z' },
    { state: 'green', gate: 'cli', test: 'T', key: 'k8', expired: false, lastSeen: null, at: '2026-11-01T08:00:00Z' },
  ];
  assert.deepEqual(findingReds(findings).map((each) => each.key), ['k1']);
  const verdicts = [
    { gate: 'driver', verdict: 'FAIL', tree: 'M', at: '2026-11-12T09:00:00Z', commit: 'abc1234', signatures: ['d1'] },
    { gate: 'cli', verdict: 'FAIL', tree: 'M', at: '2026-11-12T09:00:00Z', commit: 'abc1234', signatures: ['c1'] },
    { gate: 'cli', verdict: 'PASS', tree: 'M', at: '2026-11-12T10:00:00Z', commit: 'abc1234' },
    { gate: 'web', verdict: 'FAIL', tree: 'OTHER', at: '2026-11-12T09:00:00Z', commit: 'x', signatures: ['w1'] },
  ];
  assert.deepEqual(verdictReds(verdicts, 'M').map((each) => `${each.gate}:${each.key}`), ['driver:d1']);
});

test('the summary names each red it set down as main\'s, with where it was seen, and says it stays owed to main', () => {
  const split = attribute([fail('reached', 'XTests', 'k1')], [red('reached', 'k1')]);
  const lines = mainRedLines(split.main);
  assert.match(lines[0]!, /reached: XTests is main's red, seen at abc1234 on 2026-11-12T08:00:00Z/);
  assert.match(lines.at(-1)!, /stay owed to main/);
  assert.deepEqual(mainRedLines([]), []);
});
