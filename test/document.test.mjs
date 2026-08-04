import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeHeader, stripHeader, parseFrontmatter } from '../src/document.mjs';

const FM = `---
name: sensitive-info
applies_when: writing tracked files or rewriting history
enforces: no machine paths, no private names, no tokens
---

Body text.
`;

test('makeHeader names the pack, source, and version', () => {
  const header = makeHeader('core', 'core/sensitive-info.md', '0.1.0');
  assert.match(header, /^<!-- daoris: core\/core\/sensitive-info\.md @ 0\.1\.0 /);
  assert.match(header, /daoris upstream/);
  assert.equal(header.includes('\n'), false);
});

test('stripHeader removes only a daoris header line', () => {
  const withHeader = `${makeHeader('core', 'core/a.md', '0.1.0')}\n${FM}`;
  assert.equal(stripHeader(withHeader), FM);
  assert.equal(stripHeader(FM), FM);
});

test('parseFrontmatter reads the three index fields', () => {
  const { meta, body } = parseFrontmatter(FM);
  assert.equal(meta.name, 'sensitive-info');
  assert.equal(meta.applies_when, 'writing tracked files or rewriting history');
  assert.equal(meta.enforces, 'no machine paths, no private names, no tokens');
  assert.equal(body, '\nBody text.\n');
});

test('a value containing a colon survives', () => {
  const { meta } = parseFrontmatter('---\nname: a\napplies_when: x: y\nenforces: z\n---\nb\n');
  assert.equal(meta.applies_when, 'x: y');
});

test('an absent or incomplete block yields meta null and the text unchanged', () => {
  assert.equal(parseFrontmatter('# Title\n').meta, null);
  assert.equal(parseFrontmatter('---\nname: a\n---\nb\n').meta, null);
  assert.equal(parseFrontmatter('---\nname: a\n').body, '---\nname: a\n');
});
