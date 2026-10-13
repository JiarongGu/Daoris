using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>pausedAsks</c> and <c>pausedQuests</c> in <c>driver.json</c> (PAUSE1a, D132 point 5, design §2.5): each pause by its ask
/// or quest, with when it was made and the stops it made, each quest against the session it stopped. The driver's half of a
/// TWIN with the CLI's <c>driverconfig.ts</c>, whose <c>driverconfig.test.ts</c> holds the same table and parses this theory
/// to hold it to its own, cell for cell.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</para>
/// <para>A row's <c>at</c> is the moment read, in UTC to the second, and its <c>stopped</c> each quest and its session as
/// <c>quest=session</c>, sorted and joined by commas: one spelling both sides compute from what they read.</para>
/// </remarks>
public sealed class PausedWorkTests
{
    /// <summary>Absent is no pause; an object under its id is one, whose time and stops are read where they read.</summary>
    [Theory]
    [InlineData("absent is no pause", "{}", "ask", "a1", false, null, "")]
    [InlineData("an ask paused, with when and the stops its pause made", """{"pausedAsks":{"a1":{"at":"2026-10-02T14:02:11Z","stopped":{"q7":"s-1"}}}}""", "ask", "a1", true, "2026-10-02T14:02:11Z", "q7=s-1")]
    [InlineData("a quest paused that stopped nothing", """{"pausedQuests":{"q9":{"at":"2026-10-02T14:05:40Z","stopped":{}}}}""", "quest", "q9", true, "2026-10-02T14:05:40Z", "")]
    [InlineData("an ask's pause is not a quest's", """{"pausedAsks":{"a1":{"at":"2026-10-02T14:02:11Z","stopped":{}}}}""", "quest", "a1", false, null, "")]
    [InlineData("another ask's pause is not this one's", """{"pausedAsks":{"a2":{"at":"2026-10-02T14:02:11Z","stopped":{}}}}""", "ask", "a1", false, null, "")]
    [InlineData("an id is matched in any case", """{"pausedAsks":{"A1":{"at":"2026-10-02T14:02:11Z","stopped":{}}}}""", "ask", "a1", true, "2026-10-02T14:02:11Z", "")]
    [InlineData("an id written twice in any case is read where first written", """{"pausedQuests":{"q9":{"at":"2026-10-02T14:05:40Z"},"Q9":{"at":"2026-10-03T00:00:00Z"}}}""", "quest", "q9", true, "2026-10-02T14:05:40Z", "")]
    [InlineData("a time at an offset is read as its moment", """{"pausedAsks":{"a1":{"at":"2026-10-02T16:02:11+02:00"}}}""", "ask", "a1", true, "2026-10-02T14:02:11Z", "")]
    [InlineData("a time with a fraction is read to the second", """{"pausedAsks":{"a1":{"at":"2026-10-02T14:02:11.5Z"}}}""", "ask", "a1", true, "2026-10-02T14:02:11Z", "")]
    [InlineData("a time that is not ISO 8601 is unknown, and the pause still holds", """{"pausedAsks":{"a1":{"at":"Oct 2"}}}""", "ask", "a1", true, null, "")]
    [InlineData("a date that does not exist is unknown, and the pause still holds", """{"pausedAsks":{"a1":{"at":"2026-02-30T00:00:00Z"}}}""", "ask", "a1", true, null, "")]
    [InlineData("a pause with no time or stops is still a pause", """{"pausedAsks":{"a1":{}}}""", "ask", "a1", true, null, "")]
    [InlineData("a stopped session is read without the spaces around it, and a blank one or one that is not text is not read", """{"pausedAsks":{"a1":{"stopped":{"q1":" s-1 ","q2":"  ","q3":7}}}}""", "ask", "a1", true, null, "q1=s-1")]
    [InlineData("a quest stopped twice in any case is read where first written", """{"pausedAsks":{"a1":{"stopped":{"q1":"s-1","Q1":"s-2","q2":"s-3"}}}}""", "ask", "a1", true, null, "q1=s-1,q2=s-3")]
    [InlineData("stops that are not a map are none", """{"pausedAsks":{"a1":{"stopped":["q1"]}}}""", "ask", "a1", true, null, "")]
    [InlineData("an entry that is not an object is no pause", """{"pausedAsks":{"a1":true}}""", "ask", "a1", false, null, "")]
    [InlineData("a list is not a map", """{"pausedAsks":["a1"]}""", "ask", "a1", false, null, "")]
    [InlineData("null is absent", """{"pausedQuests":null}""", "quest", "q9", false, null, "")]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """{"pausedAsks":{"straße":{}}}""", "ask", "STRASSE", false, null, "")]
    [InlineData("a dotted capital I is not an i with a dot above", """{"pausedAsks":{"İzmir":{}}}""", "ask", "i\u0307zmir", false, null, "")]
    public void Pauses_read_as_the_cli_reads_them(string name, string file, string scope, string id, bool paused, string? at, string stopped)
    {
        var config = DriverConfig.Parse(file);
        var pause = scope == "ask" ? config.PausedAsk(id) : config.PausedQuest(id);

        var read = pause is null ? (false, null, "") : (true, Second(pause.At), Spelled(pause.Stopped));
        Assert.True((paused, at, stopped) == read, $"{name}: {read}");
    }

    [Fact]
    public void A_pause_survives_the_file_and_is_written_only_when_set()
    {
        var at = new DateTimeOffset(2026, 10, 2, 14, 2, 11, TimeSpan.Zero);
        var paused = DriverConfig.Empty
            .WithPausedAsk("a1", new WorkPause(at, new Dictionary<string, string> { ["q7"] = "s-1" }))
            .WithPausedQuest("q9", new WorkPause(at.AddMinutes(3), new Dictionary<string, string>()));

        var back = DriverConfig.Parse(paused.ToJson());
        Assert.Equal(at, back.PausedAsk("a1")!.At);
        Assert.Equal("s-1", back.PausedAsk("a1")!.Stopped["q7"]);
        Assert.Equal(at.AddMinutes(3), back.PausedQuest("q9")!.At);
        Assert.DoesNotContain("paused", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>The design's shape (§2.5): each id an object, its time in UTC to the second, and its stops always written, none as <c>{}</c>.</summary>
    [Fact]
    public void A_pause_is_written_in_the_designs_shape()
    {
        var at = new DateTimeOffset(2026, 10, 2, 16, 2, 11, 500, TimeSpan.FromHours(2));
        var written = DriverConfig.Empty
            .WithPausedAsk("a1", new WorkPause(at, new Dictionary<string, string> { ["q7"] = "s-1", ["q3"] = "s-2" }))
            .WithPausedQuest("q9", new WorkPause(null, new Dictionary<string, string>()))
            .ToJson();

        using var document = JsonDocument.Parse(written);
        var ask = document.RootElement.GetProperty("pausedAsks").GetProperty("a1");
        Assert.Equal("2026-10-02T14:02:11Z", ask.GetProperty("at").GetString());
        Assert.Equal(["q3", "q7"], ask.GetProperty("stopped").EnumerateObject().Select(stop => stop.Name));
        var quest = document.RootElement.GetProperty("pausedQuests").GetProperty("q9");
        // A time unknown is not written: absent is unknown, as it is read.
        Assert.False(quest.TryGetProperty("at", out _));
        Assert.Equal(JsonValueKind.Object, quest.GetProperty("stopped").ValueKind);
        Assert.Empty(quest.GetProperty("stopped").EnumerateObject());
    }

    /// <summary>One pause per id: a later one replaces the earlier under the spelling first written, and null removes it.</summary>
    [Fact]
    public void A_later_pause_replaces_the_earlier_and_none_removes_it()
    {
        var at = new DateTimeOffset(2026, 10, 2, 14, 2, 11, TimeSpan.Zero);
        var config = DriverConfig.Parse("""{"pausedAsks":{"A1":{"at":"2026-10-01T00:00:00Z"},"a2":{}}}""")
            .WithPausedAsk("#a1", new WorkPause(at, new Dictionary<string, string>()));

        Assert.Equal(at, config.PausedAsk("a1")!.At);
        Assert.Equal(["A1", "a2"], config.PausedAsks.Keys.Order(StringComparer.Ordinal));

        var removed = config.WithPausedAsk("a1", null).WithPausedAsk("a2", null);
        Assert.Null(removed.PausedAsk("a1"));
        Assert.Empty(removed.PausedAsks);
        Assert.DoesNotContain("pausedAsks", removed.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>🔴 Each writer keeps the other's sections: an edit of anything else keeps the pauses.</summary>
    [Fact]
    public void Another_edit_keeps_the_pauses()
    {
        var edited = DriverConfig.Parse(
                """{"pausedAsks":{"a1":{"at":"2026-10-02T14:02:11Z","stopped":{"q7":"s-1"}}},"pausedQuests":{"q9":{"stopped":{}}},"released":{"q2":"s-5"}}""")
            .WithDrivable("engine", true).WithStrikes(2).WithReleased("q3", "s-6").WithHold("engine", true);

        var back = DriverConfig.Parse(edited.ToJson());
        Assert.Equal("s-1", back.PausedAsk("a1")!.Stopped["q7"]);
        Assert.NotNull(back.PausedQuest("q9"));
        Assert.Equal("s-5", back.ReleasedFor("q2"));
    }

    [Fact]
    public void A_pause_names_its_ask_or_quest()
    {
        var pause = new WorkPause(null, new Dictionary<string, string>());

        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithPausedAsk(" ", pause));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithPausedQuest("#", pause));
    }

    private static string? Second(DateTimeOffset? at) =>
        at?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Spelled(IReadOnlyDictionary<string, string> stopped) =>
        string.Join(",", stopped.OrderBy(stop => stop.Key, StringComparer.Ordinal).Select(stop => $"{stop.Key}={stop.Value}"));
}
