using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL4d (D125 §2, §4, §5.2): a turn the door refused for an account's limit fails with <c>limit</c>, which is never a
/// strike; the account cools until the reset the agent named; a start on it is held at spawn with no process and no
/// record, the quest waiting rather than parked; and the wait is said once.
/// </summary>
/// <remarks>
/// Driven over the real client and the real planner, with the service's doors standing in (<see cref="StandInLedger"/>):
/// the start's run is the real one, which reaches the selection and holds there before anything is spawned, because
/// the repository opens a tree per session and so asks git nothing first. Observation 4 replayed through a real stub
/// agent is <c>AccountLimitTickTests</c>, in the <c>Process</c> half.
/// </remarks>
public sealed class AccountLimitHoldTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private static readonly string Refusal =
        "the ACP agent refused the call: You've hit your individual spend limit · … · your weekly limit resets "
        + $"Oct 3, 4pm ({Zone.Id})";

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-limit-hold-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private DateTimeOffset _now = Seen;

    public AccountLimitHoldTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine"));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private HarnessRoster Roster() =>
        new(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")) { Clock = () => _now, Zone = Zone };

    private static DriverConfig Config => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "acp-stub",
        // One failure would park it: a limit must not.
        Strikes = 1,
        PollSeconds = 1,
    };

    private Daoris.Driver.Driver Driver(ServiceClient service, HarnessRoster roster, string adapter = "acp-stub") =>
        new(service, Config with { Adapter = adapter }, AdapterSet.Built(), _home, harnesses: roster);

    /// <summary>Observation 4's cut-off, as the driver records one: the quest taken, its session failed with a limit.</summary>
    private async Task<string> CutOffAsync(ServiceClient service, bool limit = true)
    {
        _ledger.Publish("q1", "engine");
        var (id, _) = await service.OpenSessionAsync("q1", "acp-stub", null, null, Path.Combine(_home, "engine"), null);
        await service.AdvanceAsync(id!, "working");
        _ledger.Move("q1", "Taken");
        await service.AdvanceAsync(id!, "failed", note: $"the agent's turn failed with the quest still taken: {Refusal}", limit: limit);
        return id!;
    }

    // ——— The record (§5.2): `limit`, sent by the client, never a strike.

    [Fact]
    public async Task The_client_sends_limit_with_a_failure_and_the_record_keeps_it()
    {
        using var service = _ledger.Client();

        var id = await CutOffAsync(service);

        Assert.True(_ledger.Session(id)["limit"]!.GetValue<bool>());
        Assert.Empty(ServiceClient.ReadStrikes(JsonSerializer.Serialize(_ledger.Sessions)));
    }

    [Fact]
    public async Task A_failure_said_without_limit_sends_no_limit()
    {
        using var service = _ledger.Client();

        var id = await CutOffAsync(service, limit: false);

        Assert.Null(_ledger.Session(id)["limit"]);
        Assert.Equal(1, ServiceClient.ReadStrikes(JsonSerializer.Serialize(_ledger.Sessions))["q1"]);
    }

    // ——— The conclusion: a refused turn the table recognises cools its account (§2.3).

    [Fact]
    public void A_refused_turn_the_table_recognises_is_a_limit_and_cools_the_account_it_ran_on()
    {
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var roster = Roster();
        var driver = Driver(service, roster);
        var failed = Observation.Conclude(0, "Taken", turnFailed: Refusal);

        var (conclusion, limit) = driver.AccountLimited(
            failed, AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null), Refusal, "s1", new AcpUsage(370_104, 1_000_000));

        Assert.True(limit);
        var cooling = roster.CoolingOf("acp-stub", null);
        Assert.Equal(("stub", (string?)null, Until, true, "s1"), (cooling!.Agent, cooling.Account, cooling.Until, cooling.Stated, cooling.Session));
        Assert.Equal("failed", conclusion.State);
        // The lead-in, then the facts; the agent's sentence is the raw view's (AGT3c).
        Assert.Equal($"the agent's turn failed with the quest still taken: {CoolingWords.Note(cooling, Zone)}", conclusion.Note);

        var line = Assert.Single(lines);
        Assert.Equal("account.limited", line.Event);
        Assert.Equal(
            ["session", "adapter", "account", "hit", "window", "until", "stated", "assumedZone", "turn", "used"],
            line.Data.Select(field => field.Key));
        Assert.Equal(
            new object?[] { "s1", "acp-stub", null, "individual spend", "weekly", "2026-10-03T10:17:00Z", true, false, 1L, 370_104L },
            line.Data.Select(field => field.Value));
    }

    /// <summary>
    /// AGT3c (D125's discovery note; D80's ACPEND1): the note travels to every machine, so a limit's carries its facts, the
    /// reset as a moment and why it holds, and never the agent's sentence, whose zone is the zone of the machine it was said
    /// on. That sentence stays in the transcript and the conversation, the raw view, which stay here.
    /// </summary>
    [Fact]
    public void A_limit_s_note_carries_its_reset_as_a_moment_another_machine_words_in_its_own_zone_and_never_the_agent_s_sentence()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        // Said in another zone than this machine's, so the agent's zone and Daoris's are told apart.
        const string said = "the ACP agent refused the call: You've hit your individual spend limit · … · your weekly limit resets "
            + "Oct 3, 4pm (Europe/London)";
        var failed = Observation.Conclude(0, "Taken", turnFailed: said);

        var (conclusion, limit) = Driver(service, roster).AccountLimited(
            failed, AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null), said, "s1", used: null);

        Assert.True(limit);
        Assert.DoesNotContain("hit your", conclusion.Note);
        Assert.DoesNotContain("Europe/London", conclusion.Note);
        Assert.DoesNotContain(conclusion.Parts!, part => part.Code is null);
        Assert.Equal(["ended.turn-failed-taken", "account.cooling"], NoteAssert.Codes(conclusion.Parts));
        NoteAssert.Holds(conclusion.Note, conclusion.Parts);

        // Another machine reads the parts as the feed carries them, and words the same moment in its own zone.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("noteParts");
            NotePart.Write(writer, conclusion.Parts!);
            writer.WriteEndObject();
        }

        var travelled = NotePart.Read(JsonDocument.Parse(stream.ToArray()).RootElement)!;
        var until = DateTimeOffset.ParseExact(
            (string)travelled[^1].Value("until")!, "yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 15, 2, 0, TimeSpan.Zero), until);
        Assert.Equal(CoolingWhy.Stated, travelled[^1].Value("why"));
        // AGT3d: which kind of limit and whose account travel as facts too, the window as the reset named it.
        Assert.Equal(("weekly", "stub"), ((string?)travelled[^1].Value("window"), (string?)travelled[^1].Value("owner")));
        Assert.Equal(
            "the agent's turn failed with the quest still taken: The `stub` account it ran on hit its weekly limit and is cooling "
            + $"until {CoolingWords.When(until, Zone)}, as the agent said, and nothing starts on it until then.",
            conclusion.Note);
        Assert.Equal(
            "Oct 3, 11:02 (America/New_York)", CoolingWords.When(until, TimeZoneInfo.FindSystemTimeZoneById("America/New_York")));
        Assert.DoesNotContain(travelled, part => part.Words is { } words && words.Contains("4pm", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("stopped")]
    [InlineData("stood-down")]
    [InlineData("awaiting-person")]
    public void An_ending_that_is_not_a_failure_is_never_a_limit(string state)
    {
        using var service = _ledger.Client();
        var roster = Roster();

        var (conclusion, limit) = Driver(service, roster).AccountLimited(
            new SessionConclusion(state, "it ended."), AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null),
            Refusal, "s1", used: null);

        Assert.False(limit);
        Assert.Equal("it ended.", conclusion.Note);
        Assert.Empty(AccountCooling.Read(_home, _now));
    }

    [Fact]
    public void A_failure_the_table_does_not_recognise_is_a_failure_as_today()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        var failed = new SessionConclusion("failed", "the agent's turn failed: Internal error: Overloaded");

        var (conclusion, limit) = Driver(service, roster).AccountLimited(
            failed, AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null),
            "the ACP agent refused the call: Internal error: Overloaded", "s1", used: null);

        Assert.False(limit);
        Assert.Equal(failed, conclusion);
        Assert.Empty(AccountCooling.Read(_home, _now));
    }

    [Fact]
    public void The_turn_a_limit_refused_is_the_turns_its_record_ended_plus_one()
    {
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        var driver = new Daoris.Driver.Driver(service, Config, AdapterSet.Built(), _home, harnesses: Roster(), events: events);

        driver.AccountLimited(
            new SessionConclusion("failed", "refused."), AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null),
            Refusal, "s1", used: null);

        Assert.Equal(3L, Assert.Single(lines).Data.Single(field => field.Key == "turn").Value);
    }

    /// <summary>A refusal the stub's table recognises that names no time: the default stands in for one (§2.2).</summary>
    private const string NoTime =
        "the ACP agent refused the call: Internal error: You've hit your individual spend limit · run /usage-credits to ask "
        + "your admin for a higher limit";

    /// <summary>TOOL4e: a limit that names no time cools for the machine's <c>cooloff</c>, not the constant TOOL4d left.</summary>
    [Fact]
    public void A_driven_limit_that_names_no_time_cools_for_the_machine_s_cool_off()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        var driver = new Daoris.Driver.Driver(service, Config.WithCoolOff(90), AdapterSet.Built(), _home, harnesses: roster);

        var (_, limit) = driver.AccountLimited(
            new SessionConclusion("failed", "refused."), AdapterSet.Built().Resolve("acp-stub"), new HarnessSelection(null),
            NoTime, "s1", used: null);

        Assert.True(limit);
        var cooling = roster.CoolingOf("acp-stub", null)!;
        Assert.Equal((Seen.AddMinutes(90), false), (cooling.Until, cooling.Stated));
    }

    // ——— A conversation (§2.3): its refused turn cools the account it runs on; the conversation goes on.

    [Fact]
    public void A_conversation_s_limit_that_names_no_time_cools_for_the_cool_off_it_is_handed()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        using var runner = new ChatRunner(service, AdapterSet.Built(), _home, new SessionProcesses(), harnesses: roster);

        Assert.NotNull(runner.Limited("c1", AdapterSet.Built().Resolve("acp-stub"), profile: null, NoTime, TimeSpan.FromMinutes(25)));

        Assert.Equal(Seen.AddMinutes(25), roster.CoolingOf("acp-stub", null)!.Until);
    }

    [Fact]
    public void A_conversation_s_refused_turn_cools_its_account_and_says_so_without_naming_it()
    {
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var roster = Roster();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        using var runner = new ChatRunner(service, AdapterSet.Built(), _home, new SessionProcesses(), harnesses: roster, events: events);
        var acp = AdapterSet.Built().Resolve("acp-stub");

        var said = runner.Limited("c1", acp, profile: null, Refusal);

        var cooling = roster.CoolingOf("acp-stub", null)!;
        Assert.Equal(("c1", Until), (cooling.Session, cooling.Until));
        Assert.Equal(
            $"The `stub` account this conversation runs on hit its weekly limit and is cooling until Oct 3, 16:02 ({Zone.Id}), "
            + "as the agent said, and nothing new starts on it until then.",
            said!.Note);
        var line = Assert.Single(lines);
        Assert.Equal("account.limited", line.Event);
        Assert.Equal("c1", line.Data.Single(f => f.Key == "session").Value);
        Assert.Equal(2L, line.Data.Single(f => f.Key == "turn").Value);

        Assert.Null(runner.Limited("c1", acp, profile: null, "the ACP agent refused the call: Internal error: Overloaded"));
        Assert.Single(lines);
    }

    /// <summary>
    /// CONVNOTE1b (LANG1a; D125's TOOL4d note): a conversation's cooling note carries its line's code and values, as a session
    /// record's cooling note does, so the page words it in the reader's language rather than showing the driver's English.
    /// Its English stays the conversation's own sentence, and neither names the account (D125 §3.6).
    /// </summary>
    [Fact]
    public void A_conversation_s_cooling_note_carries_its_code_so_the_page_words_it_in_either_language()
    {
        using var service = _ledger.Client();
        using var runner = new ChatRunner(service, AdapterSet.Built(), _home, new SessionProcesses(), harnesses: Roster());

        var said = runner.Limited("c1", AdapterSet.Built().Resolve("acp-stub"), "account-1", Refusal)!;

        NoteAssert.Holds(said);
        var line = Assert.Single(said.Parts);
        Assert.Equal(("account.cooling", said.Note), (line.Code, line.Text));
        Assert.Equal(("2026-10-03T10:17:00Z", CoolingWhy.Stated), ((string?)line.Value("until"), (string?)line.Value("why")));
        // AGT3d: whose account and the kind of limit, as the session record's line carries them.
        Assert.Equal(["until", "why", "owner", "window"], line.Values.Select(value => value.Key));
        Assert.Equal(("stub", "weekly"), ((string?)line.Value("owner"), (string?)line.Value("window")));
        Assert.DoesNotContain(line.Values, value => value.Value is string text && text.Contains("account-1", StringComparison.Ordinal));
        Assert.DoesNotContain("account-1", said.Note);
    }

    // ——— The hold (§4): a carry-on on a cooling account is held at spawn, waits, is never parked, and is said once.

    [Fact]
    public async Task A_carry_on_on_a_cooling_account_is_held_with_no_record_and_waits_unparked()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        var cut = await CutOffAsync(service);
        roster.Limited("acp-stub", null, Refusal, cut);

        var look = await Driver(service, roster).TickAsync().WaitAsync(Bound);

        var sitting = Assert.Single(look.Considerations);
        Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
        Assert.Equal(CoolingWords.Hold(roster.CoolingOf("acp-stub", null)!, Zone), sitting.Reason);
        Assert.False(look.Progressed);
        Assert.Single(_ledger.Sessions);
        Assert.Contains(look.Events, line => line.StartsWith("held  #q1 → engine: ", StringComparison.Ordinal));

        var wait = Assert.Single(look.Waits);
        Assert.Equal(("acp-stub", "stub", (string?)null, "default", Until), (wait.Adapter, wait.Agent, wait.Account, wait.Workspace, wait.Until));
        Assert.Equal(["q1"], wait.Quests);
        Assert.Equal(["engine"], wait.Repositories);
        Assert.Equal(sitting.Reason, wait.Sentence);
    }

    [Fact]
    public async Task Starts_waiting_is_written_once_per_wait_however_many_looks_it_lasts()
    {
        using var service = _ledger.Client();
        var lines = new List<AccountLine>();
        service.AccountLined += lines.Add;
        var roster = Roster();
        var cut = await CutOffAsync(service);
        roster.Limited("acp-stub", null, Refusal, cut);

        for (var look = 0; look < 3; look++) await Driver(service, roster).TickAsync().WaitAsync(Bound);

        var line = Assert.Single(lines, l => l.Event == "starts.waiting");
        Assert.Equal(["adapter", "account", "workspace", "until", "quests", "signedOut"], line.Data.Select(field => field.Key));
        Assert.Equal(
            ["acp-stub", null, "default", "2026-10-03T10:17:00Z", 1, null],
            line.Data.Select(field => field.Value));

        // A new cool-off on the same account is a new wait.
        roster.Limited("acp-stub", null, Refusal.Replace("Oct 3, 4pm", "Oct 4, 4pm"), cut);
        await Driver(service, roster).TickAsync().WaitAsync(Bound);
        Assert.Equal(2, lines.Count(l => l.Event == "starts.waiting"));
    }

    /// <summary>
    /// Driven on the pipe stub, which shares the stub's own sign-in and so its cool-off: past the hold, its start goes on
    /// to the probe, and the stub with no command names nothing to run, which is a hold of its own with nothing spawned.
    /// What matters is that the cool-off no longer holds it.
    /// </summary>
    [Fact]
    public async Task The_quest_starts_again_at_the_reset_and_a_cool_off_ended_early_lets_it_go_sooner()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        var cut = await CutOffAsync(service);
        roster.Limited("acp-stub", null, Refusal, cut);

        var held = await Driver(service, roster, "stub").TickAsync().WaitAsync(Bound);
        Assert.Single(held.Waits);

        Assert.True(roster.Ready("stub", null));
        var sooner = await Driver(service, roster, "stub").TickAsync().WaitAsync(Bound);
        Assert.Empty(sooner.Waits);
        Assert.Contains("`stub` is not installed", Assert.Single(sooner.Considerations).Reason);

        roster.Limited("acp-stub", null, Refusal, cut);
        _now = Until.AddMinutes(1);
        var reset = await Driver(service, roster, "stub").TickAsync().WaitAsync(Bound);
        Assert.Empty(reset.Waits);
        Assert.Contains("`stub` is not installed", Assert.Single(reset.Considerations).Reason);
        Assert.Single(_ledger.Sessions);
    }

    [Fact]
    public async Task Three_limit_cut_offs_never_park_the_quest()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        var cut = await CutOffAsync(service);
        for (var more = 0; more < 2; more++)
        {
            var (id, _) = await service.OpenSessionAsync("q1", "acp-stub", null, null, Path.Combine(_home, "engine"), null);
            await service.AdvanceAsync(id!, "failed", note: Refusal, limit: true);
        }

        roster.Limited("acp-stub", null, Refusal, cut);
        var look = await Driver(service, roster).TickAsync().WaitAsync(Bound);

        Assert.DoesNotContain(look.Considerations, c => c.Verdict == StartVerdict.Exhausted);
        Assert.Equal(StartVerdict.Blocked, Assert.Single(look.Considerations).Verdict);
    }

    [Fact]
    public async Task A_quest_parked_on_its_strikes_before_this_stays_parked_behind_retry()
    {
        using var service = _ledger.Client();
        var roster = Roster();
        await CutOffAsync(service, limit: false);

        var look = await Driver(service, roster).TickAsync().WaitAsync(Bound);

        Assert.Equal(StartVerdict.Exhausted, Assert.Single(look.Considerations).Verdict);
        Assert.Empty(look.Waits);
    }

    // ——— Said once (§4): the attention watch and the log.

    [Fact]
    public void A_wait_is_said_once_when_it_first_appears()
    {
        var watch = new AttentionWatch();
        var wait = Wait(Until);

        Assert.Empty(watch.Observe(Look()));
        var said = watch.Observe(Look(wait));
        Assert.Empty(watch.Observe(Look(wait)));
        Assert.Empty(watch.Observe(Look()));
        Assert.Empty(watch.Observe(Look(wait)));

        var item = Assert.Single(said);
        Assert.Equal(AttentionKind.Waiting, item.Kind);
        Assert.Equal("engine — waits for an account", item.Headline);
        Assert.Equal(wait.Sentence, item.Detail);
        Assert.Equal("", item.Session);
    }

    [Fact]
    public void A_new_cool_off_on_the_same_account_is_said_again()
    {
        var watch = new AttentionWatch();
        watch.Observe(Look());
        watch.Observe(Look(Wait(Until)));

        Assert.Single(watch.Observe(Look(Wait(Until.AddDays(1)))));
    }

    [Fact]
    public void A_wait_already_there_at_the_first_look_is_a_baseline_never_a_backlog()
    {
        var watch = new AttentionWatch();

        Assert.Empty(watch.Observe(Look(Wait(Until))));
        Assert.Empty(watch.Observe(Look(Wait(Until))));
    }

    [Fact]
    public void The_log_writes_an_account_by_its_profile_name_and_nothing_else_of_it()
    {
        using var log = new MachineLog(_home, "desktop", () => _now);
        using var service = _ledger.Client();
        using var watch = new SessionLog(log, service, new SessionEvents(Path.Combine(_home, "sessions")), () => _now);
        var seen = new LimitSeen("weekly", "weekly", Until, Stated: true, AssumedZone: false);

        service.AccountSaid(AccountLine.Limited("s1", "claude-code-acp", "account-1", seen, turn: 2, used: 370_104));
        service.AccountSaid(AccountLine.Waiting("claude-code-acp", null, "work", Until, quests: 3));
        // TOOL6g: the accounts a wait passed not signed in, by name, and a wait on none cooling with no time.
        service.AccountSaid(AccountLine.Waiting("claude-code", "gmail", null, Until, quests: 2, signedOut: ["account-1", "not a name", "account-2"]));
        service.AccountSaid(AccountLine.Waiting("claude-code", null, null, until: null, quests: 1, signedOut: ["account-1"]));

        var lines = Directory.GetFiles(Path.Combine(_home, MachineLog.Folder)).SelectMany(StubFile.Lines)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToList();
        Assert.Equal(
            """{"session":"s1","adapter":"claude-code-acp","account":"account-1","hit":"weekly","window":"weekly","until":"2026-10-03T10:17:00Z","stated":true,"assumedZone":false,"turn":2,"used":370104}""",
            lines.Single(l => l.GetProperty("event").GetString() == "account.limited").GetProperty("data").GetRawText());
        Assert.Equal(
            [
                """{"adapter":"claude-code-acp","account":null,"workspace":"work","until":"2026-10-03T10:17:00Z","quests":3,"signedOut":null}""",
                """{"adapter":"claude-code","account":"gmail","workspace":null,"until":"2026-10-03T10:17:00Z","quests":2,"signedOut":"account-1,account-2"}""",
                """{"adapter":"claude-code","account":null,"workspace":null,"until":null,"quests":1,"signedOut":"account-1"}""",
            ],
            lines.Where(l => l.GetProperty("event").GetString() == "starts.waiting").Select(l => l.GetProperty("data").GetRawText()));
    }

    [Fact]
    public void A_name_that_is_not_a_profile_name_is_written_as_null()
    {
        var seen = new LimitSeen("weekly", null, Until, Stated: true, AssumedZone: false);

        var line = AccountLine.Limited("s1", "claude-code-acp", "sk-ant-api03-not a name", seen, turn: 1, used: null);

        Assert.Null(line.Data.Single(field => field.Key == "account").Value);
    }

    private static AccountWait Wait(DateTimeOffset until) =>
        new("acp-stub", "stub", null, "default", until, Stated: true, $"`stub`'s own sign-in is cooling until {until:MMM d}.")
        {
            Quests = ["q1"],
            Repositories = ["engine"],
        };

    private static TickReport Look(params AccountWait[] waits) =>
        new([], [], Progressed: false) { Waits = waits };
}
