import { test } from 'node:test';
import assert from 'node:assert/strict';
import { namedReads, treeReads, sourceRootReads, srcReads, localReads } from '../support/reads.ts';
import { tool, type Rule, repoPlan, repoLanes, runs } from '../support/merge-branch.ts';

// The census of test units reads this file's strings as the scan's data, not as reads.
// test-map: strings name paths as data
// reaches: * — the scan's cases are held to the lane table, which every tracked file and lane can fail

test('the scan reads a source found by name wherever the tree keeps it, and a walk of a project for its sources (STRUCT1)', () => {
  const files = [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/Landing.cs', 'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpGoProposals.cs',
  ];
  const text = [
    '        var source = File.ReadAllText(SourceFiles.Driver("Landing.cs"));',
    '        var loop = SourceFiles.Find("Daoris.Desktop.Modules", "DriverLoop.cs");',
    // A name no file has reads nothing here; the finder refuses it when the test runs.
    '        SourceFiles.Driver("Gone.cs");',
    '        var sources = SourceFiles.All(SourceFiles.DriverProject, "GitBranches*.cs");',
    '        var folder = Path.GetDirectoryName(SourceFiles.Driver($"{kinds[0]}.cs"));',
  ].join('\n');
  assert.deepEqual(namedReads(text, files), [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Trees/Landing.cs', 'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/probe.cs',
  ]);
});

test('the scan sees a read under any top-level folder, and never a name, a sentence or a folder the test made (MOD9b)', () => {
  const files = [
    'tools/orient-index-fixtures/decision-notes.json', 'examples/plugins/a/plugin.json', '.claude/knowledge/adoption.md',
    '.claude/settings.json', 'docs/README.md', 'daoris.json', 'src/Daoris.Web/src/App.tsx',
  ];
  const source = [
    'File.ReadAllText(Path.Combine(Root(), "tools", "orient-index-fixtures", "decision-notes.json"));',
    'foreach (var plugin in Directory.EnumerateDirectories(Path.Combine(WorkspaceRoot.Folder, "examples", "plugins"))) { }',
    'return Path.Combine(at?.FullName ?? throw new InvalidOperationException("no root above the test"),',
    '    ".claude", "knowledge", "adoption.md");',
    // A folder the test made in this repository's own layout, by a local, a field or a property: not this repository.
    'File.ReadAllText(Path.Combine(room, ".claude", "settings.json"));',
    'File.WriteAllText(Path.Combine(_home, "docs", "README.md"), "x");',
    'File.WriteAllText(Path.Combine(Root, "docs", "README.md"), "x");',
    // A workspace's name, a sentence, a marker file, a path deeper in another run, and `src/`, which the scan reads itself.
    'Entry("beta", "tools", "Dedicated readers integrate with approvals, so lookups never interrupt.");',
    'new UsageEntry("s2", "tools", "claude-code", "work");',
    'AcrossRules.Reading(config, "tools", "forge");',
    'AcrossRules.Reading(DriverConfig.Empty.WithWorkspaceReadAcross("default", false), "tools", null);',
    'while (!File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;',
    '[InlineData("CLAUDE.md", "docs", "docs/README.md", true)]',
    'Path.Combine(Root(), "src", "Daoris.Web", "src", "App.tsx");',
  ].join('\n');
  assert.deepEqual(treeReads(source, files), [
    'tools/orient-index-fixtures/decision-notes.json', 'examples/plugins/probe.txt', '.claude/knowledge/adoption.md',
  ]);

  // What the scan above then asks of the lane table: without the digest's row, the twin's read reaches no service suite.
  const without = tool.REACH.filter((rule) => !rule.paths?.includes('tools/orient-index-fixtures/**'));
  const reached = (reach: readonly Rule[]) => tool.selectGates(repoPlan, ['tools/orient-index-fixtures/decision-notes.json'], { lanes: repoLanes, reach, gated: true })
    .gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
  assert.equal(reached(without).includes('service'), false);
  assert.equal(reached(tool.REACH).includes('service'), true);
});

test('the scan resolves a source root as its walk does, and reads under it what the file names there (MOD9c)', () => {
  const files = [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Hooks.cs', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/ScanTests.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs',
    'src/Daoris.Service/Daoris.Service.Core/Asks.cs', 'src/Daoris.Service/Daoris.Service.Tests/GateTests.cs', 'daoris.json',
  ];
  const helper = (marker: string, returns = 'folder?.FullName ?? throw new InvalidOperationException("no source tree above the test")') => [
    '    private static string SourceRoot()',
    '    {',
    '        var folder = new DirectoryInfo(AppContext.BaseDirectory);',
    `        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "${marker}")))`,
    '        {',
    '            folder = folder.Parent;',
    '        }',
    '',
    `        return ${returns};`,
    '    }',
  ].join('\n');
  const scanner = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/ScanTests.cs';

  // The project list a scan hands to the root, a read through the helper, and one through the file's own wrapper of it.
  const driver = [
    '    private static readonly string[] Projects =',
    '        ["Daoris.Desktop.Driver", "Daoris.Desktop.Modules", "Daoris.Desktop.App"];',
    '    private static readonly string[] Occasions = ["TidyCarriedAsync", "CleanPlanAsync"];',
    '        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories)) { }',
    '        var loop = Directory.EnumerateFiles(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver"), "Driver*.cs")',
    '            .Append(Path.Combine(SourceRoot(), "Daoris.Desktop.Modules", "DriverLoop.cs"));',
    '    private static string Source(string project, string file) => File.ReadAllText(Path.Combine(SourceRoot(), project, file));',
    '        Assert.DoesNotContain("AskStatesAsync", Source("Daoris.Desktop.Driver", "Hooks.cs"));',
    // Names that are no path under the root are not reads, and nor is the folder the walk looks for.
    '        Assert.Equal([("LandingPlugins.cs", "AskOneAsync")], speakers);',
    helper('Daoris.Desktop.Driver'),
  ].join('\n');
  // Every search pattern names C# sources, so a folder is read for its C# sources.
  assert.deepEqual(sourceRootReads(scanner, driver, files), [
    'src/Daoris.Desktop/Daoris.Desktop.Driver/probe.cs', 'src/Daoris.Desktop/Daoris.Desktop.Modules/probe.cs',
    'src/Daoris.Desktop/Daoris.Desktop.App/probe.cs', 'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Hooks.cs',
  ]);
  // A file that enumerates anything else reads a folder for any file in it.
  const json = ['        Directory.GetFiles(Path.Combine(SourceRoot(), "Daoris.Desktop.Driver"), "*.json");', helper('Daoris.Desktop.Driver')].join('\n');
  assert.deepEqual(sourceRootReads(scanner, json, files), ['src/Daoris.Desktop/Daoris.Desktop.Driver/probe.txt']);

  // A helper that returns a path under the folder it found is rooted there.
  const service = [
    '        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)) { }',
    '        File.ReadAllText(Path.Combine(SourceRoot(), "Asks.cs"));',
    helper('Daoris.Service.Core', 'folder is null ? throw new InvalidOperationException("none") : Path.Combine(folder.FullName, "Daoris.Service.Core")'),
  ].join('\n');
  assert.deepEqual(sourceRootReads('src/Daoris.Service/Daoris.Service.Tests/GateTests.cs', service, files), ['src/Daoris.Service/Daoris.Service.Core/Asks.cs']);

  // A walk to the repository's own marker is the repository, whose reads the scan takes by its other patterns.
  const repository = [
    '        File.ReadAllText(Path.Combine(SourceRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App", "Program.cs"));',
    helper('daoris.json'),
  ].join('\n');
  assert.deepEqual(sourceRootReads(scanner, repository, files), []);

  // What the scan above then asks of the lane table: without MOD9c's row, the modules' and the app's sources reach no driver suite.
  const without = tool.REACH.filter((rule) => !rule.paths?.includes('src/Daoris.Desktop/Daoris.Desktop.Modules/**/*.cs'));
  const reached = (path: string, reach: readonly Rule[]) => tool.selectGates(repoPlan, [path], { lanes: repoLanes, reach, gated: true })
    .gates.filter((entry) => entry.run).map((entry) => entry.gate.name);
  for (const read of ['src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs', 'src/Daoris.Desktop/Daoris.Desktop.App/probe.cs']) {
    assert.equal(reached(read, without).includes('driver'), false, read);
    assert.equal(reached(read, tool.REACH).includes('driver'), true, read);
  }
});

test('the scan reads through a local holding a repository folder, and never through a folder the test made (MOD9d)', () => {
  const files = [
    'src/Daoris.Desktop/Directory.Packages.props', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverLoop.cs', 'src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs',
    'src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpGoProposals.cs', 'src/Daoris.Web/src/bridge/call.ts',
    'src/Daoris.Service/Daoris.Service.Http/Program.cs', 'examples/plugins/github-pull-request/plugin.json', 'daoris.json',
  ];
  const desktopTree = 'src/Daoris.Desktop/probe.txt';

  // The project list a scan hands to a local holding the desktop tree. The tree spans lanes, so it is no read itself, and
  // neither is a path named relative to it, nor a mention in a comment.
  const projects = [
    '    private static readonly string[] Projects = ["Daoris.Desktop.Modules", "Daoris.Desktop.Launcher"];',
    '        var root = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");',
    '        foreach (var project in Projects)',
    '            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))',
    '                missing.Add(Path.GetRelativePath(root, file)); // relative to the root, as Assert.Empty(root) would say',
  ].join('\n');
  assert.deepEqual(srcReads(projects, files), []);
  assert.deepEqual(localReads(projects, files), ['src/Daoris.Desktop/Daoris.Desktop.Modules/probe.cs', 'src/Daoris.Desktop/Daoris.Desktop.Launcher/probe.cs']);

  // Files named through a local and through a narrower local on it; a check that a retired project is gone names nothing.
  const named = [
    '        var desktop = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop");',
    '        var app = Path.Combine(desktop, "Daoris.Desktop.App");',
    '        var program = File.ReadAllText(Path.Combine(app, "Program.cs"));',
    '        Assert.False(File.Exists(Path.Combine(desktop, "Daoris.Desktop.Browser", "Daoris.Desktop.Browser.csproj")));',
    '        Assert.DoesNotContain("CefSharp", File.ReadAllText(Path.Combine(desktop, "Directory.Packages.props")));',
  ].join('\n');
  assert.deepEqual(srcReads(named, files), []);
  assert.deepEqual(localReads(named, files), ['src/Daoris.Desktop/Directory.Packages.props', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs']);

  // An expression-bodied helper, an interpolated name and a local handed on whole: a folder read for a file the scan cannot name.
  const whole = [
    '    private static string Page => Path.Combine(HelpProposalKindsTests.RepositoryRoot(), "src", "Daoris.Web", "src");',
    '        foreach (var file in Directory.GetFiles(Path.Combine(Page, "bridge"), "*.ts")) { }',
    '        var folder = Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.Driver", "Help", "Proposals");',
    '        Assert.Matches("IHelpProposalKind", File.ReadAllText(Path.Combine(folder, $"{name}.cs")));',
    '        var host = Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Service", "Daoris.Service.Http");',
    '        Assert.True(Directory.Exists(host));',
    '        var examples = Path.Combine(Repository(), "examples", "plugins");',
    '        PluginOffers.Needs(Path.Combine(examples, id));',
    '        PluginOffers.Needs(Path.Combine(examples, "github-pull-request"));',
  ].join('\n');
  assert.deepEqual(srcReads(whole, files), []);
  assert.deepEqual(treeReads(whole, files), []);
  assert.deepEqual(localReads(whole, files), [
    'src/Daoris.Web/src/bridge/probe.txt', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/probe.txt',
    'src/Daoris.Service/Daoris.Service.Http/probe.txt', 'examples/plugins/github-pull-request/probe.txt',
  ]);

  // A folder the test made: a scratch workspace on a field the test set, in this repository's own layout. No read at all.
  const scratch = [
    '    private readonly string _root = Path.Combine(',
    '        Path.GetTempPath(), "daoris-locator-" + Guid.NewGuid().ToString("N")[..8]);',
    '        var workspace = Path.Combine(_root, "workspace");',
    '        var deep = Path.Combine(workspace, "src", "Daoris.Desktop", "app", "bin");',
    '        var project = Path.Combine(workspace, "src", "Daoris.Service", "Daoris.Service.Http");',
    '        File.WriteAllText(Path.Combine(workspace, "daoris.json"), "{}");',
  ].join('\n');
  assert.deepEqual([...srcReads(scratch, files), ...treeReads(scratch, files), ...localReads(scratch, files)], []);
  // What was read before is the repository's own root, from a call or a member, as every other read of `src/` is.
  assert.deepEqual(srcReads('File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Daoris.Desktop", "Daoris.Desktop.App", "Program.cs"));', files),
    ['src/Daoris.Desktop/Daoris.Desktop.App/Program.cs']);

  // What the scan then asks of the lane table: the desktop tree no rule places, and what is read through it is placed.
  const unplaced = (path: string) => tool.selectGates(repoPlan, [path], { lanes: repoLanes }).unplaced;
  assert.deepEqual(unplaced(desktopTree), [desktopTree]);
  for (const read of [...localReads(projects, files), ...localReads(named, files)]) {
    assert.deepEqual(unplaced(read), [], read);
    assert.ok(runs([read]).includes('modules'), read);
  }
});

test('a folder searched for one name at any depth reads that file wherever it lies, and nothing else of it (STRUCT5d)', () => {
  const files = ['src/Daoris.Cli/src/layout/documents.ts', 'src/Daoris.Cli/src/cli.ts'];
  const byName = 'var source = File.ReadAllText(Directory.EnumerateFiles(Path.Combine(WorkspaceRoot(), "src", "Daoris.Cli", "src"), "documents.ts", SearchOption.AllDirectories).Single());';
  assert.deepEqual(srcReads(byName, files), ['src/Daoris.Cli/src/layout/documents.ts']);
  // A name found nowhere is still that name's read, and a pattern is no name: the folder is read whole, as before.
  assert.deepEqual(srcReads(byName.replace('"documents.ts"', '"gone.ts"'), files), ['src/Daoris.Cli/src/gone.ts']);
  assert.deepEqual(srcReads(byName.replace('"documents.ts"', '"*.ts"'), files), ['src/Daoris.Cli/src/probe.txt']);
  // What the scan then asks of the lane table: a change to the roles' file runs the service's suite (ORIENT2e2).
  assert.ok(runs(['src/Daoris.Cli/src/layout/documents.ts']).includes('service'));
});
