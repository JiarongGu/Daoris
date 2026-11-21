import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
// @ts-expect-error — untyped workspace tooling; see desktop-publish.test.ts
import { CLI_HOME, appPublishCommand, driverPublishCommand, gitDoorPublishCommand, launcherPublishCommand, layCli, pieceStartPublishCommand, commandPublishCommand } from '../../../tools/desktop-publish.mjs';
// @ts-expect-error — untyped workspace tooling; see above
import { HOSTS, STAMPED, publishCommand } from '../../../tools/service-publish.mjs';
// @ts-expect-error — untyped workspace tooling; see above
import { buildVersion, nextVersion, seenVersions } from '../../../tools/desktop-publish/version.mjs';
import { versionLine } from '../src/platform/build-stamp.ts';
import { makeFixture } from '../test/_fixture.ts';

/**
 * Every published build carries its own `0.x.y`: the next one above every version the install has carried, staged, run
 * or tried, so two publishes in a row say two versions, in the build's assemblies, the doctrine tool and a stage's manifest.
 */

function json(path: string, value: unknown) {
  mkdirSync(join(path, '..'), { recursive: true });
  writeFileSync(path, JSON.stringify(value));
}

function release(root: string, version: string) {
  json(join(root, 'canon', 'canon.json'), { version });
}

test('the next version is the patch above the highest the release and the install have carried', () => {
  assert.equal(nextVersion(['0.0.1']), '0.0.2');
  assert.equal(nextVersion(['0.0.1', '0.0.9', '0.0.4']), '0.0.10');
  assert.equal(nextVersion(['0.1.0', '0.0.40']), '0.1.1');
  // A build's own marks never decide: a prerelease or build metadata is not a version of its own.
  assert.equal(nextVersion(['0.0.3+abc1234', '0.0.2-rc.1']), '0.0.4');
  assert.equal(nextVersion(['not a version', '0.0.1', null, undefined]), '0.0.2');
});

test('an install says what it has carried: its doctrine tool, its stage, its last swap and the build that last ran', (t) => {
  const fx = makeFixture('publish-version-seen');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');
  assert.deepEqual(seenVersions(install), []);

  json(join(install, 'app', 'cli', 'package.json'), { name: 'daoris', version: '0.0.5' });
  json(join(install, 'update', 'staged', 'build.json'), { schema: 1, id: 'b', version: '0.0.6' });
  json(join(install, 'update', 'swap.json'), { phase: 'rolledBack', version: '0.0.7' });
  json(join(install, 'data', 'last-build.json'), { version: '0.0.8' });
  json(join(install, 'update', '.staging', 'build.json'), { version: '9.9.9' });

  assert.deepEqual([...seenVersions(install)].sort(), ['0.0.5', '0.0.6', '0.0.7', '0.0.8']);
});

test('two publishes in a row stamp two versions, each above the one before', async (t) => {
  const fx = makeFixture('publish-version-two');
  t.after(() => fx.cleanup());
  const repo = join(fx.root, 'repo');
  const install = join(fx.root, 'install');
  release(repo, '0.0.1');

  const stamped: string[] = [];
  for (const id of ['20261120T010000Z-aaaaaaaa', '20261120T020000Z-bbbbbbbb']) {
    const version = buildVersion(repo, install);
    stamped.push(version);
    // What the publish lays: the doctrine tool, stamped with the build.
    const source = join(fx.root, `built-${id}`);
    json(join(source, 'package.json'), { name: 'daoris', version: '0.0.1' });
    writeFileSync(join(source, 'daoris.mjs'), '');
    json(join(source, 'canon', 'canon.json'), { version: '0.0.1' });
    await layCli(source, install, {}, { id, commit: '1930be1', version });
  }

  assert.deepEqual(stamped, ['0.0.2', '0.0.3']);
  assert.equal(versionLine(join(install, ...CLI_HOME)), '0.0.3 (build 20261120T020000Z-bbbbbbbb, commit 1930be1)');
});

test('a release above the install moves the next build above the release', (t) => {
  const fx = makeFixture('publish-version-release');
  t.after(() => fx.cleanup());
  const repo = join(fx.root, 'repo');
  const install = join(fx.root, 'install');
  json(join(install, 'app', 'cli', 'package.json'), { name: 'daoris', version: '0.0.57' });
  release(repo, '0.1.0');
  assert.equal(buildVersion(repo, install), '0.1.1');
});

test('the doctrine tool an install carries says the build\'s version while its canon stays the release\'s', async (t) => {
  const fx = makeFixture('publish-version-canon');
  t.after(() => fx.cleanup());
  const install = join(fx.root, 'install');
  const source = join(fx.root, 'built');
  json(join(source, 'package.json'), { name: 'daoris', version: '0.4.2' });
  writeFileSync(join(source, 'daoris.mjs'), '');
  json(join(source, 'canon', 'canon.json'), { version: '0.4.2' });

  await layCli(source, install, {}, { id: 'x', commit: null, version: '0.4.9' });

  assert.equal(JSON.parse(readFileSync(join(install, ...CLI_HOME, 'package.json'), 'utf8')).version, '0.4.9');
  // The doctrine's own version is the canon's: a sync writes it into every file's header, so it moves only with a release.
  assert.equal(JSON.parse(readFileSync(join(install, ...CLI_HOME, 'canon', 'canon.json'), 'utf8')).version, '0.4.2');
});

test('every publish recipe stamps the build\'s version beside its commit, and none without one', () => {
  const folder = join('some', 'folder');
  const recipes = (version: string | undefined): string[] => [
    launcherPublishCommand(folder, null, version), appPublishCommand(folder, null, version), driverPublishCommand(folder, 'win-x64', null, version),
    gitDoorPublishCommand(folder, null, version), pieceStartPublishCommand(folder, null, version), commandPublishCommand(folder, null, version),
    ...HOSTS.map((host: unknown) => publishCommand(host, 'win-x64', folder, null, version)),
  ];
  for (const command of recipes('0.0.12')) {
    assert.equal(command.split(` ${STAMPED} `).length, 2, command);
    assert.equal(command.split(' -p:Version=0.0.12 ').length, 2, command);
  }
  for (const command of recipes(undefined)) assert.ok(!command.includes('-p:Version='), command);
});
