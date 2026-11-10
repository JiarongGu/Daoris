import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture } from '../../test/_fixture.ts';
import { tool } from '../support/merge-branch.ts';

// test-map: reads the merge tool's lock holder identity only
// reaches: tool-tests — the unit and its test are the tool-tests gate's

const BEGAN = Date.parse('2026-11-01T08:00:00Z');
const LATER = Date.parse('2026-11-05T08:00:00Z');

test('CODESTRREV3a: a gate lock and a CI watch whose pid a later process took read as no run holds them', () => {
  const fx = makeFixture('merge-branch-holder-running');
  try {
    mkdirSync(join(fx.root, 'local'), { recursive: true });
    writeFileSync(join(fx.root, tool.GATE_TREE_LOCK), `${JSON.stringify({ pid: 4242, commit: 'a'.repeat(40), at: '2026-11-01T08:00:05Z', started: BEGAN, holder: 'ci' })}\n`);
    writeFileSync(join(fx.root, tool.CI_WATCH), `${JSON.stringify({ pid: 4242, at: '2026-11-01T08:00:05Z', started: BEGAN })}\n`);
    const alive = () => true;
    assert.equal(tool.readGateLock(fx.root, alive, () => BEGAN + 500)!.running, true, 'the same process still runs');
    assert.equal(tool.readCiWatch(fx.root, alive, () => BEGAN + 500)!.running, true);
    assert.equal(tool.readGateLock(fx.root, alive, () => LATER)!.running, false, 'another process holds the pid now');
    assert.equal(tool.readCiWatch(fx.root, alive, () => LATER)!.running, false);

    const taken = tool.takeGateLock(fx.root, { commit: 'b'.repeat(40), alive, startOf: () => LATER, end: () => [], endStrays: () => [] });
    assert.equal(taken.stale!.pid, 4242, 'a lock whose pid was taken again is taken over, not refused');
  } finally {
    fx.cleanup();
  }
});

test('CODESTRREV3b: a take-over ends the dead holder\'s children created between its start and the pid being taken again, never later ones', () => {
  const fx = makeFixture('merge-branch-holder-children');
  try {
    mkdirSync(join(fx.root, 'local'), { recursive: true });
    writeFileSync(join(fx.root, tool.GATE_TREE_LOCK), `${JSON.stringify({ pid: 4242, commit: 'a'.repeat(40), at: '2026-11-01T08:00:05Z', started: BEGAN, holder: 'full' })}\n`);
    const asked: Array<[number, number, number | null]> = [];
    tool.takeGateLock(fx.root, {
      commit: 'b'.repeat(40), alive: () => true, startOf: (pid: number) => (pid === 4242 ? LATER : BEGAN),
      end: (pid, since, until) => { asked.push([pid, since, until]); return []; }, endStrays: () => [],
    });
    assert.deepEqual(asked, [[4242, BEGAN, LATER]], 'from the holder\'s own start, to the start of the process that took its pid');

    writeFileSync(join(fx.root, tool.GATE_TREE_LOCK), `${JSON.stringify({ pid: 4242, commit: 'a'.repeat(40), at: '2026-11-01T08:00:05Z', started: BEGAN, holder: 'full' })}\n`);
    asked.length = 0;
    tool.takeGateLock(fx.root, {
      commit: 'b'.repeat(40), alive: () => false, startOf: () => null,
      end: (pid, since, until) => { asked.push([pid, since, until]); return []; }, endStrays: () => [],
    });
    assert.deepEqual(asked, [[4242, BEGAN, null]], 'a pid nobody took again has no upper bound');
  } finally {
    fx.cleanup();
  }
});
