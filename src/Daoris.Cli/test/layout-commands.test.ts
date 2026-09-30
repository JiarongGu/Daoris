// check, upstream, doctor, init and status on the agents layout (LAYOUT3; D117 §3.3, §5.2).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { LayoutFixture } from './_layout.ts';
import { captureError } from './_fixture.ts';
import { doc, layoutFixture, skill } from './_layout.ts';
import { readLock, readManifest } from '../src/config.ts';
import { upstreamAll, upstreamFile } from '../src/upstream.ts';
import { DaorisError } from '../src/errors.ts';

const MIRROR = '.claude/skills/finder/SKILL.md';
const SOURCE = '.agents/skills/finder/SKILL.md';

function agentsSynced(tag: string): LayoutFixture {
  const fx = layoutFixture(tag, 'agents');
  fx.repoFx.write('.agents/skills/house/SKILL.md', skill('house'));
  fx.sync();
  return fx;
}

const promote = (fx: LayoutFixture, file: string) => upstreamFile({
  root: fx.repoFx.root,
  manifest: readManifest(fx.repoFx.root),
  lock: readLock(fx.repoFx.root),
  canonRoot: fx.canonFx.root,
  file,
});

// ---------------------------------------------------------------- check

test('check is clean on a freshly synced agents repository', () => {
  const fx = agentsSynced('cmd-check-clean');

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(out, /clean/);
  fx.cleanup();
});

test('check fails on an edited mirror, naming its source', () => {
  const fx = agentsSynced('cmd-check-mirror-drift');
  fx.repoFx.write(MIRROR, `${fx.repoFx.read(MIRROR)}\nAn edit.\n`);

  const { code, out } = fx.cli('check');

  assert.equal(code, 1);
  assert.match(out, /drifted\s+\.claude\/skills\/finder\/SKILL\.md — a mirror of \.agents\/skills\/finder\/SKILL\.md, edited here/);
  fx.cleanup();
});

test('check fails on a mirror behind its source', () => {
  const fx = agentsSynced('cmd-check-mirror-behind');
  fx.repoFx.write('.agents/skills/house/SKILL.md', skill('house', 'Newer steps.'));

  const { code, out } = fx.cli('check');

  assert.equal(code, 1);
  assert.match(out, /behind\s+\.claude\/skills\/house\/SKILL\.md/);
  fx.cleanup();
});

test('check fails on a skill added at the source and never mirrored', () => {
  const fx = agentsSynced('cmd-check-mirror-new');
  fx.repoFx.write('.agents/skills/later/SKILL.md', skill('later'));

  const { code, out } = fx.cli('check');

  assert.equal(code, 1);
  assert.match(out, /\.claude\/skills\/later\/SKILL\.md/);
  fx.cleanup();
});

/** 🔴 Between a manifest's flip and the sync that moves the files, the lock says where they are. */
test('check fails on a flipped manifest until sync moves the files, naming both layouts', () => {
  const fx = layoutFixture('cmd-check-stale-layout');
  fx.sync();
  fx.flip('agents');

  const { code, out } = fx.cli('check');

  assert.equal(code, 1);
  assert.match(out, /agents/);
  assert.match(out, /claude-code/);
  assert.match(out, /sync/);
  assert.doesNotMatch(out, /missing/, 'the files are where the lock says, not missing');
  fx.cleanup();
});

/** codex reads 32,768 bytes of a repository's instruction files and cuts the rest (LAYOUT2). */
test('check reports AGENTS.md over the smallest limit an agent reads, and does not fail on it (D54)', () => {
  const fx = agentsSynced('cmd-check-size');
  fx.repoFx.write('AGENTS.md', `# Ours\n\n${'word '.repeat(7000)}\n\n${fx.repoFx.read('AGENTS.md')}`);

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(out, /AGENTS\.md is \d+ bytes/);
  assert.match(out, /32768/);
  fx.cleanup();
});

test('check reports an own document in an old tier and a Claude-only skill, and does not fail on either', () => {
  const fx = agentsSynced('cmd-check-old-tier');
  fx.repoFx.write('.claude/knowledge/ours.md', doc('ours'));
  fx.repoFx.write('.claude/skills/solo/SKILL.md', skill('solo'));

  const { code, out } = fx.cli('check');

  assert.equal(code, 0, out);
  assert.match(out, /\.claude\/knowledge\/ours\.md/);
  assert.match(out, /git mv \.claude\/knowledge\/ours\.md \.agents\/knowledge\/ours\.md/);
  assert.match(out, /\.claude\/skills\/solo/);
  assert.match(out, /Claude Code alone/);
  fx.cleanup();
});

// ---------------------------------------------------------------- upstream

/** §3.3: a mirror's path is accepted, and its edit goes to the canon with its header stripped. */
test('upstream takes a canonical mirror\'s path, and the next sync is clean', () => {
  const fx = agentsSynced('cmd-up-mirror');
  fx.repoFx.write(MIRROR, fx.repoFx.read(MIRROR).replace('Steps of finder.', 'Better steps, found in the mirror.'));

  const result = promote(fx, MIRROR);

  assert.equal(result.target, 'skills/finder/SKILL.md');
  const canon = fx.canonFx.read('core/skills/finder/SKILL.md');
  assert.match(canon, /Better steps, found in the mirror/);
  assert.doesNotMatch(canon, /<!-- daoris:/);
  assert.ok(canon.startsWith('---\nname: finder\n'));
  // Improved upstream, untouched here: the source takes it, and the mirror already holds it.
  fx.sync();
  assert.match(fx.repoFx.read(SOURCE), /Better steps, found in the mirror/);
  assert.equal(fx.cli('check').code, 0, fx.cli('check').out);
  fx.cleanup();
});

test('upstream refuses a mirror of the repository\'s own skill, naming its source', () => {
  const fx = agentsSynced('cmd-up-mirror-local');

  const error = captureError(() => promote(fx, '.claude/skills/house/SKILL.md'));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /nothing canonical to promote/);
  assert.match(error.message, /\.agents\/skills\/house\/SKILL\.md/);
  fx.cleanup();
});

test('upstream refuses a mirror whose source was edited too, naming both', () => {
  const fx = agentsSynced('cmd-up-mirror-both');
  fx.repoFx.write(MIRROR, `${fx.repoFx.read(MIRROR)}\nIn the mirror.\n`);
  fx.repoFx.write(SOURCE, `${fx.repoFx.read(SOURCE)}\nIn the source.\n`);

  const error = captureError(() => promote(fx, MIRROR));

  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /\.claude\/skills\/finder\/SKILL\.md/);
  assert.match(error.message, /\.agents\/skills\/finder\/SKILL\.md/);
  assert.doesNotMatch(fx.canonFx.read('core/skills/finder/SKILL.md'), /In the/);
  fx.cleanup();
});

test('upstream takes a source\'s path under .agents', () => {
  const fx = agentsSynced('cmd-up-source');
  fx.repoFx.write('.agents/knowledge/storage.md', fx.repoFx.read('.agents/knowledge/storage.md').replace('Body of', 'Better body of'));

  assert.equal(promote(fx, '.agents/knowledge/storage.md').target, 'knowledge/storage.md');
  assert.match(fx.canonFx.read('core/knowledge/storage.md'), /Better body of storage/);
  fx.cleanup();
});

test('upstream answers an old path after a move with where the document went', () => {
  const fx = layoutFixture('cmd-up-old-path');
  fx.sync();
  fx.flip('agents');
  fx.sync();

  const error = captureError(() => promote(fx, '.claude/knowledge/storage.md'));

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /\.agents\/knowledge\/storage\.md/);
  assert.match(error.message, /moved/);
  fx.cleanup();
});

test('upstream --all promotes an edited canonical mirror with the other drifted files', () => {
  const fx = agentsSynced('cmd-up-all');
  fx.repoFx.write(MIRROR, fx.repoFx.read(MIRROR).replace('Steps of finder.', 'All at once.'));

  const promoted = upstreamAll({
    root: fx.repoFx.root, manifest: readManifest(fx.repoFx.root), lock: readLock(fx.repoFx.root), canonRoot: fx.canonFx.root,
  });

  assert.deepEqual(promoted.map((p) => p.target), ['skills/finder/SKILL.md']);
  assert.match(fx.canonFx.read('core/skills/finder/SKILL.md'), /All at once/);
  fx.cleanup();
});

// ---------------------------------------------------------------- doctor

/** A mirror would otherwise score as a perfect twin of its source. */
test('doctor never reports a mirror', () => {
  const fx = layoutFixture('cmd-doctor', 'agents');
  fx.sync();
  assert.ok(fx.repoFx.exists(MIRROR));

  const { code, out } = fx.cli('doctor');

  assert.equal(code, 0);
  assert.doesNotMatch(out, /\.claude\/skills/);
  assert.match(out, /no suspected duplicates/);
  fx.cleanup();
});

// ---------------------------------------------------------------- init

test('init --harness agents writes the layout\'s fields and names each own document the index would stop listing', () => {
  const fx = layoutFixture('cmd-init-agents');
  rmSync(join(fx.repoFx.root, 'daoris.json'));
  fx.repoFx.write('.claude/knowledge/ours.md', doc('ours'));
  fx.repoFx.write('.claude/skills/house/SKILL.md', skill('house'));
  fx.repoFx.write('.agents/skills/kept/SKILL.md', skill('kept'));

  const { code, out } = fx.cli('init', '--harness', 'agents');

  assert.equal(code, 0, out);
  const manifest = JSON.parse(fx.repoFx.read('daoris.json'));
  assert.equal(manifest.harness, 'agents');
  assert.equal(manifest.target, '.agents');
  assert.deepEqual(manifest.rooms, []);
  assert.match(out, /skills\/kept\/SKILL\.md/);
  assert.match(out, /git mv \.claude\/knowledge\/ours\.md \.agents\/knowledge\/ours\.md/);
  assert.match(out, /git mv \.claude\/skills\/house \.agents\/skills\/house/);
  fx.cleanup();
});

test('init reports a CLAUDE.md that is a link held as text', () => {
  const fx = layoutFixture('cmd-init-link');
  rmSync(join(fx.repoFx.root, 'daoris.json'));
  fx.repoFx.write('AGENTS.md', '# Ours\n');
  fx.repoFx.write('CLAUDE.md', 'AGENTS.md');

  const { code, out } = fx.cli('init', '--harness', 'agents');

  assert.equal(code, 0, out);
  assert.match(out, /CLAUDE\.md/);
  assert.match(out, /looks like a link checked out as text/);
  fx.cleanup();
});

test('init keeps the older layout unless the agents layout is chosen, and refuses a harness it does not know', () => {
  const fx = layoutFixture('cmd-init-default');
  rmSync(join(fx.repoFx.root, 'daoris.json'));

  assert.equal(fx.cli('init').code, 0);
  const manifest = JSON.parse(fx.repoFx.read('daoris.json'));
  assert.equal(manifest.harness, undefined);
  assert.equal(manifest.target, '.claude');

  rmSync(join(fx.repoFx.root, 'daoris.json'));
  const unknown = fx.cli('init', '--harness', 'nonesuch');
  assert.equal(unknown.code, 2);
  assert.match(unknown.out, /nonesuch/);
  assert.equal(fx.repoFx.exists('daoris.json'), false);
  fx.cleanup();
});

// ---------------------------------------------------------------- status

test('status names the layout, its rooms and its mirrors', () => {
  const fx = agentsSynced('cmd-status');

  const json = JSON.parse(fx.cli('status', '--json').out);

  assert.equal(json.layout, 'agents');
  assert.equal(json.target, '.agents');
  assert.deepEqual(json.rooms, []);
  assert.equal(json.mirrors, 3);
  assert.equal(fx.cli('status').code, 0);
  assert.match(fx.cli('status').out, /mirrors\s+3 in \.claude\/skills/);
  fx.cleanup();
});
