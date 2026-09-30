import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

// MOD1 (D106): the append-only records merge by union, and union keeps both versions of a line two branches
// changed. `tools/doc-duplicates.mjs` refuses what that leaves: a decision number, a heading, an index row or
// a changelog line twice.
// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const { duplicates, check, RECORDS } = await import('../../../tools/doc-duplicates.mjs') as {
  duplicates: (text: string, kind: string) => string[];
  check: (root: string) => { file: string; key: string }[];
  RECORDS: { file: string; kind: string }[];
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
  assert.deepEqual(union.sort(), RECORDS.map((record) => record.file).sort());
});

test("this repository's records hold nothing twice", () => {
  const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
  assert.deepEqual(check(root), []);
});
