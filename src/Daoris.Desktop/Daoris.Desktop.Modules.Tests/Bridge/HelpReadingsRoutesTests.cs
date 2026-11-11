using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The second check in the window: <c>HELP_READINGS</c> answers a conversation's readings a page at a time with the check's standing
/// and a range's packet when named; <c>HELP_READING_SEEN</c> keeps the person's <i>Seen</i> on a flag; and every flag not yet seen is a
/// row of <i>What needs you</i>, counted on the bell, gone once seen, a note never one.
/// </summary>
public sealed class HelpReadingsRoutesTests : DriverModuleBridge
{
    private static readonly DateTimeOffset At = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private void Flagged(string session, string range, int minute, params AskDaorisFinding[] findings)
    {
        var reports = new AskDaorisReports(Home);
        var report = new AskDaorisReport(range, session, AskDaorisStates.Read, At.AddMinutes(minute), [], [])
        {
            Closed = At.AddMinutes(minute + 1),
            Reader = new SupervisorAgentHolder("second-check", 2, "codex-acp", null, null, "gpt-6.1-sol", OpinionPosture.PacketOnly, "s-r") { Tokens = 1200 },
            Independence = ReadIndependence.AnotherMaker,
            Findings = findings,
        };
        reports.Write(report);
        reports.KeepPacket(report, "the packet as sent");
    }

    private static AskDaorisFinding Flag(string call) => new(AskDaorisFinding.Flag, "sure", "a push the person did not ask for", call, "w1");

    private static JsonElement Wire(object answer) => JsonSerializer.SerializeToElement(answer, Web);

    [Fact]
    public void Readings_are_answered_newest_first_with_the_check_s_standing_and_a_named_range_with_its_packet()
    {
        Flagged("s1", "r1", 1, Flag("p1"));
        Flagged("s1", "r2", 5, new AskDaorisFinding(AskDaorisFinding.Note, "unsure", "an edit beside the ask", "p2", "none"));
        new AskDaorisReports(Home).Write(new AskDaorisReport("r3", "s1", AskDaorisStates.Queued, At.AddMinutes(9), [], []));

        var page = Wire(HelpReadingsRoutes.Answer(Home, new HelpReadingsRead("s1"), At.AddMinutes(30)));
        var one = Wire(HelpReadingsRoutes.Answer(Home, new HelpReadingsRead("s1", Range: "r1"), At.AddMinutes(30)));

        Assert.Equal(["r3", "r2", "r1"], page.GetProperty("readings").EnumerateArray().Select(each => each.GetProperty("range").GetString()));
        Assert.Equal(["queued", "read", "read"], page.GetProperty("readings").EnumerateArray().Select(each => each.GetProperty("state").GetString()));
        var check = page.GetProperty("check");
        Assert.False(check.GetProperty("on").GetBoolean());
        Assert.Equal(1, check.GetProperty("pending").GetInt32());
        Assert.Equal((2, 2400L, 0), (check.GetProperty("today").GetProperty("readings").GetInt32(), check.GetProperty("today").GetProperty("tokens").GetInt64(),
            check.GetProperty("today").GetProperty("unknown").GetInt32()));
        Assert.False(page.TryGetProperty("packet", out _));
        Assert.Equal("the packet as sent", one.GetProperty("packet").GetString());
        Assert.Equal("gpt-6.1-sol", one.GetProperty("readings")[0].GetProperty("reader").GetProperty("ran").GetString());
    }

    [Fact]
    public async Task Seen_is_kept_on_its_flag_and_its_row_leaves_what_needs_you()
    {
        Flagged("s1", "r1", 1, Flag("p1"), new AskDaorisFinding(AskDaorisFinding.Note, "unsure", "a note", "p2", "none"), Flag("p3"));
        JsonObject[] Unseen() => [.. AttentionSecondCheckRows.Unseen(Home).Select(each => JsonSerializer.SerializeToNode(each, Web)!.AsObject())];

        var rows = NeedsAPerson.Rows(new AttentionFacts { SecondCheck = Unseen() }).Where(row => (string?)row["kind"] == "second-check").ToList();
        Assert.Equal(["s1#r1#0", "s1#r1#2"], rows.Select(row => (string?)row["id"]));
        Assert.Equal("s1", (string?)rows[0]["session"]);
        Assert.Null(rows[0]["workspace"]);
        Assert.Equal("a push the person did not ask for", (string?)rows[0]["secondCheck"]!["says"]);

        var seen = await AnswerAsync(Module(), "HELP_READING_SEEN", new { session = "s1", range = "r1", finding = 2 });
        var again = await AnswerAsync(Module(), "HELP_READING_SEEN", new { session = "s1", range = "r1", finding = 2 });

        Assert.True(seen.GetProperty("seen").GetBoolean());
        Assert.False(again.GetProperty("seen").GetBoolean());
        Assert.Equal(["s1#r1#0"], NeedsAPerson.Rows(new AttentionFacts { SecondCheck = Unseen() }).Where(row => (string?)row["kind"] == "second-check").Select(row => (string?)row["id"]));
    }

    [Fact]
    public void A_written_report_wakes_its_home_s_hearing_alone()
    {
        var woke = 0;
        using var hearing = AttentionSecondCheckRows.Hear(Home, () => Interlocked.Increment(ref woke));

        Flagged("s1", "r1", 1, Flag("p1"));
        new AskDaorisReports(Path.Combine(Home, "another")).Write(new AskDaorisReport("r9", "s9", AskDaorisStates.Queued, At, [], []));

        Assert.Equal(1, woke);
    }
}
