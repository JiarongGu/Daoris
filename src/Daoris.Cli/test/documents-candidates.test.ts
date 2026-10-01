// `init` and `analyze` name candidate records by role, from conventional names, and write none (DOC3;
// D122, the development documents design §2.7): declaring is the repository's act, done by its session.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rmSync } from 'node:fs';
import { join } from 'node:path';
import type { LayoutFixture } from './_layout.ts';
import { layoutFixture } from './_layout.ts';

// ---------------------------------------------------------------- init and analyze: candidates

/** A repository keeping records under the usual names, and some under unusual cases. */
function withRecords(tag: string): LayoutFixture {
  const fx = layoutFixture(tag);
  rmSync(join(fx.repoFx.root, 'daoris.json'));
  fx.repoFx.write('changelog.md', '# Changes\n');
  fx.repoFx.write('docs/DECISIONS.md', '# Decisions\n');
  fx.repoFx.write('docs/adr/0001-one.md', '# One\n');
  fx.repoFx.write('docs/FIX-LOG.md', '# Fixes\n');
  fx.repoFx.write('TODO.md', '# To do\n');
  fx.repoFx.write('docs/README.md', '# The documents\n');
  fx.repoFx.write('README.md', '# The project\n');
  return fx;
}

test('init names candidate records by role, and writes none of them', () => {
  const fx = withRecords('documents-init');

  const { code, out } = fx.cli('init');

  assert.equal(code, 0, out);
  assert.match(out, /records this repo seems to keep/);
  assert.match(out, /router\s+docs\/README\.md/);
  assert.match(out, /decisions\s+docs\/DECISIONS\.md/);
  assert.match(out, /decisions\s+docs\/adr/);
  assert.match(out, /backlog\s+TODO\.md/);
  assert.match(out, /fixes\s+docs\/FIX-LOG\.md/);
  assert.match(out, /changelog\s+changelog\.md/);
  assert.doesNotMatch(out, /router\s+README\.md/, "the project's own readme is not a router");
  assert.equal(JSON.parse(fx.repoFx.read('daoris.json')).documents, undefined, 'declaring is the repository\'s act');
  fx.cleanup();
});

test('init says nothing about records where there are none', () => {
  const fx = layoutFixture('documents-init-none');
  rmSync(join(fx.repoFx.root, 'daoris.json'));
  assert.doesNotMatch(fx.cli('init').out, /records this repo/);
  fx.cleanup();
});

test('analyze names the same candidates, in its report and on the page', () => {
  const fx = withRecords('documents-analyze');

  const json = JSON.parse(fx.cli('analyze', '--json').out);
  assert.deepEqual(json.documents, [
    { role: 'router', path: 'docs/README.md' },
    { role: 'decisions', path: 'docs/DECISIONS.md' },
    { role: 'decisions', path: 'docs/adr' },
    { role: 'backlog', path: 'TODO.md' },
    { role: 'fixes', path: 'docs/FIX-LOG.md' },
    { role: 'changelog', path: 'changelog.md' },
  ]);

  const text = fx.cli('analyze').out;
  assert.match(text, /records this repo seems to keep/);
  assert.match(text, /fixes\s+docs\/FIX-LOG\.md/);
  assert.equal(fx.repoFx.exists('daoris.json'), false);
  fx.cleanup();
});
