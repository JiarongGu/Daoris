using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-ii part a (D212; the foundation design §3.3, §13.4 steps 1 to 4, §19.4): the live limit move keeps its move record. Decided
/// as the turns are held, under this run's claim; <c>old-ended</c> once the old part ends; <c>opening</c> under the key its keyed open is sent
/// with; <c>opened</c> with every word the open moved, by its id on the destination; <c>delivering</c> and the prompt's mark before the hand;
/// each handed word's <c>sent</c> as the turn's write returns, then the take, then <c>delivered</c>. A move of no word closes on its effect's
/// outcome; a record that is a standing move's destination is answered that move and nothing is written, a resolvable hold closed first. The
/// conversations' take-up, at the shell's come-up and at each look, takes a dead run's move once and says a live run's quiet once.
/// </summary>
/// <remarks>
/// The ledger is the in-process one at its <c>opens</c> mark; the conversations' turns are the real queue over a door that writes nothing, so
/// nothing is spawned. Every case has its own scratch home under <c>_fixtures/</c>. One process is one run to the lease's test, so a run's death
/// is the liveness each case hands its stores, or a start time no process has.
/// </remarks>
public sealed class LimitMoveStoreTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "limit-move-store", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    public LimitMoveStoreTests()
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

    /// <summary>A move store of one run, dead where the case says so and alive otherwise.</summary>
    private MoveStore Store(string run, TimeProvider? clock = null) =>
        new(_home, () => StoreSettled.Yes, each => !_dead.Contains(each.Run), run, StoreProcess.Shell, clock);

    private static MoveDecision Decision(string left, bool fromEnded = false) =>
        new(left, MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = fromEnded };

    /// <summary>A conversation's turns over a door that writes nothing: <paramref name="wire"/> is told each message as its write is made, before it returns.</summary>
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
        public List<MoveWords?> Asked { get; } = [];

        public Turned? Opened { get; private set; }

        public async Task<ChatStart> StartAsync(
            string repository, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, string? profile = null,
            bool? ownTree = null, CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null,
            IReadOnlyList<ActorEffective>? order = null, EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false,
            bool ownFirst = false, MoveWords? moves = null)
        {
            Asked.Add(moves);
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

    /// <summary>Ask Daoris's port, never asked here: every case is a repository's conversation.</summary>
    private sealed class NoHelp : IChatHelping
    {
        public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp => null;

        public Task<string?> SetAsideHelpAsync(string? goingOn, string? workspace, CancellationToken ct) => throw new NotSupportedException();

        public Task<(string Room, IReadOnlyList<HelpRead> Reads)> HelpRoomNowAsync(string sessionId, string? workspace, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>A conversation's adapter on the protocol door with no toolchain to ask, so nothing is probed or spawned.</summary>
    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");
    }

    private sealed record World(ChatContext Context, Opening Runner, LimitMove Limit, ChatLedger Ledger, ServiceClient Client, Turned Old)
    {
        /// <summary>
        /// The old part's end as its watch writes it, which no process here makes: its record ends once the new part's open is asked, as the
        /// move's end of the old part leaves it before its open (D51).
        /// </summary>
        public void Ends(HttpRequestMessage request)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat") Ledger.Edit("c1", record => record["state"] = "stopped");
        }
    }

    /// <summary>
    /// A live conversation <c>c1</c> on account-1, its last turn refused by its limit with <c>Make the port configurable.</c>, its turns held by
    /// nothing; the limit's move keeps its record under this shell's run, <c>run-shell</c>.
    /// </summary>
    private World Live(int opens, Action<ChatMessage>? wire = null, string? profile = "account-1")
    {
        var ledger = new ChatLedger { Opens = opens }.Register("engine", _root);
        var client = ledger.Client();
        ledger.Chat("c1", "working", "talk", _root, profile: profile);
        var events = Events;
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Earlier answered exchange." });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the port configurable." });
        events.Append("c1", new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = "its account is cooling", Parts = [new NotePart { Code = NoteCodes.AccountCoolingWindow.Code }],
        });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var context = new ChatContext(
            client, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), output: null,
            new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)), events);
        var old = Talking(context, "c1");
        var runner = new Opening(client, context, wire);
        var limit = new LimitMove(context, runner, new ChatHandOver(context, runner), new NoHelp())
        {
            Moves = new ConversationMoves(_home, client, events, store: Store("run-shell")),
        };
        var world = new World(context, runner, limit, ledger, client, old);
        ledger.BeforeRequest = (request, _) =>
        {
            world.Ends(request);
            return Task.CompletedTask;
        };
        return world;
    }

    private static DriverConfig Config() => ActorChoices.With(DriverConfig.Empty, "chat", [new ActorChoice("talk")]);

    private MoveRecord? MoveFrom(string left) => Store("run-probe").Of(left).LastOrDefault();

    /// <summary>
    /// 🔴 One live move, written in the design's order (§13.4): decided under this run's claim, then <c>opening</c> under the very key its keyed
    /// open is sent with, then <c>opened</c> with the moved word by its id on the new part, then <c>delivering</c> with the prompt marked before
    /// its write; the word's <c>sent</c> as the write returns and before its take off the new part, and <c>delivered</c> only after the take.
    /// The word reaches the new part once.
    /// </summary>
    [Fact]
    public async Task The_live_move_is_decided_opening_opened_marked_sent_taken_and_delivered_in_that_order()
    {
        var seen = new List<string>();
        MoveRecord? atWire = null;
        IReadOnlyList<MoveRecordWord> wordsAtWire = [];
        var world = Live(opens: 2, wire: _ =>
        {
            atWire = MoveFrom("c1");
            wordsAtWire = atWire is null ? [] : Store("run-probe").WordsOf(atWire.Id);
            lock (seen) seen.Add("wire");
        });
        using var c = world.Client;
        world.Ledger.For("c1").Say("k1", "And add a test.");
        string? openedUnder = null;
        MoveRecord? atOpen = null;
        MoveRecordWord? atTake = null;
        string? phaseAtTake = null;
        world.Ledger.BeforeRequest = (request, body) =>
        {
            world.Ends(request);
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path == "/api/sessions/chat")
            {
                atOpen = MoveFrom("c1");
                openedUnder = JsonNode.Parse(body!)!["opens"]?.GetValue<string>();
                lock (seen) seen.Add("open");
            }
            else if (request.Method == HttpMethod.Post && path == "/api/sessions/c2/taken")
            {
                var move = MoveFrom("c1");
                phaseAtTake = move?.PhaseWord;
                atTake = move is null ? null : Store("run-probe").WordsOf(move.Id).SingleOrDefault();
                lock (seen) seen.Add("take");
            }

            return Task.CompletedTask;
        };

        var moved = await world.Limit.MoveAsync("c1", Config(), null);

        Assert.Equal(("c2", true), (moved!.SessionId, moved.Sent));
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}, seen {string.Join(",", seen)}");
        Assert.Equal(["open", "wire", "take"], seen);

        // Decided, ended and asked under its key before the open, which is sent that key.
        Assert.NotNull(atOpen);
        Assert.Equal(("opening", "run-shell", "limit", "daoris", "conversation", "brief"), (atOpen!.PhaseWord, atOpen.ClaimRun, atOpen.Why, atOpen.By, atOpen.Executor, atOpen.How));
        Assert.True(atOpen.Moments.ContainsKey(MoveRecordPhase.Decided) && atOpen.Moments.ContainsKey(MoveRecordPhase.OldEnded));
        Assert.StartsWith("chat-move/", atOpen.Opens, StringComparison.Ordinal);
        Assert.Equal(atOpen.Opens, openedUnder);

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
        Assert.Empty(world.Ledger.Said("c2"));
        Assert.Equal(1, world.Runner.Opened!.Prompts.Count(prompt => prompt.Contains("And add a test.", StringComparison.Ordinal)));
    }

    /// <summary>
    /// 🔴 Against a host whose keyed open moves nothing (its mark 1), the move is decided all the same and its record names what was done: a move
    /// of no word, closed <c>delivered</c> once its prompt's outcome is recorded as the write returns.
    /// </summary>
    [Fact]
    public async Task A_move_of_no_word_closes_delivered_on_its_effects_outcome()
    {
        var world = Live(opens: 1);
        using var c = world.Client;

        var moved = await world.Limit.MoveAsync("c1", Config(), null);

        Assert.Equal("c2", moved!.SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        var move = MoveFrom("c1")!;
        Assert.Empty(Store("run-probe").WordsOf(move.Id));
        Assert.Equal(("c2", MoveEffects.Prompt, MoveWordDispositions.Sent), (move.Destination, move.Effect!.What, move.Effect.Outcome));
        Assert.Equal(move.Opens, Assert.Single(world.Ledger.Opened).Key);
    }

    /// <summary>
    /// 🔴 A record that is the destination of another live run's standing move takes no move of its own (§3.1's <c>Never</c>, §3.3): the store
    /// answers that move, nothing is written for the record, no open is asked, and its turns are left as they were.
    /// </summary>
    [Fact]
    public async Task A_destinations_own_move_is_answered_as_standing_and_nothing_is_written()
    {
        var world = Live(opens: 2);
        using var c = world.Client;
        var other = Store("run-other");
        var into = other.Decide(Decision("c0", fromEnded: true));
        other.Opening(into.Claim!, "chat-move/other");
        other.Opened(into.Claim!, "c1", []);

        var moved = await world.Limit.MoveAsync("c1", Config(), null);

        Assert.Null(moved);
        Assert.Empty(Store("run-probe").Of("c1"));
        Assert.Empty(world.Ledger.Opened);
        Assert.Null(world.Runner.Opened);
        Assert.True(world.Old.Turns.Hold(), "the move left the old part's turns held");
        world.Old.Turns.Release();
        Assert.Equal("opened", Store("run-probe").Read(into.Move.Id)!.PhaseWord);
    }

    /// <summary>
    /// 🔴 This run's hold on the move that made the record, naming nothing that waits, is closed <c>resolved</c> by §3.3's one rule as the course
    /// reads the record's standing move, before its decision: the record then moves.
    /// </summary>
    [Fact]
    public async Task A_resolvable_hold_on_the_destination_is_closed_first_and_the_move_goes_on()
    {
        var world = Live(opens: 2);
        using var c = world.Client;
        var shell = Store("run-shell");
        var into = shell.Decide(Decision("c0", fromEnded: true));
        shell.Opening(into.Claim!, "chat-move/earlier");
        shell.Opened(into.Claim!, "c1", []);
        shell.Hold(into.Claim!, MoveHoldWhy.Undelivered);

        var moved = await world.Limit.MoveAsync("c1", Config(), null);

        Assert.Equal("c2", moved!.SessionId);
        var held = Store("run-probe").Read(into.Move.Id)!;
        Assert.Equal(("closed", MoveCloseWhy.Resolved), (held.PhaseWord, held.ClosedWhy));
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
    }

    /// <summary>
    /// 🔴 This run's move that made the record, past its write (its word's <c>sent</c> written, its take and reading not yet made), is read as
    /// the course reads the record's standing move: its sent word taken off the record and never handed again, the move closed
    /// <c>delivered</c>; the record then moves, so a new part refused at once by its limit is not held by the move that made it.
    /// </summary>
    [Fact]
    public async Task This_runs_move_past_its_write_is_read_first_and_the_record_it_made_then_moves()
    {
        var world = Live(opens: 2);
        using var c = world.Client;
        world.Ledger.For("c1").Say("w0", "Also log the port.");
        var shell = Store("run-shell");
        var into = shell.Decide(Decision("c0", fromEnded: true));
        shell.Opening(into.Claim!, "chat-move/earlier");
        shell.Opened(into.Claim!, "c1", [new MoveRecordWord("w0", null, "chat-move/earlier/w0", 0)]);
        shell.Advance(into.Claim!, MoveRecordPhase.Delivering);
        shell.MarkEffect(into.Claim!, MoveEffects.Prompt);
        shell.Sent(into.Claim!, ["chat-move/earlier/w0"]);

        var moved = await world.Limit.MoveAsync("c1", Config(), null);

        Assert.Equal("c2", moved!.SessionId);
        Assert.Equal("delivered", Store("run-probe").Read(into.Move.Id)!.PhaseWord);
        Assert.Contains(world.Ledger.TakenBy, taken => taken.Session == "c1" && taken.Ids == "w0");
        Assert.Empty(world.Ledger.Said("c2"));
        Assert.DoesNotContain(world.Runner.Opened!.Prompts, prompt => prompt.Contains("Also log the port.", StringComparison.Ordinal));
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-ii part b (§13.4 step 5, §19.3): before the new part is handed anything its line's head is read; where an older build has
    /// continued the new part meanwhile (a record from it that no key of the move's made), nothing is written to it and the move is held
    /// <c>superseded</c>, naming the person's word that waits there, which stays there never attempted for the person to take back.
    /// </summary>
    [Fact]
    public async Task A_head_moved_past_the_new_part_before_the_hand_holds_the_move_and_hands_nothing()
    {
        var world = Live(opens: 2);
        using var c = world.Client;
        world.Ledger.For("c1").Say("k1", "And add a test.");
        world.Limit.Moves.Cut = name =>
        {
            if (name == Cuts.Opened) world.Ledger.Chat("c3", "stopped", "talk", _root, from: new ContinuedFrom("c2", HandOverWhy.Limit, HandOverBy.Daoris));
        };

        var moved = await world.Limit.MoveAsync("c1", Config(), null);

        Assert.Equal(("c2", false), (moved!.SessionId, moved.Sent));
        var move = MoveFrom("c1")!;
        Assert.Equal(("held", MoveHoldWhy.Superseded, "c2"), (move.PhaseWord, move.HeldWhy, move.Destination));
        var held = Assert.Single(Store("run-probe").WordsOf(move.Id));
        Assert.Equal(("k1", MoveWordDispositions.Held), (held.Word, held.Disposition));
        Assert.Empty(world.Runner.Opened!.Prompts);
        Assert.Empty(world.Ledger.TakenBy);
        Assert.Equal(["k1"], world.Ledger.Said("c2"));
        Assert.Null(move.Effect);
    }

    private static string Said(LineCourse answer) => answer switch
    {
        LineCourse.Never never => $"never:{never.Why}",
        LineCourse.Hold hold => $"hold:{hold.Why}",
        LineCourse.Move move => $"move:{move.Why}:{move.By}:{move.Where.Source}",
        _ => answer.GetType().Name,
    };

    /// <summary>
    /// 🔴 FOUNDATION1h branch 5 (the foundation design §3.1, §14.2): the live limit move decides through <c>CourseDecision.Decide</c> for
    /// <see cref="CourseTrigger.LimitEnd"/>. Heard from the move, another account with room is a <c>limit</c> move by Daoris on the walk's
    /// account; none with room, or an account the person picked, is the course's <c>Stays</c>, and nothing opens or is held.
    /// </summary>
    [Theory]
    [InlineData("room", "move:Limit:Daoris:Settled", true)]
    [InlineData("none", "hold:Stays", false)]
    [InlineData("pick", "hold:Stays", false)]
    public async Task The_live_move_asks_the_course_and_only_a_move_opens(string what, string course, bool opens)
    {
        var world = Live(opens: 2, profile: what == "none" ? null : "account-1");
        using var c = world.Client;
        if (what == "pick") world.Context.Conversations.Pick("c1", "account-1");
        var decided = new List<(string Session, LineCourse Answer)>();
        world.Limit.Decided += (session, answer) => decided.Add((session, answer));

        var moved = await world.Limit.MoveAsync("c1", Config(), null);

        Assert.Equal(opens, moved?.SessionId is not null);
        var (session, heard) = Assert.Single(decided);
        Assert.Equal(("c1", course), (session, Said(heard)));
        Assert.Equal(opens, world.Runner.Opened is not null);
    }

    // ——— The conversations' take-up (§3.3, §13.4, §19.1).

    /// <summary>A record of this machine's for each id, so a move's records stand.</summary>
    private static ChatLedger Records(params string[] ids)
    {
        var ledger = new ChatLedger();
        foreach (var id in ids) ledger.Chat(id, "stopped", "talk", "engine");
        return ledger;
    }

    /// <summary>
    /// 🔴 A dead shell's move is taken up once by the conversations' take-up and ended by its phase, so its record goes on by its own come-up:
    /// before the open asked, it falls back on the old part; with its open asked and never answered, it is held as an effect never recorded and
    /// closes, naming nothing that waits; opened with nothing delivered, it is held as undelivered and closes. A second look takes nothing.
    /// </summary>
    [Theory]
    [InlineData("decided", "fell-back")]
    [InlineData("old-ended", "fell-back")]
    [InlineData("opening", "closed")]
    [InlineData("opened", "closed")]
    public async Task The_conversations_take_up_takes_a_dead_runs_move_once_and_ends_it_by_its_phase(string phase, string ended)
    {
        var ledger = Records("c1", "c2");
        using var client = ledger.Client();
        var dead = Store("run-dead");
        var decided = dead.Decide(Decision("c1"));
        var claim = decided.Claim!;
        if (phase != "decided") dead.Advance(claim, MoveRecordPhase.OldEnded);
        if (phase is "opening" or "opened") dead.Opening(claim, "chat-move/dead");
        if (phase == "opened") dead.Opened(claim, "c2", []);
        _dead.Add("run-dead");
        var moves = new ConversationMoves(_home, client, Events, store: Store("run-shell"));

        var look = await moves.LookAsync(CancellationToken.None);
        var again = await moves.LookAsync(CancellationToken.None);

        var now = Store("run-probe").Read(decided.Move.Id)!;
        Assert.Equal((ended, "run-shell", 2L), (now.PhaseWord, now.ClaimRun, now.ClaimGeneration));
        if (ended == "fell-back") Assert.Equal(ConversationMoves.NotOpened, now.FellBackCode);
        else Assert.Equal((phase == "opening" ? MoveHoldWhy.Unrecorded : MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved), (now.HeldWhy, now.ClosedWhy));
        Assert.Equal([decided.Move.Id], look!.Taken.Select(each => each.Move!.Id));
        Assert.Empty(again!.Taken);
        Assert.Empty(again.Refused);
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-ii part b (§13.5's first row): a dead run's move before its open was asked, whose old part an older build has since
    /// continued (a record from it, under no key of the move's), closes <c>superseded</c> at the take-up rather than falling back on a part
    /// that went on elsewhere.
    /// </summary>
    [Theory]
    [InlineData("decided")]
    [InlineData("old-ended")]
    public async Task The_take_up_closes_a_dead_runs_unopened_move_superseded_where_an_older_build_continued_its_old_part(string phase)
    {
        var ledger = Records("c1");
        ledger.Chat("c3", "stopped", "talk", "engine", from: new ContinuedFrom("c1", HandOverWhy.Limit, HandOverBy.Daoris));
        using var client = ledger.Client();
        var dead = Store("run-dead");
        var decided = dead.Decide(Decision("c1"));
        if (phase == "old-ended") dead.Advance(decided.Claim!, MoveRecordPhase.OldEnded);
        _dead.Add("run-dead");
        var moves = new ConversationMoves(_home, client, Events, store: Store("run-shell"));

        await moves.LookAsync(CancellationToken.None);

        var now = Store("run-probe").Read(decided.Move.Id)!;
        Assert.Equal(("closed", MoveCloseWhy.Superseded, (string?)null), (now.PhaseWord, now.ClosedWhy, now.FellBackCode));
    }

    /// <summary>
    /// 🔴 A live run's move past <c>old-ended</c> whose phase has stood ten minutes is said <c>move.quiet</c> once on its record's line by the
    /// conversations' look, on a moved clock, and never taken over; the look after says nothing again.
    /// </summary>
    [Fact]
    public async Task The_conversations_look_says_a_live_runs_quiet_move_once_past_ten_minutes()
    {
        var ledger = Records("c3");
        using var client = ledger.Client();
        var clock = new MovedClock();
        var live = Store("run-live", clock);
        var decided = live.Decide(Decision("c3"));
        live.Advance(decided.Claim!, MoveRecordPhase.OldEnded);
        live.Opening(decided.Claim!, "chat-move/live");
        var moves = new ConversationMoves(_home, client, Events, store: Store("run-shell", clock), clock: clock);
        int Quiet() => Events.After("c3", 0).Events.Count(e => e.Parts?.Any(part => part.Code == NoteCodes.MoveQuiet.Code) == true);

        var early = await moves.LookAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(11));
        var late = await moves.LookAsync(CancellationToken.None);
        var after = await moves.LookAsync(CancellationToken.None);

        Assert.Empty(early!.Quiet);
        Assert.Equal([decided.Move.Id], late!.Quiet.Select(move => move.Id));
        Assert.Empty(after!.Quiet);
        Assert.Equal(1, Quiet());
        Assert.Equal(("opening", "run-live"), (Store("run-probe").Read(decided.Move.Id)!.PhaseWord, Store("run-probe").Read(decided.Move.Id)!.ClaimRun));
    }

    /// <summary>
    /// 🔴 The shell's come-up (<see cref="ChatRunner.TakeUpAsync"/>, which the shell calls once as its loop comes up) takes a dead shell's
    /// conversation move up under this process's own run, its run told dead by the lease's own test: alive under another start time.
    /// </summary>
    [Fact]
    public async Task The_shells_come_up_takes_a_dead_shells_conversation_move_up_under_its_own_run()
    {
        var ledger = Records("c1");
        using var client = ledger.Client();
        var dead = new MoveStore(_home, () => StoreSettled.Yes, run: "run-gone", kind: StoreProcess.Shell);
        var decided = dead.Decide(Decision("c1"));
        using (var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            store.Write("probe", write => write.Execute("UPDATE store_runs SET started = 1 WHERE run = 'run-gone'"));
        }

        using var runner = new ChatRunner(
            client, AdapterSet.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), new SessionOutput(),
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        await runner.TakeUpAsync();

        var now = Store("run-probe").Read(decided.Move.Id)!;
        Assert.Equal(("fell-back", StoreProcess.Run, ConversationMoves.NotOpened), (now.PhaseWord, now.ClaimRun, now.FellBackCode));
    }
}
