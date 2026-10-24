using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>released</c> in <c>driver.json</c> (SESSUX1b, D126 §3.4): the quests the person released from their stop, each against
/// the session they stopped, so a later stop holds the quest again. The driver's half of a TWIN with the CLI's
/// <c>driverconfig.ts</c>, until it left with the driver family's TypeScript (PUSH1f2b).
/// </summary>
/// <remarks>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</remarks>
public sealed class ReleasedTests
{
    /// <summary>Absent is no release; a quest against a session's id is one; anything else is not read.</summary>
    [Theory]
    [InlineData("absent is no release", "{}", "q1", null)]
    [InlineData("a quest against the session it stopped is a release", """{"released":{"q1":"s1"}}""", "q1", "s1")]
    [InlineData("a quest is matched in any case", """{"released":{"Q1":"s1"}}""", "q1", "s1")]
    [InlineData("a session is read without the spaces around it", """{"released":{"q1":" s1 "}}""", "q1", "s1")]
    [InlineData("a blank session names no stop", """{"released":{"q1":"  "}}""", "q1", null)]
    [InlineData("a session that is not text is not read", """{"released":{"q1":7}}""", "q1", null)]
    [InlineData("a quest written twice in any case is read where first written", """{"released":{"q1":"s1","Q1":"s2"}}""", "q1", "s1")]
    [InlineData("another quest's release is not this one's", """{"released":{"q2":"s1"}}""", "q1", null)]
    [InlineData("a list is not a map", """{"released":["q1"]}""", "q1", null)]
    [InlineData("null is absent", """{"released":null}""", "q1", null)]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """{"released":{"straße":"s1"}}""", "STRASSE", null)]
    [InlineData("a dotted capital I is not an i with a dot above", """{"released":{"İzmir":"s1"}}""", "i\u0307zmir", null)]
    public void Released_reads_as_the_cli_reads_it(string name, string file, string quest, string? session)
    {
        var read = DriverConfig.Parse(file).ReleasedFor(quest);

        Assert.True(session == read, $"{name}: {read ?? "none"}");
    }

    [Fact]
    public void A_release_survives_the_file_and_is_written_only_when_set()
    {
        var released = DriverConfig.Empty.WithReleased("q1", "s1");

        Assert.Equal("s1", DriverConfig.Parse(released.ToJson()).ReleasedFor("q1"));
        Assert.Contains("\"released\": {", released.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("released", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>One release per quest: a later release replaces the earlier, under the spelling first written.</summary>
    [Fact]
    public void A_later_release_of_a_quest_replaces_the_earlier()
    {
        var config = DriverConfig.Parse("""{"released":{"Q1":"s1","q2":"s9"}}""").WithReleased("q1", "s2");

        Assert.Equal("s2", config.ReleasedFor("q1"));
        Assert.Equal("s9", config.ReleasedFor("q2"));
        Assert.Equal(["Q1", "q2"], config.Released.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>Whether a release names this stop: the quest in any case, and the session as the record spells it.</summary>
    [Fact]
    public void A_release_names_one_stop_only()
    {
        var config = DriverConfig.Empty.WithReleased("q1", "s1");

        Assert.True(config.Releases("Q1", "s1"));
        Assert.False(config.Releases("q1", "s2"));
        Assert.False(config.Releases("q2", "s1"));
    }

    /// <summary>🔴 Each writer keeps the other's sections: an edit of anything else keeps the releases the CLI wrote.</summary>
    [Fact]
    public void Another_edit_keeps_the_releases()
    {
        var edited = DriverConfig.Parse("""{"released":{"q1":"s1"},"cooloff":45}""")
            .WithDrivable("engine", true).WithStrikes(2).WithForgiven("q2", 3);

        Assert.Equal("s1", DriverConfig.Parse(edited.ToJson()).ReleasedFor("q1"));
    }

    [Fact]
    public void A_release_names_a_quest_and_a_session()
    {
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithReleased(" ", "s1"));
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithReleased("q1", ""));
    }

    /// <summary>
    /// AGENTERRORS1a2 (D210, the agent-errors design §5.1, §8): <i>Try again</i>'s transient release, <c>transientReleased</c>, keyed to the
    /// session AND the end it releases, apart from the stop's <c>released</c>: the same record's next end is not released, an unsaid end
    /// passes nothing, and closing the quest drops it.
    /// </summary>
    [Fact]
    public void A_transient_release_passes_only_the_end_it_names_and_survives_the_file()
    {
        var ended = new DateTimeOffset(2026, 10, 24, 9, 0, 0, 123, TimeSpan.Zero);
        var released = DriverConfig.Parse(DriverConfig.Empty.WithTransientReleased("#Q1", " s1 ", ended).ToJson());

        Assert.True(released.ReleasesTransient("q1", "s1", ended));
        Assert.False(released.ReleasesTransient("q1", "s1", ended.AddMinutes(40)));
        Assert.False(released.ReleasesTransient("q1", "s2", ended));
        Assert.False(released.ReleasesTransient("q1", "s1", null));
        Assert.Null(released.ReleasedFor("q1"));
        Assert.False(DriverConfig.Empty.ReleasesTransient("q1", "s1", ended));
        Assert.Empty(released.WithoutQuest("q1").TransientReleased);
        Assert.DoesNotContain("transientReleased", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);
        Assert.Empty(DriverConfig.Parse("""{"transientReleased":{"q1":{"session":"s1"}}}""").TransientReleased);
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithTransientReleased("q1", " ", ended));
    }
}
