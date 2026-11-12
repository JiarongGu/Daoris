/**
 * The guard of a kept build base and the census of its readers (the tree-pool design §38.2, 6-7).
 *
 *   node --test tools/tree-base.test.mjs
 *
 * `sites` lists every call site of the tracked code under `tools/` and `src/` and every root script that reads a build output: a
 * path joined with a `bin` part followed by a Debug or Release literal or a name, a `bin/Debug` string, a call of a resolver that
 * makes one, and, outside tests, a `--no-build`. Each is keyed by its file and its statement's text, never its line, so two calls in
 * one file are two sites. Each is guarded, its path passed through `tools/tree-base.mjs` or, in the product, `BaseMarker`, or exempt
 * for one reason this test checks against the code; a site in neither list fails, and so does an entry no site matches.
 */
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { PATIENCE } from '../src/Daoris.Cli/test/_fixture.ts';
import { MARKER, baseAbove, build, guarded, sites } from './tree-base.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, '_fixtures', 'tree-base-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const TEST = /(\.test\.|[/\\]test[/\\]|Tests[/\\]|tool-test[/\\])/;
const FINDERS = /\b(?:applicationsAt|running|startedHere|commandLinesAt|ago|takeWindow|existsSync)\(/;

/** Why a site may read a build output unguarded, each checked against the code. */
const REASONS = {
  guarded: (site, text) => /\bguarded\(|BaseMarker\.Above\(/.test(site.whole) || boundThrough(site, text),
  finds: (site, text) => FINDERS.test(site.whole) || FINDERS.test(text),
  resolver: (site, text) => /export function assemblyExe\(|export const targetExe =/.test(text)
    && /join\(projectDir, 'bin', flavour\)|^export const targetExe =/.test(site.statement),
  'own output': (site, text) => /\bownBuildRoot\(/.test(text) || TEST.test(site.file),
  'no output': (site) => /homeEntry\(|homeBins\(|homePlaces\(|"usr", "bin"|managed, "bin"|new\("bin"|'cache', 'bin'|'home', 'bin'|"app", "bin"/.test(site.statement),
  referenced: (site, _text, entry) => referenced(site.file, entry.project),
  fixture: (site) => TEST.test(site.file),
  'merge tool': (site) => /^tools\/merge(?:-branch(?:\.mjs$|\/)|\/)/.test(site.file) && site.shape === 'R2' && !/out\.push\(/.test(site.statement),
  text: (site) => /out\.push\(|new\(\$@"|\/--no-build\/|const string \w+ = "--no-build";/.test(site.statement),
};

function driverTestFile(name) {
  const project = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests';
  const found = execFileSync('git', ['ls-files', '--', project], { cwd: root, encoding: 'utf8', windowsHide: true })
    .split('\n').filter((path) => path.slice(path.lastIndexOf('/') + 1) === name);
  assert.equal(found.length, 1, `${name} is under ${project} ${found.length} times`);
  return found[0];
}

/** Every site of the tree as it stands, by file and the text its statement holds; an empty text takes every site of a test file. */
const CENSUS = [
  { file: 'tools/desktop/commands.mjs', text: 'guarded(assemblyExe(DESKTOP_PROJECT))', why: 'guarded' },
  { file: 'tools/desktop/commands.mjs', text: 'const httpHost = guarded(assemblyExe(HTTP_PROJECT))', why: 'guarded' },
  { file: 'tools/desktop/commands.mjs', text: 'const mcpHost = guarded(assemblyExe(MCP_PROJECT))', why: 'guarded' },
  { file: 'tools/desktop/commands.mjs', text: 'const driverProgram = guarded(assemblyExe(DRIVER_HOST_PROJECT))', why: 'guarded' },
  { file: 'tools/proof-kit.mjs', text: "'daoris-knowledge-http.dll'", why: 'guarded' },
  { file: 'tools/proof-kit.mjs', text: "'daoris-driver.dll'", why: 'guarded' },
  { file: 'tools/proof-kit.mjs', text: "'daoris-knowledge.exe'", why: 'guarded' },
  { file: 'tools/family-rehearsal.mjs', text: 'const httpDll = join(httpProject', why: 'guarded' },
  { file: 'src/Daoris.Web/scripts/e2e-host.mjs', text: "'Daoris.Service.Http', 'bin', 'Debug'", why: 'guarded' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver/Service/ServiceHostLocator.cs', text: 'var built = Path.Combine(project, "bin", flavour', why: 'guarded' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver/Knowledge/KnowledgeConnector.cs', text: 'var built = Path.Combine(project, "bin", flavour', why: 'guarded' },
  { file: 'tools/desktop.mjs', text: 'startedHere(applicationsAt(targetExe())', why: 'finds' },
  { file: 'tools/desktop.mjs', text: 'const exe = targetExe();', why: 'finds' },
  { file: 'tools/desktop/commands.mjs', text: 'running(assemblyExe(HTTP_PROJECT))', why: 'finds' },
  { file: 'tools/desktop/commands.mjs', text: 'const shell = assemblyExe(DESKTOP_PROJECT)', why: 'finds' },
  { file: 'tools/desktop/commands.mjs', text: 'const host = assemblyExe(HTTP_PROJECT)', why: 'finds' },
  { file: 'tools/desktop/executables.mjs', text: "join(projectDir, 'bin', flavour)", why: 'resolver' },
  { file: 'tools/desktop/session.mjs', text: 'export const targetExe =', why: 'resolver' },
  { file: 'tools/family-rehearsal.mjs', text: 'ownBuilt(ownOutput, driverProject', why: 'own output' },
  { file: 'tools/deployment-rehearsal.mjs', text: 'ownBuilt(ownOutput, driverProject', why: 'own output' },
  { file: 'tools/rehearsal-kit.test.mjs', text: '', why: 'own output' },
  { file: 'tools/deployment-rehearsal.mjs', text: "homeEntry(home, 'bin'", why: 'no output' },
  { file: 'tools/knowledge-server.mjs', text: "'cache', 'bin'", why: 'no output' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/GitSshProgram.cs', text: '"usr", "bin"', why: 'no output' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/HolderTerminal.cs', text: 'managed, "bin"', why: 'no output' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver/Platform/DaorisHome.cs', text: 'new("bin"', why: 'no output' },
  { file: 'src/Daoris.Service/Daoris.Service.Core/Platform/DaorisHome.cs', text: 'new("bin"', why: 'no output' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/Browser/BuiltApplication.cs', text: '', why: 'referenced', project: 'Daoris.Desktop.App' },
  { file: driverTestFile('DriverHostTests.cs'), text: '', why: 'referenced', project: 'Daoris.Desktop.Driver.Host' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Platform/PieceLauncherProcessTests.cs', text: '', why: 'referenced', project: 'Daoris.Desktop.PieceStart' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Loop/PartnerProcessTests.cs', text: '', why: 'referenced', project: 'Daoris.Desktop.PieceStart' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Platform/OwnedProcessesProcessTests.cs', text: '', why: 'referenced', project: 'Daoris.Desktop.OwnerStandIn' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/GitDoorsLaid.cs', text: '', why: 'referenced', project: 'Daoris.Desktop.GitDoor' },
  ...[
    'src/Daoris.Cli/test/deployment-rehearsal.test.ts', 'src/Daoris.Cli/test/desktop-tool.test.ts', 'src/Daoris.Cli/test/platform/home-census.test.ts',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Knowledge/KnowledgeConnectorTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Review/Heavy/UnreleasedBasesTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Service/ServiceHostLocatorTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/ContentProofLossTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/Deps/TreeBaseWordsTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/Files/FileAnalysisTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/Files/RuleFileFormTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/GitDoorsTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/GitHoldsProcessTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/Pool/BenchShapes.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/Pool/BuildAccountProcessTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/Pool/TreeBaseGitTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/Pool/TreeBaseBuildsProcessTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Workplace/TreeScratchTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/Windows/DesktopPageTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/Windows/TaskbarIdentityTests.cs',
    'tools/tree-base.test.mjs',
  ].map((file) => ({ file, text: '', why: 'fixture' })),
  { file: 'tools/merge-branch/plan.mjs', text: 'export const noBuildCommand', why: 'merge tool' },
  { file: 'tools/merge/build-once.ts', text: 'const RUN_ONLY', why: 'merge tool' },
  { file: 'tools/merge/build-once.ts', text: "else if (!word.includes('=')", why: 'merge tool' },
  { file: 'tools/merge/build-once.ts', text: 'export const withoutBuild', why: 'merge tool' },
  { file: 'tools/merge-branch.mjs', text: 'export const rerunCommand', why: 'merge tool' },
  { file: 'tools/merge-branch.mjs', text: 'out.push(', why: 'text' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/Files/FileAnalysis.cs', text: '--no-build|', why: 'text' },
  { file: 'src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/Files/FileKindSets.cs', text: 'const string SkipsBuild = "--no-build"', why: 'text' },
  { file: 'tools/tree-base.mjs', text: "const output = join(project, 'bin', configuration)", why: 'finds' },
  { file: 'tools/tree-base.mjs', text: '/--no-build/', why: 'text' },
];

/** What the census finds wrong with `found` against `census`: a site no entry lists, an entry no site matches, a reason the code does not hold. */
function problems(found, census, base = root) {
  const said = [];
  const matched = new Set();
  for (const site of found) {
    const entry = census.find((each) => each.file === site.file && site.statement.includes(each.text));
    if (!entry) {
      said.push(`unlisted: ${site.file} :: ${site.statement}`);
      continue;
    }
    matched.add(entry);
    const text = readFileSync(join(base, site.file), 'utf8');
    if (!REASONS[entry.why](site, text, entry)) said.push(`${entry.why} does not hold: ${site.file} :: ${site.statement}`);
  }
  for (const entry of census) if (!matched.has(entry)) said.push(`no site: ${entry.file} :: ${entry.text}`);
  return said;
}

/** Whether the statement declares a binding the file passes through the guard. */
function boundThrough(site, text) {
  const bound = /(?:const|let|var)\s+(\w+)\s*=/.exec(site.statement)?.[1];
  return Boolean(bound) && new RegExp(`\\bguarded\\(${bound}\\b|BaseMarker\\.Above\\(${bound}\\b`).test(text);
}

/** Whether the test project holding `file` references `project`, so the build that runs the test wrote that project's output. */
function referenced(file, project) {
  for (let folder = dirname(join(root, file)); folder.startsWith(root); folder = dirname(folder)) {
    const projects = readdirSync(folder).filter((name) => name.endsWith('.csproj'));
    if (projects.length) return projects.some((name) => readFileSync(join(folder, name), 'utf8').includes(`${project}.csproj`));
  }
  return false;
}

test('every site of the tree is guarded or exempt for a reason the code holds, and every entry names a site', () => {
  assert.deepEqual(problems(sites(root), CENSUS), []);
});

test('a reader planted in the tree, and a guarded site passed through no guard, each fail by name', () => {
  const tree = join(scratch, 'planted');
  mkdirSync(join(tree, 'tools'), { recursive: true });
  writeFileSync(join(tree, 'package.json'), JSON.stringify({ scripts: { e2e: 'dotnet test --no-build' } }));
  writeFileSync(join(tree, 'tools', 'reader.mjs'), "const exe = join(root, 'bin', 'Debug', 'net10.0', 'x.exe');\nconst guardedExe = guarded(assemblyExe(project));\n");
  const found = sites(tree, ['tools/reader.mjs']);
  const said = problems(found, [{ file: 'tools/reader.mjs', text: 'const exe =', why: 'guarded' }, { file: 'tools/reader.mjs', text: 'guardedExe', why: 'guarded' }], tree);
  assert.deepEqual(said, ['guarded does not hold: tools/reader.mjs :: const exe = join(root, \'bin\', \'Debug\', \'net10.0\', \'x.exe\');', 'unlisted: package.json :: scripts.e2e: --no-build']);
});

test('an entry whose site was removed fails by name', () => {
  const said = problems(sites(root), [...CENSUS, { file: 'tools/desktop/commands.mjs', text: 'assemblyExe(GONE_PROJECT)', why: 'guarded' }]);
  assert.deepEqual(said, ['no site: tools/desktop/commands.mjs :: assemblyExe(GONE_PROJECT)']);
});

test('each guarded reader of the desktop tool is built by `build` through the one builder, the MCP host among them', () => {
  const commands = readFileSync(join(root, 'tools', 'desktop', 'commands.mjs'), 'utf8');
  const builder = /export async function build\(args\) \{([\s\S]*?)\n\}/.exec(commands)[1];
  for (const project of ['HTTP_PROJECT', 'DESKTOP_PROJECT', 'DRIVER_HOST_PROJECT', 'MCP_PROJECT']) {
    assert.match(builder, new RegExp(`buildProject\\(${project}, configuration`), `build() builds ${project} through the helper`);
  }
  assert.doesNotMatch(builder, /run\('dotnet', \['build'/);
  const scripts = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).scripts;
  assert.match(scripts['test:web:e2e'], /node tools\/tree-base\.mjs build src\/Daoris\.Service\/Daoris\.Service\.Http/);
  assert.match(readFileSync(join(root, 'tools', 'proof-kit.mjs'), 'utf8'), /needs\('the MCP host', 'npm run desktop -- build'\)/);
});

test('a marker refuses with its sentence, a path with none passes, and an empty path passes as not built', () => {
  const tree = join(scratch, 'guard');
  const output = join(tree, 'App', 'bin', 'Debug', 'net10.0');
  mkdirSync(join(tree, '.git'), { recursive: true });
  mkdirSync(output, { recursive: true });
  const exe = join(output, 'App.exe');
  assert.equal(guarded(exe, { refuse: () => 'refused' }), exe);
  assert.equal(guarded('', { refuse: () => 'refused' }), '');
  writeFileSync(join(tree, 'App', 'bin', 'Debug', MARKER), JSON.stringify({ from: 's-aaaa0001', says: "this output is the previous line's build" }));
  assert.equal(baseAbove(exe).from, 's-aaaa0001');
  assert.equal(guarded(exe, { refuse: (message) => message }), `${exe}: this output is the previous line's build`);
  assert.equal(baseAbove(join(tree, 'App', 'Program.cs')), null);
});

test('the one builder builds through the door where a marker stands under the configuration, and plainly where none does', () => {
  const project = join(scratch, 'builder', 'App');
  mkdirSync(join(project, 'bin', 'Release'), { recursive: true });
  mkdirSync(join(project, 'bin', 'Debug'), { recursive: true });
  writeFileSync(join(project, 'bin', 'Debug', MARKER), '{}');
  const ran = [];
  const runner = (command, words) => ran.push([command, ...words].join(' '));
  build(project, 'Debug', { runner });
  build(project, 'Release', { runner });
  assert.deepEqual(ran, [`daoris tree build ${join(project, 'bin', 'Debug')}`, `dotnet build ${project} -c Release`]);
  assert.equal(existsSync(join(project, 'bin', 'Debug', MARKER)), true);
});

test('`run --real` is refused while a marker stands over the shell it would start, before it reads a home or starts anything', () => {
  const checkout = join(scratch, 'real');
  for (const folder of ['tools', join('src', 'Daoris.Cli', 'src')]) {
    cpSync(join(root, folder), join(checkout, folder), { recursive: true, filter: (from) => !/[/\\](node_modules|bin|obj)$/.test(from) });
  }
  cpSync(join(root, 'src', 'Daoris.Cli', 'package.json'), join(checkout, 'src', 'Daoris.Cli', 'package.json'));
  writeFileSync(join(checkout, 'package.json'), JSON.stringify({ type: 'module' }));
  mkdirSync(join(checkout, '.git'));
  const app = join(checkout, 'src', 'Daoris.Desktop', 'Daoris.Desktop.App');
  const built = join(app, 'bin', 'Debug', 'net10.0');
  mkdirSync(built, { recursive: true });
  writeFileSync(join(app, 'Daoris.Desktop.App.csproj'), '<Project><PropertyGroup><AssemblyName>Daoris.Desktop.App</AssemblyName></PropertyGroup></Project>');
  for (const file of ['Daoris.Desktop.exe', 'Daoris.Desktop.App.dll']) writeFileSync(join(built, file), "the previous line's\n");
  writeFileSync(join(app, 'bin', 'Debug', MARKER), JSON.stringify({ from: 's-aaaa0001', says: "this output is the previous line's build; run `daoris tree build bin/Debug` first" }));

  const home = join(checkout, 'home');
  const ran = spawnSync(process.execPath, [join(checkout, 'tools', 'desktop.mjs'), 'run', '--real'], {
    cwd: checkout, encoding: 'utf8', env: { ...process.env, DAORIS_HOME: home }, timeout: PATIENCE, windowsHide: true,
  });

  assert.equal(ran.status, 1, `${ran.stdout}${ran.stderr}`);
  assert.match(ran.stderr, /Daoris\.Desktop\.exe: this output is the previous line's build; run `daoris tree build bin\/Debug` first/);
  assert.doesNotMatch(ran.stdout, /--real/);
  assert.equal(existsSync(home), false);
});
