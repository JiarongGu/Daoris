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

function seedRepo() {
  const fx = makeFixture('indexgen');
  fx.write(
    '.claude/rules/sensitive-info.md',
    `<!-- daoris: core/core/sensitive-info.md @ 0.1.0 -->\n${doc('sensitive-info')}`,
  );
  fx.write('.claude/rules/house-style.md', doc('house-style'));
  fx.write('.claude/knowledge/storage.md', doc('storage'));
  fx.write('.claude/knowledge/legacy.md', '# Legacy\n');
  return fx;
}

const LOCK = { entries: [{ target: 'rules/sensitive-info.md', sha256: 'x' }] };

test('the index lists rules and knowledge in separate tables', () => {
  const fx = seedRepo();
  const text = buildIndex({ root: fx.root, target: '.claude', lock: LOCK });
  assert.match(text, /## Core \(always loaded\)/);
  assert.match(text, /## Knowledge \(read on demand\)/);
  assert.match(text, /when sensitive-info applies/);
  assert.match(text, /what storage enforces/);
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
