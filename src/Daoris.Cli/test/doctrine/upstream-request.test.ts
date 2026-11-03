import { test } from 'node:test';
import assert from 'node:assert/strict';
import { makeFixture, type Fixture } from '../_fixture.ts';
import { commandSync } from '../../src/doctrine/materialize.ts';
import { commandUpstream } from '../../src/doctrine/upstream.ts';

/**
 * `daoris upstream` by where it runs (D230): outside the canon's own checkout it composes a request's words to the
 * repository that keeps the shared doctrine, offline, and writes nothing across; inside that checkout it writes the edit
 * into the canon as before.
 */

const doc = (name: string, body: string) => `---\nname: ${name}\napplies_when: w\nenforces: e\n---\n${body}\n`;

/** A canon at `canonAt` (a folder of its own, or `canon/` inside the repository), and a repository synced from it. */
function seeded(name: string, inside: boolean): { canon: Fixture | null; repo: Fixture; canonRoot: string; done(): void } {
  const repo = makeFixture(`${name}-repo`);
  const canon = inside ? null : makeFixture(`${name}-canon`);
  const canonRoot = inside ? `${repo.root}/canon` : canon!.root;
  const put = (rel: string, text: string) => (inside ? repo.write(`canon/${rel}`, text) : canon!.write(rel, text));
  put('canon.json', '{"version":"0.1.0"}');
  put('core/knowledge/notes.md', doc('notes', 'the canon line'));
  repo.write('daoris.json', '{"source":"daoris@0.1.0","packs":[]}');
  process.env.DAORIS_CANON = canonRoot;
  assert.equal(commandSync({ root: repo.root, argv: [], write: () => {}, packageRoot: '' }), 0);
  repo.write('.claude/knowledge/notes.md', repo.read('.claude/knowledge/notes.md').replace('the canon line', 'the canon line, made better here'));
  return {
    canon, repo, canonRoot,
    done() {
      delete process.env.DAORIS_CANON;
      canon?.cleanup();
      repo.cleanup();
    },
  };
}

function upstream(repo: Fixture, ...argv: string[]): { code: number; text: string } {
  const out: string[] = [];
  const code = commandUpstream({ root: repo.root, argv, write: (s: string) => out.push(s), packageRoot: '' });
  return { code, text: out.join('\n') };
}

test('outside the canon\'s checkout: the request\'s words to the canon\'s repository, and nothing written', () => {
  const fx = seeded('upstream-outside', false);
  const before = fx.canon!.read('core/knowledge/notes.md');

  const { code, text } = upstream(fx.repo, 'knowledge/notes.md');
  assert.equal(code, 0, text);
  assert.equal(fx.canon!.read('core/knowledge/notes.md'), before, 'nothing written across');
  assert.match(text, /^daoris: nothing was written; the request below is for the repository that keeps the shared doctrine \(daoris@0\.1\.0\)/m);
  assert.match(text, /^Take this repository's text of `core\/knowledge\/notes\.md` into the shared doctrine$/m);
  assert.match(text, /the canon line, made better here/);
  assert.doesNotMatch(text, /\bD\d+\b|§/);

  const all = upstream(fx.repo, '--all');
  assert.equal(all.code, 0, all.text);
  assert.match(all.text, /`core\/knowledge\/notes\.md`/);
  assert.equal(fx.canon!.read('core/knowledge/notes.md'), before);

  fx.done();
});

test('inside the canon\'s own checkout: the edit is written into the canon, as before', () => {
  const fx = seeded('upstream-inside', true);

  const { code, text } = upstream(fx.repo, 'knowledge/notes.md');
  assert.equal(code, 0, text);
  assert.match(fx.repo.read('canon/core/knowledge/notes.md'), /the canon line, made better here/);
  assert.match(text, /^daoris: knowledge\/notes\.md -> canon core\/knowledge\/notes\.md$/m);

  fx.done();
});
