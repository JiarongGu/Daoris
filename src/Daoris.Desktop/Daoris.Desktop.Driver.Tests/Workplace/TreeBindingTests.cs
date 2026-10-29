using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITWORKTREECONFIG1 (D51's note), the fast half: the one check every door that writes through a session's tree asks first
/// (<see cref="TreeBinding"/>), over git standing in at the runner's seam (<see cref="WorkingTree.Runs"/>). Its real-git twin, the
/// incident's configuration on each door, is <see cref="TreeBindingGitTests"/>.
/// </summary>
public sealed class TreeBindingTests : IDisposable
{
    private const string Checkout = "X:/daoris/engine";

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "tree-binding", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _tree;
    private readonly List<string> _asked = [];

    public TreeBindingTests()
    {
        _tree = Path.Combine(_scratch, "trees", "default", "engine", "s-0b0d0e01");
        Directory.CreateDirectory(_tree);
        // A linked tree's own `.git`: the file naming its record, which is what git finds the tree by.
        File.WriteAllText(Path.Combine(_tree, ".git"), $"gitdir: {Checkout}/.git/worktrees/s-0b0d0e01\n");
    }

    public void Dispose()
    {
        WorkingTree.Runs.Value = null;
        GitSeed.Discard(_scratch);
    }

    /// <summary>The working root git reports is the tree's own folder, spelled as git spells it: nothing binds it, one start asked.</summary>
    [Fact]
    public async Task A_tree_git_reports_as_its_own_root_is_not_bound()
    {
        Git(arguments => arguments is ["rev-parse", "--show-toplevel"] ? Said(_tree.Replace('\\', '/').ToUpperInvariant()) : null);

        Assert.Null(await TreeBinding.RefusalAsync(_tree, CancellationToken.None));
        Assert.Equal(["rev-parse --show-toplevel"], _asked);
    }

    /// <summary>
    /// Another folder: the refusal says what git reports, what binds it and where (<c>git config --show-origin</c>'s answer), that
    /// nothing was done, and what the person does. Daoris writes no configuration of its own.
    /// </summary>
    [Fact]
    public async Task A_tree_git_reports_elsewhere_is_refused_saying_what_binds_it()
    {
        Git(arguments => arguments switch
        {
            ["rev-parse", "--show-toplevel"] => Said(Checkout),
            ["config", "--show-origin", "--get", "core.worktree"] => Said($"file:{Checkout}/.git/config\t{Checkout}"),
            _ => null,
        });

        var refused = await TreeBinding.RefusalAsync(_tree, CancellationToken.None);

        Assert.Equal(
            $"git reports {Checkout} as the working root of the tree at {_tree}, not that folder: `core.worktree = {Checkout}` in "
            + $"{Checkout}/.git/config, set by something other than Daoris, binds it there, so nothing was removed, reset or written through "
            + "it. Move that value into the owning checkout's own worktree configuration, or unset it, then ask again",
            refused);
        Assert.Equal(["rev-parse --show-toplevel", "config --show-origin --get core.worktree"], _asked);
        Assert.DoesNotContain(_asked, each => each.StartsWith("config", StringComparison.Ordinal) && !each.Contains("--get", StringComparison.Ordinal));
    }

    /// <summary>Where git names no origin, the sentence says where such a value is, never a file it did not read.</summary>
    [Fact]
    public async Task An_origin_git_does_not_name_is_said_as_the_shared_configuration()
    {
        Git(arguments => arguments switch
        {
            ["rev-parse", "--show-toplevel"] => Said(Checkout),
            ["config", ..] => Failed(1, ""),
            _ => null,
        });

        var refused = await TreeBinding.RefusalAsync(_tree, CancellationToken.None);

        Assert.Contains(": a `core.worktree` in the repository's shared git configuration, set by something other than Daoris, binds it there", refused);
    }

    /// <summary>The working root a door's own start read already (the tree's repository and branch, PROC1d-iii) is compared, and asks no git.</summary>
    [Fact]
    public async Task A_root_read_already_is_compared_with_no_start()
    {
        Git(_ => null);

        Assert.Null(await TreeBinding.RefusalAsync(_tree, CancellationToken.None, _tree.Replace('\\', '/')));
        Assert.Empty(_asked);
    }

    /// <summary>A folder holding no <c>.git</c> is no linked tree: git asked there would answer for whatever holds it, so it is not judged.</summary>
    [Fact]
    public async Task A_folder_holding_no_git_is_not_judged()
    {
        Git(_ => Said(Checkout));
        File.Delete(Path.Combine(_tree, ".git"));

        Assert.Null(await TreeBinding.RefusalAsync(_tree, CancellationToken.None));
        Assert.Null(await TreeBinding.RefusalAsync(_tree, CancellationToken.None, Checkout));
        Assert.Empty(_asked);
    }

    /// <summary>A working root git could not say is no proof the tree is its own: it is kept, said.</summary>
    [Fact]
    public async Task A_root_git_could_not_read_keeps_the_tree()
    {
        Git(_ => Failed(128, "fatal: not a git repository (or any of the parent directories): .git"));

        Assert.Equal(
            $"git could not say where the tree at {_tree} has its working root (fatal: not a git repository (or any of the parent "
            + "directories): .git), so nothing was removed, reset or written through it; ask again",
            await TreeBinding.RefusalAsync(_tree, CancellationToken.None));
    }

    /// <summary>🔴 The ask is git's each time: never the layout fact a landing's table keeps (<see cref="GitFacts"/>).</summary>
    [Fact]
    public async Task The_root_is_asked_of_git_even_where_a_landing_keeps_it()
    {
        Git(arguments => arguments is ["rev-parse", "--show-toplevel"] ? Said(_tree) : null);
        using (GitFacts.Within())
        {
            await WorkingTree.GitAsync(_tree, ["rev-parse", "--show-toplevel"], CancellationToken.None);
            await WorkingTree.GitAsync(_tree, ["rev-parse", "--show-toplevel"], CancellationToken.None);
            Assert.Single(_asked);

            Assert.Null(await TreeBinding.RefusalAsync(_tree, CancellationToken.None));
        }

        Assert.Equal(2, _asked.Count);
    }

    /// <summary>
    /// The tree's repository, its working root and its branch are one start (PROC1d-iii, GITWORKTREECONFIG1); where that start fails
    /// (a branch with no commit yet) the two asks run as before, and no working root is read.
    /// </summary>
    [Fact]
    public async Task A_trees_repository_root_and_branch_are_one_start()
    {
        string[] one = ["rev-parse", "--path-format=absolute", "--git-common-dir", "--show-toplevel", "--abbrev-ref", "HEAD"];
        Git(arguments => arguments.SequenceEqual(one) ? Said($"{Checkout}/.git\n{Checkout}\ndaoris/s-0b0d0e01") : null);

        var here = await TreeGit.CommonDirAndBranchAsync(_tree, CancellationToken.None);

        Assert.Equal((0, $"{Checkout}/.git", Checkout, "daoris/s-0b0d0e01", true), (here.Code, here.CommonDir, here.TopLevel, here.Branch, here.Once));
        Assert.Equal([string.Join(' ', one)], _asked);

        _asked.Clear();
        Git(arguments => arguments switch
        {
            ["rev-parse", "--path-format=absolute", "--git-common-dir"] => Said($"{Checkout}/.git"),
            ["rev-parse", "--abbrev-ref", "HEAD"] => Failed(128, "fatal: ambiguous argument 'HEAD'"),
            _ => Failed(128, "fatal: ambiguous argument 'HEAD'"),
        });

        var unborn = await TreeGit.CommonDirAndBranchAsync(_tree, CancellationToken.None);

        Assert.Equal((0, "", false), (unborn.Code, unborn.TopLevel, unborn.Once));
        Assert.Equal(3, _asked.Count);
    }

    /// <summary>git standing in: each start's arguments kept; <paramref name="answer"/>'s reply, or a usage git does not know.</summary>
    private void Git(Func<IReadOnlyList<string>, ChildRun?> answer) =>
        WorkingTree.Runs.Value = (arguments, _, _) =>
        {
            lock (_asked) _asked.Add(string.Join(' ', arguments));
            return Task.FromResult(answer(arguments) ?? Failed(129, "the stand-in does not answer this"));
        };

    private static ChildRun Said(string stdout) => new(true, 0, stdout + "\n", "", false);

    private static ChildRun Failed(int code, string stderr) => new(true, code, "", stderr + "\n", false);
}
