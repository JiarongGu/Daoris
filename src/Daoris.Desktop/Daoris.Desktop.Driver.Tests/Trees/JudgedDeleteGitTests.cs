using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END4a's second review over real git: a judged branch goes in one ref transaction with what its proof rested on
/// (<c>git update-ref --stdin</c>, <c>verify</c> beside the <c>delete</c>), for every caller of the delete, so a line rewound as it goes
/// keeps the branch with no absence anyone can observe. The one window left, a checkout made after the trees were read, is looked at again
/// once it went: an unread list is not clear, and a put-back git refused is said as such, never as put back.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class JudgedDeleteGitTests : LandedFixture
{
    private static IReadOnlySet<string> NoneInUse { get; } = new HashSet<string>();

    private static Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult(NoneInUse);

    /// <summary>A session's tree with one commit, merged onto the line; its tree removed by hand where asked, so the branch stands alone.</summary>
    private async Task<(string Branch, string Tree, string Tip)> MergedAsync(string root, bool treeless)
    {
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "a.txt", "a\n", "work on a");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", $"merge {opened.Branch}", opened.Branch);
        if (treeless) await GitAsync(root, "worktree", "remove", opened.Path);
        return (opened.Branch, opened.Path, (await GitAsync(root, "rev-parse", opened.Branch)).Trim());
    }

    private static async Task<string?> TipAsync(string root, string branch)
    {
        var listed = (await GitAsync(root, "for-each-ref", "--format=%(objectname)", $"refs/heads/{branch}")).Trim();
        return listed.Length == 0 ? null : listed;
    }

    /// <summary>The line rewound under the delete, after the last proof: git's own verify refuses the transaction.</summary>
    private static Func<string, Task> Rewinds(string root) => async _ => await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~1");

    /// <summary>🔴 The review's P1 (TreeTidies.cs:336): LAND3's door now passes what its proof rested on, so the line rewound as the branch goes keeps it.</summary>
    [Fact]
    public async Task The_doors_removal_keeps_a_branch_whose_line_moved_as_it_went()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, tip) = await MergedAsync(root, treeless: true);
        var trees = new SessionTrees(Home) { BeforeRefDeleted = Rewinds(root) };

        var removal = await trees.RemoveBranchAsync(root, "engine", branch);

        Assert.False(removal.Removed, removal.Message);
        Assert.Equal(tip, await TipAsync(root, branch));
        Assert.Contains("moved as it went", removal.Message, StringComparison.Ordinal);
    }

    /// <summary>🔴 The review's P1 (TreeRemovals.cs:189): a tree's removal keeps its branch where the line moved as it went.</summary>
    [Fact]
    public async Task A_trees_removal_keeps_its_branch_where_the_line_moved_as_it_went()
    {
        var root = await RepositoryAsync("engine");
        var (branch, tree, tip) = await MergedAsync(root, treeless: false);
        var trees = new SessionTrees(Home) { BeforeRefDeleted = Rewinds(root) };

        var removal = await trees.RemoveAsync(tree);

        Assert.Equal(tip, await TipAsync(root, branch));
        Assert.Contains($"kept its branch `{branch}`", removal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The review's P1 (ContentHolds.cs:302): a list of the trees git could not give once the branch went is no answer that no tree
    /// checked it out meanwhile, so the branch is put back.
    /// </summary>
    [Fact]
    public async Task An_unread_tree_list_after_the_delete_puts_the_branch_back()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, tip) = await MergedAsync(root, treeless: true);
        var unread = false;
        var trees = new SessionTrees(Home) { ListFails = _ => unread, BeforeRefDeleted = _ => { unread = true; return Task.CompletedTask; } };

        var removal = await trees.RemoveBranchAsync(root, "engine", branch);

        Assert.False(removal.Removed, removal.Message);
        Assert.Equal(tip, await TipAsync(root, branch));
        Assert.Contains("put back where it was", removal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The review's P1 (ContentHolds.cs:309): a late checkout's put-back git refuses (a lock, a competing creation) is said as such,
    /// never read as put back.
    /// </summary>
    [Fact]
    public async Task A_put_back_git_refuses_is_said_so()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, tip) = await MergedAsync(root, treeless: true);
        var late = Path.Combine(Path.GetDirectoryName(root)!, $"late-{Guid.NewGuid():N}"[..13]);
        var trees = new SessionTrees(Home) { BeforeRefDeleted = async _ => await GitAsync(root, "worktree", "add", "--quiet", late, branch) };
        WorkingTree.Answered.Value = arguments => arguments is ["update-ref", var reference, var at, ""] && reference == $"refs/heads/{branch}" && at == tip
            ? (1, "", $"fatal: cannot lock ref 'refs/heads/{branch}': Unable to create '{branch}.lock': File exists.")
            : null;
        try
        {
            var removal = await trees.RemoveBranchAsync(root, "engine", branch);

            Assert.False(removal.Removed, removal.Message);
            Assert.Contains("could not put it back", removal.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("put back where it was", removal.Message, StringComparison.Ordinal);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }

    // ——— the third review: the proof returns what it rested on, and the delete verifies exactly that

    /// <summary>A session's tree with one commit the line does not hold; its tree removed by hand where asked.</summary>
    private async Task<(string Branch, string Tree, string Tip)> WorkedAsync(string root, bool treeless)
    {
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "a.txt", "a\n", "work on a");
        if (treeless) await GitAsync(root, "worktree", "remove", opened.Path);
        return (opened.Branch, opened.Path, (await GitAsync(root, "rev-parse", opened.Branch)).Trim());
    }

    /// <summary>git run at once, for a person acting between two of Daoris's own git starts (<see cref="WorkingTree.Started"/>).</summary>
    private static void GitNow(string cwd, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo { FileName = "git", WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>
    /// 🔴 The third review's P1 (ContentHolds.cs:279): a branch of the person's held the work by ancestry when it was judged, and was deleted
    /// before the delete. The holder the proof named is gone, so the branch is refused: never authorized by an unchanged line that never held it.
    /// </summary>
    [Fact]
    public async Task A_vanished_ancestry_holder_refuses_the_delete()
    {
        var root = await RepositoryAsync("engine");
        var (branch, tree, tip) = await WorkedAsync(root, treeless: false);
        await GitAsync(root, "branch", "feature/keep", branch);
        // The person deletes their branch as the tree goes: after the judgement, before the branch's delete.
        WorkingTree.Started.Value = arguments =>
        {
            if (arguments is ["worktree", "remove", ..]) GitNow(root, "update-ref", "-d", "refs/heads/feature/keep");
        };
        try
        {
            var removal = await new SessionTrees(Home).RemoveAsync(tree);

            Assert.Equal(tip, await TipAsync(root, branch));
            Assert.Contains($"kept its branch `{branch}`", removal.Message, StringComparison.Ordinal);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }
    }

    /// <summary>
    /// 🔴 The third review's P1 (ContentHolds.cs:170, :269): a branch of the person's holding the work by content (a cherry-pick) is what
    /// the proof rested on, so its moving as the branch goes keeps the branch.
    /// </summary>
    [Fact]
    public async Task A_content_holder_moved_as_it_went_keeps_the_branch()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, tip) = await WorkedAsync(root, treeless: true);
        await CherryPickAsync(root, branch, "feature/pick");
        var trees = new SessionTrees(Home) { BeforeRefDeleted = async _ => await GitAsync(root, "branch", "-f", "feature/pick", "main") };

        var removal = await trees.RemoveBranchAsync(root, "engine", branch);

        Assert.False(removal.Removed, removal.Message);
        Assert.Equal(tip, await TipAsync(root, branch));
        Assert.Contains("feature/pick", removal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The third review's P1 (TreeSquashes.cs:60, the earlier DELETETX2): a squashing landing's record vouches only while its branch stands
    /// at the commit read, so that branch moved as the session branch goes keeps it.
    /// </summary>
    [Fact]
    public async Task A_squash_landings_branch_moved_as_it_went_keeps_the_branch()
    {
        var root = await RepositoryAsync("engine");
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Squash: true))
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the work\n", "the work");
        var landed = await new SessionTrees(Home).LandAsync(opened.Path, new LandingSubject("s1", "0fda18", "Fix the gap"), inUse: InUseAsync);
        Assert.True(landed.Landed, landed.Message);
        var tip = await TipAsync(root, opened.Branch);
        var trees = new SessionTrees(Home) { BeforeRefDeleted = async _ => await GitAsync(root, "update-ref", "-d", $"refs/heads/{landed.Branch}") };

        var removal = await trees.RemoveAsync(opened.Path);

        Assert.Equal(tip, await TipAsync(root, opened.Branch));
        Assert.Contains($"kept its branch `{opened.Branch}`", removal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The third review's P1 (LandedCleanups.cs:410, :294): a landing inside another rests on that one, so that branch moved as it goes
    /// keeps it.
    /// </summary>
    [Fact]
    public async Task An_inside_landings_holder_moved_as_it_went_keeps_it()
    {
        var root = await RepositoryAsync("engine");
        var a = await LandAsync(new SessionTrees(Home), root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"), extra: ("a.txt", "a\n"));
        var b = await LandAsync(new SessionTrees(Home), root, "shared.txt", "one\ntwo\nthree\n", new LandingSubject("s2", "q2", "Second"), from: a.Branch, extra: ("b.txt", "b\n"));
        await SquashAsync(root, b.Branch!);
        var aTip = await TipAsync(root, a.Branch!);
        var trees = new SessionTrees(Home)
        {
            BeforeRefDeleted = async name =>
            {
                if (name == a.Branch) await GitAsync(root, "update-ref", "-d", $"refs/heads/{b.Branch}");
            },
        };

        var done = await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());

        Assert.Equal(aTip, await TipAsync(root, a.Branch!));
        Assert.DoesNotContain(done.Landed, result => result.Removed && result.Item.Branch == a.Branch);
    }

    /// <summary>
    /// 🔴 The third review's P1 (LandedCleanups.cs:276, :289; LandedProofs.cs:94): the line rewound between the proof and the recovery ref is
    /// verified at the commit the proof read, never at the one read after the ref was made, so the landing's branch is kept.
    /// </summary>
    [Fact]
    public async Task A_rewind_between_the_proof_and_the_recovery_ref_keeps_the_landing()
    {
        var root = await RepositoryAsync("engine");
        var b = await LandAsync(new SessionTrees(Home), root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"), extra: ("b.txt", "b\n"));
        var before = (await GitAsync(root, "rev-parse", "main")).Trim();
        await SquashAsync(root, b.Branch!);
        var bTip = await TipAsync(root, b.Branch!);
        var rewound = false;
        WorkingTree.Started.Value = arguments =>
        {
            if (!rewound && arguments is ["update-ref", var reference, ..] && reference == $"{ContentHolds.DiscardedPrefix}{b.Branch}")
            {
                rewound = true;
                GitNow(root, "update-ref", "refs/heads/main", before);
            }
        };
        try
        {
            var done = await new SessionTrees(Home).CleanAsync([("engine", "aurora", root)], new HashSet<string>());

            Assert.True(rewound, "the recovery ref was made");
            Assert.Equal(bTip, await TipAsync(root, b.Branch!));
            Assert.DoesNotContain(done.Landed, result => result.Removed && result.Item.Branch == b.Branch);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }
    }

    /// <summary>
    /// 🔴 The third review's P1 (ContentHolds.cs:360): after a refused transaction, a read of the branch git could not make is unread, never
    /// "gone already": only git's own "no such ref" answer (exit 1) says it went.
    /// </summary>
    [Fact]
    public async Task A_failed_read_after_a_refused_delete_is_unread_not_removed()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, tip) = await MergedAsync(root, treeless: true);
        var refused = false;
        WorkingTree.Answered.Value = arguments =>
        {
            if (arguments is ["update-ref", "--stdin"])
            {
                refused = true;
                return (128, "", "fatal: simulated refusal");
            }

            return refused && arguments is ["rev-parse", "--verify", "--quiet", var reference] && reference == $"refs/heads/{branch}"
                ? (128, "", "fatal: simulated read failure")
                : null;
        };
        try
        {
            var removal = await new SessionTrees(Home).RemoveBranchAsync(root, "engine", branch);

            Assert.True(refused, "the transaction was refused");
            Assert.False(removal.Removed, removal.Message);
            Assert.Equal(tip, await TipAsync(root, branch));
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }
}
