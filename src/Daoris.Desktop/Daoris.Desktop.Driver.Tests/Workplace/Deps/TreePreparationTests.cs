using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A new tree is made like its checkout before a session gets it: the gates' declared <c>regenerate</c> commands run in it, then the
/// declared <c>treeBaseline</c> once, whose red is the base and no failure. Real git and real commands; fixtures live under the
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

    [Fact]
    public async Task A_new_tree_regenerates_once_then_runs_the_baseline_and_a_red_one_is_no_failure()
    {
        var tree = await MakeTreeAsync(Declared, "1");
        var said = new List<Noted>();

        var outcome = await TreeFilesStep.RunAsync(Home, Root, tree, "engine", said.Add, CancellationToken.None);

        Assert.Equal("x", await File.ReadAllTextAsync(Path.Combine(tree, "local", "regen.txt")));
        Assert.Equal("recorded", await File.ReadAllTextAsync(Path.Combine(tree, "local", "base.txt")));
        Assert.DoesNotContain(StartNoteCodes.StartedPrepFailed.Code, said.SelectMany(each => each.Parts).Select(part => part.Code));
        Assert.Contains("recorded its base", outcome.Told);
    }

    [Fact]
    public async Task A_baseline_that_could_not_run_is_said_and_the_start_goes_on()
    {
        var tree = await MakeTreeAsync(Declared, "2");
        var said = new List<Noted>();

        var outcome = await TreeFilesStep.RunAsync(Home, Root, tree, "engine", said.Add, CancellationToken.None);

        Assert.Contains(StartNoteCodes.StartedPrepFailed.Code, said.SelectMany(each => each.Parts).Select(part => part.Code));
        Assert.False(outcome.Stopped);
        Assert.DoesNotContain("recorded its base", outcome.Told ?? "");
    }
}
