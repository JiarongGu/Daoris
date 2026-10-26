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

        foreach (var word in Claude.Clear.Concat(Claude.Warned).Concat(Claude.Refused))
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
        // Claude Code's own pace warning (USAGEREAD3a, design §1.3): kept as the agent's warning, never as near.
        { "allowed_warning", "five_hour", "warned" },
        { "allowed_warning", "seven_day", "warned" },
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

    // ——— What the usage history keeps of a frame (USAGECAP1a1, D179 point 2): a parse beside the walk's, every window the
    // frame says, by its key, its length and its scope, with the precision its number was written to.

    private static IReadOnlyList<WindowSeen> Seen(string frame) =>
        AccountReadings.Seen(JsonDocument.Parse(frame).RootElement.Clone(), Claude);

    [Fact]
    public void The_recorded_frame_is_kept_for_the_history_window_by_window_its_key_its_length_and_its_precision()
    {
        Assert.Equal(
            [
                new WindowSeen("five_hour", "session", null, 300, 0.88, 0.01, In(2.0), UsageHistory.ResetSaid),
                new WindowSeen("seven_day", "weekly", null, 10080, 0.14, 0.01, In(105.3), UsageHistory.ResetSaid),
            ],
            Seen(Recorded(Now)));
    }

    [Fact]
    public void A_scoped_week_is_kept_for_the_history_while_the_walk_s_readings_are_unchanged()
    {
        var frame = Placed("""
            {"status":"allowed","resetsAt":<+2 h>,"rateLimitType":"five_hour",
             "unifiedWindows":{"five_hour":{"utilization":0.5,"resetsAt":<+2 h>},"seven_day":{"utilization":0.2,"resetsAt":<+90 h>},
                               "seven_day_opus":{"utilization":0.375,"resetsAt":<+90 h>}}}
            """, Now);

        Assert.Equal([new WindowReading("session", 0.5, In(2), "clear"), new WindowReading("weekly", 0.2, In(90))], Read(frame));
        Assert.Equal(
            [
                new WindowSeen("five_hour", "session", null, 300, 0.5, 0.1, In(2), UsageHistory.ResetSaid),
                new WindowSeen("seven_day", "weekly", null, 10080, 0.2, 0.1, In(90), UsageHistory.ResetSaid),
                new WindowSeen("seven_day_opus", null, "opus", 10080, 0.375, 0.001, In(90), UsageHistory.ResetSaid),
            ],
            Seen(frame));
    }

    [Fact]
    public void The_history_keeps_a_named_window_the_walk_has_no_name_for_and_a_use_with_no_reset_as_null()
    {
        // The walk reads neither: a window it does not name, and one with no reset (the `Unread` rows above).
        var frame = Placed("""
            {"status":"allowed_warning","resetsAt":<+40 h>,"rateLimitType":"seven_day_sonnet","utilization":0.61,
             "unifiedWindows":{"seven_day":{"utilization":0.3},"five_hour":{"resetsAt":<+1 h>},"overage":{}}}
            """, Now);

        Assert.Empty(Read(frame));
        Assert.Equal(
            [
                new WindowSeen("seven_day", "weekly", null, 10080, 0.3, 0.1, null, null),
                new WindowSeen("five_hour", "session", null, 300, null, null, In(1), UsageHistory.ResetSaid),
                new WindowSeen("seven_day_sonnet", null, "sonnet", 10080, 0.61, 0.01, In(40), UsageHistory.ResetSaid),
            ],
            Seen(frame));
    }

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("""{"status":"allowed","rateLimitType":"five_hour"}""")]
    [InlineData("""{"status":"allowed","unifiedWindows":[1]}""")]
    [InlineData("""{"status":"allowed","unifiedWindows":{"seven_day":{"utilization":-0.1,"resetsAt":"soon"}}}""")]
    public void A_frame_that_says_no_window_s_use_or_reset_keeps_nothing_for_the_history(string frame)
    {
        Assert.Empty(Seen(frame));
    }

    // ——— Near (§6 as the evidence corrects it; D165 point 2): the agent's word that a limit was reached, drawing on usage
    // credits, or a window's use over the scope's curve now (SWITCH1b-ii, the switch design §2–§3). Claude Code's `allowed`
    // came at 88% with two hours left (evidence §1.3), so its word alone would pass nothing a number says; the number, which
    // comes on every frame, is the agent's word too. Its `allowed_warning` is its own pace warning, use ahead of the time gone
    // in a window (USAGEREAD3a, account-switch design §1.3): kept as `warned`, and never near alone. A window resets three
    // hours on unless `@<hours>` says otherwise: the session is then two fifths gone, under the smart curve's flat 90.

    private static AccountSaid Said(params WindowSaid[] windows) => new(windows);

    private static WindowSaid Window(string window, double? used, string? standing = null, bool credits = false, double resetHours = 3) =>
        new(window, used, Now + TimeSpan.FromHours(resetHours), standing, credits, Now.AddMinutes(-20), "s1");

    /// <summary>
    /// A scope's switch and curve: <c>smart</c>, <c>off</c>, or <c>curve</c> and each window the person set,
    /// <c>session=85</c> flat, <c>weekly=0:10,100:60</c> as points.
    /// </summary>
    internal static RotationUse Use(string written)
    {
        var words = written.Split(' ');
        return RotationUse.Default with { Switch = words[0], Curve = Curve(words.Skip(1)) };
    }

    /// <summary>The person's curve, a window each: <c>session=85</c> flat, <c>weekly=0:10,100:60</c> as points.</summary>
    internal static UseCurve Curve(IEnumerable<string> windows)
    {
        var points = new Dictionary<string, IReadOnlyList<CurvePoint>>(StringComparer.Ordinal);
        foreach (var window in windows)
        {
            var (name, curve) = (window[..window.IndexOf('=')], window[(window.IndexOf('=') + 1)..]);
            points[name] = curve.Contains(':')
                ? [.. curve.Split(',').Select(point => new CurvePoint(int.Parse(point.Split(':')[0]), int.Parse(point.Split(':')[1])))]
                : [new CurvePoint(0, int.Parse(curve))];
        }

        return points.Count == 0 ? UseCurve.None : new UseCurve(points);
    }

    public static TheoryData<string, string, bool> Nears => new()
    {
        // { what was said, the scope's switch and curve, whether near }
        { "session 0.88 clear", "smart", false },
        { "session 0.88 clear", "curve session=85", true },
        // On its curve is not over it; a point over is.
        { "session 0.90 clear", "smart", false },
        { "session 0.91 clear", "smart", true },
        // Three sevenths of its week gone: the smart curve stands at 63% of it.
        { "session 0.30 clear, weekly 0.70@96", "smart", true },
        { "session 0.30 clear, weekly 0.60@96", "smart", false },
        // Half its week gone: the person's straight line from 10% to 60% stands at 35%, the smart curve at 70%.
        { "weekly 0.50@84", "curve weekly=0:10,100:60", true },
        { "weekly 0.50@84", "smart", false },
        // `off` reads the smart curve: a start still passes an account over it (the switch design §3).
        { "session 0.95 clear", "off", true },
        { "session 0.88 clear", "off", false },
        // By the agent's word that a limit was reached, whatever the number; by credits.
        { "weekly 0.40 refused", "smart", true },
        { "session 0.10 clear credits", "smart", true },
        { "session 0.10 clear, weekly 0.20", "curve session=50", false },
        // Its pace warning is not near alone (USAGEREAD3a): the number decides, as it does for a clear word.
        { "session 0.10 warned", "smart", false },
        { "session - warned", "smart", false },
        { "weekly 0.60 warned", "smart", false },
        { "session 0.95 warned", "smart", true },
        // A window this build has no curve for is not measured (UsageCurves.Length): its number passes nothing.
        { "60-minute 0.99 clear", "smart", false },
    };

    [Theory]
    [MemberData(nameof(Nears))]
    public void Near_by_the_agent_s_word_by_credits_and_by_its_curve(string written, string use, bool expected)
    {
        Assert.Equal(expected, AccountReadings.Near(Parse(written), Use(use), Now));
    }

    /// <summary>
    /// USAGEREAD3a: a near account is near by a limit reached, by credits or by its number, and never by a warning word, so
    /// the page's <c>near[].by</c> and the start's reason name no word. By its number, the window named is the one with the
    /// least room under its curve (SWITCH1b-ii).
    /// </summary>
    [Fact]
    public void Near_is_by_a_limit_reached_by_credits_or_by_the_number_and_never_by_a_warning()
    {
        Assert.Equal(["Refused", "Credits", "Number"], Enum.GetNames<NearBy>());
        Assert.Equal(
            (AccountWindows.Weekly, NearBy.Number),
            AccountReadings.NearWindow(Parse("session 0.10 warned, weekly 0.95@96"), RotationUse.Default, Now) is { } near ? (near.Window.Window, near.By) : default);
        // Both over: the session by five points, the week by thirty-two.
        Assert.Equal(
            (AccountWindows.Weekly, NearBy.Number),
            AccountReadings.NearWindow(Parse("session 0.95 clear, weekly 0.95@96"), RotationUse.Default, Now) is { } both ? (both.Window.Window, both.By) : default);
    }

    [Fact]
    public void Nothing_said_is_never_near_and_never_clear()
    {
        Assert.False(AccountReadings.Near(null, RotationUse.Default, Now));
        Assert.Null(AccountReadings.Standing(null, RotationUse.Default, Now));
        Assert.Null(AccountReadings.Behind(null, Now));
    }

    [Theory]
    [InlineData("session 0.88 clear", "smart", "clear")]
    [InlineData("session 0.88 clear", "curve session=85", "near")]
    [InlineData("session 0.10 clear credits", "smart", "near")]
    [InlineData("weekly 0.40 refused", "smart", "refused")]
    // Warned and not near is its own word in the log (USAGEREAD3a); warned and near by its number is near.
    [InlineData("session 0.10 warned", "smart", "warned")]
    [InlineData("session 0.95 warned", "smart", "near")]
    // Over its curve is the log's `near` (SWITCH1b-ii): the log's word for an account passed.
    [InlineData("weekly 0.70@96", "smart", "near")]
    public void Its_standing_for_the_log_is_refused_near_warned_or_clear(string written, string use, string standing)
    {
        Assert.Equal(standing, AccountReadings.Standing(Parse(written), Use(use), Now));
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
        Assert.Null(AccountReadings.Behind(Said(Window("weekly", null, standing: "warned")), Now));
    }

    /// <summary>
    /// <c>session 0.88 clear, weekly 0.95@96</c>; <c>-</c> for no use said; <c>@&lt;hours&gt;</c> to its reset, three where
    /// none is written; <c>credits</c> drawing on usage credits.
    /// </summary>
    internal static AccountSaid Parse(string written) =>
        Said([.. written.Split(", ").Select(part =>
        {
            var words = part.Split(' ');
            var (number, hours) = words[1].Split('@') is [var used, var at] ? (used, double.Parse(at, CultureInfo.InvariantCulture)) : (words[1], 3.0);
            var share = number == "-" ? (double?)null : double.Parse(number, CultureInfo.InvariantCulture);
            var standing = words.Length > 2 && words[2] is "clear" or "warned" or "refused" ? words[2] : null;
            return Window(words[0], share, standing, credits: words.Contains("credits"), resetHours: hours);
        })]);
}
