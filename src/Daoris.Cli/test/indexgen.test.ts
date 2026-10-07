import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture } from './_fixture.ts';
import { indexFromDisk, rosterFromDisk } from '../src/indexgen.ts';

const doc = (name: string) => `---
name: ${name}
applies_when: when ${name} applies
enforces: what ${name} enforces
---

Body.
`;

const skill = (name: string) => `---
name: ${name}
description: what ${name} is for
---

Steps.
`;

function seedRepo() {
  const fx = makeFixture('indexgen');
  fx.write(
    '.claude/rules/sensitive-info.md',
    `<!-- daoris: core/core/sensitive-info.md @ 0.1.0 -->\n${doc('sensitive-info')}`,
  );
  fx.write('.claude/rules/house-style.md', doc('house-style'));
  fx.write('.claude/knowledge/storage.md', doc('storage'));
  fx.write('.claude/knowledge/legacy.md', '# Legacy\n');
  fx.write('.claude/skills/doc-loader/SKILL.md', skill('doc-loader'));
  fx.write('.claude/skills/ef-migration/SKILL.md', skill('ef-migration'));
  return fx;
}

const LOCK = {
  entries: [
    { target: 'rules/sensitive-info.md', sha256: 'x' },
    { target: 'skills/doc-loader/SKILL.md', sha256: 'y' },
  ],
};

test('the index lists the on-demand tiers in separate tables', () => {
  const fx = seedRepo();
  const text = indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK });

  assert.match(text, /^## Knowledge$/m);
  assert.match(text, /^## Skills$/m);
  assert.match(text, /what storage enforces/);
  fx.cleanup();
});

/**
 * This table is what replaces a hand-written `skill-loader` skill. Its content
 * is "which skills does this repo have", which is generated, not doctrine (D14)
 * — and generating it is what lets a canonical workflow rule point at a roster
 * it cannot know in advance.
 */
test('the index lists skills by their entry file and description', () => {
  const fx = seedRepo();
  const text = indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK });
  // The path from the repository's root, as a code span: an agent opens it as written (D128 §2.3).
  assert.match(text, /^\| `\.claude\/skills\/doc-loader\/SKILL\.md` \| what doc-loader is for \|$/m);
  assert.match(text, /what ef-migration is for/);
  // A skill's supporting files are not skills.
  assert.equal(/\| \[/.test(text), false, 'named, never linked');
  fx.cleanup();
});

/**
 * A skill's `description` is the harness's TRIGGER text — long by design, because it has to match
 * against whatever a person asks. Copying it whole pays for it twice: once where the harness reads it,
 * once in every session that reads the index. Measured on the second adoption, the skills table was 46%
 * of an index that had become the largest always-loaded file in the repository.
 *
 * The index needs to say what each skill IS. What it triggers on stays in the skill.
 */
test('the index summarizes a skill rather than repeating its whole trigger', () => {
  const fx = seedRepo();
  const long = 'Load the documents a task needs before touching code. '
    + 'Use at the START of any non-trivial task, because on-demand documents are not auto-loaded '
    + 'and an unread match is a missing contract, which is the failure this exists to prevent.';
  fx.write('.claude/skills/verbose/SKILL.md', `---\nname: verbose\ndescription: ${long}\n---\n\nSteps.\n`);

  const row = indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK })
    .split('\n')
    .find((line) => line.includes('skills/verbose/'));

  assert.ok(row, 'the skill must still be listed');
  assert.match(row, /Load the documents a task needs before touching code/);
  assert.equal(row.includes('missing contract'), false, 'the trigger tail belongs in the skill');
  assert.ok(row.length < 200, `row was ${row.length} chars`);
  fx.cleanup();
});

test("a repo's own skill is marked local, a canonical one is not", () => {
  const fx = seedRepo();
  const text = indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /ef-migration.*\(local\)/);
  assert.equal(/doc-loader.*\(local\)/.test(text), false);
  fx.cleanup();
});

/**
 * A knowledge document, because that tier is still files. A repository's own always-loaded rules now
 * live in its instruction file outside the region (D59) — its own prose, which nothing enumerates.
 */
test('files not in the lock are marked local, and an old rules folder is not listed', () => {
  const fx = seedRepo();
  const text = indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /legacy.*\(local\)/);
  assert.equal(/sensitive-info|house-style/.test(text), false);
  fx.cleanup();
});

/** By its first heading, in a table of its own (WSSETUP14c, D128 §3.1); `index-headings.test.ts` has the cases. */
test('a file without frontmatter is listed by its heading, never dropped', () => {
  const fx = seedRepo();
  const text = indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /^## Knowledge without frontmatter$/m);
  assert.match(text, /^\| `\.claude\/knowledge\/legacy\.md` _\(local\)_ \| Legacy \|$/m);
  assert.doesNotMatch(text, /legacy.*needs frontmatter/);
  fx.cleanup();
});

test('output is deterministic', () => {
  const fx = seedRepo();
  const first = indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK });
  const second = indexFromDisk({ root: fx.root, target: '.claude', lock: LOCK });

  assert.equal(first, second);
  fx.cleanup();
});

/**
 * 🔴 The region's roster read from disk has NO rules rows and NO document rows. The rules' frontmatter
 * is stripped on the way in, so nothing offline can rebuild those rows; the documents are the index's
 * (WSSETUP14a, D128 §2.2). What is left is what `check` compares: the pointer, and what the manifest
 * declares.
 */
test('the region\'s disk half is the pointer and what the manifest declares, which is what keeps check offline', () => {
  const fx = seedRepo();
  const roster = rosterFromDisk({ root: fx.root, target: '.claude' });

  assert.match(roster, /## Read on demand/);
  assert.match(roster, /listed in \[\.claude\/INDEX\.md\]\(\.claude\/INDEX\.md\)/);
  // The rules table's header is there; its rows are not, because the canon is not.
  assert.equal(/sensitive-info|storage|doc-loader/.test(roster), false);
  fx.cleanup();
});
