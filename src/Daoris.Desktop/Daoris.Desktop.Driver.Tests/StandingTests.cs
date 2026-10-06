using System.Globalization;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>standing</c> in <c>driver.json</c> (KNOWUSE1b, D135 §3): what the person has told this machine holds for every session
/// in a repository, in their words, with when it was set. The driver's half of a TWIN with the CLI's <c>driverconfig.ts</c>,
/// whose <c>driverconfig.test.ts</c> holds the same table and parses this theory to hold it to its own, cell for cell. A row's
/// time is the moment read, in UTC to the second, or null where none reads.
/// </summary>
/// <remarks>🔴 <b>Keep each row on one line, its cells literals</b>: the CLI's test reads them.</remarks>
public sealed class StandingTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Absent is none; an object with the person's words is the repository's answer; anything else is not read.</summary>
    [Theory]
    [InlineData("absent is none", "{}", "app", null, null)]
    [InlineData("the person's words are the repository's answer", """{"standing":{"app":{"says":"dev writes allowed","at":"2026-10-03T09:00:00Z"}}}""", "app", "dev writes allowed", "2026-10-03T09:00:00Z")]
    [InlineData("a repository is matched in any case", """{"standing":{"App":{"says":"dev only"}}}""", "app", "dev only", null)]
    [InlineData("the words are read without the spaces around them", """{"standing":{"app":{"says":"  dev only  "}}}""", "app", "dev only", null)]
    [InlineData("blank words are no answer", """{"standing":{"app":{"says":"  "}}}""", "app", null, null)]
    [InlineData("words that are not text are not read", """{"standing":{"app":{"says":7}}}""", "app", null, null)]
    [InlineData("an entry that is not an object is not read", """{"standing":{"app":"dev only"}}""", "app", null, null)]
    [InlineData("a repository written twice in any case is read where first written", """{"standing":{"app":{"says":"first"},"APP":{"says":"second"}}}""", "app", "first", null)]
    [InlineData("a time that does not read leaves the answer standing with its time unknown", """{"standing":{"app":{"says":"dev only","at":"Oct 3"}}}""", "app", "dev only", null)]
    [InlineData("a time at an offset is read in UTC", """{"standing":{"app":{"says":"dev only","at":"2026-10-03T11:00:00+02:00"}}}""", "app", "dev only", "2026-10-03T09:00:00Z")]
    [InlineData("another repository's answer is not this one's", """{"standing":{"api":{"says":"dev only"}}}""", "app", null, null)]
    [InlineData("a list is not a map", """{"standing":["app"]}""", "app", null, null)]
    [InlineData("null is absent", """{"standing":null}""", "app", null, null)]
    [InlineData("a letter whose capital is two letters is not those two: straße is not STRASSE", """{"standing":{"straße":{"says":"dev only"}}}""", "STRASSE", null, null)]
    [InlineData("a dotted capital I is not an i with a dot above", """{"standing":{"İzmir":{"says":"dev only"}}}""", "i\u0307zmir", null, null)]
    public void Standing_reads_as_the_cli_reads_it(string name, string file, string repository, string? says, string? at)
    {
        var read = DriverConfig.Parse(file).StandingFor(repository);

        Assert.True(says == read?.Says, $"{name}: {read?.Says ?? "none"}");
        Assert.True(at == read?.At?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture), $"{name}: at {read?.At}");
    }

    [Fact]
    public void An_answer_survives_the_file_written_in_utc_to_the_second_and_only_when_set()
    {
        var kept = DriverConfig.Empty.WithStanding("app", "  dev writes allowed; prod only on a yes  ", At.AddMilliseconds(250));

        var read = DriverConfig.Parse(kept.ToJson()).StandingFor("APP");
        Assert.Equal(new StandingAnswer("dev writes allowed; prod only on a yes", At), read);
        Assert.Contains("\"at\": \"2026-10-03T09:00:00Z\"", kept.ToJson(), StringComparison.Ordinal);
        Assert.DoesNotContain("standing", DriverConfig.Empty.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>One answer per repository: a later one replaces the earlier under the spelling first written; null clears it.</summary>
    [Fact]
    public void A_later_answer_replaces_the_earlier_and_null_clears_it()
    {
        var config = DriverConfig.Parse("""{"standing":{"App":{"says":"first"},"api":{"says":"theirs"}}}""")
            .WithStanding("app", "second", At);

        Assert.Equal("second", config.StandingFor("app")!.Says);
        Assert.Equal(["App", "api"], config.Standing.Keys.Order(StringComparer.Ordinal));

        var cleared = config.WithStanding("APP", null, At);
        Assert.Null(cleared.StandingFor("app"));
        Assert.Equal("theirs", cleared.StandingFor("api")!.Says);
        Assert.DoesNotContain("\"App\"", cleared.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>A door refuses no repository, blank words and words past the bound, saying why; null is a clear, never blank.</summary>
    [Fact]
    public void A_door_refuses_no_repository_blank_words_and_words_past_the_bound()
    {
        Assert.Throws<DriverException>(() => DriverConfig.Empty.WithStanding(" ", "dev only", At));
        var blank = Assert.Throws<DriverException>(() => DriverConfig.Empty.WithStanding("app", "   ", At));
        Assert.Equal(DriverConfig.StandingRefusal, blank.Message);
        var long_ = Assert.Throws<DriverException>(() => DriverConfig.Empty.WithStanding("app", new string('x', DriverConfig.StandingLimit + 1), At));
        Assert.Equal(DriverConfig.StandingRefusal, long_.Message);
        Assert.Equal(new string('x', DriverConfig.StandingLimit), DriverConfig.Empty.WithStanding("app", new string('x', DriverConfig.StandingLimit), At).StandingFor("app")!.Says);
    }

    /// <summary>🔴 Each writer keeps the other's sections: an edit of anything else keeps the answers the CLI wrote.</summary>
    [Fact]
    public void Another_edit_keeps_the_answers()
    {
        var edited = DriverConfig.Parse("""{"standing":{"app":{"says":"dev only","at":"2026-10-03T09:00:00Z"}},"cooloff":45}""")
            .WithDrivable("app", true).WithStrikes(2).WithReleased("q1", "s1");

        Assert.Equal(new StandingAnswer("dev only", At), DriverConfig.Parse(edited.ToJson()).StandingFor("app"));
    }
}
