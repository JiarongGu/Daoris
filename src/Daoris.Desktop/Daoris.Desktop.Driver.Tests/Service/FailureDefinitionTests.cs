using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A failure is one thing for the quest's count and for its run on one choice.</summary>
public sealed class FailureDefinitionTests
{
    private const string Quiet = """[{ "code": "ended.quiet", "values": { "minutes": "10" } }]""";

    private static string Record(string id, string at, string state, string? notes = null, bool interrupted = false) =>
        $$"""{ "id": "{{id}}", "quest": "q1", "door": "a", "state": "{{state}}", "created": "2026-11-06T{{at}}Z", "interrupted": {{(interrupted ? "true" : "false")}}{{(notes is null ? "" : $", \"noteParts\": {notes}")}} }""";

    private static string List(params string[] records) => "[" + string.Join(",", records) + "]";

    [Fact]
    public void A_quiet_end_is_no_failure_for_either_count()
    {
        var json = List(Record("s1", "09:00:00", "failed"), Record("s2", "09:10:00", "failed", Quiet));

        Assert.Equal(1, ServiceClient.ReadStrikes(json)["q1"]);
        Assert.Equal(1, ServiceClient.ReadFailedRuns(json)["q1"]);
    }

    [Fact]
    public void A_cut_off_is_a_failure_for_both_counts_and_never_starts_the_run_again()
    {
        var json = List(Record("s1", "09:00:00", "failed"), Record("s2", "09:10:00", "stopped", interrupted: true));

        Assert.Equal(2, ServiceClient.ReadStrikes(json)["q1"]);
        Assert.Equal(2, ServiceClient.ReadFailedRuns(json)["q1"]);
    }

    [Fact]
    public void A_quiet_end_between_two_failures_breaks_no_run()
    {
        var json = List(Record("s1", "09:00:00", "failed"), Record("s2", "09:10:00", "failed", Quiet), Record("s3", "09:20:00", "failed"));

        Assert.Equal(2, ServiceClient.ReadStrikes(json)["q1"]);
        Assert.Equal(2, ServiceClient.ReadFailedRuns(json)["q1"]);
    }
}
