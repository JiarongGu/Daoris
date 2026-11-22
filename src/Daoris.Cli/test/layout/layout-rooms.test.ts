// Rooms: nested `AGENTS.md` files the manifest declares.
//
// A room's text is the repository's own and Daoris never writes it. What `sync` keeps of a room is a row in the
// roster. Every agent in use reads a room's `AGENTS.md` itself, so a room has no pointer: where an earlier sync
// wrote one (`<room>/CLAUDE.md` holding the import region), this one takes it out. One test per cell.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { LayoutFixture } from '../_layout.ts';
import { captureError } from '../_fixture.ts';
import { layoutFixture } from '../_layout.ts';
import { CLOSE, IMPORT, OPEN } from '../../src/doctrine/region.ts';
import { DaorisError } from '../../src/platform/errors.ts';

const ROOM = 'src/cli';
const ROOM_AGENTS = 'src/cli/AGENTS.md';
const ROOM_CLAUDE = 'src/cli/CLAUDE.md';
const POINTER = `${OPEN(IMPORT)}\n@AGENTS.md\n${CLOSE(IMPORT)}\n`;

function withRooms(tag: string, rooms: string[]): LayoutFixture {
  const fx = layoutFixture(tag, 'agents');
  fx.manifest({ harness: 'agents', target: '.agents', rooms });
  return fx;
}

test('room cell: declared, no AGENTS.md — refused: a room with no instructions', () => {
  const fx = withRooms('rooms-missing', [ROOM]);

  const dry = fx.cli('sync', '--dry-run');
  assert.equal(dry.code, 1);
  assert.match(dry.out, /src\/cli\/AGENTS\.md/);

  const error = captureError(() => fx.sync());
  assert.ok(error instanceof DaorisError);
  assert.equal(error.exitCode, 1);
  assert.match(error.message, /src\/cli\/AGENTS\.md/);
  // Daoris never writes a room's text, so it cannot answer this one itself.
  assert.equal(fx.repoFx.exists(ROOM_AGENTS), false);
  assert.equal(fx.repoFx.exists(ROOM_CLAUDE), false);
  fx.cleanup();
});

test('room cell: declared, AGENTS.md present — no CLAUDE.md is written, and the lock keeps no room', () => {
  const fx = withRooms('rooms-create', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n\nIts own conventions.\n');

  const lock = fx.sync();

  assert.equal(fx.repoFx.exists(ROOM_CLAUDE), false, "every agent reads the room's AGENTS.md itself");
  assert.equal(fx.repoFx.exists('CLAUDE.md'), false, 'nor at the root');
  assert.equal((lock as { rooms?: unknown }).rooms, undefined);
  assert.equal(fx.repoFx.read(ROOM_AGENTS), '# The CLI\n\nIts own conventions.\n', "never a word of the room's text");
  fx.cleanup();
});

test("retired pointer: a root and a room CLAUDE.md holding only the region are removed, the room's text untouched", () => {
  const fx = withRooms('rooms-retire', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, POINTER);
  fx.repoFx.write('CLAUDE.md', POINTER);

  const out = fx.cli('sync');

  assert.equal(out.code, 0, out.out);
  assert.equal(fx.repoFx.exists(ROOM_CLAUDE), false);
  assert.equal(fx.repoFx.exists('CLAUDE.md'), false);
  assert.match(out.out, /pointer {3}CLAUDE\.md \(removed/);
  assert.match(out.out, /pointer {3}src\/cli\/CLAUDE\.md \(removed/);
  assert.equal(fx.repoFx.read(ROOM_AGENTS), '# The CLI\n');
  fx.cleanup();
});

test('retired pointer: a CLAUDE.md with text of its own keeps its region, every byte, and sync says why', () => {
  const fx = withRooms('rooms-kept', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  const own = `# Only for Claude\n\nA note.\n\n${POINTER}`;
  fx.repoFx.write(ROOM_CLAUDE, own);

  const out = fx.cli('sync');

  assert.equal(out.code, 0, out.out);
  assert.equal(fx.repoFx.read(ROOM_CLAUDE), own);
  assert.match(out.out, /kept {6}src\/cli\/CLAUDE\.md — holds text of its own/);
  assert.equal(fx.cli('check').code, 0, 'a file that must stay is not a fact');
  fx.cleanup();
});

test("retired pointer: a hand-written import is the repository's own, left alone and not reported", () => {
  const fx = withRooms('rooms-imports', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, 'See @AGENTS.md for everything.\n');

  const out = fx.cli('sync');

  assert.equal(fx.repoFx.read(ROOM_CLAUDE), 'See @AGENTS.md for everything.\n');
  assert.doesNotMatch(out.out, /CLAUDE\.md/);
  fx.cleanup();
});

test('retired pointer: damaged markers — refused (D59 §4), naming the file and the line', () => {
  const fx = withRooms('rooms-damaged', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, `# Mine\n${OPEN(IMPORT)}\nhalf a region\n`);

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /src\/cli\/CLAUDE\.md: [\s\S]*line 2/);
  assert.equal(fx.repoFx.read(ROOM_CLAUDE), `# Mine\n${OPEN(IMPORT)}\nhalf a region\n`);
  fx.cleanup();
});

test('retired pointer: an undeclared folder is invisible (D5) — its CLAUDE.md is never touched', () => {
  const fx = withRooms('rooms-invisible', []);
  fx.repoFx.write('pkg/AGENTS.md', '# A package\n');
  fx.repoFx.write('pkg/CLAUDE.md', POINTER);

  fx.sync();

  assert.equal(fx.repoFx.read('pkg/CLAUDE.md'), POINTER);
  assert.doesNotMatch(fx.repoFx.region()!, /## Rooms/);
  fx.cleanup();
});

test('hidden brief: an authored CLAUDE.md with no import, at the root or in a room, is named by sync and fails check', () => {
  const fx = withRooms('rooms-hidden', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, '# Claude\nOur notes.\n');
  fx.repoFx.write('CLAUDE.md', '# Claude\nOur root notes.\n');

  const out = fx.cli('sync');

  assert.equal(out.code, 0, out.out);
  assert.match(out.out, /hidden {4}CLAUDE\.md/);
  assert.match(out.out, /hidden {4}src\/cli\/CLAUDE\.md/);
  assert.equal(fx.repoFx.read('CLAUDE.md'), '# Claude\nOur root notes.\n', 'never a word of its text touched');
  const check = fx.cli('check');
  assert.equal(check.code, 1);
  assert.match(check.out, /hidden {4}CLAUDE\.md holds text of its own and does not import AGENTS\.md/);

  fx.repoFx.write('CLAUDE.md', '# Claude\nOur root notes.\n\n@AGENTS.md\n');
  fx.repoFx.write(ROOM_CLAUDE, '@AGENTS.md\n');
  assert.equal(fx.cli('check').code, 0, "a hand-written import is the repository's answer");
  fx.cleanup();
});

test("hidden brief: an undeclared folder's CLAUDE.md is invisible (D5)", () => {
  const fx = withRooms('rooms-hidden-undeclared', []);
  fx.repoFx.write('pkg/AGENTS.md', '# A package\n');
  fx.repoFx.write('pkg/CLAUDE.md', '# Mine\n');
  fx.sync();
  assert.equal(fx.cli('check').code, 0);
  fx.cleanup();
});

test('retired pointer: a byte-order mark before the region does not make it a damaged one', () => {
  const fx = withRooms('rooms-bom', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, `﻿${POINTER}`);

  const out = fx.cli('sync');

  assert.equal(out.code, 0, out.out);
  assert.equal(fx.repoFx.exists(ROOM_CLAUDE), false);
  fx.cleanup();
});

test('retired pointer: a room an earlier lock recorded and the manifest no longer declares loses its pointer too', () => {
  const fx = withRooms('rooms-lock-undeclared', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.sync();
  // The lock an earlier build wrote: the room recorded, its pointer on disk.
  fx.repoFx.write('daoris.lock', `${JSON.stringify({ ...JSON.parse(fx.repoFx.read('daoris.lock')), rooms: [ROOM] }, null, 2)}\n`);
  fx.repoFx.write(ROOM_CLAUDE, POINTER);
  fx.manifest({ harness: 'agents', target: '.agents', rooms: [] });
  assert.equal(fx.cli('check').code, 1, 'check names it before a sync does');

  const out = fx.cli('sync');

  assert.equal(out.code, 0, out.out);
  assert.equal(fx.repoFx.exists(ROOM_CLAUDE), false);
  assert.equal(JSON.parse(fx.repoFx.read('daoris.lock')).rooms, undefined);
  fx.cleanup();
});

/** §2.2: telling rather than loading — each room's path and its first heading, and one sentence. */
test('the region lists every room with its first heading, and check fails when a heading moves on', () => {
  const fx = withRooms('rooms-roster', [ROOM, 'docs']);
  fx.repoFx.write(ROOM_AGENTS, '<!-- a comment -->\n\n# The CLI — its conventions\n\n## Later heading\n');
  fx.repoFx.write('docs/AGENTS.md', '# The records\n');
  fx.sync();

  const region = fx.repoFx.region()!;
  assert.match(region, /## Rooms/);
  assert.match(region, /Read a folder's room before changing anything in it\./);
  assert.match(region, /\| \[src\/cli\]\(src\/cli\/AGENTS\.md\) \| The CLI — its conventions \|/);
  assert.match(region, /\| \[docs\]\(docs\/AGENTS\.md\) \| The records \|/);
  assert.equal(fx.cli('check').code, 0);

  fx.repoFx.write(ROOM_AGENTS, '# The CLI, renamed\n');
  const check = fx.cli('check');
  assert.equal(check.code, 1);
  assert.match(check.out, /roster/);
  fx.cleanup();
});

test('check fails on a declared room with no AGENTS.md, and on a pointer file holding only the retired region', () => {
  const fx = withRooms('rooms-check', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.sync();
  assert.equal(fx.cli('check').code, 0);

  fx.repoFx.write(ROOM_CLAUDE, POINTER);
  const pointer = fx.cli('check');
  assert.equal(pointer.code, 1);
  assert.match(pointer.out, /pointer {3}src\/cli\/CLAUDE\.md holds only the retired import/);

  fx.sync();
  assert.equal(fx.cli('check').code, 0);
  rmSync(join(fx.repoFx.root, ROOM_AGENTS));
  const room = fx.cli('check');
  assert.equal(room.code, 1);
  assert.match(room.out, /src\/cli\/AGENTS\.md/);
  fx.cleanup();
});

/** Rooms are the layout's, not one descriptor's: the older layout keeps them the same way. */
test('a room declared on claude-code writes no pointer either', () => {
  const fx = layoutFixture('rooms-claude');
  fx.manifest({ rooms: [ROOM] });
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');

  fx.sync();

  assert.equal(fx.repoFx.exists(ROOM_CLAUDE), false);
  fx.cleanup();
});
