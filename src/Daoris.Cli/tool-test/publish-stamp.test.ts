import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
// Untyped workspace tooling, suppressed at the one site — see desktop-tool.test.ts for why.
import {
  appPublishCommand, driverPublishCommand, gitDoorPublishCommand, launcherPublishCommand, pieceStartPublishCommand,
  // @ts-expect-error — untyped workspace tooling; see above
} from '../../../tools/desktop-publish.mjs';
// @ts-expect-error — untyped workspace tooling; see above
import { HOSTS, STAMPED, publishCommand } from '../../../tools/service-publish.mjs';

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

/**
 * A development build carries `<version>+dev` and no commit (the root `Directory.Build.props`), since a commit stamped there is a
 * compile input of every assembly and recompiled every project at every switch. Every publish asks for the stamp, so an installed
 * build, its stage's `build.json` and the rollback read `<version>+<commit>`.
 */
test('every publish stamps its commit, told an output of its own or not', () => {
  assert.equal(STAMPED, '-p:DaorisStamp=true');
  const folder = join(tmpdir(), 'an-install', 'x');
  for (const artifacts of [null, join(tmpdir(), 'an-output')]) {
    const recipes: string[] = [
      launcherPublishCommand(folder, artifacts), appPublishCommand(folder, artifacts), driverPublishCommand(folder, 'win-x64', artifacts),
      gitDoorPublishCommand(folder, artifacts), pieceStartPublishCommand(folder, artifacts),
      ...HOSTS.map((host: unknown) => publishCommand(host, 'win-x64', folder, artifacts)),
    ];
    for (const command of recipes) assert.equal(command.split(` ${STAMPED} `).length, 2, command);
  }
});

/** The stamp is decided once, at the root: each tree's own build settings import it, so no tree stamps its commit in development. */
test("each tree's build settings import the root's, which stamps the commit only when told", () => {
  const root = readFileSync(join(repoRoot, 'Directory.Build.props'), 'utf8');
  assert.match(root, /<PropertyGroup Condition="'\$\(DaorisStamp\)' != 'true'">/);
  for (const property of ['<EnableSourceControlManagerQueries>false<', '<EnableSourceLink>false<', '<SourceRevisionId>dev<']) {
    assert.ok(root.includes(property), `the root's settings no longer set ${property}`);
  }
  const trees = readdirSync(join(repoRoot, 'src'), { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => join(repoRoot, 'src', entry.name, 'Directory.Build.props'))
    .filter((path) => { try { readFileSync(path); return true; } catch { return false; } });
  assert.ok(trees.length >= 3, `only ${trees.length} trees have build settings: this test has stopped finding them`);
  for (const path of trees) {
    assert.match(readFileSync(path, 'utf8'), /<Import Project="\$\(MSBuildThisFileDirectory\)\.\.\\\.\.\\Directory\.Build\.props" \/>/, path);
  }
});
