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
import { markFindings, noteLabel, planIndex, readDecision, readMarks } from './orient-index.mjs';

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
// The mark a note carries (KNOWFRESH1a-i): the twin table the service's DecisionNotes is held to, and the gate

const MARKS = JSON.parse(readFileSync(join(here, 'orient-index-fixtures', 'note-marks.json'), 'utf8'));

/** A decision's marks as the table writes them: `D10:19 amended by D121 replacing D10:5`. */
function markRows(id, lines) {
  return readMarks(readDecision(id, lines)).map((mark) => {
    const target = mark.quote === null ? '' : ` replacing ${mark.target === null ? 'none' : `${id}:${mark.target}`}`;
    return `${id}:${mark.line} ${mark.kind} by ${mark.by === null ? 'self' : `D${mark.by}`}${target}`;
  });
}

test("each decision's marks are the rows the mark table gives them", () => {
  for (const decision of MARKS.decisions) {
    assert.deepEqual(markRows(decision.id, decision.lines), decision.marks, `${decision.id}: ${decision.why}`);
  }
});

test('the mark table holds each form of mark and a decision with none, and the repository real lines', () => {
  assert.ok(MARKS.decisions.some((decision) => decision.marks.length === 0));
  const marks = MARKS.decisions.flatMap((decision) => decision.marks);
  for (const form of ['amended by D', 'superseded by D', 'by self', 'replacing none', 'replacing D10:']) {
    assert.ok(marks.some((row) => row.includes(form)), `a row holds ${form}`);
  }
  const text = MARKS.decisions[0].lines.join('\n');
  for (const real of ["**Amended by D158's ASKSCOPE1c note, 2026-10-19:**", '*Amended by D124 (WSSETUP5, 2026-10-01), §6.5:', '**Amended by D85 (2026-09-28).**', '*(Amended by D70, 2026-09-24:']) {
    assert.ok(text.includes(real), `the table carries ${real}`);
  }
});

test('a quote is held to the note label in full, which the digest cuts at 80 characters', () => {
  const long = MARKS.decisions[0].lines[6].split('**')[1];
  assert.ok(long.length > 80, 'the long label is longer than the digest keeps');
  const [quoted, cut] = [20, 22].map((at) => /replacing "(.*)":/.exec(MARKS.decisions[0].lines[at])[1]);
  assert.equal(quoted, long);
  assert.equal(noteLabel(MARKS.decisions[0].lines[6]).endsWith('…'), true);
  assert.ok(cut.endsWith('…'), 'the cut label is the one the digest shows');
});

/** The findings of the gate over a scratch tree whose decisions are `files`, each `D<n>` to its lines. */
function gate(name, files) {
  const at = join(scratch, name);
  mkdirSync(join(at, 'docs', 'decisions'), { recursive: true });
  writeFileSync(join(at, 'daoris.json'), JSON.stringify({ documents: { decisions: 'docs/decisions' } }));
  const paths = ['daoris.json'];
  for (const [id, lines] of Object.entries(files)) {
    writeFileSync(join(at, 'docs', 'decisions', `${id}.md`), lines.join('\n'));
    paths.push(`docs/decisions/${id}.md`);
  }
  return markFindings(at, paths).map((finding) => `${finding.file}:${finding.line} ${finding.fact}`);
}

const OPENING = (id) => [`## ${id} — Subject (2026-09-01)`, '', `**Decision (${id}, 2026-09-01).** The rule.`, ''];

test('the gate passes a mark that names a known, higher decision and a quote that names an earlier note', () => {
  const older = [...OPENING('D10'), '**Built 2026-09-02 (A): the first.** It.', '', '*Amended by D20 (B, 2026-09-03): changed.*', '', '**Superseded by D20 (C, 2026-09-04), replacing "Built 2026-09-02 (A): the first.":** gone.', ''];
  assert.deepEqual(gate('gate-pass', { D10: older, D20: OPENING('D20') }), []);
});

test('the gate refuses a mark naming a decision that does not exist', () => {
  const older = [...OPENING('D10'), '*Amended by D99 (B, 2026-09-03): changed.*', ''];
  assert.deepEqual(gate('gate-unknown', { D10: older }), ['docs/decisions/D10.md:5 names D99, which is no decision of this repository']);
});

test('the gate refuses a mark naming a decision numbered no higher than the one it sits in', () => {
  const same = [...OPENING('D30'), '*Amended by D30 (B, 2026-09-03): itself.*', '', '*Amended by D20 (C, 2026-09-04): an older one.*', ''];
  assert.deepEqual(gate('gate-lower', { D30: same, D20: OPENING('D20') }), [
    'docs/decisions/D30.md:5 names D30, which is not numbered higher than D30',
    'docs/decisions/D30.md:7 names D20, which is not numbered higher than D30',
  ]);
});

test('the gate refuses a quote that names no earlier note: a later one, a cut label, and none at all', () => {
  const older = [
    ...OPENING('D10'),
    '**Amended by D20 (B, 2026-09-03), replacing "Built 2026-09-05 (L): later.":** a later note is no earlier one.',
    '',
    '**Built 2026-09-05 (L): later.** It.',
    '',
    '**Built 2026-09-06 (M): a note whose label runs past the eighty characters the digest keeps, to the end.** It.',
    '',
    '**Amended (X, 2026-09-07), replacing "Built 2026-09-06 (M): a note whose label runs past the eighty characters…":** cut.',
    '',
    '**Amended (Y, 2026-09-08), replacing "Built 2026-09-06 (M): a note whose label runs past the eighty characters the digest keeps, to the end.":** whole.',
    '',
  ];
  assert.deepEqual(gate('gate-quote', { D10: older, D20: OPENING('D20') }), [
    'docs/decisions/D10.md:5 quotes a label that is no earlier note of D10, the full text of one and never the digest\'s cut',
    'docs/decisions/D10.md:11 quotes a label that is no earlier note of D10, the full text of one and never the digest\'s cut',
  ]);
});

test('the gate reads a mark by the same rule the table does: an opening decision and a mid-paragraph remark are no mark', () => {
  const older = [...OPENING('D10'), '**Amended by D99 (B, 2026-09-03):** an entry that does not exist.', '', 'A paragraph.', '*(Amended by D98, 2026-09-24: inline.)*', ''];
  assert.deepEqual(gate('gate-reads', { D10: older }), ['docs/decisions/D10.md:5 names D99, which is no decision of this repository']);
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
