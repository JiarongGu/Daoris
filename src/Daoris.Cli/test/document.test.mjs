import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeHeader, withHeader, stripHeader, parseFrontmatter } from '../src/document.mjs';

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
  const stamped = `${makeHeader('core', 'core/a.md', '0.1.0')}\n${FM}`;
  assert.equal(stripHeader(stamped), FM);
  assert.equal(stripHeader(FM), FM);
});

/**
 * The harness reads a document's frontmatter to decide whether to surface it at
 * all, and frontmatter is only frontmatter when it starts at the first byte. A
 * provenance comment above the opening fence silently turns a skill into an
 * unreachable one — so the header goes under the closing fence instead.
 */
test('withHeader puts the header under the frontmatter, never above it', () => {
  const header = makeHeader('core', 'core/skills/doc-loader/SKILL.md', '0.0.1');
  const stamped = withHeader(header, FM);
  assert.equal(stamped.startsWith('---\n'), true, 'frontmatter must start at byte 0');
  assert.match(stamped, /---\n<!-- daoris: /);
  assert.equal(parseFrontmatter(stamped).meta.name, 'sensitive-info');
});

test('withHeader falls back to the top when there is no frontmatter', () => {
  const header = makeHeader('core', 'core/a.md', '0.0.1');
  assert.equal(withHeader(header, '# Plain\n'), `${header}\n# Plain\n`);
});

test('a stamped document round-trips back to its canonical body', () => {
  const header = makeHeader('core', 'core/a.md', '0.0.1');
  for (const body of [FM, '# Plain\n', '---\nunclosed: true\n']) {
    assert.equal(stripHeader(withHeader(header, body)), body, `round-trip failed for: ${body}`);
  }
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
