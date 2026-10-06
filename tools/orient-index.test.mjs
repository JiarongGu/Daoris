/**
 * ORIENT1h: the decisions digest's half of a twin with the service's `DecisionNotes` (D134's ORIENT1g note).
 *
 *   node --test tools/orient-index.test.mjs
 *
 * The digest (`orient-index.mjs`, over `doc-duplicates.mjs`' `fenced` and `isNoteLabel`) and the service's scanner find
 * and label a decision's dated notes with code of their own. Both are held to one table,
 * `orient-index-fixtures/decision-notes.json`, row for row: each decision's lines, the lines of its own entry and
 * each note's row, then each label line and the label it opens. The service's `DecisionNotesTests` reads the same
 * file. Before ORIENT1h the service was held to rows this side wrote once, so a note's form changed here alone
 * passed every gate.
 *
 * Run by `npm run verify`, which every merge runs, unlike the benches beside it: it takes a fraction of a second,
 * and a twin table no gate reads holds nothing.
 *
 * The digest is read as it is written: each decision goes into a scratch work tree, `planIndex` writes the digest,
 * and its rows are compared, so the row's own shape is held too. The rest of `orient-index.mjs` is the CLI suite's
 * (`src/Daoris.Cli/test/orient-index.test.ts`). Its scratch is a gitignored folder of the repository.
 */
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { noteLabel, planIndex } from './orient-index.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const root = dirname(here);
const TABLE = JSON.parse(readFileSync(join(here, 'orient-index-fixtures', 'decision-notes.json'), 'utf8'));
const scratch = join(root, 'local', 'scratch', 'orient-index-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

/** The digest's rows for each decision of the table: its title row's lines, then each note's row, `D<n>:<a>-<b> <label>`. */
function digestRows() {
  mkdirSync(join(scratch, 'docs', 'decisions'), { recursive: true });
  writeFileSync(join(scratch, 'daoris.json'), JSON.stringify({ documents: { decisions: 'docs/decisions' } }));
  const files = ['daoris.json'];
  for (const decision of TABLE.decisions) {
    writeFileSync(join(scratch, 'docs', 'decisions', `${decision.id}.md`), decision.lines.join('\n'));
    files.push(`docs/decisions/${decision.id}.md`);
  }
  const digest = planIndex(scratch, files).get('docs/index/decisions.md').split('\n');
  const notesAt = digest.findIndex((line) => line.startsWith('## Notes ('));
  const rows = new Map(TABLE.decisions.map((decision) => [decision.id, { entry: null, notes: [] }]));
  digest.forEach((line, i) => {
    const row = /^- ((D\d+):\d+-\d+)( .*)$/.exec(line);
    if (!row) return;
    if (i < notesAt) rows.get(row[2]).entry = row[1];
    else rows.get(row[2]).notes.push(`${row[1]}${row[3]}`);
  });
  return rows;
}

test("each decision's entry and notes are the rows the table gives them", () => {
  const rows = digestRows();
  for (const decision of TABLE.decisions) {
    assert.deepEqual(rows.get(decision.id), { entry: decision.entry, notes: decision.notes }, `${decision.id}: ${decision.why}`);
  }
});

test('each line opens the label the table gives it, or none', () => {
  for (const [why, line, label] of TABLE.labels) {
    const read = noteLabel(line);
    assert.equal(read, label, `${why}: ${JSON.stringify(line)} expected ${JSON.stringify(label)}, read ${JSON.stringify(read)}`);
  }
});

test('the table holds a decision with notes and one without, and a line that is a label and one that is not', () => {
  assert.ok(TABLE.decisions.some((decision) => decision.notes.length > 0));
  assert.ok(TABLE.decisions.some((decision) => decision.notes.length === 0));
  assert.ok(TABLE.labels.some(([, , label]) => label !== null));
  assert.ok(TABLE.labels.some(([, , label]) => label === null));
});
