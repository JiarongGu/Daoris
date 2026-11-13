import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repoRoot, contracts } from '../support/merge-branch.ts';

// test-map: strings name paths as data

test("the web gates write the page's types with a generator built into an output of its own", () => {
  // Each web gate's pre-script runs the generator, which references the contracts and the desktop modules: built in their own
  // folders it would write the contracts' obj/ while the service's or the desktop's gate builds them there.
  const webScripts = (JSON.parse(readFileSync(join(repoRoot, 'src', 'Daoris.Web', 'package.json'), 'utf8')) as { scripts: Record<string, string> }).scripts;
  for (const pre of ['pretypecheck', 'pretest', 'prebuild']) assert.match(webScripts[pre]!, /^node \.\.\/\.\.\/tools\/contracts\.mjs --if-stale$/, pre);
  const { build, built } = contracts.generatorBuild(join(repoRoot, contracts.OWN_OUTPUT));
  assert.deepEqual(build.slice(0, 4), ['build', 'tools/contracts', '--artifacts-path', join(repoRoot, '_fixtures', 'contracts-build')]);
  assert.ok(built.startsWith(join(repoRoot, '_fixtures', 'contracts-build')), built);
  const text = readFileSync(join(repoRoot, 'tools', 'contracts.mjs'), 'utf8');
  assert.doesNotMatch(text, /'run',\s*'--project'/, 'the generator is run from its own build, never by `dotnet run` in its project');
});
