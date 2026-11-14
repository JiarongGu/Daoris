import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { CLI_HOME, layCli } from '../../../tools/desktop-publish.mjs';
import { BUILD_STAMP, versionLine } from '../src/platform/build-stamp.ts';
import { makeFixture } from '../test/_fixture.ts';

function built(at: string): string {
  const folder = join(at, 'built');
  mkdirSync(join(folder, 'canon'), { recursive: true });
  writeFileSync(join(folder, 'package.json'), JSON.stringify({ name: 'daoris', version: '0.4.2' }));
  writeFileSync(join(folder, 'daoris.mjs'), '');
  writeFileSync(join(folder, 'canon', 'canon.json'), JSON.stringify({ version: '0.4.2' }));
  return folder;
}

test('the doctrine tool an install carries is stamped with the build that laid it, so its --version says which', async (t) => {
  const fx = makeFixture('publish-cli-stamp');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');

  await layCli(built(fx.root), install, {}, { id: '20261111T010203Z-0a1b2c3d', commit: '1930be1' });

  assert.equal(versionLine(join(install, ...CLI_HOME)), '0.4.2 (build 20261111T010203Z-0a1b2c3d, commit 1930be1)');
});

test('laid with no build, the doctrine tool carries no stamp and says its version alone', async (t) => {
  const fx = makeFixture('publish-cli-unstamped');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');

  await layCli(built(fx.root), install);

  assert.equal(existsSync(join(install, ...CLI_HOME, BUILD_STAMP)), false);
  assert.equal(versionLine(join(install, ...CLI_HOME)), '0.4.2');
});
