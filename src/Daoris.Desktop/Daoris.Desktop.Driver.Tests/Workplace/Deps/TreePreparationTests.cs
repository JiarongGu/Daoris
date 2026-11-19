using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a repository declares to prepare a tree: a session's tree is handed without it, its session told the commands; a verification
/// tree is prepared by no declared command: a check runs the <c>regenerate</c> of its gates (<c>VerifyExecutionRegenerateProcessTests</c>). Real git and real commands; fixtures live under the
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

}
