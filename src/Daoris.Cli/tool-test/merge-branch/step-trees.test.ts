import { test } from 'node:test';
import assert from 'node:assert/strict';
import { tool } from '../support/merge-branch.ts';

type Ran = { status?: number | null; stdout?: string | null };

/** `endTree` on Windows over a stand-in `taskkill` answering each status in turn, with the calls made and the pauses asked between them. */
function ended(statuses: (number | null)[]) {
  const calls: [string, string[]][] = [];
  const paused: number[] = [];
  const answer = tool.endTree(4242, {
    platform: 'win32',
    run: (command, args): Ran => { calls.push([command, args]); return { status: statuses[calls.length - 1] ?? null }; },
    pause: (ms) => { paused.push(ms); },
  });
  return { answer, calls, paused };
}

test('a step\'s tree is ended by its own pid: taskkill /T on Windows, every descendant ps lists elsewhere, never by image name', () => {
  const calls: [string, string[]][] = [];
  const killed: number[] = [];
  assert.equal(tool.endTree(4242, { platform: 'win32', run: (command, args) => { calls.push([command, args]); return { status: 0 }; } }), true);
  assert.deepEqual(calls, [['taskkill', ['/T', '/F', '/PID', '4242']]]);
  // Elsewhere a kill reaches the one process it names, and a shell's command outlives it: `ps` says what is under it.
  const listing = ['    1     0', '   10     1', '   11    10', '   12    11', '   13    10', '   20     1', ''].join('\n');
  assert.deepEqual([...tool.descendants(listing, 10)].sort((a, b) => a - b), [11, 12, 13]);
  assert.deepEqual(tool.descendants(listing, 20), []);
  calls.length = 0;
  assert.equal(tool.endTree(10, {
    platform: 'linux',
    run: (command, args) => { calls.push([command, args]); return { stdout: listing }; },
    kill: (pid) => { killed.push(pid); },
  }), true);
  assert.deepEqual(calls.map(([command]) => command), ['ps']);
  assert.deepEqual([...killed].sort((a, b) => a - b), [10, 11, 12, 13]);
});

test('TOOLTESTHANG1: taskkill\'s own status is read: a process already gone is ended, a refusal is asked again and said once it stays', () => {
  // 128 is taskkill's "no such process": nothing is left to end.
  assert.deepEqual(ended([128]), { answer: true, calls: [['taskkill', ['/T', '/F', '/PID', '4242']]], paused: [] });
  const again = ended([1, 0]);
  assert.equal(again.answer, true);
  assert.equal(again.calls.length, 2);
  assert.equal(again.paused.length, 1);
  // A tree that will not end is answered false after the last try, never thrown, so its caller can name the leak and go on.
  const refused = ended([1, 255, null, 0]);
  assert.equal(refused.answer, false);
  assert.equal(refused.calls.length, 3);
  assert.equal(refused.paused.length, 2);
});
