using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a repository declares to prepare a tree: a session's tree is handed without it, its session told the commands; a verification
/// tree runs each gate's <c>regenerate</c> once before each check, the first that does not finish named and the rest not run, never the
/// <c>treeBaseline</c>. Real git and real commands; fixtures live under the repository's gitignored <c>_fixtures/</c>.
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
    public void A_declarations_text_names_the_same_regenerates_as_its_file()
    {
        Assert.Equal(["node regen.cjs"], TreePrepDeclared.Of(Declared).Regenerate);
        Assert.True(TreePrepDeclared.Of("{ not json").Empty);
        Assert.True(TreePrepDeclared.Of(null).Empty);
    }

    [Fact]
    public async Task A_verification_tree_regenerates_each_command_once_and_never_runs_the_baseline()
    {
        var tree = await MakeTreeAsync(Declared, "1");

        var failed = await TreePreparation.RegenerateAsync(Home, "engine", TreePrepDeclared.Of(Declared).Regenerate, tree, CancellationToken.None);

        Assert.Null(failed);
        Assert.Equal("x", await File.ReadAllTextAsync(Path.Combine(tree, "local", "regen.txt")));
        Assert.False(File.Exists(Path.Combine(tree, "local", "base.txt")));
    }

    [Fact]
    public async Task A_regenerate_that_does_not_finish_is_named_with_its_output_and_the_rest_do_not_run()
    {
        var tree = await MakeTreeAsync(Declared, "0");
        const string broken = "node -e \"console.log('contracts would not build'); process.exit(2)\"";

        var failed = await TreePreparation.RegenerateAsync(Home, "engine", [broken, "node regen.cjs"], tree, CancellationToken.None);

        Assert.NotNull(failed);
        Assert.Equal((broken, 2), (failed.Command, failed.Ran.Code));
        Assert.Contains("contracts would not build", failed.Ran.Output);
        Assert.False(File.Exists(Path.Combine(tree, "local", "regen.txt")));
    }
}
