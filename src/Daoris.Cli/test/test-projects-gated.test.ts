import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { existsSync, readdirSync } from 'node:fs';
import { readText } from '../src/platform/fsx.ts';

const repoRoot = dirname(dirname(dirname(dirname(fileURLToPath(import.meta.url)))));

/**
 * The service's gate is `dotnet test src/Daoris.Service`, which runs the SOLUTION in that folder (HTTP1), so a suite added beside
 * the others and left out of the `.slnx` builds, passes by hand, and is run by no gate. Every test project is reached by a declared
 * gate (named directly, or a member of the solution a gated folder holds), or is an adapter's suite, which its own development runs
 * (`adapters.ts release`, named by its manifest's `tests`). Found shallowly (`src/<tree>/<project>/<project>.csproj`) and by the test
 * SDK reference, which is what makes `dotnet test` run a project: the devkit's suite never says `IsTestProject`.
 */
test('every .NET test project is run by a declared gate or its adapter release', () => {
  const gates = JSON.parse(readText(join(repoRoot, 'daoris.gates.json'))) as { gates: { name: string; run: string }[] };
  const targets = gates.gates
    .map((gate) => /^dotnet test (\S+)/.exec(gate.run)?.[1]?.replace(/\/+$/, ''))
    .filter((target): target is string => target !== undefined);
  const directories = (path: string) =>
    readdirSync(join(repoRoot, path), { withFileTypes: true }).filter((entry) => entry.isDirectory()).map((entry) => entry.name);

  const projects = directories('src').flatMap((tree) => directories(`src/${tree}`)
    .map((project) => `src/${tree}/${project}/${project}.csproj`)
    .filter((file) => existsSync(join(repoRoot, file))
      && /<PackageReference\s+Include="Microsoft\.NET\.Test\.Sdk"/.test(readText(join(repoRoot, file)))));
  const released = directories('src/Daoris.Adapters')
    .map((name) => join(repoRoot, 'src/Daoris.Adapters', name, 'adapter.json'))
    .filter((file) => existsSync(file))
    .map((file) => (JSON.parse(readText(file)) as { tests: string }).tests);

  assert.ok(projects.length >= 5, `only ${projects.length} test projects were found — the search stopped reaching them`);
  assert.ok(projects.includes('src/Daoris.Service/Daoris.Service.Http.Tests/Daoris.Service.Http.Tests.csproj'),
    'the HTTP host suite is not found by this search — it has stopped asking about the project it was written for');
  for (const project of projects) {
    const folder = project.slice(0, project.lastIndexOf('/'));
    const reached = released.includes(folder) || targets.some((target) => {
      if (folder === target) return true;
      if (!project.startsWith(`${target}/`)) return false;
      const member = project.slice(target.length + 1);
      return readdirSync(join(repoRoot, target))
        .filter((name) => /\.slnx?$/.test(name))
        .some((solution) => readText(join(repoRoot, target, solution)).replace(/\\/g, '/').includes(member));
    });
    assert.ok(reached, `${project} is a test project no declared gate runs — add it to its folder's solution, or declare it`);
  }
});
