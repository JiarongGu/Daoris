import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseTree } from '../../src/family/gitread.ts';
import { unsafePath } from '../../src/family/writepins.ts';

/** What `family materialize` reads of a tree and which of its paths it refuses to write, with no git and no disk. */

const entry = (mode: string, name: string, id: string) => Buffer.concat([Buffer.from(`${mode} ${name}\0`, 'utf8'), Buffer.from(id, 'hex')]);

test('a tree object is read entry by entry: mode, name and id, a folder\'s mode as git writes it', () => {
  const file = 'a'.repeat(40);
  const folder = 'b'.repeat(40);
  const data = Buffer.concat([entry('100755', 'run.sh', file), entry('40000', 'src', folder), entry('100644', 'naïve.txt', file)]);

  assert.deepEqual(parseTree(data, 20), [
    { mode: '100755', name: 'run.sh', id: file },
    { mode: '40000', name: 'src', id: folder },
    { mode: '100644', name: 'naïve.txt', id: file },
  ]);
});

test('a tree cut short is a tool error, never read as fewer entries', () => {
  const data = entry('100644', 'a.txt', 'c'.repeat(40)).subarray(0, 20);
  assert.throws(() => parseTree(data, 20), /cut short/);
});

test('a SHA-256 tree\'s ids are 32 bytes', () => {
  const id = 'd'.repeat(64);
  assert.deepEqual(parseTree(entry('100644', 'a.txt', id), 32), [{ mode: '100644', name: 'a.txt', id }]);
});

test('a path out of the producer\'s folder, into a .git or not a name on this machine is refused; any other is written', () => {
  for (const path of ['../out', 'a/../../out', 'a/./b', '.git/config', 'deep/.GIT/hooks/x', 'a\\b', 'a//b']) {
    assert.equal(unsafePath(path, 'linux'), true, path);
  }
  for (const path of ['a:b', 'what?', 'a|b']) {
    assert.equal(unsafePath(path, 'win32'), true, path);
    assert.equal(unsafePath(path, 'linux'), false, path);
  }
  for (const path of ['src/deep/run.sh', '.gitignore', '.github/workflows/ci.yml', 'naïve.txt']) assert.equal(unsafePath(path, 'win32'), false, path);
});
