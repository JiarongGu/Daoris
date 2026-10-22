import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * PROCREACH1 (D180 point 2): which classes of a `Process` half a change reaches, read from the sources by
 * `tools/process-reach.mjs`, so the merge tool's CI runs those again and keeps the rest. The census is held here to
 * C# as this repository writes it, to a small tree whose every rule a case names, and to this repository itself: every
 * class carrying the Process trait is read into a half. Run by the `tool-tests` gate with the merge tool's tests.
 */

interface Type {
  name: string; kind: string; modifiers: string[]; bases: string[]; attributes: string; topLevel: boolean;
  outer: string | null; tests: boolean; methods: string[];
}
interface Half { project: string; settings: string }
interface CensusClass { name: string; full: string; files: string[]; traits: Record<string, string[]>; reach: string[] }
interface Census {
  half: Half; filter: string | null; sources: string[]; programs: { folder: string; names: string[]; linked: string[]; everything: boolean }[];
  classes: CensusClass[]; problems: string[]; files: Record<string, { unnamed: boolean; own: boolean }>;
}
type Placed = { whole?: string; areas?: string[]; classes?: string[]; none?: string };

const reach = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/process-reach.mjs') as {
  readCs: (text: string) => { code: string; literal: string };
  namesIn: (code: string) => Set<string>;
  typesIn: (text: string) => { namespace: string; types: Type[]; extensions: string[] };
  traitsIn: (attributes: string, constants?: Map<string, string>) => [string, string][];
  reachesIn: (attributes: string) => { areas: string[]; why: string | null }[];
  settingsFilter: (xml: string) => string | null;
  filterHolds: (filter: string, traits: Map<string, Set<string>>) => boolean;
  reachSettings: (xml: string, kept: string[]) => string;
  halfOf: (run: string) => Half | null;
  withSettings: (run: string, file: string) => string;
  referencedProjects: (project: string, files: string[], read: (file: string) => string) => string[];
  areaOf: (path: string, sources: string[]) => string | null;
  censusOf: (half: Half, files: string[], read: (file: string) => string) => Census;
  pathReach: (path: string, census: Census) => Placed;
  classesReached: (paths: string[], census: Census) => { whole: { path: string; why: string }[]; classes: string[]; by: Map<string, string> };
  repository: (root: string, commit?: string | null) => { files: string[]; read: (file: string) => string };
};

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const traits = (pairs: Record<string, string[]>) => new Map(Object.entries(pairs).map(([name, values]) => [name, new Set(values)]));

test('C# is read as code: comments and literals blanked, an interpolated hole kept as code, and the literals kept apart', () => {
  const text = [
    '// a comment naming Ghost',
    'var a = "Phantom { brace";',
    "var c = '{';",
    'var b = $"x {Real.Name} y";',
    'var v = @"C:\\path ""Quoted""";',
    'var r = """',
    '  Raw { Spectre',
    '  """;',
    '/* Block Wraith */ var d = new Solid();',
  ].join('\n');
  const { code, literal } = reach.readCs(text);
  assert.equal(code.length, text.length);
  assert.equal(code.split('\n').length, text.split('\n').length, 'its lines are kept, so a position reads the same in both');
  const names = reach.namesIn(code);
  for (const name of ['Real', 'Name', 'Solid', 'a', 'b']) assert.ok(names.has(name), name);
  for (const name of ['Ghost', 'Phantom', 'Spectre', 'Wraith', 'Quoted']) assert.ok(!names.has(name), `${name} is in a comment or a literal`);
  assert.ok(!code.includes('{ brace') && !code.includes("'{'"), 'a brace in a literal is no brace of the code');
  assert.ok(literal.includes('Phantom { brace') && literal.includes('Raw { Spectre'), 'the literals stay in the literal reading');
  assert.ok(!literal.includes('Ghost') && !literal.includes('Wraith'), 'and the comments go from both');
});

test('a file declares its types: top-level or nested, its bases, its attributes, its tests, a test\'s own traits and its extension methods', () => {
  const { namespace, types, extensions } = reach.typesIn([
    'namespace Desk.Tests;',
    '',
    '/// <summary>[Trait(Category.Name, Category.Process)] in a comment is no trait.</summary>',
    '[Trait(Category.Name, Category.Process)]',
    '[Collection(Alone.Name)]',
    'public sealed class AlphaTests : DeskFixture<int>, IDisposable',
    '{',
    '    [Fact]',
    '    public void Runs() { }',
    '',
    '    [Theory, InlineData(1)]',
    '    [Trait("Parallel", "Git")]',
    '    public void Each(int n) { }',
    '',
    '    public sealed class Inner',
    '    {',
    '        [Fact] public void Nested() { }',
    '    }',
    '',
    '    public void Dispose() { }',
    '}',
    '',
    'internal static class Shining',
    '{',
    '    public static string Shine(this string text) => text;',
    '    public static T Polish<T>(this T value) where T : class => value;',
    '}',
    '',
    'public record Row(string Name);',
    'internal abstract class DeskFixture<T> where T : struct { }',
  ].join('\n'));
  assert.equal(namespace, 'Desk.Tests');
  const named = (name: string) => types.find((type) => type.name === name)!;
  assert.deepEqual(types.map((type) => `${type.name} ${type.kind} ${type.topLevel ? 'top' : `in ${type.outer}`}`),
    ['AlphaTests class top', 'Inner class in AlphaTests', 'Shining class top', 'Row record top', 'DeskFixture class top']);
  assert.deepEqual(named('AlphaTests').bases, ['DeskFixture', 'IDisposable']);
  assert.match(named('AlphaTests').attributes, /Trait\(Category\.Name, Category\.Process\)/);
  assert.match(named('AlphaTests').attributes, /Collection\(Alone\.Name\)/);
  assert.doesNotMatch(named('AlphaTests').attributes, /in a comment/);
  assert.deepEqual([named('AlphaTests').tests, named('Inner').tests, named('Shining').tests], [true, true, false]);
  assert.equal(named('AlphaTests').methods.length, 2, "its own tests, not its nested class's");
  assert.match(named('AlphaTests').methods[1]!, /Trait\("Parallel", "Git"\)/);
  assert.deepEqual(named('DeskFixture').modifiers, ['internal', 'abstract']);
  assert.deepEqual(extensions, ['Shine', 'Polish']);
});

test('a trait is read from literals and from constants; a hand-written reach keeps its reason', () => {
  const constants = new Map([['Category.Name', 'Category'], ['Category.Process', 'Process']]);
  assert.deepEqual(reach.traitsIn('[Trait(Category.Name, Category.Process)] [Trait("Parallel", "Git")] [Trait(Unknown.X, "y")]', constants),
    [['Category', 'Process'], ['Parallel', 'Git']]);
  assert.deepEqual(reach.reachesIn('[Reaches("Desk.Core/Alpha", "Desk.Tool", Why = "it starts the tool, which reads Alpha")]'),
    [{ areas: ['Desk.Core/Alpha', 'Desk.Tool'], why: 'it starts the tool, which reads Alpha' }]);
  assert.deepEqual(reach.reachesIn('[Reaches("*")]'), [{ areas: ['*'], why: null }]);
});

test("a run settings file's filter is read, and a class's traits hold it as VSTest would choose its tests", () => {
  const xml = '<RunSettings><!-- <TestCaseFilter>Not=This</TestCaseFilter> --><RunConfiguration><TestCaseFilter>Category=Process&amp;Parallel!=Git</TestCaseFilter></RunConfiguration></RunSettings>';
  const filter = reach.settingsFilter(xml)!;
  assert.equal(filter, 'Category=Process&Parallel!=Git');
  assert.equal(reach.filterHolds(filter, traits({ Category: ['Process'] })), true);
  assert.equal(reach.filterHolds(filter, traits({ Category: ['Process'], Parallel: ['Git'] })), false);
  assert.equal(reach.filterHolds(filter, traits({})), false);
  assert.equal(reach.filterHolds('Category=Process&Parallel=Git', traits({ Category: ['Process'], Parallel: ['Git'] })), true);
  assert.equal(reach.filterHolds('(Category=A|Category=B)&!(Parallel=Git)', traits({ Category: ['B'] })), true);
  assert.equal(reach.filterHolds('(Category=A|Category=B)&!(Parallel=Git)', traits({ Category: ['B'], Parallel: ['Git'] })), false);
  assert.equal(reach.filterHolds('Category~Proc', traits({ Category: ['Process'] })), true);
  assert.equal(reach.filterHolds('FullyQualifiedName~Anything&Category=Process', traits({ Category: ['Process'] })), true, "a test's name is the half's own filter's to choose");
  assert.equal(reach.settingsFilter('<RunSettings></RunSettings>'), null);
});

test('a half runs by reach with settings of its own: its filter ANDed with each kept class kept out, in a file, every other setting as it was', () => {
  const xml = [
    '<?xml version="1.0" encoding="utf-8"?>',
    '<RunSettings>',
    '  <RunConfiguration>',
    '    <TestCaseFilter>Category=Process&amp;Parallel=Git</TestCaseFilter>',
    '  </RunConfiguration>',
    '  <xUnit><MaxParallelThreads>4</MaxParallelThreads></xUnit>',
    '</RunSettings>',
  ].join('\n');
  const narrowed = reach.reachSettings(xml, ['Desk.Tests.AlphaTests', 'Desk.Tests.Outer+Inner']);
  assert.equal(reach.settingsFilter(narrowed),
    '(Category=Process&Parallel=Git)&FullyQualifiedName!~Desk.Tests.AlphaTests.&FullyQualifiedName!~Desk.Tests.Outer+Inner.',
    'each kept class with its dot, so AlphaTestsMore still runs');
  assert.match(narrowed, /&amp;FullyQualifiedName!~Desk\.Tests\.AlphaTests\./, 'written as XML');
  assert.match(narrowed, /<MaxParallelThreads>4<\/MaxParallelThreads>/);
  assert.equal(reach.reachSettings(xml, []), xml, 'nothing kept is the half as it is');
  assert.equal(reach.settingsFilter(reach.reachSettings(xml, ['N.A(b)'])), '(Category=Process&Parallel=Git)&FullyQualifiedName!~N.A\\(b\\).', "VSTest's special characters escaped");
  assert.throws(() => reach.reachSettings('<RunSettings/>', ['N.A']), /no TestCaseFilter/);
});

test("a half is the project and settings its gate's command names, and a command can be told other settings", () => {
  assert.deepEqual(reach.halfOf('dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --settings src/Daoris.Desktop/git.process.runsettings'),
    { project: 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests', settings: 'src/Daoris.Desktop/git.process.runsettings' });
  assert.deepEqual(reach.halfOf('dotnet test "src\\Desk\\Desk.Tests\\Desk.Tests.csproj" -s ./src/Desk/process.runsettings'),
    { project: 'src/Desk/Desk.Tests', settings: 'src/Desk/process.runsettings' });
  assert.equal(reach.halfOf('dotnet test src/Desk/Desk.Tests --filter Category!=Process'), null, 'a half with no settings is no half to narrow');
  assert.equal(reach.halfOf('npm run rehearse'), null);
  assert.equal(reach.withSettings('dotnet test src/Desk/Desk.Tests --settings src/Desk/git.process.runsettings', 'D:/x y/run/desk.git.process.runsettings'),
    'dotnet test src/Desk/Desk.Tests --settings "D:/x y/run/desk.git.process.runsettings"');
  assert.throws(() => reach.withSettings('dotnet test src/Desk/Desk.Tests', 'x.runsettings'), /names no settings/);
});

/**
 * A small tree with every rule a case below names: a test project compiling against `Desk.Core` (its folders the areas),
 * starting `Desk.Tool` (which links one of Core's files), and beside them `Desk.App`, which nothing compiles against.
 */
const DESK: Record<string, string> = {
  'src/Desk/process.runsettings': '<RunSettings><RunConfiguration><TestCaseFilter>Category=Process&amp;Parallel!=Git</TestCaseFilter></RunConfiguration></RunSettings>',
  'src/Desk/Directory.Build.props': '<Project />',
  'src/Desk/Desk.Tests/Desk.Tests.csproj': [
    '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>',
    '  <ProjectReference Include="..\\Desk.Core\\Desk.Core.csproj" />',
    '  <ProjectReference Include="..\\Desk.Tool\\Desk.Tool.csproj" ReferenceOutputAssembly="false" />',
    '</ItemGroup></Project>',
  ].join('\n'),
  'src/Desk/Desk.Core/Desk.Core.csproj': '<Project Sdk="Microsoft.NET.Sdk" />',
  'src/Desk/Desk.Core/Alpha/AlphaThing.cs': 'namespace Desk;\npublic sealed class AlphaThing { }\n',
  'src/Desk/Desk.Core/Beta/BetaThing.cs': 'namespace Desk;\npublic sealed class BetaThing { }\n',
  'src/Desk/Desk.Core/Gamma/Shining.cs': 'namespace Desk;\npublic static class Shining { public static string Shine(this string text) => text; }\n',
  'src/Desk/Desk.Tool/Desk.Tool.csproj': [
    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><AssemblyName>desk-tool</AssemblyName></PropertyGroup>',
    '<ItemGroup><Compile Include="..\\Desk.Core\\Beta\\BetaThing.cs" Link="BetaThing.cs" /></ItemGroup></Project>',
  ].join('\n'),
  'src/Desk/Desk.Tool/Program.cs': 'namespace Desk.Tool;\ninternal static class Program { static void Main() { } }\n',
  'src/Desk/Desk.App/Window.cs': 'namespace Desk.App;\npublic sealed class Window { }\n',
  'src/Desk/Desk.Tests/Category.cs': [
    'namespace Desk.Tests;',
    'internal static class Category { public const string Name = "Category"; public const string Process = "Process"; public const string Parallel = "Parallel"; public const string Git = "Git"; }',
  ].join('\n'),
  'src/Desk/Desk.Tests/GlobalUsings.cs': 'global using Xunit;\n',
  'src/Desk/Desk.Tests/BetaFixture.cs': 'namespace Desk.Tests;\npublic class BetaFixture { protected BetaThing Thing = new(); }\n',
  'src/Desk/Desk.Tests/fixtures/alpha-rows.json': '[]\n',
  'src/Desk/Desk.Tests/AlphaTests.cs': [
    'namespace Desk.Tests;',
    '[Trait(Category.Name, Category.Process)]',
    'public sealed class AlphaTests',
    '{',
    '    [Fact] public void Reads() { var rows = Path.Combine("fixtures", "alpha-rows.json"); _ = new AlphaThing(); }',
    '}',
  ].join('\n'),
  'src/Desk/Desk.Tests/BetaTests.cs': [
    'namespace Desk.Tests;',
    '[Trait(Category.Name, Category.Process)]',
    'public sealed class BetaTests : BetaFixture',
    '{',
    '    [Fact] public void Holds() { }',
    '}',
  ].join('\n'),
  'src/Desk/Desk.Tests/ShineTests.cs': [
    'namespace Desk.Tests;',
    '[Trait(Category.Name, Category.Process)]',
    'public sealed class ShineTests { [Fact] public void Shines() => Assert.Equal("a", "a".Shine()); }',
  ].join('\n'),
  'src/Desk/Desk.Tests/ToolTests.cs': [
    'namespace Desk.Tests;',
    '[Trait(Category.Name, Category.Process)]',
    'public sealed class ToolTests { [Fact] public void Starts() { var exe = Path.Combine("src", "Desk", "Desk.Tool", "bin", "desk-tool.dll"); } }',
  ].join('\n'),
  // Planted: a Process class whose files name nothing the census can place. It runs at every change that reaches its half.
  'src/Desk/Desk.Tests/LonelyTests.cs': 'namespace Desk.Tests;\n[Trait(Category.Name, Category.Process)]\npublic sealed class LonelyTests { [Fact] public void Waits() { } }\n',
  'src/Desk/Desk.Tests/PartlyTests.cs': [
    'namespace Desk.Tests;',
    'public sealed class PartlyTests',
    '{',
    '    [Fact] public void Fast() { }',
    '    [Fact, Trait(Category.Name, Category.Process)] public void Slow() { _ = new AlphaThing(); }',
    '}',
  ].join('\n'),
  'src/Desk/Desk.Tests/GitTests.cs': 'namespace Desk.Tests;\n[Trait(Category.Name, Category.Process)]\n[Trait(Category.Parallel, Category.Git)]\npublic sealed class GitTests { [Fact] public void Git() { _ = new AlphaThing(); } }\n',
  'src/Desk/Desk.Tests/FastTests.cs': 'namespace Desk.Tests;\npublic sealed class FastTests { [Fact] public void Quick() { _ = new BetaThing(); } }\n',
};
const deskCensus = () => reach.censusOf({ project: 'src/Desk/Desk.Tests', settings: 'src/Desk/process.runsettings' }, Object.keys(DESK), (file) => DESK[file] ?? '');
const short = (names: string[]): string[] => names.map((name) => name.split('.').at(-1) ?? name).sort();

test("the census reads a half's classes from its sources: their traits, a test's own, a base's files, and each one's reach", () => {
  const census = deskCensus();
  assert.deepEqual(census.problems, []);
  assert.equal(census.filter, 'Category=Process&Parallel!=Git');
  assert.deepEqual(census.sources, ['src/Desk/Desk.Core']);
  assert.deepEqual(census.programs, [{ folder: 'src/Desk/Desk.Tool', names: ['Desk.Tool', 'desk-tool.dll', 'desk-tool.exe'], linked: ['src/Desk/Desk.Core/Beta'], everything: false }]);
  assert.deepEqual(short(census.classes.map((each) => each.full)), ['AlphaTests', 'BetaTests', 'LonelyTests', 'PartlyTests', 'ShineTests', 'ToolTests'],
    'the git class is the other half\'s, the fast class no half\'s, and a class with one Process test is in it');
  const of = (name: string) => census.classes.find((each) => each.name === name)!;
  assert.equal(of('AlphaTests').full, 'Desk.Tests.AlphaTests');
  assert.deepEqual(of('AlphaTests').reach, ['src/Desk/Desk.Core/Alpha']);
  assert.deepEqual(of('BetaTests').reach, ['src/Desk/Desk.Core/Beta'], 'through its base, a fixture of the test project');
  assert.ok(of('BetaTests').files.includes('src/Desk/Desk.Tests/BetaFixture.cs'));
  assert.deepEqual(of('ShineTests').reach, ['src/Desk/Desk.Core/Gamma'], 'an extension method named is its file\'s');
  assert.deepEqual(of('ToolTests').reach, ['src/Desk/Desk.Core/Beta', 'src/Desk/Desk.Tool'], 'a program it starts, and what that compiles in by link');
  assert.deepEqual(of('LonelyTests').reach, [], 'planted: no reach');
  assert.deepEqual(of('PartlyTests').traits, { Category: ['Process'] });
  assert.deepEqual([census.files['src/Desk/Desk.Tests/GlobalUsings.cs'], census.files['src/Desk/Desk.Tests/AlphaTests.cs'], census.files['src/Desk/Desk.Tests/BetaFixture.cs']],
    [{ unnamed: true, own: false }, { unnamed: false, own: true }, { unnamed: false, own: false }]);
});

test('a changed path reaches the classes whose reach holds it; what acts unnamed or cannot be placed reaches the whole half', () => {
  const census = deskCensus();
  const at = (path: string): Placed => {
    const placed = reach.pathReach(path, census);
    return placed.classes ? { classes: short(placed.classes) } : placed;
  };
  assert.deepEqual(at('src/Desk/Desk.Core/Alpha/AlphaThing.cs'), { areas: ['src/Desk/Desk.Core/Alpha'] });
  assert.deepEqual(at('src/Desk/Desk.Tests/AlphaTests.cs'), { classes: ['AlphaTests'] });
  assert.deepEqual(at('src/Desk/Desk.Tests/BetaFixture.cs'), { classes: ['BetaTests'] }, 'a helper reaches the classes that read it');
  assert.deepEqual(at('src/Desk/Desk.Tests/fixtures/alpha-rows.json'), { classes: ['AlphaTests'] }, 'a fixture the classes whose strings name it');
  assert.deepEqual(at('src/Desk/Desk.Tests/FastTests.cs'), { none: 'no class of the half reads src/Desk/Desk.Tests/FastTests.cs' });
  assert.deepEqual(at('src/Desk/Desk.Tool/Program.cs'), { areas: ['src/Desk/Desk.Tool'] });
  assert.deepEqual(at('src/Desk/Desk.App/Window.cs'), { none: 'no class of the half compiles against, starts or names Desk.App' });
  assert.deepEqual(at('src/Desk/Desk.App/Desk.App.csproj'), { none: 'no class of the half compiles against, starts or names Desk.App' });
  assert.deepEqual(at('src/Desk/Desk.Tool/Desk.Tool.csproj'), { areas: ['src/Desk/Desk.Tool'] }, "a program's project file reaches the classes that start it");
  assert.match(at('src/Desk/Desk.Tests/GlobalUsings.cs').whole ?? '', /acts on every test unnamed/);
  assert.match(at('src/Desk/Desk.Tests/Desk.Tests.csproj').whole ?? '', /project file/);
  assert.match(at('src/Desk/Desk.Tests/Gone.cs').whole ?? '', /does not hold/);
  assert.match(at('src/Desk/Desk.Core/Desk.Core.csproj').whole ?? '', /root/);
  assert.match(at('src/Desk/Directory.Build.props').whole ?? '', /no class's reach places/);
  assert.match(at('src/Desk/process.runsettings').whole ?? '', /no class's reach places/);
  assert.match(at('package.json').whole ?? '', /no class's reach places/);
});

test('one module changed reaches its classes and the planted class with no reach; a fixture every class shares, all of them; a project file, the whole half', () => {
  const census = deskCensus();
  let reached = reach.classesReached(['src/Desk/Desk.Core/Alpha/AlphaThing.cs'], census);
  assert.deepEqual(short(reached.classes), ['AlphaTests', 'LonelyTests', 'PartlyTests']);
  assert.equal(reached.by.get('Desk.Tests.AlphaTests'), 'src/Desk/Desk.Core/Alpha/AlphaThing.cs');
  reached = reach.classesReached(['src/Desk/Desk.Core/Beta/BetaThing.cs'], census);
  assert.deepEqual(short(reached.classes), ['BetaTests', 'LonelyTests', 'ToolTests'], 'the tool compiles Beta in, so the class that starts it is reached');
  reached = reach.classesReached(['src/Desk/Desk.Tests/GlobalUsings.cs', 'src/Desk/Desk.Core/Alpha/AlphaThing.cs'], census);
  assert.equal(reached.classes.length, census.classes.length);
  assert.deepEqual(reached.whole.map((each) => each.path), ['src/Desk/Desk.Tests/GlobalUsings.cs']);
  reached = reach.classesReached(['src/Desk/Desk.App/Window.cs'], census);
  assert.deepEqual(reached.classes, [], 'a path that places nothing reaches nothing, not even the class with no reach');
  reached = reach.classesReached(['src/Desk/Desk.Tests/BetaFixture.cs', 'src/Desk/Desk.Tests/fixtures/alpha-rows.json'], census);
  assert.deepEqual(short(reached.classes), ['AlphaTests', 'BetaTests'], "a test project's file reaches the classes that read it, read as every class is");
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
  const twice = [...inHalf].filter(([, halves]) => halves.length > 1);
  assert.deepEqual(twice, [], 'no class is in two halves of one project');
  // Read the plain way: each class a Process trait stands before, in the test projects the halves run, by its own name (a
  // nested class's is the last part of the census's `Outer+Inner`).
  const projects = [...new Set(gates.map((gate) => reach.halfOf(gate.run)!.project))];
  const known = new Set([...inHalf.keys()].map((key) => key.replace(/ .*[.+]/, ' ')));
  const marked: string[] = [];
  for (const project of projects) {
    for (const file of files.filter((path) => path.startsWith(`${project}/`) && path.endsWith('.cs'))) {
      const { code } = reach.readCs(read(file));
      for (const match of code.matchAll(/\[Trait\(Category\.Name,\s*Category\.Process\)\][^{;]*?\bclass\s+(\w+)/g)) marked.push(`${project} ${match[1]}`);
    }
  }
  assert.ok(marked.length > 100, `${marked.length} marked classes`);
  const missing = marked.filter((name) => !known.has(name));
  assert.deepEqual(missing, [], 'a class the census misses would never be kept, but it would be counted nowhere');
});
