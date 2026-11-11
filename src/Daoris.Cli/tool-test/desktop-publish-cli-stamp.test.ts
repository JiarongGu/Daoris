import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { CLI_PACKAGE, layCli } from '../../../tools/desktop-publish.mjs';
import { BUILD_STAMP, versionLine } from '../src/platform/build-stamp.ts';
import { makeFixture } from '../test/_fixture.ts';
import { TAR_END, tarEntry } from '../test/_tar.ts';

/** The release's package in miniature, packed as `npm pack` packs one: what `layCli` checks before it lays it. */
function packed(at: string): string {
  const files: Record<string, string> = {
    'package/package.json': JSON.stringify({ name: 'daoris', version: '0.4.2', bin: { daoris: 'bin/daoris.mjs' } }),
    'package/bin/daoris.mjs': '',
    'package/dist/cli.js': '',
    'package/canon/canon.json': JSON.stringify({ version: '0.4.2' }),
  };
  const tarball = join(at, 'daoris-0.4.2.tgz');
  writeFileSync(tarball, gzipSync(Buffer.concat([...Object.entries(files).map(([path, body]) => tarEntry(path, body)), TAR_END])));
  return tarball;
}

test('the doctrine tool an install carries is stamped with the build that laid it, so its --version says which', async (t) => {
  const fx = makeFixture('publish-cli-stamp');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');

  await layCli(packed(fx.root), install, {}, { id: '20261111T010203Z-0a1b2c3d', commit: '1930be1' });

  assert.equal(versionLine(join(install, ...CLI_PACKAGE)), '0.4.2 (build 20261111T010203Z-0a1b2c3d, commit 1930be1)');
});

test('laid with no build, the doctrine tool carries no stamp and says its version alone', async (t) => {
  const fx = makeFixture('publish-cli-unstamped');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');

  await layCli(packed(fx.root), install);

  assert.equal(existsSync(join(install, ...CLI_PACKAGE, BUILD_STAMP)), false);
  assert.equal(versionLine(join(install, ...CLI_PACKAGE)), '0.4.2');
});
