using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LAND2c (D145 §3, D149): related work lands on one branch per repository. What is related is the `follows` chain, whichever
/// ask published each quest; a later done advances the chain's branch as a fast-forward of a branch Daoris made, and never
/// one whose work already reads on the line; the landing record keeps each advance, and the plugin is told the pull request
/// it already opened and who accepted the work. Tables and files only, so the suite's fast half; the advance over real git is
/// <see cref="LandingTests"/>.
/// </summary>
public sealed class LandingChainTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-landing-chain-" + Guid.NewGuid().ToString("N")[..8]);

    public LandingChainTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private const string Tip = "1111111111111111111111111111111111111111";
    private const string Next = "2222222222222222222222222222222222222222";

    private static LandedBranch Entry(string session = "s1", string tip = Tip) =>
        new("engine", "aurora", "feature/q1-fix-the-gap", "main", tip, session, "q1", "Fix the gap", At);

    // ——— what is related

    /// <summary>
    /// The owner's TK-2203, as its shape: a quest, its verify step, and a follow-up ask's drill-down that follows the verify
    /// step. The drill-down was published by another ask, and still lands on the chain's branch, named for its first quest; a
    /// quest of the second ask that follows nothing is its own work, on its own branch.
    /// </summary>
    [Fact]
    public async Task A_quest_that_follows_the_chain_lands_on_its_branch_whichever_ask_published_it()
    {
        var quests = new Dictionary<string, QuestView>
        {
            ["q1"] = new("q1", "ask #a1", "portal-ui", "TK-2203: the common report", "", "Done"),
            ["q2"] = new("q2", "ask #a1", "portal-ui", "Verify the common report in the browser", "", "Done") { Parent = "q1" },
            ["q3"] = new("q3", "ask #a2", "portal-ui", "Drill down into the report's totals", "", "Done") { Parent = "q2" },
            ["q4"] = new("q4", "ask #a2", "portal-ui", "Rename the report's export", "", "Done"),
        };
        Task<QuestView?> Find(string id) => Task.FromResult(quests.GetValueOrDefault(id));

        var drillDown = await LandingRules.SubjectAsync("s3", "q3", Find, opening: null);
        var unrelated = await LandingRules.SubjectAsync("s4", "q4", Find, opening: null);

        Assert.Equal(new LandingSubject("s3", "q1", "TK-2203: the common report"), drillDown);
        Assert.Equal(new LandingSubject("s4", "q4", "Rename the report's export"), unrelated);
        var rule = "feature/{slug}-{quest}";
        Assert.Equal(LandingRules.Expand(rule, new LandingNames("q1", LandingRules.Slug("TK-2203: the common report"), "portal-ui", "s1")),
            LandingRules.Expand(rule, new LandingNames(drillDown.Quest!, LandingRules.Slug(drillDown.Title), "portal-ui", "s3")));
    }

    /// <summary>
    /// A chain's step is told at its start the branch its done will move on, named for the chain's first quest; a quest that
    /// follows nothing is told its own, and a service that cannot walk the chain leaves the step its own, since a start never
    /// waits on a sentence.
    /// </summary>
    [Fact]
    public async Task A_chains_step_is_told_its_chains_branch_and_a_walk_that_fails_leaves_its_own()
    {
        var first = new QuestView("q1", "ask #a1", "engine", "Fix the gap", "", "Done");
        var step = new QuestView("q2", "ask #a1", "engine", "Verify the gap is fixed", "", "Open") { Parent = "q1" };
        Task<QuestView?> Find(string id) => Task.FromResult<QuestView?>(id switch { "q1" => first, "q2" => step, _ => null });

        Assert.Equal(new LandingSubject("s2", "q1", "Fix the gap"), await Daoris.Driver.Driver.LandsAsAsync("s2", step, Find, default));
        Assert.Equal(new LandingSubject("s1", "q1", "Fix the gap"), await Daoris.Driver.Driver.LandsAsAsync("s1", first, _ => throw new InvalidOperationException("never asked"), default));
        Assert.Equal(new LandingSubject("s2", "q2", "Verify the gap is fixed"),
            await Daoris.Driver.Driver.LandsAsAsync("s2", step, _ => throw new DriverException("the service did not answer"), default));
        Assert.Equal(new LandingSubject("s2", "q2", "Verify the gap is fixed"),
            await Daoris.Driver.Driver.LandsAsAsync("s2", step, _ => Task.FromResult<QuestView?>(null), default));
    }

    // ——— what an advance refuses

    /// <summary>
    /// A branch of the pattern's name moves on only where Daoris made and recorded it, it stands at the recorded tip, no working
    /// tree has it checked out, its work does not already read on the line, and the session's work grows from it with something
    /// new. Each refusal names what stands in the way; only one whose pull request was merged is <c>completed</c>.
    /// </summary>
    [Theory]
    [InlineData("unrecorded", AutoLandingCode.Exists, "Daoris does not move a branch it did not make")]
    [InlineData("moved", AutoLandingCode.Exists, "moved since Daoris landed on it")]
    [InlineData("checked-out", AutoLandingCode.Exists, "checked out at")]
    [InlineData("completed", AutoLandingCode.Completed, "its pull request was merged")]
    [InlineData("kept-completed", AutoLandingCode.Completed, "'s pull request completed, as `acme.lands` answered at 2026-10-04 14:02 UTC")]
    [InlineData("kept-abandoned", AutoLandingCode.Completed, "'s pull request was abandoned, as `acme.lands` answered")]
    [InlineData("kept-open", null, null)]
    [InlineData("apart", AutoLandingCode.Exists, "does not grow from")]
    [InlineData("nothing-new", AutoLandingCode.Nothing, "nothing to land")]
    [InlineData("advances", null, null)]
    public void An_advance_moves_only_a_branch_Daoris_made_at_its_tip_and_never_one_whose_work_is_on_the_line(
        string facts, string? code, string? says)
    {
        var standing = new AdvanceFacts(Entry(), Tip, CheckedOutAt: null, Descends: true, Ahead: 1, Completed: null);
        // PLUGHOOK1a (D149 point 3): a kept answer the platform gave refuses as git's own proof does, since git cannot see a
        // squash once the line has moved on.
        AdvanceFacts Kept(string state) => standing with
        {
            Recorded = Entry() with
            {
                PullRequestState = new PullRequestState(state)
                {
                    MergeCommit = new string('a', 40), SourceCommit = new string('b', 40), Plugin = "acme.lands",
                    AskedAt = new DateTimeOffset(2026, 10, 4, 14, 2, 0, TimeSpan.Zero),
                },
            },
        };
        var given = facts switch
        {
            "unrecorded" => standing with { Recorded = null },
            "moved" => standing with { Tip = Next },
            "checked-out" => standing with { CheckedOutAt = "/elsewhere" },
            "completed" => standing with { Completed = "origin/main" },
            "kept-completed" => Kept(PullRequestStates.Completed),
            "kept-abandoned" => Kept(PullRequestStates.Abandoned),
            "kept-open" => Kept(PullRequestStates.Open),
            "apart" => standing with { Descends = false, Ahead = 0 },
            "nothing-new" => standing with { Ahead = 0 },
            _ => standing,
        };

        var refused = LandingAdvance.Refusal("feature/q1-fix-the-gap", "engine", "daoris/s-2", given);

        if (code is null)
        {
            Assert.Null(refused);
            return;
        }

        Assert.NotNull(refused);
        Assert.False(refused.Landed);
        Assert.Equal(code, refused.Refusal);
        Assert.Contains(says!, refused.Message);
    }

    /// <summary>
    /// D149 point 3: a branch whose pull request was merged is not moved on, and the sentence says the way through — bring the
    /// repository up to date, which replays this session's own commits and removes that branch, then Accept lands it fresh.
    /// </summary>
    [Fact]
    public void A_merged_branch_is_refused_with_the_way_to_a_fresh_one()
    {
        var refused = LandingAdvance.Refusal("feature/q1-fix-the-gap", "engine", "daoris/s-2",
            new AdvanceFacts(Entry(), Tip, null, Descends: true, Ahead: 2, Completed: "origin/main"))!;

        Assert.Contains("already reads on `origin/main`", refused.Message);
        Assert.Contains("would ride no pull request", refused.Message);
        Assert.Contains("daoris-driver trees sync", refused.Message);
        Assert.Contains("a fresh `feature/q1-fix-the-gap`", refused.Message);
    }

    // ——— the codes

    [Fact]
    public void An_advance_is_kept_as_advanced_and_a_merged_branch_waits_as_completed()
    {
        var advanced = new TreeLanding(true, "moved `feature/x` on.", "feature/x") { AdvancedFrom = Tip };
        Assert.Equal(AutoLandingCode.Advanced, AutoLandingRules.CodeOf(advanced));
        // The push is still the plugin's: an advance it did not push is kept as that, as a landing's is.
        Assert.Equal(AutoLandingCode.PluginFailed, AutoLandingRules.CodeOf(advanced with
        {
            Plugin = new PluginLanding("acme.lands", Pushed: false, "https://example.test/pull/7", "its pull request is completed"),
        }));
        Assert.Equal(AutoLandingCode.Completed, AutoLandingRules.CodeOf(new TreeLanding(false, "merged") { Refusal = AutoLandingCode.Completed }));

        Assert.True(AutoLandingCode.Closes(AutoLandingCode.Advanced));
        Assert.False(AutoLandingCode.Closes(AutoLandingCode.Completed));
        Assert.True(AutoLandingCode.OnChange(AutoLandingCode.Completed));
        Assert.True(AutoLandingNotes.Says(AutoLandingCode.Advanced));
        Assert.True(AutoLandingNotes.Says(AutoLandingCode.Completed));
    }

    /// <summary>
    /// The conversation is told in the codes the page already words: an advance is an acceptance (its work is on the branch),
    /// and a merged branch is a refusal, each with the landing's own sentence beneath.
    /// </summary>
    [Fact]
    public void An_advance_is_said_as_an_acceptance_and_a_merged_branch_as_a_refusal()
    {
        var advanced = AutoLandingNotes.Of(AutoLandingCode.Advanced,
            new TreeLanding(true, "moved `feature/x` on from 1111111 — 1 more commit(s).", "feature/x") { AdvancedFrom = Tip });
        Assert.Equal(NoteCodes.LandingAccepted.Code, advanced.Parts![0].Code);
        Assert.Contains("moved `feature/x` on", advanced.Text);

        var completed = AutoLandingNotes.Of(AutoLandingCode.Completed,
            new TreeLanding(false, "`feature/x`'s work already reads on `main`.") { Refusal = AutoLandingCode.Completed }, branch: "feature/x");
        Assert.Equal(NoteCodes.LandingRefused.Code, completed.Parts![0].Code);
        Assert.Contains("already reads on `main`", completed.Text);
    }

    // ——— the record

    /// <summary>
    /// An advance moves the entry's tip and is kept with who did it and when; the advancing session finds the landing as its
    /// own, by its review and by the hand-off, and a push after it keeps the pull request the platform already has.
    /// </summary>
    [Fact]
    public void An_advance_is_kept_on_the_landing_and_its_session_finds_it()
    {
        var landings = new LandedBranches(_home);
        landings.Record(Entry() with { AcceptedBy = AcceptedBy.Person, Pushed = true, Plugin = "acme.lands", PullRequest = "https://example.test/pull/7" });

        landings.Advanced("ENGINE", "feature/q1-fix-the-gap", new LandedAdvance(Tip, Next, At.AddHours(1), "s2") { AcceptedBy = AcceptedBy.Auto });
        landings.Pushed("engine", "feature/q1-fix-the-gap", new PluginLanding("acme.lands", Pushed: true, PullRequest: null, "pushed it."), Next);

        var entry = new LandedBranches(_home).Landing("s2")!;
        Assert.Equal("s1", entry.Session);
        Assert.Equal(Next, entry.Tip);
        Assert.Equal(Next, entry.PushedTip);
        Assert.Equal("https://example.test/pull/7", entry.PullRequest);
        var advance = Assert.Single(entry.Advances);
        Assert.Equal((Tip, Next, "s2", AcceptedBy.Auto), (advance.From, advance.To, advance.Session, advance.AcceptedBy));
        Assert.True(entry.Names("S2"));
        Assert.Equal(At.AddHours(1), entry.AcceptedAt("s2"));
        Assert.Equal(At, entry.AcceptedAt("s1"));
        Assert.Equal(AcceptedBy.Auto, entry.AcceptedByOf("s2"));
        Assert.Equal(AcceptedBy.Person, entry.AcceptedByOf("s1"));
        Assert.Equal("feature/q1-fix-the-gap", Assert.Single(landings.Find("s2")).Branch);

        var text = File.ReadAllText(Path.Combine(_home, LandedBranches.FileName));
        Assert.Contains("\"advances\"", text);
        Assert.Contains("\"session\": \"s2\"", text);
    }

    /// <summary>The review's words for a session whose done moved a branch on: whose landing it moved, when, and who accepted it.</summary>
    [Fact]
    public void The_review_says_a_session_moved_its_chains_branch_on()
    {
        var entry = Entry() with
        {
            AcceptedBy = AcceptedBy.Person,
            Tip = Next,
            Advances = [new LandedAdvance(Tip, Next, At.AddHours(1), "s2") { AcceptedBy = AcceptedBy.Auto }],
        };

        var said = LandedReviewWords.Describe("s2", new LandedReview(entry, LandedState.Standing));

        Assert.StartsWith("`s2` moved `feature/q1-fix-the-gap` on in `engine` on 2026-10-04 13:00 UTC, after `s1`'s landing, "
            + "accepted automatically when its quest was done; that branch still stands.", said);
        Assert.StartsWith("`s1` landed on `feature/q1-fix-the-gap` in `engine` on 2026-10-04 12:00 UTC;",
            LandedReviewWords.Describe("s1", new LandedReview(entry, LandedState.Standing)));
    }

    // ——— what the plugin is told

    /// <summary>
    /// The frame carries the pull request the landing already holds, so a plugin pushes and opens no second one, and who
    /// accepted the work, so the description it writes is true. A first landing's frame says none, and the person.
    /// </summary>
    [Fact]
    public void The_landing_frame_carries_the_pull_request_and_who_accepted_it()
    {
        var first = Frame(new LandingFrame("engine", "aurora", "/root", "feature/x", "main", "Fix", "q1", "s1", []));
        Assert.Equal(JsonValueKind.Null, first.GetProperty("pullRequest").ValueKind);
        Assert.Equal(AcceptedBy.Person, first.GetProperty("acceptedBy").GetString());

        var advance = Frame(new LandingFrame("engine", "aurora", "/root", "feature/x", "main", "Fix", "q1", "s2", [],
            PullRequest: "https://example.test/pull/7", AcceptedBy: AcceptedBy.Auto));
        Assert.Equal("https://example.test/pull/7", advance.GetProperty("pullRequest").GetString());
        Assert.Equal(AcceptedBy.Auto, advance.GetProperty("acceptedBy").GetString());

        // The kit's sample is the same function's: a plugin made with the kit sees both fields.
        var sample = PluginKit.Find(HookPoints.Land)!.Frame;
        Assert.True(sample.ContainsKey("pullRequest"));
        Assert.Equal(AcceptedBy.Person, sample["acceptedBy"]!.GetValue<string>());
    }

    private static JsonElement Frame(LandingFrame frame) =>
        JsonDocument.Parse(JsonSerializer.Serialize(HookFrames.Land(frame))).RootElement.Clone();
}
