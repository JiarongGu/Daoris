// A move that would carry a kept file refuses, and the dry run, which says what a sync would do, refuses with it.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { layoutFixture } from '../_layout.ts';
import { canonFileDigest } from '../../src/doctrine/keep.ts';
import { readCanon } from '../../src/doctrine/canon.ts';

test('a move carrying a kept file: sync refuses, --force too, and --dry-run exits 1 saying why', () => {
  const fx = layoutFixture('move-kept');
  fx.sync();
  const digest = canonFileDigest(readCanon(fx.canonFx.root), { source: 'core/knowledge/storage.md' });
  fx.manifest({ kept: { 'knowledge/storage.md': { reason: 'our own text', canon: digest } } });
  fx.flip('agents');

  for (const argv of [['sync'], ['sync', '--force']]) {
    const refused = fx.cli(...argv);
    assert.equal(refused.code, 1);
    assert.match(refused.out, /would carry 1 kept file\(s\): knowledge\/storage\.md/);
  }
  const dry = fx.cli('sync', '--dry-run');
  assert.equal(dry.code, 1, dry.out);
  assert.match(dry.out, /KEPT MOVE\s+knowledge\/storage\.md/);
  fx.cleanup();
});
