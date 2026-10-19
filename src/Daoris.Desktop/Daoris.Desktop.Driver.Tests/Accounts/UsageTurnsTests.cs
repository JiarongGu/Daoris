using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// USAGECAP1a2 (D179 point 2): every turn's tokens kept in the usage history beside the readings, with the model. What a turn
/// carries, a turn replayed twice written once, the bound the readings' log shares, the account its start chose and its epoch,
/// a session no start bound, the model a door's split agrees on (a helper's tokens outside the total, a split read as a running
/// total, a model switched mid-session), a cancelled turn, Codex's report unknown, and nothing of anyone's words.
/// </summary>
/// <remarks>
/// Every moment is this test's own: the writer and the watch are handed their clock, and each event its stamp, so no margin of
/// real time is asked of them. Nothing here starts a process.
/// </remarks>
public sealed class UsageTurnsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private const string Opus = "claude-opus-4-1", Sonnet = "claude-sonnet-4-5", Haiku = "claude-haiku-3-5";

    private static readonly TurnBinding Claude = new("claude-code", "account-1", "claude-code-acp", UsageHistory.PerTurn);

    private static readonly TurnTokens Total = new(Input: 4, Output: 88, CacheRead: 48666, CacheWrite: 20035);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-turns-" + Guid.NewGuid().ToString("N")[..8]);

    public UsageTurnsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Segment(string name) => DaorisHome.Entry(_home, HomeEntry.UsageHistory, name);

    private string[] Lines(string name = "turns-2026-10.jsonl") =>
        File.ReadAllText(Segment(name)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static TurnSeen Seen(
        long seq = 7, string session = "3f9c2a71", TurnTokens? tokens = null, TurnModels? models = null, bool cancelled = false,
        DateTimeOffset? started = null, DateTimeOffset? ended = null) =>
        new(session, seq, started, ended ?? Now, tokens, models, cancelled);

    private SessionEvents Watched() => UsageTurns.Watched(_home, new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), () => Now);

    // ——— A turn.

    [Fact]
    public void A_turn_is_one_line_of_every_field_a_null_written_as_null_and_it_reads_back_whole()
    {
        var models = new TurnModels(Opus, [new ModelTokens(Haiku, new(909, 12, null, null)), new ModelTokens(Opus, Total)], false, TurnModels.FromQuota);
        UsageHistory.Turned(_home, Claude, Seen(tokens: Total, models: models, started: Now.AddMinutes(-2)), Now);
        // A session no start bound, whose turn said no tokens and no model: every field there, each null.
        UsageHistory.Turned(_home, null, Seen(seq: 9, session: "9b1e", cancelled: true), Now);

        Assert.Equal("turns-2026-10.jsonl", UsageHistory.TurnsSegmentOf(Now));
        var bytes = File.ReadAllBytes(Segment("turns-2026-10.jsonl"));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no byte-order mark");
        Assert.DoesNotContain((byte)'\r', bytes);
        var lines = Lines();
        Assert.Equal(2, lines.Length);

        var first = JsonNode.Parse(lines[0])!.AsObject();
        Assert.Equal(
            [
                "v", "kind", "id", "written", "agent", "account", "epoch", "door", "session", "seq", "started", "ended", "input", "output",
                "cacheRead", "cacheWrite", "model", "split", "attribution", "whole", "cancelled", "meaning", "reader",
            ],
            first.Select(pair => pair.Key));
        Assert.Equal(1, (int)first["v"]!);
        Assert.Equal("turn", (string)first["kind"]!);
        Assert.Matches("^[0-9a-f]{32}$", (string)first["id"]!);
        Assert.Equal("2026-10-19T11:58:00.000Z", (string)first["started"]!);
        Assert.Equal(
            ["model", "input", "output", "cacheRead", "cacheWrite"],
            first["split"]!.AsArray()[0]!.AsObject().Select(pair => pair.Key));

        // Absent is never zero (D57): no account, no tokens and no model say null, never 0 or "".
        var second = JsonNode.Parse(lines[1])!.AsObject();
        foreach (var name in new[] { "agent", "account", "epoch", "door", "started", "input", "output", "cacheRead", "cacheWrite", "split", "attribution", "whole" })
        {
            Assert.True(second.ContainsKey(name), name);
            Assert.Null(second[name]);
        }

        Assert.Equal(TurnModels.Unknown, (string)second["model"]!);
        Assert.Equal(UsageHistory.MeaningUnknown, (string)second["meaning"]!);

        var read = UsageHistory.ReadTurns(_home);
        Assert.Empty(read.Gaps);
        Assert.Equal(0, read.Unread);
        Assert.Equal(2, read.Turns.Count);
        var kept = read.Turns[0];
        Assert.Equal(
            ("claude-code", "account-1", (int?)1, "claude-code-acp", "3f9c2a71", 7L, (DateTimeOffset?)Now.AddMinutes(-2), Now),
            (kept.Agent, kept.Account, kept.Epoch, kept.Door, kept.Session, kept.Seq, kept.Started, kept.Ended));
        Assert.Equal(Total, kept.Tokens);
        Assert.Equal((Opus, (bool?)false, TurnModels.FromQuota, false, UsageHistory.PerTurn, UsageHistory.TurnReader),
            (kept.Model, kept.Whole, kept.Attribution, kept.Cancelled, kept.Meaning, kept.Reader));
        Assert.Equal([(Haiku, new TurnTokens(909, 12, null, null)), (Opus, Total)], kept.Split!.Select(each => (each.Model, each.Tokens)));

        var bare = read.Turns[1];
        Assert.Equal(
            (null, null, null, null, null, new TurnTokens(null, null, null, null), TurnModels.Unknown, null, true, UsageHistory.MeaningUnknown),
            (bare.Agent, bare.Account, bare.Epoch, bare.Door, bare.Started, bare.Tokens, bare.Model, bare.Split, bare.Cancelled, bare.Meaning));
    }

    [Fact]
    public void A_turn_replayed_twice_is_written_once_and_another_event_of_its_session_is_another_turn()
    {
        UsageHistory.Turned(_home, Claude, Seen(tokens: Total), Now);
        UsageHistory.Turned(_home, Claude, Seen(tokens: Total), Now.AddSeconds(5));
        UsageHistory.Turned(_home, Claude, Seen(seq: 8, tokens: Total), Now.AddSeconds(5));
        UsageHistory.Turned(_home, Claude, Seen(session: "other", tokens: Total), Now.AddSeconds(5));

        Assert.Equal([("3f9c2a71", 7L), ("3f9c2a71", 8L), ("other", 7L)], UsageHistory.ReadTurns(_home).Turns.Select(each => (each.Session, each.Seq)));
    }

    // ——— The bound: the readings' own, the two logs together.

    [Fact]
    public void The_turns_share_the_readings_bound_the_oldest_month_pruned_from_both_and_each_gap_said_in_its_own_log()
    {
        Directory.CreateDirectory(UsageHistory.FolderOf(_home));
        File.WriteAllText(Segment("readings-2026-08.jsonl"), new string('x', 700) + "\n");
        File.WriteAllText(Segment("turns-2026-08.jsonl"), new string('x', 700) + "\n");
        File.WriteAllText(Segment("turns-2026-09.jsonl"), new string('x', 700) + "\n");
        // A month that ended ninety days ago goes whatever the size, from either log.
        File.WriteAllText(Segment("readings-2026-06.jsonl"), "");

        UsageHistory.Turned(_home, Claude, Seen(tokens: Total), Now, cap: 2000);

        Assert.False(File.Exists(Segment("readings-2026-06.jsonl")));
        Assert.False(File.Exists(Segment("readings-2026-08.jsonl")));
        Assert.False(File.Exists(Segment("turns-2026-08.jsonl")));
        Assert.True(File.Exists(Segment("turns-2026-09.jsonl")));
        var total = Directory.EnumerateFiles(UsageHistory.FolderOf(_home), "*.jsonl").Sum(path => new FileInfo(path).Length);
        Assert.InRange(total, 0, 2000);

        var turns = UsageHistory.ReadTurns(_home);
        Assert.Single(turns.Turns);
        Assert.Equal(1, turns.Unread);
        Assert.Equal(
            [("size", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), "turns-2026-08.jsonl")],
            turns.Gaps.Select(gap => (gap.Why, gap.From, gap.To, string.Join(',', gap.Segments))));
        var readings = UsageHistory.Read(_home);
        Assert.Empty(readings.Readings);
        Assert.Equal(
            [
                ("age", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), "readings-2026-06.jsonl"),
                ("size", new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), "readings-2026-08.jsonl"),
            ],
            readings.Gaps.Select(gap => (gap.Why, gap.From, gap.To, string.Join(',', gap.Segments))));

        // A reading's write counts the turns too: the month being written is never pruned, of either log, and alone at the cap
        // the reading's own log is marked full.
        UsageHistory.Kept(_home, new ReadingFrom("claude-code", "account-1", "claude-code-acp", UsageHistory.SourceFrame, 1, Now),
            [new WindowSeen("seven_day", "weekly", null, 10080, 0.14, 0.01, Now.AddHours(100), UsageHistory.ResetSaid)], Now, cap: 1000);
        Assert.True(File.Exists(Segment("turns-2026-10.jsonl")));
        Assert.Contains(UsageHistory.Read(_home).Gaps, gap => gap.Why == "full");
    }

    // ——— The account and its epoch.

    [Fact]
    public void A_turn_carries_its_account_s_epoch_begins_one_the_history_never_met_and_the_next_after_a_sign_out()
    {
        UsageHistory.Turned(_home, Claude, Seen(seq: 1, tokens: Total), Now);
        Assert.Equal(1, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        UsageHistory.Login(_home, "claude-code", "account-1", LoginState.Out, null, Now.AddMinutes(1));
        UsageHistory.Turned(_home, Claude, Seen(seq: 2, tokens: Total, ended: Now.AddMinutes(2)), Now.AddMinutes(2));
        UsageHistory.Turned(_home, null, Seen(seq: 3, session: "unbound"), Now.AddMinutes(3));

        Assert.Equal([(int?)1, 2, null], UsageHistory.ReadTurns(_home).Turns.Select(each => each.Epoch));
        Assert.Equal(2, UsageHistory.EpochOf(_home, "claude-code", "account-1"));
    }

    // ——— The watch: a session's record, its account as its start chose it.

    [Fact]
    public void A_session_s_turns_are_kept_under_the_account_its_start_chose_from_its_ask_to_its_end_and_watched_once()
    {
        var events = Watched();
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty, adapters, _home, events: events);
        _ = driver.Said(adapters.Resolve("claude-code-acp"), new HarnessSelection(null, Profile: "account-1"), "s1");

        // The words shown the moment they are said are no ask (MSG1d): the turn begins where the session took them.
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "go on", Reaches = "turn-end", At = Now.AddMinutes(-3) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "go on", At = Now.AddMinutes(-2) });
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "done", At = Now.AddMinutes(-1) });
        var models = new TurnModels(Opus, [new ModelTokens(Opus, Total)], true, TurnModels.FromQuota);
        var ended = events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", Tokens = Total, Models = models, At = Now });
        // A turn's end with no ask before it in this process: when it began is not known.
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled", At = Now.AddMinutes(1) });

        // Another look's driver on the same record watches it once: one turn is one line.
        _ = new Daoris.Driver.Driver(new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty, adapters, _home, events: events);
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", Tokens = Total, At = Now.AddMinutes(2) });

        var turns = UsageHistory.ReadTurns(_home).Turns;
        Assert.Equal(3, turns.Count);
        var first = turns[0];
        Assert.Equal(
            ("claude-code", "account-1", (int?)1, "claude-code-acp", "s1", ended.Seq, (DateTimeOffset?)Now.AddMinutes(-2), Now, Opus, false, UsageHistory.PerTurn),
            (first.Agent, first.Account, first.Epoch, first.Door, first.Session, first.Seq, first.Started, first.Ended, first.Model, first.Cancelled, first.Meaning));
        Assert.Equal(Total, first.Tokens);
        Assert.True(first.Whole);

        var stopped = turns[1];
        Assert.Equal((null, true, new TurnTokens(null, null, null, null), TurnModels.Unknown), (stopped.Started, stopped.Cancelled, stopped.Tokens, stopped.Model));
    }

    [Fact]
    public void A_brainstorm_pass_s_turn_is_kept_under_the_account_its_walk_chose()
    {
        // USAGECAP1a4: the pass's session id is brainstorm-<id>-<turn>; its start binds it as any other driven start does.
        var events = Watched();
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(new ServiceClient("http://127.0.0.1:9", null), DriverConfig.Empty, adapters, _home, events: events);
        _ = driver.Said(adapters.Resolve("claude-code-acp"), new HarnessSelection(null, Profile: "account-1"), "brainstorm-b1-1");

        events.Append("brainstorm-b1-1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "the pass", At = Now.AddMinutes(-1) });
        events.Append("brainstorm-b1-1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", Tokens = Total, At = Now });

        var turn = Assert.Single(UsageHistory.ReadTurns(_home).Turns);
        Assert.Equal(("claude-code", "account-1", "brainstorm-b1-1"), (turn.Agent, turn.Account, turn.Session));
    }

    [Fact]
    public void A_turn_of_a_session_no_start_bound_is_kept_with_no_account()
    {
        var events = Watched();

        events.Append("s9", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "the quest", At = Now.AddMinutes(-1) });
        events.Append("s9", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn", Tokens = Total, At = Now });

        var turn = Assert.Single(UsageHistory.ReadTurns(_home).Turns);
        Assert.Equal(
            (null, null, null, null, "s9", (DateTimeOffset?)Now.AddMinutes(-1), UsageHistory.MeaningUnknown),
            (turn.Agent, turn.Account, turn.Epoch, turn.Door, turn.Session, turn.Started, turn.Meaning));
        Assert.Equal(Total, turn.Tokens);
    }

    private sealed class Bare(string name) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public HarnessToolchain? Toolchain => null;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    [Fact]
    public void Each_door_s_turn_report_means_per_turn_where_measured_and_codex_s_unknown_until_a_frame_is_recorded()
    {
        var adapters = AdapterSet.Built();

        Assert.Equal(
            new TurnBinding("claude-code", "account-1", "claude-code", UsageHistory.PerTurn),
            TurnBinding.Of(adapters.Resolve("claude-code"), "account-1"));
        Assert.Equal(
            new TurnBinding("claude-code", "account-1", "claude-code-acp", UsageHistory.PerTurn),
            TurnBinding.Of(adapters.Resolve("claude-code-acp"), "account-1"));
        Assert.Equal(
            new TurnBinding(CodexAccounts.Agent, "account-2", "codex-acp", UsageHistory.MeaningUnknown),
            TurnBinding.Of(adapters.Resolve("codex-acp"), "account-2"));
        // The tool's own sign-in is named as its readings name it; an agent with no accounts has none to name.
        Assert.Equal(AccountWindows.Own, TurnBinding.Of(adapters.Resolve("claude-code"), null).Account);
        Assert.Equal(new TurnBinding(null, null, "bare", UsageHistory.MeaningUnknown), TurnBinding.Of(new Bare("bare"), "account-1"));
    }

    // ——— The model: what the door's split agrees on with the turn's total.

    [Fact]
    public void A_split_that_agrees_with_the_total_names_its_model_and_a_helper_s_tokens_outside_the_total_make_the_turn_not_whole()
    {
        var helper = new TurnTokens(909, 30, null, null);
        var reported = new Dictionary<string, TurnTokens> { [Opus] = Total, [Haiku] = helper };

        var models = TurnModels.Of(Total, [Opus], TurnModels.FromUpdates, reported, null, TurnModels.FromQuota);

        Assert.Equal((Opus, (bool?)false, TurnModels.FromQuota), (models.Model, models.Whole, models.From));
        Assert.Equal([(Haiku, helper), (Opus, Total)], models.Split!.Select(each => (each.Model, each.Tokens)));

        // Named by none of its answers, the one model whose share is the turn's total is the turn's.
        var unnamed = TurnModels.Of(Total, [], TurnModels.FromUpdates, reported, null, TurnModels.FromQuota);
        Assert.Equal((Opus, (bool?)false), (unnamed.Model, unnamed.Whole));
        // With no helper beside it, the split is whole.
        Assert.True(TurnModels.Of(Total, [Opus], TurnModels.FromUpdates, new Dictionary<string, TurnTokens> { [Opus] = Total }, null, TurnModels.FromQuota).Whole);
    }

    [Fact]
    public void A_running_split_is_read_as_the_turn_s_own_less_the_one_before_and_a_model_switched_mid_session_splits_by_turn()
    {
        var first = new TurnTokens(4, 80, 51061, 16717);
        var second = new TurnTokens(2, 10, 60000, 300);
        var afterFirst = new Dictionary<string, TurnTokens> { [Opus] = first };

        // The process's running total: the turn's own is what grew since the turn before.
        var running = TurnModels.Of(
            second, [Sonnet], TurnModels.FromMessages, new Dictionary<string, TurnTokens> { [Opus] = first, [Sonnet] = second }, afterFirst,
            TurnModels.FromModelUsage);
        Assert.Equal((Sonnet, (bool?)true, TurnModels.FromModelUsage + TurnModels.Since), (running.Model, running.Whole, running.From));
        Assert.Equal([(Sonnet, second)], running.Split!.Select(each => (each.Model, each.Tokens)));

        // The turn's own as reported reads the same turn the same.
        var own = TurnModels.Of(
            second, [Sonnet], TurnModels.FromMessages, new Dictionary<string, TurnTokens> { [Sonnet] = second }, afterFirst, TurnModels.FromModelUsage);
        Assert.Equal((Sonnet, (bool?)true, TurnModels.FromModelUsage), (own.Model, own.Whole, own.From));
        Assert.Equal([(Sonnet, second)], own.Split!.Select(each => (each.Model, each.Tokens)));
    }

    [Fact]
    public void A_split_that_does_not_agree_with_the_total_is_no_split_and_the_model_its_answers_named_stands()
    {
        var off = new Dictionary<string, TurnTokens> { [Opus] = Total with { Input = 5 } };

        var named = TurnModels.Of(Total, [Opus], TurnModels.FromMessages, off, null, TurnModels.FromModelUsage);
        Assert.Equal((Opus, (IReadOnlyList<ModelTokens>?)null, (bool?)null, TurnModels.FromMessages), (named.Model, named.Split, named.Whole, named.From));

        Assert.Equal(TurnModels.Mixed, TurnModels.Of(Total, [Opus, Sonnet], TurnModels.FromMessages, null, null, TurnModels.FromModelUsage).Model);
        var none = TurnModels.Of(Total, [], TurnModels.FromMessages, off, null, TurnModels.FromModelUsage);
        Assert.Equal((TurnModels.Unknown, (string?)null), (none.Model, none.From));
        // A name that is not a plain name is no name: it never reaches the history.
        Assert.Equal(TurnModels.Unknown, TurnModels.Of(Total, ["someone@example.com"], TurnModels.FromMessages, null, null, TurnModels.FromModelUsage).Model);
        Assert.Null(TurnModels.Of(Total, [Opus], TurnModels.FromMessages, new Dictionary<string, TurnTokens> { [Opus] = Total, ["Acme Holdings"] = Total }, null, TurnModels.FromModelUsage).Split);
    }

    [Fact]
    public void A_cancelled_turn_that_reported_nothing_keeps_the_model_its_answers_named_and_no_counts()
    {
        // The protocol door's stopped turn answers every count zero (CONV5), which is no report: the total is null.
        var models = TurnModels.Of(null, [Opus], TurnModels.FromUpdates, new Dictionary<string, TurnTokens>(), null, TurnModels.FromQuota);

        Assert.Equal((Opus, (IReadOnlyList<ModelTokens>?)null, (bool?)null), (models.Model, models.Split, models.Whole));

        UsageHistory.Turned(_home, Claude, Seen(models: models, cancelled: true), Now);
        var kept = Assert.Single(UsageHistory.ReadTurns(_home).Turns);
        Assert.Equal((true, Opus, new TurnTokens(null, null, null, null)), (kept.Cancelled, kept.Model, kept.Tokens));
    }

    // ——— What is never written (D179 point 2): a prompt, a word, a raw frame, a key, an email, an organisation, who signed in.

    [Fact]
    public void No_prompt_words_email_organisation_or_key_reach_the_turns_from_a_door_s_frames()
    {
        const string Email = "someone@example.com", Org = "Acme Holdings", Key = "sk-ant-api03-secret", Words = "the agent said this";
        var events = Watched();
        UsageTurns.Bound(_home, events, "s1", TurnBinding.Of(AdapterSet.Built().Resolve("claude-code"), "account-1"));
        events.Append("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = $"{Words} {Email} {Key}", At = Now.AddMinutes(-1) });

        var mapper = new ClaudeStreamJson();
        string[] stdout =
        [
            $$$"""{"type":"system","subtype":"init","session_id":"c1","model":"{{{Email}}}","apiKeySource":"{{{Key}}}"}""",
            $$$"""{"type":"assistant","message":{"id":"msg_1","model":"{{{Opus}}}","content":[{"type":"text","text":"{{{Words}}} {{{Email}}}"},{"type":"tool_use","id":"t1","name":"Bash","input":{"command":"echo {{{Key}}}"}}],"usage":{"input_tokens":2}},"parent_tool_use_id":null}""",
            $$$"""{"type":"assistant","message":{"id":"msg_2","model":"{{{Org}}}","content":[{"type":"text","text":"{{{Org}}}"}]},"parent_tool_use_id":null}""",
            $$$"""{"type":"result","subtype":"success","is_error":false,"result":"{{{Words}}} {{{Email}}} {{{Org}}}","usage":{"input_tokens":4,"output_tokens":88,"cache_read_input_tokens":48666,"cache_creation_input_tokens":20035},"modelUsage":{"{{{Opus}}}":{"inputTokens":4,"outputTokens":88,"cacheReadInputTokens":48666,"cacheCreationInputTokens":20035},"{{{Email}}}":{"inputTokens":1,"outputTokens":1}},"organization":"{{{Org}}}","email":"{{{Email}}}"}""",
        ];
        foreach (var line in stdout)
        {
            foreach (var e in mapper.Read(line).Events) events.Append("s1", e with { At = Now });
        }

        var turn = Assert.Single(UsageHistory.ReadTurns(_home).Turns);
        Assert.Equal((Opus, Total), (turn.Model, turn.Tokens));
        var text = string.Concat(Directory.EnumerateFiles(UsageHistory.FolderOf(_home)).Select(File.ReadAllText));
        foreach (var needle in new[] { Email, "@", Org, "Acme", Key, "sk-ant", Words, "agent said", "email", "organization", "apiKey", "echo" })
        {
            Assert.DoesNotContain(needle, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
