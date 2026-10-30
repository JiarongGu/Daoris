using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The live limit move, and the curve's (FOUNDATION1f-iii part b), across a killed shell (FOUNDATION1f-ii part b, D212; the foundation design
/// §13.5's downgrade table, §19.3, §19.4): one
/// home, one in-process ledger, and two kinds of shell over them. The <b>killed shell</b> runs the live move (<see cref="LimitMove.MoveAsync"/>)
/// under a run of its own, its new part's door a stand-in that writes nothing but tells the case each write, and is stopped at a boundary of
/// <see cref="Cuts"/> by blocking there for good, as a killed process never leaves it; its run is then declared dead the way the lease tells a
/// dead process, a start time no process has, and the records it left are what a restarted service leaves of a dead shell's: ended. The
/// <b>coming-up shell</b> is the real chat runner over the real protocol stub, a stand-in agent that writes down each prompt, so a word a
/// destination's go-on carries is heard once or not at all. An <b>older build's</b> come-up is the same runner whose move holder reads every run
/// as alive and so takes nothing up: the older build's statements are the go-on's, which this build did not change.
/// </summary>
/// <remarks>
/// Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment. One OS process is one run to
/// the lease's test, so the killed shell's run is a name of its own, "run-a", and the coming-up shells' the process's.
/// </remarks>
internal sealed class LiveLimitHarness : IAsyncDisposable
{
    /// <summary>The person's word kept on the old part before the move's open, which the open moves.</summary>
    public const string Word = "And add a test.";

    /// <summary>The word the limit refused.</summary>
    public const string Refused = "Make the port configurable.";

    /// <summary>The killed shell's run.</summary>
    public const string Killed = "run-a";

    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    private readonly List<IDisposable> _owned = [];

    private readonly ManualResetEventSlim _gate = new(false);

    private Task<object?>? _moving;

    public LiveLimitHarness(string name)
    {
        Home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", name, Guid.NewGuid().ToString("N")[..8]);
        Root = Path.Combine(Home, "engine");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Gate);
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "stub", account));
            Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "talk", account));
        }

        File.WriteAllText(TalkCommand, "");
        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).WithRotation("talk", ["account-1", "account-2"])
            .Save(DaorisHome.Entry(Home, HomeEntry.Harnesses));
        Ledger = new ChatLedger { Opens = 2 }.Register("engine", Root);
        Client = Ledger.Client();
    }

    public string Home { get; }

    public string Root { get; }

    public ChatLedger Ledger { get; }

    public ServiceClient Client { get; }

    private string Gate => Path.Combine(Home, "gate");

    public string HeardFile => Path.Combine(Home, "heard.txt");

    public SessionEvents Events => new(DaorisHome.Entry(Home, HomeEntry.Sessions));

    /// <summary>A move store of one run, alive by the lease's own test unless the case says otherwise.</summary>
    public MoveStore Store(string run, Func<StoreRun, bool>? alive = null) =>
        alive is null ? new(Home, () => StoreSettled.Yes, run: run, kind: StoreProcess.Shell) : new(Home, () => StoreSettled.Yes, alive, run, StoreProcess.Shell);

    /// <summary>The move of a record left, as a reader of the store sees it.</summary>
    public MoveRecord? MoveFrom(string left) => Store("run-probe").Of(left).LastOrDefault();

    /// <summary>The run declared dead as the lease declares one: alive under another start time.</summary>
    public void Die(string run)
    {
        using var store = DesktopStore.Open(DesktopStore.LocationIn(Home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        store.Write("probe", write => write.Execute("UPDATE store_runs SET started = 1 WHERE run = $run", ("$run", run)));
    }

    public DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), HeardFile, Gate],
                ["stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        return config;
    }

    // ——— The killed shell.

    /// <summary>The boundary the killed shell reached, or null while it runs on.</summary>
    public string? Reached { get; private set; }

    private sealed class ShellKilled : Exception;

    /// <summary>
    /// A live conversation <c>c1</c> on account-1, its last turn refused by its limit, the person's word kept on it; the live move run by a shell
    /// of run <see cref="Killed"/> that is stopped for good at <paramref name="cut"/>. Returns once it is there.
    /// </summary>
    /// <param name="atTheWrite">Told the new part's message as the door writes it, before the write returns.</param>
    /// <param name="cut">The boundary to stop at; none runs the move to its end.</param>
    public async Task<ShellRun> RunShellAsync(string? cut, Action<ChatMessage>? atTheWrite = null, Action<string>? atTheCut = null)
    {
        var events = Events;
        Limited(events, "working", null);

        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var context = new ChatContext(
            Client, Home, new SessionProcesses(DaorisHome.Entry(Home, HomeEntry.Sessions)), output: null,
            new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)), events);
        var old = Talking(context, "c1", shows: false);
        var runner = new Opening(Client, context, atTheWrite);
        var limit = new LimitMove(context, runner, new ChatHandOver(context, runner), new NoHelp())
        {
            Moves = new ConversationMoves(Home, Client, events, store: Store(Killed)),
        };
        var hit = Hook(limit.Moves, cut, atTheCut);
        _moving = Task.Run(async () => (object?)await limit.MoveAsync("c1", ActorChoices.With(DriverConfig.Empty, "chat", [new ActorChoice("talk")]), null));
        await ReachedAsync(hit, cut);
        return new ShellRun(runner, old) { Limit = limit };
    }

    /// <summary>
    /// The curve's twin of <see cref="RunShellAsync"/> (FOUNDATION1f-iii part b): a live conversation <c>c1</c> on account-1, past its curve and
    /// its last turn ended, the person's word kept on it; the curve's move (<see cref="CurveMove.MoveAsync"/>) run by a shell of run
    /// <see cref="Killed"/>, stopped for good at <paramref name="cut"/>. Its brief is the one message the move writes; the word the open moves
    /// waits on the new part for its own go-on, and no write of the move's carries it.
    /// </summary>
    /// <param name="atTheWrite">Told the new part's message as the door writes it, before the write returns.</param>
    /// <param name="cut">The boundary to stop at; none runs the move to its end.</param>
    /// <param name="atTheCut">Told each boundary as it is passed, after the old part's end is written.</param>
    /// <param name="keep">Words the person kept on the old part, by key; the one default is <see cref="Word"/> under <c>k1</c>.</param>
    public async Task<ShellRun> RunCurveShellAsync(
        string? cut, Action<ChatMessage>? atTheWrite = null, Action<string>? atTheCut = null, IReadOnlyDictionary<string, string>? keep = null)
    {
        foreach (var (account, weekly) in new[] { ("account-1", 0.75), ("account-2", 0.30) })
        {
            AccountWindows.Said(
                Home, "talk", account, [new WindowReading("session", 0.10, CurveNow.AddHours(1)), new WindowReading("weekly", weekly, CurveNow.AddDays(3.5))],
                CurveNow.AddSeconds(-30), session: null, source: AccountWindows.SourceRead);
        }

        Ledger.Chat("c1", "working", "talk", Root, profile: "account-1");
        var events = Events;
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Earlier answered exchange." });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        foreach (var (key, word) in keep ?? new Dictionary<string, string> { ["k1"] = Word }) Ledger.For("c1").Say(key, word);

        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["talk"] = new CurveTalk(new HarnessToolchain(
                Binary: [TalkCommand], VersionArguments: ["--version"], ProfileVariable: "TALK_HOME", ProbeByPresence: true,
                Usage: new UsageQuestion("talk", ["talk-agent", "usage"], [], UsageKind.Claude))),
        });
        var context = new ChatContext(
            Client, Home, new SessionProcesses(DaorisHome.Entry(Home, HomeEntry.Sessions)), output: null,
            new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)) { Clock = () => CurveNow }, events);
        var old = Talking(context, "c1", shows: false);
        var runner = new Opening(Client, context, atTheWrite);
        var curve = new CurveMove(context, new ChatHandOver(context, runner))
        {
            Moves = new ConversationMoves(Home, Client, events, store: Store(Killed)),
        };
        var hit = Hook(curve.Moves, cut, atTheCut);
        _moving = Task.Run(async () => (object?)await curve.MoveAsync("c1", DriverConfig.Empty, null));
        await ReachedAsync(hit, cut);
        return new ShellRun(runner, old) { Curve = curve };
    }

    /// <summary>
    /// The ended move's twin of <see cref="RunShellAsync"/> (FOUNDATION1f-vi part b): an ended conversation <c>c1</c> on account-1, nothing of it
    /// running, its last turn refused by its limit and its account cooling, the person's word waiting on it; the ended move
    /// (<see cref="LimitMove.MoveEndedAsync"/>) run by a shell of run <see cref="Killed"/>, stopped for good at <paramref name="cut"/>. It passes
    /// eight of <see cref="Cuts.All"/>, never <see cref="Cuts.OldEnded"/>, an ended record having no part to end, and at <see cref="Cuts.Decided"/>
    /// the move already stands at <c>old-ended</c>. It writes the refused turn's word and the moved one in its one message, as the live move does.
    /// </summary>
    /// <param name="atTheWrite">Told the new part's message as the door writes it, before the write returns.</param>
    /// <param name="cut">The boundary to stop at; none runs the move to its end.</param>
    /// <param name="atTheCut">Told each boundary as it is passed.</param>
    /// <param name="keep">Words the person kept on the record, by key; the one default is <see cref="Word"/> under <c>k1</c>.</param>
    public async Task<ShellRun> RunEndedShellAsync(
        string? cut, Action<ChatMessage>? atTheWrite = null, Action<string>? atTheCut = null, IReadOnlyDictionary<string, string>? keep = null)
    {
        var events = Events;
        Limited(events, "stopped", keep);

        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        var context = new ChatContext(
            Client, Home, new SessionProcesses(DaorisHome.Entry(Home, HomeEntry.Sessions)), output: null,
            new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)), events);
        var runner = new Opening(Client, context, atTheWrite);
        var limit = new LimitMove(context, runner, new ChatHandOver(context, runner), new NoHelp())
        {
            Moves = new ConversationMoves(Home, Client, events, store: Store(Killed)),
        };
        var hit = Hook(limit.Moves, cut, atTheCut);
        _moving = Task.Run(async () => (object?)await limit.MoveEndedAsync("c1", lost: null, ActorChoices.With(DriverConfig.Empty, "chat", [new ActorChoice("talk")]), null));
        await ReachedAsync(hit, cut);
        return new ShellRun(runner, null) { Limit = limit };
    }

    /// <summary>A conversation <c>c1</c> on account-1 in <paramref name="state"/>, its last turn refused by its limit, the words kept on it waiting.</summary>
    private void Limited(SessionEvents events, string state, IReadOnlyDictionary<string, string>? keep)
    {
        Ledger.Chat("c1", state, "talk", Root, profile: "account-1");
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Earlier answered exchange." });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = Refused });
        events.Append("c1", new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = "its account is cooling", Parts = [new NotePart { Code = NoteCodes.AccountCoolingWindow.Code }],
        });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        foreach (var (key, word) in keep ?? new Dictionary<string, string> { ["k1"] = Word }) Ledger.For("c1").Say(key, word);
    }

    /// <summary>When the curve is judged: ten minutes after the ledger's records are made, as the curve's own cases hold it.</summary>
    private static readonly DateTimeOffset CurveNow = new(2026, 10, 3, 8, 10, 0, TimeSpan.Zero);

    private string TalkCommand => Path.Combine(Home, "talk-agent");

    /// <summary>The boundary hook: the old part's end as its watch writes it, which no process here makes, and the stop at the cut's place.</summary>
    private TaskCompletionSource Hook(ConversationMoves moves, string? cut, Action<string>? atTheCut)
    {
        var hit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        moves.Cut = name =>
        {
            // The old part's end as its watch writes it, which no process here makes: its record ends once the move has ended it.
            if (name == Cuts.OldEnded) Ledger.Edit("c1", record => record["state"] = "stopped");
            atTheCut?.Invoke(name);
            if (name != cut) return;
            Reached = name;
            hit.TrySetResult();
            _gate.Wait();
            throw new ShellKilled();
        };
        return hit;
    }

    private async Task ReachedAsync(TaskCompletionSource hit, string? cut)
    {
        if (cut is not null)
        {
            // A boundary of the door's write is reached on the new part's pump after the move's own task has returned.
            await Poll.Until(() => hit.Task.IsCompleted || _moving!.IsFaulted, () => $"the shell reached {Reached ?? "nothing"}, its move {_moving!.Status}, {Seen()}");
            if (!hit.Task.IsCompleted) throw new InvalidOperationException($"the move faulted before `{cut}`: {await Settled(_moving!)}");
        }
        else
        {
            await _moving!;
        }
    }

    private static async Task<string> Settled(Task<object?> task)
    {
        try
        {
            return await task switch
            {
                StartedFrom done => $"moved to {done.SessionId}, sent {done.Sent}",
                var other => $"judged {other}",
            };
        }
        catch (Exception error)
        {
            return error.GetType().Name + ": " + error.Message;
        }
    }

    /// <summary>
    /// The shell is killed: its run is dead by the lease's test, and the records it had left are what a restart of the service leaves of a dead
    /// shell's, ended, each on the protocol stub's adapter so the coming-up shell can go on in it. Nothing it blocked on is released.
    /// </summary>
    public void Kill()
    {
        Die(Killed);
        for (var at = 1; at <= Ledger.Count; at++)
        {
            Ledger.Edit($"c{at}", record =>
            {
                record["state"] = "stopped";
                record["adapter"] = "acp-stub";
            });
        }
    }

    /// <summary>The killed shell is let go: whatever it blocked on throws, as a process that died never would; its move's task is awaited and its error said.</summary>
    public async Task<string> ReleaseAsync()
    {
        _gate.Set();
        return _moving is null ? "no move" : await Settled(_moving);
    }

    /// <summary>The killed shell's door, for the case to read what it wrote.</summary>
    public sealed record ShellRun(Opening Runner, Turned? Old)
    {
        /// <summary>The live limit move or the ended one the shell ran, where it ran one (the ended one has no live part, so no <see cref="Old"/>).</summary>
        public LimitMove? Limit { get; init; }

        /// <summary>The curve's move the shell ran, where it ran one (FOUNDATION1f-iii part b).</summary>
        public CurveMove? Curve { get; init; }
    }

    // ——— The coming-up shell and the stub.

    /// <summary>The real chat runner over the real protocol stub, its move holder the home's own, or an older build's.</summary>
    public ChatRunner Runner(bool older = false)
    {
        var client = Ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)) { Clock = () => _now };
        var runner = new ChatRunner(client, adapters, Home, new SessionProcesses(DaorisHome.Entry(Home, HomeEntry.Sessions)), events: Events, harnesses: roster);
        if (older)
        {
            // A build before the store knows no move: its come-up takes none up, whoever's run it reads.
            runner.Moves = new ConversationMoves(Home, client, Events, store: Store("run-older", alive: _ => true));
        }

        _owned.Add(runner);
        _owned.Add(client);
        return runner;
    }

    /// <summary>The prompts the stub wrote down, each as it heard it.</summary>
    public IReadOnlyList<string> Prompts => [.. StubFile.Lines(HeardFile).Where(line => line.StartsWith("prompt[", StringComparison.Ordinal))];

    /// <summary>How many times the stub heard <paramref name="text"/>.</summary>
    public int Heard(string text) => Prompts.Sum(prompt => Count(prompt, text));

    /// <summary>How many prompts the killed shell's new part's door wrote that carried <paramref name="text"/>.</summary>
    public static int Wrote(ShellRun shell, string text) => shell.Runner.Opened?.Prompts.Count(prompt => prompt.Contains(text, StringComparison.Ordinal)) ?? 0;

    /// <summary>How many times the stub heard <paramref name="text"/> as the conversation on <paramref name="account"/> (the stub names each prompt by its account).</summary>
    public int HeardBy(string text, string account) =>
        Prompts.Where(prompt => prompt.StartsWith($"prompt[{account}]", StringComparison.Ordinal)).Sum(prompt => Count(prompt, text));

    /// <summary>How many messages the killed shell's new part's door wrote: the curve's brief, which is its one message.</summary>
    public int Briefed(ShellRun shell) => shell.Runner.Opened?.Prompts.Count ?? 0;

    public static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>Every word still waiting, on any record, as <c>record:word</c>.</summary>
    public IReadOnlyList<string> Waiting() =>
        [.. Enumerable.Range(1, Ledger.Count).SelectMany(at => Ledger.Said($"c{at}").Select(word => $"c{at}:{word}"))];

    public string Seen() =>
        $"heard [{string.Join(" | ", StubFile.Lines(HeardFile))}], records {Ledger.Count}, waiting [{string.Join(",", Waiting())}], "
        + $"opened [{string.Join(",", Ledger.Opened.Select(each => each.Key))}], moves [{string.Join(" | ", Store("run-probe").Of("c1").Select(move => $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}"))}]";

    public async ValueTask DisposeAsync()
    {
        if (_moving is not null) await ReleaseAsync();
        foreach (var owned in _owned) owned.Dispose();
        Client.Dispose();
        _gate.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(Home);
    }

    // ——— The killed shell's stand-ins, as part a's cases build them.

    /// <summary>A conversation's turns over a door that writes nothing, which records each moved word's event before the write as the real door does.</summary>
    internal sealed class Turned : ITurnedChat
    {
        public ChatTurns Turns { get; set; } = null!;

        IChatTurns ITurnedChat.Turns => Turns;

        public List<string> Prompts { get; } = [];

        public Task FinishAsync(Action endInput) => Task.CompletedTask;
    }

    private static Turned Talking(ChatContext context, string id, bool shows, Action<ChatMessage>? wire = null)
    {
        var chat = new Turned();
        chat.Turns = new ChatTurns(
            ready: () => Task.FromResult(true),
            take: (message, sent) =>
            {
                // The moved words' events are recorded under their ids before the write (FOUNDATION1d-ii part 3), so an older build's
                // come-up finds a moved word shown and writes no `lost` note for it.
                if (shows)
                {
                    foreach (var recorded in ChatRecorded.Recorded(message, context.TakeShown(id, message))) context.Record(id, recorded);
                }

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
    internal sealed class Opening(ServiceClient client, ChatContext context, Action<ChatMessage>? wire) : IChatRunning
    {
        public Turned? Opened { get; private set; }

        public async Task<ChatStart> StartAsync(
            string repository, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, string? profile = null,
            bool? ownTree = null, CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null,
            IReadOnlyList<ActorEffective>? order = null, EarlierMaker? leftBy = null, HarnessSelection? settled = null, bool picked = false,
            bool ownFirst = false, MoveWords? moves = null)
        {
            var opened = await client.OpenChatMovingAsync(repository, adapter, null, profile, tree, null, from, moves!, "default", ct);
            if (opened.SessionId is { } id) Opened = Talking(context, id, shows: true, wire);
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

    /// <summary>The killed shell's adapter on the protocol door with no toolchain to ask, so nothing is probed or spawned.</summary>
    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");
    }

    /// <summary>The curve's agent on the protocol door with accounts and a usage question, so the curve judges it and nothing is probed or spawned.</summary>
    private sealed class CurveTalk(HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) => throw new InvalidOperationException("nothing spawns");
    }

    // ——— The stub agent, as the limit's process cases run it.

    private string Agent()
    {
        var script = Path.Combine(Home, "acp-crash-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + account;
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                conversation = frame.params.sessionId;
                note(`resumed[${account}]: ${conversation}`);
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note(`prompt[${account}]: ` + text.replace(/\r?\n/g, ' '));
                await new Promise((resolve) => setTimeout(resolve, 50));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard it' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>The stub's command: it answers the sign-in question for the stub's accounts, as the protocol stand-in's owner.</summary>
    private string TextAgent()
    {
        var script = Path.Combine(Home, "text-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => console.log('heard: ' + line));
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
