using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-v part a (D212; the foundation design §3.3, §13.4, §13.5): a conversation's hand-over, the person's press and the agent's ask,
/// keeps its move record in the store and writes no <c>&lt;s&gt;.hand-over.json</c> beside it. The move is decided at the ask, under this run's
/// claim, and waits there for the conversation's turn to end; then <c>old-ended</c>, <c>opening</c> under the key its keyed open is sent with,
/// <c>opened</c>, <c>delivering</c> and the prompt's mark before the hand, and <c>delivered</c>. A standing move means no second ask; a dead run's
/// hand-over found decided is taken at the come-up, not fallen back; an older build's conversation box is imported once at the come-up and set
/// aside, and a quest session's box, which is the driver's look's, is left where it is.
/// </summary>
/// <remarks>As <c>CurveMoveStoreTests</c>: the in-process ledger, the real turns over a door that writes nothing, a scratch home of each case's own under <c>_fixtures/</c>.</remarks>
public sealed class HandOverMoveStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 10, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "hand-over-move-store", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    public HandOverMoveStoreTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Command, "");
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "talk", account));
        }

        new HarnessSettings().WithRotation("talk", ["account-1", "account-2"]).Save(Settings);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "talk-agent");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>A move store of one run, dead where the case says so and alive otherwise.</summary>
    private MoveStore Store(string run) =>
        new(_home, () => StoreSettled.Yes, each => !_dead.Contains(each.Run), run, StoreProcess.Shell);

    private MoveRecord? MoveFrom(string left) => Store("run-probe").Of(left).LastOrDefault();

    private string[] Boxes() => [.. new HandOverBox(_home).Pending()];

    /// <summary>The person's decision as the press writes it: to <c>talk</c> on <c>account-2</c>, by its why and its asker.</summary>
    private static MoveDecision Decision(string left, string why = HandOverWhy.Press, string by = HandOverBy.Person) =>
        new(left, MoveExecutors.Conversation, MoveHows.Brief, why, by) { Agent = "talk", Account = "account-2" };

    private sealed class Talk(HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");
    }

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
        /// <summary>Whether the old part's turn is running: the press waits for its end.</summary>
        public bool Busy { get; set; }

        public List<MoveWords?> Asked { get; } = [];

        public Turned? Opened { get; private set; }

        public async Task<ChatStart> StartAsync(
            string repository, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, string? profile = null,
            bool? ownTree = null, CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null,
            IReadOnlyList<ActorEffective>? order = null, EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false,
            bool ownFirst = false, MoveWords? moves = null)
        {
            Asked.Add(moves);
            if (moves is null)
            {
                var plain = await client.OpenChatAsync(repository, adapter, null, profile, tree, null, ct, from);
                if (plain.SessionId is { } plainId) Opened = Talking(context, plainId, wire);
                return new ChatStart(plain.SessionId, plain.Message);
            }

            var opened = await client.OpenChatMovingAsync(repository, adapter, null, profile, tree, null, from, moves, "default", ct);
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

        public bool Taking(string sessionId) => Busy;

        public ChatMessage WithProcess(string sessionId, ChatMessage message) => message;

        public Task<ConversationWalk> WalkAsync(
            string actor, IReadOnlyList<ActorEffective> order, DriverConfig config, string? workspace, string? profile, CancellationToken ct,
            (ActorEffective Entry, string? Account)? refused = null, bool counts = true, long except = 0) => throw new NotSupportedException();
    }

    private sealed record World(ChatContext Context, Opening Runner, ChatHandOver HandOver, ConversationMoves Moves, ChatLedger Ledger, ServiceClient Client, Turned Old)
    {
        /// <summary>The old part's end as its watch writes it, which no process here makes: its record ends once the new part's open is asked (D51).</summary>
        public void Ends(HttpRequestMessage request)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat") Ledger.Edit("c1", record => record["state"] = "stopped");
        }
    }

    /// <summary>A live conversation <c>c1</c> on account-1 of <c>talk</c>, a quest's session <c>q1s</c> beside it; the hand-over's moves are this shell's, <c>run-shell</c>.</summary>
    private World Live(int opens = 2, Action<ChatMessage>? wire = null)
    {
        var ledger = new ChatLedger { Opens = opens }.Register("engine", _root);
        var client = ledger.Client();
        // Its turns are this shell's own (a record in a live state that no turn here holds is another process's, which a press refuses).
        ledger.Chat("c1", "stopped", "talk", _root, profile: "account-1");
        ledger.Chat("q1s", "stopped", "talk", _root, kind: "driven", quest: "q1", profile: "account-1");
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
        var moves = new ConversationMoves(_home, client, events, store: Store("run-shell"));
        var handOver = new ChatHandOver(context, runner) { Moves = moves };
        var world = new World(context, runner, handOver, moves, ledger, client, old);
        ledger.BeforeRequest = (request, _) =>
        {
            world.Ends(request);
            return Task.CompletedTask;
        };
        return world;
    }

    private static DriverConfig Config() => DriverConfig.Empty;

    private Task<HandedOver> Press(World world) =>
        world.HandOver.HandOverAsync("c1", "talk", "account-2", HandOverWhy.Press, HandOverBy.Person, Config(), null, CancellationToken.None);

    /// <summary>
    /// 🔴 The person's press decides the move in the store as it is asked, then writes it in the design's order: <c>old-ended</c>, <c>opening</c>
    /// under the very key the keyed open is sent with, <c>opened</c>, <c>delivering</c> with the prompt marked before its write, and
    /// <c>delivered</c>; and at no point is a <c>&lt;s&gt;.hand-over.json</c> written.
    /// </summary>
    [Fact]
    public async Task The_press_decides_the_move_in_the_store_and_writes_it_in_order_with_no_box()
    {
        MoveRecord? atOpen = null;
        string[] boxesAtOpen = [];
        string? openedUnder = null;
        MoveRecord? atWire = null;
        var world = Live(wire: _ => atWire = MoveFrom("c1"));
        using var c = world.Client;
        world.Ledger.BeforeRequest = (request, body) =>
        {
            world.Ends(request);
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat")
            {
                atOpen = MoveFrom("c1");
                boxesAtOpen = Boxes();
                openedUnder = JsonNode.Parse(body!)!["opens"]?.GetValue<string>();
            }

            return Task.CompletedTask;
        };

        var handed = await Press(world);

        Assert.True(handed.Kept, handed.Message);
        Assert.Equal("c3", handed.SessionId);
        Assert.Equal(("opening", "run-shell", HandOverWhy.Press, HandOverBy.Person, "talk", "account-2"), (atOpen!.PhaseWord, atOpen.ClaimRun, atOpen.Why, atOpen.By, atOpen.Agent, atOpen.Account));
        Assert.True(atOpen.Moments.ContainsKey(MoveRecordPhase.Decided) && atOpen.Moments.ContainsKey(MoveRecordPhase.OldEnded));
        Assert.StartsWith("chat-move/", atOpen.Opens, StringComparison.Ordinal);
        Assert.Equal(atOpen.Opens, openedUnder);
        Assert.Equal(("delivering", "c3"), (atWire!.PhaseWord, atWire.Destination));
        Assert.Equal((MoveEffects.Prompt, (string?)null), (atWire.Effect!.What, atWire.Effect.Outcome));
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        Assert.Equal(1, world.Runner.Opened!.Prompts.Count);
        var asked = Assert.Single(world.Runner.Asked);
        Assert.Equal(("c1", true), (asked!.From, asked.Opens is { Length: > 0 }));
        Assert.Empty(boxesAtOpen);
        Assert.Empty(Boxes());
        Assert.Null(new HandOverBox(_home).Read("c1"));
    }

    /// <summary>
    /// 🔴 A conversation that is talking keeps the move decided, at <c>quiescing</c>, until its turn ends; a second ask meanwhile is refused as
    /// waiting, read from the store, and the turn's end takes the one move once.
    /// </summary>
    [Fact]
    public async Task A_standing_move_means_no_second_ask_and_the_turn_s_end_takes_the_one_move()
    {
        var world = Live();
        using var c = world.Client;
        world.Runner.Busy = true;

        var first = await Press(world);
        var again = await world.HandOver.HandOverAsync("c1", "talk", "account-2", HandOverWhy.Press, HandOverBy.Person, Config(), null, CancellationToken.None);

        Assert.Equal(((string?)null, true), (first.SessionId, first.Kept));
        Assert.Equal((false, HandOver.Waiting), (again.Kept, again.Why));
        Assert.Equal("c1 is already waiting to be handed over to talk.", again.Message);
        var waiting = Assert.Single(Store("run-probe").Of("c1"));
        Assert.Equal(("quiescing", "run-shell"), (waiting.PhaseWord, waiting.ClaimRun));
        Assert.Empty(world.Ledger.Opened);
        Assert.Empty(Boxes());

        world.Runner.Busy = false;
        world.HandOver.Due("c1", ChatQueue.Idle, Config(), null);

        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        Assert.Single(Store("run-probe").Of("c1"));
        Assert.Single(world.Ledger.Opened);
    }

    /// <summary>
    /// 🔴 A hand-over a dead run decided and never took is taken at the come-up, not fallen back: the sweep leaves it standing under the new
    /// run (§13.5's first row, "else it goes on from its phase"), and the hand-over's own take opens the new part once, briefed.
    /// </summary>
    [Fact]
    public async Task A_dead_run_s_decided_hand_over_is_taken_at_the_come_up()
    {
        var world = Live();
        using var c = world.Client;
        world.Ledger.Edit("c1", record => record["state"] = "stopped");
        _dead.Add("run-gone");
        var dead = new MoveStore(_home, () => StoreSettled.Yes, run: "run-gone", kind: StoreProcess.Shell);
        var decided = dead.Decide(Decision("c1"));
        using (var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            store.Write("probe", write => write.Execute("UPDATE store_runs SET started = 1 WHERE run = 'run-gone'"));
        }

        await world.Moves.ComeUpAsync(say: null, CancellationToken.None);
        var standing = Store("run-probe").Read(decided.Move.Id)!;
        Assert.Equal(("decided", "run-shell"), (standing.PhaseWord, standing.ClaimRun));

        var taken = await world.HandOver.TakeAllAsync((_, _) => Task.CompletedTask, CancellationToken.None);

        Assert.Equal("c3", Assert.Single(taken).SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        Assert.Equal(1, world.Runner.Opened!.Prompts.Count);
    }

    /// <summary>
    /// 🔴 An older build's conversation box is imported once at the come-up and set aside, and its move is then taken as the press's is; the
    /// box is never taken as a file.
    /// </summary>
    [Fact]
    public async Task An_older_build_s_conversation_box_is_imported_set_aside_and_taken()
    {
        var world = Live();
        using var c = world.Client;
        world.Ledger.Edit("c1", record => record["state"] = "stopped");
        var box = new HandOverBox(_home);
        box.Write("c1", new HandOverRequest("talk", "account-2", HandOverWhy.Agent, HandOverBy.Agent, Now));

        await world.Moves.ComeUpAsync(say: null, CancellationToken.None);

        Assert.Null(box.Read("c1"));
        Assert.True(File.Exists(box.PathOf("c1") + ".imported"));
        var imported = Assert.Single(Store("run-probe").Of("c1"));
        Assert.Equal(("decided", "run-shell", HandOverWhy.Agent, HandOverBy.Agent, "talk", "account-2"), (imported.PhaseWord, imported.ClaimRun, imported.Why, imported.By, imported.Agent, imported.Account));

        var taken = await world.HandOver.TakeAllAsync((_, _) => Task.CompletedTask, CancellationToken.None);

        Assert.Equal("c3", Assert.Single(taken).SessionId);
        await Poll.Until(() => MoveFrom("c1")?.PhaseWord == "delivered", () => $"the move at {MoveFrom("c1")?.PhaseWord}");
        Assert.Empty(Boxes());
    }

    /// <summary>🔴 A quest session's box is the driver's look's, and the conversations' come-up leaves it where it is, with no move for it.</summary>
    [Fact]
    public async Task A_quest_session_s_box_is_left_for_the_look()
    {
        var world = Live();
        using var c = world.Client;
        var box = new HandOverBox(_home);
        box.Write("q1s", new HandOverRequest("talk", null, HandOverWhy.Press, HandOverBy.Person, Now));

        await world.Moves.ComeUpAsync(say: null, CancellationToken.None);

        Assert.NotNull(box.Read("q1s"));
        Assert.Empty(Store("run-probe").Of("q1s"));
        Assert.False(File.Exists(box.PathOf("q1s") + ".imported"));
    }

    /// <summary>
    /// 🔴 A box still on disk keeps the go-on off its record: the conversation's hand-over is taken first, and no word waiting on the old part
    /// resumes it before the box is imported.
    /// </summary>
    [Fact]
    public async Task A_box_or_a_waiting_hand_over_holds_the_record_as_the_course_s_never()
    {
        var world = Live();
        using var c = world.Client;
        Assert.False(world.HandOver.Holds("c1"));

        new HandOverBox(_home).Write("c1", new HandOverRequest("talk", "account-2", HandOverWhy.Press, HandOverBy.Person, Now));
        Assert.True(world.HandOver.Holds("c1"));
        new HandOverBox(_home).Clear("c1");
        Assert.False(world.HandOver.Holds("c1"));

        world.Runner.Busy = true;
        await Press(world);
        Assert.True(world.HandOver.Holds("c1"));
    }
}
