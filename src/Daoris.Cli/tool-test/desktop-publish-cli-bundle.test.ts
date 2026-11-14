import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { CLI_BUNDLE, CLI_HOME, bundleCli, layCli } from '../../../tools/desktop-publish.mjs';
import { makeFixture } from '../test/_fixture.ts';

const workspace = join(import.meta.dirname, '..', '..', '..');

/** What a program started with no `PATH` entry at all sees: the install's tool is found by its own path, never by a lookup. */
function runBundle(install: string, args: string[], cwd: string) {
  const env: Record<string, string> = { SystemRoot: process.env.SystemRoot ?? '' };
  return spawnSync(process.execPath, [join(install, ...CLI_BUNDLE), ...args], { cwd, env, encoding: 'utf8', windowsHide: true });
}

test('the install carries the doctrine tool as one bundled file and its canon, which run from a fixture install with no PATH entry', async (t) => {
  const fx = makeFixture('publish-cli-bundle');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');
  mkdirSync(join(install, 'data'), { recursive: true });
  writeFileSync(join(install, 'INSTALLED.md'), '# Daoris — installed desktop\n');

  const built = join(fx.root, 'built');
  await bundleCli(workspace, built);
  const laid = await layCli(built, install, {}, { id: '20261114T010203Z-0a1b2c3d', commit: '1930be1' });

  assert.deepEqual(readdirSync(join(install, ...CLI_HOME)).sort(), ['build.json', 'canon', 'daoris.mjs', 'package.json']);

  const version = runBundle(install, ['--version'], fx.root);
  assert.equal(version.status, 0, version.stderr);
  assert.equal(version.stdout.trim(), `${laid.version} (build 20261114T010203Z-0a1b2c3d, commit 1930be1)`);

  // A doctrine command that reads the canon the bundle carries beside it.
  const repository = join(fx.root, 'repository');
  mkdirSync(repository, { recursive: true });
  const analyzed = runBundle(install, ['analyze', '--json'], repository);
  assert.equal(analyzed.status, 0, analyzed.stderr || analyzed.stdout);
  assert.ok(analyzed.stdout.length > 0);
  assert.ok(existsSync(join(install, ...CLI_HOME, 'canon', 'canon.json')));
  assert.equal(readFileSync(join(install, ...CLI_BUNDLE), 'utf8').includes('node_modules/daoris'), false);
});

test('a republish replaces an older layout whole: the npm-style app/cli/node_modules goes', async (t) => {
  const fx = makeFixture('publish-cli-bundle-old');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');
  mkdirSync(join(install, ...CLI_HOME, 'node_modules', 'daoris', 'dist'), { recursive: true });
  writeFileSync(join(install, ...CLI_HOME, 'node_modules', 'daoris', 'dist', 'cli.js'), '// the layout before\n');

  const built = join(fx.root, 'built');
  await bundleCli(workspace, built);
  await layCli(built, install);

  assert.equal(existsSync(join(install, ...CLI_HOME, 'node_modules')), false);
  assert.ok(existsSync(join(install, ...CLI_BUNDLE)));
});
