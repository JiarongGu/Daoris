using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL6c (D130 §5.2, §6, §16.3 steps 2 and 5, as the limit-signals evidence corrects them): what an agent says about an
/// account's windows, read from its frame by the agent's table. Claude Code's <c>rate_limit_event</c> carries each window's
/// use as a fraction and its reset on every frame of an ordinary turn (evidence §1.1, §1.2), and its protocol door forwards
/// the same object as <c>usage_update._meta["_claude/rateLimit"]</c> (§3). Pure, so these tables are the whole contract:
/// which window a frame names, its standing, its use, and when an account is near or behind its week's pace.
/// </summary>
/// <remarks>
/// The frames are the evidence's (§1.1), each moment written relative to when it was seen (<c>&lt;+2.0 h&gt;</c>) and
/// placed here against this test's own moment: no account, no address and no machine time is in them.
/// </remarks>
public sealed class AccountReadingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly WindowWords Claude = new ClaudeCodeAdapter().Toolchain!.Windows!;

    /// <summary>The frame this branch's evidence recorded (§1.1), its moments placed against <paramref name="seen"/>.</summary>
    internal static string Recorded(DateTimeOffset seen) => Placed(Claude.Recorded[0].Frame, seen);

    /// <summary>A recorded frame's relative moments (<c>&lt;+2.0 h&gt;</c>) as Unix seconds after <paramref name="seen"/>.</summary>
    internal static string Placed(string frame, DateTimeOffset seen) =>
        Regex.Replace(frame, @"<\+(?<hours>[0-9.]+) h>", match =>
            (seen + TimeSpan.FromHours(double.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture)))
            .ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

    private static IReadOnlyList<WindowReading> Read(string frame) =>
        AccountReadings.Read(JsonDocument.Parse(frame).RootElement.Clone(), Claude);

    private static DateTimeOffset In(double hours) =>
        DateTimeOffset.FromUnixTimeSeconds((Now + TimeSpan.FromHours(hours)).ToUnixTimeSeconds());

    // ——— The table (D125 §1.3's rule, kept for readings, §5.2): every entry stands on a frame the evidence recorded.

    [Fact]
    public void Every_window_status_word_and_field_the_table_reads_stands_on_a_recorded_frame()
    {
        var frames = Claude.Recorded.Select(recorded => JsonDocument.Parse(Placed(recorded.Frame, Now)).RootElement.Clone()).ToList();
        string? Status(JsonElement frame) => frame.TryGetProperty("status", out var s) ? s.GetString() : null;

        foreach (var window in Claude.Windows.Keys)
        {
            Assert.Contains(frames, frame => frame.TryGetProperty("unifiedWindows", out var windows) && windows.TryGetProperty(window, out _));
        }

        foreach (var word in Claude.Clear.Concat(Claude.Near).Concat(Claude.Refused))
        {
            Assert.Contains(frames, frame => Status(frame) == word);
        }

        Assert.Contains(frames, frame => frame.TryGetProperty(Claude.Credits, out _));
        Assert.All(Claude.Recorded, recorded => Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", recorded.Seen));
        // Its own scale: a fraction from 0 to 1 (§1.2), and its windows Daoris's two words.
        Assert.Equal(1.0, Claude.Scale);
        Assert.Equal(["session", "weekly"], Claude.Windows.Values.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Claude_Code_and_the_stub_mirroring_it_declare_the_table_and_a_door_reads_its_owner_s()
    {
        var adapters = AdapterSet.Built();
        var declaring = adapters.Names
            .Where(name => adapters.Resolve(name).Toolchain is { Windows: not null })
            .Order(StringComparer.Ordinal)
            .ToArray();

        // The protocol doors run as their owners' accounts (AGT7), so their readings are their owners' tables'; Codex's
        // door forwards none of Codex's limits (evidence §4), so no table is declared for it.
        Assert.Equal(["claude-code", "stub"], declaring);
        var roster = new HarnessRoster(adapters, Path.Combine(Path.GetTempPath(), "daoris-readings-unused", "harnesses.json"));
        Assert.Same(ClaudeWindows.Words, roster.WindowsOf("claude-code-acp"));
        Assert.Same(ClaudeWindows.Words, roster.WindowsOf("acp-stub"));
        Assert.Null(roster.WindowsOf("codex-acp"));
    }

    // ——— Each window state (§1.2): the frame names its windows, their use and resets, and one standing.

    [Fact]
    public void The_recorded_frame_says_both_windows_use_and_resets_and_its_standing_on_the_window_it_names()
    {
        Assert.Equal(
            [
                new WindowReading("session", 0.88, In(2.0), "clear"),
                new WindowReading("weekly", 0.14, In(105.3)),
            ],
            Read(Recorded(Now)));
    }

    public static TheoryData<string, string, string?> Standings => new()
    {
        // { status, the window it names, standing }
        { "allowed", "five_hour", "clear" },
        { "allowed_warning", "five_hour", "near" },
        { "allowed_warning", "seven_day", "near" },
        { "rejected", "seven_day", "refused" },
        // A word the table does not read says no standing; the numbers still stand.
        { "allowed_soon", "five_hour", null },
    };

    [Theory]
    [MemberData(nameof(Standings))]
    public void Its_standing_is_the_table_s_word_for_its_status_on_the_window_it_names(string status, string named, string? standing)
    {
        var frame = Placed("""
            {"status":"STATUS","resetsAt":<+2 h>,"rateLimitType":"NAMED","isUsingOverage":false,
             "unifiedWindows":{"five_hour":{"utilization":0.5,"resetsAt":<+2 h>},"seven_day":{"utilization":0.2,"resetsAt":<+90 h>}}}
            """, Now).Replace("STATUS", status, StringComparison.Ordinal).Replace("NAMED", named, StringComparison.Ordinal);

        var readings = Read(frame);

        var window = named == "five_hour" ? "session" : "weekly";
        Assert.Equal(standing, readings.Single(reading => reading.Window == window).Standing);
        Assert.Null(readings.Single(reading => reading.Window != window).Standing);
        Assert.Equal([0.5, 0.2], readings.Select(reading => reading.Used));
    }

    [Fact]
    public void Drawing_on_usage_credits_is_said_on_the_window_it_names()
    {
        var frame = Recorded(Now).Replace("\"isUsingOverage\":false", "\"isUsingOverage\":true", StringComparison.Ordinal);

        Assert.Equal([true, false], Read(frame).Select(reading => reading.Credits));
    }

    public static TheoryData<string, string> Unread => new()
    {
        // { why, frame }
        { "not an object", "[1, 2]" },
        { "no windows and no reset", """{"status":"allowed","rateLimitType":"five_hour"}""" },
        { "a window the table does not name", """{"status":"allowed","unifiedWindows":{"seven_day_opus":{"utilization":0.3,"resetsAt":1790000000}}}""" },
        { "a window with no reset", """{"status":"allowed","unifiedWindows":{"seven_day":{"utilization":0.3}}}""" },
        { "a reset that is not a number", """{"status":"allowed","unifiedWindows":{"seven_day":{"utilization":0.3,"resetsAt":"soon"}}}""" },
        { "a window with neither use nor standing", """{"status":"allowed","rateLimitType":"five_hour","unifiedWindows":{"seven_day":{"resetsAt":1790000000}}}""" },
        { "windows that are not an object", """{"status":"allowed","unifiedWindows":[1]}""" },
        { "a use below nothing", """{"status":"allowed","unifiedWindows":{"seven_day":{"utilization":-0.1,"resetsAt":1790000000}}}""" },
    };

    [Theory]
    [MemberData(nameof(Unread))]
    public void A_frame_or_window_that_does_not_read_says_nothing(string why, string frame)
    {
        Assert.True(Read(frame).Count == 0, why);
    }

    [Fact]
    public void A_named_window_the_frame_s_windows_leave_out_is_read_from_its_own_reset_and_use()
    {
        var frame = Placed("""{"status":"rejected","resetsAt":<+30 h>,"rateLimitType":"seven_day","utilization":0.97,"unifiedWindows":{"five_hour":{"utilization":0.4,"resetsAt":<+1 h>}}}""", Now);

        Assert.Equal(
            [new WindowReading("session", 0.4, In(1)), new WindowReading("weekly", 0.97, In(30), "refused")],
            Read(frame));
    }

    // ——— Near (§6 as the evidence corrects it): the agent's word, drawing on usage credits, or a window's use at or over
    // the scope's *near*. Claude Code's `allowed` came at 88% with two hours left (evidence §1.3), so its word alone would
    // pass nothing a number says; the number, which comes on every frame, is the agent's word too.

    private static AccountSaid Said(params WindowSaid[] windows) => new(windows);

    private static WindowSaid Window(string window, double? used, string? standing = null, bool credits = false, double resetHours = 3) =>
        new(window, used, Now + TimeSpan.FromHours(resetHours), standing, credits, Now.AddMinutes(-20), "s1");

    public static TheoryData<string, int, bool> Nears => new()
    {
        // { what was said, near, whether near }
        { "session 0.88 clear", 90, false },
        { "session 0.88 clear", 85, true },
        { "session 0.90 clear", 90, true },
        { "session 0.30 clear, weekly 0.95", 90, true },
        // By the agent's word, whatever the number.
        { "session 0.10 near", 90, true },
        { "weekly 0.40 refused", 90, true },
        { "session 0.10 clear credits", 90, true },
        { "session - near", 99, true },
        { "session 0.10 clear, weekly 0.20", 50, false },
    };

    [Theory]
    [MemberData(nameof(Nears))]
    public void Near_by_the_agent_s_word_by_credits_and_by_percent(string written, int near, bool expected)
    {
        Assert.Equal(expected, AccountReadings.Near(Parse(written), near));
    }

    [Fact]
    public void Nothing_said_is_never_near_and_never_clear()
    {
        Assert.False(AccountReadings.Near(null, 50));
        Assert.Null(AccountReadings.Standing(null, 90));
        Assert.Null(AccountReadings.Behind(null, Now));
    }

    [Theory]
    [InlineData("session 0.88 clear", 90, "clear")]
    [InlineData("session 0.88 clear", 85, "near")]
    [InlineData("session 0.10 near", 90, "near")]
    [InlineData("session 0.10 clear credits", 90, "near")]
    [InlineData("weekly 0.40 refused", 90, "refused")]
    public void Its_standing_for_the_log_is_refused_near_or_clear(string written, int near, string standing)
    {
        Assert.Equal(standing, AccountReadings.Standing(Parse(written), near));
    }

    // ——— Pace (§16.3 step 5): how far the week's use is behind the share of the week already gone, from the weekly
    // window's reading; nothing where the week was not said.

    [Theory]
    // 4 days to its reset: 3 of 7 days gone.
    [InlineData(0.14, 96.0, 3.0 / 7 - 0.14)]
    [InlineData(0.70, 96.0, 3.0 / 7 - 0.70)]
    // A week just begun, and one in its last hour.
    [InlineData(0.0, 168.0, 0.0)]
    [InlineData(0.5, 1.0, 167.0 / 168 - 0.5)]
    // A reset further than a week off reads as a week just begun, never a negative share.
    [InlineData(0.1, 200.0, -0.1)]
    public void Behind_is_the_week_s_share_gone_less_its_share_used(double used, double resetHours, double behind)
    {
        var said = Said(Window("weekly", used, resetHours: resetHours));

        Assert.Equal(behind, AccountReadings.Behind(said, Now)!.Value, precision: 9);
    }

    [Fact]
    public void A_week_whose_use_was_not_said_has_no_pace()
    {
        Assert.Null(AccountReadings.Behind(Said(Window("session", 0.3)), Now));
        Assert.Null(AccountReadings.Behind(Said(Window("weekly", null, standing: "near")), Now));
    }

    /// <summary><c>session 0.88 clear, weekly 0.95</c>; <c>-</c> for no use said; <c>credits</c> drawing on usage credits.</summary>
    internal static AccountSaid Parse(string written) =>
        Said([.. written.Split(", ").Select(part =>
        {
            var words = part.Split(' ');
            var used = words[1] == "-" ? (double?)null : double.Parse(words[1], CultureInfo.InvariantCulture);
            var standing = words.Length > 2 && words[2] is "clear" or "near" or "refused" ? words[2] : null;
            return Window(words[0], used, standing, credits: words.Contains("credits"));
        })]);
}
