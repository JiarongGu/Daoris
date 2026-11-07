// `status` between a manifest's flip and the sync that moves the files reads them where the lock says they are.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { doc, layoutFixture } from '../_layout.ts';

test('a pending move still lists an upstream change to a file at the old root', () => {
  const fx = layoutFixture('move-status');
  fx.sync();
  fx.flip('agents');
  fx.canonFx.write('core/knowledge/storage.md', doc('storage', 'Reworded upstream.'));

  const { code, out } = fx.cli('status', '--json');

  assert.equal(code, 0, out);
  assert.deepEqual(JSON.parse(out).update?.changed, ['knowledge/storage.md']);
  fx.cleanup();
});
