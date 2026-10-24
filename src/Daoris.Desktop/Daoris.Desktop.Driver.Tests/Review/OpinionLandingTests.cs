using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1f (D155 points 9 and 10; the second-agent design §7–§8): the second opinion's gate at every landing door, over real
/// git. The press (<c>LAND_SESSION_TREE</c> and <c>trees land</c> land through <see cref="SessionTrees.LandAsync"/> with the gate
/// the door read once) and the look under <i>Accept automatically</i> each hold a chain's work the level says another agent reads,
/// until an opinion that covers its tip is settled; a commit after it holds again, until the person goes on anyway; a required
/// opinion with no reviewer to be had holds, and one not required rides beside; an earlier step waits for the chain's last step
/// here. What let it go is kept on the landing record.
/// </summary>
/// <remarks>
/// Real git, as the other tree tests, and the service's quests and the local host's opinions stood in: nothing reaches a network
/// or a harness.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class OpinionLandingTests : IDisposable
{
    private readonly string _scratch;
    private readonly string _home;
    private readonly World _world = new();
    private readonly List<LandingLine> _lines = [];

    public OpinionLandingTests()
    {
        _scratch = Path.Combine(RepoRoot(), "_fixtures", "opinion-landing", Guid.NewGuid().ToString("N")[..8]);
        _home = Path.Combine(_scratch, "daoris-home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose()
    {
        GitSeed.Discard(_scratch);
    }

    /// <summary>
    /// The press, under a branch rule: done and nothing asked, it is refused with the gate's sentence and nothing is made; an
    /// opinion that raised nothing at the tree's tip settles it, and the landing record keeps who read it.
    /// </summary>
    [Fact]
    public async Task The_press_waits_for_its_opinion_and_the_landing_keeps_it_once_settled()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var tip = await HeadAsync(tree);

        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.Equal(AutoLandingCode.Opinion, held.Refusal);
        Assert.Equal("Waits for a second opinion: it is asked at the driver's next look. `daoris-driver opinion ask s1` asks it now.", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "nothing is made while the opinion waits");

        Read("o1", tip, reading: true);
        Assert.StartsWith("Waits for a second opinion: being read by Codex (OpenAI)", (await PressAsync(tree)).Message);

        Read("o1", tip);
        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        var record = new LandedBranches(_home).Landing("s1")!.Opinion!;
        Assert.Equal((OpinionGateStates.Settled, "o1", "codex-acp", tip, 1), (record.Said, record.Opinion, record.Reviewer, record.Tip, record.Passes));
    }

    /// <summary>
    /// Bound to its candidate (§8.3): a commit after the opinion's tip is work no other agent read, and the press holds it, saying
    /// how many; the person's <i>Go on anyway…</i> at the new tip lets it land, and the record keeps their words and the commit.
    /// </summary>
    [Fact]
    public async Task A_commit_after_the_opinion_holds_again_until_the_person_goes_on_anyway()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        Read("o1", await HeadAsync(tree));

        await CommitAsync(tree, "late.txt", "added after the opinion", "a late change");
        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.Equal(AutoLandingCode.Opinion, held.Refusal);
        Assert.StartsWith("1 commit since was not read by another agent", held.Message);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        new OpinionGates(_home).Said("q1", "web-app", new OpinionPersonWord(OpinionPersonSaid.Anyway, await HeadAsync(tree), DateTimeOffset.UtcNow)
        {
            Words = "a comment only", Door = ReviewDoors.Terminal,
        });
        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        var record = new LandedBranches(_home).Landing("s1")!.Opinion!;
        Assert.Equal((OpinionGateStates.Anyway, OpinionPersonSaid.Anyway, "a comment only", 1), (record.Said, record.Person, record.Words, record.Unread));
    }

    /// <summary>
    /// Required (§8.4): with no reviewer to be had, the landing holds and says the doors; a merge rule waits as a branch rule does.
    /// Not required, it lands at once and the record says it landed with none, and why.
    /// </summary>
    [Fact]
    public async Task Required_with_no_reviewer_to_be_had_holds_and_not_required_lands_saying_so()
    {
        var root = await RepositoryAsync("web-app");
        Configure(landing: null, required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var line = await HeadAsync(root);
        Unavailable(await HeadAsync(tree));

        var held = await PressAsync(tree);
        Assert.False(held.Landed);
        Assert.StartsWith("No second opinion: no listed reviewer of another maker is installed. The rule requires one, so the work waits for you:", held.Message);
        Assert.Equal(line, await HeadAsync(root));

        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal((OpinionGateStates.Unavailable, ReviewerUnavailable.NoReviewer),
            (new LandedBranches(_home).Landing("s1")!.Opinion!.Said, new LandedBranches(_home).Landing("s1")!.Opinion!.Code));
    }

    /// <summary>
    /// <i>Accept automatically</i> (§8.1): the look keeps a due session due as <c>opinion</c>, told once and read again at every look,
    /// logged as <c>opinion.held</c>, and lands it at the first look once the opinion is settled, as the press would.
    /// </summary>
    [Fact]
    public async Task The_look_keeps_it_due_as_opinion_and_lands_it_at_the_first_look_once_settled()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1", due: true);
        var tip = await HeadAsync(tree);
        Read("o1", tip, reading: true);

        var first = await LookAsync();
        Assert.Contains("Waits for a second opinion: being read by Codex (OpenAI)", Assert.Single(first));
        var entry = new AutoLandings(_home).Of("s1")!;
        Assert.Equal((AutoLandingCode.Opinion, (DateTimeOffset?)null), (entry.Last!.Code, entry.Closed));
        var logged = Assert.Single(_lines, each => each.Event == "opinion.held");
        Assert.Equal((OpinionGateStates.Reading, (object?)true, ReviewDoors.Look),
            (Value(logged, "state"), Value(logged, "required"), Value(logged, "door")));

        // Said once: a look with nothing changed says nothing more, and still lands nothing.
        Assert.Empty(await LookAsync());
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"));

        Read("o1", tip);
        Assert.Single(await LookAsync());
        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(OpinionGateStates.Settled, new LandedBranches(_home).Landing("s1")!.Opinion!.Said);
    }

    /// <summary>
    /// A chain is read once per repository (§8.1): under <c>landing</c>, a step whose chain has a later step here still to run waits
    /// for it, so one opinion reads the whole work there; once that step is done and its opinion covers both, each lands.
    /// </summary>
    [Fact]
    public async Task An_earlier_step_waits_for_the_chains_last_step_here_whose_opinion_covers_it()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await DoneSessionAsync(root, "s1", "q1");
        _world.Quests["q2"] = new QuestView("q2", "ask #a1", "web-app", "Fix the rest", "", "Open") { Parent = "q1" };

        var waits = await PressAsync(tree);
        Assert.False(waits.Landed);
        Assert.Contains("quest `#q2` still works there", waits.Message);

        _world.Quests["q2"] = _world.Quests["q2"] with { Status = "Done" };
        await CommitAsync(tree, "rest.txt", "the rest", "the rest of it");
        var opinion = await HeadAsync(tree);
        Read("o1", opinion, working: "s2");
        await ResetAsync(tree, "HEAD~1");

        var landed = await PressAsync(tree);
        Assert.True(landed.Landed, landed.Message);
        Assert.Equal(OpinionGateStates.Settled, new LandedBranches(_home).Landing("s1")!.Opinion!.Said);
    }

    /// <summary>
    /// FABRIC-CLOSE1h4 (D173's CLOSELAND1 note; §E5, D155): a bound quest at its agent's submission, never done, under <i>Accept
    /// automatically</i>. A second opinion that raised a <c>must</c> and went to the person stands disputed: the look keeps the entry as
    /// <c>opinion</c> and makes nothing. The person's <i>Go on anyway…</i> at its tip lets it go, and the first look after lands it while
    /// the quest stays taken at its submission: the landing waits on the unheld submission and writes no Done.
    /// </summary>
    [Fact]
    public async Task A_disputed_opinion_holds_a_bound_submission_and_the_persons_answer_lands_it_with_no_done()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true), required: true);
        var tree = await DoneSessionAsync(root, "s1", "q1", due: true, submitted: true);
        var tip = await HeadAsync(tree);
        Read("o1", tip, findings: [new OpinionFindingView(1, OpinionViews.Must, "report.ts", "The total drops a row", "Wrong totals", "Run it", "sure")]);
        new OpinionDeliveries(_home).ToPerson("o1", ContinueWhy.Elsewhere, DateTimeOffset.UtcNow);

        Assert.Contains("disputed", Assert.Single(await LookAsync()));
        Assert.Equal(AutoLandingCode.Opinion, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.False(await BranchAsync(root, "feature/q1-fix-the-gap"), "nothing is made while the dispute stands");

        new OpinionGates(_home).Said("q1", "web-app", new OpinionPersonWord(OpinionPersonSaid.Anyway, tip, DateTimeOffset.UtcNow)
        {
            Words = "the totals are right", Door = ReviewDoors.Terminal,
        });
        Assert.Single(await LookAsync());

        Assert.True(await BranchAsync(root, "feature/q1-fix-the-gap"));
        Assert.Equal(AutoLandingCode.Landed, new AutoLandings(_home).Of("s1")!.Last!.Code);
        Assert.Equal(OpinionGateStates.Anyway, new LandedBranches(_home).Landing("s1")!.Opinion!.Said);
        Assert.True(QuestResults.Submitted(_world.Quests["q1"]), "the landing writes no Done");
    }

    /// <summary>
    /// CHATFLOW1d (the chat-flow design §5.2 points 1 to 4 and 7): a chat run's press finds its opinion not asked, asks it at the landing
    /// occasion with the tip it captured and the person's turn count, and is refused saying so; pressed again before the look starts it,
    /// refused and nothing asked; while it is read, refused; once it settled, landed at the press, the record keeping who read it.
    /// </summary>
    [Fact]
    public async Task A_chats_press_asks_its_opinion_once_and_lands_at_the_press_once_it_settled()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await ChatTreeAsync(root);
        var tip = await HeadAsync(tree);

        var asked = await ChatPressAsync(tree, turns: 2);
        Assert.False(asked.Landed.Landed);
        Assert.Equal((ChatPresses.Codes.Asked, AutoLandingCode.Opinion), (asked.Code, asked.Landed.Refusal));
        Assert.Equal("A second opinion is asked: `codex-acp` reads it from the driver's next look; press Accept again once it settles.", asked.Landed.Message);
        var due = Assert.Single(new OpinionDues(_home).Open());
        Assert.Equal((ChatRun, tip, (int?)2, OpinionRules.Landing, ""), (due.Run, due.Tip, due.Turns, due.Occasion, due.Quest));

        var again = await ChatPressAsync(tree);
        Assert.Equal(ChatPresses.Codes.Asked, again.Code);
        Assert.Single(new OpinionDues(_home).Open());

        ReadChat("o1", tip, reading: true);
        var reading = await ChatPressAsync(tree);
        Assert.False(reading.Landed.Landed);
        Assert.StartsWith("Waits for a second opinion: being read by Codex (OpenAI)", reading.Landed.Message);
        Assert.Single(new OpinionDues(_home).Open());

        ReadChat("o1", tip);
        var landed = await ChatPressAsync(tree);
        Assert.True(landed.Landed.Landed, landed.Landed.Message);
        Assert.True(await BranchAsync(root, landed.Landed.Branch!));
        var record = new LandedBranches(_home).Landing(ChatSession)!.Opinion!;
        Assert.Equal((OpinionGateStates.Settled, "o1", tip), (record.Said, record.Opinion, record.Tip));
    }

    /// <summary>CHATFLOW1d (§5.2 point 7): under a merge-form rule, the press that finds the opinion settled merges the captured tip into the line.</summary>
    [Fact]
    public async Task A_merge_form_rule_merges_a_chats_work_at_the_press_that_finds_its_opinion_settled()
    {
        var root = await RepositoryAsync("web-app");
        Configure(landing: null, required: false);
        var tree = await ChatTreeAsync(root);
        var tip = await HeadAsync(tree);
        ReadChat("o1", tip);

        var landed = await ChatPressAsync(tree);
        Assert.True(landed.Landed.Landed, landed.Landed.Message);
        Assert.Contains(tip, await GitAsync(root, "log", "--format=%H", "-n", "5"));
    }

    /// <summary>CHATFLOW1d (§5.2 point 7, D164 point 4): uncommitted work refuses the press, settled opinion or not, and nothing lands.</summary>
    [Fact]
    public async Task Uncommitted_work_refuses_a_chats_press()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await ChatTreeAsync(root);
        ReadChat("o1", await HeadAsync(tree));
        await File.WriteAllTextAsync(Path.Combine(tree, "half.txt"), "not committed\n");

        var refused = await ChatPressAsync(tree);
        Assert.False(refused.Landed.Landed);
        Assert.Null(new LandedBranches(_home).Landing(ChatSession));
    }

    /// <summary>
    /// CHATFLOW1d, Codex's finding 1 (§5.2 point 1): a commit made between the gate's judgement and the landing is refused as moved, never
    /// landed under the judgement of the earlier tip, the press saying so on its door.
    /// </summary>
    [Fact]
    public async Task A_chat_candidate_moved_after_its_judgement_is_refused_and_never_landed()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await ChatTreeAsync(root);
        ReadChat("o1", await HeadAsync(tree));

        var moved = await ChatPressAsync(tree, before: () => CommitAsync(tree, "racing.txt", "a turn's commit", "a turn's commit"));
        Assert.False(moved.Landed.Landed);
        Assert.Equal((ChatPresses.Moved, ChatPresses.Codes.Moved), (moved.Landed.Message, moved.Code));
        Assert.Null(new LandedBranches(_home).Landing(ChatSession));
    }

    /// <summary>
    /// CHATFLOW1d (§5.2 point 1): a press while a turn runs is refused and asks nothing; a live conversation no runner here holds is another
    /// window's, refused there; and either way the conversation is let go, nothing reserved.
    /// </summary>
    [Fact]
    public async Task A_chats_press_while_a_turn_runs_or_held_elsewhere_is_refused_and_asks_nothing()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await ChatTreeAsync(root);

        var holder = new Holder(PressHold.Busy);
        var busy = await ChatPressAsync(tree, holder: holder, live: true);
        Assert.Equal((ChatPresses.Busy, ChatPresses.Codes.Busy), (busy.Landed.Message, busy.Code));
        Assert.Equal(0, holder.Released);

        var elsewhere = await ChatPressAsync(tree, live: true);
        Assert.Equal((ChatPresses.Elsewhere, ChatPresses.Codes.Elsewhere), (elsewhere.Landed.Message, elsewhere.Code));
        Assert.Empty(new OpinionDues(_home).Open());

        var held = new Holder(PressHold.Held);
        var asked = await ChatPressAsync(tree, holder: held, live: true);
        Assert.Equal(ChatPresses.Codes.Asked, asked.Code);
        Assert.Equal(1, held.Released);
    }

    /// <summary>
    /// CHATFLOW1d (§5.2 point 8): after a hand-over the run, its gate and its due are the line's, so the next record's press reads the same
    /// gate: the opinion settled on the first record's press lands the second's.
    /// </summary>
    [Fact]
    public async Task A_hand_overs_next_record_presses_the_same_chat_run_gate()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await ChatTreeAsync(root);
        Assert.Equal(ChatPresses.Codes.Asked, (await ChatPressAsync(tree)).Code);
        ReadChat("o1", await HeadAsync(tree));

        var landed = await ChatPressAsync(tree, session: "c2d3e4f5");
        Assert.True(landed.Landed.Landed, landed.Landed.Message);
        Assert.Equal(OpinionGateStates.Settled, new LandedBranches(_home).Landing("c2d3e4f5")!.Opinion!.Said);
    }

    /// <summary>
    /// CHATFLOW1d (§5.2 point 9, §7): a commit after the settled opinion shows <c>commits-since</c>, and the person's press over it, kept by
    /// the run where a quest's is kept by its chain, lets that tip land; the recheck is not the press's to ask.
    /// </summary>
    [Fact]
    public async Task A_chat_runs_press_answers_commits_since_by_the_run()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await ChatTreeAsync(root);
        ReadChat("o1", await HeadAsync(tree));
        await CommitAsync(tree, "later.txt", "a later turn", "a later turn");

        var held = await ChatPressAsync(tree);
        Assert.False(held.Landed.Landed);
        Assert.StartsWith("1 commit since was not read by another agent", held.Landed.Message);
        Assert.Empty(new OpinionDues(_home).Open());

        new OpinionGates(_home).Said(ChatRun, "web-app", new OpinionPersonWord(OpinionPersonSaid.Press, await HeadAsync(tree), DateTimeOffset.UtcNow)
        {
            Door = ReviewDoors.Screen,
        });
        var landed = await ChatPressAsync(tree);
        Assert.True(landed.Landed.Landed, landed.Landed.Message);
    }

    /// <summary>
    /// CHATFLOW1d (§5.2 point 5): the plan asks nothing. Reading a chat run's gate and its landing's plan, as <c>LANDING</c> and
    /// <c>trees land --plan</c> read them, shows the opinion not asked and writes no due.
    /// </summary>
    [Fact]
    public async Task A_chat_runs_plan_reads_its_gate_and_asks_nothing()
    {
        var root = await RepositoryAsync("web-app");
        Configure(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}"), required: false);
        var tree = await ChatTreeAsync(root);
        var trees = new SessionTrees(_home);

        var gate = await trees.GateAsync(tree, null, _world, ChatSession, run: ChatRun);
        await trees.PlanAsync(tree, new LandingSubject(ChatSession, null, "The chat's work"), process: gate.Process);

        Assert.Equal((OpinionGateStates.NotAsked, false, ChatRun), (gate.Opinion.State, gate.Opinion.Asked, gate.Opinion.Run));
        Assert.Empty(new OpinionDues(_home).Open());
        var kept = new OpinionGates(_home).Read(ChatRun, "web-app");
        Assert.Equal((0, 0, 0), (kept.Asks.Count, kept.Requests.Count, kept.Person.Count));
    }

    private const string ChatSession = "c1a2b3c4";

    private const string ChatRun = ChatRuns.Prefix + ChatSession;

    /// <summary>The runner a press reserves the conversation in, standing in: it answers as told, and counts each let-go.</summary>
    private sealed class Holder(PressHold answer) : IChatPressing
    {
        public int Released { get; private set; }

        public Task<PressHold> HoldAsync(string sessionId, CancellationToken ct = default) => Task.FromResult(answer);

        public void Release(string sessionId) => Released++;
    }

    /// <summary>A chat's own tree, grown as a conversation grows one, with a commit of its work.</summary>
    private async Task<string> ChatTreeAsync(string root)
    {
        var opened = await new SessionTrees(_home).OpenAsync(root, "web-app", "aurora");
        await CommitAsync(opened.Path, "chat.txt", "the chat's work", "the chat's work");
        return opened.Path;
    }

    /// <summary>
    /// A chat's press as the driver's one operation makes it (CHATFLOW1d): its run bound first, then <see cref="ChatPresses.PressAsync"/>, its
    /// gate read by the run at the captured tip and its landing pinned there; <paramref name="before"/> runs between the judgement and the landing.
    /// </summary>
    private async Task<ChatPressed> ChatPressAsync(
        string tree, int turns = 0, Func<Task>? before = null, IChatPressing? holder = null, bool live = false, string session = ChatSession)
    {
        Assert.Null(ChatRuns.BindFirst(Config(), _home, ChatRun, tree, [], DateTimeOffset.UtcNow).Failed);
        var trees = new SessionTrees(_home);
        return await ChatPresses.PressAsync(
            _home, new ChatPressAsk(session, tree, ChatRun, "web-app", "aurora", live), holder,
            (tip, token) => trees.GateAsync(tree, null, _world, session, token, tip: tip, run: ChatRun),
            async (gate, token) =>
            {
                if (before is not null) await before();
                return await trees.LandAsync(tree, new LandingSubject(session, null, "The chat's work"), token, gate: gate);
            },
            () => turns);
    }

    /// <summary>A first pass by Codex on a chat run's work at <paramref name="tip"/>, kept at the run's gate as the look keeps it.</summary>
    private void ReadChat(string id, string tip, bool reading = false)
    {
        _world.Opinions[id] = new OpinionView(id, "landing", "first", ChatSession, "r-" + id, "web-app", tip, tip, "codex-acp", ReviewerLabels.AnotherMaker,
            reading ? "reading" : "given")
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20, Findings = reading ? null : [], Read = "the whole change",
        };
        var gates = new OpinionGates(_home);
        if (!gates.Read(ChatRun, "web-app").Asks.Any(ask => ask.Opinion == id))
        {
            gates.Asked(ChatRun, "web-app", new OpinionAskKept(DateTimeOffset.UtcNow, "landing", ChatSession, tip) { Opinion = id, Reviewer = "codex-acp" });
        }
    }

    // ——— the world and the fixtures

    /// <summary>The service's quests and records, and the local host's opinions, standing in.</summary>
    private sealed class World : IAutoLandingWorld
    {
        public Dictionary<string, QuestView> Quests { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SessionRecord> Records { get; } = [];

        public Dictionary<string, OpinionView> Opinions { get; } = new(StringComparer.Ordinal);

        public Task<QuestView?> QuestAsync(string id, CancellationToken ct) => Task.FromResult(Quests.GetValueOrDefault(id));

        public Task<IReadOnlyList<SessionRecord>> RecordsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionRecord>>([.. Records]);

        public Task<IReadOnlySet<string>> InUseAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestView>>([.. Quests.Values]);

        public Task<AskView?> AskAsync(string id, CancellationToken ct) => Task.FromResult<AskView?>(null);

        public Task<OpinionView?> OpinionAsync(string id, CancellationToken ct) => Task.FromResult(Opinions.GetValueOrDefault(id));
    }

    /// <summary>The press, as a door makes it: the whole gate read once for the tree, and handed to the landing.</summary>
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

    /// <summary>
    /// A first pass by Codex on the work at <paramref name="tip"/>, kept at the gate as asked, reading, or raising nothing unless
    /// <paramref name="findings"/> are named.
    /// </summary>
    private void Read(string id, string tip, bool reading = false, string working = "s1", IReadOnlyList<OpinionFindingView>? findings = null)
    {
        _world.Opinions[id] = new OpinionView(id, "landing", "first", working, "r-" + id, "web-app", tip, tip, "codex-acp", ReviewerLabels.AnotherMaker,
            reading ? "reading" : "given")
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20, Findings = reading ? null : findings ?? [], Read = "the whole change",
        };
        var gates = new OpinionGates(_home);
        if (!gates.Read("q1", "web-app").Asks.Any(ask => ask.Opinion == id))
        {
            gates.Asked("q1", "web-app", new OpinionAskKept(DateTimeOffset.UtcNow, "landing", working, tip) { Opinion = id, Reviewer = "codex-acp" });
        }
    }

    /// <summary>A pass the walk found no reviewer for, kept at the gate as asked.</summary>
    private void Unavailable(string tip) =>
        new OpinionGates(_home).Asked("q1", "web-app", new OpinionAskKept(DateTimeOffset.UtcNow, "landing", "s1", tip) { Code = ReviewerUnavailable.NoReviewer });

    private DriverConfig Config() => DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig));

    /// <summary>The landing rule, and an opinion rule for the repository that reads before landing.</summary>
    private void Configure(LandingRule? landing, bool required)
    {
        var config = DriverConfig.Empty with
        {
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["web-app"] = new OpinionRule(["codex-acp"], [OpinionRules.Landing], required),
            },
        };
        (landing is null ? config : config.WithLanding("web-app", landing)).Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
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

    private static object? Value(LandingLine line, string key) => line.Data.Single(pair => pair.Key == key).Value;

    private static async Task<bool> BranchAsync(string root, string branch) =>
        (await GitAsync(root, "branch", "--list", branch)).Trim().Length > 0;

    private static async Task<string> HeadAsync(string tree) => (await GitAsync(tree, "rev-parse", "HEAD")).Trim();

    private static Task ResetAsync(string tree, string to) => GitAsync(tree, "reset", "--hard", "--quiet", to);

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
