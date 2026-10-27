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
    /// 🔴 DETACHTX1 (TreeRemovals.cs:178, :205): a detached tree's commit is held by the tree's own HEAD and the holder its proof found.
    /// The holder rewound as the tree goes leaves that commit on a recovery ref, kept until a transaction shows the holder where the proof
    /// read it, never on no ref at all.
    /// </summary>
    [Fact]
    public async Task A_detached_trees_removal_keeps_its_commit_where_its_holder_moved_as_it_went()
    {
        var root = await RepositoryAsync("engine");
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await GitAsync(opened.Path, "switch", "--quiet", "--detach");
        await CommitAsync(opened.Path, "a.txt", "a\n", "work on a, detached");
        var judged = (await GitAsync(opened.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the detached work", judged);
        var trees = new SessionTrees(Home);
        // The line rewound as the tree goes, after its proof: the commit is then the tree's alone.
        WorkingTree.Started.Value = arguments =>
        {
            if (arguments is ["worktree", "remove", ..]) GitNow(root, "reset", "--hard", "--quiet", "HEAD~1");
        };
        TreeRemoval removal;
        try
        {
            removal = await trees.RemoveAsync(opened.Path);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        Assert.True(removal.Removed, removal.Message);
        Assert.False(Directory.Exists(opened.Path));
        var holding = (await GitAsync(root, "for-each-ref", "--contains", judged, "--format=%(refname)")).Trim();
        Assert.StartsWith(ContentHolds.DiscardedPrefix, holding, StringComparison.Ordinal);
        Assert.Contains(holding, removal.Message, StringComparison.Ordinal);
    }

    /// <summary>DETACHTX1's other half: a detached tree whose holder stood as it went leaves no recovery ref behind.</summary>
    [Fact]
    public async Task A_detached_trees_removal_whose_holder_stood_keeps_no_recovery_ref()
    {
        var root = await RepositoryAsync("engine");
        var opened = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        await GitAsync(opened.Path, "switch", "--quiet", "--detach");
        await CommitAsync(opened.Path, "a.txt", "a\n", "work on a, detached");
        var judged = (await GitAsync(opened.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge the detached work", judged);

        var removal = await new SessionTrees(Home).RemoveAsync(opened.Path);

        Assert.True(removal.Removed, removal.Message);
        Assert.Empty((await GitAsync(root, "for-each-ref", "--format=%(refname)", ContentHolds.DiscardedPrefix)).Trim());
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
            if (!rewound && arguments is ["update-ref", "--create-reflog", var reference, ..] && reference == $"{ContentHolds.DiscardedPrefix}{b.Branch}")
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

    // ——— the fourth review

    /// <summary>The branch a landing of <c>0fda18</c> titled "Fix the gap" makes, squashing or not.</summary>
    private const string Made = "feature/0fda18-fix-the-gap";

    /// <summary>
    /// A chain of two sessions, the second grown from the first, and the second accepted under a rule that cleans up (LAND3): the first's
    /// branch is the one more the landing's tidy judges. <paramref name="act"/> is told each git start once the pressed session's branch went
    /// (its delete's transaction), as a person acting before the tidy reads the landing's branch.
    /// </summary>
    private async Task<(TreeOpened First, string FirstTip, TreeLanding Landed)> ChainLandedAsync(
        string root, bool squash, Action<IReadOnlyList<string>> act)
    {
        DriverConfig.Empty.WithLanding("engine", new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, Squash: squash))
            .Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var trees = new SessionTrees(Home);
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "the first step\n", "the first step");
        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "the verify step\n", "the verify step");
        var firstTip = (await GitAsync(root, "rev-parse", first.Branch)).Trim();
        var armed = false;
        WorkingTree.Started.Value = arguments =>
        {
            if (armed) act(arguments);
            else if (arguments is ["update-ref", "--stdin"]) armed = true;
        };
        try
        {
            var landed = await trees.LandAsync(second.Path, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: InUseAsync);
            Assert.True(landed.Landed, landed.Message);
            Assert.True(armed, "the pressed session's branch went in a transaction");
            return (first, firstTip, landed);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }
    }

    /// <summary>
    /// 🔴 The fourth review's P1 (TreeTidies.cs:67 and :69, TreeLands.cs:563): LAND3's squash path compares the other branches with the
    /// session tip it squashed, so what its removals rest on is the branch the squash made at a commit holding the one the landing made. That
    /// branch made again elsewhere before the tidy reads it holds none of the squashed work, so the chain's earlier branch stays, and the
    /// landing says the clean-up could not judge it, never that the branch holds its commits.
    /// </summary>
    [Fact]
    public async Task A_squash_holder_replaced_before_the_tidys_read_keeps_the_earlier_branch()
    {
        var root = await RepositoryAsync("engine");
        var unrelated = (await GitAsync(root, "rev-parse", "main")).Trim();
        var replaced = false;
        var (first, firstTip, landed) = await ChainLandedAsync(root, squash: true, arguments =>
        {
            if (!replaced && arguments is ["rev-parse", "--verify", "--quiet", var revision] && revision == $"refs/heads/{Made}^{{commit}}")
            {
                replaced = true;
                GitNow(root, "update-ref", $"refs/heads/{Made}", unrelated);
            }
        });

        Assert.True(replaced, "the landing's branch was made again before the tidy read it");
        Assert.Equal(firstTip, await TipAsync(root, first.Branch));
        var kept = Assert.Single(landed.Tidied!);
        Assert.Equal(first.Branch, kept.Branch);
        Assert.False(kept.Removed);
        Assert.Contains("could not judge", landed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain($"whose commits `{Made}` holds", landed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 The fourth review's P2 (TreeTidies.cs:67, :72 and :225): a landing's branch git could not read leaves every other branch unjudged.
    /// They stay, and the landing says the clean-up could not judge them, where it used to say nothing.
    /// </summary>
    [Fact]
    public async Task An_unread_holder_says_the_clean_up_could_not_judge()
    {
        var root = await RepositoryAsync("engine");
        var unread = false;
        WorkingTree.Answered.Value = arguments =>
            unread && arguments is ["rev-parse", "--verify", "--quiet", var revision] && revision == $"refs/heads/{Made}^{{commit}}"
                ? (128, "", "fatal: simulated read failure")
                : null;
        try
        {
            var (first, firstTip, landed) = await ChainLandedAsync(root, squash: false, _ => unread = true);

            Assert.True(unread, "the landing's branch was unread once the pressed branch went");
            Assert.Equal(firstTip, await TipAsync(root, first.Branch));
            var kept = Assert.Single(landed.Tidied!);
            Assert.Equal(first.Branch, kept.Branch);
            Assert.False(kept.Removed);
            Assert.Contains($"could not judge `{first.Branch}`", landed.Message, StringComparison.Ordinal);
            Assert.DoesNotContain($"whose commits `{Made}` holds", landed.Message, StringComparison.Ordinal);
        }
        finally
        {
            WorkingTree.Answered.Value = null;
        }
    }

    /// <summary>
    /// 🔴 The fourth review's P2 (ContentHolds.cs:376): git's quiet verification exits 1 on any failure to resolve, a broken ref's too, so a
    /// branch broken as its delete was refused is unread, never "gone already": only git's own "missing" says it went.
    /// </summary>
    [Fact]
    public async Task A_broken_ref_after_a_refused_delete_is_unread_not_gone()
    {
        var root = await RepositoryAsync("engine");
        var (branch, _, _) = await MergedAsync(root, treeless: true);
        var loose = Path.Combine([root, ".git", "refs", "heads", .. branch.Split('/')]);
        Assert.True(File.Exists(loose), "the branch is a loose ref");
        var trees = new SessionTrees(Home) { BeforeRefDeleted = async _ => await File.WriteAllTextAsync(loose, "not a commit\n") };

        var removal = await trees.RemoveBranchAsync(root, "engine", branch);

        Assert.False(removal.Removed, removal.Message);
        Assert.Contains("unread", removal.Message, StringComparison.Ordinal);
        Assert.Equal("not a commit\n", await File.ReadAllTextAsync(loose));
    }

    // ——— FABRIC-TIDYHELD-TEST1: the held tidy and the cut-off opening, moved at the delete

    /// <summary>
    /// A chain of two sessions, the second grown from the first, accepted under <paramref name="rule"/> through <see cref="TreeLands"/>: the
    /// first's branch is the one more the held tidy judges (<c>TreeTidies.TidyOneAsync</c>). <paramref name="move"/> runs once, right before
    /// the first's ref delete, after every proof and the trees' read; the pressed session's own delete runs untouched.
    /// </summary>
    private async Task<(TreeOpened First, string FirstTip, TreeLanding Landed)> HeldMovedAsync(string root, LandingRule rule, Func<string, Task> move)
    {
        DriverConfig.Empty.WithLanding("engine", rule).Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        string? target = null;
        var moved = false;
        var trees = new SessionTrees(Home)
        {
            BeforeRefDeleted = async name =>
            {
                if (moved || name != target) return;
                moved = true;
                await move(name);
            },
        };
        var first = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(first.Path, "first.txt", "the first step\n", "the first step");
        var second = await trees.OpenAsync(root, "engine", "aurora", from: first.Branch);
        await CommitAsync(second.Path, "second.txt", "the verify step\n", "the verify step");
        var firstTip = (await GitAsync(root, "rev-parse", first.Branch)).Trim();
        target = first.Branch;

        var landed = await trees.LandAsync(second.Path, new LandingSubject("s2", "0fda18", "Fix the gap"), inUse: InUseAsync);

        Assert.True(landed.Landed, landed.Message);
        Assert.True(moved, "the held tidy reached the earlier branch's delete");
        return (first, firstTip, landed);
    }

    /// <summary>
    /// 🔴 FABRIC-TIDYHELD-TEST1: the ref the held tidy's proof rested on (the landing's branch, the squash's branch, or the line a merge went
    /// into) set back before the landing as the earlier branch goes refuses the delete in git's own transaction: the branch stays at its
    /// tip, and the landing says what moved, never that it went.
    /// </summary>
    [Theory]
    [InlineData(LandingForm.Branch, false)]
    [InlineData(LandingForm.Branch, true)]
    [InlineData(LandingForm.Merge, false)]
    public async Task A_held_branchs_holder_moved_as_it_goes_keeps_it(string form, bool squash)
    {
        var root = await RepositoryAsync("engine");
        var before = (await GitAsync(root, "rev-parse", "main")).Trim();
        var holder = form == LandingForm.Merge ? "main" : Made;
        var rule = form == LandingForm.Merge
            ? new LandingRule(LandingForm.Merge, Tidy: true)
            : new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true, Squash: squash);

        var (first, firstTip, landed) = await HeldMovedAsync(root, rule, async _ => await GitAsync(root, "update-ref", $"refs/heads/{holder}", before));

        Assert.Equal(firstTip, await TipAsync(root, first.Branch));
        var kept = Assert.Single(landed.Tidied!);
        Assert.Equal(first.Branch, kept.Branch);
        Assert.False(kept.Removed);
        Assert.Contains($"`{holder}` moved as it went", kept.Why, StringComparison.Ordinal);
        Assert.Contains($"Kept `{first.Branch}`", landed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Also removed", landed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 FABRIC-TIDYHELD-TEST1: the earlier branch itself moved on as it goes, to a commit the landing does not hold, is no longer the commit
    /// judged: git refuses the delete, and the new commit stays on it.
    /// </summary>
    [Fact]
    public async Task A_held_branch_moved_as_it_goes_keeps_its_new_commit()
    {
        var root = await RepositoryAsync("engine");
        var after = "";

        var (first, firstTip, landed) = await HeldMovedAsync(
            root, new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Tidy: true), async branch =>
            {
                after = (await GitAsync(root, "commit-tree", $"{branch}^{{tree}}", "-p", branch, "-m", "a commit made as it went")).Trim();
                await GitAsync(root, "update-ref", $"refs/heads/{branch}", after);
            });

        Assert.NotEqual(firstTip, after);
        Assert.Equal(after, await TipAsync(root, first.Branch));
        var kept = Assert.Single(landed.Tidied!);
        Assert.False(kept.Removed);
        Assert.Equal(ContentHolds.MovedSince, kept.Why);
    }

    /// <summary>
    /// 🔴 FABRIC-TIDYHELD-TEST1, the cut-off opening (<c>TreeStrays.CutOffAsync</c>): a tree whose opening a kill cut off, its branch held by
    /// the line. Its folder is moved aside, and its branch goes in one transaction with the line at the commit judged, so the line rewound
    /// past it, or the branch moved on, as the branch goes keeps the branch, said beside the move.
    /// </summary>
    [Theory]
    [InlineData("line", "`main` moved as it went")]
    [InlineData("branch", ContentHolds.MovedSince)]
    public async Task A_cut_off_openings_branch_moved_at_its_delete_stays(string what, string said)
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "later.txt", "the line moves on\n", "the line moves on");
        const string branch = "daoris/s-0031408d";
        var cut = DaorisHome.Entry(Home, HomeEntry.Trees, RemoteTarget.Workspace(null), "engine", "s-0031408d");
        await GitAsync(root, "worktree", "add", "--quiet", "-b", branch, cut, "main");
        await GitAsync(root, "worktree", "lock", "--reason", TreeOpenings.GitsOwn, cut);
        var tip = (await GitAsync(root, "rev-parse", branch)).Trim();
        var reached = false;
        var trees = new SessionTrees(Home)
        {
            BeforeRefDeleted = async name =>
            {
                if (name != branch) return;
                reached = true;
                if (what == "line") await GitAsync(root, "reset", "--hard", "--quiet", "HEAD~1");
                else await GitAsync(root, "update-ref", $"refs/heads/{branch}", $"{tip}~1");
            },
        };

        var results = await trees.TidyStraysAsync(root, "engine", null, InUseAsync, DateTimeOffset.UtcNow);

        Assert.True(reached, "the cut-off opening's branch delete was reached");
        var result = Assert.Single(results);
        Assert.Equal((StrayKind.Opening, branch), (result.Kind, result.Branch));
        Assert.True(result.Moved, result.Message);
        Assert.False(Directory.Exists(cut));
        Assert.Contains(said, result.BranchKept, StringComparison.Ordinal);
        Assert.Equal(what == "line" ? tip : (await GitAsync(root, "rev-parse", $"{tip}~1")).Trim(), await TipAsync(root, branch));
    }
}
