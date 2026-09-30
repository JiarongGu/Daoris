// The skills mirror (LAYOUT3; D117 §3.2, §3.3 and §5.4's mirror table).
//
// Where the reference links `.claude/skills` to `.agents/skills`, `sync` writes a copy of every skill
// — canonical and the repository's own — for the agent that reads only there. The lock records each
// mirror and it is measured against the lock (D13), so each cell below is a lock × disk × source
// question, one test per cell, written before the code.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { LayoutFixture } from './_layout.ts';
import { captureError } from './_fixture.ts';
import { layoutFixture, skill } from './_layout.ts';
import { writeLock } from '../src/config.ts';
import { DaorisError } from '../src/errors.ts';

const MIRROR = '.claude/skills/finder/SKILL.md';
const SOURCE = '.agents/skills/finder/SKILL.md';
const HOUSE = '.agents/skills/house/SKILL.md';
const HOUSE_MIRROR = '.claude/skills/house/SKILL.md';

/** An agents repository with the canon's `finder` and a skill of its own, `house`. */
function agentsRepo(tag: string): LayoutFixture {
  const fx = layoutFixture(tag, 'agents');
  fx.repoFx.write(HOUSE, skill('house'));
  fx.repoFx.write('.agents/skills/house/references/notes.md', '# notes\n');
  // Per-agent metadata another agent writes: it belongs to that agent, not to the one the mirror serves.
  fx.repoFx.write('.agents/skills/house/agents/openai.yaml', 'interface: {}\n');
  return fx;
}

const mirrorState = (fx: LayoutFixture, path: string) =>
  fx.plan().mirrors!.writes.find((write) => write.path === path)?.state;

test('mirror cell: not in the lock, absent, source present — created, with its header under the frontmatter', () => {
  const fx = agentsRepo('mirror-create');

  fx.sync();

  const mirror = fx.repoFx.read(MIRROR);
  // Frontmatter at byte 0, or the harness never surfaces the skill (D14).
  assert.ok(mirror.startsWith('---\nname: finder\n'), mirror.slice(0, 80));
  assert.match(mirror, /---\n<!-- daoris: mirror of \.agents\/skills\/finder\/SKILL\.md for agents that read only \.claude\/skills/);
  assert.match(mirror, /daoris upstream/, 'a canonical skill names the way to promote an edit');
  assert.doesNotMatch(mirror, /<!-- daoris: core\//, 'one header, not the source\'s provenance line as well');
  assert.match(mirror, /Steps of finder/);
  // Any other file is a copy, byte for byte: a script's first line may be a shebang.
  assert.equal(fx.repoFx.read('.claude/skills/finder/run.sh'), fx.repoFx.read('.agents/skills/finder/run.sh'));
  // The repository's own skill is mirrored too — Claude Code needs it as much as the canon's.
  const house = fx.repoFx.read(HOUSE_MIRROR);
  assert.match(house, /mirror of \.agents\/skills\/house\/SKILL\.md/);
  assert.doesNotMatch(house, /upstream/, 'nothing canonical to promote from the repository\'s own skill');
  assert.equal(fx.repoFx.read('.claude/skills/house/references/notes.md'), '# notes\n');
  assert.equal(fx.repoFx.exists('.claude/skills/house/agents'), false);
  fx.cleanup();
});

test('mirror cell: not in the lock, the same as would be written — adopted silently', () => {
  const fx = agentsRepo('mirror-adopt');
  fx.sync();
  const { mirrors: _dropped, ...rest } = fx.lock();
  writeLock(fx.repoFx.root, rest);

  assert.equal(mirrorState(fx, MIRROR), 'unchanged');
  const lock = fx.sync();

  assert.ok(lock.mirrors!.some((m) => m.path === MIRROR));
  fx.cleanup();
});

test('mirror cell: not in the lock, differs — a collision, refused; --force accepts the mirror', () => {
  const fx = agentsRepo('mirror-collide');
  // A skill the repository keeps for Claude Code alone, under the name of one it also keeps for all.
  fx.repoFx.write(MIRROR, skill('finder', 'Claude-only steps.'));

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /\.claude\/skills\/finder\/SKILL\.md/);
  assert.match(fx.repoFx.read(MIRROR), /Claude-only steps/);

  fx.sync(true);
  assert.match(fx.repoFx.read(MIRROR), /mirror of/);
  fx.cleanup();
});

test('mirror cell: in the lock, as the lock, source changed — renewed', () => {
  const fx = agentsRepo('mirror-renew');
  fx.sync();
  fx.repoFx.write(HOUSE, skill('house', 'Better steps.'));
  fx.canonFx.write('core/skills/finder/SKILL.md', skill('finder', 'Improved steps of finder.'));

  assert.equal(mirrorState(fx, HOUSE_MIRROR), 'update');
  fx.sync();

  assert.match(fx.repoFx.read(HOUSE_MIRROR), /Better steps/);
  assert.match(fx.repoFx.read(MIRROR), /Improved steps of finder/);
  fx.cleanup();
});

test('mirror cell: in the lock, as the lock, source unchanged — unchanged', () => {
  const fx = agentsRepo('mirror-unchanged');
  fx.sync();

  assert.ok(fx.plan().mirrors!.writes.every((write) => write.state === 'unchanged'));
  fx.cleanup();
});

test('mirror cell: in the lock, differs from the lock but the same as would be written — only the lock moves', () => {
  const fx = agentsRepo('mirror-lock-only');
  fx.sync();
  // The edit already reached the source: source and mirror both carry it.
  fx.repoFx.write(HOUSE, skill('house', 'Edited in both.'));
  const wanted = fx.plan().mirrors!.writes.find((write) => write.path === HOUSE_MIRROR)!;
  fx.repoFx.write(HOUSE_MIRROR, String(wanted.content));

  const lock = fx.sync();

  assert.equal(lock.mirrors!.find((m) => m.path === HOUSE_MIRROR)!.sha256, wanted.sha256);
  assert.equal(fx.cli('check').code, 0);
  fx.cleanup();
});

test('mirror cell: in the lock, differs from both, source unchanged — the mirror was edited: refused, naming its source', () => {
  const fx = agentsRepo('mirror-edited');
  fx.sync();
  fx.repoFx.write(MIRROR, fx.repoFx.read(MIRROR).replace('Steps of finder.', 'Edited in the mirror.'));

  const dry = fx.cli('sync', '--dry-run');
  assert.equal(dry.code, 1);
  assert.match(dry.out, /DRIFTED\s+\.claude\/skills\/finder\/SKILL\.md — a mirror of \.agents\/skills\/finder\/SKILL\.md, edited here/);

  const error = captureError(() => fx.sync());
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /\.claude\/skills\/finder\/SKILL\.md/);
  assert.match(error.message, /\.agents\/skills\/finder\/SKILL\.md/);
  assert.match(error.message, /daoris upstream \.claude\/skills\/finder\/SKILL\.md/);
  assert.match(error.message, /--force/);
  assert.match(fx.repoFx.read(MIRROR), /Edited in the mirror/);

  fx.sync(true);
  assert.doesNotMatch(fx.repoFx.read(MIRROR), /Edited in the mirror/);
  fx.cleanup();
});

test('mirror cell: an edited mirror of the repository\'s own skill says to move the edit into the source', () => {
  const fx = agentsRepo('mirror-edited-local');
  fx.sync();
  fx.repoFx.write(HOUSE_MIRROR, fx.repoFx.read(HOUSE_MIRROR).replace('Steps of house.', 'Edited in the mirror.'));

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /\.agents\/skills\/house\/SKILL\.md/);
  assert.doesNotMatch(error.message, /daoris upstream \.claude\/skills\/house/);
  fx.cleanup();
});

test('mirror cell: in the lock, differs from both, and its canonical source drifted too — refused, naming both', () => {
  const fx = agentsRepo('mirror-both');
  fx.sync();
  fx.repoFx.write(MIRROR, fx.repoFx.read(MIRROR).replace('Steps of finder.', 'Edited in the mirror.'));
  fx.repoFx.write(SOURCE, fx.repoFx.read(SOURCE).replace('Steps of finder.', 'Edited in the source.'));

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /\.claude\/skills\/finder\/SKILL\.md/);
  assert.match(error.message, /\.agents\/skills\/finder\/SKILL\.md/);
  assert.match(error.message, /both/);
  fx.cleanup();
});

test('mirror cell: in the lock, absent, source present — recreated, and check reports it missing first', () => {
  const fx = agentsRepo('mirror-recreate');
  fx.sync();
  rmSync(join(fx.repoFx.root, MIRROR));

  const check = fx.cli('check');
  assert.equal(check.code, 1);
  assert.match(check.out, /missing\s+\.claude\/skills\/finder\/SKILL\.md/);

  fx.sync();
  assert.match(fx.repoFx.read(MIRROR), /mirror of/);
  fx.cleanup();
});

test('mirror cell: in the lock, as the lock, source gone — the mirror retires', () => {
  const fx = agentsRepo('mirror-retire');
  fx.sync();
  rmSync(join(fx.repoFx.root, '.agents/skills/house'), { recursive: true });

  const lock = fx.sync();

  assert.equal(fx.repoFx.exists(HOUSE_MIRROR), false);
  assert.equal(fx.repoFx.exists('.claude/skills/house'), false, 'the folder the retirement empties goes too');
  assert.equal(lock.mirrors!.some((m) => m.path.startsWith('.claude/skills/house/')), false);
  fx.cleanup();
});

test('mirror cell: in the lock, differs, source gone — an edited mirror of a skill that went, refused', () => {
  const fx = agentsRepo('mirror-edited-gone');
  fx.sync();
  fx.repoFx.write(HOUSE_MIRROR, `${fx.repoFx.read(HOUSE_MIRROR)}\nA last edit.\n`);
  rmSync(join(fx.repoFx.root, '.agents/skills/house'), { recursive: true });

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /\.claude\/skills\/house\/SKILL\.md/);
  assert.match(fx.repoFx.read(HOUSE_MIRROR), /A last edit/);
  fx.cleanup();
});

test('mirror cell: in the lock, absent, source gone — the entry is dropped', () => {
  const fx = agentsRepo('mirror-drop');
  fx.sync();
  rmSync(join(fx.repoFx.root, '.agents/skills/house'), { recursive: true });
  rmSync(join(fx.repoFx.root, '.claude/skills/house'), { recursive: true });

  const lock = fx.sync();

  assert.equal(lock.mirrors!.some((m) => m.path.startsWith('.claude/skills/house/')), false);
  fx.cleanup();
});

/** §5.3: the roster points at the source, and says once where the copy is and which to edit. */
test('the region lists skills at .agents and says the mirror sentence once', () => {
  const fx = agentsRepo('mirror-roster');
  fx.sync();

  const region = fx.repoFx.region()!;

  assert.match(region, /\[finder\]\(\.agents\/skills\/finder\)/);
  assert.match(region, /\[storage\]\(\.agents\/knowledge\/storage\.md\)/);
  assert.match(region, /Skills live in `\.agents\/skills\/`; `\.claude\/skills\/` mirrors them for the agent that reads only there — edit the source\./);
  assert.equal(region.match(/mirrors them/g)!.length, 1);
  fx.cleanup();
});
