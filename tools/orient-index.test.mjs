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
 * and its rows are compared, so the row's own shape is held too.
 *
 * ORIENT2a2 adds the README's *Kept by hand* section here, beside the twin and as fast: the canon's
 * `templates/index.md` lists the indexes a repository keeps by hand first, under that heading, and the generated files
 * after them under *Generated*. The rest of `orient-index.mjs` is the CLI suite's
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

// ---------------------------------------------------------------------------------------------------
// The README's own shape (ORIENT2a2): what is kept by hand first, then what this tool generates

/** The README `planIndex` writes for a scratch tree of `files`, each path to its content. */
function readmeOf(name, files) {
  const at = join(scratch, name);
  for (const [path, content] of Object.entries(files)) {
    mkdirSync(dirname(join(at, path)), { recursive: true });
    writeFileSync(join(at, path), content);
  }
  return planIndex(at, Object.keys(files)).get('docs/index/README.md');
}

/** The lines under `## <heading>`, up to the next heading of that level. */
function section(readme, heading) {
  const lines = readme.split('\n');
  const start = lines.indexOf(`## ${heading}`);
  assert.ok(start >= 0, `no "## ${heading}" in:\n${readme}`);
  const end = lines.findIndex((line, i) => i > start && line.startsWith('## '));
  return lines.slice(start + 1, end < 0 ? lines.length : end).join('\n');
}

const GENERATED_ROWS = ['`routes.md`', '`verbs.md`', '`catalogues.md`', '`fixtures.md`', '`decisions.md`', '`outlines/<path>.md`'];

test('the README lists the indexes kept by hand first, then the files it generates, then the large files', () => {
  const readme = readmeOf('kept-by-hand', {
    'daoris.json': JSON.stringify({ target: '.claude', documents: { router: 'docs/README.md', decisions: 'docs/decisions' } }),
    'docs/README.md': '# The documents\n',
    'daoris.lanes.json': JSON.stringify({ lanes: [{ id: 'code', title: 'Code', paths: ['src/**'] }] }),
    '.claude/INDEX.md': '# Index\n',
    'docs/code-map.json': '{ "version": 1, "modules": [] }\n',
  });
  const headings = readme.split('\n').filter((line) => line.startsWith('## '));
  assert.deepEqual(headings.map((line) => line.replace(/ \(\d+\)$/, '')), ['## Kept by hand', '## Generated', '## Files over 40 KB']);

  const kept = section(readme, 'Kept by hand');
  assert.match(kept, /^\| Index \| Answers \|$/m);
  const rows = kept.split('\n').filter((line) => /^\| `/.test(line)).map((line) => line.split(' | ')[0].slice(2));
  assert.deepEqual(rows, ['`docs/README.md`', '`daoris.lanes.json`'], 'the router, then the lanes map, each named from the tree');
  // The generated indexes kept elsewhere are named above the sections, as the template names the knowledge's, and
  // are not listed as kept by hand.
  const opening = readme.slice(0, readme.indexOf('## Kept by hand'));
  assert.match(opening, /`\.claude\/INDEX\.md`/);
  assert.match(opening, /`docs\/code-map\.json`/);
  assert.match(opening, /nothing here restates/);

  const generated = section(readme, 'Generated');
  assert.match(generated, /^\| File \| Answers \|$/m);
  for (const row of GENERATED_ROWS) assert.ok(generated.includes(`| ${row} |`), `${row} is still a generated row`);
});

test('a tree that keeps no index by hand says none, so an empty section is not a forgotten one', () => {
  const readme = readmeOf('none-kept', { 'src/a.ts': 'export const a = 1;\n' });
  const kept = section(readme, 'Kept by hand');
  assert.match(kept, /^None\b/m);
  assert.doesNotMatch(kept, /\| Index \|/);
  assert.doesNotMatch(readme.slice(0, readme.indexOf('## Kept by hand')), /INDEX\.md|code-map/, 'nothing absent is named');
  const generated = section(readme, 'Generated');
  for (const row of GENERATED_ROWS) assert.ok(generated.includes(`| ${row} |`), `${row} is still a generated row`);
});
