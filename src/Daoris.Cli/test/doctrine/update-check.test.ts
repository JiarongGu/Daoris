import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, type Fixture } from '../_fixture.ts';
import { readLock } from '../../src/doctrine/config.ts';
import { commandStatus } from '../../src/doctrine/commands.ts';
import { canonDigest, readCanon } from '../../src/doctrine/canon.ts';
import { commandSync } from '../../src/doctrine/materialize.ts';

/**
 * The update check by content: `status` runs the sync's own plan whatever the versions say, because the
 * canon's stamp stays at 0.0.1 through every 0.0.x build. Which side is newer only the version can say, so files are
 * called behind only when the lock's version is older; equal versions over a different canon are said as a difference
 * whose direction cannot be told, both digests named. Drift stays the disk against the lock.
 */

const doc = (name: string, body = 'x') => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n${body}\n`;
const skill = (name: string) => `---\nname: ${name}\ndescription: d\n---\nsteps\n`;

/** An adopter synced by `daoris sync` from canon 0.1.0, and that canon. */
function adopter(name: string): { canon: Fixture; repo: Fixture; done(): void } {
  const canon = makeFixture(`${name}-canon`);
  canon.write('canon.json', '{"version":"0.1.0"}');
  canon.write('core/rules/sensitive-info.md', doc('sensitive-info'));
  canon.write('core/knowledge/storage.md', doc('storage'));
  canon.write('core/knowledge/notes.md', doc('notes'));
  const repo = makeFixture(`${name}-repo`);
  repo.write('daoris.json', '{"source":"s","packs":[]}');
  process.env.DAORIS_CANON = canon.root;
  assert.equal(commandSync({ root: repo.root, argv: [], write: () => {}, packageRoot: '' }), 0);
  return {
    canon, repo,
    done() {
      delete process.env.DAORIS_CANON;
      canon.cleanup();
      repo.cleanup();
    },
  };
}

/** The canon moves on: one document reworded, one skill added. */
function moveCanon(canon: Fixture): void {
  canon.write('core/knowledge/storage.md', doc('storage', 'REWORDED'));
  canon.write('core/skills/co-working/SKILL.md', skill('co-working'));
}

/** An edit here: drift, the disk against the lock. */
const editHere = (repo: Fixture) =>
  repo.write('.claude/knowledge/notes.md', `${repo.read('.claude/knowledge/notes.md')}a local line\n`);

function status(repo: Fixture, ...argv: string[]): { code: number; text: string } {
  const out: string[] = [];
  const code = commandStatus({ root: repo.root, argv, write: (s: string) => out.push(s), packageRoot: '' });
  return { code, text: out.join('\n') };
}

test('sync records the digest of the canon it wrote from, beside its version', () => {
  const fx = adopter('update-digest');
  const lock = readLock(fx.repo.root)!;
  assert.equal(lock.canonVersion, '0.1.0');
  assert.equal(lock.canonDigest, canonDigest(readCanon(fx.canon.root)));
  assert.match(lock.canonDigest!, /^[0-9a-f]{64}$/);

  fx.done();
});

test('the canon digest moves with a document, and not with the stamp or the changelog', () => {
  const fx = adopter('update-digest-moves');
  const at = () => canonDigest(readCanon(fx.canon.root));
  const first = at();
  fx.canon.write('canon.json', '{"version":"0.2.0"}');
  fx.canon.write('CHANGELOG.md', '# Canon changelog\n\n## 0.2.0\n\n- words\n');
  assert.equal(at(), first);
  fx.canon.write('core/knowledge/storage.md', doc('storage', 'moved'));
  assert.notEqual(at(), first);

  fx.done();
});

test('a newer canon: status names the file it changed and the one it added, and the edit stays drift', () => {
  const fx = adopter('update-newer');
  fx.canon.write('canon.json', '{"version":"0.2.0"}');
  moveCanon(fx.canon);
  editHere(fx.repo);

  const { code, text } = status(fx.repo);
  assert.equal(code, 0);
  assert.match(text, /^ {2}update {8}canon 0\.2\.0 available; the lock is behind at 0\.1\.0 — run 'daoris sync'$/m);
  assert.match(text, /^ {18}changed {2}knowledge\/storage\.md$/m);
  assert.match(text, /^ {18}new {6}skills\/co-working\/SKILL\.md$/m);
  assert.match(text, /^ {2}drifted {7}knowledge\/notes\.md$/m);
  assert.doesNotMatch(text, /changed {2}knowledge\/notes\.md/, 'an edited file is drift, never an update');

  const report = JSON.parse(status(fx.repo, '--json').text);
  assert.equal(report.update.direction, 'newer');
  assert.deepEqual(report.update.changed, ['knowledge/storage.md']);
  assert.deepEqual(report.update.added, ['skills/co-working/SKILL.md']);
  assert.deepEqual(report.drifted, ['knowledge/notes.md']);

  fx.done();
});

test('a stamp that does not move no longer hides an update: equal versions over another canon are a difference', () => {
  const fx = adopter('update-tie');
  const locked = readLock(fx.repo.root)!.canonDigest!;
  moveCanon(fx.canon);
  const carried = canonDigest(readCanon(fx.canon.root));
  assert.notEqual(carried, locked);

  const { text } = status(fx.repo);
  assert.match(text, /^ {2}update {8}differs from this CLI's canon \(newer or older cannot be told\): both are canon 0\.1\.0$/m);
  assert.match(text, new RegExp(`^ {18}this CLI's canon {2}${carried.slice(0, 12)}$`, 'm'));
  assert.match(text, new RegExp(`^ {18}the lock's canon {2}${locked.slice(0, 12)}$`, 'm'));
  assert.match(text, /^ {18}differs {11}knowledge\/storage\.md$/m);
  assert.match(text, /^ {18}not in the lock {3}skills\/co-working\/SKILL\.md$/m);
  // Never a rollback: a difference with no direction is not behind, and sync is not offered as the fix.
  assert.doesNotMatch(text, /behind|run 'daoris sync'/);

  const report = JSON.parse(status(fx.repo, '--json').text);
  assert.equal(report.update.direction, 'unknown');
  assert.deepEqual(report.update.digest, { carried, locked });
  assert.deepEqual(report.update.changed, ['knowledge/storage.md']);
  assert.deepEqual(report.update.added, ['skills/co-working/SKILL.md']);
  assert.equal(report.canonDigest, locked);

  fx.done();
});

test('a lock from before the digest, at the same version, is a difference whose side cannot be told', () => {
  const fx = adopter('update-tie-old-lock');
  const lock = JSON.parse(fx.repo.read('daoris.lock'));
  delete lock.canonDigest;
  fx.repo.write('daoris.lock', `${JSON.stringify(lock, null, 2)}\n`);
  moveCanon(fx.canon);

  const { text } = status(fx.repo);
  assert.match(text, /differs from this CLI's canon \(newer or older cannot be told\)/);
  assert.match(text, /^ {18}the lock's canon {2}none recorded$/m);
  assert.equal(JSON.parse(status(fx.repo, '--json').text).update.digest.locked, null);

  fx.done();
});

test('the same version and the same canon: status says no update', () => {
  const fx = adopter('update-none');
  editHere(fx.repo);

  const { text } = status(fx.repo);
  assert.doesNotMatch(text, /update/);
  assert.equal(JSON.parse(status(fx.repo, '--json').text).update, null);

  fx.done();
});
