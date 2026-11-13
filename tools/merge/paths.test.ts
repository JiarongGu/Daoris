import { test } from 'vitest';
import assert from 'node:assert/strict';
import { posix, win32 } from 'node:path';
import { diffPaths, isInside, zPaths, type GitRun } from './paths.ts';

test('a message file is inside local/ only when it is below it: not on another drive, not the folder, a dotted name is', () => {
  const local = win32.join('C:', 'repo', 'local');
  assert.equal(isInside(local, win32.join(local, 'msg.txt'), win32), true);
  assert.equal(isInside(local, win32.join(local, '..notes'), win32), true, 'a name that starts with two dots is inside');
  assert.equal(isInside(local, win32.join('D:', 'other', 'msg.txt'), win32), false, 'another drive');
  assert.equal(isInside(local, local, win32), false, 'the folder itself');
  assert.equal(isInside(local, win32.join('C:', 'repo', 'msg.txt'), win32), false);
  assert.equal(isInside('/repo/local', '/repo/local/a/b.txt', posix), true);
  assert.equal(isInside('/repo/local', '/repo/local/../x', posix), false);
});

test('a -z listing gives each path whole, a name with spaces or any letter included, and nothing for an empty one', () => {
  assert.deepEqual(zPaths('docs/café notes.md\0a.txt\0'), ['docs/café notes.md', 'a.txt']);
  assert.deepEqual(zPaths(''), []);
});

test('a change\'s paths are asked of git with -z, no renames and no optional locks, and a failure allowed is null', () => {
  const asked: Array<[string[], boolean]> = [];
  const answering = (status: number, out: string): GitRun => (_root, args, { allowFail }) => {
    asked.push([args, allowFail]);
    return { status, out };
  };
  assert.deepEqual(diffPaths(answering(0, 'a.txt\0b/c.md\0'), 'root', ['HEAD...feature']), ['a.txt', 'b/c.md']);
  assert.deepEqual(asked[0], [['--no-optional-locks', 'diff', '-z', '--name-only', '--no-renames', 'HEAD...feature'], false]);
  assert.equal(diffPaths(answering(128, ''), 'root', ['a', 'b'], { allowFail: true }), null);
  assert.equal(asked[1]![1], true);
});
