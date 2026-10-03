import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, symlinkSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { makeFixture } from './_fixture.ts';

/**
 * The tools' runner guard (REV3 tools F9, C8). A `tools/` script whose helpers are imported runs only
 * when it is the script node was asked to run. Node resolves the main module's links, so a script
 * reached through a junction saw `import.meta.url` as its REAL path and `process.argv[1]` as the path
 * typed: the guard was false, and a gate ran nothing and exited 0 — a pass nobody earned.
 */
const fsx = pathToFileURL(join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'tools', 'fsx.mjs')).href;

test('a tool reached through a junction still knows it is the one being run', () => {
  const fx = makeFixture('tools-main-junction');
  const real = join(fx.root, 'real');
  mkdirSync(real, { recursive: true });
  writeFileSync(join(real, 'probe.mjs'),
    `import { isMain } from ${JSON.stringify(fsx)};\nconsole.log(isMain(import.meta.url) ? 'ran' : 'skipped');\n`);
  const linked = join(fx.root, 'linked');
  symlinkSync(real, linked, 'junction');

  const direct = spawnSync(process.execPath, [join(real, 'probe.mjs')], { encoding: 'utf8' });
  const through = spawnSync(process.execPath, [join(linked, 'probe.mjs')], { encoding: 'utf8' });

  assert.equal(direct.stdout.trim(), 'ran', direct.stderr);
  assert.equal(through.stdout.trim(), 'ran', through.stderr);
  fx.cleanup();
});

test('an imported tool is not the one being run', async () => {
  const { isMain } = await import(fsx) as { isMain: (url: string) => boolean };
  assert.equal(isMain(fsx), false);
});

/**
 * How the family rehearsal asks whether the examples are current (REV3 tools F14): sync a COPY and
 * compare. Synced in place, the first run repaired the examples it had just found stale, and every
 * later run passed on the repair.
 */
test('two trees disagree on changed bytes and on a file either side lacks, and on nothing else', async () => {
  const { treeDiff } = await import(fsx) as { treeDiff: (left: string, right: string) => string[] };
  const fx = makeFixture('tools-tree-diff');
  const tree = (name: string, files: Record<string, string>) => {
    for (const [path, text] of Object.entries(files)) {
      mkdirSync(dirname(join(fx.root, name, path)), { recursive: true });
      writeFileSync(join(fx.root, name, path), text);
    }
    return join(fx.root, name);
  };
  const left = tree('left', { 'same.md': 'a', 'deep/edited.md': 'old', 'gone.md': 'x' });
  const right = tree('right', { 'same.md': 'a', 'deep/edited.md': 'new', 'added.md': 'y' });

  assert.deepEqual(treeDiff(left, right), ['added.md', 'deep/edited.md', 'gone.md']);
  assert.deepEqual(treeDiff(left, left), []);
  fx.cleanup();
});

/**
 * A folder renamed just after it was written (UPDATE1's stage, 2026-10-03): on Windows the rename of a folder of fresh
 * executables fails with EPERM while something still holds a file in it, the virus scanner most often, and gives way a
 * moment later. The deployment rehearsal's update phase failed on exactly that.
 */
type Rename = (from: string, to: string) => void;
type RenameHeld = (from: string, to: string, options?: { tries?: number; waitMs?: number; rename?: Rename }) => void;
const held = (code: string) => Object.assign(new Error(`${code}: operation not permitted, rename`), { code });

test('a rename a held file refuses is tried again until it gives way', async () => {
  const { renameHeld } = await import(fsx) as { renameHeld: RenameHeld };
  const calls: string[] = [];
  let refusals = 2;
  renameHeld('a', 'b', {
    waitMs: 1,
    rename: (from, to) => {
      calls.push(`${from}->${to}`);
      if (refusals-- > 0) throw held('EPERM');
    },
  });
  assert.deepEqual(calls, ['a->b', 'a->b', 'a->b']);
});

test('a rename still refused after its tries throws the last refusal', async () => {
  const { renameHeld } = await import(fsx) as { renameHeld: RenameHeld };
  let calls = 0;
  assert.throws(
    () => renameHeld('a', 'b', { tries: 3, waitMs: 1, rename: () => { calls++; throw held('EBUSY'); } }),
    (error: NodeJS.ErrnoException) => error.code === 'EBUSY',
  );
  assert.equal(calls, 3);
});

test('a rename that fails for any other reason fails at once', async () => {
  const { renameHeld } = await import(fsx) as { renameHeld: RenameHeld };
  let calls = 0;
  assert.throws(
    () => renameHeld('a', 'b', { waitMs: 1, rename: () => { calls++; throw held('ENOENT'); } }),
    (error: NodeJS.ErrnoException) => error.code === 'ENOENT',
  );
  assert.equal(calls, 1);
});
