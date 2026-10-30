using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iii part a (D212; the foundation design §3.3, §3.4 rule 5, §13.4, §10's row): the curve's move keeps its move record in the
/// store, as the live limit move does since f-ii, and writes no hand-over box beside it. Decided as the move begins, under this run's claim;
/// <c>old-ended</c> once the old part ends; <c>opening</c> under the key its keyed open is sent with; <c>opened</c>; <c>delivering</c> and the
/// prompt's mark before the hand; the prompt's outcome as the turn's write returns (the curve's message carries no word id, so no word's
/// <c>sent</c>), then the take, then <c>delivered</c>. A move whose hand cannot be made is ended by its phase; a record a standing move holds
/// takes no move; a dead shell's curve move is taken up by the conversations' come-up and sends nothing.
/// </summary>
/// <remarks>
/// As <c>LimitMoveStoreTests</c>: the in-process ledger, the real turns over a door that writes nothing, and a scratch home of each case's own
/// under <c>_fixtures/</c>; the shell's own box files are listed to prove none is written.
/// </remarks>
public sealed class CurveMoveStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 10, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "curve-move-store", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    public CurveMoveStoreTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Command, "");
        foreach (var account in new[] { "account-1", "account-2", "account-3" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "talk", account));
        }

        new HarnessSettings().WithRotation("talk", ["account-1", "account-2", "account-3"]).Save(Settings);
        AccountWindows.Said(_home, "talk", "account-1", Windows(0.75), Now.AddSeconds(-30), session: null, source: AccountWindows.SourceRead);
        AccountWindows.Said(_home, "talk", "account-2", Windows(0.30), Now.AddSeconds(-30), session: null, source: AccountWindows.SourceRead);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "talk-agent");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private static IReadOnlyList<WindowReading> Windows(double weekly) =>
        [new WindowReading("session", 0.10, Now.AddHours(1)), new WindowReading("weekly", weekly, Now.AddDays(3.5))];

    /// <summary>A move store of one run, dead where the case says so and alive otherwise.</summary>
    private MoveStore Store(string run) =>
        new(_home, () => StoreSettled.Yes, each => !_dead.Contains(each.Run), run, StoreProcess.Shell);

    private MoveRecord? MoveFrom(string left) => Store("run-probe").Of(left).LastOrDefault();

    private static MoveDecision Decision(string left) =>
        new(left, MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Curve, HandOverBy.Daoris);

    /// <summary>The conversation's agent on the protocol door with accounts and no usage question, so nothing is probed or spawned.</summary>
    private sealed class Talk(HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");
    }

    /// <summary>A conversation's turns over a door that writes nothing: <paramref name="wire"/> is told each message as its write is made.</summary>
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
        /// <summary>Where the start is refused: no record is opened.</summary>
        public bool Refuses { get; set; }

        /// <summary>Where the new part has no turns to hand a message to: its record opens and its door takes nothing.</summary>
        public bool Silent { get; set; }

        public List<MoveWords?> Asked { get; } = [];

        public Turned? Opened { get; private set; }

        public async Task<ChatStart> StartAsync(
            string repository, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, string? profile = null,
            bool? ownTree = null, CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null,
            IReadOnlyList<ActorEffective>? order = null, EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false,
            bool ownFirst = false, MoveWords? moves = null)
        {
            Asked.Add(moves);
            if (Refuses) return new ChatStart(null, "the agent would not start");
            if (moves is null)
            {
                // The hand-over's own open, unkeyed: no move record stands behind it.
                var plain = await client.OpenChatAsync(repository, adapter, null, profile, tree, null, ct, from);
                if (plain.SessionId is { } plainId && !Silent) Opened = Talking(context, plainId, wire);
                return new ChatStart(plain.SessionId, plain.Message);
            }

            var opened = await client.OpenChatMovingAsync(repository, adapter, null, profile, tree, null, from, moves, "default", ct);
            if (opened.SessionId is { } id && !Silent) Opened = Talking(context, id, wire);
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

    private sealed record World(ChatContext Context, Opening Runner, CurveMove Curve, ChatLedger Ledger, ServiceClient Client, Turned Old)
    {
        /// <summary>The old part's end as its watch writes it, which no process here makes: its record ends once the new part's open is asked (D51).</summary>
        public void Ends(HttpRequestMessage request)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat") Ledger.Edit("c1", record => record["state"] = "stopped");
        }
    }

    /// <summary>
    /// A live conversation <c>c1</c> on account-1 whose curve says to move to account-2; the curve's move keeps its record under this shell's
    /// run, <c>run-shell</c>.
    /// </summary>
    private World Live(int opens, Action<ChatMessage>? wire = null, Action<Opening>? shape = null)
    {
        var ledger = new ChatLedger { Opens = opens }.Register("engine", _root);
        var client = ledger.Client();
        ledger.Chat("c1", "working", "talk", _root, profile: "account-1");
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["talk"] = new Talk(new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "TALK_HOME", ProbeByPresence: true,
                Usage: new UsageQuestion("talk", ["talk-agent", "usage"], [], UsageKind.Claude))),
        });
        var events = Events;
        var roster = new HarnessRoster(adapters, Settings) { Clock = () => Now };
        var context = new ChatContext(client, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), output: null, roster, events);
        var old = Talking(context, "c1");
        var runner = new Opening(client, context, wire);
        shape?.Invoke(runner);
        var curve = new CurveMove(context, new ChatHandOver(context, runner))
        {
            Moves = new ConversationMoves(_home, client, events, store: Store("run-shell")),
        };
        var world = new World(context, runner, curve, ledger, client, old);
        ledger.BeforeRequest = (request, _) =>
        {
            world.Ends(request);
            return Task.CompletedTask;
        };
        return world;
    }

    private static DriverConfig Config() => DriverConfig.Empty;

    /// <summary>The shell's own hand-over box files, none of which a move the store carries may leave.</summary>
    private string[] Boxes() => [.. new HandOverBox(_home).Pending()];

    /// <summary>
    /// 🔴 One curve move, written in the design's order (§13.4): decided under this run's claim as the move begins, then <c>opening</c> under the
    /// very key its keyed open is sent with, then <c>opened</c>, then <c>delivering</c> with the prompt marked before its write; the prompt's
    /// outcome as the write returns and before any take, and <c>delivered</c> after it. A message with no word id writes no word's <c>sent</c>;
    /// and no box is written at any point.
    /// </summary>
    [Fact]
    public async Task The_curve_move_is_decided_opening_opened_marked_sent_and_delivered_in_that_order_and_writes_no_box()
    {
        var seen = new List<string>();
        MoveRecord? atWire = null;
        string[] boxesAtWire = [];
        var world = Live(opens: 2, wire: _ =>
        {
            atWire = MoveFrom("c1");
            boxesAtWire = Boxes();
            lock (seen) seen.Add("wire");
        });
        using var c = world.Client;
        MoveRecord? atOpen = null;
        string[] boxesAtOpen = [];
        string? openedUnder = null;
        world.Ledger.BeforeRequest = (request, body) =>
        {
            world.Ends(request);
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat")
            {
                atOpen = MoveFrom("c1");
                boxesAtOpen = Boxes();
                openedUnder = JsonNode.Parse(body!)!["opens"]?.GetValue<string>();
                lock (seen) seen.Add("open");
            }

            return Task.CompletedTask;
        };

        var choice = await world.Curve.MoveAsync("c1", Config(), null);

        var move = Assert.IsType<SwitchChoice.MoveTo>(choice);
        Assert.Equal("account-2", move.Account);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}, seen {string.Join(",", seen)}");
        Assert.Equal(["open", "wire"], seen);

        // Decided, ended and asked under its key before the open, which is sent that key.
        Assert.NotNull(atOpen);
        Assert.Equal(("opening", "run-shell", "curve", "daoris", "conversation", "brief"), (atOpen!.PhaseWord, atOpen.ClaimRun, atOpen.Why, atOpen.By, atOpen.Executor, atOpen.How));
        Assert.True(atOpen.Moments.ContainsKey(MoveRecordPhase.Decided) && atOpen.Moments.ContainsKey(MoveRecordPhase.OldEnded));
        Assert.StartsWith("chat-move/", atOpen.Opens, StringComparison.Ordinal);
        Assert.Equal(atOpen.Opens, openedUnder);
        Assert.Equal(atOpen.Opens, Assert.Single(world.Ledger.Opened).Key);

        // Opened on the new part, and the prompt marked before its write.
        Assert.Equal(("delivering", "c2"), (atWire!.PhaseWord, atWire.Destination));
        Assert.Equal((MoveEffects.Prompt, (string?)null), (atWire.Effect!.What, atWire.Effect.Outcome));
        Assert.Empty(Store("run-probe").WordsOf(atWire.Id));

        // The prompt's outcome as the write returns; no word to have sent; the hand reached the new part once.
        var delivered = MoveFrom("c1")!;
        Assert.Equal((MoveWordDispositions.Sent, "c2", "account-2"), (delivered.Effect!.Outcome, delivered.Destination, delivered.Account));
        Assert.Empty(Store("run-probe").WordsOf(delivered.Id));
        Assert.Equal(1, world.Runner.Opened!.Prompts.Count);

        // No box was ever kept beside the record.
        Assert.Empty(boxesAtOpen);
        Assert.Empty(boxesAtWire);
        Assert.Empty(Boxes());
        Assert.Null(new HandOverBox(_home).Read("c1"));
    }

    /// <summary>
    /// 🔴 The open asked is keyed with nothing named and nothing read (<c>MovedWords.Asked(id, [], [])</c>), as the limit move asks it where no
    /// word waits: a host that moves nothing, its mark below 2, still has the move decided and closed <c>delivered</c> on its effect's outcome.
    /// </summary>
    [Fact]
    public async Task A_host_that_moves_no_word_still_has_the_move_decided_and_delivered()
    {
        var world = Live(opens: 1);
        using var c = world.Client;

        var choice = await world.Curve.MoveAsync("c1", Config(), null);

        Assert.IsType<SwitchChoice.MoveTo>(choice);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        var asked = Assert.Single(world.Runner.Asked);
        Assert.Equal(("c1", true), (asked!.From, asked.Opens is { Length: > 0 }));
        Assert.Empty(asked.Words);
        Assert.Empty(asked.Read);
        Assert.Empty(Boxes());
    }

    /// <summary>
    /// 🔴 The new part's record continues the old one by the curve, asked by Daoris and left on the account the decision named, as the box's take
    /// made it, so what a person reads of the move does not change with where its record is kept.
    /// </summary>
    [Fact]
    public async Task The_new_part_continues_the_old_by_the_curve_on_the_account_named()
    {
        var world = Live(opens: 2);
        using var c = world.Client;

        await world.Curve.MoveAsync("c1", Config(), null);

        var moved = (await world.Client.RecordAsync("c2"))!;
        Assert.Equal(("talk", "account-2", _root), (moved.Adapter, moved.Profile, moved.Tree));
        Assert.Equal(("c1", "curve", "daoris"), (moved.From, moved.FromWhy, moved.FromBy));
    }

    /// <summary>
    /// 🔴 A hand that cannot be made (the new part has no turns to take the message) ends the move by its phase: its prompt known unwritten, the
    /// hold undelivered named and resolved, so the move closes rather than standing for a prompt nobody wrote.
    /// </summary>
    [Fact]
    public async Task A_hand_that_cannot_be_made_ends_the_move_by_its_phase_with_its_prompt_unsent()
    {
        var world = Live(opens: 2, shape: runner => runner.Silent = true);
        using var c = world.Client;

        await world.Curve.MoveAsync("c1", Config(), null);

        var move = MoveFrom("c1")!;
        Assert.Equal(("closed", "c2", ConversationMoves.Unsent), (move.PhaseWord, move.Destination, move.Effect!.Outcome));
        Assert.Equal((MoveHoldWhy.Undelivered, MoveCloseWhy.Resolved), (move.HeldWhy, move.ClosedWhy));
        Assert.Empty(Boxes());
    }

    /// <summary>
    /// 🔴 An open the host refuses ends the move by its phase, its old part already ended: the open asked and no answer recorded is held as an
    /// effect never recorded and closed, naming nothing that waits; nothing is said to a part that does not exist.
    /// </summary>
    [Fact]
    public async Task A_refused_open_ends_the_move_by_its_phase_and_hands_nothing()
    {
        var world = Live(opens: 2, shape: runner => runner.Refuses = true);
        using var c = world.Client;

        var choice = await world.Curve.MoveAsync("c1", Config(), null);

        Assert.IsType<SwitchChoice.MoveTo>(choice);
        var move = MoveFrom("c1")!;
        Assert.Equal(("closed", MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved), (move.PhaseWord, move.HeldWhy, move.ClosedWhy));
        Assert.Null(world.Runner.Opened);
        Assert.Empty(Boxes());
    }

    /// <summary>
    /// 🔴 A record that is the destination of another run's standing move takes no curve move (§3.1's <c>Never</c>, §3.3): nothing is written for
    /// it, no open is asked and its turns are left as they were.
    /// </summary>
    [Fact]
    public async Task A_standing_move_on_the_record_holds_the_curve_with_no_move()
    {
        var world = Live(opens: 2);
        using var c = world.Client;
        var other = Store("run-other");
        var into = other.Decide(new MoveDecision("c0", MoveExecutors.Conversation, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Daoris) { FromEnded = true });
        other.Opening(into.Claim!, "chat-move/other");
        other.Opened(into.Claim!, "c1", []);

        var choice = await world.Curve.MoveAsync("c1", Config(), null);

        Assert.IsType<SwitchChoice.MoveTo>(choice);
        Assert.Empty(Store("run-probe").Of("c1"));
        Assert.Empty(world.Ledger.Opened);
        Assert.Null(world.Runner.Opened);
        Assert.True(world.Old.Turns.Hold(), "the move left the old part's turns held");
        world.Old.Turns.Release();
        Assert.Equal("opened", Store("run-probe").Read(into.Move.Id)!.PhaseWord);
        Assert.Empty(Boxes());
    }

    /// <summary>
    /// 🔴 A box an older build left for the conversation is still the hand-over's own: the curve judges nothing and decides nothing, and the box
    /// stands for its own take.
    /// </summary>
    [Fact]
    public async Task A_box_an_older_build_left_is_taken_as_before_and_no_move_is_decided()
    {
        var world = Live(opens: 2);
        using var c = world.Client;
        var asked = new HandOverRequest("talk", "account-2", HandOverWhy.Curve, HandOverBy.Daoris, Now);
        new HandOverBox(_home).Write("c1", asked);

        Assert.Null(await world.Curve.MoveAsync("c1", Config(), null));

        Assert.Empty(Store("run-probe").Of("c1"));
        Assert.Empty(world.Ledger.Opened);
        Assert.Equal(asked, new HandOverBox(_home).Read("c1"));
    }

    /// <summary>
    /// <summary>
    /// 🔴 A box an older build left is no longer taken as a file (FOUNDATION1f-v part a): the hand-over's take reads the store, so with the box
    /// not yet imported nothing waits to be taken and the box stays for the come-up's import.
    /// </summary>
    [Fact]
    public async Task The_hand_over_s_take_reads_the_store_so_a_box_not_yet_imported_is_not_taken()
    {
        var world = Live(opens: 2);
        using var c = world.Client;
        var asked = new HandOverRequest("talk", "account-2", HandOverWhy.Curve, HandOverBy.Daoris, Now);
        new HandOverBox(_home).Write("c1", asked);
        var handOver = new ChatHandOver(world.Context, world.Runner);

        var taken = await handOver.TakeAsync("c1", Config(), null);

        Assert.Equal((null, false), (taken.SessionId, taken.Kept));
        Assert.Empty(Store("run-probe").Of("c1"));
        Assert.Empty(world.Runner.Asked);
        Assert.Equal(asked, new HandOverBox(_home).Read("c1"));
    }

    // ——— The conversations' take-up (§3.3, §13.4, §19.1).

    /// <summary>
    /// 🔴 A dead shell's curve move is taken up once by the shell's come-up (<see cref="ChatRunner.TakeUpAsync"/>, over the home's conversation
    /// moves, which the limit move shares) and ended by its phase, and it sends nothing: before the open asked it falls back on the old part;
    /// with its open asked and never answered it is held as an effect never recorded and closes.
    /// </summary>
    [Theory]
    [InlineData("decided", "fell-back")]
    [InlineData("opening", "closed")]
    public async Task The_shells_come_up_takes_a_dead_shells_curve_move_up_and_ends_it_by_its_phase_sending_nothing(string phase, string ended)
    {
        var ledger = new ChatLedger { Opens = 2 };
        ledger.Chat("c1", "stopped", "talk", "engine");
        using var client = ledger.Client();
        var dead = new MoveStore(_home, () => StoreSettled.Yes, run: "run-gone", kind: StoreProcess.Shell);
        var decided = dead.Decide(Decision("c1"));
        if (phase == "opening")
        {
            dead.Advance(decided.Claim!, MoveRecordPhase.OldEnded);
            dead.Opening(decided.Claim!, "chat-move/dead");
        }

        using (var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            store.Write("probe", write => write.Execute("UPDATE store_runs SET started = 1 WHERE run = 'run-gone'"));
        }

        using var runner = new ChatRunner(
            client, AdapterSet.Built(), _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), new SessionOutput(),
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        await runner.TakeUpAsync();

        var now = Store("run-probe").Read(decided.Move.Id)!;
        Assert.Equal((ended, StoreProcess.Run), (now.PhaseWord, now.ClaimRun));
        if (ended == "fell-back") Assert.Equal(ConversationMoves.NotOpened, now.FellBackCode);
        else Assert.Equal((MoveHoldWhy.Unrecorded, MoveCloseWhy.Resolved), (now.HeldWhy, now.ClosedWhy));
        Assert.Equal("curve", now.Why);
        Assert.Empty(ledger.Opened);
        Assert.Empty(ledger.TakenBy);
        Assert.Empty(ledger.Said("c1"));
        Assert.Empty(Boxes());
    }
}
