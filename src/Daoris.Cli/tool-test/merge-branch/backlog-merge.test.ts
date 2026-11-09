import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { BACKLOG_MERGES, repoRoot, tool } from '../support/merge-branch.ts';

// reaches: src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/backlog-merges.json — the driver's twin reads the table row for row

// The line merge alone, which a declared row table settles by and the backlog's rows stand on.
test('the merge by base keeps a row a side added, drops one a side removed, and leaves one both sides edited', () => {
  const base = ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A2** two.', '- [ ] **A3** three.', ''].join('\n');
  // An added row beside a removed one, which git reads as one conflict.
  const added = tool.mergeByBase(base, base.replace('- [ ] **A2** two.\n', ''), base.replace('- [ ] **A3** three.\n', '- [ ] **A3** three.\n- [ ] **A4** four.\n'));
  assert.equal(added.text, ['# Tasks', '', '- [ ] **A1** one.', '- [ ] **A3** three.', '- [ ] **A4** four.', ''].join('\n'));
  assert.deepEqual([added.kept, added.dropped, added.both], [1, 1, []]);
  // One row both sides edited is the person's: no text, and the base's line it is.
  const edited = tool.mergeByBase(base, base.replace('**A2** two.', '**A2** two, mine.'), base.replace('**A2** two.', '**A2** two, theirs.'));
  assert.equal(edited.text, null);
  assert.deepEqual(edited.both, [{ line: 4 }]);
  const same = base.replace('**A2** two.', '**A2** edited.');
  assert.equal(tool.mergeByBase(base, same, same).text, same);
});

type Merge = {
  name: string; base: string[]; ours: string[]; theirs: string[]; text: string[] | null; kept: number; dropped: number; both: number[]; table?: boolean;
  differ?: { id: string; ours: string | null; theirs: string | null }[]; twice?: string[]; unread?: string[];
};
type Held = { name: string; base: string[]; ours: string[]; theirs: string[]; made: string[]; twice: string[]; kept: string[]; lost: string[]; refused: boolean };
const table = JSON.parse(readFileSync(join(repoRoot, BACKLOG_MERGES), 'utf8')) as {
  paths: { name: string; manifest: string | null; backlog: string | null }[];
  rows: { name: string; declaration: string | null; tables: string[] }[];
  merges: Merge[];
  ids: { name: string; line: string; key: string | null; ids: string[] | null }[];
  held: Held[];
};

// The rules are a twin: a landing's replay settles the backlog by the driver's BacklogRows and holds the made replay to them by its
// BacklogHeld, and both hold this one table, row for row.
test("the declared backlog, its rows and its merges read as the table the driver's landing holds too (LANDBACKLOG1, PROCSTEPS1a)", () => {
  assert.ok(table.paths.length >= 10 && table.merges.length >= 25 && table.rows.length >= 10 && table.ids.length >= 15 && table.held.length >= 5,
    'the table holds its rows');
  for (const row of table.paths) assert.equal(tool.backlogOf(row.manifest), row.backlog, row.name);
  for (const row of table.rows) assert.deepEqual(tool.rowTablesOf(row.declaration), row.tables, row.name);
  for (const row of table.ids) {
    const read = tool.rowOf(row.line);
    assert.deepEqual(read && { key: read.key, ids: read.ids }, row.key === null ? null : { key: row.key, ids: row.ids }, row.name);
  }
  for (const row of table.merges) {
    const [base, ours, theirs] = [row.base, row.ours, row.theirs].map((lines) => lines.join('\n')) as [string, string, string];
    const merged = row.table ? { differ: [], twice: [], unread: [], ...tool.mergeByBase(base, ours, theirs) } : tool.mergeByRows(base, ours, theirs);
    assert.deepEqual(
      { text: merged.text, kept: merged.kept, dropped: merged.dropped, both: merged.both.map((entry) => entry.line), differ: merged.differ, twice: merged.twice, unread: merged.unread },
      { text: row.text?.join('\n') ?? null, kept: row.kept, dropped: row.dropped, both: row.both, differ: row.differ ?? [], twice: row.twice ?? [], unread: row.unread ?? [] },
      row.name);
  }
  for (const row of table.held) {
    const held = tool.rowsHeld(...([row.base, row.ours, row.theirs, row.made].map((lines) => lines.join('\n')) as [string, string, string, string]));
    assert.deepEqual({ twice: held.twice, kept: held.kept, lost: held.lost, refused: held.refused !== null },
      { twice: row.twice, kept: row.kept, lost: row.lost, refused: row.refused }, row.name);
  }
});

test('a refused backlog says each id twice and each row both sides changed, with both texts', () => {
  const merged = tool.mergeByRows('- [ ] **A2** two.\n', '- [ ] **A2** two, then B.\n', '');
  assert.deepEqual(tool.rowsRefused('TASKS.md', merged), [
    'TASKS.md: row A2 changed on both sides; left for you: main has "- [ ] **A2** two, then B.", and the branch removed it',
  ]);
  assert.deepEqual(tool.rowsRefused('TASKS.md', tool.mergeByRows('', '', '- [ ] **A1** one.\n- [ ] **A1** again.\n')), [
    'TASKS.md: holds the row A1 twice on a side, so none of it is resolved by its rows; left for you',
  ]);
});
