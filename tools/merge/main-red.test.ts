import { test } from 'vitest';
import assert from 'node:assert/strict';
import { attribute, findingReds, gateOutcome, keyTest, mainRedLines, STALE_FINDING_MS, unspecificLines, verdictReds, type Failure, type MainRed } from './main-red.ts';

const testKey = (name: string): string => JSON.stringify([name, 'Assert.Equal', null, 'Assert.Equal() Failure']);
const gateKey = (gate: string): string => JSON.stringify([gate, 'run', null, '❯ src<path> (<n> tests | <n> failed) <n>ms']);
const fail = (gate: string, name: string): Failure => ({ gate, unit: name.split('.').slice(0, -1).join('.'), key: testKey(name), test: name });
const whole = (gate: string): Failure => ({ gate, unit: gate, key: gateKey(gate), test: null });
const red = (gate: string, name: string): MainRed => ({ gate, key: testKey(name), test: name, from: 'finding', at: '2026-11-12T08:00:00Z', commit: 'abc1234', stale: false });
const NOW = Date.parse('2026-11-13T08:00:00Z');
const finding = (gate: string, name: string, key: string, lastSeen = '2026-11-13T02:00:00Z') => ({
  state: 'red', gate, test: name, key, expired: false, since: '2026-11-12T08:00:00Z', lastSeen: { commit: 'abc1234', at: lastSeen },
});

test('a failure main already has, the same test under the same signature, is main\'s, and one it has not is the branch\'s', () => {
  const split = attribute([fail('reached', 'XTests.A'), fail('reached', 'YTests.B')], [red('reached', 'XTests.A')]);
  assert.deepEqual(split.main.map((each) => each.test), ['XTests.A']);
  assert.deepEqual(split.branch.map((each) => each.test), ['YTests.B']);
});

test('the same test and signature on another gate is not main\'s red', () => {
  assert.equal(attribute([fail('cli', 'XTests.A')], [red('reached', 'XTests.A')]).main.length, 0);
});

test('a finding that names no test excuses no failure, though the gate and its run\'s line are the same', () => {
  const mains = findingReds([finding('web-rules', 'web-rules', gateKey('web-rules'))], NOW);
  assert.equal(mains[0]!.test, null);
  const split = attribute([whole('web-rules'), fail('web-rules', 'src/rules.test.ts.a new rule')], mains);
  assert.deepEqual(split.main, []);
  assert.equal(split.branch.length, 2);
  assert.equal(gateOutcome([whole('web-rules')], mains), 'branch');
});

test('a test-level finding excuses exactly that test and no other of its gate', () => {
  const mains = findingReds([finding('driver', 'XTests.A', testKey('XTests.A'))], NOW);
  const split = attribute([fail('driver', 'XTests.A'), fail('driver', 'XTests.B'), whole('driver')], mains);
  assert.deepEqual(split.main.map((each) => each.test), ['XTests.A']);
  assert.deepEqual(split.branch.map((each) => each.test), ['XTests.B', null]);
});

test('a finding that names no test is said to name none on each gate this merge failed, once per gate', () => {
  const mains = findingReds([
    finding('web-rules', 'web-rules', gateKey('web-rules')), finding('web-rules', 'web-rules', JSON.stringify(['web-rules', 'run', null, 'other'])),
    finding('cli', 'cli', gateKey('cli')),
  ], NOW);
  const lines = unspecificLines(mains, ['web-rules']);
  assert.equal(lines.length, 1);
  assert.match(lines[0]!, /web-rules: 2 of main's findings name no test, so they excuse none of its failures/);
  assert.deepEqual(unspecificLines(mains, []), []);
});

test('a finding not seen again within the bound is stale, and the summary says so; one seen since is not', () => {
  const old = new Date(NOW - STALE_FINDING_MS - 60_000).toISOString();
  const [stale, fresh] = findingReds([finding('driver', 'XTests.A', testKey('XTests.A'), old), finding('driver', 'XTests.B', testKey('XTests.B'))], NOW);
  assert.deepEqual([stale!.stale, fresh!.stale], [true, false]);
  const held = attribute([fail('driver', 'XTests.A'), fail('driver', 'XTests.B')], [stale!, fresh!]).main;
  const lines = mainRedLines(held);
  assert.match(lines[0]!, /XTests\.A is main's red, last seen at abc1234 on .*; stale: main's CI has not seen it for over 24 h/);
  assert.doesNotMatch(lines[1]!, /stale/);
});

test('main\'s standing comes from its open findings and from its failing verdicts on its own tree, unless a later pass cleared them', () => {
  const findings = [
    finding('reached', 'XTests.A', testKey('XTests.A')),
    { ...finding('cli', 'T.t', testKey('T.t')), expired: true },
    { ...finding('cli', 'T.u', testKey('T.u')), state: 'green' },
  ];
  assert.deepEqual(findingReds(findings, NOW).map((each) => each.test), ['XTests.A']);
  const verdicts = [
    { gate: 'driver', verdict: 'FAIL', tree: 'M', at: '2026-11-12T09:00:00Z', commit: 'abc1234', signatures: [testKey('DTests.d'), gateKey('driver')] },
    { gate: 'cli', verdict: 'FAIL', tree: 'M', at: '2026-11-12T09:00:00Z', commit: 'abc1234', signatures: [testKey('CTests.c')] },
    { gate: 'cli', verdict: 'PASS', tree: 'M', at: '2026-11-12T10:00:00Z', commit: 'abc1234' },
    { gate: 'web', verdict: 'FAIL', tree: 'OTHER', at: '2026-11-12T09:00:00Z', commit: 'x', signatures: [testKey('WTests.w')] },
  ];
  assert.deepEqual(verdictReds(verdicts, 'M').map((each) => `${each.gate}:${each.test}`), ['driver:DTests.d', 'driver:null']);
});

test('a key names its test, and a gate\'s key names none', () => {
  assert.equal(keyTest(testKey('XTests.A')), 'XTests.A');
  assert.equal(keyTest(gateKey('web-rules')), null);
  assert.equal(keyTest(JSON.stringify(['driver', 'build', ['CS0001 a.cs'], null])), null);
  assert.equal(keyTest('not a key'), null);
});

test('the summary names each red it set down as main\'s, with where it was last seen, and says it stays owed to main', () => {
  const lines = mainRedLines(attribute([fail('reached', 'XTests.A')], [red('reached', 'XTests.A')]).main);
  assert.match(lines[0]!, /reached: XTests\.A is main's red, last seen at abc1234 on 2026-11-12T08:00:00Z/);
  assert.match(lines.at(-1)!, /stay owed to main/);
  assert.deepEqual(mainRedLines([]), []);
});
