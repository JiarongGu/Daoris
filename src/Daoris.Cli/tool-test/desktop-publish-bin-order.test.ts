import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { CLI_BIN, COMMAND, GIT_DOORS, PIECE_START, layBinPrograms, layCli } from '../../../tools/desktop-publish.mjs';
import { makeFixture } from '../test/_fixture.ts';

/** The bundle folder in miniature, as `bundleCli` writes it. */
function built(at: string): string {
  const folder = join(at, 'built');
  mkdirSync(join(folder, 'canon'), { recursive: true });
  writeFileSync(join(folder, 'package.json'), JSON.stringify({ name: 'daoris', version: '0.4.2' }));
  writeFileSync(join(folder, 'daoris.mjs'), '');
  writeFileSync(join(folder, 'canon', 'canon.json'), JSON.stringify({ version: '0.4.2' }));
  return folder;
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

  await layCli(built(fx.root), install, {}, null);
  layBinPrograms(stages, install);

  const { existsSync } = await import('node:fs');
  const missing = [COMMAND, ...GIT_DOORS, PIECE_START].filter((name) => !existsSync(join(install, ...CLI_BIN, name)));
  assert.deepEqual(missing, []);
});
