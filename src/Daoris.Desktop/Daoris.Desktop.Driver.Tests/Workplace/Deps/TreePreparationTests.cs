using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a repository declares to prepare a tree: a session's tree is handed without it, its session told the commands; a verification
/// tree runs each gate's <c>regenerate</c> once, never the <c>treeBaseline</c>. Real git and real commands; fixtures live under the
/// repository's gitignored <c>_fixtures/</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreePreparationTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "tree-prep", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose() => GitSeed.Discard(_scratch);

    private async Task<string> MakeTreeAsync(string gates, string baselineExit)
    {
        Directory.CreateDirectory(Root);
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "local/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "regen.cjs"), "const f = require('fs'); f.mkdirSync('local', { recursive: true }); f.appendFileSync('local/regen.txt', 'x');\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "base.cjs"), $"const f = require('fs'); f.mkdirSync('local', {{ recursive: true }}); f.writeFileSync('local/base.txt', 'recorded'); process.exit({baselineExit});\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.gates.json"), gates);
        await GitAsync(Root, "add", ".");
        await GitAsync(Root, "commit", "-q", "-m", "first");
        var tree = Path.Combine(_scratch, "t1");
        await GitAsync(Root, "worktree", "add", "-q", "-b", "daoris/t1", tree, "main");
        TreeFilesRecord.Mark(Home, tree);
        return tree;
    }

    private const string Declared = """
        { "gates": [ { "name": "a", "run": "x", "regenerate": "node regen.cjs" }, { "name": "b", "run": "y", "regenerate": "node regen.cjs" } ],
          "treeBaseline": "node base.cjs" }
        """;

    [Fact]
    public async Task The_declaration_names_each_regenerate_once_and_the_baseline()
    {
        await MakeTreeAsync(Declared, "0");

        var read = TreePrepDeclared.Read(Root);

        Assert.Equal(["node regen.cjs"], read.Regenerate);
        Assert.Equal("node base.cjs", read.Baseline);
        Assert.True(TreePrepDeclared.Read(Path.Combine(_scratch, "nowhere")).Empty);
    }

    /// <summary>
    /// OPENSLOW1: a session's open waited minutes on the declared preparation (TREEPREP1), its agent idle; the tree is handed with what the
    /// store placed, the session told the declared commands, and nothing of them runs. Each step of the stage is timed.
    /// </summary>
    [Fact]
    public async Task A_sessions_tree_is_handed_without_waiting_on_the_declared_preparation_and_is_told_it()
    {
        var tree = await MakeTreeAsync(Declared, "0");
        var said = new List<Noted>();
        var clock = 0L;
        var steps = new LookStages(clock: () => clock += 20);

        var outcome = await TreeFilesStep.RunAsync(Home, Root, tree, "engine", said.Add, CancellationToken.None, steps: steps);

        Assert.False(File.Exists(Path.Combine(tree, "local", "regen.txt")));
        Assert.False(File.Exists(Path.Combine(tree, "local", "base.txt")));
        Assert.Contains("`node regen.cjs`, then `node base.cjs`", outcome.Told);
        Assert.Contains("before you change a file", outcome.Told);
        var line = steps.Take()!;
        Assert.All(["files.read=", "files.deps=", "files.hook=", "files.setup=", "files.shares=", "files.local="], step => Assert.Contains(step, line));
        Assert.DoesNotContain("files.prep", line);
    }

    [Fact]
    public async Task A_verification_tree_regenerates_once_and_never_runs_the_baseline()
    {
        var tree = await MakeTreeAsync(Declared, "1");
        var said = new List<Noted>();

        var outcome = await TreeFilesStep.RunAsync(Home, Root, tree, "engine", said.Add, CancellationToken.None, regenerate: true);

        Assert.Equal("x", await File.ReadAllTextAsync(Path.Combine(tree, "local", "regen.txt")));
        Assert.False(File.Exists(Path.Combine(tree, "local", "base.txt")));
        Assert.DoesNotContain(StartNoteCodes.StartedPrepFailed.Code, said.SelectMany(each => each.Parts).Select(part => part.Code));
        Assert.Null(outcome.Told);
    }

    [Fact]
    public async Task A_regenerate_that_could_not_run_is_said_and_the_preparation_goes_on()
    {
        var tree = await MakeTreeAsync("""{ "gates": [ { "name": "a", "run": "x", "regenerate": "node -e \"process.exit(2)\"" } ] }""", "0");
        var said = new List<Noted>();

        var outcome = await TreeFilesStep.RunAsync(Home, Root, tree, "engine", said.Add, CancellationToken.None, regenerate: true);

        Assert.Contains(StartNoteCodes.StartedPrepFailed.Code, said.SelectMany(each => each.Parts).Select(part => part.Code));
        Assert.False(outcome.Stopped);
    }
}
