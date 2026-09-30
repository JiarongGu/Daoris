using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEW2 (D113): the landing record keeps a trace of a branch once it is gone, so the review of the session that
/// landed it can still say where its work went; and what that review says, in words. Files only, so this is the
/// suite's fast half; the review over real git is <see cref="LandedReviewTests"/>.
/// </summary>
public sealed class LandedRecordTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-landed-record-" + Guid.NewGuid().ToString("N")[..8]);

    public LandedRecordTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static LandedBranch Entry(string branch, string session, string repository = "engine") =>
        new(repository, "aurora", branch, "main", "tip00001", session, "q1", "Fix the gap", At) { From = "from0001" };

    /// <summary>A branch the clean-up removed is no longer judged or handed on, and its session still finds where its work landed.</summary>
    [Fact]
    public void A_removed_branch_leaves_a_trace_its_session_finds_and_nothing_else_judges()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Entry("feature/q1-fix", "s1a2b3c4"));
        landings.Record(Entry("feature/q2-other", "s9f8e7d6"));

        landings.Removed("ENGINE", "feature/q1-fix", LandedKind.OnLine, "origin/main");

        // Standing only, for the clean-up, the hand-off and Ask Daoris, as before.
        Assert.Equal(["feature/q2-other"], landings.All().Select(entry => entry.Branch));
        Assert.Null(landings.Of("engine", "feature/q1-fix"));
        Assert.Empty(landings.Find("s1a2b3c4"));
        // The session's own review still finds it, gone, with what the clean-up proved.
        var trace = landings.Landing("s1a2b3c4")!;
        Assert.Equal("feature/q1-fix", trace.Branch);
        Assert.NotNull(trace.GoneAt);
        Assert.Equal(LandedKind.OnLine, trace.RemovedAs);
        Assert.Equal("origin/main", trace.RemovedOn);
        Assert.Equal("from0001", trace.From);
        Assert.Null(landings.Landing("s9f8e7d6")!.GoneAt);
    }

    /// <summary>A branch found gone, or no longer the landing's, is a trace with nothing proven: Daoris did not remove it.</summary>
    [Fact]
    public void A_branch_found_gone_is_a_trace_with_no_removal_said()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Entry("feature/q1-fix", "s1a2b3c4"));
        landings.Record(Entry("feature/q1-fix", "s5", repository: "game"));

        landings.Gone("engine", ["feature/q1-fix"]);

        var trace = landings.Landing("s1a2b3c4")!;
        Assert.NotNull(trace.GoneAt);
        Assert.Null(trace.RemovedAs);
        Assert.Null(trace.RemovedOn);
        // One repository only.
        Assert.NotNull(landings.Of("game", "feature/q1-fix"));
    }

    /// <summary>
    /// A later landing of the same name stands on its own and leaves the earlier session's trace, so each session's
    /// review says its own landing; the newest landing of a session is the one its review says.
    /// </summary>
    [Fact]
    public void A_new_landing_of_a_gone_name_leaves_the_earlier_trace_and_a_session_means_its_newest()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Entry("feature/q1-fix", "s1"));
        landings.Removed("engine", "feature/q1-fix", LandedKind.Merged, "main");
        landings.Record(Entry("feature/q1-fix", "s2") with { Tip = "tip00002" });

        Assert.Equal("tip00002", landings.Of("engine", "feature/q1-fix")!.Tip);
        Assert.NotNull(landings.Landing("s1")!.GoneAt);
        Assert.Null(landings.Landing("s2")!.GoneAt);

        // A session carried on and landed again once its first branch went: its review says the newer landing.
        landings.Removed("engine", "feature/q1-fix", LandedKind.OnLine, "main");
        landings.Record(Entry("feature/q1-again", "s2") with { Tip = "tip00003" });
        Assert.Equal("feature/q1-again", landings.Landing("S2")!.Branch);
    }

    /// <summary>A push and a replay change the standing branch only: a trace is a fact about a branch that is gone.</summary>
    [Fact]
    public void A_push_or_a_replay_moves_the_standing_entry_and_never_a_trace()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Entry("feature/q1-fix", "s1"));
        landings.Gone("engine", ["feature/q1-fix"]);
        landings.Record(Entry("feature/q1-fix", "s2"));

        landings.Moved("engine", "feature/q1-fix", "tip00009", "from0009");
        landings.Pushed("engine", "feature/q1-fix", new PluginLanding("example.lands", true, "https://example.test/pr/7", "pushed it.", false), "tip00009");

        var trace = landings.Landing("s1")!;
        Assert.Equal("tip00001", trace.Tip);
        Assert.False(trace.Pushed);
        var standing = landings.Of("engine", "feature/q1-fix")!;
        Assert.Equal("tip00009", standing.Tip);
        Assert.Equal("https://example.test/pr/7", standing.PullRequest);
    }

    /// <summary>The trace is written and read back whole, and a record written before traces reads as standing.</summary>
    [Fact]
    public void A_trace_round_trips_and_an_older_record_reads_as_standing()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Entry("feature/q1-fix", "s1"));
        landings.Removed("engine", "feature/q1-fix", LandedKind.Inside, "feature/q2-other");

        var text = File.ReadAllText(Path.Combine(_home, LandedBranches.FileName));
        Assert.Contains("\"goneAt\"", text);
        Assert.Contains("\"removedAs\": \"inside\"", text);
        Assert.Contains("\"removedOn\": \"feature/q2-other\"", text);
        Assert.DoesNotContain("\r", text);
        var again = new LandedBranches(_home).Landing("s1")!;
        Assert.Equal(LandedKind.Inside, again.RemovedAs);

        File.WriteAllText(Path.Combine(_home, LandedBranches.FileName), """
            { "branches": [ { "repository": "engine", "branch": "feature/q3", "tip": "abcd1234", "session": "s3",
                              "landedAt": "2026-09-30T12:00:00.0000000+00:00" } ] }
            """);
        var older = Assert.Single(new LandedBranches(_home).All());
        Assert.Null(older.GoneAt);
        Assert.Equal(older, new LandedBranches(_home).Landing("s3"));
    }

    // ——— what the review says, and when it reads as landed

    /// <summary>
    /// The review reads as landed while the landed branch stands, or once the tree is gone; a tree still here after its
    /// branch went is a session that may have carried on, and its review is the tree's again.
    /// </summary>
    [Theory]
    [InlineData(LandedState.Standing, false, true)]
    [InlineData(LandedState.Standing, true, true)]
    [InlineData(LandedState.Gone, true, true)]
    [InlineData(LandedState.Gone, false, false)]
    [InlineData(LandedState.NotOurs, false, false)]
    [InlineData(LandedState.NoCheckout, true, true)]
    public void The_review_reads_as_landed_while_its_branch_stands_or_its_tree_is_gone(string state, bool treeGone, bool landed) =>
        Assert.Equal(landed, new LandedReview(Entry("feature/q1-fix", "s1"), state).ReadsAsLanded(treeGone));

    /// <summary>The terminal's sentence: the branch, when, in which repository, and where that branch stands — each state its own.</summary>
    [Fact]
    public void Each_state_of_a_landed_branch_is_said_in_its_own_sentence()
    {
        var entry = Entry("feature/q1-fix", "s1a2b3c4");
        string Say(LandedReview review) => LandedReviewWords.Describe("s1a2b3c4", review);

        Assert.Equal("`s1a2b3c4` landed on `feature/q1-fix` in `engine` on 2026-09-30 12:00 UTC; that branch still stands.",
            Say(new LandedReview(entry, LandedState.Standing)));
        Assert.EndsWith("still stands. Its pull request: https://example.test/pr/7",
            Say(new LandedReview(entry with { Pushed = true, PullRequest = "https://example.test/pr/7" }, LandedState.Standing)));
        Assert.Contains("no longer holds the commit the landing made it at", Say(new LandedReview(entry, LandedState.NotOurs)));
        Assert.EndsWith("`engine` has no checkout on this machine to read it in.", Say(new LandedReview(entry, LandedState.NoCheckout)));

        var gone = entry with { GoneAt = At, RemovedAs = LandedKind.OnLine, RemovedOn = "origin/main" };
        Assert.Equal(
            "`s1a2b3c4` landed on `feature/q1-fix` in `engine` on 2026-09-30 12:00 UTC; that branch is gone now. "
            + "The clean-up removed it once its work read on `origin/main`. Its work reads on `main`.",
            Say(new LandedReview(gone, LandedState.Gone, Reads: new LandedReads(LandedKind.OnLine, "main", [], null))));
        Assert.EndsWith("inside `feature/q2-other`, whose work read on the line.",
            Say(new LandedReview(gone with { RemovedAs = LandedKind.Inside, RemovedOn = "feature/q2-other" }, LandedState.Gone)));
        Assert.EndsWith("that branch is gone now. 2 files it changed read otherwise on `main`: a.ts, b.ts.",
            Say(new LandedReview(entry, LandedState.Gone, Reads: new LandedReads(LandedKind.Differs, "main", ["a.ts", "b.ts"], null))));
        Assert.EndsWith("whether its work reads on the line cannot be said.",
            Say(new LandedReview(entry, LandedState.Gone, Reads: new LandedReads(LandedReads.CommitsGone, null, [], null))));
        Assert.EndsWith("on the line: git said so",
            Say(new LandedReview(entry, LandedState.Gone, Reads: new LandedReads(LandedKind.Unknown, null, [], "git said so"))));
        // A landing whose moment did not read says no moment rather than the year one.
        Assert.StartsWith("`s1a2b3c4` landed on `feature/q1-fix` in `engine`; ",
            Say(new LandedReview(entry with { LandedAt = DateTimeOffset.MinValue }, LandedState.Standing)));
    }

    /// <summary>
    /// The terminal's press on a session whose review reads as landed (`trees land`): where it landed, and why nothing
    /// lands again; and the hand-off's door where the branch stands and nothing pushed it.
    /// </summary>
    [Fact]
    public void Landing_again_says_where_it_landed_and_why_nothing_lands_again()
    {
        var entry = Entry("feature/q1-fix", "s1a2b3c4");

        var tidied = LandedReviewWords.NotAgain("s1a2b3c4", new LandedReview(entry, LandedState.Standing), treeGone: true);
        Assert.StartsWith("`s1a2b3c4` landed on `feature/q1-fix` in `engine`", tidied);
        Assert.Contains("that branch still stands. Nothing was landed again: its tree is gone.", tidied);
        Assert.EndsWith("`daoris-driver trees hand s1a2b3c4` hands that branch to a landing plugin.", tidied);

        var kept = LandedReviewWords.NotAgain("s1a2b3c4", new LandedReview(entry with { Pushed = true, PullRequest = "https://example.test/pr/7" }, LandedState.Standing), treeGone: false);
        Assert.EndsWith("Its pull request: https://example.test/pr/7 Nothing was landed again: a second landing is refused while that branch stands.", kept);

        var gone = LandedReviewWords.NotAgain("s1a2b3c4", new LandedReview(entry with { GoneAt = At }, LandedState.Gone), treeGone: true);
        Assert.EndsWith("that branch is gone now. Nothing was landed again: its tree is gone.", gone);
    }

    /// <summary>A tree is gone where its record names none, its folder is not there, or only an empty folder is left (D109 §5).</summary>
    [Fact]
    public void A_tree_is_gone_where_nothing_or_only_an_empty_folder_is_left()
    {
        var tree = Path.Combine(_home, "trees", "aurora", "engine", "s-1");
        Assert.True(SessionTrees.TreeGone(null));
        Assert.True(SessionTrees.TreeGone(tree));
        Directory.CreateDirectory(tree);
        Assert.True(SessionTrees.TreeGone(tree));
        File.WriteAllText(Path.Combine(tree, ".git"), "gitdir: elsewhere\n");
        Assert.False(SessionTrees.TreeGone(tree));
    }
}
