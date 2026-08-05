import { test } from 'node:test';
import assert from 'node:assert/strict';
import type { Fixture } from './_fixture.ts';
import { makeFixture } from './_fixture.ts';
import { compareVersions, notesBetween } from '../src/notes.ts';

const CHANGELOG = `# Canon changelog

Why each version changed. Printed to consumers by \`status\` and \`sync\`.

## 0.3.0

- \`sensitive-info\` now covers commit messages, after a machine path reached history.

## 0.2.0

- Retired \`legacy-layout\`; it described a structure no repository still has.

## 0.1.0

- The first canon.
`;

function seeded() {
  const fx = makeFixture('notes');
  fx.write('CHANGELOG.md', CHANGELOG);
  return fx;
}

test('versions compare numerically, not as strings', () => {
  assert.ok(compareVersions('0.10.0', '0.9.0') > 0, '10 is after 9');
  assert.ok(compareVersions('0.1.0', '0.1.0') === 0);
  assert.ok(compareVersions('1.0.0', '0.99.99') > 0);
});

/**
 * The whole point of TOOL4: a consumer that sees `changed rules/sensitive-info.md`
 * still has to guess whether it matters. This is the "why", and it comes from
 * the canon shipped in the package, so it stays offline (D8, D11).
 */
test('only the versions between the lock and the canon are returned', () => {
  const fx = seeded();
  const notes = notesBetween(fx.root, '0.1.0', '0.3.0');
  assert.deepEqual(notes.map((n) => n.version), ['0.2.0', '0.3.0']);
  assert.match(notes[1]!.body, /commit messages/);
  // The version already installed is not news.
  assert.equal(notes.some((n) => n.version === '0.1.0'), false);
  fx.cleanup();
});

test('a partial upgrade stops at the version actually shipped', () => {
  const fx = seeded();
  assert.deepEqual(notesBetween(fx.root, '0.1.0', '0.2.0').map((n) => n.version), ['0.2.0']);
  fx.cleanup();
});

test('nothing to say is not an error', () => {
  const fx = seeded();
  assert.deepEqual(notesBetween(fx.root, '0.3.0', '0.3.0'), []);
  fx.cleanup();

  // A canon with no changelog at all still syncs; the notes are a courtesy.
  const bare = makeFixture('notes-bare');
  assert.deepEqual(notesBetween(bare.root, '0.1.0', '0.2.0'), []);
  bare.cleanup();
});
