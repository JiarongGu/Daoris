import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from './_fixture.ts';

// SESSOPT1b (D127, design §3): an entry has a shape, and `tools/doc-shapes.mjs` reports a backlog row, a router
// row or a new archive outcome over it. A shape is a judgement, so it reports and exits 0 (D54); a configuration
// it cannot read is a fact, so it fails.
// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const shapes = await import('../../../tools/doc-shapes.mjs') as {
  words: (text: string) => number;
  fenced: (text: string[]) => boolean[];
  readShapes: (text: string) => {
    shapes: { backlogRow: number; routerRow: number; archiveOutcome: number; archiveCutOver: string | null };
    errors: string[];
  };
  backlogRows: (text: string) => { id: string; words: number }[];
  routerRows: (text: string) => { id: string; words: number }[];
  archiveEntries: (text: string) => { id: string; date: string | null; outcome: number }[];
  report: (root: string) => { lines: string[]; failures: string[] };
};
// @ts-expect-error — untyped workspace tooling
const budgets = await import('../../../tools/doc-budgets.mjs') as { words: (text: string) => number };
// @ts-expect-error — untyped workspace tooling
const duplicates = await import('../../../tools/doc-duplicates.mjs') as { fenced: (text: string[]) => boolean[] };

const CONFIG = 'tools/doc-shapes.json';
const config = (over: Record<string, unknown> = {}) =>
  JSON.stringify({ _why: 'prose', backlogRow: 60, routerRow: 60, archiveOutcome: 60, archiveCutOver: null, ...over });
const many = (n: number, word = 'word') => Array.from({ length: n }, () => word).join(' ');

test('a word is counted as doc-budgets counts one, by the same function', () => {
  assert.equal(shapes.words, budgets.words);
});

test('a fence is read as doc-duplicates reads one, by the same function', () => {
  assert.equal(shapes.fenced, duplicates.fenced);
});

test('a backlog row is its item and its indented lines, blank lines inside it too, named by its identifier', () => {
  const text = [
    '# Tasks', '',
    '- [ ] **ROW1 — the first** (§2): two words', '  and three more', '',
    '  **A later paragraph** of the same row', 'An unindented line ends it.', '',
    '- [ ] ⏸ **ROW2 — held**: one', '- [x] **ROW3\'s leftovers** done', '',
    '### Next', '- [ ] a row with no bold name at all',
  ].join('\n');

  assert.deepEqual(shapes.backlogRows(text), [
    // -, [, ], **ROW1, —, the, first**, (§2):, two, words, and, three, more, **A, later, paragraph**, of, the, same, row:
    // counted as written, the marker and the box too, as doc-budgets counts a document
    { id: 'ROW1', words: 20 },
    { id: 'ROW2', words: 8 },
    { id: 'ROW3\'s leftovers', words: 5 },
    { id: 'a row with no bold name at all', words: 11 },
  ]);
});

test('a router row is a table row whose first cell is a code span, counted as written, pipes too', () => {
  const text = [
    '| Document | Kind | For | Where it stands |', '|---|---|---|---|',
    '| `a-design.md` | contract | the CLI | Current |',
    '| plain | not | a | document |',
    '| `b-study.md` | study | why a \\| b | Built under D1, D2 and D3 |',
  ].join('\n');

  assert.deepEqual(shapes.routerRows(text), [
    { id: '`a-design.md`', words: 10 },
    // an escaped pipe is a cell's text, not the end of the first cell
    { id: '`b-study.md`', words: 17 },
  ]);
});

test('an archive entry is a second-level heading, dated by the last date it names, and its outcome carries the label', () => {
  const text = [
    '# Archive', '',
    '## OLD1 — before (2026-09-30)', '', '- [x] an old checklist item', '',
    '## NEW1 — the shape (2026-10-03)', '', '> - [ ] **NEW1 — the shape**: the row as it stood', '',
    '**Outcome.** Built: one thing. Detail: D1\'s note.', '',
    '```', '## not a heading, inside a fence', '```', '',
    '## NEW2 — no label (2026-10-01 → 2026-10-04)', '', '> the row', '', 'What changed, told without the label.', '',
    '## Undated — a section with no date', '', 'text',
  ].join('\n');

  assert.deepEqual(shapes.archiveEntries(text), [
    { id: 'OLD1', date: '2026-09-30', outcome: 6 },
    // **Outcome.**, Built:, one, thing., Detail:, D1's, note., then the fence and the heading-like line inside it
    { id: 'NEW1', date: '2026-10-03', outcome: 16 },
    // an entry with no label: what it says beyond its heading and its quoted row
    { id: 'NEW2', date: '2026-10-04', outcome: 6 },
    { id: 'Undated', date: null, outcome: 1 },
  ]);
});

test('a fence is read as markdown reads one: a longer fence quoting a shorter one, and a tilde fence, hold their headings', () => {
  // ORIENT2h4: a three-backtick line inside a four-backtick fence closes nothing, and a tilde fence fences.
  const text = [
    '# Archive', '',
    '## LONG1 — quotes an example (2026-10-07)', '', '**Outcome.** Quoted.', '',
    '````markdown', '```markdown', '## QUOTED — inside the longer fence, not an entry', '```', '````', '',
    '## TILDE1 — a tilde fence (2026-10-07)', '', '**Outcome.** Fenced.', '',
    '~~~text', '## TILDED — inside a tilde fence, not an entry', '~~~', '',
    '## AFTER1 — after both fences (2026-10-08)', '', '**Outcome.** Read.',
  ].join('\n');

  assert.deepEqual(shapes.archiveEntries(text).map((entry) => entry.id), ['LONG1', 'TILDE1', 'AFTER1']);
});

test('the configuration reads, and each way it cannot read is named', () => {
  assert.deepEqual(shapes.readShapes(config()).errors, []);
  assert.deepEqual(shapes.readShapes(config({ archiveCutOver: '2026-10-03' })).shapes.archiveCutOver, '2026-10-03');

  const errors = (text: string) => shapes.readShapes(text).errors;
  assert.match(errors('{ not json').join(), /does not read/);
  assert.match(errors(config({ backlog: 60 })).join(), /unknown key `backlog`/);
  assert.match(errors(config({ archiveCutOver: 'soon' })).join(), /archiveCutOver.*not a date/);
  assert.match(errors(config({ archiveCutOver: '2026-02-30' })).join(), /archiveCutOver.*not a date/);
  assert.match(errors(config({ routerRow: 0 })).join(), /routerRow.*positive whole number/);
  assert.match(errors(JSON.stringify({ backlogRow: 60, routerRow: 60, archiveCutOver: null })).join(), /names no `archiveOutcome`/);
  assert.match(errors('[]').join(), /not an object/);
});

test('rows and router rows over their shape are reported longest first, and the rest only counted', () => {
  const fx = makeFixture('doc-shapes-over');
  fx.write('daoris.json', JSON.stringify({
    documents: { backlog: { path: 'TASKS.md', words: 6600 }, router: 'docs/README.md', archive: 'docs/task-archive.md' },
  }));
  fx.write(CONFIG, config({ backlogRow: 10, routerRow: 10 }));
  fx.write('TASKS.md', [
    `- [ ] **SHORT1 — fine** ${many(3)}`,
    `- [ ] **LONG1 — long** ${many(12)}`,
    `- [ ] **LONGER1 — longer** ${many(20)}`,
  ].join('\n'));
  fx.write('docs/README.md', `| \`a.md\` | contract | ${many(15)} |\n| \`b.md\` | study | short |\n`);
  fx.write('docs/task-archive.md', '## OLD1 — before (2026-09-30)\n\ntext\n');

  const { lines, failures } = shapes.report(fx.root);
  const text = lines.join('\n');

  assert.deepEqual(failures, []);
  assert.match(text, /backlog `TASKS\.md`: 2 of 3 rows over 10 words/);
  assert.ok(text.indexOf('LONGER1') < text.indexOf('LONG1 '), 'the longest row comes first');
  assert.match(text, /LONGER1\s+26/);
  assert.doesNotMatch(text, /SHORT1/);
  assert.match(text, /router `docs\/README\.md`: 1 of 2 rows over 10 words/);
  assert.match(text, /`a\.md`\s+21/);
  fx.cleanup();
});

test('with no cut-over every archive entry stands as written, and after one only the new outcomes are measured', () => {
  const fx = makeFixture('doc-shapes-archive');
  fx.write('daoris.json', JSON.stringify({ documents: { archive: 'docs/task-archive.md' } }));
  fx.write('docs/task-archive.md', [
    `## OLD1 — long and old (2026-09-30)`, '', `**Outcome.** ${many(80)}`, '',
    `## NEW1 — long and new (2026-10-03)`, '', `**Outcome.** ${many(70)}`, '',
    `## NEW2 — short and new (2026-10-04)`, '', `**Outcome.** ${many(5)}`, '',
    `## Notes — undated`, '', many(90),
  ].join('\n'));

  fx.write(CONFIG, config());
  const before = shapes.report(fx.root).lines.join('\n');
  assert.match(before, /archive `docs\/task-archive\.md`: no cut-over yet, so its 4 entries stand as written/);
  assert.doesNotMatch(before, /OLD1|NEW1/);

  fx.write(CONFIG, config({ archiveCutOver: '2026-10-03' }));
  const after = shapes.report(fx.root);
  const text = after.lines.join('\n');
  assert.deepEqual(after.failures, []);
  assert.match(text, /archive `docs\/task-archive\.md`: 1 of 2 outcomes since 2026-10-03 over 60 words; 1 before it, standing as written; 1 undated, not measured/);
  assert.match(text, /NEW1\s+71/);
  assert.doesNotMatch(text, /OLD1|NEW2|Notes/);
  fx.cleanup();
});

test('a role declared with no file on disk is one line and no failure, and a role not declared is said', () => {
  const fx = makeFixture('doc-shapes-missing');
  fx.write('daoris.json', JSON.stringify({ documents: { backlog: 'TASKS.md' } }));
  fx.write(CONFIG, config());

  const { lines, failures } = shapes.report(fx.root);
  const text = lines.join('\n');

  assert.deepEqual(failures, []);
  assert.match(text, /backlog `TASKS\.md`: declared, and no file is there/);
  assert.match(text, /router: none declared/);
  assert.match(text, /archive: none declared/);
  fx.cleanup();
});

test('a configuration that is missing or does not read fails, and measures nothing', () => {
  const fx = makeFixture('doc-shapes-unread');
  fx.write('daoris.json', JSON.stringify({ documents: { backlog: 'TASKS.md' } }));
  fx.write('TASKS.md', `- [ ] **LONG1 — long** ${many(80)}`);

  assert.match(shapes.report(fx.root).failures.join(), /tools\/doc-shapes\.json.*no file/);

  fx.write(CONFIG, config({ archiveCutOver: 'next week' }));
  const { lines, failures } = shapes.report(fx.root);
  assert.match(failures.join(), /archiveCutOver.*not a date/);
  assert.doesNotMatch(lines.join('\n'), /LONG1/);
  fx.cleanup();
});

test("this repository's configuration reads, and every role it declares is measured", () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
  const { lines, failures } = shapes.report(root);
  assert.deepEqual(failures, []);
  for (const role of ['backlog', 'router', 'archive']) {
    const measured = new RegExp(`^doc-shapes: ${role} \`[^\`]+\`: (\\d+ of \\d+|no cut-over yet)`);
    assert.ok(lines.some((line) => measured.test(line)), `${role} is measured`);
  }
});
