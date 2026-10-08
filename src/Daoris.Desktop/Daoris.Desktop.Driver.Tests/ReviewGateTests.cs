using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1c (D154 points 3, 4, 7 and 9; the review environment design §1.4, §2.1, §3.1–§3.2): the driver's half of the review.
/// The level's table reads, from the most specific down, the person's skip, a set-up step in the chain, the chain's choice, the
/// ask's choice, the repository's rule, the workspace's, and nothing; the gate lets work go only on the person's <i>reviewed</i> of
/// a set-up whose commit holds what lands, or their skip; the planner sits a set-up step it cannot honestly start. Pure tables,
/// with git's ancestry stood in for: the fast half (MOD8). <see cref="ReviewLandingTests"/> holds the doors over real git.
/// </summary>
public sealed class ReviewGateTests
{
    private const string Shown = "0123456789abcdef0123456789abcdef01234567";
    private const string Later = "89abcdef0123456789abcdef0123456789abcdef";

    private static readonly ReviewEnvironment Local = new("local", "local", "README.md", "http://localhost:4200");
    private static readonly ReviewEnvironment Dev = new("dev", "deployed", "docs/deploying-to-dev.md", "https://dev.example.test");

    private static ResolvedReview Rule(bool required = true, string source = ReviewSource.Repository) =>
        new(new ReviewRule([Local, Dev], required), source);

    private static QuestView Quest(string id, string to = "web-app", string status = "Done", string? parent = null) =>
        new(id, "ask #a1", to, $"Work {id}", "", status) { Parent = parent };

    private static QuestView Step(string id, string parent, string status = "Done", IReadOnlyList<QuestSetUpView>? setUps = null) =>
        Quest(id, status: status, parent: parent) with { Title = $"Show #{parent} in `local` for review", SetUpIn = "local", SetUps = setUps ?? [] };

    private static QuestSetUpView SetUp(string commit, long sequence, string? session = "s7") =>
        new(commit) { Look = "http://localhost:4200/reports", Shows = "the new column", Again = "open reports", Session = session, Local = true, Machine = "desk", Sequence = sequence };

    private static QuestReviewVerdictView Verdict(string said, long? sequence = null, string? words = null) =>
        new(said) { SetUpMachine = sequence is null ? null : "desk", SetUpSequence = sequence, Words = words, At = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero) };

    private static AskView Ask(params AskReviewChoiceView[] choices) => new("a1", "work", "add the compare setting", "Published", "intake") { ReviewChoices = choices };

    private static AskReviewChoiceView Chose(string choice) => new(choice, DateTimeOffset.UnixEpoch);

    // ——— the chain

    [Fact]
    public void A_chain_is_its_follows_chain_read_from_any_of_its_steps()
    {
        IReadOnlyList<QuestView> quests =
        [
            Quest("q9", "media-api"), Quest("q1"), Quest("q2", parent: "q1"), Quest("q3", "media-api", parent: "q2"),
            Step("q4", "q2"), Quest("q5", parent: "q9"),
        ];

        Assert.Equal(["q1", "q2", "q3", "q4"], ReviewGate.ChainOf(quests, "q3").Select(quest => quest.Id));
        Assert.Equal(["q1", "q2", "q3", "q4"], ReviewGate.ChainOf(quests, "#q1").Select(quest => quest.Id));
        Assert.Empty(ReviewGate.ChainOf(quests, "gone"));
    }

    // ——— the level's table (design §1.4)

    [Fact]
    public void Nothing_set_anywhere_is_todays_behaviour()
    {
        var decided = ReviewGate.Decide([Quest("q1")], "web-app", ask: null, rule: null);

        Assert.Equal((ReviewLevels.Nothing, (string?)null, false), (decided.Level, decided.Environment, decided.Reviews));
    }

    [Fact]
    public void A_rule_says_review_only_where_it_is_required_and_a_repositorys_none_says_none()
    {
        Assert.Equal((ReviewLevels.Workspace, "local"), Of(ReviewGate.Decide([Quest("q1")], "web-app", null, Rule(source: ReviewSource.Workspace))));
        Assert.Equal((ReviewLevels.Repository, "local"), Of(ReviewGate.Decide([Quest("q1")], "web-app", null, Rule())));
        Assert.Equal((ReviewLevels.Repository, (string?)null), Of(ReviewGate.Decide([Quest("q1")], "web-app", null, Rule(required: false))));
        Assert.Equal((ReviewLevels.Repository, (string?)null),
            Of(ReviewGate.Decide([Quest("q1")], "web-app", null, new ResolvedReview(ReviewRule.None, ReviewSource.Repository))));
    }

    [Fact]
    public void The_asks_choice_is_over_the_rule_and_the_chains_over_the_asks()
    {
        Assert.Equal((ReviewLevels.Ask, "dev"), Of(ReviewGate.Decide([Quest("q1")], "web-app", Ask(Chose("on"), Chose("dev")), Rule(required: false))));
        Assert.Equal((ReviewLevels.Ask, (string?)null), Of(ReviewGate.Decide([Quest("q1")], "web-app", Ask(Chose("off")), Rule())));

        var chosen = Quest("q1") with { Review = new QuestReviewChoiceView("on", "show me it running") };
        var decided = ReviewGate.Decide([chosen], "web-app", Ask(Chose("off")), Rule(required: false));
        Assert.Equal((ReviewLevels.Chain, "local", "show me it running"), (decided.Level, decided.Environment, decided.Words));

        // `on` applies only where an environment is declared (design §1.4): a repository with none lands without a review.
        Assert.Equal((ReviewLevels.Chain, (string?)null), Of(ReviewGate.Decide([chosen], "web-app", null, null)));
        Assert.Equal((ReviewLevels.Chain, (string?)null),
            Of(ReviewGate.Decide([chosen with { Review = new QuestReviewChoiceView("off") }], "web-app", null, Rule())));
    }

    [Fact]
    public void A_set_up_step_in_the_chain_is_over_the_chains_off_composed_or_published()
    {
        var off = new QuestReviewChoiceView("off");
        var composing = Quest("q1", status: "Taken") with
        {
            Review = off,
            Then = [new QuestStepView("web-app", "Show {parent} in `dev` for review", "Set it up.") { SetUpIn = "dev" }],
        };
        var composed = ReviewGate.Decide([composing], "web-app", null, Rule());
        Assert.Equal((ReviewLevels.SetUpStep, "dev", "q1"), (composed.Level, composed.Environment, composed.ComposedBy));
        Assert.Null(composed.SetUpStep);

        var published = ReviewGate.Decide([Quest("q1") with { Review = off }, Step("q2", "q1", "Open")], "web-app", null, null);
        Assert.Equal((ReviewLevels.SetUpStep, "local", "q2"), (published.Level, published.Environment, published.SetUpStep?.Id));
    }

    [Fact]
    public void Another_repositorys_set_up_step_and_a_declined_one_decide_nothing_here()
    {
        var elsewhere = Step("q2", "q1") with { To = "media-api" };
        Assert.Equal(ReviewLevels.Nothing, ReviewGate.Decide([Quest("q1"), elsewhere], "web-app", null, null).Level);
        Assert.Equal(ReviewLevels.Nothing, ReviewGate.Decide([Quest("q1"), Step("q2", "q1", "Declined")], "web-app", null, null).Level);
    }

    [Fact]
    public void The_persons_skip_is_over_everything()
    {
        var skipped = Quest("q1") with { Verdicts = [Verdict(ReviewVerdicts.Skipped, words: "seen it run")] };
        var decided = ReviewGate.Decide([skipped, Step("q2", "q1", "Open")], "web-app", Ask(Chose("dev")), Rule());

        Assert.Equal((ReviewLevels.Skip, (string?)null, "q1", "seen it run"), (decided.Level, decided.Environment, decided.SkippedOn, decided.Words));
    }

    // ——— the gate's states (design §3.2)

    [Fact]
    public async Task Nothing_to_review_and_a_skip_let_the_work_go()
    {
        var none = await Judge(ReviewGate.Decide([Quest("q1")], "web-app", null, null));
        Assert.True(none.LetsGo);
        Assert.Equal((ReviewStates.None, "Nothing waits for a review here."), (none.State, none.Says));
        Assert.Null(none.Landing);

        var skip = await Judge(ReviewGate.Decide([Quest("q1") with { Verdicts = [Verdict(ReviewVerdicts.Skipped, words: "seen it run")] }], "web-app", null, Rule()));
        Assert.True(skip.LetsGo);
        Assert.Equal(ReviewStates.Skipped, skip.State);
        Assert.Equal(new LandingReview(ReviewVerdicts.Skipped, null, "q1") { At = skip.Decision.Skip!.At, Words = "seen it run" }, skip.Landing);
    }

    [Fact]
    public async Task With_no_set_up_step_it_is_not_shown_and_names_the_skip()
    {
        var gate = await Judge(ReviewGate.Decide([Quest("q1")], "web-app", null, Rule()));

        Assert.False(gate.LetsGo);
        Assert.Equal(ReviewStates.NotShown, gate.State);
        Assert.Equal(
            "Waits for your review in `local`: nothing shows it there yet: no set-up step for `web-app` is in its chain. "
            + "`daoris-driver quest review q1 skip \"…\"` lets it land without one.",
            gate.Says);
    }

    [Fact]
    public async Task A_step_still_open_or_with_nothing_posted_is_being_set_up()
    {
        var open = await Judge(ReviewGate.Decide([Quest("q1"), Step("q2", "q1", "Taken")], "web-app", null, null));
        Assert.Equal(ReviewStates.BeingSetUp, open.State);
        Assert.Contains("set-up step `#q2` is taken", open.Says);

        var unposted = await Judge(ReviewGate.Decide([Quest("q1"), Step("q2", "q1")], "web-app", null, null));
        Assert.Equal(ReviewStates.BeingSetUp, unposted.State);
        Assert.Contains("posted when its session ends", unposted.Says);
    }

    [Fact]
    public async Task Shown_and_unanswered_it_waits_and_names_the_terminals_verdict()
    {
        var gate = await Judge(ReviewGate.Decide([Quest("q1"), Step("q2", "q1", setUps: [SetUp(Shown, 41)])], "web-app", null, null));

        Assert.False(gate.LetsGo);
        Assert.Equal(ReviewStates.Shown, gate.State);
        Assert.Equal(
            "Waits for your review in `local`: set-up step `#q2` showed it at `01234567`, at <http://localhost:4200/reports>: the new "
            + "column. Look at it, then `daoris-driver quest review q2 reviewed`, or `daoris-driver quest review q2 not-yet \"…\"` with "
            + "what is not right yet.",
            gate.Says);
    }

    [Fact]
    public async Task Reviewed_on_a_set_up_that_holds_the_tip_lets_it_go_and_is_what_the_landing_keeps()
    {
        var step = Step("q2", "q1", setUps: [SetUp(Shown, 41)]) with { Verdicts = [Verdict(ReviewVerdicts.Reviewed, 41, "right")] };
        var asked = new List<string>();
        var gate = await ReviewGate.JudgeAsync(ReviewGate.Decide([Quest("q1"), step], "web-app", null, null), Later, commit =>
        {
            asked.Add(commit);
            return Task.FromResult<bool?>(true);
        });

        Assert.True(gate.LetsGo);
        Assert.Equal(ReviewStates.Reviewed, gate.State);
        Assert.Equal([Shown], asked);
        Assert.Equal(new LandingReview(ReviewVerdicts.Reviewed, "local", "q2") { Commit = Shown, At = step.Verdicts[0].At, Words = "right" }, gate.Landing);
    }

    [Fact]
    public async Task What_was_reviewed_does_not_let_newer_work_through()
    {
        var step = Step("q2", "q1", setUps: [SetUp(Shown, 41)]) with { Verdicts = [Verdict(ReviewVerdicts.Reviewed, 41)] };
        var gate = await Judge(ReviewGate.Decide([Quest("q1"), step], "web-app", null, null), holds: false);

        Assert.False(gate.LetsGo);
        Assert.Equal(ReviewStates.NotHeld, gate.State);
        Assert.Contains("what you reviewed does not hold these commits", gate.Says);
        Assert.Contains($"`{Later[..8]}` is not that commit or one before it", gate.Says);
        // The set-up step was reviewed, so a skip there is refused: the skip goes on the chain's work.
        Assert.Contains("`daoris-driver quest review q1 skip", gate.Says);

        // Git that cannot say holds nothing either.
        Assert.Equal(ReviewStates.NotHeld, (await Judge(ReviewGate.Decide([Quest("q1"), step], "web-app", null, null), holds: null)).State);
    }

    [Fact]
    public async Task A_newer_set_up_after_a_reviewed_one_waits_for_its_own_look_where_the_older_does_not_hold_the_tip()
    {
        var step = Step("q2", "q1", setUps: [SetUp(Shown, 41), SetUp(Later, 44)]) with { Verdicts = [Verdict(ReviewVerdicts.Reviewed, 41)] };

        var older = await Judge(ReviewGate.Decide([Quest("q1"), step], "web-app", null, null), holds: true);
        Assert.Equal(ReviewStates.Reviewed, older.State);
        Assert.Equal(Shown, older.SetUp!.Commit);

        var newer = await Judge(ReviewGate.Decide([Quest("q1"), step], "web-app", null, null), holds: false);
        Assert.Equal(ReviewStates.Shown, newer.State);
        Assert.Equal(Later, newer.SetUp!.Commit);
    }

    [Fact]
    public async Task Not_yet_waits_for_the_next_set_up_shown()
    {
        var step = Step("q2", "q1", setUps: [SetUp(Shown, 41)]) with { Verdicts = [Verdict(ReviewVerdicts.NotYet, 41, "the total is off")] };
        var gate = await Judge(ReviewGate.Decide([Quest("q1"), step], "web-app", null, null));

        Assert.Equal(ReviewStates.NotYet, gate.State);
        Assert.Equal("the total is off", gate.Verdict!.Words);
        Assert.Contains("you said not yet to what set-up step `#q2` showed at `01234567`", gate.Says);
    }

    [Fact]
    public void A_set_up_step_waits_until_its_newest_set_up_is_reviewed_or_it_is_skipped()
    {
        Assert.False(ReviewGate.Waits(Quest("q1")));
        Assert.True(ReviewGate.Waits(Step("q2", "q1", setUps: [SetUp(Shown, 41)])));
        Assert.False(ReviewGate.Waits(Step("q2", "q1", setUps: [SetUp(Shown, 41)]) with { Verdicts = [Verdict(ReviewVerdicts.Reviewed, 41)] }));
        Assert.True(ReviewGate.Waits(Step("q2", "q1", setUps: [SetUp(Shown, 41), SetUp(Later, 44)]) with { Verdicts = [Verdict(ReviewVerdicts.Reviewed, 41)] }));
        Assert.False(ReviewGate.Waits(Step("q2", "q1") with { Verdicts = [Verdict(ReviewVerdicts.Skipped)] }));
    }

    // ——— the gate's world: the chain, its ask, and a service that does not answer

    [Fact]
    public async Task The_gate_reads_the_chain_from_any_step_and_its_asks_choice()
    {
        var world = new World([Quest("q1"), Quest("q2", parent: "q1")], Ask(Chose("dev")));
        var config = DriverConfig.Empty with { Reviews = new Dictionary<string, ReviewRule> { ["web-app"] = Rule(required: false).Rule } };

        var gate = await ReviewGate.ReadAsync(world, config, Path.Combine(AppContext.BaseDirectory, "no-such-tree"), "web-app", "aurora", "q2");

        Assert.Equal((ReviewLevels.Ask, "dev", ReviewStates.NotShown), (gate.Decision.Level, gate.Decision.Environment, gate.State));
        Assert.Equal(["a1"], world.AsksRead);
    }

    [Fact]
    public async Task A_conversation_has_nothing_to_wait_for_and_an_unread_service_holds_only_where_a_rule_stands()
    {
        var rule = DriverConfig.Empty with { Reviews = new Dictionary<string, ReviewRule> { ["web-app"] = Rule().Rule } };
        Assert.Equal(ReviewStates.None, (await ReviewGate.ReadAsync(new World([]), rule, "tree", "web-app", "aurora", quest: null)).State);

        var unread = await ReviewGate.ReadAsync(new World([]) { Fails = true }, rule, "tree", "web-app", "aurora", "q1");
        Assert.False(unread.LetsGo);
        Assert.Equal(ReviewStates.Unread, unread.State);
        Assert.Equal("Whether this work waits for your review could not be read: the service is down. Nothing lands until it can be.", unread.Says);

        Assert.True((await ReviewGate.ReadAsync(new World([]) { Fails = true }, DriverConfig.Empty, "tree", "web-app", "aurora", "q1")).LetsGo);
    }

    // ——— the planner's sit (design §2.1)

    [Fact]
    public void The_planner_sits_a_set_up_step_no_rule_here_names_or_with_no_window_to_show_it_in()
    {
        var step = Step("q2", "q1", "Open");
        var registered = new RepoView("web-app", Adopted: true, Root: "C:/work/web-app", Workspace: "work");
        var snapshot = new Snapshot([step], [registered], []);
        var driven = DriverConfig.Empty with { Drivable = ["web-app"] };

        var unnamed = Assert.Single(Planner.Plan(snapshot, driven, window: true));
        Assert.Equal(StartVerdict.CannotShow, unnamed.Verdict);
        Assert.Contains("no review rule here names `local` for `web-app`", unnamed.Reason);

        var declared = driven with { WorkspaceReviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase) { ["work"] = Rule().Rule } };
        var headless = Assert.Single(Planner.Plan(snapshot, declared));
        Assert.Equal(StartVerdict.CannotShow, headless.Verdict);
        Assert.Contains("this loop has no window to show it in", headless.Reason);

        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(snapshot, declared, window: true)).Verdict);
        // A deployed environment is shown where it runs: a headless loop may start it.
        var deployed = snapshot with { Quests = [step with { SetUpIn = "dev" }] };
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(deployed, declared)).Verdict);
        // Every other quest plans as it did.
        Assert.Equal(StartVerdict.Start, Assert.Single(Planner.Plan(snapshot with { Quests = [Quest("q1", status: "Open")] }, driven)).Verdict);
    }

    [Fact]
    public void A_set_up_steps_tree_must_hold_its_procedure()
    {
        var tree = Path.Combine(AppContext.BaseDirectory, "review-procedure-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tree);
        try
        {
            Assert.Contains("this tree holds no `README.md`", ReviewSetUps.ProcedureMissing(Local, Step("q2", "q1", "Open"), tree));
            File.WriteAllText(Path.Combine(tree, "README.md"), "# web-app\n");
            Assert.Null(ReviewSetUps.ProcedureMissing(Local, Step("q2", "q1", "Open"), tree));
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    [Fact]
    public void A_question_held_for_its_review_says_so_and_names_the_verdicts_door()
    {
        var question = Step("q2", "q1") with { Held = true, Hold = EvidenceCodes.Unreviewed, SetUps = [SetUp(Shown, 41)] };
        var waiting = Quest("q5", "media-api", "Taken") with { Awaits = "q2" };
        var snapshot = new Snapshot([waiting, question], [new RepoView("media-api", true, "C:/work/media-api")], [])
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q5"] = new PriorSession("s5", "C:/trees/s5", "completed") },
        };

        var waits = Planner.Plan(snapshot, DriverConfig.Empty with { Drivable = ["media-api"] }).Single(c => c.Quest.Id == "q5");

        Assert.Equal(StartVerdict.Waiting, waits.Verdict);
        Assert.Contains("it waits for your review in `local`", waits.Reason);
        Assert.Contains("`daoris-driver quest review q2 reviewed`", waits.Reason);
        Assert.DoesNotContain("quest accept", waits.Reason);
    }

    // ——— the trace and the accept card read the review as itself (REVIEWENV1b2's hand-back)

    [Fact]
    public void The_trace_words_a_reviews_verdicts_and_a_review_hold_as_themselves()
    {
        var at = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
        var chain = new TraceChain(TraceEntry.Quest, "q2")
        {
            Links =
            [
                new TraceLink(TraceLink.AskKind)
                {
                    Ask = new TraceAskLink("a1")
                    {
                        Workspace = "work", State = "Published", Tier = "intake", Sentence = "add the compare setting",
                        Words =
                        [
                            new AskWordView(AskWordView.Asked, "add the compare setting", at),
                            new AskWordView(AskWordView.NotYet, "the total is off", at.AddHours(1), Quest: "q2"),
                            new AskWordView(AskWordView.Reviewed, "right now", at.AddHours(2), Quest: "q2"),
                            new AskWordView(AskWordView.Skipped, "a readme change", at.AddHours(3), Quest: "q4"),
                        ],
                        GoAheads = [],
                    },
                },
                new TraceLink(TraceLink.QuestKind)
                {
                    Quest = new TraceQuestLink("q2")
                    {
                        Address = "web-app", Title = "Show #q1 in `local` for review", Status = "Done", Ask = "a1", Held = true,
                        Hold = EvidenceCodes.Unreviewed, Records = [],
                    },
                },
            ],
        };

        var said = TraceWords.Say(chain);

        Assert.Contains("· said not yet to what a set-up step showed, on quest #q2\n", said);
        Assert.Contains("· reviewed what a set-up step showed, on quest #q2\n", said);
        Assert.Contains("· skipped the review of the work, on quest #q4\n", said);
        Assert.DoesNotContain("to session a session", said);
        Assert.Contains("  held: it waits for your review of what it showed (`daoris-driver quest review q2 reviewed`", said);
        Assert.DoesNotContain("departed from what you required", said);
    }

    // ——— the look keeps a review's wait

    [Fact]
    public void An_unreviewed_try_is_read_again_at_every_look_and_never_closes_its_entry()
    {
        var entry = new AutoLanding("s1", "q1", "web-app", "work", "tree", DateTimeOffset.UnixEpoch)
        {
            Tries = [new AutoTry(DateTimeOffset.UnixEpoch, AutoLandingCode.Unreviewed) { Tip = Shown, Status = "clean" }],
        };

        Assert.True(AutoLandingRules.ShouldTry(entry, Shown, "clean"));
        Assert.False(AutoLandingCode.Closes(AutoLandingCode.Unreviewed));
        Assert.True(AutoLandingNotes.Says(AutoLandingCode.Unreviewed));
    }

    /// <summary>
    /// REVIEWENV1c2: the look's note for a wait on the review has a code of its own, never a refused landing's, with the gate's
    /// sentence beneath it as the driver said it, a program's words.
    /// </summary>
    [Fact]
    public void An_unreviewed_try_is_noted_as_a_wait_for_the_review_with_the_gate_s_sentence_beneath()
    {
        const string gate = "Waits for your review in `local`: nothing shows it there yet: no set-up step for `web-app` is in its chain.";

        var note = AutoLandingNotes.Of(AutoLandingCode.Unreviewed, new TreeLanding(false, gate) { Refusal = AutoLandingCode.Unreviewed });

        Assert.Equal(NoteCodes.LandingUnreviewed.Code, note.Parts![0].Code);
        Assert.Equal(
            "not accepted automatically: its work waits for your review before it lands, as follows. It lands at the first look "
            + "after you review it or skip the review.",
            note.Parts[0].Text);
        Assert.Equal((gate, NoteBy.Program), (note.Parts[1].Words, note.Parts[1].By));
        Assert.DoesNotContain("refused", note.Text, StringComparison.Ordinal);
    }

    private static (string Level, string? Environment) Of(ReviewDecision decided) => (decided.Level, decided.Environment);

    private static Task<ReviewGateState> Judge(ReviewDecision decision, bool? holds = true) =>
        ReviewGate.JudgeAsync(decision, Later, _ => Task.FromResult(holds));

    /// <summary>The service's quests and asks, standing in; or a service that does not answer.</summary>
    private sealed class World(IReadOnlyList<QuestView> quests, AskView? ask = null) : IReviewWorld
    {
        public bool Fails { get; init; }

        public List<string> AsksRead { get; } = [];

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) =>
            Fails ? throw new HttpRequestException("the service is down.") : Task.FromResult(quests);

        public Task<AskView?> AskAsync(string id, CancellationToken ct)
        {
            AsksRead.Add(id);
            return Task.FromResult(ask);
        }
    }
}
