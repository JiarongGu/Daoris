using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A sync's list asks git for a checkout's line once, and its git starts are counted (PROC1d-i): the product's own starts are
/// most of the git starts of the slowest Process tests (PROC1a), and the list guessed the line three times a repository.
/// </summary>
/// <remarks>
/// The counter is <see cref="WorkingTree.Started"/>, an async local that is null in production, so classes running in
/// parallel never see each other's starts.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TreeSyncStartsTests : TreeSyncFixture
{
    private const int Branches = 3;

    [Fact]
    public async Task A_sync_plan_asks_for_the_line_once_and_its_starts_are_counted()
    {
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var guessed = 0;
        var trees = new SessionTrees(Home)
        {
            CheckoutLine = (at, ct) =>
            {
                Interlocked.Increment(ref guessed);
                return WorkingTree.DefaultBranchAsync(at, ct);
            },
        };
        for (var each = 0; each < Branches; each++)
        {
            var tree = await trees.OpenAsync(root, "engine", "aurora");
            await CommitAsync(tree.Path, $"work{each}.txt", "work\n", $"session {each}");
        }
        await PlatformCommitAsync(origin, "other.txt", "other\n", "Someone else's (#8)");
        Interlocked.Exchange(ref guessed, 0);

        var starts = new List<string>();
        WorkingTree.Started.Value = arguments => { lock (starts) starts.Add(string.Join(' ', arguments)); };
        try
        {
            await trees.SyncPlanAsync(Repositories(root), Nobody);
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        var report = $"{starts.Count} git starts: {string.Join(" | ", starts)}";
        Assert.True(guessed == 1, $"the line was guessed {guessed} times; {report}");
        // PROC1d-ii: a branch's tip, the line's forms and an upstream come from one read of the refs per judgement, so asking
        // git for one ref at a time is left only to the line's guess (`rev-parse --verify refs/heads/main`), whatever N is.
        Assert.True(starts.Count(each => each.StartsWith("rev-parse --verify", StringComparison.Ordinal)) <= 1, report);
        // 44 starts before the look (two guesses of the line), 42 after, over three session branches; PROC1d-iii cuts the rest.
        Assert.True(starts.Count is > 0 and <= 42, report);
    }

    /// <summary>
    /// The snapshot answers as git does over a real checkout (PROC1d-ii): a branch on origin with its upstream, one whose upstream
    /// is gone, one tracking a branch here, one pushed under another name, a session branch, a name nobody has, and a name only
    /// case tells from the line's. One read answers them all.
    /// </summary>
    [Fact]
    public async Task A_snapshot_answers_every_read_as_git_does()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "work.txt", "work\n", "session work");
        await GitAsync(root, "branch", "feature/pushed");
        await GitAsync(root, "push", "--quiet", "-u", "origin", "feature/pushed");
        await CommitOnAsync(root, "feature/pushed", "later.txt", "later\n", "after the push");
        await GitAsync(root, "branch", "feature/gone");
        await GitAsync(root, "push", "--quiet", "-u", "origin", "feature/gone");
        await GitAsync(root, "push", "--quiet", "origin", "--delete", "feature/gone");
        await GitAsync(root, "branch", "--track", "feature/local-up", "main");
        await GitAsync(root, "branch", "feature/renamed");
        await GitAsync(root, "push", "--quiet", "-u", "origin", "feature/renamed:other-name");
        string[] names = ["main", tree.Branch, "feature/pushed", "feature/gone", "feature/local-up", "feature/renamed", "feature/none", "MAIN"];

        async Task<List<string>> ReadAsync(RefSnapshot? refs)
        {
            var said = new List<string>();
            foreach (var name in names)
            {
                var tip = await TreeRefs.CommitOfAsync(root, $"refs/heads/{name}", CancellationToken.None, refs);
                said.Add($"{name}: tip {tip}");
                said.Add($"{name}: origin {await TreeRefs.CommitOfAsync(root, $"refs/remotes/origin/{name}", CancellationToken.None, refs)}");
                said.Add($"{name}: comparable {await TreeGit.ComparableAsync(root, name, CancellationToken.None, refs)}");
                said.Add($"{name}: forms {string.Join(',', await LandedProofs.LineFormsAsync(root, name, CancellationToken.None, refs))}");
                said.Add($"{name}: on remote {await TreeRefs.OnRemoteAsync(root, name, CancellationToken.None, refs)}");
                if (tip is not null) said.Add($"{name}: ahead {await LandedCleanups.AheadOfRemoteAsync(root, name, tip, CancellationToken.None, refs)}");
            }

            said.Add($"sessions {string.Join(',', await LandedProofs.SessionBranchesAsync(root, CancellationToken.None, refs))}");
            return said;
        }

        var asked = await ReadAsync(null);
        var starts = new List<string>();
        WorkingTree.Started.Value = arguments => { lock (starts) starts.Add(string.Join(' ', arguments)); };
        List<string> snapshot;
        try
        {
            snapshot = await ReadAsync(RefSnapshot.Take(root));
        }
        finally
        {
            WorkingTree.Started.Value = null;
        }

        Assert.Equal(asked, snapshot);
        Assert.Contains("feature/pushed: ahead 1", asked);
        Assert.Contains("feature/gone: on remote False", asked);
        Assert.Single(starts, each => each.StartsWith("for-each-ref", StringComparison.Ordinal));
    }

    /// <summary>A write in the judgement's own flow drops what it read (PROC1d-ii): the next question reads the ref as it is now.</summary>
    [Fact]
    public async Task A_write_in_the_flow_makes_the_next_read_fresh()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var tree = await new SessionTrees(Home).OpenAsync(root, "engine", "aurora");
        var grown = await RevAsync(root, tree.Branch);
        await CommitAsync(tree.Path, "work.txt", "work\n", "session work");
        var worked = await RevAsync(root, tree.Branch);
        var refs = RefSnapshot.Take(root);
        Assert.Equal(worked, await TreeRefs.CommitOfAsync(root, $"refs/heads/{tree.Branch}", CancellationToken.None, refs));

        var (code, _, err) = await WorkingTree.GitAsync(root, ["update-ref", $"refs/heads/{tree.Branch}", grown, worked], CancellationToken.None);

        Assert.True(code == 0, err);
        Assert.Equal(grown, await TreeRefs.CommitOfAsync(root, $"refs/heads/{tree.Branch}", CancellationToken.None, refs));
    }

    /// <summary>
    /// 🔴 D88: each removal is judged again right before it goes, from the refs as they are then. Two landed branches whose work
    /// is on the line; while the first is deleted the second moves, and its own judgement again sees it and keeps it, never
    /// reaching its delete.
    /// </summary>
    [Fact]
    public async Task A_branch_that_moves_after_the_judgement_is_kept_by_the_judgement_again()
    {
        var root = await RepositoryAsync("engine");
        var first = await LandAsync(new SessionTrees(Home), root, "first.txt", "first\n", new LandingSubject("s1", "q1", "First"));
        var second = await LandAsync(new SessionTrees(Home), root, "second.txt", "second\n", new LandingSubject("s2", "q2", "Second"));
        await SquashAsync(root, first.Branch!);
        await SquashAsync(root, second.Branch!);
        var reached = new List<string>();
        var trees = new SessionTrees(Home)
        {
            // The session branches the landings left go first, by the same delete; only the landed two are counted here.
            BeforeDeleting = async branch =>
            {
                if (branch != first.Branch && branch != second.Branch) return;
                reached.Add(branch);
                if (reached.Count == 1) await CommitOnAsync(root, branch == first.Branch ? second.Branch! : first.Branch!, "late.txt", "late\n", "arrived meanwhile");
            },
        };

        var done = await trees.CleanAsync(Repositories(root), Nobody);

        var gone = Assert.Single(reached);
        var moved = gone == first.Branch ? second.Branch! : first.Branch!;
        Assert.Contains(done.Landed, result => result.Item.Branch == gone && result.Removed);
        var kept = Assert.Single(done.Landed, result => result.Item.Branch == moved);
        Assert.False(kept.Removed, kept.Message);
        Assert.Contains("changed since the list", kept.Message);
        Assert.Equal(["arrived meanwhile"], await SubjectsAsync(root, $"{moved}~1..{moved}"));
    }

    [Fact]
    public async Task Without_a_counter_nothing_is_recorded_and_a_start_still_answers()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");

        Assert.Null(WorkingTree.Started.Value);
        Assert.Equal("main", await WorkingTree.DefaultBranchAsync(root, CancellationToken.None));
    }
}
