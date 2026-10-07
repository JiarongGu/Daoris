import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { firstHeading, markdownFence } from '../src/document.ts';

// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const tools = await import('../../../tools/doc-duplicates.mjs') as { fenced: (text: string[]) => boolean[] };

/**
 * A fence read as CommonMark reads one (ORIENT2h6, D151's ORIENT2h3, ORIENT2h4 and ORIENT2h6 notes): a run of three or
 * more backticks or tildes after any indent opens it, a bare run of the same at least as long closes it, a backtick run
 * with a backtick later on its line is code inline, and a fence never closed holds the rest of the file. The heading
 * reader toggled on any three of either, so a four-backtick fence closed on the example it quoted and the example's
 * heading named the document.
 *
 * One TWIN table, `fixtures/fence-cases.json`, read row for row by the CLI's `markdownFence`, the tools' `fenced` and
 * the driver's `SelfDescription.Fence` (`SelfDescriptionTests`); the service's `MarkdownFence` is held to the tools'
 * `fenced` by `tools/orient-index-fixtures/decision-notes.json`.
 */

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..', '..');

type Case = { why: string; lines: string[]; fenced: number[] };
const CASES = (JSON.parse(readFileSync(join(here, 'fixtures', 'fence-cases.json'), 'utf8')) as { cases: Case[] }).cases;

/** The lines, from 1, a reader's answers say a fence holds. */
const held = (answers: readonly boolean[]) => answers.flatMap((inFence, at) => (inFence ? [at + 1] : []));

test('the CLI reads each case\'s fence as the table does', () => {
  for (const { why, lines, fenced } of CASES) {
    const holds = markdownFence();
    assert.deepEqual(held(lines.map((line) => holds(line))), fenced, why);
  }
  assert.ok(CASES.some((row) => row.fenced.length === 0) && CASES.some((row) => row.fenced.length > 0), 'the table holds both kinds');
});

test('the tools read each case\'s fence as the table does', () => {
  for (const { why, lines, fenced } of CASES) assert.deepEqual(held(tools.fenced(lines)), fenced, why);
});

test('the driver\'s twin reads the same table', () => {
  const twin = readFileSync(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'SelfDescriptionTests.cs'), 'utf8');
  assert.ok(/"fixtures",\s*"fence-cases\.json"/.test(twin), 'SelfDescriptionTests reads fixtures/fence-cases.json');
});

test('a document\'s first heading is never one a longer fence quotes', () => {
  const text = '````markdown\n```\n# Not this one\n```\n````\n\n# The heading\n';
  assert.equal(firstHeading(text), 'The heading');
});

test('a document\'s first heading is never one a tilde fence holds, past a backtick run inside it', () => {
  const text = '~~~\n```\n# Not this one\n~~~\n\n# The heading\n';
  assert.equal(firstHeading(text), 'The heading');
});

test('inline code at a line\'s start opens no fence, so the heading after it is read', () => {
  const text = '```a``` is code inline.\n\n# The heading\n';
  assert.equal(firstHeading(text), 'The heading');
});

test('below frontmatter, a fence quoting a frontmatter-shaped block and a heading holds both', () => {
  const text = '---\nname: x\napplies_when: w\nenforces: e\n---\n````yaml\n---\n# a comment\n```\n# Not this one\n````\n# The heading\n';
  assert.equal(firstHeading(text), 'The heading');
});
