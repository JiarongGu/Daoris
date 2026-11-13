import { test } from 'vitest';
import assert from 'node:assert/strict';
import { changedCode, nightLines, tiersOf } from './tiers.ts';

const gates = JSON.stringify({ tiers: { needs: { git: ['src/GitDoor/', 'src/Ports/Git/'], browser: ['src/Ports/Browser/IInAppBrowser.cs'] } } });

test('the tiers are the gates file\'s, and a file with none declares none', () => {
  assert.deepEqual(tiersOf(gates).git, ['src/GitDoor/', 'src/Ports/Git/']);
  assert.deepEqual(tiersOf('{}'), {});
  assert.deepEqual(tiersOf('not json'), {});
});

test('a class that needs git runs only for a changed path in git\'s code, and one that needs a browser for that file', () => {
  const tiers = tiersOf(gates);
  assert.deepEqual(changedCode(['git'], ['src/GitDoor/X.cs', 'src/Other/Y.cs'], tiers), ['src/GitDoor/X.cs']);
  assert.deepEqual(changedCode(['git'], ['src/Other/Y.cs'], tiers), []);
  assert.deepEqual(changedCode(['browser'], ['src/Ports/Browser/IInAppBrowser.cs', 'src/Ports/Browser/Other.cs'], tiers), ['src/Ports/Browser/IInAppBrowser.cs']);
  assert.deepEqual(changedCode(['filesystem'], ['src/GitDoor/X.cs'], tiers), [], 'a dependency the file does not name is never changed');
});

test('what a merge leaves to the night is named by dependency and counted', () => {
  assert.deepEqual(nightLines({}), []);
  assert.match(nightLines({ process: 3, git: 2 })[0] as string, /5 dependency classes left to the nightly run \(--ci --night\).*git 2, process 3/);
});
