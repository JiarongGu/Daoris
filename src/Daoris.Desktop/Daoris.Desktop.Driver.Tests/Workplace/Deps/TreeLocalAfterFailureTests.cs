using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A tree's machine-local inputs are copied whatever happens to its dependencies: a dependency step that fails on the disk (a folder
/// Windows holds, a denied rename) leaves the tree without its install, never without the private pattern list and build props its checks
/// read, which would make its checks differ from the merge's. Real git and a real install command; fixtures live under the repository's
/// gitignored <c>_fixtures/</c>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeLocalAfterFailureTests : IDisposable
{
    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "tree-local-failure", Guid.NewGuid().ToString("N")[..8]);
    private string Home => Path.Combine(_scratch, "home");
    private string Root => Path.Combine(_scratch, "engine");

    public void Dispose()
    {
        DependencyCommands.Installing.Value = null;
        GitSeed.Discard(_scratch);
    }

    private async Task<string> MakeTreeAsync()
    {
        Directory.CreateDirectory(Path.Combine(Root, "local"));
        await GitAsync(Root, "init", "-q", "-b", "main");
        await GitAsync(Root, "config", "user.email", "test@example.com");
        await GitAsync(Root, "config", "user.name", "Test");
        await File.WriteAllTextAsync(Path.Combine(Root, ".gitignore"), "a/\nlocal/\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "a.cjs"), "require('fs').mkdirSync('a', { recursive: true });\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "a.lock"), "one\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.files.json"),
            """{ "rules": [ { "kind": "dependency", "paths": ["a/"], "install": "node a.cjs", "key": ["a.lock"] } ] }""");
        await File.WriteAllTextAsync(Path.Combine(Root, "daoris.gates.json"),
            """{ "localInputs": ["local/engine-local.props"], "sensitive": { "patternsFile": "local/sensitive-patterns.txt" } }""");
        await GitAsync(Root, "add", ".");
        await GitAsync(Root, "commit", "-q", "-m", "first");
        await File.WriteAllTextAsync(Path.Combine(Root, "local", "sensitive-patterns.txt"), "quiltmark\n");
        await File.WriteAllTextAsync(Path.Combine(Root, "local", "engine-local.props"), "<Project />\n");
        var tree = Path.Combine(_scratch, "t1");
        await GitAsync(Root, "worktree", "add", "-q", "-b", "daoris/t1", tree, "main");
        TreeFilesRecord.Mark(Home, tree);
        return tree;
    }

    [Fact]
    public async Task A_dependency_step_that_fails_on_the_disk_still_leaves_the_tree_the_checkout_s_private_inputs()
    {
        var tree = await MakeTreeAsync();
        DependencyCommands.Installing.Value = _ => throw new UnauthorizedAccessException("Access to the path 'a' is denied.");
        var said = new List<Noted>();

        await TreeFilesStep.RunAsync(Home, Root, tree, "engine", said.Add, CancellationToken.None);

        var codes = said.SelectMany(each => each.Parts).Select(part => part.Code).ToList();
        Assert.Contains(StartNoteCodes.StartedFilesFailed.Code, codes);
        Assert.Contains(StartNoteCodes.StartedLocalCopied.Code, codes);
        foreach (var input in new[] { "sensitive-patterns.txt", "engine-local.props" })
        {
            var copy = Path.Combine(tree, "local", input);
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(Root, "local", input)), await File.ReadAllBytesAsync(copy));
            Assert.True(File.GetAttributes(copy).HasFlag(FileAttributes.ReadOnly), $"{input} is read-only in the tree");
        }
    }
}
