// Rooms: nested `AGENTS.md` files the manifest declares (LAYOUT3; D117 §2.2 and §5.4's pointer table).
//
// A room's text is the repository's own and Daoris never writes it. What `sync` keeps is the pointer
// beside it — `<room>/CLAUDE.md` holding the import region, exactly as at the root (D59 §4) — and a
// row for it in the roster. One test per cell of the pointer table, written before the code.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { LayoutFixture } from './_layout.ts';
import { captureError } from './_fixture.ts';
import { layoutFixture } from './_layout.ts';
import { CLOSE, IMPORT, OPEN } from '../src/region.ts';
import { DaorisError } from '../src/errors.ts';

const ROOM = 'src/cli';
const ROOM_AGENTS = 'src/cli/AGENTS.md';
const ROOM_CLAUDE = 'src/cli/CLAUDE.md';
const POINTER = `${OPEN(IMPORT)}\n@AGENTS.md\n${CLOSE(IMPORT)}\n`;

function withRooms(tag: string, rooms: string[]): LayoutFixture {
  const fx = layoutFixture(tag, 'agents');
  fx.manifest({ harness: 'agents', target: '.agents', rooms });
  return fx;
}

test('pointer cell: declared, no AGENTS.md — refused: a room with no instructions', () => {
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

test('pointer cell: declared, AGENTS.md present, no CLAUDE.md — created, the import alone', () => {
  const fx = withRooms('rooms-create', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n\nIts own conventions.\n');

  const lock = fx.sync();

  assert.equal(fx.repoFx.read(ROOM_CLAUDE), POINTER);
  assert.deepEqual(lock.rooms, [ROOM]);
  assert.equal(fx.repoFx.read(ROOM_AGENTS), '# The CLI\n\nIts own conventions.\n', 'never a word of the room\'s text');
  fx.cleanup();
});

test('pointer cell: declared, CLAUDE.md already imports it — nothing', () => {
  const fx = withRooms('rooms-imports', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, 'See @AGENTS.md for everything.\n');

  fx.sync();

  assert.equal(fx.repoFx.read(ROOM_CLAUDE), 'See @AGENTS.md for everything.\n');
  fx.cleanup();
});

test('pointer cell: declared, CLAUDE.md with text of its own — the import region is appended, every word kept', () => {
  const fx = withRooms('rooms-append', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, '# Only for Claude\n\nA note.\n');

  fx.sync();

  const text = fx.repoFx.read(ROOM_CLAUDE);
  assert.ok(text.startsWith('# Only for Claude\n\nA note.\n'));
  assert.match(text, /@AGENTS\.md/);
  fx.cleanup();
});

test('pointer cell: declared, damaged markers — refused (D59 §4), naming the line', () => {
  const fx = withRooms('rooms-damaged', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, `# Mine\n${OPEN(IMPORT)}\nhalf a region\n`);

  const error = captureError(() => fx.sync());

  assert.ok(error instanceof DaorisError);
  assert.match(error.message, /line 2/);
  assert.equal(fx.repoFx.read(ROOM_CLAUDE), `# Mine\n${OPEN(IMPORT)}\nhalf a region\n`);
  fx.cleanup();
});

test('pointer cell: undeclared, in the lock, holding the region — the region is removed, and the file when it was all', () => {
  const fx = withRooms('rooms-undeclare', [ROOM, 'docs']);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write('docs/AGENTS.md', '# The records\n');
  fx.repoFx.write('docs/CLAUDE.md', '# Mine\n');
  fx.sync();
  fx.manifest({ harness: 'agents', target: '.agents', rooms: [] });

  const lock = fx.sync();

  assert.equal(fx.repoFx.exists(ROOM_CLAUDE), false, 'the region was all of it');
  assert.equal(fx.repoFx.read('docs/CLAUDE.md'), '# Mine\n', 'the room\'s own words survive, byte for byte');
  assert.equal(fx.repoFx.read(ROOM_AGENTS), '# The CLI\n');
  assert.equal(lock.rooms, undefined);
  fx.cleanup();
});

test('pointer cell: undeclared, in the lock, no region — the room is dropped', () => {
  const fx = withRooms('rooms-drop', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.repoFx.write(ROOM_CLAUDE, 'See @AGENTS.md.\n');
  fx.sync();
  fx.manifest({ harness: 'agents', target: '.agents', rooms: [] });

  const lock = fx.sync();

  assert.equal(fx.repoFx.read(ROOM_CLAUDE), 'See @AGENTS.md.\n');
  assert.equal(lock.rooms, undefined);
  fx.cleanup();
});

test('pointer cell: undeclared and never in the lock — invisible (D5)', () => {
  const fx = withRooms('rooms-invisible', []);
  fx.repoFx.write('pkg/AGENTS.md', '# A package\n');
  fx.repoFx.write('pkg/CLAUDE.md', `# Mine\n\n${POINTER}`);

  fx.sync();

  assert.equal(fx.repoFx.read('pkg/CLAUDE.md'), `# Mine\n\n${POINTER}`);
  assert.doesNotMatch(fx.repoFx.region()!, /## Rooms/);
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

test('check fails on a declared room with no AGENTS.md, and on a room whose pointer is missing', () => {
  const fx = withRooms('rooms-check', [ROOM]);
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');
  fx.sync();
  assert.equal(fx.cli('check').code, 0);

  rmSync(join(fx.repoFx.root, ROOM_CLAUDE));
  const pointer = fx.cli('check');
  assert.equal(pointer.code, 1);
  assert.match(pointer.out, /src\/cli\/CLAUDE\.md/);

  fx.sync();
  rmSync(join(fx.repoFx.root, ROOM_AGENTS));
  const room = fx.cli('check');
  assert.equal(room.code, 1);
  assert.match(room.out, /src\/cli\/AGENTS\.md/);
  fx.cleanup();
});

/** Rooms are the layout's, not one descriptor's: the older layout keeps them the same way. */
test('a room declared on claude-code gets its pointer too', () => {
  const fx = layoutFixture('rooms-claude');
  fx.manifest({ rooms: [ROOM] });
  fx.repoFx.write(ROOM_AGENTS, '# The CLI\n');

  fx.sync();

  assert.equal(fx.repoFx.read(ROOM_CLAUDE), POINTER);
  fx.cleanup();
});
