// D19, for a move (LAYOUT3; D117 §5.4, the first two tables).
//
// A MOVE is a repository whose manifest names a root other than the one its lock was written under.
// D19's table still applies to every document at its new root; these are the cells the move adds,
// one test per cell, each written before the code and watched failing.
//
// Every test starts from a repository synced on `claude-code` — the lock holds `knowledge/storage.md`
// and the `finder` skill under `.claude/` — and flips its manifest to `agents`.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { LayoutFixture } from './_layout.ts';
import { captureError } from './_fixture.ts';
import { doc, layoutFixture, skill } from './_layout.ts';
import { renderCanonFile } from '../src/document.ts';
import { DaorisError } from '../src/errors.ts';

/** Synced on the older layout, then flipped: the state every cell below starts from. */
function adoptedThenFlipped(tag: string): LayoutFixture {
  const fx = layoutFixture(tag);
  fx.sync();
  fx.flip('agents');
  return fx;
}

const OLD = '.claude/knowledge/storage.md';
const NEW = '.agents/knowledge/storage.md';

// ---------------------------------------------------------------- a canonical document, old root

test('move cell: selected, old untouched, new absent — written at the new root, the old deleted, the lock re-rooted', () => {
  const fx = adoptedThenFlipped('move-plain');
  const before = fx.repoFx.read(OLD);

  const lock = fx.sync();

  assert.equal(fx.repoFx.read(NEW), before);
  assert.equal(fx.repoFx.exists(OLD), false);
  // A folder the move empties is removed — and only when empty.
  assert.equal(fx.repoFx.exists('.claude/knowledge'), false);
  assert.equal(lock.harness, 'agents');
  assert.equal(lock.target, '.agents');
  assert.ok(lock.entries.some((entry) => entry.target === 'knowledge/storage.md'));
  // The skill moved too, and its old path now holds the mirror Claude Code reads.
  assert.ok(fx.repoFx.exists('.agents/skills/finder/SKILL.md'));
  assert.match(fx.repoFx.read('.claude/skills/finder/SKILL.md'), /mirror of \.agents\/skills\/finder\/SKILL\.md/);
  assert.equal(fx.cli('check').code, 0, fx.cli('check').out);
  fx.cleanup();
});

test('a move is printed by --dry-run as moved lines, and nothing is written', () => {
  const fx = adoptedThenFlipped('move-dry');

  const { code, out } = fx.cli('sync', '--dry-run');

  assert.equal(code, 0, out);
  assert.match(out, /moved\s+\.claude\/knowledge\/storage\.md -> \.agents\/knowledge\/storage\.md/);
  assert.match(out, /mirror\s+\.claude\/skills\/finder\/SKILL\.md/);
  assert.equal(fx.repoFx.exists(NEW), false);
  assert.ok(fx.repoFx.exists(OLD));
  fx.cleanup();
});

test('move cell: selected, old untouched, new the same as the lock — the move is finished: the old deleted', () => {
  const fx = adoptedThenFlipped('move-finish-lock');
  // A move interrupted, or made by hand with a copy.
  fx.repoFx.write(NEW, fx.repoFx.read(OLD));

  const plan = fx.plan();
  assert.equal(plan.writes.find((w) => w.target === 'knowledge/storage.md')!.state, 'unchanged');
  fx.sync();

  assert.equal(fx.repoFx.exists(OLD), false);
  assert.ok(fx.repoFx.exists(NEW));
  fx.cleanup();
});

test('move cell: selected, old untouched, new as the canon renders now — the move is finished', () => {
  const fx = adoptedThenFlipped('move-finish-canon');
  fx.canonFx.write('core/knowledge/storage.md', doc('storage', 'IMPROVED body of storage.'));
  const rendered = renderCanonFile(
    { pack: 'core', source: 'core/knowledge/storage.md', target: 'knowledge/storage.md' },
    doc('storage', 'IMPROVED body of storage.'),
    '0.1.0',
  );
  fx.repoFx.write(NEW, rendered);

  fx.sync();

  assert.equal(fx.repoFx.exists(OLD), false);
  assert.equal(fx.repoFx.read(NEW), rendered);
  fx.cleanup();
});

test('move cell: selected, old untouched, new differs — a collision at the new root, refused naming both paths', () => {
  const fx = adoptedThenFlipped('move-collide-new');
  fx.repoFx.write(NEW, '# Our own storage notes\n');

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /\.agents\/knowledge\/storage\.md/);
  assert.match(error.message, /\.claude\/knowledge\/storage\.md/);
  // Refused before anything was written: both files as they were, and no lock moved.
  assert.equal(fx.repoFx.read(NEW), '# Our own storage notes\n');
  assert.ok(fx.repoFx.exists(OLD));
  assert.equal(fx.lock().target, undefined);

  // --force accepts the canonical version, as a D12 collision does.
  fx.sync(true);
  assert.match(fx.repoFx.read(NEW), /Body of storage/);
  assert.equal(fx.repoFx.exists(OLD), false);
  fx.cleanup();
});

test('move cell: selected, old edited but its body is the canon\'s now — read as untouched, and moved', () => {
  const fx = adoptedThenFlipped('move-promoted');
  // The state right after `upstream`: the edit is canonical already, and only the lock is stale.
  fx.repoFx.write(OLD, fx.repoFx.read(OLD).replace('Body of storage.', 'Promoted body of storage.'));
  fx.canonFx.write('core/knowledge/storage.md', doc('storage', 'Promoted body of storage.'));

  fx.sync();

  assert.match(fx.repoFx.read(NEW), /Promoted body of storage/);
  assert.equal(fx.repoFx.exists(OLD), false);
  fx.cleanup();
});

test('move cell: selected, old edited and not the canon — drift, refused; upstream or --force', () => {
  const fx = adoptedThenFlipped('move-drift');
  fx.repoFx.write(OLD, fx.repoFx.read(OLD).replace('Body of storage.', 'A local edit.'));

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /knowledge\/storage\.md/);
  assert.match(error.message, /upstream/);
  assert.match(fx.repoFx.read(OLD), /A local edit/);
  assert.equal(fx.repoFx.exists(NEW), false);
  fx.cleanup();
});

test('move cell: selected, old absent, new absent — created at the new root, the old path dropped', () => {
  const fx = adoptedThenFlipped('move-recreate');
  rmSync(join(fx.repoFx.root, OLD));

  fx.sync();

  assert.match(fx.repoFx.read(NEW), /Body of storage/);
  assert.equal(fx.repoFx.exists(OLD), false);
  fx.cleanup();
});

test('move cell: selected, old absent, new the same — adopted at the new root', () => {
  const fx = adoptedThenFlipped('move-by-hand');
  // A `git mv` made by hand before the sync.
  fx.repoFx.write(NEW, fx.repoFx.read(OLD));
  rmSync(join(fx.repoFx.root, OLD));

  const plan = fx.plan();
  assert.equal(plan.writes.find((w) => w.target === 'knowledge/storage.md')!.state, 'unchanged');
  fx.sync();

  assert.match(fx.repoFx.read(NEW), /Body of storage/);
  fx.cleanup();
});

test('move cell: selected, old absent, new differs — a collision at the new root, refused', () => {
  const fx = adoptedThenFlipped('move-collide-only');
  rmSync(join(fx.repoFx.root, OLD));
  fx.repoFx.write(NEW, '# Ours\n');

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /\.agents\/knowledge\/storage\.md/);
  assert.equal(fx.repoFx.read(NEW), '# Ours\n');
  fx.cleanup();
});

test('move cell: retired, old untouched — the old is retired, nothing is written at the new root', () => {
  const fx = adoptedThenFlipped('move-retire');
  rmSync(join(fx.canonFx.root, 'core/knowledge/storage.md'));

  const lock = fx.sync();

  assert.equal(fx.repoFx.exists(OLD), false);
  assert.equal(fx.repoFx.exists(NEW), false);
  assert.equal(lock.entries.some((entry) => entry.target === 'knowledge/storage.md'), false);
  fx.cleanup();
});

test('move cell: retired, old edited — an edited retirement, refused', () => {
  const fx = adoptedThenFlipped('move-retire-edited');
  rmSync(join(fx.canonFx.root, 'core/knowledge/storage.md'));
  fx.repoFx.write(OLD, `${fx.repoFx.read(OLD)}\nA local edit.\n`);

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /retired upstream, but edited here/);
  assert.match(fx.repoFx.read(OLD), /A local edit/);
  fx.cleanup();
});

test('move cell: retired, old absent — the entry is dropped', () => {
  const fx = adoptedThenFlipped('move-retire-gone');
  rmSync(join(fx.canonFx.root, 'core/knowledge/storage.md'));
  rmSync(join(fx.repoFx.root, OLD));

  const lock = fx.sync();

  assert.equal(lock.entries.some((entry) => entry.target === 'knowledge/storage.md'), false);
  assert.equal(fx.repoFx.exists(NEW), false);
  fx.cleanup();
});

test('move cell: a confirmed switch turns it off — D71\'s rows at the old root, nothing at the new', () => {
  const fx = adoptedThenFlipped('move-switch');
  fx.canonFx.write('packs/lean/pack.json', JSON.stringify({
    name: 'lean', description: 'Lean', switchesOff: { 'knowledge/storage.md': 'its own storage note replaces it' },
  }));
  fx.canonFx.write('packs/lean/knowledge/lean-storage.md', doc('lean-storage'));
  fx.manifest({
    harness: 'agents', target: '.agents', packs: ['lean'], switchedOff: { 'knowledge/storage.md': 'lean' },
  });

  const lock = fx.sync();

  assert.equal(fx.repoFx.exists(OLD), false);
  assert.equal(fx.repoFx.exists(NEW), false);
  assert.deepEqual(lock.switchedOff, [{ target: 'knowledge/storage.md', by: 'lean' }]);
  assert.ok(fx.repoFx.exists('.agents/knowledge/lean-storage.md'));
  fx.cleanup();
});

// ---------------------------------------------------------------- the repository's own documents

/**
 * 🔴 Left in the old tier, the index stops listing it and nothing else says so — the silent failure
 * D23 names. Moving it is the repository's own act (D5), and the command is one line.
 */
test('own documents in an old on-demand tier refuse the move, each named with the git mv that moves it', () => {
  const fx = layoutFixture('move-left-behind');
  fx.repoFx.write('.claude/knowledge/ours.md', doc('ours'));
  fx.repoFx.write('.claude/skills/house/SKILL.md', skill('house'));
  fx.repoFx.write('.claude/skills/house/notes.md', '# notes\n');
  fx.sync();
  fx.flip('agents');

  const dry = fx.cli('sync', '--dry-run');
  assert.equal(dry.code, 1, dry.out);
  assert.match(dry.out, /LEFT BEHIND\s+\.claude\/knowledge\/ours\.md/);
  assert.match(dry.out, /git mv \.claude\/skills\/house \.agents\/skills\/house/);

  const error = captureError(() => fx.sync());
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /git mv \.claude\/knowledge\/ours\.md \.agents\/knowledge\/ours\.md/);
  assert.match(error.message, /git mv \.claude\/skills\/house \.agents\/skills\/house/);
  // Not even --force leaves them behind: the answer is a move, not a discard.
  assert.ok(captureError(() => fx.sync(true)) instanceof DaorisError);
  // Refused before anything was written.
  assert.ok(fx.repoFx.exists(OLD));
  assert.equal(fx.repoFx.exists(NEW), false);

  // Once the repository moves them, the move goes through, and both are listed from the new root.
  fx.repoFx.write('.agents/knowledge/ours.md', fx.repoFx.read('.claude/knowledge/ours.md'));
  fx.repoFx.write('.agents/skills/house/SKILL.md', fx.repoFx.read('.claude/skills/house/SKILL.md'));
  fx.repoFx.write('.agents/skills/house/notes.md', '# notes\n');
  rmSync(join(fx.repoFx.root, '.claude/knowledge/ours.md'));
  rmSync(join(fx.repoFx.root, '.claude/skills/house'), { recursive: true });
  fx.sync();
  assert.match(fx.repoFx.region()!, /\[ours\]\(\.agents\/knowledge\/ours\.md\) _\(local\)_/);
  assert.match(fx.repoFx.region()!, /\[house\]\(\.agents\/skills\/house\) _\(local\)_/);
  fx.cleanup();
});

test('the same own document under both roots refuses the move: which copy is meant is the repository\'s call', () => {
  const fx = layoutFixture('move-both-roots');
  fx.repoFx.write('.claude/knowledge/ours.md', doc('ours'));
  fx.sync();
  fx.flip('agents');
  fx.repoFx.write('.agents/knowledge/ours.md', doc('ours', 'Another copy.'));

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /\.claude\/knowledge\/ours\.md/);
  assert.match(error.message, /\.agents\/knowledge\/ours\.md/);
  assert.match(error.message, /both/);
  fx.cleanup();
});

test('an old rules/ file is reported, not refused, with where its text could go', () => {
  const fx = layoutFixture('move-old-rules');
  fx.repoFx.write('.claude/rules/house-style.md', doc('house-style'));
  fx.sync();
  fx.flip('agents');

  const { code, out } = fx.cli('sync');

  assert.equal(code, 0, out);
  assert.match(out, /\.claude\/rules\/house-style\.md/);
  assert.match(out, /Claude Code alone/);
  assert.match(out, /AGENTS\.md/);
  assert.equal(fx.repoFx.read('.claude/rules/house-style.md'), doc('house-style'));
  fx.cleanup();
});

test('anything else under the old root — settings, hooks, worktrees — is invisible to the move', () => {
  const fx = layoutFixture('move-invisible');
  fx.repoFx.write('.claude/settings.json', '{"permissions":{}}\n');
  fx.repoFx.write('.claude/hooks/pre.sh', '#!/bin/sh\n');
  fx.repoFx.write('.claude/worktrees/x/notes.md', '# a worktree\n');
  fx.sync();
  fx.flip('agents');

  fx.sync();

  assert.equal(fx.repoFx.read('.claude/settings.json'), '{"permissions":{}}\n');
  assert.equal(fx.repoFx.read('.claude/hooks/pre.sh'), '#!/bin/sh\n');
  assert.equal(fx.repoFx.read('.claude/worktrees/x/notes.md'), '# a worktree\n');
  fx.cleanup();
});

/** The reverse, by the same cells: the mirror retires, and the skill lands where Claude Code reads it. */
test('flipping back to claude-code moves the tiers home and retires every mirror', () => {
  const fx = layoutFixture('move-back', 'agents');
  fx.sync();
  fx.flip('claude-code');

  const lock = fx.sync();

  assert.equal(lock.harness, undefined);
  assert.equal(lock.target, undefined);
  assert.equal(lock.mirrors, undefined);
  assert.match(fx.repoFx.read('.claude/skills/finder/SKILL.md'), /<!-- daoris: core\/core\/skills\/finder\/SKILL\.md @/);
  assert.doesNotMatch(fx.repoFx.read('.claude/skills/finder/SKILL.md'), /mirror of/);
  assert.ok(fx.repoFx.exists('.claude/knowledge/storage.md'));
  assert.equal(fx.repoFx.exists('.agents'), false);
  assert.equal(fx.cli('check').code, 0, fx.cli('check').out);
  fx.cleanup();
});
