using System.Text.Json;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTOTIDY2a and AUTOTIDY2b over real git: the look waits for the line to hold what Accept landed on a branch of the person's,
/// and takes the branches landings made once it does, after their sessions' branches. Split from
/// <see cref="BranchTidyingProcessTests"/> (PROC1g), and kept out of the git half: its looks judge the most branches of the tidy's
/// classes, and with a gate set beside it one ran past the fixture's four-minute <c>Bound</c> alone, so four at once there would
/// break it. Three classes of one look each (PROCPAR1): a class runs its tests one after another, and the three in one were the
/// Process half's longest class, three minutes the half waited on; eight at once with the half's other classes, a look took about
/// a minute, as it did one class at a time.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class BranchTidyingLandingTests : BranchTidyingFixture
{
    /// <summary>
    /// 🔴 AUTOTIDY2a, the look waits for the line: Accept's landing put each session's commits on a branch of the person's, its
    /// pull request still open, so the line does not hold them and the person may still ask that session to change them in its
    /// own tree. The look keeps each tree and branch, saying nothing, though the press would take them. Once the line has the
    /// commits, merged into its own branch, or only into origin's copy as a fetch leaves a platform's merge nobody pulled yet,
    /// the next look takes each.
    /// </summary>
    [Fact]
    public async Task A_branch_landed_on_a_branch_of_the_person_s_waits_for_the_line()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var merged = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/q1-merged", merged.Branch);
        var fetched = await TreeWithWorkAsync(trees, root);
        await GitAsync(root, "branch", "feature/q2-fetched", fetched.Branch);
        _ledger.Register("engine", root);
        var tidying = new BranchTidying(_home) { Pace = TimeSpan.Zero };
        var plan = await trees.SweepPlanAsync([("engine", "default", root)], new HashSet<string>());
        Assert.All(plan, item => Assert.True(item is { Kind: SweepKind.Landed, Removable: true }, item.Branch));

        var early = await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Empty(early);
        var branches = await GitAsync(root, "branch", "--list", "daoris/*");
        foreach (var each in new[] { merged, fetched })
        {
            Assert.Contains(each.Branch, branches, StringComparison.Ordinal);
            Assert.True(Directory.Exists(each.Path), each.Path);
        }

        // The first is merged into the line's own branch; the second reaches only origin's copy, as a platform's fast-forward a
        // fetch brought here before anyone pulled it.
        await GitAsync(root, "update-ref", "refs/remotes/origin/main", "refs/heads/feature/q2-fetched");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", "feature/q1-merged");

        var next = await tidying.LookAsync(_ledger.Client(), null, _closing.Token).WaitAsync(Bound);

        Assert.Equal(2, next.Count);
        Assert.Contains(next, line => line.StartsWith(Removed(merged), StringComparison.Ordinal));
        Assert.Contains(next, line => line.StartsWith(
            $"tidy  engine: removed `{fetched.Branch}`, since its work is on `origin/main`; its tree's folder was moved aside to ", StringComparison.Ordinal));
        var after = await GitAsync(root, "branch", "--list", "daoris/*");
        Assert.DoesNotContain(merged.Branch, after, StringComparison.Ordinal);
        Assert.DoesNotContain(fetched.Branch, after, StringComparison.Ordinal);
    }
}

/// <summary>The landing branches a look takes, a class of their own look (PROCPAR1, <see cref="BranchTidyingLandingTests"/>).</summary>
[Trait(Category.Name, Category.Process)]
public sealed class BranchTidyingLandingBranchesTests : BranchTidyingFixture
{
    /// <summary>
    /// 🔴 AUTOTIDY2b, the row's own case: the branches Accept's landings made (D102's group) go by the look once the line holds
    /// their work, after their sessions' branches, as the press takes them: one merged into the line, and one squash-merged, its
    /// commits kept on a ref of their own. Each is said in the report by its name, and in the machine log by its session alone,
    /// since its name carries the title's words. One whose pull request is still open stays, with its session's branch, saying
    /// nothing. The session branch of each the line holds goes first, its work on the line through the landing's branch.
    /// </summary>
    [Fact]
    public async Task The_look_takes_the_landing_branches_the_line_holds_after_their_session_branches()
    {
        var root = await RepositoryAsync("engine");
        var trees = LandingTrees();
        var merged = await LandingAsync(trees, root, new LandingSubject("s1", "q1", "Merged"));
        await GitAsync(root, "merge", "--no-ff", "--no-edit", merged.Branch);
        var squashed = await LandingAsync(trees, root, new LandingSubject("s2", "q2", "Squashed"));
        await SquashAsync(root, squashed.Branch);
        var open = await LandingAsync(trees, root, new LandingSubject("s3", "q3", "Open"));
        _ledger.Register("engine", root);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var branches = await GitAsync(root, "branch", "--list");
        foreach (var gone in new[] { merged, squashed })
        {
            Assert.DoesNotContain(gone.Branch, branches, StringComparison.Ordinal);
            Assert.DoesNotContain(gone.Tree.Branch, branches, StringComparison.Ordinal);
        }

        Assert.Contains(open.Branch, branches, StringComparison.Ordinal);
        Assert.Contains(open.Tree.Branch, branches, StringComparison.Ordinal);
        var discarded = (await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/")).Trim();
        Assert.Equal($"refs/daoris/discarded/{squashed.Branch}", discarded);
        Assert.Equal(4, said.Count);
        Assert.Equal(2, said.Count(line => line.Contains("a branch a landing made", StringComparison.Ordinal)));
        Assert.Contains($"tidy  engine: removed `{merged.Branch}`, a branch a landing made, since its work is on `main`.", said);
        Assert.Contains(
            $"tidy  engine: removed `{squashed.Branch}`, a branch a landing made, since its work is on `main` by content (each file it changed reads "
            + $"the same there). {ContentHold.KeptAt(squashed.Branch, discarded)}",
            said);
        Assert.DoesNotContain(said, line => line.Contains(open.Branch, StringComparison.Ordinal) || line.Contains(open.Tree.Branch, StringComparison.Ordinal));

        var tidied = Events("branch.tidied");
        Assert.Equal(4, tidied.Length);
        var landings = tidied.Where(data => data.TryGetProperty("session", out _)).ToList();
        Assert.Equal(
            [("s1", LandedKind.Merged), ("s2", LandedKind.OnLine)],
            landings.Select(data => (data.GetProperty("session").GetString(), data.GetProperty("kind").GetString())).Order());
        Assert.All(landings, data => Assert.Equal(JsonValueKind.Null, data.GetProperty("branch").ValueKind));
        Assert.Empty(Events("branch.kept"));
        Assert.DoesNotContain("feature/", string.Join('\n', Directory.EnumerateFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").SelectMany(StubFile.Lines)),
            StringComparison.Ordinal);
        var record = new LandedBranches(_home);
        Assert.Equal(LandedKind.Merged, record.Landing("s1")!.RemovedAs);
        Assert.Equal(LandedKind.OnLine, record.Landing("s2")!.RemovedAs);
        Assert.Null(record.Landing("s3")!.GoneAt);
    }
}

/// <summary>The landing branches a look keeps, a class of their own look (PROCPAR1, <see cref="BranchTidyingLandingTests"/>).</summary>
[Trait(Category.Name, Category.Process)]
public sealed class BranchTidyingLandingKeptTests : BranchTidyingFixture
{
    /// <summary>
    /// AUTOTIDY2b: a landing's branch whose work the line holds stays from the look, as the press keeps it, saying nothing, where it
    /// is checked out in a tree of the person's, holds a commit its remote lacks (pushed, then moved), or a session branch a session
    /// still works in leans on it. The session branch of each of the first two goes, its work on the line through that branch.
    /// </summary>
    [Fact]
    public async Task A_landing_branch_checked_out_moved_since_its_push_or_leaned_on_stays_from_the_look()
    {
        var root = await RepositoryAsync("engine");
        var origin = Path.Combine(_scratch, "engine-origin.git");
        Directory.CreateDirectory(origin);
        await GitAsync(origin, "init", "--quiet", "--bare", "-b", "main");
        await GitAsync(root, "remote", "add", "origin", origin);
        await GitAsync(root, "push", "--quiet", "-u", "origin", "main");
        var trees = LandingTrees();
        var checkedOut = await LandingAsync(trees, root, new LandingSubject("s4", "q4", "Checked out"));
        await SquashAsync(root, checkedOut.Branch);
        await GitAsync(root, "worktree", "add", "--quiet", Path.Combine(_scratch, "elsewhere"), checkedOut.Branch);
        var pushed = await LandingAsync(trees, root, new LandingSubject("s5", "q5", "Pushed"));
        await GitAsync(root, "push", "--quiet", "-u", "origin", pushed.Branch);
        await CommitOnAsync(root, pushed.Branch);
        await SquashAsync(root, pushed.Branch);
        var leaned = await LandingAsync(trees, root, new LandingSubject("s6", "q6", "Leaned on"));
        await SquashAsync(root, leaned.Branch);
        _ledger.Register("engine", root);
        _ledger.Working("s-leaned", "engine", leaned.Tree.Path);
        using var log = Log();

        var said = await new BranchTidying(_home).LookAsync(_ledger.Client(), log, _closing.Token).WaitAsync(Bound);

        var branches = await GitAsync(root, "branch", "--list");
        foreach (var kept in new[] { checkedOut, pushed, leaned }) Assert.Contains(kept.Branch, branches, StringComparison.Ordinal);
        Assert.Contains(leaned.Tree.Branch, branches, StringComparison.Ordinal);
        Assert.Equal(2, said.Count);
        foreach (var gone in new[] { checkedOut, pushed })
        {
            Assert.DoesNotContain(gone.Tree.Branch, branches, StringComparison.Ordinal);
            Assert.Contains(said, line => line.StartsWith($"tidy  engine: removed `{gone.Tree.Branch}`, since its work is on `main`; ", StringComparison.Ordinal));
        }

        Assert.DoesNotContain(said, line => line.Contains("feature/", StringComparison.Ordinal));
        Assert.Empty((await GitAsync(root, "for-each-ref", "--format=%(refname)", "refs/daoris/discarded/")).Trim());
        Assert.Empty(Events("branch.kept"));
        Assert.DoesNotContain(Events("branch.tidied"), data => data.TryGetProperty("session", out _));
    }
}
