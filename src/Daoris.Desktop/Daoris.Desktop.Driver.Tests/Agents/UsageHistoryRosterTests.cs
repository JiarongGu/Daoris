using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// USAGECAP1a1 (D179 point 2) through the roster: every reading is kept in the usage history before <c>windows.json</c> keeps
/// the newest of each window, from both seams. A session's frame (<see cref="HarnessRoster.Said"/>, from the recorded
/// <c>rate_limit_info</c>) and an asked read (Claude's <c>get_usage</c> and Codex's <c>account/rateLimits/read</c>, from their
/// recorded answers), each with the account's epoch, the plan as read, and nothing of who signed in.
/// </summary>
/// <remarks>
/// Nothing here starts a process: an asked read is the roster's in-process stand-in (<see cref="HarnessRoster.AskingUsage"/>),
/// answering what the real reader parses from the recorded answer, under the account's lock as the real one is. Every moment
/// is the roster's clock, this test's own.
/// </remarks>
public sealed class UsageHistoryRosterTests : IDisposable
{
    private static readonly DateTimeOffset Seen = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-history-roster-" + Guid.NewGuid().ToString("N")[..8]);

    private DateTimeOffset _now = Seen;

    // What the account's agent answers when its windows are asked: the reader's parse of an answer, as the real read returns it.
    private IReadOnlyList<WindowReading>? _answer;

    public UsageHistoryRosterTests()
    {
        Directory.CreateDirectory(_home);
        File.WriteAllText(Command, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Command => Path.Combine(_home, "agent-here");

    private HarnessRoster Doors() =>
        new(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => _now };

    private static JsonElement Frame(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private HistoryRead History() => UsageHistory.Read(_home);

    // ——— A session's frame.

    [Fact]
    public void A_door_s_frame_is_kept_in_the_history_before_windows_json_keeps_its_newest_and_never_the_own_sign_in_s()
    {
        var roster = Doors();
        var frame = Frame(AccountReadingsTests.Recorded(Seen));

        Assert.NotNull(roster.Said("claude-code-acp", "account-2", frame, "s4"));
        Assert.Null(roster.Said("claude-code", null, frame, "s5"));
        Assert.Null(roster.Said("codex-acp", "account-1", frame, "s6"));

        var reset = (double hours) => DateTimeOffset.FromUnixTimeSeconds((Seen + TimeSpan.FromHours(hours)).ToUnixTimeSeconds());
        Assert.Equal(
            [
                new KeptReading("claude-code", "account-2", 1, "claude-code-acp",
                    new WindowSeen("five_hour", "session", null, 300, 0.88, 0.01, reset(2.0), UsageHistory.ResetSaid),
                    UsageHistory.SourceFrame, Seen, "s4", null, null, AccountReadings.HistoryReader),
                new KeptReading("claude-code", "account-2", 1, "claude-code-acp",
                    new WindowSeen("seven_day", "weekly", null, 10080, 0.14, 0.01, reset(105.3), UsageHistory.ResetSaid),
                    UsageHistory.SourceFrame, Seen, "s4", null, null, AccountReadings.HistoryReader),
            ],
            History().Readings);
        // The moment the history keeps is the one windows.json keeps.
        Assert.Equal(Seen, AccountWindows.SaidOf(_home, "claude-code", "account-2", Seen)!.Seen);
    }

    [Fact]
    public void A_scoped_week_a_frame_says_is_kept_in_the_history_while_windows_json_keeps_the_walk_s_two()
    {
        var frame = Frame(AccountReadingsTests.Placed("""
            {"status":"allowed","resetsAt":<+2 h>,"rateLimitType":"five_hour",
             "unifiedWindows":{"five_hour":{"utilization":0.5,"resetsAt":<+2 h>},"seven_day":{"utilization":0.2,"resetsAt":<+90 h>},
                               "seven_day_opus":{"utilization":0.4,"resetsAt":<+90 h>}}}
            """, Seen));

        Doors().Said("claude-code", "account-1", frame, "s1");

        Assert.Equal(["session", "weekly"], AccountWindows.SaidOf(_home, "claude-code", "account-1", Seen)!.Windows.Select(w => w.Window));
        Assert.Equal(
            [("five_hour", null), ("seven_day", null), ("seven_day_opus", "opus")],
            History().Readings.Select(each => (each.Window.Window, each.Window.Scope)));
    }

    // ——— An asked read.

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private HarnessRoster Asked(UsageKind kind) =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", ProbeByPresence: true,
                Usage: new UsageQuestion("fake", ["fake-agent"], CodexUsage.Question.Recorded, kind))),
        }), DaorisHome.Entry(_home, HomeEntry.Harnesses))
        {
            Clock = () => _now,
            AskingUsage = (_, _, _) => Task.FromResult(_answer),
        };

    private Task Read(HarnessRoster roster, params string[] accounts)
    {
        foreach (var name in accounts) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "fake", name));
        return roster.ReadUsageAsync("fake", "fake", accounts, DriverConfig.Empty with { Adapter = "fake" });
    }

    private static string At(double hours) => (Seen + TimeSpan.FromHours(hours)).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffffzzz", CultureInfo.InvariantCulture);

    /// <summary>A recorded answer, its placeholders filled in order.</summary>
    private static string Placed(string recorded, string placeholder, params string[] values)
    {
        foreach (var value in values)
        {
            var at = recorded.IndexOf(placeholder, StringComparison.Ordinal);
            recorded = recorded[..at] + value + recorded[(at + placeholder.Length)..];
        }

        return recorded;
    }

    private static string ClaudeAnswer(string? plan = null)
    {
        var body = Placed(ClaudeUsage.Question.Recorded[0].Frame, "<reset>", At(3.5), At(100.25));
        return plan is null ? body : body.Replace("\"subscription_type\":\"team\"", $"\"subscription_type\":\"{plan}\"", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_claude_read_s_answer_is_kept_in_the_history_with_its_plan_and_a_made_up_reset_marked()
    {
        // The recorded answer, its five-hour window not yet opened: no reset at 0%.
        var body = Placed(ClaudeUsage.Question.Recorded[0].Frame, "<reset>", At(3.5), At(100.25))
            .Replace($"\"utilization\":23,\"resets_at\":\"{At(3.5)}\"", "\"utilization\":0,\"resets_at\":null", StringComparison.Ordinal);
        _answer = ClaudeUsage.Read(Frame(body), _now);

        await Read(Asked(UsageKind.Claude), "account-1");

        var kept = History().Readings;
        Assert.Equal(
            [
                ("five_hour", (DateTimeOffset?)(Seen + ClaudeUsage.SessionLength), UsageHistory.ResetMadeUp),
                ("seven_day", (DateTimeOffset?)DateTimeOffset.Parse(At(100.25), CultureInfo.InvariantCulture), UsageHistory.ResetSaid),
            ],
            kept.Select(each => (each.Window.Window, each.Window.Reset, each.Window.ResetFrom!)));
        Assert.All(kept, each =>
        {
            Assert.Equal(("fake", "account-1", 1, "fake", "get_usage", null), (each.Agent, each.Account, each.Epoch!.Value, each.Door, each.Source, each.Session));
            Assert.Equal(("team", "get_usage", ClaudeUsage.HistoryReader), (each.Plan!, each.PlanFrom!, each.Reader));
            Assert.Equal(Seen, each.Seen);
        });
        // windows.json still keeps the walk's readings, as asked.
        Assert.Equal("read", AccountWindows.SaidOf(_home, "fake", "account-1", Seen)!.Of("session")!.Source);
    }

    [Fact]
    public async Task A_codex_read_s_answer_is_kept_in_the_history_by_its_windows_lengths_and_its_limit_never_its_plan()
    {
        var seconds = (double hours) => (Seen + TimeSpan.FromHours(hours)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        _answer = CodexUsage.Read(Frame(Placed(CodexUsage.Question.Recorded[0].Frame, "<reset>", seconds(3.5), seconds(100.25))));

        await Read(Asked(UsageKind.Codex), "account-1");

        Assert.Equal(
            [("300-minute", "session", "codex", 0.01), ("10080-minute", "weekly", "codex", 0.15)],
            History().Readings.Select(each => (each.Window.Window, each.Window.Walk!, each.Window.Scope!, each.Window.Used!.Value)));
        Assert.All(History().Readings, each => Assert.Equal((CodexUsage.Method, (string?)null), (each.Source, each.Plan)));
    }

    // ——— The epoch.

    [Fact]
    public async Task Another_plan_as_read_begins_the_account_s_next_epoch()
    {
        var roster = Asked(UsageKind.Claude);
        _answer = ClaudeUsage.Read(Frame(ClaudeAnswer()), _now);
        await Read(roster, "account-1");

        _now += TimeSpan.FromMinutes(10);
        _answer = ClaudeUsage.Read(Frame(ClaudeAnswer("max")), _now);
        await Read(roster, "account-1");

        Assert.Equal([(1, "team"), (1, "team"), (2, "max"), (2, "max")], History().Readings.Select(each => (each.Epoch!.Value, each.Plan!)));
        Assert.Equal(2, UsageHistory.EpochOf(_home, "fake", "account-1"));
    }

    [Fact]
    public void A_sign_out_and_a_reading_after_it_begin_the_next_epoch_as_an_account_removed_and_made_again_does()
    {
        var roster = Doors();
        var frame = Frame(AccountReadingsTests.Recorded(Seen));
        roster.Said("claude-code-acp", "account-1", frame, "s1");

        roster.SignedOut("claude-code-acp", "account-1");
        _now += TimeSpan.FromMinutes(1);
        roster.Said("claude-code-acp", "account-1", frame, "s2");
        Assert.Equal(2, UsageHistory.EpochOf(_home, "claude-code", "account-1"));

        roster.Removed("claude-code", "account-1");
        _now += TimeSpan.FromMinutes(1);
        roster.Said("claude-code-acp", "account-1", frame, "s3");

        Assert.Equal([1, 1, 2, 2, 3, 3], History().Readings.Select(each => each.Epoch!.Value));
    }

    // ——— What is never written (D179 point 2): a prompt, a word, a raw frame, a key, an email, an organisation, who signed in.

    [Fact]
    public async Task No_email_organisation_key_or_words_reach_the_history_from_either_seam()
    {
        const string Email = "someone@example.com", Org = "Acme Holdings", Key = "sk-ant-api03-secret", Words = "the agent said this";
        var frame = Frame(AccountReadingsTests.Placed($$$"""
            {"status":"{{{Words}}}","resetsAt":<+2 h>,"rateLimitType":"{{{Words}}}","email":"{{{Email}}}","organization":"{{{Org}}}","apiKey":"{{{Key}}}",
             "message":"{{{Words}}}","isUsingOverage":false,
             "unifiedWindows":{"five_hour":{"utilization":0.5,"resetsAt":<+2 h>,"note":"{{{Words}}}"},
                               "seven_day_{{{Email}}}":{"utilization":0.2,"resetsAt":<+90 h>},
                               "{{{Org}}}":{"utilization":0.3,"resetsAt":<+90 h>} } }
            """, Seen));
        Doors().Said("claude-code-acp", "account-1", frame, "s1");

        var body = ClaudeAnswer(Email)
            .Replace("\"limits\":[]", $$"""
                "limits":[{"kind":"weekly_scoped","scope":"{{Org}}","percent":40,"resets_at":"{{At(9)}}","severity":"{{Words}}"},
                          {"kind":"{{Email}}","percent":10}],
                "account":{"email_address":"{{Email}}","organization_name":"{{Org}}","api_key":"{{Key}}"}
                """, StringComparison.Ordinal);
        _answer = ClaudeUsage.Read(Frame(body), _now);
        await Read(Asked(UsageKind.Claude), "account-1");

        var codex = Placed(CodexUsage.Question.Recorded[0].Frame, "<reset>", "1790000000", "1790000000")
            .Replace("\"limitId\":\"codex\"", $"\"limitId\":\"{Email}\",\"accountId\":\"{Key}\",\"planType\":\"{Org}\"", StringComparison.Ordinal);
        _now += TimeSpan.FromMinutes(10);
        _answer = CodexUsage.Read(Frame(codex));
        await Read(Asked(UsageKind.Codex), "account-2");

        var text = string.Concat(Directory.EnumerateFiles(UsageHistory.FolderOf(_home)).Select(File.ReadAllText));
        Assert.NotEmpty(History().Readings);
        foreach (var needle in new[] { Email, "@", Org, "Acme", Key, "sk-ant", Words, "agent said", "email", "organization", "apiKey" })
        {
            Assert.DoesNotContain(needle, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
