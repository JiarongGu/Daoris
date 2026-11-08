// A rule whose own text holds a line the region reads as structure would be cut apart on its way back out.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { doc, layoutFixture } from '../_layout.ts';

test('a canon rule holding a provenance line of its own is a tool error before anything is written', () => {
  const fx = layoutFixture('marker-in-rule');
  fx.canonFx.write('core/rules/quoting.md', doc('quoting', 'The header reads:\n\n<!-- daoris: core/core/rules/x.md @ 0.1.0 — canonical -->\n\nand that is all.'));

  const sync = fx.cli('sync');

  assert.equal(sync.code, 2, sync.out);
  assert.match(sync.out, /core\/rules\/quoting\.md holds a line the region reads as its own structure/);
  assert.equal(fx.repoFx.exists('AGENTS.md'), false);
  assert.equal(fx.repoFx.exists('daoris.lock'), false);
  fx.cleanup();
});

test('damaged markers in the pointer file refuse the sync before anything is written', () => {
  const fx = layoutFixture('marker-pointer');
  fx.repoFx.write('CLAUDE.md', '# mine\n\n<!-- daoris:import — generated; edit the canon, not this -->\nsee the doctrine\n');

  const sync = fx.cli('sync');

  assert.equal(sync.code, 1, sync.out);
  assert.match(sync.out, /daoris:import/);
  assert.equal(fx.repoFx.exists('AGENTS.md'), false);
  assert.equal(fx.repoFx.exists('.claude/knowledge/storage.md'), false);
  assert.equal(fx.repoFx.exists('daoris.lock'), false);
  fx.cleanup();
});

test('a rule that quotes a marker mid-line, or indented, is prose and syncs clean', () => {
  const fx = layoutFixture('marker-quoted');
  fx.canonFx.write('core/rules/quoting.md', doc('quoting', 'The header reads `<!-- daoris: x -->` and\n    <!-- daoris: indented -->\nis prose.'));

  assert.equal(fx.cli('sync').code, 0);
  const checked = fx.cli('check');
  assert.equal(checked.code, 0, checked.out);
  fx.cleanup();
});
