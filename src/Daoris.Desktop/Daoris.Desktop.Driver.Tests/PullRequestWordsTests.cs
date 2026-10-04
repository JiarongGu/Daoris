using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PLUGHOOK1c (D148 point 6, the plugin hooks design §2.3–§2.5): the kept answer said in the driver's words, one way for every
/// terminal door that reads it (the clean-up's rows and bringing up to date's, <c>trees land --plan</c>, <c>trees state</c>,
/// <c>git branches</c>, <c>trace</c> and Ask Daoris's room): its state, when and by which plugin, what keeps a branch it does not
/// clear, a failed ask beside it, and why nothing was asked.
/// </summary>
public sealed class PullRequestWordsTests
{
    private static readonly DateTimeOffset Asked = new(2026, 10, 4, 14, 2, 0, TimeSpan.Zero);

    private static PullRequestState Completed(string? target = "main") => new(PullRequestStates.Completed)
    {
        PullRequest = "https://example.test/org/project/_git/engine/pullrequest/7",
        MergeCommit = new string('a', 40),
        SourceCommit = new string('b', 40),
        Target = target,
        How = MergeHow.Squash,
        At = new DateTimeOffset(2026, 10, 4, 13, 50, 0, TimeSpan.Zero),
        Plugin = "azure-devops-pull-request",
        AskedAt = Asked,
    };

    private static LandedBranch Entry(PullRequestState? kept = null) =>
        new("engine", "aurora", "feature/q1-fix", "main", new string('c', 40), "s1a2b3c4", "q1", "Fix", Asked.AddDays(-1))
        {
            Plugin = "azure-devops-pull-request", Pushed = true, PullRequestState = kept,
        };

    [Fact]
    public void A_kept_answer_says_its_state_how_into_what_when_and_who_answered()
    {
        Assert.Equal(
            "completed by squash into `main` on 2026-10-04 13:50 UTC, as `azure-devops-pull-request` answered at 2026-10-04 14:02 UTC",
            PullRequestWords.Kept(Completed()));
        Assert.Equal("open, as `acme` answered at 2026-10-04 14:02 UTC",
            PullRequestWords.Kept(new PullRequestState(PullRequestStates.Open) { Plugin = "acme", AskedAt = Asked }));
        Assert.Equal("abandoned on 2026-10-03 09:00 UTC, as `acme` answered at 2026-10-04 14:02 UTC",
            PullRequestWords.Kept(new PullRequestState(PullRequestStates.Abandoned)
            {
                Plugin = "acme", AskedAt = Asked, At = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
            }));
        Assert.StartsWith("unknown (no pull request from that branch, or a status Daoris has no word for)",
            PullRequestWords.Kept(new PullRequestState(PullRequestStates.Unknown) { Plugin = "acme", AskedAt = Asked }));
        // A record that kept no plugin or no moment says only what it kept: absent is never a guess.
        Assert.Equal("completed, as its plugin answered", PullRequestWords.Kept(new PullRequestState(PullRequestStates.Completed)));
    }

    /// <summary>
    /// A row that is kept says what was kept of its pull request and why it does not clear the branch (design §2.3), a failed
    /// ask beside the answer it never overwrote (§2.4), and why nothing was asked (§2.1). A row with none of them says nothing.
    /// </summary>
    [Fact]
    public void A_rows_clause_says_the_answer_what_keeps_the_branch_a_failure_and_why_nothing_was_asked()
    {
        Assert.Equal("", PullRequestWords.Row(null, null, null, null));
        Assert.Equal(
            "; its pull request: completed by squash into `main` on 2026-10-04 13:50 UTC, as `azure-devops-pull-request` answered at "
            + "2026-10-04 14:02 UTC, but its merge commit is not on the line on this machine (bringing the repository up to date fetches it)",
            PullRequestWords.Row(Completed(), PullRequestCodes.MergeNotHere, null, null));
        Assert.Contains("but it completed into `release`, not the line",
            PullRequestWords.Row(Completed("release"), PullRequestCodes.OtherTarget, null, null));
        Assert.Contains("but the branch holds commits its pull request did not carry",
            PullRequestWords.Row(Completed(), PullRequestCodes.Beyond, null, null));
        Assert.Contains("but it merged a commit this machine never had",
            PullRequestWords.Row(Completed(), PullRequestCodes.SourceNotHere, null, null));
        // The state is its own code: said once.
        Assert.Equal("; its pull request: open, as `acme` answered at 2026-10-04 14:02 UTC",
            PullRequestWords.Row(new PullRequestState(PullRequestStates.Open) { Plugin = "acme", AskedAt = Asked }, PullRequestStates.Open, null, null));

        var failed = new PullRequestAskFailed(PluginEvents.Late, "acme", Asked.AddMinutes(30));
        Assert.Equal("; its pull request: open, as `acme` answered at 2026-10-04 14:02 UTC; asking `acme` again at 2026-10-04 14:32 UTC failed (`late`)",
            PullRequestWords.Row(new PullRequestState(PullRequestStates.Open) { Plugin = "acme", AskedAt = Asked }, PullRequestStates.Open, failed, null));
        Assert.Equal("; asking `acme` at 2026-10-04 14:32 UTC failed (`late`)", PullRequestWords.Row(null, null, failed, null));

        Assert.Equal("; no plugin pushed it and its repository's landing rule names none, so its pull request was not asked about",
            PullRequestWords.Row(null, null, null, new PullRequestNotAsked(PullRequestCodes.NoPlugin, PullRequestWords.NoPlugin)));
    }

    /// <summary>
    /// The clean-up's rows and bringing up to date's (design §2.5): a landed branch that is kept says what its pull request's
    /// answer was and why it does not clear it, or why nothing was asked; one that goes says only what proved it, and a session's
    /// row says nothing of a pull request.
    /// </summary>
    [Fact]
    public void The_clean_ups_rows_and_bringing_up_to_dates_say_the_code_on_a_kept_landed_branch()
    {
        var differs = new LandedItem("engine", "aurora", "feature/q1-fix", LandedKind.Differs, "origin/main", ["a.txt"], null, null, 1)
        {
            State = Completed(), StateCode = PullRequestCodes.MergeNotHere,
        };
        Assert.Equal(
            "engine  feature/q1-fix  1 file still differs on the line: a.txt; its pull request: completed by squash into `main` on "
            + "2026-10-04 13:50 UTC, as `azure-devops-pull-request` answered at 2026-10-04 14:02 UTC, but its merge commit is not on "
            + "the line on this machine (bringing the repository up to date fetches it)",
            LandedWords.Describe(differs));
        Assert.EndsWith("a.txt; plugin `acme` is switched off on this machine, so its pull request was not asked about",
            LandedWords.Describe(differs with
            {
                State = null, StateCode = null,
                NotAsked = new PullRequestNotAsked(PullRequestCodes.Unready, "plugin `acme` is switched off on this machine, so its pull request was not asked about."),
            }));
        // A branch that goes says what proved it, and nothing more.
        Assert.Equal("engine  feature/q1-fix  merged into `main`",
            LandedWords.Describe(differs with { Kind = LandedKind.Merged, Where = "main", StateCode = null }));

        var replay = new RebaseItem("engine", "aurora", "feature/q1-fix", Landed: true, RebaseKind.Pushed, "main", null, null, null, 0, null)
        {
            State = new PullRequestState(PullRequestStates.Open) { Plugin = "acme", AskedAt = Asked }, StateCode = PullRequestStates.Open,
        };
        Assert.Equal(
            "engine  feature/q1-fix  on its remote: replaying it would need a force push; its pull request: open, as `acme` answered at 2026-10-04 14:02 UTC",
            SyncWords.Describe(replay));
        Assert.Equal("engine  daoris/s-1  on its remote: replaying it would need a force push",
            SyncWords.Describe(replay with { Branch = "daoris/s-1", Landed = false }));
    }

    /// <summary><c>trees state</c> after an answer: who was asked, what is kept with its address, and what that proves here.</summary>
    [Fact]
    public void Asking_again_says_who_answered_what_is_kept_and_what_it_proves_here()
    {
        var said = PullRequestWords.AskedAgain(new PullRequestAskedAgain(Entry(Completed()))
        {
            Answered = true, Verdict = new PullRequestVerdict(null, "origin/main", Carries: true, Clears: true),
        });

        Assert.Equal(
            [
                "`feature/q1-fix` in `engine` (session s1a2b3c4): asked `azure-devops-pull-request` again.",
                "its pull request https://example.test/org/project/_git/engine/pullrequest/7: completed by squash into `main` on "
                + "2026-10-04 13:50 UTC, as `azure-devops-pull-request` answered at 2026-10-04 14:02 UTC.",
                "its merge commit is on `origin/main` here and the branch stands at or under the commit it merged, so it may go at the "
                + "clean-up and at bringing the repository up to date, each of which lists it first.",
            ],
            said);
    }

    /// <summary>What the kept answer proves here, for each code of design §2.3 and a branch that is gone.</summary>
    [Theory]
    [InlineData(null, true, false, "its merge commit is on `origin/main` here; the session branches at or under the commit it merged go at the clean-up.")]
    [InlineData(PullRequestCodes.Beyond, true, false, "its merge commit is on `origin/main` here, but the branch holds commits its pull request did not carry, so it stays; the session branches under the commit it merged go at the clean-up.")]
    [InlineData(PullRequestCodes.MergeNotHere, false, false, "nothing goes on its word here: its merge commit is not on the line on this machine (bringing the repository up to date fetches it).")]
    [InlineData(PullRequestCodes.OtherTarget, false, false, "nothing goes on its word here: it completed into `release`, not the line.")]
    [InlineData(PullRequestCodes.SourceNotHere, false, false, "nothing goes on its word here: it merged a commit this machine never had.")]
    [InlineData(PullRequestStates.Open, false, false, "nothing goes on its word: its pull request has not completed.")]
    [InlineData(PullRequestStates.Abandoned, false, false, "nothing goes on its word: its pull request was abandoned, not completed.")]
    [InlineData(PullRequestStates.Unknown, false, false, "nothing goes on its word: it named no state Daoris can act on.")]
    public void What_the_kept_answer_proves_here_is_said_for_each_code(string? code, bool carries, bool clears, string expected)
    {
        var said = PullRequestWords.AskedAgain(new PullRequestAskedAgain(Entry(Completed(code == PullRequestCodes.OtherTarget ? "release" : "main")))
        {
            Answered = true, Verdict = new PullRequestVerdict(code, carries ? "origin/main" : null, carries, clears),
        });

        Assert.Equal(expected, said[^1]);
    }

    /// <summary>
    /// Completed is final and asked no more; no plugin, one not ready and one that failed each say why, and a failure says the
    /// kept answer stands. Nothing kept is said as nothing kept, never as a state.
    /// </summary>
    [Fact]
    public void Nothing_asked_and_a_failed_ask_each_say_why()
    {
        Assert.Equal("`feature/q1-fix` in `engine` (session s1a2b3c4): its pull request completed, which is final, so it was not asked again.",
            PullRequestWords.AskedAgain(new PullRequestAskedAgain(Entry(Completed())) { Final = true })[0]);

        var none = PullRequestWords.AskedAgain(new PullRequestAskedAgain(Entry() with { Plugin = null })
        {
            Code = PullRequestCodes.NoPlugin, Why = PullRequestWords.NoPlugin,
        });
        Assert.Equal(
            ["`feature/q1-fix` in `engine` (session s1a2b3c4): " + PullRequestWords.NoPlugin, "nothing is kept of its pull request."],
            none);

        var failed = PullRequestWords.AskedAgain(new PullRequestAskedAgain(Entry(new PullRequestState(PullRequestStates.Open) { Plugin = "acme", AskedAt = Asked }))
        {
            Code = PluginEvents.Late, Why = "plugin `acme` did not answer `hook/work/state` within 30 s.",
            Verdict = new PullRequestVerdict(PullRequestStates.Open, null, false, false),
        });
        Assert.Equal(
            "`feature/q1-fix` in `engine` (session s1a2b3c4): asking again failed (`late`): plugin `acme` did not answer `hook/work/state` "
            + "within 30 s. Nothing is removed on its word until it answers, and what was kept stands.",
            failed[0]);
        Assert.Equal("its pull request: open, as `acme` answered at 2026-10-04 14:02 UTC.", failed[1]);
    }
}
