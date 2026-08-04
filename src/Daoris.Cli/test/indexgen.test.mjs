import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture } from './_fixture.mjs';
import { buildIndex, INDEX_PATH } from '../src/indexgen.mjs';

const doc = (name) => `---
name: ${name}
applies_when: when ${name} applies
enforces: what ${name} enforces
---

Body.
`;

const skill = (name) => `---
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

test('the index lists rules and knowledge in separate tables', () => {
  const fx = seedRepo();
  const text = buildIndex({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /## Core \(always loaded\)/);
  assert.match(text, /## Knowledge \(read on demand\)/);
  assert.match(text, /when sensitive-info applies/);
  assert.match(text, /what storage enforces/);
  fx.cleanup();
});

/**
 * This table is what replaces a hand-written `skill-loader` skill. Its content
 * is "which skills does this repo have", which is generated, not doctrine (D14)
 * — and generating it is what lets a canonical workflow rule point at a roster
 * it cannot know in advance.
 */
test('the index lists skills by directory name and description', () => {
  const fx = seedRepo();
  const text = buildIndex({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /## Skills \(invoke by name\)/);
  assert.match(text, /\[doc-loader\]\(\.\.\/skills\/doc-loader\/SKILL\.md\)/);
  assert.match(text, /what doc-loader is for/);
  assert.match(text, /what ef-migration is for/);
  // A skill's SKILL.md is an implementation detail; the directory is its name.
  assert.equal(/\| \[SKILL\]/.test(text), false);
  fx.cleanup();
});

/**
 * The index is always-loaded, and a skill's `description` is the harness's TRIGGER text — long by
 * design, because it has to match against whatever a person asks. Copying it whole into the index
 * pays for it twice: once where the harness reads it, once on every session that loads the index.
 * Measured on the second adoption, the skills table was 46% of an index that had become the largest
 * always-loaded file in the repository.
 *
 * The roster needs to say what each skill IS. What it triggers on stays in the skill.
 */
test('the index summarizes a skill rather than repeating its whole trigger', () => {
  const fx = seedRepo();
  const long = 'Load the documents a task needs before touching code. '
    + 'Use at the START of any non-trivial task, because on-demand documents are not auto-loaded '
    + 'and an unread match is a missing contract, which is the failure this exists to prevent.';
  fx.write('.claude/skills/verbose/SKILL.md', `---\nname: verbose\ndescription: ${long}\n---\n\nSteps.\n`);

  const row = buildIndex({ root: fx.root, target: '.claude', lock: LOCK })
    .split('\n')
    .find((line) => line.includes('[verbose]'));

  assert.ok(row, 'the skill must still be listed');
  assert.match(row, /Load the documents a task needs before touching code/);
  assert.equal(row.includes('missing contract'), false, 'the trigger tail belongs in the skill');
  assert.ok(row.length < 200, `row was ${row.length} chars`);
  fx.cleanup();
});

test("a repo's own skill is marked local, a canonical one is not", () => {
  const fx = seedRepo();
  const text = buildIndex({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /ef-migration.*\(local\)/);
  assert.equal(/doc-loader.*\(local\)/.test(text), false);
  fx.cleanup();
});

test('files not in the lock are marked local', () => {
  const fx = seedRepo();
  const text = buildIndex({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /house-style.*\(local\)/);
  assert.equal(/sensitive-info.*\(local\)/.test(text), false);
  fx.cleanup();
});

test('a file without frontmatter is listed with a warning, never dropped', () => {
  const fx = seedRepo();
  const text = buildIndex({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /legacy.*needs frontmatter/);
  fx.cleanup();
});

test('output is deterministic and excludes the index itself', () => {
  const fx = seedRepo();
  const first = buildIndex({ root: fx.root, target: '.claude', lock: LOCK });
  fx.write(`.claude/${INDEX_PATH}`, first);
  const second = buildIndex({ root: fx.root, target: '.claude', lock: LOCK });
  assert.equal(first, second);
  assert.equal(/\| \[RULES_INDEX\]/.test(second), false);
  fx.cleanup();
});
