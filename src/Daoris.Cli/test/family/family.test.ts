import { test } from 'node:test';
import assert from 'node:assert/strict';
import { captureError } from '../_fixture.ts';
import { judgeFamily, readFamily, readLock, type MemberState } from '../../src/family/family.ts';

/**
 * The family's graph, judged from what each member declares: a cycle refused with its path, a non-member,
 * an edge to an equal or higher layer, a denied pair, a pin with no `uses` and a second pin of one producer refused; a `uses` with
 * no pin warned. No git here: what a pin's commit is, is `family-check.test.ts`'s.
 */

const ALPHA = 'a'.repeat(40);

function member(uses: string[], pins: { producer: string; commit: string }[] = [], checkout = true): MemberState {
  return { checkout, uses, pins };
}

const codes = (findings: { verdict: string; code: string }[]) => findings.map((f) => `${f.verdict}:${f.code}`).sort();

test('a family file names its layers lowest first and its denied pairs; its members are the layers\' names', () => {
  const family = readFamily(JSON.stringify({
    layers: [['alpha'], ['beta', 'epsilon']],
    denied: [{ consumer: 'beta', producer: 'epsilon', why: 'a pair the family file keeps apart' }],
  }), 'daoris.family.json');

  assert.deepEqual(family.layers, [['alpha'], ['beta', 'epsilon']]);
  assert.deepEqual(family.denied, [{ consumer: 'beta', producer: 'epsilon', why: 'a pair the family file keeps apart' }]);
});

test('a family file of the wrong shape is a tool error naming the file, never a family read as empty', () => {
  for (const text of ['[]', '{}', '{"layers": "alpha"}', '{"layers": [["alpha"], "beta"]}', '{"layers": [["alpha"]], "denied": [{"consumer": "x"}]}', 'not json']) {
    const error = captureError(() => readFamily(text, 'daoris.family.json'));
    assert.equal(error.exitCode, 2, text);
    assert.match(error.message, /daoris\.family\.json/, text);
  }
});

test('a member named in two layers is refused by the reading', () => {
  const error = captureError(() => readFamily(JSON.stringify({ layers: [['alpha'], ['beta', 'alpha']] }), 'daoris.family.json'));
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /`alpha` is named in two layers/);
});

test('a lock is a list of pins, each a producer and a commit; another shape is a tool error naming the file', () => {
  assert.deepEqual(readLock(JSON.stringify({ pins: [{ producer: 'alpha', commit: ALPHA }] }), 'beta/daoris.family.lock'),
    [{ producer: 'alpha', commit: ALPHA }]);
  for (const text of ['{}', '{"pins": {}}', '{"pins": [{"producer": "alpha"}]}', '{"pins": [{"producer": 1, "commit": "x"}]}']) {
    const error = captureError(() => readLock(text, 'beta/daoris.family.lock'));
    assert.equal(error.exitCode, 2, text);
    assert.match(error.message, /beta\/daoris\.family\.lock/, text);
  }
});

test('beta to alpha, pinned, is green', () => {
  const family = { layers: [['alpha'], ['beta']], denied: [] };
  const findings = judgeFamily(family, new Map([
    ['alpha', member([])],
    ['beta', member(['alpha'], [{ producer: 'alpha', commit: ALPHA }])],
  ]));
  assert.deepEqual(findings, []);
});

test('a uses with no pin is warned, never refused: an edge may be declared before its first pin', () => {
  const family = { layers: [['alpha'], ['beta']], denied: [] };
  const findings = judgeFamily(family, new Map([['alpha', member([])], ['beta', member(['alpha'])]]));
  assert.deepEqual(codes(findings), ['warned:no-pin']);
  assert.match(findings[0]!.message, /beta uses alpha and pins no commit of it/);
});

test('a cycle is refused with its path', () => {
  const family = { layers: [['alpha', 'beta', 'gamma']], denied: [] };
  const findings = judgeFamily(family, new Map([
    ['alpha', member(['gamma'])],
    ['beta', member(['alpha'])],
    ['gamma', member(['beta'])],
  ]));
  const cycle = findings.filter((f) => f.code === 'cycle');
  assert.equal(cycle.length, 1, 'one cycle is said once, not once per member on it');
  assert.equal(cycle[0]!.verdict, 'refused');
  assert.match(cycle[0]!.message, /alpha → gamma → beta → alpha/);
});

test('an edge to an equal or a higher layer is refused, naming both layers', () => {
  const family = { layers: [['alpha'], ['beta', 'epsilon']], denied: [] };
  const findings = judgeFamily(family, new Map([
    ['alpha', member(['beta'])],
    ['beta', member(['epsilon'])],
    ['epsilon', member([])],
  ]));
  const layer = findings.filter((f) => f.code === 'layer').map((f) => f.message).sort();
  assert.equal(layer.length, 2);
  assert.match(layer[0]!, /alpha → beta: alpha is in layer 1 and beta in layer 2/);
  assert.match(layer[1]!, /beta → epsilon: beta and epsilon are both in layer 2/);
});

test('a uses naming a repository outside the family is refused as a non-member', () => {
  const family = { layers: [['alpha'], ['beta']], denied: [] };
  const findings = judgeFamily(family, new Map([['alpha', member([])], ['beta', member(['alpha', 'gamma'])]]));
  assert.deepEqual(codes(findings), ['refused:non-member', 'warned:no-pin']);
  assert.match(findings.find((f) => f.code === 'non-member')!.message, /beta uses gamma, which is no member of the family/);
});

test('a denied pair is refused with its reason', () => {
  const family = { layers: [['alpha', 'epsilon'], ['beta']], denied: [{ consumer: 'beta', producer: 'epsilon', why: 'a pair the family file keeps apart' }] };
  const findings = judgeFamily(family, new Map([
    ['alpha', member([])], ['epsilon', member([])],
    ['beta', member(['alpha', 'epsilon'], [{ producer: 'alpha', commit: ALPHA }, { producer: 'epsilon', commit: ALPHA }])],
  ]));
  assert.deepEqual(codes(findings), ['refused:denied']);
  assert.match(findings[0]!.message, /beta → epsilon is denied: a pair the family file keeps apart/);
});

test('a pin of a producer the consumer does not use is refused, and so is a second pin of one producer', () => {
  const family = { layers: [['alpha', 'delta'], ['beta']], denied: [] };
  const findings = judgeFamily(family, new Map([
    ['alpha', member([])], ['delta', member([])],
    ['beta', member(['alpha'], [
      { producer: 'alpha', commit: ALPHA }, { producer: 'alpha', commit: 'b'.repeat(40) }, { producer: 'delta', commit: ALPHA },
    ])],
  ]));
  assert.deepEqual(codes(findings), ['refused:no-uses', 'refused:second-pin']);
  assert.match(findings.find((f) => f.code === 'no-uses')!.message, /beta pins delta and does not use it/);
  assert.match(findings.find((f) => f.code === 'second-pin')!.message, /beta pins alpha twice/);
});

test('a member with no checkout beside the family file is refused, and its edges are not guessed', () => {
  const family = { layers: [['alpha'], ['beta']], denied: [] };
  const findings = judgeFamily(family, new Map([['alpha', member([], [], false)], ['beta', member(['alpha'])]]));
  assert.deepEqual(codes(findings), ['refused:no-checkout', 'warned:no-pin']);
  assert.match(findings.find((f) => f.code === 'no-checkout')!.message, /alpha has no checkout/);
});
