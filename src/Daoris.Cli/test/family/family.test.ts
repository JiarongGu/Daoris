import { test } from 'node:test';
import assert from 'node:assert/strict';
import { captureError } from '../_fixture.ts';
import { judgeFamily, readFamily, readLock, type MemberState } from '../../src/family/family.ts';

/**
 * The family's graph, judged from what each member declares: a cycle refused with its path, a non-member,
 * an edge to an equal or higher layer, a denied pair, a pin with no `uses` and a second pin of one producer refused; a `uses` with
 * no pin warned. No git here: what a pin's commit is, is `family-check.test.ts`'s.
 */

const ENGINE = 'a'.repeat(40);

function member(uses: string[], pins: { producer: string; commit: string }[] = [], checkout = true): MemberState {
  return { checkout, uses, pins };
}

const codes = (findings: { verdict: string; code: string }[]) => findings.map((f) => `${f.verdict}:${f.code}`).sort();

test('a family file names its layers lowest first and its denied pairs; its members are the layers\' names', () => {
  const family = readFamily(JSON.stringify({
    layers: [['engine'], ['game', 'tools']],
    denied: [{ consumer: 'game', producer: 'tools', why: 'tools build the game, never the other way' }],
  }), 'daoris.family.json');

  assert.deepEqual(family.layers, [['engine'], ['game', 'tools']]);
  assert.deepEqual(family.denied, [{ consumer: 'game', producer: 'tools', why: 'tools build the game, never the other way' }]);
});

test('a family file of the wrong shape is a tool error naming the file, never a family read as empty', () => {
  for (const text of ['[]', '{}', '{"layers": "engine"}', '{"layers": [["engine"], "game"]}', '{"layers": [["engine"]], "denied": [{"consumer": "x"}]}', 'not json']) {
    const error = captureError(() => readFamily(text, 'daoris.family.json'));
    assert.equal(error.exitCode, 2, text);
    assert.match(error.message, /daoris\.family\.json/, text);
  }
});

test('a member named in two layers is refused by the reading', () => {
  const error = captureError(() => readFamily(JSON.stringify({ layers: [['engine'], ['game', 'engine']] }), 'daoris.family.json'));
  assert.equal(error.exitCode, 2);
  assert.match(error.message, /`engine` is named in two layers/);
});

test('a lock is a list of pins, each a producer and a commit; another shape is a tool error naming the file', () => {
  assert.deepEqual(readLock(JSON.stringify({ pins: [{ producer: 'engine', commit: ENGINE }] }), 'game/daoris.family.lock'),
    [{ producer: 'engine', commit: ENGINE }]);
  for (const text of ['{}', '{"pins": {}}', '{"pins": [{"producer": "engine"}]}', '{"pins": [{"producer": 1, "commit": "x"}]}']) {
    const error = captureError(() => readLock(text, 'game/daoris.family.lock'));
    assert.equal(error.exitCode, 2, text);
    assert.match(error.message, /game\/daoris\.family\.lock/, text);
  }
});

test('game to engine, pinned, is green', () => {
  const family = { layers: [['engine'], ['game']], denied: [] };
  const findings = judgeFamily(family, new Map([
    ['engine', member([])],
    ['game', member(['engine'], [{ producer: 'engine', commit: ENGINE }])],
  ]));
  assert.deepEqual(findings, []);
});

test('a uses with no pin is warned, never refused: an edge may be declared before its first pin', () => {
  const family = { layers: [['engine'], ['game']], denied: [] };
  const findings = judgeFamily(family, new Map([['engine', member([])], ['game', member(['engine'])]]));
  assert.deepEqual(codes(findings), ['warned:no-pin']);
  assert.match(findings[0]!.message, /game uses engine and pins no commit of it/);
});

test('a cycle is refused with its path', () => {
  const family = { layers: [['engine', 'game', 'server']], denied: [] };
  const findings = judgeFamily(family, new Map([
    ['engine', member(['server'])],
    ['game', member(['engine'])],
    ['server', member(['game'])],
  ]));
  const cycle = findings.filter((f) => f.code === 'cycle');
  assert.equal(cycle.length, 1, 'one cycle is said once, not once per member on it');
  assert.equal(cycle[0]!.verdict, 'refused');
  assert.match(cycle[0]!.message, /engine → server → game → engine/);
});

test('an edge to an equal or a higher layer is refused, naming both layers', () => {
  const family = { layers: [['engine'], ['game', 'tools']], denied: [] };
  const findings = judgeFamily(family, new Map([
    ['engine', member(['game'])],
    ['game', member(['tools'])],
    ['tools', member([])],
  ]));
  const layer = findings.filter((f) => f.code === 'layer').map((f) => f.message).sort();
  assert.equal(layer.length, 2);
  assert.match(layer[0]!, /engine → game: engine is in layer 1 and game in layer 2/);
  assert.match(layer[1]!, /game → tools: game and tools are both in layer 2/);
});

test('a uses naming a repository outside the family is refused as a non-member', () => {
  const family = { layers: [['engine'], ['game']], denied: [] };
  const findings = judgeFamily(family, new Map([['engine', member([])], ['game', member(['engine', 'server'])]]));
  assert.deepEqual(codes(findings), ['refused:non-member', 'warned:no-pin']);
  assert.match(findings.find((f) => f.code === 'non-member')!.message, /game uses server, which is no member of the family/);
});

test('a denied pair is refused with its reason', () => {
  const family = { layers: [['engine', 'tools'], ['game']], denied: [{ consumer: 'game', producer: 'tools', why: 'tools build the game' }] };
  const findings = judgeFamily(family, new Map([
    ['engine', member([])], ['tools', member([])],
    ['game', member(['engine', 'tools'], [{ producer: 'engine', commit: ENGINE }, { producer: 'tools', commit: ENGINE }])],
  ]));
  assert.deepEqual(codes(findings), ['refused:denied']);
  assert.match(findings[0]!.message, /game → tools is denied: tools build the game/);
});

test('a pin of a producer the consumer does not use is refused, and so is a second pin of one producer', () => {
  const family = { layers: [['engine', 'audio'], ['game']], denied: [] };
  const findings = judgeFamily(family, new Map([
    ['engine', member([])], ['audio', member([])],
    ['game', member(['engine'], [
      { producer: 'engine', commit: ENGINE }, { producer: 'engine', commit: 'b'.repeat(40) }, { producer: 'audio', commit: ENGINE },
    ])],
  ]));
  assert.deepEqual(codes(findings), ['refused:no-uses', 'refused:second-pin']);
  assert.match(findings.find((f) => f.code === 'no-uses')!.message, /game pins audio and does not use it/);
  assert.match(findings.find((f) => f.code === 'second-pin')!.message, /game pins engine twice/);
});

test('a member with no checkout beside the family file is refused, and its edges are not guessed', () => {
  const family = { layers: [['engine'], ['game']], denied: [] };
  const findings = judgeFamily(family, new Map([['engine', member([], [], false)], ['game', member(['engine'])]]));
  assert.deepEqual(codes(findings), ['refused:no-checkout', 'warned:no-pin']);
  assert.match(findings.find((f) => f.code === 'no-checkout')!.message, /engine has no checkout/);
});
