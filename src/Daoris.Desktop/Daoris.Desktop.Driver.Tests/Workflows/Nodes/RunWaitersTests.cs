using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The sessions a run's end ends are this machine's records awaiting a step whose note names that run: found by the run they wait on,
/// whatever quest or branch they served.
/// </summary>
public sealed class RunWaitersTests
{
    private static string Record(string id, string state, string? code, string? run, string? quest = null)
    {
        var values = run is null ? "{}" : "{\"run\":\"" + run + "\",\"node\":\"work\"}";
        var parts = code is null ? "" : ",\"noteParts\":[{\"code\":\"" + code + "\",\"values\":" + values + ",\"text\":\"It waits.\"}]";
        var served = quest is null ? "" : ",\"quest\":\"" + quest + "\"";
        return "{\"id\":\"" + id + "\",\"repository\":\"Owner\",\"state\":\"" + state + "\"" + served + parts + "}";
    }

    [Fact]
    public void A_run_s_waiting_sessions_are_its_records_awaiting_a_step_whose_note_names_it()
    {
        var records = SessionRecords.Parse("[" + string.Join(",",
            Record("s-wait", "awaiting-step", "waits.step", "q1", quest: "q9"),
            Record("s-other", "awaiting-step", "waits.step", "q2"),
            Record("s-check", "awaiting-step", "waits.check", null),
            Record("s-done", "completed", "waits.step", "q1"),
            Record("s-bare", "awaiting-step", null, null),
            Record("origin/s-team", "awaiting-step", "waits.step", "q1")) + "]");

        Assert.Equal("q1", records.Single(record => record.Id == "s-wait").WaitsOn);
        Assert.Null(records.Single(record => record.Id == "s-check").WaitsOn);
        Assert.Equal(["s-wait"], RunWaiters.Of("q1", records));
        Assert.Equal(["s-other"], RunWaiters.Of("q2", records));
    }
}
