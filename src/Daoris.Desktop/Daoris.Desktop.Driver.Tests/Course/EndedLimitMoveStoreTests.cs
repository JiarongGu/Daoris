using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-vi part a (D212; the foundation design §3.3, §13.4, §13.5): the ended limit move keeps its move record and writes no
/// <c>&lt;s&gt;.limit-move.json</c> box beside it. Decided at <c>old-ended</c>, an ended record having no part to end, under this run's claim;
/// <c>opening</c> under the key its keyed open is sent with; <c>opened</c> with every word the open moved; <c>delivering</c> and the prompt's
/// mark before the hand; each handed word's <c>sent</c> as the turn's write returns, then the take, then <c>delivered</c>. Against a host
/// that moves nothing the move is one of no word whose prompt's outcome closes it, and LIMITWORD1's word kept after the decision still reaches
/// the new part once. A box an older build wrote is imported once at the shell's come-up and set aside, and a box still in place keeps the
/// go-on and the move off its record.
/// </summary>
/// <remarks>
/// As <see cref="LimitMoveStoreTests"/>: the ledger is the in-process one, the new part's turns are the real queue over a door that writes
/// nothing, so nothing is spawned; every case has its own scratch home under <c>_fixtures/</c>.
/// </remarks>
public sealed class EndedLimitMoveStoreTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ended-limit-move-store", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    public EndedLimitMoveStoreTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private MoveStore Store(string run) => new(_home, () => StoreSettled.Yes, each => !_dead.Contains(each.Run), run, StoreProcess.Shell);

    private MoveRecord? MoveFrom(string left) => Store("run-probe").Of(left).LastOrDefault();

    private static MoveDecision Decision(string left, bool fromEnded = false) =>
        new(left, MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = fromEnded };

    private sealed class Turned : ITurnedChat
    {
        public ChatTurns Turns { get; set; } = null!;

        IChatTurns ITurnedChat.Turns => Turns;

        public List<string> Prompts { get; } = [];

        public Task FinishAsync(Action endInput) => Task.CompletedTask;
    }

    private static Turned Talking(ChatContext context, string id, Action<ChatMessage>? wire = null)
    {
        var chat = new Turned();
        chat.Turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                wire?.Invoke(message);
                lock (chat.Prompts) chat.Prompts.Add(message.Prompt);
                sent();
                return Task.CompletedTask;
            },
            interrupt: () => Task.CompletedTask,
            changed: _ => { });
        context.Turned[id] = chat;
        return chat;
    }

    /// <summary>The runner's port with only its start: the record opened through the keyed door, its new part's turns told to the context.</summary>
    private sealed class Opening(ServiceClient client, ChatContext context, Action<ChatMessage>? wire) : IChatRunning
    {
        public Turned? Opened { get; private set; }

        /// <summary>Where the start is refused: no record is opened.</summary>
        public bool Refuses { get; set; }

        public async Task<ChatStart> StartAsync(
            string repository, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, string? profile = null,
            bool? ownTree = null, CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null,
            IReadOnlyList<ActorEffective>? order = null, EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false,
            bool ownFirst = false, MoveWords? moves = null)
        {
            if (Refuses) return new ChatStart(null, "the agent would not start");
            var opened = await client.OpenChatMovingAsync(repository, adapter, null, profile, tree, null, from, moves!, "default", ct);
            if (opened.SessionId is { } id) Opened = Talking(context, id, wire);
            return new ChatStart(opened.SessionId, opened.Message) { Moves = opened.Moves, Moved = opened.Moved };
        }

        public Task<HelpPlaced> PlaceHelpAsync(
            string adapter, DriverConfig config, string? workspace = null, CancellationToken ct = default, string? carries = null,
            IReadOnlyList<ActorEffective>? order = null) => throw new NotSupportedException();

        public Task<ChatStart> RunHelpAsync(
            HelpPlaced placed, DriverConfig config, Func<CancellationToken, Task<HelpMachine?>> machine, Func<string, string, Task>? onEnded = null,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ChatStart> RunAsync(
            string actor, string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
            ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn = null, ActorEffective? chosen = null,
            ChatPending? pending = null) => throw new NotSupportedException();

        public ChatPlace RepositoryPlace(
            DriverConfig config, IReadOnlyList<RepoView> registry, RepoView known, string repository, string workTree,
            IReadOnlyList<string>? continued = null, bool answers = false) => throw new NotSupportedException();

        public ChatPlace HelpPlace(string room, IReadOnlyList<HelpRead> reads, string? workspace = null) => throw new NotSupportedException();

        public Task<bool> AnswersFindingsAsync(string sessionId, string tree, DriverConfig config) => Task.FromResult(false);

        public bool Taking(string sessionId) => false;

        public ChatMessage WithProcess(string sessionId, ChatMessage message) => message;

        public Task<ConversationWalk> WalkAsync(
            string actor, IReadOnlyList<ActorEffective> order, DriverConfig config, string? workspace, string? profile, CancellationToken ct,
            (ActorEffective Entry, string? Account)? refused = null, bool counts = true, long except = 0) => throw new NotSupportedException();
    }

    private sealed class NoHelp : IChatHelping
    {
        public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp => null;

        public Task<string?> SetAsideHelpAsync(string? goingOn, string? workspace, CancellationToken ct) => throw new NotSupportedException();

        public Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(string sessionId, string? workspace, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");
    }

    private sealed record World(ChatContext Context, Opening Runner, LimitMove Limit, ChatLedger Ledger, ServiceClient Client);

    /// <summary>
    /// An ended conversation <c>c1</c> on account-1, nothing of it running, its last turn refused by its limit with <c>Make the port configurable.</c>
    /// (or, with <paramref name="limited"/> off, ended as any turn ends), the words named waiting on its record; the ended move keeps its record
    /// under this shell's run, <c>run-shell</c>.
    /// </summary>
    private World Ended(
        int opens, bool limited = true, Action<ChatMessage>? wire = null, string? profile = "account-1", params (string Id, string Text)[] waiting)
    {
        var ledger = new ChatLedger { Opens = opens }.Register("engine", _root);
        var client = ledger.Client();
        var words = ledger.Chat("c1", "stopped", "talk", _root, profile: profile);
        foreach (var (id, text) in waiting) words.Say(id, text);
        var events = Events;
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Earlier answered exchange." });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the port configurable." });
        if (limited)
        {
            events.Append("c1", new SessionEvent
            {
                Kind = SessionEventKind.Note, Text = "its account is cooling", Parts = [new NotePart { Code = NoteCodes.AccountCoolingWindow.Code }],
            });
            events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        }
        else
        {
            events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        }

        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var context = new ChatContext(
            client, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), output: null,
            new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)), events);
        var runner = new Opening(client, context, wire);
        var limit = new LimitMove(context, runner, new ChatHandOver(context, runner), new NoHelp())
        {
            Moves = new ConversationMoves(_home, client, events, store: Store("run-shell")),
        };
        return new World(context, runner, limit, ledger, client);
    }

    private static DriverConfig Config() => ActorChoices.With(DriverConfig.Empty, "chat", [new ActorChoice("talk")]);

    private static bool Posts(HttpRequestMessage request, string path) =>
        request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == path;

    /// <summary>
    /// 🔴 One ended move, written in the design's order (§13.4): decided at <c>old-ended</c> under this run's claim, since an ended record has no
    /// part to end; <c>opening</c> under the very key its keyed open is sent with; <c>opened</c> with the moved word by its id on the new part;
    /// <c>delivering</c> with the prompt marked before its write; the word's <c>sent</c> as the write returns and before its take off the new part,
    /// and <c>delivered</c> only after the take. No box is written at any point, and the word reaches the new part once, after the refused turn's.
    /// </summary>
    [Fact]
    public async Task The_ended_move_is_decided_at_old_ended_opening_opened_marked_sent_taken_and_delivered_with_no_box()
    {
        var seen = new List<string>();
        MoveRecord? atWire = null;
        IReadOnlyList<MoveRecordWord> wordsAtWire = [];
        string? boxes = null;
        var world = Ended(2, wire: _ =>
        {
            atWire = MoveFrom("c1");
            wordsAtWire = atWire is null ? [] : Store("run-probe").WordsOf(atWire.Id);
            lock (seen) seen.Add("wire");
        }, waiting: ("k1", "And add a test."));
        using var c = world.Client;
        string? openedUnder = null;
        MoveRecord? atOpen = null;
        MoveRecordWord? atTake = null;
        string? phaseAtTake = null;
        world.Ledger.BeforeRequest = (request, body) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (Posts(request, "/api/sessions/chat"))
            {
                atOpen = MoveFrom("c1");
                openedUnder = JsonNode.Parse(body!)!["opens"]?.GetValue<string>();
                boxes = string.Join(",", new LimitBoxes(_home).Pending());
                lock (seen) seen.Add("open");
            }
            else if (Posts(request, "/api/sessions/c2/taken"))
            {
                var move = MoveFrom("c1");
                phaseAtTake = move?.PhaseWord;
                atTake = move is null ? null : Store("run-probe").WordsOf(move.Id).SingleOrDefault();
                lock (seen) seen.Add("take");
            }

            return Task.CompletedTask;
        };

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Equal(("c2", true), (moved!.SessionId, moved.Sent));
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}, seen {string.Join(",", seen)}");
        Assert.Equal(["open", "wire", "take"], seen);

        // Decided at old-ended, asked under its key before the open, which is sent that key; no box beside it.
        Assert.NotNull(atOpen);
        Assert.Equal(("opening", "run-shell", "limit", "daoris", "conversation", "brief"), (atOpen!.PhaseWord, atOpen.ClaimRun, atOpen.Why, atOpen.By, atOpen.Executor, atOpen.How));
        Assert.True(atOpen.Moments.ContainsKey(MoveRecordPhase.OldEnded));
        Assert.StartsWith("chat-move/", atOpen.Opens, StringComparison.Ordinal);
        Assert.Equal(atOpen.Opens, openedUnder);
        Assert.Equal("", boxes);
        Assert.Empty(new LimitBoxes(_home).Pending());

        // Opened with the moved word by its id on the new part, and the prompt marked before its write.
        Assert.Equal(("delivering", "c2"), (atWire!.PhaseWord, atWire.Destination));
        Assert.Equal((MoveEffects.Prompt, (string?)null), (atWire.Effect!.What, atWire.Effect.Outcome));
        var word = Assert.Single(wordsAtWire);
        Assert.Equal(("k1", (string?)null), (word.Word, word.Disposition));

        // The brief's recipe is bound before the refused turns, whose words are read within the events the decision saw.
        var refused = LimitHeld.Of([.. Events.After("c1", 0).Events.Take(5)])!;
        Assert.Equal((HandOverWhy.Limit, (int?)refused.Before, (int?)5), (atWire.RecipeWhy, atWire.RecipeBound, atWire.RefusedBound));

        // `sent` before the take, `delivered` after it.
        Assert.Equal(("delivering", MoveWordDispositions.Sent), (phaseAtTake, atTake!.Disposition));
        var delivered = MoveFrom("c1")!;
        Assert.Equal((MoveWordDispositions.Sent, "c2"), (delivered.Effect!.Outcome, delivered.Destination));
        Assert.Single(world.Ledger.TakenBy, taken => taken.Session == "c2" && taken.Ids == "k1");
        Assert.DoesNotContain(world.Ledger.TakenBy, taken => taken.Session == "c1");
        Assert.Empty(world.Ledger.Said("c2"));
        var prompt = Assert.Single(world.Runner.Opened!.Prompts);
        Assert.Equal((1, 1), (Count(prompt, "Make the port configurable."), Count(prompt, "And add a test.")));
        Assert.True(prompt.IndexOf("Make the port configurable.", StringComparison.Ordinal) < prompt.IndexOf("And add a test.", StringComparison.Ordinal), prompt);
        Assert.Empty(new LimitBoxes(_home).Pending());
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>
    /// 🔴 The move's reason and its recipe are the ones its message is composed by: <c>limit</c> where a limit held the record, with the recipe
    /// bound before its refused turns; <c>resume</c> for a restart that found it with no limit, and <c>retry</c> for a move off an account that
    /// cools, each with the recipe bound at every event the decision saw.
    /// </summary>
    [Theory]
    [InlineData("limit", "limit")]
    [InlineData("restart", "resume")]
    [InlineData("switch", "retry")]
    public async Task The_decisions_why_and_recipe_are_the_ones_the_message_is_composed_by(string kind, string why)
    {
        var world = Ended(2, limited: kind == "limit");
        using var c = world.Client;
        var seen = Events.After("c1", 0).Events;
        var bound = LimitHeld.Of(seen)?.Before ?? seen.Count;

        var moved = kind == "switch"
            ? await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null, switching: true)
            : await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null, restarted: kind == "restart");

        Assert.Equal("c2", moved!.SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        var move = MoveFrom("c1")!;
        Assert.Equal((why, why, (int?)bound, (int?)seen.Count), (move.Why, move.RecipeWhy, move.RecipeBound, move.RefusedBound));
        Assert.Equal(("daoris", "conversation", "brief"), (move.By, move.Executor, move.How));
        Assert.Empty(new LimitBoxes(_home).Pending());
    }

    private static string Said(LineCourse answer) => answer switch
    {
        LineCourse.Hold hold => $"hold:{hold.Why}",
        LineCourse.Move move => $"move:{move.Why}:{move.By}:{move.Where.Source}",
        _ => answer.GetType().Name,
    };

    /// <summary>
    /// 🔴 FOUNDATION1h branch 5 (the foundation design §3.1, §14.2): the ended limit move decides through <c>CourseDecision.Decide</c>, each of
    /// its four entries by its own trigger: a limit's end, a restart or a conversation proven lost (the conversation lost), and a switch off a
    /// cooling account (the words kept). A move opens only where the course answers one; no account with room, or one the person picked
    /// whose conversation is not lost, is the course's hold and nothing opens.
    /// </summary>
    [Theory]
    [InlineData("limit", "move:Limit:Daoris:Settled", true)]
    [InlineData("restart", "move:Resume:Daoris:Settled", true)]
    [InlineData("lost", "move:Limit:Daoris:Settled", true)]
    [InlineData("switch", "move:Retry:Daoris:Settled", true)]
    [InlineData("none", "hold:Stays", false)]
    [InlineData("pick", "hold:Stays", false)]
    public async Task The_ended_move_asks_the_course_by_its_entry_and_only_a_move_opens(string what, string course, bool opens)
    {
        var world = Ended(2, limited: what is not ("restart" or "switch"), profile: what == "none" ? null : "account-1", waiting: ("w1", "Make it fast."));
        using var c = world.Client;
        if (what == "pick") world.Context.Conversations.Pick("c1", "account-1");
        var decided = new List<(string Session, LineCourse Answer)>();
        world.Limit.Decided += (session, answer) => decided.Add((session, answer));

        var moved = what switch
        {
            "lost" => await world.Limit.MoveEndedAsync("c1", new ContinueReason("session-not-found", "it is gone"), Config(), null),
            "switch" => await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null, switching: true),
            _ => await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null, restarted: what == "restart"),
        };

        Assert.Equal(opens, moved?.SessionId is not null);
        var (session, heard) = Assert.Single(decided);
        Assert.Equal(("c1", course), (session, Said(heard)));
    }

    /// <summary>
    /// 🔴 Where the ended move finds nothing to move for a conversation its limit held and proven lost, the course's lost answer is no briefed
    /// move in its place: the caller's fall-back to the briefed move is the caller's own decision (<c>BriefedPress</c> or its own trigger).
    /// </summary>
    [Fact]
    public void A_lost_conversation_with_no_room_is_no_briefed_move_in_the_limit_moves_place()
    {
        var head = new CourseHead(
            "stopped", Limited: true, Role: "chat", Adapter: "talk", Profile: "account-1", Conversation: true, Session: "c1",
            Waiting: [new CourseWord("w1", CourseAuthor.Person)], Tree: "tree");
        var none = new LineAnswer.Hold(NextHold.Cooling, LineRepair.None);

        var answer = CourseDecision.Decide(
            new CourseFacts(head, CourseBoundary.ProcessExited, Account: new CourseAccount(Answer: none), Rules: new CourseRules(Runner: true)),
            CourseTrigger.ConversationLost);

        Assert.IsNotType<LineCourse.Move>(answer);
    }

    /// <summary>
    /// 🔴 A conversation proven lost (<c>lost</c>) goes on in the walk's account whichever it is, its record decided as any ended move's, no box
    /// written.
    /// </summary>
    [Fact]
    public async Task A_lost_conversations_move_is_decided_and_writes_no_box()
    {
        var world = Ended(2);
        using var c = world.Client;

        var moved = await world.Limit.MoveEndedAsync("c1", new ContinueReason("session-not-found", "it is gone"), Config(), null);

        Assert.Equal("c2", moved!.SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        Assert.Equal(HandOverWhy.Limit, MoveFrom("c1")!.Why);
        Assert.Empty(new LimitBoxes(_home).Pending());
    }

    /// <summary>
    /// 🔴 Against a host whose keyed open moves nothing (its mark 1), the move is decided all the same: a move of no word, closed
    /// <c>delivered</c> on its prompt's outcome as the write returns, the old record's words taken off it named as taken by the new part.
    /// </summary>
    [Fact]
    public async Task Against_an_older_host_the_move_is_one_of_no_word_and_takes_the_old_records_words_off_it()
    {
        var world = Ended(1, waiting: ("w1", "Make it fast."));
        using var c = world.Client;

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Equal("c2", moved!.SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered" && world.Ledger.Said("c1").Count == 0, () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        var move = MoveFrom("c1")!;
        Assert.Empty(Store("run-probe").WordsOf(move.Id));
        Assert.Equal(("c2", MoveEffects.Prompt, MoveWordDispositions.Sent), (move.Destination, move.Effect!.What, move.Effect.Outcome));
        Assert.Contains(world.Ledger.TakenBy, taken => taken.Session == "c1" && taken.Ids == "w1" && taken.By == "c2");
        Assert.Equal(1, Count(Assert.Single(world.Runner.Opened!.Prompts), "Make it fast."));
        Assert.Empty(new LimitBoxes(_home).Pending());
    }

    /// <summary>
    /// 🔴 LIMITWORD1 against a host that moves nothing: a word the person kept on the old record after the move was decided, before its open, goes
    /// to the new part once with the rest, and leaves the old record named as taken by the new part.
    /// </summary>
    [Fact]
    public async Task Against_an_older_host_a_word_kept_after_the_decision_reaches_the_new_part_once()
    {
        var world = Ended(1, waiting: ("w1", "Make it fast."));
        using var c = world.Client;
        var late = 0;
        world.Ledger.BeforeRequest = (request, _) =>
        {
            if (Posts(request, "/api/sessions/chat") && Interlocked.Exchange(ref late, 1) == 0) world.Ledger.For("c1").Say("w2", "And add a test.");
            return Task.CompletedTask;
        };

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Equal("c2", moved!.SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered" && world.Ledger.Said("c1").Count == 0, () => $"said {string.Join(",", world.Ledger.Said("c1"))}");
        var prompt = Assert.Single(world.Runner.Opened!.Prompts);
        Assert.Equal((1, 1), (Count(prompt, "Make it fast."), Count(prompt, "And add a test.")));
        Assert.Contains(world.Ledger.TakenBy, taken => taken.Session == "c1" && taken.Ids == "w1,w2" && taken.By == "c2");
        Assert.Empty(world.Ledger.Said("c2"));
    }

    /// <summary>
    /// 🔴 A word kept after the decision against a host that moves them: the open moves it with the rest, so it reaches the new part once, after
    /// the refused turn's words, and the old record keeps none to be taken.
    /// </summary>
    [Fact]
    public async Task A_word_kept_after_the_decision_is_moved_by_the_open_and_reaches_the_new_part_once()
    {
        var world = Ended(2, waiting: ("w1", "Make it fast."));
        using var c = world.Client;
        var late = 0;
        world.Ledger.BeforeRequest = (request, _) =>
        {
            if (Posts(request, "/api/sessions/chat") && Interlocked.Exchange(ref late, 1) == 0) world.Ledger.For("c1").Say("w2", "And add a test.");
            return Task.CompletedTask;
        };

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Equal("c2", moved!.SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        var prompt = Assert.Single(world.Runner.Opened!.Prompts);
        Assert.Equal((1, 1, 1), (Count(prompt, "Make the port configurable."), Count(prompt, "Make it fast."), Count(prompt, "And add a test.")));
        Assert.Equal(["w1", "w2"], Store("run-probe").WordsOf(MoveFrom("c1")!.Id).Select(word => word.Word));
        Assert.Contains(world.Ledger.TakenBy, taken => taken.Session == "c2" && taken.Ids == "w1,w2");
        Assert.Empty(world.Ledger.Said("c1"));
        Assert.Empty(world.Ledger.Said("c2"));
    }

    /// <summary>
    /// 🔴 A record a move still stands for takes no move now (§3.1's <c>Never</c>): nothing is written for it, no open is asked, and its words
    /// stay where they are.
    /// </summary>
    [Fact]
    public async Task A_standing_move_for_the_record_means_no_move_now()
    {
        var world = Ended(2, waiting: ("w1", "Make it fast."));
        using var c = world.Client;
        var other = Store("run-other");
        var standing = other.Decide(Decision("c1", fromEnded: true));

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Null(moved);
        Assert.Equal([standing.Move.Id], Store("run-probe").Of("c1").Select(move => move.Id));
        Assert.Empty(world.Ledger.Opened);
        Assert.Null(world.Runner.Opened);
        Assert.Equal(["w1"], world.Ledger.Said("c1"));
    }

    /// <summary>
    /// 🔴 A store that will not take the move's write before its open moves nothing: the claim moved to another run between the decision and
    /// the open's key, so no open is asked, the words stay on the old record and the person is told.
    /// </summary>
    [Fact]
    public async Task A_refused_write_before_the_open_asks_no_open_and_leaves_the_words()
    {
        var world = Ended(2, waiting: ("w1", "Make it fast."));
        using var c = world.Client;
        world.Limit.Moves.Cut = name =>
        {
            if (name != Cuts.Decided) return;
            _dead.Add("run-shell");
            var move = MoveFrom("c1")!;
            Assert.Null(Store("run-thief").Take(move.Id, move.ClaimGeneration).Refused);
        };

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Null(moved!.SessionId);
        Assert.Empty(world.Ledger.Opened);
        Assert.Equal(["w1"], world.Ledger.Said("c1"));
        Assert.Equal(("run-thief", "old-ended"), (MoveFrom("c1")!.ClaimRun, MoveFrom("c1")!.PhaseWord));
        Assert.Contains(Events.After("c1", 0).Events, e => e.Parts?.Any(part => part.Code == NoteCodes.ChatLimitNotMoved.Code) == true);
        Assert.Empty(new LimitBoxes(_home).Pending());
    }

    /// <summary>
    /// 🔴 A store that will not take the open's answer makes no hand: the new part is opened and holds its moved word, never attempted and never
    /// written, which its own go-on carries; the old record holds none.
    /// </summary>
    [Fact]
    public async Task A_refused_write_after_the_open_makes_no_hand_and_the_moved_word_waits_on_the_new_part()
    {
        var world = Ended(2, waiting: ("w1", "Make it fast."));
        using var c = world.Client;
        world.Limit.Moves.Cut = name =>
        {
            if (name != Cuts.Opening) return;
            _dead.Add("run-shell");
            var move = MoveFrom("c1")!;
            Assert.Null(Store("run-thief").Take(move.Id, move.ClaimGeneration).Refused);
        };

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Equal(("c2", false), (moved!.SessionId, moved.Sent));
        Assert.Single(world.Ledger.Opened);
        Assert.Empty(world.Runner.Opened!.Prompts);
        Assert.Empty(world.Ledger.TakenBy);
        Assert.Equal(["w1"], world.Ledger.Said("c2"));
        Assert.Empty(world.Ledger.Said("c1"));
        Assert.Empty(new LimitBoxes(_home).Pending());
    }

    /// <summary>
    /// 🔴 An open the host refuses ends the move by its phase, the old part being an ended one that stays as it was: the open asked and no
    /// answer recorded, held as an effect never recorded and closed at once, naming nothing; the words stay on the old record, no box left.
    /// </summary>
    [Fact]
    public async Task A_refused_open_ends_the_move_by_its_phase_and_leaves_the_words_and_no_box()
    {
        var world = Ended(2, waiting: ("w1", "Make it fast."));
        using var c = world.Client;
        world.Runner.Refuses = true;

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Null(moved!.SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "closed", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        Assert.Equal(MoveCloseWhy.Resolved, MoveFrom("c1")!.ClosedWhy);
        Assert.Equal(["w1"], world.Ledger.Said("c1"));
        Assert.Empty(new LimitBoxes(_home).Pending());
    }

    // ——— A box an older build wrote (§3.3, §13.5): imported once at the shell's come-up, and its record `Never` until it is.

    private static ChatLedger Records(params string[] ids)
    {
        var ledger = new ChatLedger();
        foreach (var id in ids) ledger.Chat(id, "stopped", "talk", "engine", profile: "account-1");
        return ledger;
    }

    private string BoxOf(string session) => new LimitBoxes(_home).PathOf(session);

    /// <summary>
    /// 🔴 A box an older build wrote is imported once at the conversations' come-up, once the start's confirmation is settled, and set aside as
    /// <c>.imported</c>, and the move goes on from its phase there as a dead shell's does: a box with no new part falls back on its old part, to
    /// be made again by the wake; one that names its new part and the words its open moved is read by what the new part holds, its words
    /// waiting there never attempted, so the part's own go-on carries them; one that names it and moved none is opened with no word. A second
    /// come-up finds nothing to import.
    /// </summary>
    [Theory]
    [InlineData("old-ended", "fell-back")]
    [InlineData("moved", "closed")]
    [InlineData("unmoved", "closed")]
    public async Task The_come_up_imports_a_box_an_older_build_wrote_once_and_goes_on_from_its_phase(string kind, string ended)
    {
        var ledger = Records("c1");

        // A box with no new part has no record that continues its old one: one that did would be an older build's own, and supersede the move.
        if (kind != "old-ended") ledger.Chat("c2", "stopped", "talk", "engine", from: new ContinuedFrom("c1", HandOverWhy.Limit, HandOverBy.Daoris)).Say("w1", "Make it fast.");
        else ledger.Chat("c2", "stopped", "talk", "engine").Say("w1", "Make it fast.");
        using var client = ledger.Client();
        var events = Events;
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the port configurable." });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        var box = kind switch
        {
            "old-ended" => new LimitMoveBox(HandOverWhy.Limit, 0, 2, ["w1"]),
            "moved" => new LimitMoveBox(HandOverWhy.Limit, 0, 2, ["w1"], "c2", Moved: true),
            _ => new LimitMoveBox(HandOverWhy.Limit, 0, 2, ["w1"], "c2"),
        };
        new LimitBoxes(_home).Write("c1", box);
        var bytes = File.ReadAllBytes(BoxOf("c1"));
        var moves = new ConversationMoves(_home, client, events, store: Store("run-shell"));

        var look = await moves.ComeUpAsync(say: null, CancellationToken.None);
        var again = await moves.ComeUpAsync(say: null, CancellationToken.None);

        var imported = Assert.Single(look!.Imported);
        var now = Store("run-probe").Read(imported.Id)!;
        Assert.Equal((ended, "c1", "run-shell"), (now.PhaseWord, now.Left, now.ClaimRun));
        if (ended == "fell-back") Assert.Equal(ConversationMoves.NotOpened, now.FellBackCode);
        else Assert.Equal(("c2", MoveCloseWhy.Resolved), (now.Destination, now.ClosedWhy));
        Assert.Equal(kind == "moved" ? ["w1"] : [], Store("run-probe").WordsOf(now.Id).Select(word => word.Word));

        // Set aside whole, never deleted; no box left for today's readers, nothing imported twice, no word taken or said by the importing.
        Assert.False(File.Exists(BoxOf("c1")));
        Assert.Equal(bytes, File.ReadAllBytes(BoxOf("c1") + ".imported"));
        Assert.Empty(again!.Imported);
        Assert.Single(Store("run-probe").Of("c1"));
        Assert.Equal(["w1"], ledger.Said("c2"));
        Assert.Empty(ledger.TakenBy);
    }

    /// <summary>
    /// 🔴 Until a box is imported its record is the course's <c>Never</c>: with the start's confirmation not settled the come-up imports nothing,
    /// the box stays in place and names the record held, and the go-on, the come-up's go-ons and the wake move nothing on it.
    /// </summary>
    [Fact]
    public async Task A_box_the_start_has_not_settled_for_stays_in_place_and_nothing_goes_on_or_moves_on_its_record()
    {
        var ledger = Records("c1");
        ledger.Chat("c3", "stopped", "talk", "engine", profile: "account-1");
        ledger.For("c1").Say("w1", "Make it fast.");
        using var client = ledger.Client();
        var events = Events;
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        new LimitBoxes(_home).Write("c1", new LimitMoveBox(HandOverWhy.Limit, 0, 1, ["w1"]));
        var unsettled = new MoveStore(_home, () => new StoreSettled(false, "the install has not confirmed"), _ => true, "run-shell", StoreProcess.Shell);
        var moves = new ConversationMoves(_home, client, events, store: unsettled);

        var look = await moves.ComeUpAsync(say: null, CancellationToken.None);

        Assert.Empty(look!.Imported);
        Assert.Equal("c1", Assert.Single(look.Never));
        Assert.True(File.Exists(BoxOf("c1")));
        Assert.False(File.Exists(BoxOf("c1") + ".imported"));
        Assert.Empty(Store("run-probe").Of("c1"));
    }

    /// <summary>
    /// 🔴 The go-on and the shell's come-up leave a record whose box an older build wrote alone while it is in place (§3.3's <c>Never</c>), here
    /// held behind another run's standing move for the record: its words wait, its state stays, nothing is read or settled on it.
    /// </summary>
    [Fact]
    public async Task A_box_in_place_keeps_the_go_on_and_the_take_up_off_its_record()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        new HarnessConversations(_home).Keep("c1", "talk", "conv-c1");
        Store("run-other").Decide(Decision("c1", fromEnded: true));
        new LimitBoxes(_home).Write("c1", new LimitMoveBox(HandOverWhy.Limit, 0, 1, ["w1"]));
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        using var runner = new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        var went = await runner.GoOnAsync("c1");
        var taken = await runner.TakeUpAsync();

        Assert.Null(went.SessionId);
        Assert.Contains("older Daoris", went.Message, StringComparison.Ordinal);
        Assert.Empty(taken);
        Assert.Equal("completed", ledger.State("c1"));
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Null(new GoOnMarks(_home).Read("c1"));
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>
    /// 🔴 A box an older build wrote for a record that already has a standing move waits behind it, left in place: the importing leaves both
    /// records as they are, and the box is imported once that move ends.
    /// </summary>
    [Fact]
    public async Task A_box_behind_a_standing_move_is_left_in_place_and_imported_once_that_move_ends()
    {
        var ledger = Records("c1", "c2");
        using var client = ledger.Client();
        var events = Events;
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        var other = Store("run-other");
        var standing = other.Decide(Decision("c1", fromEnded: true));
        new LimitBoxes(_home).Write("c1", new LimitMoveBox(HandOverWhy.Limit, 0, 1, []));
        var moves = new ConversationMoves(_home, client, events, store: Store("run-shell"));

        var behind = await moves.ComeUpAsync(say: null, CancellationToken.None);

        Assert.Empty(behind!.Imported);
        Assert.Equal("c1", Assert.Single(behind.Never));
        Assert.True(File.Exists(BoxOf("c1")));
        Assert.Equal([standing.Move.Id], Store("run-probe").Of("c1").Select(move => move.Id));

        other.Close(standing.Claim!, MoveCloseWhy.Superseded);
        var after = await moves.ComeUpAsync(say: null, CancellationToken.None);

        Assert.Single(after!.Imported);
        Assert.False(File.Exists(BoxOf("c1")));
        Assert.True(File.Exists(BoxOf("c1") + ".imported"));
    }

    /// <summary>
    /// 🔴 A box an older build wrote for the record that is still in place is a move not yet imported: the course reads the record as
    /// <c>Never</c> (§3.3), so no move is decided over it and nothing is opened.
    /// </summary>
    [Fact]
    public async Task A_box_still_in_place_keeps_the_move_off_its_record()
    {
        var world = Ended(2, waiting: ("w1", "Make it fast."));
        using var c = world.Client;
        new LimitBoxes(_home).Write("c1", new LimitMoveBox(HandOverWhy.Limit, 2, 5, ["w1"]));

        var moved = await world.Limit.MoveEndedAsync("c1", lost: null, Config(), null);

        Assert.Null(moved);
        Assert.Empty(Store("run-probe").Of("c1"));
        Assert.Empty(world.Ledger.Opened);
        Assert.NotNull(new LimitBoxes(_home).Read("c1"));
    }
}
