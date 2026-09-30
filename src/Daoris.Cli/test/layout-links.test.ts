// Links, and links held as text (LAYOUT3; D117 §3 and §5.4's last table).
//
// D3 stands: Daoris writes real files. A link at a path it would write is refused and never written
// through, and so is the text a checkout without links (`core.symlinks=false`) leaves in its place.
// The refusal names both answers — a real file holding `@AGENTS.md`, or a folder of mirrors — and says
// the choice is the repository's, because on the other side of the guess is how the repository is
// laid out for its other contributors.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, symlinkSync, unlinkSync } from 'node:fs';
import { join } from 'node:path';
import type { TestContext } from 'node:test';
import { captureError } from './_fixture.ts';
import { layoutFixture, skill } from './_layout.ts';
import { DaorisError } from '../src/errors.ts';

const TEXT = /looks like a link checked out as text/;

test('link cell: CLAUDE.md held as text — refused, saying what was seen and both answers, even with --force', () => {
  const fx = layoutFixture('links-claude-text', 'agents');
  // The reference's `CLAUDE.md -> AGENTS.md`, checked out on a machine without links: 9 bytes.
  fx.repoFx.write('AGENTS.md', '# Ours\n');
  fx.repoFx.write('CLAUDE.md', 'AGENTS.md');

  const dry = fx.cli('sync', '--dry-run');
  assert.equal(dry.code, 1);
  assert.match(dry.out, /LINK\s+CLAUDE\.md/);

  const error = captureError(() => fx.sync());
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /CLAUDE\.md/);
  assert.match(error.message, TEXT);
  assert.match(error.message, /core\.symlinks=false/);
  assert.match(error.message, /@AGENTS\.md/);
  assert.match(error.message, /the choice is this repository's/);
  // Never written through: on a checkout without links, git would read an edit as the link's new target.
  assert.ok(captureError(() => fx.sync(true)) instanceof DaorisError);
  assert.equal(fx.repoFx.read('CLAUDE.md'), 'AGENTS.md');
  assert.equal(fx.repoFx.read('AGENTS.md'), '# Ours\n');
  fx.cleanup();
});

test('link cell: .claude/skills held as text where the mirror folder must go — refused', () => {
  const fx = layoutFixture('links-skills-text', 'agents');
  fx.repoFx.write('.claude/skills', '../.agents/skills');

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /\.claude\/skills/);
  assert.match(error.message, TEXT);
  assert.match(error.message, /a folder of mirrors/);
  fx.cleanup();
});

test('link cell: a room\'s CLAUDE.md held as text — refused', () => {
  const fx = layoutFixture('links-room-text', 'agents');
  fx.manifest({ harness: 'agents', target: '.agents', rooms: ['pkg'] });
  fx.repoFx.write('pkg/AGENTS.md', '# A package\n');
  fx.repoFx.write('pkg/CLAUDE.md', 'AGENTS.md\n');

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /pkg\/CLAUDE\.md/);
  assert.equal(fx.repoFx.read('pkg/CLAUDE.md'), 'AGENTS.md\n');
  fx.cleanup();
});

/** A real link on a machine that makes them. A junction needs no privilege on Windows. */
test('link cell: a real link (a junction) at the mirror root — refused, never written through', () => {
  const fx = layoutFixture('links-junction', 'agents');
  fx.repoFx.write('.agents/skills/house/SKILL.md', skill('house'));
  mkdirSync(join(fx.repoFx.root, '.claude'), { recursive: true });
  const link = join(fx.repoFx.root, '.claude', 'skills');
  symlinkSync(join(fx.repoFx.root, '.agents', 'skills'), link, 'junction');

  const error = captureError(() => fx.sync());

  unlinkSync(link);
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /\.claude\/skills is a link/);
  assert.equal(fx.repoFx.exists('.agents/skills/finder/SKILL.md'), false, 'refused before anything was written');
  fx.cleanup();
});

test('link cell: a real file link at CLAUDE.md — refused', (t: TestContext) => {
  const fx = layoutFixture('links-file', 'agents');
  fx.repoFx.write('AGENTS.md', '# Ours\n');
  try {
    symlinkSync('AGENTS.md', join(fx.repoFx.root, 'CLAUDE.md'), 'file');
  } catch (error) {
    // A file link needs a privilege Windows does not grant by default; the junction above holds the cell.
    fx.cleanup();
    t.skip(`this machine makes no file links: ${(error as NodeJS.ErrnoException).code}`);
    return;
  }

  const error = captureError(() => fx.sync());

  unlinkSync(join(fx.repoFx.root, 'CLAUDE.md'));
  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /CLAUDE\.md is a link/);
  fx.cleanup();
});

test('a link held as text is refused on the older layout too, since it writes the same pointer', () => {
  const fx = layoutFixture('links-claude-code');
  fx.repoFx.write('CLAUDE.md', 'AGENTS.md');

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, TEXT);
  fx.cleanup();
});

test('check fails on a path daoris writes that has become a link held as text', () => {
  const fx = layoutFixture('links-check', 'agents');
  fx.sync();
  fx.repoFx.write('CLAUDE.md', 'AGENTS.md');

  const { code, out } = fx.cli('check');

  assert.equal(code, 1);
  assert.match(out, /LINK\s+CLAUDE\.md/);
  fx.cleanup();
});

test('a short CLAUDE.md that is prose, or an import, is not mistaken for a link', () => {
  const fx = layoutFixture('links-prose', 'agents');
  fx.repoFx.write('CLAUDE.md', '@AGENTS.md\n');
  fx.repoFx.write('AGENTS.md', 'Hello.\n');

  fx.sync();

  assert.equal(fx.repoFx.read('CLAUDE.md'), '@AGENTS.md\n');
  fx.cleanup();
});
