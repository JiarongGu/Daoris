import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { driverFile } from '../test/_csharp.ts';
import { makeFixture } from '../test/_fixture.ts';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { BUILD_MANIFEST, NOT_NOW, STAGED, UPDATE_REQUEST, holdNotNow, promoteStage } from '../../../tools/desktop-publish.mjs';

/**
 * `--stage --not-now`: the build staged held from the start, in one act. The person's word, as `daoris update --cancel`
 * writes it for that build, is in `data/update.json` before the stage is visible to the running desktop, so its idle install can
 * never race a later cancel.
 */

function install(t: { after: (done: () => void) => void }, name: string) {
  const fx = makeFixture(`publish-not-now-${name}`);
  t.after(fx.cleanup);
  const staging = join(fx.root, 'update', '.staging');
  mkdirSync(join(staging, 'app'), { recursive: true });
  writeFileSync(join(staging, 'app', 'Daoris.Desktop.exe'), 'new');
  writeFileSync(join(staging, BUILD_MANIFEST), '{}\n');
  return { at: fx.root, staging };
}

const word = (at: string) => JSON.parse(readFileSync(join(at, ...UPDATE_REQUEST), 'utf8'));

test('a stage held not now writes the word for its build before the stage is visible', (t) => {
  const { at, staging } = install(t, 'rename');
  const seen: Array<{ mode: string; build: string; staged: boolean }> = [];

  promoteStage(staging, at, {
    notNow: 'b2',
    rename: (from: string, to: string) => {
      // The moment the stage becomes visible: the word is already there, and nothing is staged yet.
      seen.push({ mode: word(at).mode, build: word(at).build, staged: existsSync(join(at, ...STAGED, BUILD_MANIFEST)) });
      renameSync(from, to);
    },
  });

  assert.deepEqual(seen, [{ mode: NOT_NOW, build: 'b2', staged: false }]);
  assert.ok(existsSync(join(at, ...STAGED, BUILD_MANIFEST)));
});

test('a stage copied into place, its rename refused, writes the word before the first file', (t) => {
  const { at, staging } = install(t, 'copied');
  const before: Array<string | null> = [];
  const refused = () => {
    throw Object.assign(new Error('held'), { code: 'EPERM' });
  };

  promoteStage(staging, at, {
    notNow: 'b3',
    rename: refused,
    tries: 1,
    waitMs: 0,
    copied: () => before.push(existsSync(join(at, ...UPDATE_REQUEST)) ? word(at).build : null),
  });

  assert.ok(before.length > 0);
  assert.ok(before.every((build) => build === 'b3'), 'every file of the stage lands after the word');
});

test('the word is the one `daoris update --cancel` writes for the build: its mode, build and moment, LF', (t) => {
  const { at } = install(t, 'shape');
  holdNotNow(at, 'b4', new Date('2026-11-16T03:53:00.250Z'));

  const text = readFileSync(join(at, ...UPDATE_REQUEST), 'utf8');
  assert.deepEqual(JSON.parse(text), { mode: 'not-now', build: 'b4', at: '2026-11-16T03:53:00Z' });
  assert.ok(text.endsWith('}\n') && !text.includes('\r'));
  // The spellings the driver reads: `UpdateMode.NotNow` and the home's `update` entry.
  assert.match(readFileSync(driverFile('StagedBuild.cs'), 'utf8'), new RegExp(`NotNow = "${NOT_NOW}"`));
  assert.match(readFileSync(driverFile('DaorisHome.cs'), 'utf8'), new RegExp(`"${UPDATE_REQUEST.join('/')}"`));
});

test('a stage with no not-now writes no word, and leaves the person\'s word as it was', (t) => {
  const { at, staging } = install(t, 'none');
  mkdirSync(join(at, UPDATE_REQUEST[0]), { recursive: true });
  writeFileSync(join(at, ...UPDATE_REQUEST), '{"mode":"when-idle","build":"b1","at":"2026-11-16T03:00:00Z"}\n');

  promoteStage(staging, at);

  assert.equal(word(at).build, 'b1');
});
