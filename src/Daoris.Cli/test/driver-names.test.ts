import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { DESKTOP, desktopFile, driverFile, driverTestFile, oneNamed, projectFiles } from './_csharp.ts';

/**
 * Every reader of the driver's files finds each by name, wherever it lies under its project (STRUCT1, D166; the
 * code-structure design §6), so the driver's files move into module folders, one module per branch, and no reader is
 * edited for it. Held two ways over every reader in the repository: the driver's and the modules' C# tests, the CLI's
 * tests, the page's tests and the tools. Each name a reader reads by is one file under its project, which this suite,
 * run at every merge, proves for the readers no fast gate runs (a Process class, a rehearsal's helper); and no reader
 * joins a fixed folder to a file of the driver's projects, which a move would break.
 */

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const DRIVER = 'Daoris.Desktop.Driver';
const DRIVER_TESTS = 'Daoris.Desktop.Driver.Tests';

test('a name found once is that file wherever it lies, and one found nowhere or twice is refused (STRUCT1)', () => {
  assert.equal(oneNamed(DRIVER, 'Landing.cs', ['driver/Trees/Landing.cs']), 'driver/Trees/Landing.cs');
  assert.throws(() => oneNamed(DRIVER, 'Gone.cs', []),
    { message: 'no Gone.cs under Daoris.Desktop.Driver: a source read by name must be there, at any depth' });
  assert.throws(() => oneNamed(DRIVER, 'Hooks.cs', ['driver/Plugins/Hooks.cs', 'driver/Trees/Hooks.cs']),
    { message: 'Hooks.cs is under Daoris.Desktop.Driver 2 times, so its name does not say which: driver/Plugins/Hooks.cs, driver/Trees/Hooks.cs' });
});

test("the driver's sources and tests are found at any depth, and the build's output is none of them (STRUCT1)", () => {
  assert.equal(driverFile('NoteCodes.cs'), join(DESKTOP, DRIVER, 'Platform', 'NoteCodes.cs'));
  assert.equal(driverFile('HelpGoProposals.cs'), join(DESKTOP, DRIVER, 'Help', 'Proposals', 'HelpGoProposals.cs'));
  assert.equal(driverTestFile('StructureTests.cs'), join(DESKTOP, DRIVER_TESTS, 'StructureTests.cs'));
  assert.equal(desktopFile('Daoris.Desktop.Modules', 'DriverLoop.cs'), join(DESKTOP, 'Daoris.Desktop.Modules', 'Loop', 'DriverLoop.cs'));
  for (const project of [DRIVER, DRIVER_TESTS]) {
    const outside = projectFiles(project).filter((path) => /^(?:bin|obj)$/.test(relative(join(DESKTOP, project), path).split(/[\\/]/)[0]!));
    assert.deepEqual(outside, [], `${project}: the build's output is no source`);
  }
});

/** Every file of the repository a reader may be: tracked, or new and not ignored. */
const repositoryFiles = (): string[] =>
  execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard'], { cwd: repoRoot, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 })
    .split('\n').filter(Boolean);

/** What reads the driver's files: the desktop's C# tests, the CLI's tests, the page's sources and the tools. */
const isReader = (path: string): boolean =>
  (/^src\/Daoris\.Desktop\/[^/]+\.Tests\/.+\.cs$/.test(path))
  || (/^src\/Daoris\.Service\/Daoris\.Service\.Tests\/.+\.cs$/.test(path))
  || (/^src\/Daoris\.Cli\/test\/.+\.ts$/.test(path))
  || (/^src\/Daoris\.Web\/src\/.+\.tsx?$/.test(path))
  || (/^tools\/[^/]+\.mjs$/.test(path));

/**
 * The cases about readers, not reads: this file's, the merge tool's scan of what each suite reads, and the page finder's own
 * test, which asks for a name no file has.
 */
const SCANNERS = new Set([
  'src/Daoris.Cli/test/driver-names.test.ts', 'src/Daoris.Cli/tool-test/merge-branch.test.ts', 'src/Daoris.Web/src/test/driverSource.test.ts',
  // LIVELOGIN1's census plants a tree of its own in scratch, a driver file among it, and reads nothing of the repository's.
  'tools/live-logins.test.mjs',
]);

/** Each way a reader reads a desktop source by name, and the project it looks in when the call does not name one. */
const BY_NAME: readonly { call: RegExp; project?: string }[] = [
  { call: /\bSourceFiles\.Driver\(\s*"([^"\\]+)"\s*\)/g, project: DRIVER },
  { call: /\bSourceFiles\.Module\(\s*"([^"\\]+)"\s*\)/g, project: 'Daoris.Desktop.Modules' },
  { call: /\bSourceFiles\.Find\(\s*"([^"\\]+)"\s*,\s*"([^"\\]+)"\s*\)/g },
  { call: /\bdriverFile\(\s*'([^'\\]+)'\s*\)/g, project: DRIVER },
  { call: /\bdriverTestFile\(\s*'([^'\\]+)'\s*\)/g, project: DRIVER_TESTS },
  { call: /\bdesktopFile\(\s*'([^'\\]+)'\s*,\s*'([^'\\]+)'\s*\)/g },
  { call: /\bdriverSource\(\s*'([^'\\]+)'\s*\)/g, project: DRIVER },
  { call: /\bdriverFileIn\(\s*\w+\s*,\s*'([^'\\]+)'\s*\)/g, project: DRIVER },
];

/** Each read by name in a reader's text: the project and the name. */
function namedReads(text: string): [string, string][] {
  return BY_NAME.flatMap(({ call, project }) => [...text.matchAll(call)]
    .map((match): [string, string] => (project ? [project, match[1]!] : [match[1]!, match[2]!])));
}

test('each name a reader reads a desktop source by is one file under its project (STRUCT1)', () => {
  const readers = repositoryFiles().filter((path) => isReader(path) && !SCANNERS.has(path));
  const problems: string[] = [];
  const names = new Set<string>();
  for (const reader of readers) {
    for (const [project, name] of namedReads(readFileSync(join(repoRoot, reader), 'utf8'))) {
      names.add(`${project}/${name}`);
      try {
        desktopFile(project, name);
      } catch (error) {
        problems.push(`${reader}: ${(error as Error).message}`);
      }
    }
  }
  assert.deepEqual(problems, [], 'each a name that does not name one file');
  assert.ok(names.size >= 40, `the scan found only ${names.size} names: its patterns no longer match how the readers read by name`);
  // PluginIconTests.cs left the list with the CLI's plugins.ts, its last reader (PUSH1f2f).
  for (const read of [`${DRIVER}/NoteCodes.cs`, `${DRIVER}/Landing.cs`, `${DRIVER}/DriverCommand.cs`]) {
    assert.ok(names.has(read), `the scan does not see ${read} read by name`);
  }
});

/** Files of the driver's projects a reader may name by a fixed path, and why: each stays at its project's top. */
const FIXED = new Map([
  ['resources.json', "a resource, not a source: the driver's project file copies it from the driver's top, and desktop-publish.test.ts holds the two together"],
  ['Daoris.Desktop.Driver.csproj', "the driver's project file, at its project's top"],
]);

/**
 * Readers that still join a fixed folder to a driver file, each owed with its reason. A reader fixed is a row this test
 * refuses until it is dropped.
 */
const OWED: string[] = [];

/**
 * The files of the driver's projects a reader names by a fixed folder: a run of literals ending in the project and a file
 * (`'Daoris.Desktop.Driver', 'NoteCodes.cs'`, or the same in C#), a local holding the project's folder joined to a file
 * (`join(DRIVER, 'AccountReads.cs')`), or, in the page and the tools, a path ending in a file directly under the project
 * (`'src/Daoris.Desktop/Daoris.Desktop.Driver/NoteCodes.cs'`).
 */
function fixedReads(path: string, text: string): string[] {
  const project = String.raw`Daoris\.Desktop\.Driver(?:\.Tests)?`;
  // A file by its kind, so a project list (`"Daoris.Desktop.Driver", "Daoris.Desktop.Driver.Host"`) names none.
  const file = String.raw`([^'"\\/\s]+\.(?:cs|csproj|json|mjs|js|ts|template|md|txt))`;
  const names = [...text.matchAll(new RegExp(String.raw`(['"])${project}\1\s*,\s*(['"])${file}\2`, 'g'))].map((match) => match[3]!);
  // A helper's join of the folder and a name it is handed (`join(root, …, 'Daoris.Desktop.Driver', name)`): whatever it reads.
  const handed = String.raw`(?:join|Path\.Combine)\([^;()]*(['"])${project}\1\s*,\s*([A-Za-z_]\w*)\s*\)`;
  names.push(...[...text.matchAll(new RegExp(handed, 'g'))].map((match) => `<${match[2]!}>`));
  const locals = [...text.matchAll(new RegExp(String.raw`\b(?:const|let|var|string)\s+(\w+)\s*=\s*(?:join|Path\.Combine)\([^;]*?(['"])${project}\2\s*\)`, 'g'))]
    .map((match) => match[1]!);
  for (const local of locals) {
    names.push(...[...text.matchAll(new RegExp(String.raw`(?:join|Path\.Combine)\(\s*${local}\s*,\s*(['"])${file}\1`, 'g'))].map((match) => match[2]!));
  }
  if (path.startsWith('src/Daoris.Web/') || path.startsWith('tools/')) {
    names.push(...[...text.matchAll(new RegExp(String.raw`${project}/${file}(?=['"\x60])`, 'g'))].map((match) => match[1]!));
  }
  return names.filter((name) => !FIXED.has(name)).map((name) => `${path} reads ${name} by its folder`);
}

test("no reader joins a fixed folder to a file of the driver's projects, which a move would break (STRUCT1)", () => {
  const found = repositoryFiles().filter((path) => isReader(path) && !SCANNERS.has(path))
    .flatMap((path) => fixedReads(path, readFileSync(join(repoRoot, path), 'utf8')));
  assert.deepEqual([...new Set(found)].sort(), [...OWED].sort(), 'each a reader to find its file by name (driverFile, driverTestFile, SourceFiles.Driver)');
});

test('the scan sees each way of naming a fixed folder, and never a fixture, a project folder or a file left at the top', () => {
  const text = [
    "const a = readFileSync(join(here, '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'ToolsTests.cs'), 'utf8');",
    'var source = File.ReadAllText(Path.Combine(Root(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "NoteCodes.cs"));',
    "const DRIVER = join(here, '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver');",
    "const b = readFileSync(join(DRIVER, 'AccountReads.cs'), 'utf8');",
    "const c = readFileSync(join(here, 'Daoris.Desktop.Driver.Tests', 'fixtures', 'kept-names.json'), 'utf8');",
    "const d = join(repoRoot, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'resources.json');",
    "const e = source('src/Daoris.Desktop/Daoris.Desktop.Driver/Landing.cs');",
    "const f = 'src/Daoris.Desktop/Daoris.Desktop.Driver/**/NoteCodes.cs';",
    "const driver = (name: string) => readFileSync(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', name), 'utf8');",
    'while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "Daoris.Desktop.Driver"))) { }',
  ].join('\n');
  assert.deepEqual(fixedReads('src/Daoris.Cli/test/x.test.ts', text), [
    'src/Daoris.Cli/test/x.test.ts reads ToolsTests.cs by its folder',
    'src/Daoris.Cli/test/x.test.ts reads NoteCodes.cs by its folder',
    'src/Daoris.Cli/test/x.test.ts reads <name> by its folder',
    'src/Daoris.Cli/test/x.test.ts reads AccountReads.cs by its folder',
  ]);
  assert.deepEqual(fixedReads('src/Daoris.Web/src/x.test.ts', text).at(-1), 'src/Daoris.Web/src/x.test.ts reads Landing.cs by its folder');
  assert.deepEqual(namedReads("driverFile('A.cs'); driverTestFile('B.cs'); desktopFile('P', 'C.cs'); SourceFiles.Driver(\"D.cs\");"),
    [[DRIVER, 'D.cs'], [DRIVER, 'A.cs'], [DRIVER_TESTS, 'B.cs'], ['P', 'C.cs']]);
});
