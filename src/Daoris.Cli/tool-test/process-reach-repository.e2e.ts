import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The Process census read over every source of this repository, and from a commit: a full scan of the tree, so an end-to-end spec. */

interface Half { project: string; settings: string }
interface CensusClass { full: string }
interface Census { classes: CensusClass[]; problems: string[] }

const reach = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/process-reach.mjs') as {
  readCs: (text: string) => { code: string; literal: string };
  settingsFilter: (xml: string) => string | null;
  halfOf: (run: string) => Half | null;
  censusOf: (half: Half, files: string[], read: (file: string) => string) => Census;
  repository: (root: string, commit?: string | null) => { files: string[]; read: (file: string) => string };
};

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

test("the census read from a commit finds the service half's classes as the working tree's does", () => {
  const gate = (JSON.parse(readFileSync(join(repoRoot, 'daoris.gates.json'), 'utf8')) as { gates: { name: string; run: string }[] }).gates
    .find((each) => each.name === 'service-process')!;
  const half = reach.halfOf(gate.run)!;
  const names = (source: { files: string[]; read: (file: string) => string }) => reach.censusOf(half, source.files, source.read).classes.map((each) => each.full);
  const now = names(reach.repository(repoRoot));
  assert.ok(now.length > 0, 'the working tree reads the half');
  assert.deepEqual(names(reach.repository(repoRoot, 'HEAD')), now, "a commit's census reads every source the half's project does");
});

test("this repository's census: every class carrying the Process trait is read into one half, each declared half reads, and no hand-written reach lacks its reason", () => {
  const { files, read } = reach.repository(repoRoot);
  const gates = (JSON.parse(readFileSync(join(repoRoot, 'daoris.gates.json'), 'utf8')) as { gates: { name: string; run: string }[] }).gates
    .filter((gate) => /process\.runsettings/.test(gate.run));
  assert.ok(gates.length >= 3, gates.map((gate) => gate.name).join(', '));
  const inHalf = new Map<string, string[]>();
  for (const gate of gates) {
    const half = reach.halfOf(gate.run);
    assert.ok(half, `${gate.name} names its project and settings`);
    const census = reach.censusOf(half, files, read);
    assert.deepEqual(census.problems, [], gate.name);
    assert.ok(census.classes.length > 0, `${gate.name} has classes`);
    for (const each of census.classes) inHalf.set(`${half.project} ${each.full}`, [...(inHalf.get(`${half.project} ${each.full}`) ?? []), gate.name]);
  }
  const filters = new Map(gates.map((gate) => [gate.name, reach.settingsFilter(readFileSync(join(repoRoot, reach.halfOf(gate.run)!.settings), 'utf8')) ?? '']));
  const legs = new Set([...filters].filter(([, filter]) => /FullyQualifiedName~/.test(filter)).map(([name]) => name));
  const twice = [...inHalf].filter(([, halves]) => halves.filter((half) => !legs.has(half)).length > 1);
  assert.deepEqual(twice, [], 'no class is in two halves of one project, but a leg running it once more, whose filter names it');
  for (const [key, halves] of inHalf) {
    for (const leg of halves.filter((half) => legs.has(half))) assert.ok(filters.get(leg)!.includes(key.split(/[.+ ]/).at(-1)!), `${leg} names ${key}`);
  }
  // Read the plain way: each class a Process trait stands before, in the test projects the halves run, by its own name (a
  // nested class's is the last part of the census's `Outer+Inner`).
  const projects = [...new Set(gates.map((gate) => reach.halfOf(gate.run)!.project))];
  const known = new Set([...inHalf.keys()].map((key) => key.replace(/ .*[.+]/, ' ')));
  const marked: string[] = [];
  for (const project of projects) {
    for (const file of files.filter((path) => path.startsWith(`${project}/`) && path.endsWith('.cs'))) {
      const { code } = reach.readCs(read(file));
      for (const match of code.matchAll(/\[Trait\(Category\.Name,\s*Category\.Process\)\]([^{;]*?)\bclass\s+(\w+)/g)) {
        if (!/\bstatic\b/.test(match[1] ?? '')) marked.push(`${project} ${match[2]}`);
      }
    }
  }
  assert.ok(marked.length > 100, `${marked.length} marked classes`);
  const missing = marked.filter((name) => !known.has(name));
  assert.deepEqual(missing, [], 'a class the census misses would never be kept, but it would be counted nowhere');
});

test("a change to the knowledge connector's tools reaches the driver tests whose goldens list them", () => {
  const plan = spawnSync(process.execPath, [join(repoRoot, 'tools', 'reached-checks.mjs'), '--plan', '--paths', 'src/Daoris.Service/Daoris.Service.Mcp/ToolClasses.cs'], {
    cwd: repoRoot, encoding: 'utf8', timeout: 600_000, windowsHide: true,
  });
  const driver = plan.stdout.split('\n').find((line) => /^\s+driver: dotnet test/.test(line)) ?? '';
  for (const name of ['AgentsTextTests', 'BrainstormPostureTests', 'SuperviseContainmentTests']) {
    assert.match(driver, new RegExp(`\\.${name}\\b`), `the driver's reached classes name ${name}: ${plan.stdout.slice(-600)}`);
  }
});
