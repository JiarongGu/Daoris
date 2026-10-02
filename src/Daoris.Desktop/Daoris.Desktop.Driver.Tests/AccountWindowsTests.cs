using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6b (D130 §5.2, §16.3 step 4, §16.4): <c>windows.json</c> keeps the weekly reset a limit tells, per agent and
/// account, and a start reads it carried forward a week at a time where the agent's maker fixes the weekly reset per
/// account (Claude, C1), and dropped at its reset where that was not read. Written and read by the driver alone (§14).
/// </summary>
public sealed class AccountWindowsTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>Observation 4's weekly reset, read and margined: 3 October, 16:02 in the test's zone.</summary>
    private static readonly DateTimeOffset Reset = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-windows-" + Guid.NewGuid().ToString("N")[..8]);

    public AccountWindowsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string File => AccountWindows.PathOf(_home);

    // ——— The carry (C1: a weekly reset stays the same time each week).

    [Theory]
    // Before it passes it is what the limit said, carried or not.
    [InlineData(-30.0, true, 0)]
    [InlineData(-30.0, false, 0)]
    // Once it passes, a week on where the maker fixes it, as many weeks as it takes; nothing where that was not read.
    [InlineData(0.0, true, 7)]
    [InlineData(1.0, true, 7)]
    [InlineData(15.0 * 24 * 60, true, 21)]
    [InlineData(0.0, false, null)]
    [InlineData(1.0, false, null)]
    public void A_weekly_reset_is_carried_a_week_at_a_time_where_the_maker_fixes_it(double minutesAfter, bool fixedWeek, int? days)
    {
        var now = Reset.AddMinutes(minutesAfter);

        Assert.Equal(days is { } weeks ? Reset.AddDays(weeks) : null, AccountWindows.Next(Reset, now, fixedWeek));
    }

    // ——— The file.

    [Fact]
    public void A_weekly_reset_a_limit_told_is_kept_per_agent_and_account_and_read_back()
    {
        AccountWindows.Told(_home, "claude-code", "account-2", Reset, Seen, "s1");

        Assert.Equal(Reset, AccountWindows.WeekOf(_home, "claude-code", "account-2", Seen, fixedWeek: true));
        Assert.Equal(Reset, AccountWindows.WeekOf(_home, "CLAUDE-CODE", "Account-2", Seen, fixedWeek: false));
        Assert.Null(AccountWindows.WeekOf(_home, "claude-code", "account-1", Seen, fixedWeek: true));
        Assert.Null(AccountWindows.WeekOf(_home, "codex", "account-2", Seen, fixedWeek: true));
        Assert.Equal(Reset.AddDays(7), AccountWindows.WeekOf(_home, "claude-code", "account-2", Reset.AddHours(1), fixedWeek: true));
        Assert.Null(AccountWindows.WeekOf(_home, "claude-code", "account-2", Reset.AddHours(1), fixedWeek: false));
    }

    [Fact]
    public void The_file_holds_names_times_and_the_session_and_keeps_what_it_has_no_field_for()
    {
        System.IO.File.WriteAllText(File, """
            {
              "claude-code": {
                "account-1": { "weekly": { "reset": "2026-09-30T10:00:00Z", "used": 0.4 }, "five_hour": { "standing": "clear" } },
                "account-2": { "weekly": { "reset": "2026-09-29T10:00:00Z", "seen": "2026-09-28T10:00:00Z" } }
              },
              "codex": { "account-1": { "weekly": { "reset": "2026-09-30T10:00:00Z" } } }
            }
            """);

        AccountWindows.Told(_home, "claude-code", "account-2", Reset, Seen, "s7");

        var text = System.IO.File.ReadAllText(File);
        Assert.DoesNotContain("\r", text);
        Assert.False(text.StartsWith('﻿'));
        var root = JsonNode.Parse(text)!.AsObject();
        Assert.Equal(
            """{"reset":"2026-10-03T10:17:00Z","seen":"2026-10-01T08:15:00Z","session":"s7"}""",
            root["claude-code"]!["account-2"]!["weekly"]!.ToJsonString());
        // Another account's window, a field this build does not read, another window and another agent: as written.
        Assert.Equal(
            """{"weekly":{"reset":"2026-09-30T10:00:00Z","used":0.4},"five_hour":{"standing":"clear"}}""",
            root["claude-code"]!["account-1"]!.ToJsonString());
        Assert.Equal("""{"account-1":{"weekly":{"reset":"2026-09-30T10:00:00Z"}}}""", root["codex"]!.ToJsonString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "claude-code": { "account-2": { "weekly": { "reset": "Oct 3" } } } }""")]
    [InlineData("""{ "claude-code": { "account-2": { "weekly": "2026-10-03T10:17:00Z" } } }""")]
    [InlineData("""{ "claude-code": { "account-2": [] } }""")]
    public void Missing_or_unreadable_is_nothing_known(string file)
    {
        Assert.Null(AccountWindows.WeekOf(_home, "claude-code", "account-2", Seen, fixedWeek: true));
        System.IO.File.WriteAllText(File, file);
        Assert.Null(AccountWindows.WeekOf(_home, "claude-code", "account-2", Seen, fixedWeek: true));
    }

    [Fact]
    public void An_unreadable_file_is_replaced_by_the_next_reset_told()
    {
        System.IO.File.WriteAllText(File, "not json");

        AccountWindows.Told(_home, "claude-code", "account-2", Reset, Seen, null);

        Assert.Equal(Reset, AccountWindows.WeekOf(_home, "claude-code", "account-2", Seen, fixedWeek: true));
    }

    // ——— Told by a limit, through the roster's reader (D125 §2): only a weekly reset the agent named.

    private sealed class Adapter(string name, HarnessToolchain? toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Pipe;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) => new("unused");
    }

    private HarnessRoster Roster() =>
        new(new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["fake"] = new Adapter("fake", new HarnessToolchain(
                Binary: ["fake"], VersionArguments: ["--version"], ProfileVariable: "FAKE_HOME", Limits: ClaudeLimits.Words,
                WeekFixed: true)),
        }), Path.Combine(_home, "harnesses.json"))
        {
            Clock = () => Seen,
            Zone = Zone,
        };

    [Fact]
    public void A_weekly_limit_the_agent_named_tells_its_account_s_weekly_reset()
    {
        var roster = Roster();

        roster.Limited("fake", "account-1", $"You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm ({Zone.Id})", "s1");
        roster.Limited("fake", "account-2", $"You've hit your weekly limit · resets Oct 3, 4pm ({Zone.Id})", "s2");

        Assert.Equal(Reset, AccountWindows.WeekOf(_home, "fake", "account-1", Seen, fixedWeek: true));
        Assert.Equal(Reset, AccountWindows.WeekOf(_home, "fake", "account-2", Seen, fixedWeek: true));
    }

    [Fact]
    public void A_five_hour_limit_a_default_and_the_tool_s_own_sign_in_tell_no_weekly_reset()
    {
        var roster = Roster();

        roster.Limited("fake", "account-1", $"You've hit your individual spend limit · … · your session limit resets 7:50am ({Zone.Id})", "s1");
        roster.Limited("fake", "account-2", "You've hit your weekly limit", "s2");
        roster.Limited("fake", null, $"You've hit your weekly limit · resets Oct 3, 4pm ({Zone.Id})", "s3");

        Assert.False(System.IO.File.Exists(File));
        // Each still cooled its account, as before.
        Assert.NotNull(roster.CoolingOf("fake", "account-1"));
        Assert.NotNull(roster.CoolingOf("fake", "account-2"));
        Assert.NotNull(roster.CoolingOf("fake", null));
    }
}
