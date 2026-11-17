import { test } from 'vitest';
import assert from 'node:assert/strict';
import { fixtureFolder } from './fixture-home.ts';
import { appendWatch, readWatch, startWatch, watchGates, watchHold, type WatchRun } from './main-watch.ts';

const at = (minute: number): string => `2026-11-17T08:${String(minute).padStart(2, '0')}:00.000Z`;
const failure = { gate: 'cli-tests', unit: 'home-census', test: 'src/Daoris.Cli/test/home-census.test.ts > every tool names the home', key: '["home-census","run",null,"x"]', message: 'AssertionError: tools/merge/new.ts reads the home' };
const red = (commit: string, minute: number): WatchRun => ({ commit, state: 'red', at: at(minute), ended: at(minute + 5), brief: `local/scratch/main-watch-${commit.slice(0, 7)}/brief.md`, failures: [failure] });
const green = (commit: string, minute: number): WatchRun => ({ commit, state: 'green', at: at(minute), ended: at(minute + 5), failures: [] });
const nothingKnown = { alive: () => false, known: () => false };

test('a landing with a planted red on main holds the next merge, naming the brief', () => {
  const held = watchHold([green('aaaaaaa1', 0), red('bbbbbbb2', 10)], { ...nothingKnown, mainTip: 'bbbbbbb2' });
  assert.equal(held.hold, true);
  assert.match(held.why, /main is red at bbbbbbb \(cli-tests\)/);
  assert.match(held.why, /local\/scratch\/main-watch-bbbbbbb\/brief\.md/);
  assert.match(held.why, /--queue --known/);
  assert.equal(held.watchOn, null, 'the red is on main\'s tip: nothing new to watch');
});

test('a green main, or a red named known, holds nothing', () => {
  assert.equal(watchHold([], { ...nothingKnown, mainTip: 'a' }).hold, false);
  assert.equal(watchHold([red('bbbbbbb2', 10), green('ccccccc3', 20)], { ...nothingKnown, mainTip: 'ccccccc3' }).hold, false);
  assert.equal(watchHold([red('bbbbbbb2', 10)], { alive: () => false, mainTip: 'bbbbbbb2', known: (each) => each.key === failure.key }).hold, false);
});

test('a fix committed on main is watched before the hold lifts: the red holds until a watch of the new tip is green', () => {
  const moved = watchHold([red('bbbbbbb2', 10)], { ...nothingKnown, mainTip: 'ccccccc3' });
  assert.deepEqual([moved.hold, moved.watchOn], [true, 'ccccccc3']);
  const running: WatchRun = { commit: 'ccccccc3', state: 'running', at: at(30), pid: 41 };
  const watching = watchHold([red('bbbbbbb2', 10), running], { ...nothingKnown, alive: (pid) => pid === 41, mainTip: 'ccccccc3' });
  assert.deepEqual([watching.hold, watching.watchOn], [true, null], 'a watch already runs there');
  const died = watchHold([red('bbbbbbb2', 10), running], { ...nothingKnown, mainTip: 'ccccccc3' });
  assert.equal(died.watchOn, 'ccccccc3', 'a watch whose process died is started again');
});

test('the watch runs the cheap suites the plan declares, and its runs are kept in one record', () => {
  assert.deepEqual(watchGates([{ name: 'universal' }, { name: 'cli' }, { name: 'cli-tests' }, { name: 'driver' }, { name: 'tool-tests' }, { name: 'censuses' }]).map((gate) => gate.name), ['cli', 'cli-tests', 'tool-tests', 'censuses']);
  const { dir, remove } = fixtureFolder('main-watch');
  try {
    appendWatch(dir, { commit: 'bbbbbbb2', state: 'running', at: at(10), pid: 7 });
    appendWatch(dir, red('bbbbbbb2', 10));
    assert.deepEqual(readWatch(dir).map((run) => run.state), ['red'], "a run's running line is replaced by the line that settles it");
    const started: string[][] = [];
    const lines = startWatch('bbbbbbb2cafe', { start: (args) => { started.push(args); return 42; } });
    assert.deepEqual(started, [['tools/merge-branch.mjs', '--main-watch', 'bbbbbbb2cafe']]);
    assert.match(lines[0]!, /main watch: started on bbbbbbb .*pid 42/);
  } finally {
    remove();
  }
});
