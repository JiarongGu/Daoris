using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TREEGONE1 (D51, D131): a session's tree asked for before it goes on, over real git. One git lists stands as it is; one whose
/// folder went while its branch stands is made again in its place on that branch, its commits there; one whose branch went too, or
/// whose branch another tree holds, or that this home never opened, is made nowhere, each said.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeReturnsTests : BranchTidyingFixture
{
    [Fact]
    public async Task A_tree_git_lists_stands_and_nothing_is_said()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");

        var stands = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal(new TreeStanding(TreeStand.Stands, tree.Branch, null), stands);
    }

    [Fact]
    public async Task A_removed_tree_whose_branch_stands_is_made_again_in_its_place_with_its_commits()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        var head = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);

        var remade = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal((TreeStand.Remade, tree.Branch), (remade.Stand, remade.Branch));
        Assert.Equal(NoteCodes.TreeRemade.Code, Assert.Single(remade.Said!.Parts).Code);
        Assert.Equal(tree.Branch, (await GitAsync(tree.Path, "rev-parse", "--abbrev-ref", "HEAD")).Trim());
        Assert.Equal(head, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal(TreeStand.Stands, (await trees.EnsureAsync(root, tree.Path)).Stand);
    }

    /// <summary>
    /// TREELOCAL2 (D216): a tree made again carries what an opened one does: the checkout's declared machine-local files, read-only,
    /// with its bytes and a clean status, the dependencies its checkout installed, and a declared file the checkout lacks said; and the
    /// agent is told what was copied.
    /// </summary>
    [Fact]
    public async Task A_remade_tree_holds_the_same_read_only_copies_and_dependencies_an_opened_one_does()
    {
        var root = await DeclaringRepositoryAsync();
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        var head = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);

        var remade = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal((TreeStand.Remade, tree.Branch), (remade.Stand, remade.Branch));
        var copy = Path.Combine(tree.Path, "local", "sensitive-patterns.txt");
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(root, "local", "sensitive-patterns.txt")), await File.ReadAllBytesAsync(copy));
        Assert.True(File.GetAttributes(copy).HasFlag(FileAttributes.ReadOnly), "the copy is read-only");
        Assert.Equal("", (await GitAsync(tree.Path, "status", "--porcelain", "--untracked-files=all")).Trim());
        Assert.Equal("module.exports = 'left-pad';\n", await File.ReadAllTextAsync(Path.Combine(tree.Path, "node_modules", "left-pad", "index.js")));
        Assert.Equal(head, (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim());
        Assert.Equal("", (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n').FirstOrDefault(line => line.StartsWith("locked", StringComparison.Ordinal)) ?? "");
        var said = remade.Said!;
        Assert.Contains(NoteCodes.TreeRemade.Code, said.Parts.Select(part => part.Code));
        Assert.Contains(NoteCodes.StartedLocalCopied.Code, said.Parts.Select(part => part.Code));
        Assert.Contains("`local/allowed-hosts.txt` was not copied: the repository's gates read it, and your checkout does not hold it.", said.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("installed again", said.Note, StringComparison.Ordinal);
        Assert.Contains("`node_modules`", remade.Told, StringComparison.Ordinal);
        Assert.Contains("read-only, the machine-local files its gates read: `local/sensitive-patterns.txt`", remade.Told, StringComparison.Ordinal);
        Assert.Equal(TreeStand.Stands, (await trees.EnsureAsync(root, tree.Path)).Stand);
    }

    /// <summary>TREELOCAL2: a remake cut off while it copies takes back its copies and its folder, and never the branch holding the session's commits.</summary>
    [Fact]
    public async Task A_remake_cancelled_while_it_copies_takes_back_the_folder_and_keeps_the_branch()
    {
        var root = await DeclaringRepositoryAsync();
        var trees = new SessionTrees(_home);
        var tree = await TreeWithWorkAsync(trees, root);
        var head = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);
        using var closing = new CancellationTokenSource();
        TreeLocalInputs.Copying.Value = _ => closing.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => trees.EnsureAsync(root, tree.Path, closing.Token));
        }
        finally
        {
            TreeLocalInputs.Copying.Value = null;
        }

        Assert.False(Directory.Exists(tree.Path), "the folder it made is left");
        Assert.Equal(head, (await GitAsync(root, "rev-parse", tree.Branch)).Trim());
        Assert.Single((await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n'), line => line.StartsWith("worktree ", StringComparison.Ordinal));
        Assert.Equal(TreeStand.Remade, (await trees.EnsureAsync(root, tree.Path)).Stand);
    }

    /// <summary>A repository whose gates file declares two machine-local inputs, one of them held, and whose checkout installed an npm project.</summary>
    private async Task<string> DeclaringRepositoryAsync()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "config", "core.autocrlf", "false");
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), "*.db\nbuild/\nlocal/\nnode_modules/\n");
        await File.WriteAllTextAsync(Path.Combine(root, "daoris.gates.json"), """{ "localInputs": ["local/allowed-hosts.txt"], "sensitive": { "patternsFile": "local/sensitive-patterns.txt" } }""");
        var lockfile = Path.Combine(root, "package-lock.json");
        await File.WriteAllTextAsync(lockfile, "{\n  \"lockfileVersion\": 3,\n  \"packages\": {\n    \"\": {},\n    \"node_modules/left-pad\": { \"version\": \"1.3.0\" }\n  }\n}\n");
        await GitAsync(root, "add", ".");
        await GitAsync(root, "commit", "--quiet", "-m", "gates and an npm project");
        Directory.CreateDirectory(Path.Combine(root, "local"));
        await File.WriteAllTextAsync(Path.Combine(root, "local", "sensitive-patterns.txt"), "# private names\nquiltmark\n");
        Directory.CreateDirectory(Path.Combine(root, "node_modules", "left-pad"));
        await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "left-pad", "index.js"), "module.exports = 'left-pad';\n");
        var hidden = Path.Combine(root, "node_modules", ".package-lock.json");
        await File.WriteAllTextAsync(hidden, "{}\n");
        File.SetLastWriteTimeUtc(hidden, File.GetLastWriteTimeUtc(lockfile).AddMinutes(1));
        return root;
    }

    [Fact]
    public async Task A_tree_whose_branch_is_gone_too_is_made_nowhere_and_said()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);
        await GitAsync(root, "branch", "-D", tree.Branch);

        var gone = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal((TreeStand.Gone, tree.Branch), (gone.Stand, gone.Branch));
        Assert.Equal(NoteCodes.TreeGone.Code, Assert.Single(gone.Said!.Parts).Code);
        Assert.False(Directory.Exists(tree.Path));
        Assert.DoesNotContain(tree.Branch, await GitAsync(root, "branch", "--list"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tree_whose_branch_another_tree_holds_is_not_made_again()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var tree = await trees.OpenAsync(root, "engine", "default");
        await GitAsync(root, "worktree", "remove", "--force", tree.Path);
        var elsewhere = Path.Combine(_scratch, "elsewhere");
        await GitAsync(root, "worktree", "add", "--quiet", elsewhere, tree.Branch);

        var unmade = await trees.EnsureAsync(root, tree.Path);

        Assert.Equal(TreeStand.Unmade, unmade.Stand);
        Assert.Contains("is checked out in", unmade.Said!.Note, StringComparison.Ordinal);
        Assert.False(Directory.Exists(tree.Path));
    }

    [Fact]
    public async Task A_folder_this_home_never_opened_is_never_made()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);

        var unmade = await trees.EnsureAsync(root, Path.Combine(_scratch, "not-a-tree"));

        Assert.Equal((TreeStand.Unmade, (string?)null), (unmade.Stand, unmade.Branch));
        Assert.False(Directory.Exists(Path.Combine(_scratch, "not-a-tree")));
    }
}
