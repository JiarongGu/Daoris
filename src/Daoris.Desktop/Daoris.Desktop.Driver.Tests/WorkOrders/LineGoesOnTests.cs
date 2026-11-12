using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A part's end is no <i>Ended</i> notice of its line while the line goes on: a later part that has not ended, or a co-worker serving
/// the line that works.
/// </summary>
public sealed class LineGoesOnTests
{
    private static SessionRecord Record(string id, string state, int minute, string? line = null, string? serves = null)
    {
        var json = new JsonObject
        {
            ["id"] = id, ["repository"] = "engine", ["adapter"] = "claude-code", ["state"] = state, ["kind"] = "chat",
            ["line"] = line ?? id, ["serves"] = serves,
            ["created"] = new DateTimeOffset(2026, 11, 12, 9, minute, 0, TimeSpan.Zero).ToString("O"),
            ["updated"] = new DateTimeOffset(2026, 11, 12, 9, minute + 1, 0, TimeSpan.Zero).ToString("O"),
        };
        return SessionLook.From(new JsonArray(json).ToJsonString(), [], [], _ => 0).Records[0];
    }

    [Fact]
    public void A_line_with_a_later_part_that_has_not_ended_goes_on()
    {
        var records = new[] { Record("a1", "stopped", 0), Record("a2", "working", 5, line: "a1") };

        Assert.True(LineGoesOn.Of("a1", records));
    }

    [Fact]
    public void A_line_whose_later_part_ended_too_does_not()
    {
        var records = new[] { Record("a1", "stopped", 0), Record("a2", "failed", 5, line: "a1") };

        Assert.False(LineGoesOn.Of("a1", records));
        Assert.False(LineGoesOn.Of("a2", records));
    }

    [Fact]
    public void A_line_whose_co_worker_works_goes_on_and_one_whose_co_worker_ended_does_not()
    {
        Assert.True(LineGoesOn.Of("a1", [Record("a1", "completed", 0), Record("h1", "working", 3, serves: "a1")]));
        Assert.False(LineGoesOn.Of("a1", [Record("a1", "completed", 0), Record("h1", "completed", 3, serves: "a1")]));
    }

    [Fact]
    public void A_session_alone_and_one_the_records_do_not_hold_do_not_go_on()
    {
        Assert.False(LineGoesOn.Of("a1", [Record("a1", "completed", 0)]));
        Assert.False(LineGoesOn.Of("zz", [Record("a1", "completed", 0)]));
    }
}
