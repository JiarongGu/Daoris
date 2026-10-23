using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LIMITRESUME1c (D206 §3.4, §3.5) with real conversations: a conversation whose every account is at its limit waits, its words with it,
/// and the look's <c>limits</c> stage wakes it, its own account first, in its own conversation, with the refused turns' words, the words
/// said while it waited and Daoris's word, each once; or, another account freeing first, moves there handed the brief and both kinds of
/// words. A conversation it cannot resume because its conversation is lost goes on briefed, never <i>cannot go on</i>; a refusal that
/// proves no loss waits for the person with no Daoris word left. The limit's move to a new part is delivered once across a shell's end.
/// </summary>
/// <remarks>
/// The conversations run on the <c>acp-stub</c> adapter (a node stand-in on the protocol door), whose accounts are the stub's (TOOL4j);
/// the stand-in reads which one it runs as from the stub's variable, answers a prompt with Claude Code's limit sentence while a gate file
/// says its account is limited, and refuses a resume as the gate says. The roster's clock is the test's, held: the stage reads it, so a
/// reset comes when the test moves it, never by the machine's clock. No model, no account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitWaitsProcessTests : IDisposable
{
    private const string Limit = "Internal error: You've hit your weekly limit";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-limit-waits-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    // The roster's clock, which the stage reads: held where the test put it.
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public LimitWaitsProcessTests()
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

    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Heard, Gate],
                ["stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private HarnessRoster Roster(AdapterSet adapters) =>
        new(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => _now };

    private ChatRunner Runner(ServiceClient client, AdapterSet adapters, HarnessRoster roster, SessionEvents events) =>
        new(client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events, harnesses: roster);

    /// <summary>An account the stand-in refuses with the limit sentence until the gate is lifted.</summary>
    private void Limited(string account) => File.WriteAllText(Path.Combine(Gate, "limited-" + account), "");

    private void Lifted(string account) => File.Delete(Path.Combine(Gate, "limited-" + account));

    /// <summary>An account cooling until <paramref name="until"/>, as the cooling store keeps it.</summary>
    private void Cooling(string account, DateTimeOffset until) =>
        AccountCooling.Cool(_home, new CoolingEntry("stub", account, until, Stated: true, "weekly", _now, null), _now);

    private IReadOnlyList<string> Prompts => [.. StubFile.Lines(Heard).Where(line => line.StartsWith("prompt[", StringComparison.Ordinal))];

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, said [{string.Join(",", ledger.Said(id))}], "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Text}"))}]";

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    private static bool Coded(SessionEvent e, string code) =>
        e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true;

    /// <summary>A conversation's last turn refused by its account's limit, as the protocol door records it: the words, the cooling line, the turn's end.</summary>
    private static void Refused(SessionEvents events, string session, string words)
    {
        events.Append(session, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = words });
        events.Append(session, new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = "its account is cooling", Parts = [new NotePart { Code = NoteCodes.AccountCoolingWindow.Code }],
        });
        events.Append(session, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
    }

    /// <summary>A live conversation on account-1 that answered once, then met its limit with account-2 cooling too, and so waits.</summary>
    private async Task<string> WaitingAsync(ChatRunner runner, ChatLedger ledger, SessionEvents events, HarnessRoster roster, DriverConfig config, bool help)
    {
        var session = help
            ? (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!
            : (await runner.StartAsync("engine", "acp-stub", config, profile: "account-1")).SessionId!;
        await Poll.Until(() => ledger.State(session) == "working", () => Seen(ledger, events, session));
        Assert.True(runner.Say(session, "Earlier answered exchange."));
        await Poll.Until(() => events.After(session, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, session));
        return session;
    }

    private async Task LimitedNowAsync(ChatRunner runner, ChatLedger ledger, SessionEvents events, HarnessRoster roster, string session, string words)
    {
        Assert.True(runner.Say(session, words));
        await Poll.Until(
            () => events.After(session, 0).Events.Any(e => Coded(e, NoteCodes.ChatLimitWaits.Code)), () => Seen(ledger, events, session));
    }

    /// <summary>
    /// 🔴 A chat and Ask Daoris, every account cooling: each waits, a word said meanwhile kept once and sent nowhere, and the stage asks
    /// nothing while no wait has come. At the reset the stage wakes each once, on its own account, in the same process: one prompt with
    /// the refused turn's words, the word said while it waited and Daoris's word last, each once; the record shows each once.
    /// </summary>
    [Fact]
    public async Task A_chat_and_Ask_Daoris_with_every_account_cooling_wait_and_go_on_once_in_the_same_process_at_the_reset()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = Roster(adapters);
        using var runner = Runner(client, adapters, roster, events);

        var chat = await WaitingAsync(runner, ledger, events, roster, config, help: false);
        var help = await WaitingAsync(runner, ledger, events, roster, config, help: true);
        Limited("account-1");
        Limited("account-2");
        Assert.NotNull(roster.Limited("acp-stub", "account-2", Limit, "x0"));
        await LimitedNowAsync(runner, ledger, events, roster, chat, "Make the port configurable.");
        await LimitedNowAsync(runner, ledger, events, roster, help, "how do I drive a repository?");
        Assert.Equal(2, ledger.Count);

        // A word said while it waits: kept on its record once, queued, and sent nowhere.
        var said = await runner.SayKeptAsync(chat, "And add a test.", null, null, MessageLevel.Urgent, "press-1");
        Assert.Equal("turn-end", said!.Reaches);
        Assert.Equal([said.Word!], ledger.Said(chat));

        // No wait has come: the stage reads once as it comes up, then asks nothing on the held clock.
        Assert.Empty(await runner.WakeLimitsAsync(config));
        var asked = runner.LimitsAsked;
        Assert.Empty(await runner.WakeLimitsAsync(config));
        Assert.Empty(await runner.WakeLimitsAsync(config));
        Assert.Equal(asked, runner.LimitsAsked);
        Assert.DoesNotContain(Prompts, line => line.Contains("And add a test.", StringComparison.Ordinal));
        var refusedOn = Prompts.Single(line => line.Contains("Make the port configurable.", StringComparison.Ordinal));

        // The reset: each is woken once, in its own process on its own account.
        Lifted("account-1");
        Lifted("account-2");
        _now = _now.AddHours(2);
        var woke = await runner.WakeLimitsAsync(config);
        Assert.Equal([chat, help], woke.Select(start => start.SessionId).Order());
        Assert.Empty(await runner.WakeLimitsAsync(config));

        await Poll.Until(() => Prompts.Any(line => line.Contains("And add a test.", StringComparison.Ordinal)), () => Seen(ledger, events, chat));
        await Poll.Until(
            () => Prompts.Count(line => line.Contains(LimitResetWord.Text, StringComparison.Ordinal)) == 2, () => Seen(ledger, events, help));
        var went = Prompts.Single(line => line.Contains("And add a test.", StringComparison.Ordinal));
        Assert.StartsWith(refusedOn[..refusedOn.IndexOf(':')], went, StringComparison.Ordinal);
        Assert.Equal((1, 1, 1), (Count(went, "Make the port configurable."), Count(went, "And add a test."), Count(went, LimitResetWord.Text)));
        Assert.True(went.IndexOf("Make the port configurable.", StringComparison.Ordinal) < went.IndexOf("And add a test.", StringComparison.Ordinal));
        Assert.True(went.IndexOf("And add a test.", StringComparison.Ordinal) < went.IndexOf(LimitResetWord.Text, StringComparison.Ordinal));
        var helped = Prompts.Last(line => line.Contains(LimitResetWord.Text, StringComparison.Ordinal) && line != went);
        Assert.Equal(1, Count(helped, "how do I drive a repository?"));

        // Its record shows each once, and the words leave it once on the wire.
        await Poll.Until(() => ledger.Said(chat).Count == 0, () => Seen(ledger, events, chat));
        var words = events.After(chat, 0).Events.Where(e => e.Kind == SessionEventKind.User).Select(e => e.Text).ToList();
        Assert.Equal(1, words.Count(text => text == "Make the port configurable."));
        Assert.Equal(1, words.Count(text => text == "And add a test."));
        Assert.Single(events.After(chat, 0).Events, e => Coded(e, NoteCodes.LimitResumed.Code));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// 🔴 An ended conversation another account frees first for: the stage moves it by the walk to that account, handed the brief, the
    /// refused turn's words and the word said while it waited, none twice and no Daoris word; the waiting word leaves the old record as
    /// taken by the new part, and the move's box goes.
    /// </summary>
    [Fact]
    public async Task An_ended_conversation_another_account_frees_first_for_moves_with_the_brief_and_both_kinds_of_words_once()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = Roster(adapters);
        string chat;
        string kept;
        using (var runner = Runner(client, adapters, roster, events))
        {
            chat = await WaitingAsync(runner, ledger, events, roster, config, help: false);
            Limited("account-1");
            Assert.NotNull(roster.Limited("acp-stub", "account-2", Limit, "x0"));
            await LimitedNowAsync(runner, ledger, events, roster, chat, "Make the port configurable.");
            kept = (await runner.SayKeptAsync(chat, "And add a test.", null, null, MessageLevel.Urgent, "press-1"))!.Word!;
        }

        // The shell ended: the conversation ended with it, its waiting word still on its record.
        Assert.Equal("stopped", ledger.State(chat));
        Assert.Equal([kept], ledger.Said(chat));

        // Account-2 frees first; account-1 still cools.
        Assert.True(AccountCooling.End(_home, "stub", "account-2", _now));
        using var again = Runner(client, adapters, roster, events);
        var woke = Assert.Single(await again.WakeLimitsAsync(config));
        Assert.Equal("c2", woke.SessionId);
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("account-2", chat, HandOverWhy.Limit), (next.Profile, next.From, next.FromWhy));

        await Poll.Until(() => Prompts.Any(line => line.StartsWith("prompt[account-2]", StringComparison.Ordinal)), () => Seen(ledger, events, "c2"));
        var handed = Prompts.Single(line => line.StartsWith("prompt[account-2]", StringComparison.Ordinal));
        Assert.Contains($"You continue session `{chat}`", handed);
        Assert.Equal((1, 1, 0), (Count(handed, "Make the port configurable."), Count(handed, "And add a test."), Count(handed, LimitResetWord.Text)));

        await Poll.Until(() => ledger.Said(chat).Count == 0, () => Seen(ledger, events, chat));
        Assert.Contains((chat, kept, "c2"), ledger.TakenBy);
        Assert.False(File.Exists(new LimitBoxes(_home).PathOf(chat)));
        Assert.Empty(await again.WakeLimitsAsync(config));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// An ended chat whose person's words wait for its own cooling account (D137 point 7) never met a limit: another account free moves
    /// nothing, and at its own account's reset the stage goes on with its words in its own conversation, on that account.
    /// </summary>
    [Fact]
    public async Task An_ended_chats_waiting_words_are_never_moved_and_go_on_on_its_own_account_at_its_reset()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "acp-stub", _root, profile: "account-1").Say("w1", "Hello again.");
        new HarnessConversations(_home).Keep("c1", "acp-stub", "conv-account-1");
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = Roster(adapters);
        Cooling("account-1", _now.AddHours(1));
        using var runner = Runner(client, adapters, roster, events);

        Assert.Empty(await runner.WakeLimitsAsync(config));
        Assert.Equal(1, ledger.Count);
        Assert.Empty(StubFile.Lines(Heard));

        _now = _now.AddHours(2);
        var woke = Assert.Single(await runner.WakeLimitsAsync(config));
        Assert.Equal("c1", woke.SessionId);
        await Poll.Until(() => Prompts.Any(line => line.Contains("Hello again.", StringComparison.Ordinal)), () => Seen(ledger, events, "c1"));
        Assert.StartsWith("prompt[account-1]", Prompts.Single(line => line.Contains("Hello again.", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("resumed[account-1]: conv-account-1", StubFile.Lines(Heard));
        Assert.DoesNotContain(LimitResetWord.Text, string.Join("\n", Prompts), StringComparison.Ordinal);
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>An ended conversation held by its limit, on account-1, its conversation kept; account-2 cooling for a day.</summary>
    private ChatLedger HeldEnded(SessionEvents events, params (string Id, string Text)[] waiting)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var words = ledger.Chat("c1", "stopped", "acp-stub", _root, profile: "account-1");
        foreach (var (id, text) in waiting) words.Say(id, text);
        new HarnessConversations(_home).Keep("c1", "acp-stub", "conv-account-1");
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Earlier answered exchange." });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        Refused(events, "c1", "Make the port configurable.");
        Cooling("account-2", _now.AddDays(1));
        return ledger;
    }

    /// <summary>
    /// 🔴 B9: its own account ready, and its agent answers the resume <c>resource_not_found</c>: Daoris's word leaves the record, and the
    /// conversation goes on in a new part on the account the walk gives, its own allowed, handed the brief and the refused turn's words,
    /// its record saying its own conversation could not be resumed. Never <i>cannot go on</i>.
    /// </summary>
    [Fact]
    public async Task A_lost_conversation_on_its_ready_own_account_goes_on_in_a_new_part_never_cannot_go_on()
    {
        var events = Events;
        var ledger = HeldEnded(events);
        File.WriteAllText(Path.Combine(Gate, "gone-conv-account-1"), "");
        var config = Config();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events);

        Assert.Single(await runner.WakeLimitsAsync(config));
        await Poll.Until(() => ledger.Count == 2 && Prompts.Count > 0, () => Seen(ledger, events, "c1"));
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("account-1", "c1", HandOverWhy.Limit), (next.Profile, next.From, next.FromWhy));
        var handed = Assert.Single(Prompts);
        Assert.StartsWith("prompt[account-1]", handed, StringComparison.Ordinal);
        Assert.Contains("You continue session `c1`", handed);
        Assert.Equal((1, 0), (Count(handed, "Make the port configurable."), Count(handed, LimitResetWord.Text)));

        var old = events.After("c1", 0).Events;
        Assert.DoesNotContain(old, e => e.Kind == SessionEventKind.Note && e.Why is not null);
        var refused = Assert.Single(old, e => Coded(e, NoteCodes.LimitResumeRefused.Code));
        Assert.Equal(ContinueWhy.Gone, refused.Parts!.Single(part => part.Code is not null).Value("why"));
        Assert.Empty(ledger.Said("c1"));
        Assert.Contains(ledger.TakenBy, taken => taken.Session == "c1" && taken.By == "c1");
    }

    /// <summary>
    /// A refusal that proves no loss (the protocol door's plain refusal, as the native door's <c>exited</c> is): the conversation waits for
    /// the person, said why, no Daoris word left on its record, and the stage tries it no more by itself.
    /// </summary>
    [Fact]
    public async Task A_resume_refused_without_a_loss_waits_for_the_person_with_no_daoris_word_left()
    {
        var events = Events;
        var ledger = HeldEnded(events);
        File.WriteAllText(Path.Combine(Gate, "refuse-resume"), "");
        var config = Config();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events);

        Assert.Single(await runner.WakeLimitsAsync(config));
        await Poll.Until(
            () => events.After("c1", 0).Events.Any(e => e.Kind == SessionEventKind.Note && e.Why == ContinueWhy.Refused),
            () => Seen(ledger, events, "c1"));
        Assert.Empty(ledger.Said("c1"));
        Assert.Equal(1, ledger.Count);
        Assert.Empty(Prompts);

        _now = _now.AddDays(2);
        Assert.Empty(await runner.WakeLimitsAsync(config));
        Assert.Single(StubFile.Lines(Heard), line => line.StartsWith("resumed", StringComparison.Ordinal) || line.StartsWith("refused", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 Codex 5, N16: a wait cached for tomorrow is woken at the next look by what frees an account early, read by the stamp: the own
    /// account's <i>Try now</i> and its sign-in go on in its own conversation; an account added goes on there, briefed.
    /// </summary>
    [Theory]
    [InlineData("try-now")]
    [InlineData("sign-in")]
    [InlineData("added")]
    public async Task A_wait_cached_for_tomorrow_wakes_at_the_next_look_when_an_account_frees_early(string how)
    {
        var events = Events;
        var ledger = HeldEnded(events);
        Cooling("account-1", _now.AddDays(1));
        var config = Config();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = Roster(adapters);
        using var runner = Runner(client, adapters, roster, events);

        Assert.Empty(await runner.WakeLimitsAsync(config));
        var asked = runner.LimitsAsked;
        Assert.Empty(await runner.WakeLimitsAsync(config));
        Assert.Equal(asked, runner.LimitsAsked);

        var account = "account-1";
        switch (how)
        {
            case "try-now":
                Assert.True(AccountCooling.End(_home, "stub", "account-1", _now));
                break;
            case "sign-in":
                Assert.True(AccountCooling.SignedIn(HarnessSettings.ProfileHome(_home, "stub", "account-1"), _now));
                break;
            default:
                account = "account-3";
                Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
                new HarnessSettings().WithRotation("stub", ["account-1", "account-2", account]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
                break;
        }

        Assert.Single(await runner.WakeLimitsAsync(config));
        Assert.Equal(asked + 1, runner.LimitsAsked);
        await Poll.Until(() => Prompts.Any(line => line.StartsWith($"prompt[{account}]", StringComparison.Ordinal)), () => Seen(ledger, events, "c1"));
        Assert.Contains("Make the port configurable.", Prompts.Single());
    }

    /// <summary>
    /// 🔴 Codex 8: a shell that ended after the limit's move opened its new part and before its words were delivered. The come-up
    /// delivers the box to that part, which goes on with the brief and both kinds of words in its own conversation; the old record's
    /// waiting word stays on it until then, and leaves it once, as taken by the new part.
    /// </summary>
    [Fact]
    public async Task A_move_opened_and_not_delivered_is_delivered_to_its_new_part_at_the_come_up()
    {
        var events = Events;
        var ledger = HeldEnded(events, ("w1", "And add a test."));
        ledger.Chat("c2", "stopped", "acp-stub", _root, profile: "account-2", from: new ContinuedFrom("c1", HandOverWhy.Limit, HandOverBy.Daoris));
        new HarnessConversations(_home).Keep("c2", "acp-stub", "conv-account-2");
        AccountCooling.End(_home, "stub", "account-2", _now);
        Cooling("account-1", _now.AddDays(1));
        new LimitBoxes(_home).Write("c1", new LimitMoveBox(HandOverWhy.Limit, 2, events.After("c1", 0).Events.Count, ["w1"], "c2"));
        var config = Config();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events);

        await runner.TakeUpAsync();
        await Poll.Until(() => Prompts.Count > 0 && ledger.Said("c1").Count == 0, () => Seen(ledger, events, "c2"));
        var handed = Assert.Single(Prompts);
        Assert.StartsWith("prompt[account-2]", handed, StringComparison.Ordinal);
        Assert.Contains("You continue session `c1`", handed);
        Assert.Equal((1, 1), (Count(handed, "Make the port configurable."), Count(handed, "And add a test.")));
        Assert.Contains("resumed[account-2]: conv-account-2", StubFile.Lines(Heard));

        // The new part's own word left first, then the old record's, as taken by the new part.
        var taken = ledger.TakenBy.ToList();
        var own = taken.FindIndex(each => each.Session == "c2");
        var old = taken.FindIndex(each => each == ("c1", "w1", "c2"));
        Assert.True(own >= 0 && old > own, string.Join(" | ", taken));
        Assert.False(File.Exists(new LimitBoxes(_home).PathOf("c1")));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// 🔴 Codex 8: a shell that ended as the move's words were delivered, its new part having taken its turn. The come-up ends the box: the
    /// old record's waiting word leaves it once, as taken by the new part, and nothing is delivered again.
    /// </summary>
    [Fact]
    public async Task A_move_delivered_as_the_shell_ended_is_never_delivered_twice()
    {
        var events = Events;
        var ledger = HeldEnded(events, ("w1", "And add a test."));
        ledger.Chat("c2", "stopped", "acp-stub", _root, profile: "account-2", from: new ContinuedFrom("c1", HandOverWhy.Limit, HandOverBy.Daoris));
        new HarnessConversations(_home).Keep("c2", "acp-stub", "conv-account-2");
        events.Append("c2", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the port configurable.\n\nAnd add a test." });
        new LimitBoxes(_home).Write("c1", new LimitMoveBox(HandOverWhy.Limit, 2, events.After("c1", 0).Events.Count, ["w1"], "c2"));
        Config();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events);

        await runner.TakeUpAsync();
        Assert.Equal([("c1", "w1", "c2")], ledger.TakenBy.Select(each => (each.Session, each.Ids, each.By)));
        Assert.Empty(ledger.Said("c1"));
        Assert.Empty(ledger.Said("c2"));
        Assert.False(File.Exists(new LimitBoxes(_home).PathOf("c1")));
        Assert.Empty(StubFile.Lines(Heard));
    }

    /// <summary>
    /// *Stop waiting* (D206 §3.7, N27): an ended conversation held by its limit waits no more. Its waiting word is marked, its record says
    /// so, and at its account's reset the stage starts nothing; its word waits for the person's next.
    /// </summary>
    [Fact]
    public async Task Stop_waiting_leaves_the_wait_and_nothing_goes_on_by_itself()
    {
        var events = Events;
        var ledger = HeldEnded(events, ("w1", "And add a test."));
        Cooling("account-1", _now.AddHours(1));
        var config = Config();
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        using var runner = Runner(client, adapters, Roster(adapters), events);

        Assert.Empty(await runner.WakeLimitsAsync(config));
        Assert.Null(await runner.StopWaitingAsync("c1"));
        var mark = new GoOnMarks(_home).Read("c1")!;
        Assert.Equal(GoOnMarks.StoppedWaiting, mark.Why);
        Assert.Equal(["w1"], mark.Said);
        Assert.Single(events.After("c1", 0).Events, e => Coded(e, NoteCodes.ChatLimitStoppedWaiting.Code));
        Assert.NotNull(await runner.StopWaitingAsync("c1"));

        _now = _now.AddDays(2);
        Assert.Empty(await runner.WakeLimitsAsync(config));
        Assert.Empty(StubFile.Lines(Heard));
        Assert.Equal(["w1"], ledger.Said("c1"));
    }

    /// <summary>
    /// A stand-in ACP agent: names its conversation, writes down each prompt with the stub account and process it runs as, and answers a
    /// prompt with the protocol door's limit refusal while the gate says its account is limited. A resume is refused
    /// <c>resource_not_found</c> where the gate says the conversation is gone, plainly where it says resumes are refused.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-limit-waits-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync } from 'node:fs';
            import { basename, join } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, gate] = process.argv.slice(2);
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
                const id = frame.params.sessionId;
                if (existsSync(join(gate, 'gone-' + id))) {
                  note(`refused[${account}]: ${id}`);
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32002, message: 'Resource not found' } });
                  return;
                }
                if (existsSync(join(gate, 'refuse-resume'))) {
                  note(`refused[${account}]: ${id}`);
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: it cannot be resumed now' } });
                  return;
                }
                conversation = id;
                note(`resumed[${account}]: ${id}`);
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note(`prompt[${account}][${process.pid}]: ` + text.replace(/\r?\n/g, ' '));
                if (existsSync(join(gate, 'limited-' + account)) && text !== 'Earlier answered exchange.') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: "Internal error: You've hit your weekly limit" } });
                  return;
                }
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
