import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from './_fixture.ts';

// MOD1 (D106): the append-only records merge by union, and union keeps both versions of a line two branches
// changed. `tools/doc-duplicates.mjs` refuses what that leaves: a decision number, a heading, an index row or
// a changelog line twice. DOC8b (D134 §3.4): once the decisions record is a folder of one file per decision, it
// refuses what union can leave in one decision's file, and a decision written into the page the record left.
// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const { duplicates, check, records, PAGE } = await import('../../../tools/doc-duplicates.mjs') as {
  duplicates: (text: string, kind: string) => string[];
  check: (root: string) => { file: string; fact: string; key: string }[];
  records: (root: string) => { file: string; kind: string }[];
  PAGE: string;
};

test('a decision number twice is a duplicate, a number cited in prose is not', () => {
  const text = '## D104 — one\n\nas D104 says\n\n## D105 — two\n\n## D104 — the same number again\n';
  assert.deepEqual(duplicates(text, 'decision'), ['D104']);
});

test('a heading twice is a duplicate; a deeper heading is not counted', () => {
  const text = '## USE1 — closed\n\n### detail\n\n## LOG2 — closed\n\n### detail\n\n## USE1 — closed\n';
  assert.deepEqual(duplicates(text, 'heading'), ['## USE1 — closed']);
});

test('a table row twice by its first cell is a duplicate, and the headers several tables share are not', () => {
  const text = [
    '| Document | Kind |', '|---|---|', '| `a.md` | contract |', '',
    '| Document | Kind |', '|:--|:-:|', '| `b.md` | evidence |', '| `a.md` | contract, amended |',
  ].join('\n');
  assert.deepEqual(duplicates(text, 'row'), ['`a.md`']);
});

test('a changelog line twice is a duplicate; a short or blank line is not', () => {
  const text = '- **A plugin remembers where it came from** (PLUG9 c).\n- ok\n- ok\n\n- **A plugin remembers where it came from** (PLUG9 c).\n';
  assert.deepEqual(duplicates(text, 'line'), ['- **A plugin remembers where it came from** (PLUG9 c).']);
});

test('the records the attributes mark union are exactly the ones checked', async () => {
  const { readFileSync } = await import('node:fs');
  const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
  const attributes = readFileSync(join(root, '.gitattributes'), 'utf8');
  const union = attributes.split('\n').filter((line) => /\smerge=union\s*$/.test(line)).map((line) => line.split(/\s+/)[0]);
  assert.deepEqual(union.sort(), records(root).map((record) => record.file).sort());
});

test("this repository's records hold nothing twice", () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
  assert.deepEqual(check(root), []);
});

// DOC8b: the folder's facts, each on a fixture folder.

/** One decision's file as the migration writes it: its heading first, its notes each after a blank line. */
const entry = (n: number, ...notes: string[]) =>
  [`## D${n} — the decision (2026-10-03)`, '', '**Decision.** It is decided.', '', '**Why.** Because.', ...notes.flatMap((note) => ['', note]), ''].join('\n');

/** The page `docs/DECISIONS.md` becomes (D134 §3.2): where the decisions are, and no rows. */
const page = [
  '# Decisions', '',
  'Each decision is its own file, `docs/decisions/D<n>.md`. D114 was never taken.', '',
  "`grep '^## D' docs/decisions/*.md` lists them all.", '',
].join('\n');

/** A repository whose manifest declares the decisions record as a folder, with the page beside it when given. */
function folder(name: string, files: Record<string, string>, pageText?: string) {
  const fx = makeFixture(`doc-duplicates-${name}`);
  fx.write('daoris.json', JSON.stringify({ documents: { decisions: 'docs/decisions' } }));
  for (const [file, text] of Object.entries(files)) fx.write(`docs/decisions/${file}`, text);
  if (pageText !== undefined) fx.write(PAGE, pageText);
  return fx;
}

const facts = (root: string) => check(root).map(({ file, fact }) => `${file}: ${fact}`).sort();

test('a folder of decisions passes when each file holds its own heading and its notes stand apart', () => {
  const fx = folder('folder-clean', {
    'D1.md': entry(1),
    'D2.md': entry(2, '**Built 2026-10-02 (ROW1): the build.** It landed.', '*Amended by D3 (ROW2, 2026-10-03): the change.*'),
    // A fence's lines are text: a heading or a label shown inside one is neither.
    'D3.md': entry(3) + '\n```\n## D9 — an example\ntext\n**Built 2026-10-02 (EX1).**\n```\n',
    // An emphasised sentence after a line is not a note's label.
    'D10.md': entry(10, 'A paragraph.\n**Proven without the rehearsal**: the checks.\n**Not run in the branch**: the rehearsal.'),
  }, page);
  assert.deepEqual(check(fx.root), []);
  fx.cleanup();
});

test('a file in the folder not named D<n>.md, its number unpadded, is refused', () => {
  const fx = folder('folder-names', {
    'D7.md': entry(7),
    'D007.md': entry(7),
    'd9.md': entry(9),
    'notes.md': entry(8),
    'README.txt': 'not a decision file, and not the union pattern\n',
  });
  assert.deepEqual(facts(fx.root), [
    'docs/decisions/D007.md: not named D<n>.md',
    'docs/decisions/d9.md: not named D<n>.md',
    'docs/decisions/notes.md: not named D<n>.md',
  ]);
  fx.cleanup();
});

test('each file holds exactly one decision heading outside a fence, and it is its own number', () => {
  const fx = folder('folder-headings', {
    'D1.md': 'A note with no heading at all.\n',
    // A criss-cross that kept both copies of one decision.
    'D2.md': entry(2) + '\n' + entry(2),
    'D3.md': entry(4),
    'D5.md': entry(5) + '\n' + entry(6),
    // Its only heading is inside a fence.
    'D8.md': '```\n## D8 — shown, not held\n```\n',
  });
  assert.deepEqual(facts(fx.root), [
    'docs/decisions/D1.md: no heading of its own',
    'docs/decisions/D2.md: its heading twice',
    'docs/decisions/D3.md: a heading not its own',
    'docs/decisions/D3.md: no heading of its own',
    'docs/decisions/D5.md: a heading not its own',
    'docs/decisions/D8.md: no heading of its own',
  ]);
  const twice = check(fx.root).find((found) => found.fact === 'its heading twice');
  assert.equal(twice?.key, '## D2 — the decision (2026-10-03)');
  fx.cleanup();
});

test('a conflict marker in a decision file is refused, wherever it stands', () => {
  const fx = folder('folder-markers', {
    'D1.md': entry(1) + '\n<<<<<<< ours\nOne side.\n=======\nThe other.\n>>>>>>> theirs\n',
    'D2.md': entry(2) + '\n```\n<<<<<<< HEAD\n```\n',
  });
  assert.deepEqual(check(fx.root).map(({ file, fact, key }) => `${file}: ${fact}: ${key}`).sort(), [
    'docs/decisions/D1.md: a conflict marker: <<<<<<< ours',
    'docs/decisions/D1.md: a conflict marker: >>>>>>> theirs',
    'docs/decisions/D2.md: a conflict marker: <<<<<<< HEAD',
  ]);
  fx.cleanup();
});

test("a note's label straight after a non-blank line is refused in each form the record writes; after a blank line it stands", () => {
  const labels = [
    '**Built 2026-10-02 (ROW1): the build.**',
    '**As built (ROW2, 2026-10-01): the rest.**',
    '**Fixed 2026-10-02 (FIX1).**',
    '**Read 2026-10-02 (READ1).**',
    '**Amended 2026-09-24 (AM1).**',
    '**Amended the same day, by an answer** (the owner).',
    '*Amended by D130 (ROW3, 2026-10-02): the change.*',
    "*As built (PLUGUI1d, 2026-10-01): the host's half.",
    '*Built by LAYOUT3 (2026-10-01): the frame.*',
    '*Noted by D115 (DEV4, 2026-10-01): the remote carries lanes.',
    '*Extended by D108 (TASKBAR1, 2026-09-30): the taskbar id.',
    '**Decided 2026-08-05, settling DEV1.**',
    '**Measured (KNOW3, 2026-10-02).**',
    '**DRIFT1a, built 2026-10-02: the words are kept.**',
  ];
  const files: Record<string, string> = {};
  labels.forEach((label, i) => { files[`D${i + 1}.md`] = `## D${i + 1} — glued\n\nA paragraph.\n${label} More.\n`; });
  files['D99.md'] = entry(99, ...labels);
  const fx = folder('folder-labels', files);
  assert.deepEqual(facts(fx.root), labels.map((_, i) => `docs/decisions/D${i + 1}.md: a note label after a non-blank line`).sort());
  assert.deepEqual(check(fx.root).map((found) => found.key), labels.map((label) => `${label} More.`));
  fx.cleanup();
});

// DUPNOTE1: union keeps both copies of a note two sides carried, each after its blank line, so nothing above sees it:
// D150 held PLUGTOOL1a's note twice, and the check passed.
test('a note held twice in one decision is refused, by its label; the same note under two decisions is not', () => {
  const note = '**PLUGTOOL1a, built 2026-10-04: a manifest declares its tools** (point 7). Both twins read it.';
  const other = '**UX6g, built 2026-10-04: a workspace has a page** (point 3).';
  const fx = folder('folder-note-twice', {
    // Each copy stands apart, as union leaves a note it kept from both sides: no label is glued.
    'D1.md': entry(1, note, other, `${note}\nThe same body again.`),
    // One note under two decisions is a citation's business, not this record's.
    'D2.md': entry(2, note),
    // A note shown in a fence is text, so a fenced copy of a label is not a second note.
    'D3.md': entry(3, other) + '\n```\n' + other + '\n```\n',
    // A note in each of the record's forms, held once.
    'D4.md': entry(4, '**Built 2026-10-02 (ROW1): the build.**', '*Amended by D3 (ROW2, 2026-10-03): the change.*'),
  });
  assert.deepEqual(check(fx.root).map(({ file, fact, key }) => `${file}: ${fact}: ${key}`), [
    `docs/decisions/D1.md: a note twice: ${note}`,
  ]);
  fx.cleanup();
});

test('the page the record leaves behind holds no decision and no note, so a late note is refused there', () => {
  const late = page + '\n## D2 — written into the page by a late branch\n\n**Built 2026-10-03 (LATE1): a note under an old decision.**\n';
  const fx = folder('folder-page', { 'D1.md': entry(1) }, late);
  assert.deepEqual(facts(fx.root), [
    'docs/DECISIONS.md: a decision in the page',
    'docs/DECISIONS.md: a note label in the page',
  ]);
  fx.cleanup();
});

test('the decisions record is the one daoris.json declares, a file or a folder, and it is the union record either way', () => {
  const asFolder = folder('records-folder', { 'D1.md': entry(1) });
  assert.deepEqual(records(asFolder.root).find((record) => record.file.includes('decision')), { file: 'docs/decisions/*.md', kind: 'decisions' });
  asFolder.write('daoris.json', JSON.stringify({ documents: { decisions: { path: 'docs/decisions/' } } }));
  assert.deepEqual(records(asFolder.root).find((record) => record.file.includes('decision')), { file: 'docs/decisions/*.md', kind: 'decisions' });
  asFolder.cleanup();

  const asFile = makeFixture('doc-duplicates-records-file');
  asFile.write('daoris.json', JSON.stringify({ documents: { decisions: 'docs/DECISIONS.md' } }));
  asFile.write('docs/DECISIONS.md', entry(1));
  assert.deepEqual(records(asFile.root).find((record) => record.file.includes('DECISIONS')), { file: 'docs/DECISIONS.md', kind: 'decision' });
  asFile.cleanup();

  const undeclared = makeFixture('doc-duplicates-records-none');
  assert.deepEqual(records(undeclared.root).find((record) => record.file.includes('DECISIONS')), { file: 'docs/DECISIONS.md', kind: 'decision' });
  undeclared.cleanup();
});

test("a decisions record still one file is checked as it was: a number twice, and none of the folder's facts", () => {
  const fx = makeFixture('doc-duplicates-one-file');
  fx.write('daoris.json', JSON.stringify({ documents: { decisions: 'docs/DECISIONS.md' } }));
  fx.write('docs/DECISIONS.md', '## D1 — a\n\nText.\n*As built (X, 2026-10-01): glued to the text.\n\n## D1 — the same number again\n');
  assert.deepEqual(check(fx.root), [{ file: 'docs/DECISIONS.md', fact: 'twice', key: 'D1' }]);
  fx.cleanup();
});
