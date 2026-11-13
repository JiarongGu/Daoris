import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { CLI_BIN, COMMAND, GIT_DOORS, PIECE_START, layBinPrograms, layCli } from '../../../tools/desktop-publish.mjs';
import { makeFixture } from '../test/_fixture.ts';
import { TAR_END, tarEntry } from '../test/_tar.ts';

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

test('every program of app/bin/ is laid after the doctrine tool, which empties the folder', async (t) => {
  const fx = makeFixture('publish-bin-order');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');
  const stages = join(fx.root, 'stages');
  for (const [folder, names] of [['command', [COMMAND]], ['git-doors', GIT_DOORS], ['piece-start', [PIECE_START]]] as const) {
    mkdirSync(join(stages, folder), { recursive: true });
    for (const name of names) writeFileSync(join(stages, folder, name), name);
  }

  await layCli(packed(fx.root), install, {}, null);
  layBinPrograms(stages, install);

  const { existsSync } = await import('node:fs');
  const missing = [COMMAND, ...GIT_DOORS, PIECE_START].filter((name) => !existsSync(join(install, ...CLI_BIN, name)));
  assert.deepEqual(missing, []);
});
