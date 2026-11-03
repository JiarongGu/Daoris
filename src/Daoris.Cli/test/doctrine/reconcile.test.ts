import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import { makeFixture, type Fixture } from '../_fixture.ts';
import { readCanon } from '../../src/doctrine/canon.ts';
import { canonFileDigest } from '../../src/doctrine/keep.ts';
import { commandSync } from '../../src/doctrine/materialize.ts';
import { commandReconcile } from '../../src/doctrine/reconcile.ts';
import { DaorisError } from '../../src/platform/errors.ts';

/**
 * `daoris reconcile` (D230): offline, a brief in the canon's words that lists only the differences — what a sync would
 * update or add, the copies edited here, a keep the canon moved past, and a set-up gap `check` reports — and asks the
 * session to adopt, keep with its reason, or propose upstream each, with the repository's checks kept green.
 */

const doc = (name: string, body = 'x') => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n${body}\n`;

function adopter(name: string): { canon: Fixture; repo: Fixture; done(): void } {
  const canon = makeFixture(`${name}-canon`);
  canon.write('canon.json', '{"version":"0.1.0"}');
  canon.write('core/rules/sensitive-info.md', doc('sensitive-info'));
  canon.write('core/knowledge/storage.md', doc('storage'));
  canon.write('core/knowledge/notes.md', doc('notes'));
  canon.write('core/knowledge/steady.md', doc('steady'));
  const repo = makeFixture(`${name}-repo`);
  repo.write('daoris.json', '{"source":"s","packs":[]}');
  process.env.DAORIS_CANON = canon.root;
  assert.equal(commandSync({ root: repo.root, argv: [], write: () => {}, packageRoot: '' }), 0);
  return {
    canon, repo,
    done() {
      delete process.env.DAORIS_CANON;
      canon.cleanup();
      repo.cleanup();
    },
  };
}

function reconcile(repo: Fixture): { code: number; text: string } {
  const out: string[] = [];
  try {
    const code = commandReconcile({ root: repo.root, argv: [], write: (s: string) => out.push(s), packageRoot: '' });
    return { code, text: out.join('\n') };
  } catch (error) {
    if (!(error instanceof DaorisError)) throw error;
    return { code: error.exitCode, text: error.message };
  }
}

test('the brief over an adopter lists only the differences, each with its decision, in the canon\'s words', () => {
  const fx = adopter('reconcile-brief');
  fx.canon.write('canon.json', '{"version":"0.2.0"}');
  fx.canon.write('core/knowledge/storage.md', doc('storage', 'REWORDED'));
  fx.canon.write('core/skills/co-working/SKILL.md', '---\nname: co-working\ndescription: d\n---\nsteps\n');
  fx.repo.write('.claude/knowledge/notes.md', `${fx.repo.read('.claude/knowledge/notes.md')}a local line\n`);
  fx.repo.write('AGENTS.md', `${fx.repo.read('AGENTS.md')}\n${'own words. '.repeat(3200)}\n`);

  const { code, text } = reconcile(fx.repo);
  assert.equal(code, 0, text);
  const canon = readCanon(fx.canon.root);
  const storage = canonFileDigest(canon, { source: 'core/knowledge/storage.md' });
  const notes = canonFileDigest(canon, { source: 'core/knowledge/notes.md' });

  assert.match(text, /^Reconcile this repository with the shared doctrine$/m);
  assert.match(text, /This repository was synced from canon 0\.1\.0, older than the canon 0\.2\.0 this tool carries/);
  assert.match(text, /^1\. `knowledge\/storage\.md` — the canon changed it, and the copy here is untouched\.$/m);
  assert.match(text, new RegExp(`^ {3}To keep it: "knowledge/storage\\.md": \\{ "reason": "<why>", "canon": "${storage}" \\}$`, 'm'));
  assert.match(text, /^\d\. `skills\/co-working\/SKILL\.md` — the canon added it, a core file, which is adopted\.$/m);
  assert.doesNotMatch(text, /"skills\/co-working\/SKILL\.md": \{/, 'a core file is never kept absent, so no keep line is offered');
  assert.match(text, /^\d\. `knowledge\/notes\.md` — edited here: this repository's copy differs from what the tool last wrote\.$/m);
  assert.match(text, new RegExp(`"knowledge/notes\\.md": \\{ "reason": "<why>", "canon": "${notes}" \\}`));
  assert.match(text, /^\d\. `AGENTS\.md` is \d+ bytes, and one agent reads 32768 bytes of a repository's instruction files/m);
  assert.doesNotMatch(text, /steady\.md|sensitive-info/, 'only the differences');

  assert.match(text, /\*\*Adopt\*\*/);
  assert.match(text, /\*\*Keep, with its reason\*\*/);
  assert.match(text, /\*\*Upstream\*\*/);
  assert.match(text, /`daoris sync`/);
  assert.match(text, /`daoris upstream <file>`/);
  assert.match(text, /before the first change, and again at the close/, 'the checks kept green');
  assert.doesNotMatch(text, /\bD\d+\b|§|codex/i, 'no decision number, no product');

  fx.done();
});

test('a keep the canon moved past comes back as an item, and one it still holds does not', () => {
  const fx = adopter('reconcile-keeps');
  fx.canon.write('core/knowledge/storage.md', doc('storage', 'SECOND'));
  const judged = canonFileDigest(readCanon(fx.canon.root), { source: 'core/knowledge/storage.md' });
  const manifest = JSON.parse(fx.repo.read('daoris.json'));
  fx.repo.write('daoris.json', JSON.stringify({ ...manifest, kept: { 'knowledge/storage.md': { reason: 'ours reads better', canon: judged } } }));

  const quiet = reconcile(fx.repo);
  assert.equal(quiet.code, 0);
  assert.match(quiet.text, /^daoris: nothing to reconcile/m);

  fx.canon.write('core/knowledge/storage.md', doc('storage', 'THIRD'));
  const { text } = reconcile(fx.repo);
  assert.match(text, /^1\. `knowledge\/storage\.md` — kept here \(ours reads better\), and the canon's file moved since it was kept\.$/m);

  fx.done();
});

test('equal versions over another canon: the brief says which is newer cannot be told', () => {
  const fx = adopter('reconcile-tie');
  fx.canon.write('core/knowledge/storage.md', doc('storage', 'REWORDED'));

  const { text } = reconcile(fx.repo);
  assert.match(text, /both are canon 0\.1\.0, and the two differ: which is newer cannot be told from here/);
  assert.match(text, /adopt an item only where the canon's text reads as the newer/);

  fx.done();
});

test('a repository that never synced has nothing to reconcile: its set-up comes first', () => {
  const fx = adopter('reconcile-unsynced');
  rmSync(join(fx.repo.root, 'daoris.lock'));
  const { code, text } = reconcile(fx.repo);
  assert.equal(code, 1);
  assert.match(text, /has not synced the doctrine/);

  fx.done();
});
