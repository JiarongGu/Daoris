using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// REVIEWENV1c (D154 point 7; the review environment design §3.1–§3.2, §3.5): the gate at every landing door, over real git. The
/// press (<c>LAND_SESSION_TREE</c> and <c>trees land</c> land through <see cref="SessionTrees.LandAsync"/> with the gate the door
/// read) and the look under <i>Accept automatically</i> each refuse a chain's work the level says to review, until the person's
/// <i>reviewed</i> on a set-up whose commit holds the tip, or their skip; a later commit holds again; a merge rule waits as a branch
/// rule does; a repository set to none lands at once. What let it go is kept on the landing record.
/// </summary>
/// <remarks>
/// Real git, as the other tree tests, and the service's quests stood in: nothing reaches a network or a harness.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReviewLandingTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly World _world = new();
    private readonly List<LandingLine> _lines = [];

    public ReviewLandingTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "review-landing", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// The press, under a branch rule: shown and unreviewed, it is refused with the gate's sentence and nothing is made; the
    /// person's <i>reviewed</i> on the set-up at the tree's tip lets it land, and the landing record keeps the review.
    /// </summary>
    [Fact]
    public async Task The_press_waits_for_the_persons_reviewed_and_the_landing_keeps_it()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);
        ShowStep(tip);

        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.Equal(AutoLandingCode.Unreviewed, held.Refusal);
        Assert.StartsWith("Waits for your review in `local`: set-up step `#q2` showed it at", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "nothing is made while the review waits");

        Review(tip, words: "the column reads right");
        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        var record = new LandedBranches(_home).Landing("s1")!;
        Assert.Equal(new LandingReview(ReviewVerdicts.Reviewed, "local", "q2") { Commit = tip, At = Verdicted, Words = "the column reads right" }, record.Review);
    }

    /// <summary>A commit added after the review is work the person has not seen run: the gate holds it again.</summary>
    [Fact]
    public async Task A_later_commit_holds_again()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);
        ShowStep(tip);
        Review(tip);

        await CommitAsync(tree, "late.txt", "added after the look", "a late change");
        var held = await PressAsync(tree);

        Assert.False(held.Landed);
        Assert.Contains("what you reviewed does not hold these commits", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
    }

    /// <summary>A merge rule waits as a branch rule does (design §3.1): nothing merges into the line until the review lets it go.</summary>
    [Fact]
    public async Task A_merge_rule_waits_as_a_branch_rule_does()
    {
        var root = await RepositoryAsync("web-app");
        Configure(landing: null, required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var line = await HeadAsync(root);

        var held = await PressAsync(tree);

        Assert.False(held.Landed);
        Assert.Contains("nothing shows it there yet: no set-up step for `web-app` is in its chain", held.Message);
        Assert.Equal(line, await HeadAsync(root));
    }

    /// <summary>The person's skip lets the work land without a review, and the landing record keeps it with their words.</summary>
    [Fact]
    public async Task A_skip_lets_it_land_and_is_kept()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        _world.Quests["q1"] = _world.Quests["q1"] with
        {
            Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.Skipped) { Words = "a readme change", At = Verdicted }],
        };

        var landed = await PressAsync(tree);

        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(new LandingReview(ReviewVerdicts.Skipped, null, "q1") { At = Verdicted, Words = "a readme change" },
            new LandedBranches(_home).Landing("s1")!.Review);
    }

    /// <summary>A repository set to none has no review environment, whatever its workspace says (design §1.3): it lands at once.</summary>
    [Fact]
    public async Task A_repository_set_to_none_lands_at_once()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true, repositoryNone: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");

        var landed = await PressAsync(tree);

        Assert.True(landed.Landed, landed.Message);
        Assert.Null(new LandedBranches(_home).Landing("s1")!.Review);
    }

    /// <summary>
    /// WORKFLOW1f (the workflow design §4.6, §5.5): under a named workflow, D154's rows 5 and 6 are its bound version's. A version
    /// with no look lands where Current, its rule requiring one, would hold the work; the landing record, its sentence and the trace
    /// say which version decided.
    /// </summary>
    [Fact]
    public async Task A_named_version_with_no_look_lands_where_Current_would_hold_for_one()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        Follow("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"you"}]""");
        var tree = await DoneSessionAsync(root, "s1", "q1");

        var landed = await PressAsync(tree);

        Assert.True(landed.Landed, landed.Message);
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Contains("It landed as `docs-to-pr` v1 says, `web-app`'s default.", landed.Message);
        var record = new LandedBranches(_home).Landing("s1")!;
        Assert.Null(record.Review);
        Assert.Equal(new LandingWorkflow("docs-to-pr", 1, WorkflowLevels.Repository), record.Workflow);
        Assert.Equal(new LandedRule(null, AutoAccept: false, LandingSource.Repository), record.Rule);
    }

    /// <summary>
    /// WORKFLOW1f (design §3.7): a version's look in an environment the repository does not declare cannot start. The press is
    /// refused saying why, before anything is made, and the person's skip, row 1, lets it land without one.
    /// </summary>
    [Fact]
    public async Task A_named_versions_look_that_cannot_start_holds_saying_why_and_the_skip_lets_it_land()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        Follow("""[{"id":"work","kind":"work"},{"id":"look","kind":"look","environment":"staging"},{"id":"land","kind":"landing","form":"branch","accept":"you"}]""");
        var tree = await DoneSessionAsync(root, "s1", "q1");

        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.Equal(AutoLandingCode.Unreviewed, held.Refusal);
        Assert.StartsWith("Waits for your review, and it cannot start: `docs-to-pr` v1 asks for one, and its look `look` reads the work in "
            + "`staging`, which `web-app` does not declare (it declares `local`)", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "nothing is made while the look cannot start");

        _world.Quests["q1"] = _world.Quests["q1"] with
        {
            Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.Skipped) { Words = "staging is down", At = Verdicted }],
        };
        Assert.True((await PressAsync(tree)).Landed);
    }

    /// <summary>
    /// WORKFLOW1f (design §4.5): a run bound to a named workflow that cannot be read here any more holds; nothing lands, and no other
    /// workflow is followed in its place, Current among them.
    /// </summary>
    [Fact]
    public async Task A_run_whose_workflow_is_gone_holds_and_nothing_is_swapped_in()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        Follow("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"branch","accept":"you"}]""");
        File.Delete(WorkflowStore.PathOf(_home, "docs-to-pr"));
        var tree = await DoneSessionAsync(root, "s1", "q1");

        var held = await PressAsync(tree);

        Assert.False(held.Landed);
        Assert.Equal(AutoLandingCode.Refused, held.Refusal);
        Assert.StartsWith("Its run was bound to `docs-to-pr` v1, `web-app`'s default, which cannot start it here: no workflow `docs-to-pr` "
            + "is saved here any more.", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));
    }

    /// <summary>
    /// <i>Accept automatically</i> (design §3.1): the look keeps a due session due as <c>unreviewed</c>, told once and read again at
    /// every look, and lands it at the first look after the review, as the press would, the review kept on its record.
    /// </summary>
    [Fact]
    public async Task The_look_keeps_it_unreviewed_and_lands_it_at_the_first_look_after_the_review()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1", due: true);
        var tip = await HeadAsync(tree);
        ShowStep(tip);

        var first = await LookAsync();
        Assert.Contains("Waits for your review in `local`", Assert.Single(first));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal(AutoLandingCode.Unreviewed, entry.Last!.Code);
        Assert.Null(entry.Closed);
        Assert.Equal(NoteCodes.LandingUnreviewed.Code, Notes("s1").Last().Parts![0].Code);
        var logged = Assert.Single(_lines, line => line.Event == "review.held");
        Assert.Equal((ReviewStates.Shown, ReviewLevels.SetUpStep, ReviewDoors.Look),
            (Value(logged, "state"), Value(logged, "level"), Value(logged, "door")));

        // Said once: a look with nothing changed says nothing more, and still lands nothing.
        Assert.Empty(await LookAsync());
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        Review(tip);
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(ReviewVerdicts.Reviewed, new LandedBranches(_home).Landing("s1")!.Review!.Said);
    }

    /// <summary>
    /// FABRIC-CLOSE1h4 (D173's CLOSELAND1 note; §E5, D154): a bound quest at its agent's submission, never done, under <i>Accept
    /// automatically</i>. The person's <i>not yet</i> on what its set-up step showed holds the look's landing as <c>unreviewed</c>, and
    /// nothing is made; once the step shows again and the person reviews that, the first look lands it while the quest stays taken at its
    /// submission: the landing waits on the unheld submission and writes no Done.
    /// </summary>
    [Fact]
    public async Task A_not_yet_holds_a_bound_submission_and_the_next_set_up_reviewed_lands_it_with_no_done()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1", due: true, submitted: true);
        var tip = await HeadAsync(tree);
        ShowStep(tip);
        _world.Quests["q2"] = _world.Quests["q2"] with
        {
            Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.NotYet) { SetUpMachine = "desk", SetUpSequence = 41, Commit = tip, Words = "the column is empty", At = Verdicted }],
        };

        Assert.Contains("you said not yet", Assert.Single(await LookAsync()));
        Assert.Equal(AutoLandingCode.Unreviewed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "nothing is made while the person's not yet stands");

        _world.Quests["q2"] = _world.Quests["q2"] with
        {
            SetUps = [.. _world.Quests["q2"].SetUps, new QuestSetUpView(tip) { Shows = "the column, filled", Session = "s2", Local = true, Machine = "desk", Sequence = 42 }],
            Held = false,
            Hold = null,
            Verdicts =
            [
                .. _world.Quests["q2"].Verdicts,
                new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { SetUpMachine = "desk", SetUpSequence = 42, Commit = tip, At = Verdicted.AddMinutes(5) },
            ],
        };
        Assert.Single(await LookAsync());

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(ReviewVerdicts.Reviewed, new LandedBranches(_home).Landing("s1")!.Review!.Said);
        Assert.True(QuestResults.Submitted(_world.Quests["q1"]), "the landing writes no Done");
    }

    /// <summary>
    /// FABRIC-END7's review, finding 1: the gate is read again just before the landing, never taken from the look that chose it. A
    /// commit the tree gains after the look chose it is work nobody reviewed: it waits as unreviewed, and nothing is made.
    /// </summary>
    [Fact]
    public async Task A_commit_after_the_look_chose_it_holds_it_for_review_before_it_lands()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1", due: true);
        var tip = await HeadAsync(tree);
        ShowStep(tip);
        Review(tip);
        var lander = new AutoLander(_home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add);
        var (chosen, _) = await lander.ChooseAsync(Config());
        Assert.Single(chosen);

        await CommitAsync(tree, "late.txt", "added after the look chose it", "a late change");
        var said = await lander.LandAsync(chosen);

        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "the late commit is work nobody reviewed");
        Assert.Equal(AutoLandingCode.Unreviewed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Contains("what you reviewed does not hold these commits", Assert.Single(said));
    }

    /// <summary>
    /// The set-up's commit is read, never reported (design §2.6): at a set-up step's end the driver posts its session's said
    /// set-ups with its tree's <c>HEAD</c> and the kind the rule declares, tells the conversation, and logs <c>review.shown</c>. A
    /// quest that is no set-up step posts nothing.
    /// </summary>
    [Fact]
    public async Task A_set_up_steps_end_posts_the_commit_its_tree_holds()
    {
        var root = await RepositoryAsync("web-app");
        Configure(landing: null, required: false);
        var tree = await DoneSessionAsync(root, "s7", "q2");
        var head = await HeadAsync(tree);
        var (service, heard) = ReviewStandIn.Of(ReviewStandIn.Step());
        using var _ = service;
        var lines = new List<LandingLine>();
        service.LandingLined += lines.Add;
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));

        Assert.Null(await ReviewSetUps.PostAsync(service, Config(), events, _world.Quests["q2"], "s7", tree, "aurora", DateTimeOffset.UtcNow,
            CancellationToken.None));
        var said = await ReviewSetUps.PostAsync(service, Config(), events, ReviewStandIn.Step(), "s7", tree, "aurora",
            new DateTimeOffset(2026, 10, 8, 8, 59, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.StartsWith($"Daoris read `{head[..8]}` from its tree and posted what it showed in `local`:", said);
        var (path, body) = Assert.Single(heard, each => each.Path.EndsWith("/set-up", StringComparison.Ordinal));
        Assert.Equal("/api/quests/q2/set-up", path);
        using var sent = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal((head, "local", "s7"),
            (sent.RootElement.GetProperty("commit").GetString(), sent.RootElement.GetProperty("kind").GetString(), sent.RootElement.GetProperty("session").GetString()));
        var shown = Assert.Single(lines);
        Assert.Equal(("review.shown", "local", (object?)60L), (shown.Event, Value(shown, "kind"), Value(shown, "seconds")));
        Assert.Contains(events.After("s7", 0).Events, e => e.Kind == SessionEventKind.Note && e.Text!.Contains("posted what it showed"));
    }

    // ——— the world and the fixtures

    /// <summary>
    /// CHATFLOW1c (the chat-flow design §4.3): a press on a chat run whose repository chooses a graph version binds nothing first, and is
    /// gated and landed as before, never refused <c>bind-failed</c>.
    /// </summary>
    [Fact]
    public async Task A_chat_s_press_under_a_graph_choice_lands_as_before_and_is_never_refused_bind_failed()
    {
        var root = await RepositoryAsync("web-app");
        WorkflowStore.Add(_home, new WorkflowVersionAdded("web-graph", "A graph", "terminal", "2026-10-21T09:00:00Z", null, System.Text.Json.Nodes.JsonNode.Parse(
            """{"schemaVersion":2,"entries":["claimed"],"end":"handed","nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},"""
            + """{"id":"handed","kind":"handed","version":1,"reach":"observed"}],"wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}""")), kept: []);
        DriverConfig.Parse("""{"workflows":{"web-app":{"default":"web-graph"}}}""").Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var tree = await ChatAsync(root);
        var line = await HeadAsync(root);

        var (landed, refused) = await ChatPressAsync(tree);

        Assert.Null(refused);
        Assert.True(landed!.Landed, landed.Message);
        Assert.NotEqual(line, await HeadAsync(root));
        Assert.False(WorkflowRunBindings.Bound(_home, "chat-c1a2b3c4"));
    }

    /// <summary>CHATFLOW1c (§4.3): a home that cannot keep the binding refuses the press, <c>bind-failed</c>, with nothing landed.</summary>
    [Fact]
    public async Task A_chat_s_press_on_a_home_that_cannot_keep_its_binding_is_refused_bind_failed()
    {
        var root = await RepositoryAsync("web-app");
        Configure(landing: null, required: false);
        var tree = await ChatAsync(root);
        var line = await HeadAsync(root);
        var runs = WorkflowRunBindings.FolderOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(runs)!);
        File.WriteAllText(runs, "not a folder");

        var (landed, refused) = await ChatPressAsync(tree);

        Assert.Null(landed);
        Assert.StartsWith("trees: session `c1a2b3c4`'s repository's workflow could not be bound, so its work cannot be landed yet: ", refused!.Error[0]);
        Assert.Equal(1, refused.Code);
        Assert.Equal(line, await HeadAsync(root));
    }

    /// <summary>
    /// CHATFLOW1c (§4.3): a named workflow that cannot be read is bound with its problem at the press's bind-first, never refused
    /// <c>bind-failed</c> and never swapped for Current, and the gate holds the press by that problem, nothing landed.
    /// </summary>
    [Fact]
    public async Task A_chat_s_press_under_an_unreadable_named_workflow_is_held_by_its_problem()
    {
        var root = await RepositoryAsync("web-app");
        DriverConfig.Parse("""{"workflows":{"web-app":{"default":"release-train"}}}""").Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var tree = await ChatAsync(root);
        var line = await HeadAsync(root);

        var (landed, refused) = await ChatPressAsync(tree);

        Assert.Null(refused);
        Assert.False(landed!.Landed);
        Assert.Contains("no workflow `release-train` is saved here", landed.Message);
        Assert.Equal("no workflow `release-train` is saved here.", WorkflowRunBindings.Read(_home, "chat-c1a2b3c4")!.Problem);
        Assert.Equal(line, await HeadAsync(root));
    }

    /// <summary>
    /// CHATFLOW1d2 (the chat-flow design §5.3): under a rule that requires a look, a chat run's look reads <c>cannot-start</c>, and its press
    /// is refused with the limit's words, nothing landed. <i>Accept without the look…</i>, the person's own press with their words, keeps the
    /// skip at the tip the press captured and lands that tip; the landing record names it, its commit and the words.
    /// </summary>
    [Fact]
    public async Task A_chat_runs_press_under_a_look_is_refused_with_its_limit_and_the_persons_way_past_it_lands_at_that_tip()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await ChatAsync(root);
        var tip = await HeadAsync(tree);
        var line = await HeadAsync(root);

        var held = await ChatOperationAsync(tree);
        Assert.False(held.Landed.Landed);
        Assert.Contains(ChatLooks.Limit, held.Landed.Message);
        Assert.Equal(ReviewStates.CannotStart, held.Gate!.Review.State);
        Assert.Equal(line, await HeadAsync(root));
        Assert.Empty(new ChatLooks(_home).Skips(ChatRun));

        var landed = await ChatOperationAsync(tree, new ChatLookPress("no environment shows a conversation's work", ReviewDoors.Screen));
        Assert.True(landed.Landed.Landed, landed.Landed.Message);
        Assert.Equal(tip, Assert.Single(new ChatLooks(_home).Skips(ChatRun)).Tip);
        var review = new LandedBranches(_home).Landing(ChatSession)!.Review!;
        Assert.Equal((ReviewVerdicts.Skipped, tip, "no environment shows a conversation's work"), (review.Said, review.Commit, review.Words));
    }

    /// <summary>
    /// CHATFLOW1d2 (§5.3): the person's way past the look covers the tip it was pressed at and the commits before it; a later commit asks
    /// the look again, and the press is refused with its limit.
    /// </summary>
    [Fact]
    public async Task A_commit_after_the_persons_way_past_a_chat_runs_look_asks_it_again()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await ChatAsync(root);
        new ChatLooks(_home).Skip(ChatRun, new ChatLookSkip(await HeadAsync(tree), DateTimeOffset.UtcNow, "fine as it is", ReviewDoors.Screen));
        await CommitAsync(tree, "later.txt", "a later turn's work", "a later turn's work");

        var held = await ChatOperationAsync(tree);
        Assert.False(held.Landed.Landed);
        Assert.Contains(ChatLooks.Limit, held.Landed.Message);
    }

    /// <summary>
    /// CHATFLOW1d2, Codex's finding 1 (§5.3): the tip skipped is the tip landed. A commit made after the skip is judged, before the landing,
    /// is refused as moved, never landed under the skip; the skip stays at the tip the press captured.
    /// </summary>
    [Fact]
    public async Task A_chat_tree_that_moves_after_the_skip_is_judged_is_refused_and_never_landed_under_it()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: true);
        var tree = await ChatAsync(root);
        var tip = await HeadAsync(tree);

        var moved = await ChatOperationAsync(tree, new ChatLookPress("as it is", ReviewDoors.Terminal),
            before: () => CommitAsync(tree, "racing.txt", "a turn's commit", "a turn's commit"));

        Assert.False(moved.Landed.Landed);
        Assert.Equal((ChatPresses.Moved, ChatPresses.Codes.Moved), (moved.Landed.Message, moved.Code));
        Assert.Equal(tip, Assert.Single(new ChatLooks(_home).Skips(ChatRun)).Tip);
        Assert.Null(new LandedBranches(_home).Landing(ChatSession));
    }

    private const string ChatSession = "c1a2b3c4";

    private const string ChatRun = ChatRuns.Prefix + ChatSession;

    /// <summary>
    /// A chat's press as the driver's one operation makes it (CHATFLOW1d, CHATFLOW1d2): its run bound first, then
    /// <see cref="ChatPresses.PressAsync"/> over an ended record no runner holds, its gate read by the run at the captured tip and its landing
    /// pinned there; <paramref name="before"/> runs between the judgement and the landing.
    /// </summary>
    private async Task<ChatPressed> ChatOperationAsync(string tree, ChatLookPress? withoutLook = null, Func<Task>? before = null)
    {
        Assert.Null(ChatRuns.BindFirst(Config(), _home, ChatRun, tree, [], DateTimeOffset.UtcNow).Failed);
        var trees = new SessionTrees(_home);
        return await ChatPresses.PressAsync(
            _home, new ChatPressAsk(ChatSession, tree, ChatRun, "web-app", "aurora", Live: false) { WithoutLook = withoutLook }, holder: null,
            (tip, token) => trees.GateAsync(tree, null, _world, ChatSession, token, tip: tip, run: ChatRun),
            async (gate, token) =>
            {
                if (before is not null) await before();
                return await trees.LandAsync(tree, new LandingSubject(ChatSession, null, "The chat's work"), token, gate: gate);
            },
            () => 0);
    }

    /// <summary>A chat's own tree, grown as a conversation grows one, with a commit of its work.</summary>
    private async Task<string> ChatAsync(string root)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "web-app", "aurora");
        await CommitAsync(opened.Path, "chat.txt", "the chat's work", "the chat's work");
        return opened.Path;
    }

    /// <summary>
    /// A chat's press, as <c>LAND_SESSION_TREE</c> makes it (CHATFLOW1c): its run bound first, refused where the home cannot keep it, then
    /// the gate read by its run's process and handed to the landing.
    /// </summary>
    private async Task<(TreeLanding? Landed, TreesSaid? Refused)> ChatPressAsync(string tree)
    {
        const string Session = "c1a2b3c4";
        var run = ChatRuns.Prefix + Session;
        if (ChatRuns.BindFirst(Config(), _home, run, tree, [], DateTimeOffset.UtcNow) is { Failed: { } failed }) return (null, TreesWords.BindFailed(Session, failed));
        var trees = new SessionTrees(_home);
        var gate = await trees.GateAsync(tree, null, _world, Session, run: run);
        return (await trees.LandAsync(tree, new LandingSubject(Session, null, "The chat's work"), gate: gate), null);
    }

    private static readonly DateTimeOffset Verdicted = new(2026, 10, 9, 9, 30, 0, TimeSpan.Zero);

    /// <summary>The service's quests and records, standing in.</summary>
    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        // The second opinion's gate reads the host's opinions (XAGENT1f): no rule names a reviewer here, so none is asked.
        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult<OpinionView?>(null);
    }

    /// <summary>The press, as a door makes it: the gate read for the tree, and handed to the landing.</summary>
    private async Task<TreeLanding> PressAsync(string tree)
    {
        var trees = new SessionTrees(_home);
        var gate = await trees.GateAsync(tree, "q1", _world, "s1");
        return await trees.LandAsync(tree, new LandingSubject("s1", "q1", "Fix the gap"), gate: gate);
    }

    private async Task<IReadOnlyList<string>> LookAsync()
    {
        var lander = new AutoLander(_home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add);
        var (chosen, said) = await lander.ChooseAsync(Config());
        return [.. said, .. await lander.LandAsync(chosen)];
    }

    /// <summary>The set-up step that follows q1, done, having shown its work at <paramref name="commit"/>.</summary>
    private void ShowStep(string commit) =>
        _world.Quests["q2"] = new QuestView("q2", "ask #a1", "web-app", "Show #q1 in `local` for review", "", "Done")
        {
            Parent = "q1",
            SetUpIn = "local",
            Held = true,
            Hold = EvidenceCodes.Unreviewed,
            SetUps = [new QuestSetUpView(commit) { Look = "http://localhost:4200/reports", Shows = "the new column", Session = "s2", Local = true, Machine = "desk", Sequence = 41 }],
        };

    /// <summary>The person's reviewed of the set-up step's set-up.</summary>
    private void Review(string commit, string? words = null) =>
        _world.Quests["q2"] = _world.Quests["q2"] with
        {
            Held = false,
            Hold = null,
            Verdicts = [new QuestReviewVerdictView(ReviewVerdicts.Reviewed) { SetUpMachine = "desk", SetUpSequence = 41, Commit = commit, Words = words, At = Verdicted }],
        };

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>The landing rule, and a review rule for the workspace and the repository: the repository's own, or its <c>false</c>.</summary>
    private void Configure(LandingRule? landing, bool required, bool repositoryNone = false)
    {
        var rule = new ReviewRule([new ReviewEnvironment("local", "local", "README.md", "http://localhost:4200")], required);
        var config = DriverConfig.Empty with
        {
            Reviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase) { ["web-app"] = repositoryNone ? ReviewRule.None : rule },
            WorkspaceReviews = new Dictionary<string, ReviewRule>(StringComparer.OrdinalIgnoreCase) { ["aurora"] = rule },
        };
        (landing is null ? config : config.WithLanding("web-app", landing)).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
    }

    /// <summary>
    /// A named workflow saved here with one version, chosen as `web-app`'s default, and run q1 bound to it as its first start binds
    /// it (WORKFLOW1e), over the rules <see cref="Configure"/> wrote.
    /// </summary>
    private void Follow(string steps)
    {
        WorkflowStore.Add(_home, new WorkflowVersionAdded(
            "docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", System.Text.Json.Nodes.JsonNode.Parse(steps)), kept: []);
        WorkflowSelection.Apply(Config(), new WorkflowUse("docs-to-pr", "web-app", null, null)).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        Assert.True(WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(Config(), _home, "q1", "web-app", "aurora", null, null, [], DateTimeOffset.UtcNow)));
    }

    /// <summary>
    /// A session that worked in its own tree and concluded on its quest's done, or, <paramref name="submitted"/>, at its closure graph's
    /// submission with the quest still taken (FABRIC-CLOSE1h4, §E5); made due as the conclusion makes it, where asked.
    /// </summary>
    private async Task<string> DoneSessionAsync(string root, string session, string quest, bool due = false, bool submitted = false)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "web-app", "aurora");
        await CommitAsync(opened.Path, $"{session}.txt", $"the work of {session}", $"the work of {session}");
        var view = new QuestView(quest, "ask #a1", "web-app", "Fix the gap", "Fix it.", submitted ? "Taken" : "Done");
        _world.Quests[quest] = submitted
            ? view with { Closure = "sha256:closure", Submission = new QuestSubmissionView("desk", 1, DateTimeOffset.UtcNow, "ready") }
            : view;
        _world.Records.Add(new SessionRecord(session, "web-app", "completed") { Quest = quest, Tree = opened.Path, Created = DateTimeOffset.UtcNow });
        if (due)
        {
            Assert.Equal(AutoConcluded.Due, AutoLander.Concluded(_home, Config(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), session,
                _world.Quests[quest], QuestResults.Status(_world.Quests[quest]), "completed", opened.Path, "aurora"));
        }

        return opened.Path;
    }

    private IReadOnlyList<SessionEvent> Notes(string session) =>
        [.. new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note)];

    private static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    private static async Task<string> HeadAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    /// <summary>A copy of the run's plain seed (<see cref="GitSeed.PlainAsync"/>; PROCSPEED1, PROCSPEED1b): no test here reads the first commit's id or time.</summary>
    private Task<string> RepositoryAsync(string name) => GitSeed.PlainAsync(Path.Combine(_scratch, name), name);

    private static async Task CommitAsync(string tree, string file, string content, string message)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), content + "\n");
        await GitAsync(tree, "add", "-A");
        await GitAsync(tree, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", message);
    }

    private static async Task<string> GitAsync(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        // Both streams read at once, before the wait, so a git that writes a pipe's worth to one never blocks on it.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync();
        return await stdout;
    }

    /// <summary>The checkout this build runs from: a linked worktree's <c>.git</c> is a file, so it stops there too.</summary>
    private static string RepoRoot() => GitSeed.RepoRoot();
}
