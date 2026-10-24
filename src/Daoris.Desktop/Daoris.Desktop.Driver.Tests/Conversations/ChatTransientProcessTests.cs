using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTERRORS1b (D210, the agent-errors design §5.1, §7, §11's row) with real conversations: a transient failure of a turn of Ask Daoris or
/// a repository's chat, on the protocol door, is tried again in the SAME conversation, on the same process, inside the turn, on the retry
/// clock the test holds (<c>.claude/knowledge/held-clocks.md</c>). A turn that fails transient twice and then answers is one turn: the failed
/// tries release no queue, end no turn in the record, move no curve, notify nobody and move no limit. A word said during the wait goes at
/// once, first, in one prompt with Daoris's go-on. A limit on a try goes to LIMITGOON1/2 once, holding no word a try carried and not the
/// failed turn's own, each original word received once at the limit's wake or move. A conversation whose every try fails ends the turn
/// <c>error</c> with <c>chat.transient-stopped</c>, and the person's next word is a turn of the same conversation.
/// </summary>
/// <remarks>
/// The conversations run on the <c>acp-stub</c> adapter (a node stand-in on the protocol door), never an agent's binary
/// (<c>node tools/live-logins.mjs</c>). Its 529 is replayed from <c>claude-agent-acp</c> 0.84.0's source (<c>acp-agent.js:4178-4180</c>,
/// <c>:7249-7251</c>): source-derived (the design §3.3). Which prompts fail, and with what, is a gate folder the test writes, so a case
/// decides each turn's fate at the moment it matters. The service is <see cref="ChatLedger"/>'s in-process stand-in. No window, no real
/// console, no account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChatTransientProcessTests : IDisposable
{
    private const string Limit = "Internal error: You've hit your weekly limit";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chat-transient-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    // The roster's clock, which the limit's stage reads: held where the test put it. The retry clock is the case's own, a MovedClock.
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    private readonly Dictionary<string, int> _passed = new(StringComparer.Ordinal);

    public ChatTransientProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Gate);
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string Gate => Path.Combine(_home, "gate");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private static readonly HelpMachine Machine = new()
    {
        Adapter = "acp-stub",
        Helper = "acp-stub",
        Cap = 1,
        Repositories = [new("engine", "default") { Checkout = true, Drivable = true }],
    };

    private DriverConfig Config(string mode = "")
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Heard, Gate, mode],
                ["stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private HarnessRoster Roster(AdapterSet adapters) =>
        new(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => _now };

    private ChatRunner Runner(ServiceClient client, AdapterSet adapters, HarnessRoster roster, SessionEvents events, MovedClock clock) =>
        new(client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events, harnesses: roster)
        {
            RetryClock = clock,
        };

    /// <summary>How many of the following prompts (the warm exchange apart) the stand-in answers with the 529, the rest of them in order.</summary>
    private void Overloaded(int prompts) => File.WriteAllText(Path.Combine(Gate, "transient"), prompts.ToString());

    /// <summary>An account the stand-in refuses with the limit sentence until the gate is lifted, once no 529 is owed.</summary>
    private void Limited(string account) => File.WriteAllText(Path.Combine(Gate, "limited-" + account), "");

    private void Lifted(string account) => File.Delete(Path.Combine(Gate, "limited-" + account));

    private IReadOnlyList<string> Prompts => [.. StubFile.Lines(Heard).Where(line => line.StartsWith("prompt[", StringComparison.Ordinal))];

    private static IEnumerable<string> Carrying(IEnumerable<string> prompts, string part) => prompts.Where(line => line.Contains(part, StringComparison.Ordinal));

    private const string GoOn = "The last request failed on the provider's side (Internal error: API Error: 529 Overloaded.";

    private static bool Coded(SessionEvent e, string code) =>
        e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true;

    private static int Turns(IReadOnlyList<SessionEvent> events) => events.Count(e => e.Kind == SessionEventKind.Turn);

    private string Seen(ChatLedger ledger, SessionEvents events, string id, MovedClock? clock = null) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, said [{string.Join(",", ledger.Said(id))}], "
        + $"clock {clock?.GetUtcNow():O} pending [{string.Join(", ", clock?.Pending.Select(at => at.ToString("O")) ?? [])}], "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.StopReason}:{(e.Next is { } next ? "next " + next.ToString("O") : "")}{(e.Try is { } number ? "try " + number : "")}:{e.Text}"))}]";

    /// <summary>When each of the session's waits ends, as its record's quiet <c>transient</c> events said them, in order.</summary>
    private IReadOnlyList<DateTimeOffset> Waits(SessionEvents events, string session) =>
        [.. events.After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Transient && e.Next is not null).Select(e => e.Next!.Value)];

    /// <summary>
    /// Passes each wait of <paramref name="session"/>'s turn on the held clock, one at a time, by the record's own state, never by time: the
    /// record's next quiet event says the wait began and when its try goes, and the clock holds a timer due at exactly that moment, which says
    /// the turn waits on it. Only then is the clock moved, to that moment and no other.
    /// </summary>
    private async Task PassAsync(MovedClock clock, ChatLedger ledger, SessionEvents events, string session, params int[] minutes)
    {
        foreach (var each in minutes)
        {
            var wait = _passed.GetValueOrDefault(session) + 1;
            DateTimeOffset? until = null;
            await Poll.Until(
                () =>
                {
                    until = Waits(events, session).Skip(wait - 1).Select(at => (DateTimeOffset?)at).FirstOrDefault();
                    return until is { } at && clock.Pending.Contains(at);
                },
                () => $"wait {wait} never began on the held clock\n{Seen(ledger, events, session, clock)}", Poll.Patience);
            Assert.Equal(TimeSpan.FromMinutes(each), until!.Value - clock.GetUtcNow());
            _passed[session] = wait;
            clock.Advance(until.Value - clock.GetUtcNow());
        }
    }

    /// <summary>A live conversation that answered once, so a turn after it is the one that fails.</summary>
    private async Task<string> WarmAsync(ChatRunner runner, ChatLedger ledger, SessionEvents events, DriverConfig config, bool help, string? profile = null)
    {
        var session = help
            ? (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!
            : (await runner.StartAsync("engine", "acp-stub", config, profile: profile)).SessionId!;
        await Poll.Until(() => ledger.State(session) == "working", () => Seen(ledger, events, session));
        Assert.True(runner.Say(session, "Earlier answered exchange."));
        await Poll.Until(() => Turns(events.After(session, 0).Events) == 1, () => Seen(ledger, events, session));
        return session;
    }

    /// <summary>
    /// 🔴 A repository's chat: the 529 fails its turn twice and the third try answers. One turn, on the same process and in the same
    /// conversation (one <c>session/new</c>), the person's words received once: while it waits the queue is not released, no turn ends in
    /// the record, the curve's judge is not asked, nobody is notified and no limit is read; the line is said once. The record's quiet events
    /// say each wait and each try, in order, and the turn ends once, answered. Red before the retry, as today's <c>error</c> turn.
    /// </summary>
    [Fact]
    public async Task A_chat_turn_failing_transient_twice_then_answering_is_one_turn_on_the_same_process()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        var clock = new MovedClock();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = Roster(adapters);
        using var runner = Runner(client, adapters, roster, events, clock);
        var queues = new List<ChatQueue>();
        runner.QueueChanged += (_, queue) => { lock (queues) queues.Add(queue); };
        var answered = 0;
        runner.Answered += _ => Interlocked.Increment(ref answered);

        var id = await WarmAsync(runner, ledger, events, config, help: false);
        await Poll.Until(() => Volatile.Read(ref answered) == 1, () => Seen(ledger, events, id));
        DateTimeOffset? warmEnd;
        int sayAt;
        lock (queues)
        {
            warmEnd = queues[^1].LastTurnEnded;
            sayAt = queues.Count;
        }

        Overloaded(2);
        Assert.True(runner.Say(id, "Make the port configurable."));

        // The first wait: the turn is in flight, nothing has ended, and the line is said once.
        await Poll.Until(() => Waits(events, id).Count == 1 && clock.Pending.Count == 1, () => Seen(ledger, events, id, clock));
        var waiting = events.After(id, 0).Events;
        Assert.Equal(1, Turns(waiting));
        Assert.Single(waiting, e => Coded(e, NoteCodes.AgentTransientRetrying.Code));
        Assert.Equal(1, Volatile.Read(ref answered));
        Assert.True(runner.Taking(id));

        await PassAsync(clock, ledger, events, id, 1, 2);
        await Poll.Until(() => Turns(events.After(id, 0).Events) == 2, () => Seen(ledger, events, id, clock));
        await Poll.Until(() => !runner.Taking(id) && Volatile.Read(ref answered) == 2, () => Seen(ledger, events, id, clock));

        // One turn: three prompts of the conversation, the person's words once, the tries Daoris's go-on alone.
        var all = events.After(id, 0).Events;
        var prompts = Prompts;
        Assert.Equal(4, prompts.Count);
        Assert.Single(Carrying(prompts, "Make the port configurable."));
        Assert.StartsWith("prompt[account-1]: " + GoOn, prompts[2], StringComparison.Ordinal);
        Assert.StartsWith("prompt[account-1]: " + GoOn, prompts[3], StringComparison.Ordinal);
        Assert.DoesNotContain("Make the port configurable.", prompts[2], StringComparison.Ordinal);
        Assert.Single(StubFile.Lines(Heard), line => line == "session/new");
        Assert.Equal(1, ledger.Count);
        Assert.Equal("working", ledger.State(id));
        Assert.Single(all, e => Coded(e, NoteCodes.AgentTransientRetrying.Code));
        Assert.Equal(["end_turn", "end_turn"], all.Where(e => e.Kind == SessionEventKind.Turn).Select(e => e.StopReason));
        Assert.DoesNotContain(all, e => e.Kind == SessionEventKind.Note && e.Text?.Contains("could not be taken", StringComparison.Ordinal) == true);
        Assert.Single(all, e => e is { Kind: SessionEventKind.User, Origin: "person", Text: "Make the port configurable." });
        Assert.Equal(
            ["wait", "try:1", "wait", "try:2"],
            all.Where(e => e.Kind == SessionEventKind.Transient).Select(e => e.Try is { } number ? $"try:{number}" : e.Next is not null ? "wait" : "neither"));

        // The queue was never released and the curve never asked until the turn ended: every state of the turn in flight was Taking, and none
        // read as a turn just ended.
        List<ChatQueue> since;
        lock (queues) since = [.. queues.Skip(sayAt)];
        var ends = since.FindIndex(queue => queue.LastTurnEnded != warmEnd);
        Assert.True(ends >= 0, "the turn's end was never told");
        Assert.All(since.Take(ends), queue => Assert.True(queue.Taking));
        Assert.DoesNotContain(since.Take(ends), queue => CurveMove.TurnJustEnded(queue, warmEnd));
        Assert.Single(since.Select(queue => queue.LastTurnEnded).Where(ended => ended != warmEnd).Distinct());

        // No limit read, no account cooled.
        Assert.Null(roster.CoolingOf("acp-stub", null));
        Assert.False(File.Exists(AccountCooling.PathOf(_home)) && AccountCooling.Read(_home, DateTimeOffset.UtcNow).Count > 0, "an outage cooled an account");
    }

    /// <summary>
    /// 🔴 Ask Daoris, the same: its turn fails once and the first try answers; one turn, one process, the person's words once, the line once.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_turn_failing_transient_then_answering_is_one_turn_on_the_same_process()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        var clock = new MovedClock();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events, clock);

        var id = await WarmAsync(runner, ledger, events, config, help: true);
        Overloaded(1);
        Assert.True(runner.Say(id, "how do I drive a repository?"));

        await PassAsync(clock, ledger, events, id, 1);
        await Poll.Until(() => Turns(events.After(id, 0).Events) == 2 && !runner.Taking(id), () => Seen(ledger, events, id, clock));

        var all = events.After(id, 0).Events;
        Assert.Equal(3, Prompts.Count);
        Assert.Single(Carrying(Prompts, "how do I drive a repository?"));
        Assert.StartsWith("prompt[account-1]: " + GoOn, Prompts[2], StringComparison.Ordinal);
        Assert.Single(StubFile.Lines(Heard), line => line == "session/new");
        Assert.Single(all, e => Coded(e, NoteCodes.AgentTransientRetrying.Code));
        Assert.DoesNotContain(all, e => e is { Kind: SessionEventKind.Turn, StopReason: "error" });
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>
    /// 🔴 A word said during the wait goes at once and first: the wait ends without the clock moving, and the try is ONE prompt, the person's
    /// words and then Daoris's go-on, not the word alone as a prompt of its own at the agent's next step, which this agent takes words at. The
    /// word is received once and recorded once.
    /// </summary>
    [Fact]
    public async Task A_word_said_during_the_wait_goes_at_once_and_first_in_one_prompt_with_the_go_on()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config("queues");
        var events = Events;
        var clock = new MovedClock();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events, clock);

        var id = await WarmAsync(runner, ledger, events, config, help: false);
        Overloaded(1);
        Assert.True(runner.Say(id, "Make the port configurable."));
        await Poll.Until(() => Waits(events, id).Count == 1 && clock.Pending.Count == 1, () => Seen(ledger, events, id, clock));
        var began = clock.GetUtcNow();

        // Urgent, to an agent that takes a prompt during a turn: held all the same, and the try goes at once.
        var said = await runner.SayKeptAsync(id, "Use port 9090.", null, null, MessageLevel.Urgent, "press-1");
        Assert.Equal("turn-end", said!.Reaches);
        await Poll.Until(() => Turns(events.After(id, 0).Events) == 2 && !runner.Taking(id), () => Seen(ledger, events, id, clock));

        Assert.Equal(began, clock.GetUtcNow());
        var prompts = Prompts;
        Assert.Equal(3, prompts.Count);
        var tried = prompts[2];
        Assert.StartsWith("prompt[account-1]: Use port 9090. | " + GoOn, tried, StringComparison.Ordinal);
        Assert.Single(Carrying(prompts, "Use port 9090."));
        var all = events.After(id, 0).Events;
        Assert.Single(all, e => e is { Kind: SessionEventKind.User, Origin: "person", Text: "Use port 9090." });
        Assert.Single(all, e => e is { Kind: SessionEventKind.User, Origin: "target" } && e.Text!.StartsWith(GoOn, StringComparison.Ordinal));
        // Off its record once on the wire, and the conversation's one process went on.
        await Poll.Until(() => ledger.Said(id).Count == 0, () => Seen(ledger, events, id, clock));
        Assert.Single(StubFile.Lines(Heard), line => line == "session/new");
    }

    /// <summary>
    /// 🔴 A limit on a try goes to LIMITGOON1 once, as the move to the next account: the failed turn's own words and the words a try carried
    /// are held to be sent again by nobody, so each original word is received once. The new part opens on account-2 handed Daoris's brief
    /// alone, and no person's word is repeated to it. The conversation moves once, the account cools once, and the turn ends <c>error</c>
    /// as a limit's turn does.
    /// </summary>
    [Fact]
    public async Task A_limit_on_a_try_moves_the_chat_once_and_holds_no_word_the_failed_turn_or_a_try_carried()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        var clock = new MovedClock();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = Roster(adapters);
        using var runner = Runner(client, adapters, roster, events, clock);

        var old = await WarmAsync(runner, ledger, events, config, help: false, profile: "account-1");
        // The failed turn is answered with the 529 once; the try the person's word rides in meets account-1's limit.
        Overloaded(1);
        Limited("account-1");
        Assert.True(runner.Say(old, "Make the port configurable."));
        await Poll.Until(() => Waits(events, old).Count == 1 && clock.Pending.Count == 1, () => Seen(ledger, events, old, clock));
        Assert.True(runner.Say(old, "Use port 9090."));

        // The limit ends the episode as a limit; that turn's end moves the conversation, once.
        await Poll.Until(() => ledger.Count == 2 && ledger.State(old) is "completed" or "stopped" or "failed", () => Seen(ledger, events, old, clock));
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("acp-stub", "account-2", _root), (next.Adapter, next.Profile, next.Tree));
        Assert.Equal((old, HandOverWhy.Limit, HandOverBy.Daoris), (next.From, next.FromWhy, next.FromBy));
        Assert.NotNull(roster.CoolingOf("acp-stub", "account-1"));
        await Poll.Until(() => Prompts.Any(line => line.StartsWith("prompt[account-2]: ", StringComparison.Ordinal)), () => Seen(ledger, events, next.Session));
        await Poll.Until(() => Turns(events.After(next.Session, 0).Events) == 1, () => Seen(ledger, events, next.Session));

        // Each original word was received once, by the conversation it was said to; the new part is handed none of them as the person's.
        var prompts = Prompts;
        Assert.Single(Carrying(prompts, "Make the port configurable."), line => line.StartsWith("prompt[account-1]: ", StringComparison.Ordinal));
        Assert.Single(Carrying(prompts, "Use port 9090."), line => line.StartsWith("prompt[account-1]: ", StringComparison.Ordinal));
        Assert.Single(prompts, line => line.StartsWith("prompt[account-1]: Use port 9090. | " + GoOn, StringComparison.Ordinal));
        var handed = Assert.Single(prompts, line => line.StartsWith("prompt[account-2]: ", StringComparison.Ordinal));
        Assert.DoesNotContain("Use port 9090.", handed, StringComparison.Ordinal);
        Assert.DoesNotContain(GoOn, handed, StringComparison.Ordinal);
        var part = events.After(next.Session, 0).Events;
        Assert.DoesNotContain(part, e => e is { Kind: SessionEventKind.User, Origin: "person" });
        Assert.Single(part, e => e is { Kind: SessionEventKind.User, Origin: "target" } && e.Continues == old);

        // Said once on each part, and the old turn ended as a limit's does.
        Assert.Single(events.After(old, 0).Events, e => Coded(e, NoteCodes.MovedOn.Code));
        Assert.Single(events.After(old, 0).Events, e => e is { Kind: SessionEventKind.Turn, StopReason: "error" });
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// 🔴 The same limit on a try with every account of its agent cooling: the conversation waits for its limit (LIMITRESUME1c), a word said
    /// meanwhile kept once, and at the reset the stage wakes it once, in the same process and its own conversation, with the word said
    /// while it waited and Daoris's word, each once. The failed turn's own words are not among them: each original word was received once.
    /// </summary>
    [Fact]
    public async Task A_limit_on_a_try_with_every_account_cooling_waits_and_the_wake_receives_each_original_word_once()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        var clock = new MovedClock();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = Roster(adapters);
        using var runner = Runner(client, adapters, roster, events, clock);

        var id = await WarmAsync(runner, ledger, events, config, help: false, profile: "account-1");
        Limited("account-1");
        Limited("account-2");
        Assert.NotNull(roster.Limited("acp-stub", "account-2", Limit, "x0"));
        Overloaded(1);
        Assert.True(runner.Say(id, "Make the port configurable."));
        await PassAsync(clock, ledger, events, id, 1);
        await Poll.Until(
            () => events.After(id, 0).Events.Any(e => Coded(e, NoteCodes.ChatLimitWaits.Code)), () => Seen(ledger, events, id, clock));
        Assert.Equal(1, ledger.Count);

        // A word said while it waits for its limit: kept on its record once, queued, and sent nowhere.
        var said = await runner.SayKeptAsync(id, "And add a test.", null, null, MessageLevel.Urgent, "press-2");
        Assert.Equal("turn-end", said!.Reaches);
        Assert.Equal([said.Word!], ledger.Said(id));
        Assert.Empty(Carrying(Prompts, "And add a test."));

        // The reset: woken once, in its own process on its own account.
        Lifted("account-1");
        Lifted("account-2");
        _now = _now.AddHours(2);
        var woke = await runner.WakeLimitsAsync(config);
        Assert.Equal([id], woke.Select(start => start.SessionId));
        Assert.Empty(await runner.WakeLimitsAsync(config));
        await Poll.Until(() => Carrying(Prompts, "And add a test.").Any(), () => Seen(ledger, events, id, clock));

        var prompts = Prompts;
        var went = Assert.Single(Carrying(prompts, "And add a test."));
        Assert.StartsWith("prompt[account-1]: ", went, StringComparison.Ordinal);
        Assert.Equal(1, Count(went, "And add a test."));
        Assert.Equal(1, Count(went, LimitResetWord.Text));
        Assert.Equal(0, Count(went, "Make the port configurable."));
        Assert.Equal(0, Count(went, GoOn));
        // The failed turn's words were received once in all: the prompt the 529 answered.
        Assert.Single(Carrying(prompts, "Make the port configurable."));
        await Poll.Until(() => ledger.Said(id).Count == 0, () => Seen(ledger, events, id, clock));
        Assert.Single(StubFile.Lines(Heard), line => line == "session/new");
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>
    /// 🔴 The same agent erring every try: past the bound the turn ends <c>error</c> with <c>chat.transient-stopped</c>, after five tries and
    /// 31 minutes of waits, with no door note, no limit and no account cooled; the conversation stays, its record still working. The person's
    /// next word is a turn of the same conversation on the same process, answered.
    /// </summary>
    [Fact]
    public async Task Erring_every_try_ends_the_turn_error_with_chat_transient_stopped_and_the_next_word_is_a_turn_of_the_same_conversation()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        var clock = new MovedClock();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = Roster(adapters);
        using var runner = Runner(client, adapters, roster, events, clock);

        var id = await WarmAsync(runner, ledger, events, config, help: false);
        Overloaded(999);
        Assert.True(runner.Say(id, "Make the port configurable."));
        await PassAsync(clock, ledger, events, id, 1, 2, 4, 8, 16);
        await Poll.Until(() => Turns(events.After(id, 0).Events) == 2 && !runner.Taking(id), () => Seen(ledger, events, id, clock));

        var all = events.After(id, 0).Events;
        var stopped = Assert.Single(all, e => Coded(e, NoteCodes.ChatTransientStopped.Code));
        Assert.Contains("provider kept failing", stopped.Text);
        Assert.Contains("API Error: 529 Overloaded", stopped.Text);
        Assert.Equal(NoteCodes.ChatTransientStopped.Code, stopped.Parts![0].Code);
        Assert.Equal(["end_turn", "error"], all.Where(e => e.Kind == SessionEventKind.Turn).Select(e => e.StopReason));
        Assert.DoesNotContain(all, e => e.Kind == SessionEventKind.Note && e.Text?.Contains("could not be taken", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(all, e => Coded(e, NoteCodes.AccountCooling.Code) || Coded(e, NoteCodes.AccountCoolingWindow.Code));
        Assert.Equal(7, Prompts.Count);
        Assert.Equal("working", ledger.State(id));
        Assert.Equal(1, ledger.Count);
        Assert.False(File.Exists(AccountCooling.PathOf(_home)) && AccountCooling.Read(_home, DateTimeOffset.UtcNow).Count > 0, "an outage cooled an account");

        // The person's next word is a turn of the same conversation, on the same process.
        Overloaded(0);
        Assert.True(runner.Say(id, "Now the tests."));
        await Poll.Until(() => Turns(events.After(id, 0).Events) == 3 && !runner.Taking(id), () => Seen(ledger, events, id, clock));
        Assert.Equal("prompt[account-1]: Now the tests.", Prompts[^1]);
        Assert.Equal(["end_turn", "error", "end_turn"], events.After(id, 0).Events.Where(e => e.Kind == SessionEventKind.Turn).Select(e => e.StopReason));
        Assert.Single(StubFile.Lines(Heard), line => line == "session/new");
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>
    /// The person's stop during a wait ends the turn: <c>cancelled</c>, no try follows, no <c>error</c>, and the queue is free for the next word.
    /// </summary>
    [Fact]
    public async Task The_persons_stop_during_the_wait_ends_the_turn_cancelled_and_no_try_follows()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        var clock = new MovedClock();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events, clock);

        var id = await WarmAsync(runner, ledger, events, config, help: false);
        Overloaded(999);
        Assert.True(runner.Say(id, "Make the port configurable."));
        await Poll.Until(() => Waits(events, id).Count == 1 && clock.Pending.Count == 1, () => Seen(ledger, events, id, clock));

        await runner.CancelTurnAsync(id);
        await Poll.Until(() => Turns(events.After(id, 0).Events) == 2 && !runner.Taking(id), () => Seen(ledger, events, id, clock));

        Assert.Equal(["end_turn", "cancelled"], events.After(id, 0).Events.Where(e => e.Kind == SessionEventKind.Turn).Select(e => e.StopReason));
        // The warm exchange and the failed turn: no try followed the stop.
        Assert.Equal(2, Prompts.Count);
        Assert.DoesNotContain(events.After(id, 0).Events, e => Coded(e, NoteCodes.ChatTransientStopped.Code));
        Overloaded(0);
        Assert.True(runner.Say(id, "Now the tests."));
        await Poll.Until(() => Turns(events.After(id, 0).Events) == 3 && !runner.Taking(id), () => Seen(ledger, events, id, clock));
        Assert.Equal("prompt[account-1]: Now the tests.", Prompts[^1]);
    }

    /// <summary>
    /// 🔴 A hand-over is no recovery (Codex's owed read of D210, finding 1, on a conversation's turn): a word said while a turn runs is
    /// handed to it at its next step, and the agent answers the turn it hands off, <c>end_turn</c>, at the word's echo; the word's own turn
    /// then meets the 529. That turn's prompt was refused after the word was taken, so the turn that failed is the one the word opened: it
    /// is tried again inside the conversation's turn, which lasts, never a note of the word's own and never a turn that ended. One turn
    /// ends in the queue, the word is received once, and the try is Daoris's go-on.
    /// </summary>
    [Fact]
    public async Task A_word_taken_by_a_hand_over_whose_turn_fails_transient_is_tried_again_inside_the_turn()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config("handover");
        var events = Events;
        var clock = new MovedClock();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events, clock);
        var queues = new List<ChatQueue>();
        runner.QueueChanged += (_, queue) => { lock (queues) queues.Add(queue); };

        var id = await WarmAsync(runner, ledger, events, config, help: false);
        DateTimeOffset? warmEnd;
        int sayAt;
        lock (queues)
        {
            warmEnd = queues[^1].LastTurnEnded;
            sayAt = queues.Count;
        }

        Overloaded(1);
        Assert.True(runner.Say(id, "Hold on while I read the files."));
        await Poll.Until(() => Carrying(Prompts, "Hold on while I read the files.").Any(), () => Seen(ledger, events, id, clock));
        await Poll.Until(() => runner.Taking(id), () => Seen(ledger, events, id, clock));
        var said = await runner.SayKeptAsync(id, "And add a test.", null, null, MessageLevel.Urgent, "press-1");
        Assert.Equal("next-step", said!.Reaches);

        // The first turn is handed off to the word; the word's own turn meets the 529, and the episode waits.
        await PassAsync(clock, ledger, events, id, 1);
        await Poll.Until(() => Turns(events.After(id, 0).Events) == 3 && !runner.Taking(id), () => Seen(ledger, events, id, clock));

        var all = events.After(id, 0).Events;
        var prompts = Prompts;
        Assert.Equal(4, prompts.Count);
        Assert.Single(Carrying(prompts, "Hold on while I read the files."));
        Assert.Single(Carrying(prompts, "And add a test."));
        Assert.StartsWith("prompt[account-1]: " + GoOn, prompts[3], StringComparison.Ordinal);
        Assert.Single(all, e => Coded(e, NoteCodes.AgentTransientRetrying.Code));
        Assert.DoesNotContain(all, e => e is { Kind: SessionEventKind.Turn, StopReason: "error" });
        Assert.DoesNotContain(all, e => e.Kind == SessionEventKind.Note && e.Text?.Contains("would not take", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Single(all, e => e is { Kind: SessionEventKind.User, Origin: "person", Text: "And add a test.", Reaches: null });
        Assert.Single(StubFile.Lines(Heard), line => line == "session/new");

        // One turn ended in the queue: the pump found the turn over once, after the try answered.
        List<ChatQueue> since;
        lock (queues) since = [.. queues.Skip(sayAt)];
        Assert.Single(since.Select(queue => queue.LastTurnEnded).Where(ended => ended != warmEnd).Distinct());
        var ends = since.FindIndex(queue => queue.LastTurnEnded != warmEnd);
        Assert.All(since.Take(ends), queue => Assert.True(queue.Taking));
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>
    /// A stand-in ACP agent: it names its conversation, writes down <c>session/new</c> and each prompt with the stub account it runs as, and
    /// answers each after a moment. A prompt that is not the warm exchange (<c>Earlier…</c>) is answered, while the gate's <c>transient</c>
    /// file owes any, with claude-agent-acp's 529: its error streamed as the agent's words, then the prompt's JSON-RPC error with
    /// <c>data.errorKind: overloaded</c> (source-derived); then, while the gate says its account is limited, with Claude Code's limit
    /// sentence; else answered. <c>queues</c> says it takes a prompt during a turn; <c>handover</c> says so too and hands a held turn
    /// (a prompt that begins <c>Hold</c>) off to the next prompt.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-transient-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
            import { basename, join } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, gate, mode] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const overloaded = 'API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment. If it persists, check https://status.claude.com.';
            const owed = () => { const file = join(gate, 'transient'); return existsSync(file) ? Number(readFileSync(file, 'utf8')) : 0; };
            let conversation = null;
            const held = [];
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                const agentCapabilities = { loadSession: true, sessionCapabilities: { resume: {}, close: {} } };
                if (mode === 'queues' || mode === 'handover') agentCapabilities._meta = { claudeCode: { promptQueueing: true } };
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                conversation = 'conv-' + account;
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note(`prompt[${account}]: ` + text.replace(/\r?\n/g, ' '));
                if (mode === 'handover') {
                  // A prompt that begins `Hold` runs until a later prompt hands its turn off, as claude-agent-acp does at a queued prompt's
                  // echo (STEER1): the turn handed off answers end_turn, and the word's own turn goes on from there.
                  if (text.startsWith('Hold')) { held.push(frame.id); return; }
                  if (held.length > 0) send({ jsonrpc: '2.0', id: held.shift(), result: { stopReason: 'end_turn' } });
                }
                if (!text.startsWith('Earlier')) {
                  if (owed() > 0) {
                    writeFileSync(join(gate, 'transient'), String(owed() - 1));
                    // claude-agent-acp streams the error as its agent's words, then answers the prompt with it (source-derived).
                    say(overloaded);
                    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: ' + overloaded, data: { errorKind: 'overloaded' } } });
                    return;
                  }
                  if (existsSync(join(gate, 'limited-' + account))) {
                    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: "Internal error: You've hit your weekly limit" } });
                    return;
                  }
                }
                await new Promise((resolve) => setTimeout(resolve, 60));
                say('heard ' + text);
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
        var script = Path.Combine(_home, "text-agent.mjs");
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
