// The agents descriptor, the manifest's new fields and the lock's (LAYOUT3; D117 §2, §5.1).
//
// A second implementation beside `claude-code` in `src/harness.ts`, selected by the manifest's
// existing `harness` field. The lock grows additively, as D71 grew `switchedOff`: a lock that uses
// nothing new is byte for byte what it was.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { captureError, makeFixture } from './_fixture.ts';
import { layoutFixture, skill } from './_layout.ts';
import { readManifest, readLock, writeLock } from '../src/config.ts';
import { HARNESSES, resolveHarness, verifyHarnessContract } from '../src/harness.ts';
import { lockLayout } from '../src/layout.ts';
import { digestBytes } from '../src/fsx.ts';
import { DaorisError } from '../src/errors.ts';

test('the agents descriptor keeps the tiers under .agents, the region in AGENTS.md, and mirrors skills for Claude Code', () => {
  const agents = resolveHarness('agents');

  assert.equal(agents.defaultTarget, '.agents');
  assert.deepEqual(agents.tiers.rules!.region, { file: 'AGENTS.md', name: 'rules' });
  assert.equal(agents.tiers.knowledge!.dir, 'knowledge');
  assert.equal(agents.tiers.skills!.dir, 'skills');
  assert.equal(agents.tiers.skills!.entryFile, 'SKILL.md');
  assert.deepEqual(agents.pointer, { file: 'CLAUDE.md', imports: 'AGENTS.md' });
  // Where the reference links `.claude/skills` to `.agents/skills`, Daoris writes a copy (D3).
  assert.deepEqual(agents.mirror, { tier: 'skills', root: '.claude/skills', reader: 'Claude Code', skip: ['agents'] });
  assert.equal(agents.formerly, '.claude');
  // The older layout stays, unchanged, for every repository that has not moved.
  assert.equal(HARNESSES['claude-code']!.mirror, undefined);
  assert.equal(HARNESSES['claude-code']!.defaultTarget, '.claude');
});

test('a manifest naming agents resolves its target from the descriptor', () => {
  const fx = makeFixture('layout-desc-manifest');
  fx.write('daoris.json', JSON.stringify({ source: 's', harness: 'agents' }));

  const manifest = readManifest(fx.root);

  assert.equal(manifest.harnessDescriptor.id, 'agents');
  assert.equal(manifest.target, '.agents');
  assert.deepEqual(manifest.rooms, []);
  fx.cleanup();
});

/** §5.1: the skills tier would land on the very folder its mirror is written to. */
test('a target whose skills would sit on the mirror root is refused', () => {
  const fx = makeFixture('layout-desc-mirror-root');
  fx.write('daoris.json', JSON.stringify({ source: 's', harness: 'agents', target: '.claude' }));

  const error = captureError(() => readManifest(fx.root));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /\.claude\/skills/);
  assert.match(error.message, /mirror/);
  fx.cleanup();
});

/** D18 over the declared roots: a target is a folder inside the repository, never beside it. */
test('a target that leaves the repository is refused', () => {
  const fx = makeFixture('layout-desc-escape');
  fx.write('daoris.json', JSON.stringify({ source: 's', harness: 'agents', target: '../elsewhere' }));

  const error = captureError(() => readManifest(fx.root));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /\.\.\/elsewhere/);
  assert.match(error.message, /inside the repository/);
  fx.cleanup();
});

/** §5.1: a room is a folder of the repository's own, not the root, not a tier, not a mirror. */
test('a room that escapes, is the root, or sits in the target or a mirror root is refused, naming it', () => {
  for (const room of ['../sibling', '.', '', '.agents/skills', '.agents', '.claude/skills/x', 'C:/elsewhere', '/abs']) {
    const fx = makeFixture('layout-desc-rooms');
    fx.write('daoris.json', JSON.stringify({ source: 's', harness: 'agents', rooms: [room] }));

    const error = captureError(() => readManifest(fx.root));

    assert.ok(error instanceof DaorisError, `room '${room}' was accepted`);
    assert.match(error.message, /room/);
    fx.cleanup();
  }
});

test('rooms are a list of folders, normalised to forward slashes, and a duplicate is refused', () => {
  const fx = makeFixture('layout-desc-rooms-list');
  fx.write('daoris.json', JSON.stringify({ source: 's', harness: 'agents', rooms: ['src\\cli', 'docs/'] }));
  assert.deepEqual(readManifest(fx.root).rooms, ['src/cli', 'docs']);

  fx.write('daoris.json', JSON.stringify({ source: 's', harness: 'agents', rooms: ['docs', 'docs/'] }));
  assert.match(captureError(() => readManifest(fx.root)).message, /twice/);

  fx.write('daoris.json', JSON.stringify({ source: 's', harness: 'agents', rooms: 'docs' }));
  assert.match(captureError(() => readManifest(fx.root)).message, /list of folders/);
  fx.cleanup();
});

/**
 * 🔴 §5.1: the lock, not the manifest, says where the files are. Absent means the only layout written
 * before, so every lock on disk today reads as `claude-code` under `.claude`.
 */
test('the layout a lock was written under is its own fields, else claude-code under .claude', () => {
  const fx = layoutFixture('layout-desc-order');
  fx.sync();
  const manifest = readManifest(fx.repoFx.root);

  assert.deepEqual(lockLayout(fx.repoFx.root, fx.lock(), manifest).target, '.claude');
  assert.equal(lockLayout(fx.repoFx.root, fx.lock(), manifest).harness.id, 'claude-code');

  // The manifest is flipped: the files are still where the lock says.
  fx.flip('agents');
  const flipped = readManifest(fx.repoFx.root);
  assert.equal(lockLayout(fx.repoFx.root, fx.lock(), flipped).target, '.claude');
  assert.equal(lockLayout(fx.repoFx.root, fx.lock(), flipped).harness.id, 'claude-code');

  // No lock: nothing was written anywhere, so the manifest's own layout is the answer.
  assert.equal(lockLayout(fx.repoFx.root, null, flipped).target, '.agents');
  fx.cleanup();
});

test('a lock whose target escapes the repository is refused (D18)', () => {
  const fx = layoutFixture('layout-desc-lock-escape', 'agents');
  fx.sync();
  writeLock(fx.repoFx.root, { ...fx.lock(), target: '../outside' });

  const error = captureError(() => lockLayout(fx.repoFx.root, readLock(fx.repoFx.root), readManifest(fx.repoFx.root)));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /\.\.\/outside/);
  fx.cleanup();
});

/** D18 for the mirror: a delete is contained in the mirror root, and a layout with none has no mirror to delete. */
test('a lock mirror outside the mirror root, or on a layout with none, is refused and nothing is deleted', () => {
  const fx = layoutFixture('layout-desc-mirror-escape', 'agents');
  fx.sync();
  fx.repoFx.write('src/important.ts', 'export {};\n');
  const lock = fx.lock();
  const crafted = { path: 'src/important.ts', of: '.agents/skills/gone/SKILL.md', sha256: lock.mirrors![0]!.sha256 };
  // The hash of what is there, so the retirement cell would delete it if nothing contained it.
  crafted.sha256 = digestBytes(Buffer.from('export {};\n'));
  writeLock(fx.repoFx.root, { ...lock, mirrors: [...lock.mirrors!, crafted] });

  assert.match(captureError(() => fx.sync()).message, /outside \.claude\/skills/);
  assert.ok(fx.repoFx.exists('src/important.ts'));

  const { harness: _h, target: _t, ...older } = fx.lock();
  writeLock(fx.repoFx.root, older);
  fx.flip('claude-code');
  assert.match(captureError(() => fx.sync()).message, /tampered/);
  assert.ok(fx.repoFx.exists('src/important.ts'));
  fx.cleanup();
});

/**
 * Additive, as D71 was: a lock on the older layout carries no layout field. Every lock names the index it
 * wrote since WSSETUP14a (D128 §2.4), on either layout, which is what makes that file Daoris's to rewrite.
 */
test('a claude-code lock carries no layout fields; an agents lock carries harness, target and mirrors', () => {
  const old = layoutFixture('layout-desc-lock-old');
  old.sync();
  assert.deepEqual(Object.keys(JSON.parse(old.repoFx.read('daoris.lock'))), ['version', 'canonVersion', 'source', 'entries', 'index']);
  assert.equal(old.lock().index, '.claude/INDEX.md');
  old.cleanup();

  const fresh = layoutFixture('layout-desc-lock-new', 'agents');
  fresh.sync();
  const lock = fresh.lock();
  assert.equal(lock.harness, 'agents');
  assert.equal(lock.target, '.agents');
  // Sorted as the entries are, so the lock diffs cleanly in review.
  assert.deepEqual(lock.mirrors!.map((m) => m.path), ['.claude/skills/finder/run.sh', '.claude/skills/finder/SKILL.md']);
  const entry = lock.mirrors!.find((m) => m.path === '.claude/skills/finder/SKILL.md')!;
  assert.equal(entry.of, '.agents/skills/finder/SKILL.md');
  assert.match(entry.sha256, /^[0-9a-f]{64}$/);
  assert.equal(lock.rooms, undefined, 'no rooms declared, so none recorded');
  fresh.cleanup();
});

/** §5.2: a skill without frontmatter installs and never fires, on any of the three agents. */
test('the contract check reads .agents/skills under the agents descriptor', () => {
  const fx = makeFixture('layout-desc-contract');
  fx.write('.agents/skills/broken/SKILL.md', 'Steps, but no frontmatter.\n');
  fx.write('.agents/skills/fine/SKILL.md', skill('fine'));

  const problems = verifyHarnessContract(fx.root, '.agents', HARNESSES['agents']);

  assert.equal(problems.length, 1);
  assert.match(problems[0]!, /broken/);
  fx.cleanup();
});
