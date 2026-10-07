// A knowledge document without frontmatter, by its heading (WSSETUP14c; D128 §3.1–§3.2).
//
// The first real set-up met 166 of a repository's own documents with no frontmatter, and the index
// gave each a row reading *needs frontmatter* twice, which told a reader nothing. A heading says what
// a document is about, so the index lists such a document by its first heading in a table of its own,
// and `check` says how many once, never failing (D54). A skill keeps its warning: one without
// frontmatter never fires on any agent. Written before the code.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture } from './_fixture.ts';
import { doc, layoutFixture, skill } from './_layout.ts';
import { indexFromDisk } from '../src/indexgen.ts';
import { roomHeading } from '../src/rooms.ts';
import { HARNESSES } from '../src/harness.ts';

const INDEX = '.claude/INDEX.md';
const WITHOUT = '## Knowledge without frontmatter';
const LOCK = { entries: [{ target: 'knowledge/storage.md', sha256: 'x' }] };

/** The index of a repository holding these knowledge documents beside one canonical one. */
function indexOf(files: Record<string, string>, skills: Record<string, string> = {}): string {
  const fx = makeFixture('index-headings');
  fx.write('.claude/knowledge/storage.md', doc('storage'));
  for (const [name, text] of Object.entries(files)) fx.write(`.claude/knowledge/${name}`, text);
  for (const [name, text] of Object.entries(skills)) fx.write(`.claude/skills/${name}/SKILL.md`, text);
  try {
    return indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK });
  } finally {
    fx.cleanup();
  }
}

/** The lines of one `##` section of the index, its heading excluded. */
function section(index: string, heading: string): string[] {
  const lines = index.split('\n');
  const start = lines.indexOf(heading);
  if (start === -1) return [];
  const end = lines.findIndex((line, at) => at > start && line.startsWith('## '));
  return lines.slice(start + 1, end === -1 ? lines.length : end);
}

test('frontmatter: a described document is a row of the Knowledge table, and there is no heading table', () => {
  const index = indexOf({ 'ours.md': doc('ours') });
  assert.ok(section(index, '## Knowledge').includes('| `.claude/knowledge/ours.md` _(local)_ | w | e |'), index);
  assert.ok(!index.includes(WITHOUT), 'the table is there only when a document needs it');
  noWarningIn(index);
});

test('a heading only: listed in its own table by its path and first heading, after the described ones', () => {
  const index = indexOf({ 'legacy.md': '# Legacy storage notes\n\nHow the old store was laid out.\n\n## Detail\n' });
  const without = section(index, WITHOUT);
  assert.deepEqual(without.filter((line) => line.startsWith('|')), [
    '| Document | Its first heading |',
    '|---|---|',
    '| `.claude/knowledge/legacy.md` _(local)_ | Legacy storage notes |',
  ]);
  assert.ok(!section(index, '## Knowledge').some((line) => line.includes('legacy')), 'not a row of the described table');
  // After the described ones, before the skills (D128 §2.3).
  assert.ok(index.indexOf('## Knowledge\n') < index.indexOf(WITHOUT));
  assert.ok(index.indexOf(WITHOUT) < index.indexOf('## Skills'));
  noWarningIn(index);
});

test('neither frontmatter nor a heading: listed by its path, with a dash where the heading would be', () => {
  const index = indexOf({ 'scratch.md': 'Some notes with no heading at all.\n' });
  assert.ok(section(index, WITHOUT).includes('| `.claude/knowledge/scratch.md` _(local)_ | — |'), index);
  noWarningIn(index);
});

test('the first heading is any level, outside a fence, and kept whole in its cell', () => {
  const index = indexOf({
    'a.md': '```\n# not this one\n```\n\n## Second-level first | a pipe\n\n# Later\n',
    'b.md': '# Notes on C#\n',
    'c.md': '### Closed heading ###\n',
  });
  const without = section(index, WITHOUT);
  assert.ok(without.includes('| `.claude/knowledge/a.md` _(local)_ | Second-level first \\| a pipe |'), without.join('\n'));
  assert.ok(without.includes('| `.claude/knowledge/b.md` _(local)_ | Notes on C# |'), without.join('\n'));
  assert.ok(without.includes('| `.claude/knowledge/c.md` _(local)_ | Closed heading |'), without.join('\n'));
});

test('a document whose frontmatter lacks a field keeps its warning: it has frontmatter, and the field is the defect', () => {
  const index = indexOf({ 'half.md': '---\nname: half\napplies_when: w\n---\n\n# Half described\n' });
  assert.ok(section(index, '## Knowledge').includes(
    '| `.claude/knowledge/half.md` _(local)_ | ⚠ needs frontmatter | ⚠ needs frontmatter |',
  ), index);
  assert.ok(!index.includes(WITHOUT));
});

test('a skill without frontmatter keeps its warning in the index, since it never fires on any agent', () => {
  const index = indexOf({ 'legacy.md': '# Legacy\n' }, { bare: '# A bare skill\n\nSteps.\n', house: skill('house') });
  assert.ok(section(index, '## Skills').includes('| `.claude/skills/bare/SKILL.md` _(local)_ | ⚠ needs frontmatter |'), index);
  assert.ok(section(index, '## Skills').includes('| `.claude/skills/house/SKILL.md` _(local)_ | Use when house is wanted. |'));
  assert.ok(!section(index, WITHOUT).some((line) => line.includes('skills/')), 'the heading table is knowledge only');
});

test('check reports how many of the repository\'s own documents have none, once, and never fails on it', () => {
  const fx = layoutFixture('headings-check');
  fx.repoFx.write('.claude/knowledge/legacy.md', '# Legacy\n');
  fx.repoFx.write('.claude/knowledge/scratch.md', 'No heading.\n');
  fx.repoFx.write('.claude/knowledge/ours.md', doc('ours'));
  fx.sync();

  const { code, out } = fx.cli('check');
  assert.equal(code, 0, out);
  const lines = out.split('\n').filter((line) => line.includes('frontmatter'));
  assert.deepEqual(lines, [
    '  frontmatter  2 knowledge documents have none; the index lists them by their first heading — advisory',
  ]);
  assert.match(out, /daoris: clean/);
  fx.cleanup();
});

test('check says it in the singular for one, and says nothing when every document is described', () => {
  const fx = layoutFixture('headings-check-one');
  fx.repoFx.write('.claude/knowledge/ours.md', doc('ours'));
  fx.sync();
  assert.doesNotMatch(fx.cli('check').out, /frontmatter/);

  fx.repoFx.write('.claude/knowledge/legacy.md', '# Legacy\n');
  fx.sync();
  const { code, out } = fx.cli('check');
  assert.equal(code, 0, out);
  assert.match(out, /^ {2}frontmatter {2}1 knowledge document has none; the index lists it by its first heading — advisory$/m);
  fx.cleanup();
});

/**
 * A canonical document without frontmatter is a defect of the canon, held by the canon's own tests
 * (D128 §3.2), so the index lists it by its heading, unmarked, and `check`'s count leaves it out.
 */
test('a canonical document without frontmatter is listed by its heading and left out of check\'s count', () => {
  const fx = layoutFixture('headings-canonical');
  fx.canonFx.write('core/knowledge/bare.md', '# A bare canonical note\n\nBody.\n');
  fx.repoFx.write('.claude/knowledge/legacy.md', '# Legacy\n');
  fx.sync();

  const index = fx.repoFx.read(INDEX);
  assert.ok(section(index, WITHOUT).includes('| `.claude/knowledge/bare.md` | A bare canonical note |'), index);
  const { code, out } = fx.cli('check');
  assert.equal(code, 0, out);
  assert.match(out, /frontmatter {2}1 knowledge document has none/);
  fx.cleanup();
});

test('a room\'s row reads the same first heading, below any frontmatter', () => {
  const fx = makeFixture('headings-room');
  fx.write('area/AGENTS.md', '---\ntitle: x\n# a comment among the fields\n---\n\n# The area, in C#\n');
  try {
    assert.equal(roomHeading(fx.root, 'area', HARNESSES['claude-code']!), 'The area, in C#');
  } finally {
    fx.cleanup();
  }
});

/** No row anywhere in the index says a knowledge document lacks fields it never had. */
function noWarningIn(index: string): void {
  assert.ok(!section(index, '## Knowledge').some((line) => line.includes('needs frontmatter')), index);
}
