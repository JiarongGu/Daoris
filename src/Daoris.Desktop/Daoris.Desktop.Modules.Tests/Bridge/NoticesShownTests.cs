using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>notices --show</c> of a memory wait: found among the rows <i>What needs you</i> holds by its session or its sweep's operation, a
/// sweep's end said from its own row, and a subject that waits on no memory refused, nothing read from the service for it.
/// </summary>
public sealed class NoticesShownTests
{
    private static SliceSnapshot Attention(JsonObject rows) =>
        new(AttentionStateSlice.Name, "e1", StateSlice.Ready, [new SlicePartition(AttentionStateSlice.Rows, 1, StateSlice.Ready, rows)]);

    private static JsonObject Rows() => new()
    {
        ["memory:s1"] = new JsonObject
        {
            ["kind"] = "memory", ["id"] = "s1", ["session"] = "s1",
            ["memory"] = new JsonObject { ["ends"] = 1, ["replacements"] = 0, ["staged"] = 0 },
        },
        ["memory:op9"] = new JsonObject
        {
            ["kind"] = "memory", ["id"] = "op9",
            ["memory"] = new JsonObject
            {
                ["ends"] = 1, ["replacements"] = 0, ["staged"] = 0,
                ["end"] = new JsonObject { ["op"] = "op9", ["repository"] = "Engine", ["target"] = "entry-2", ["revision"] = 4, ["reason"] = "Unread now." },
            },
        },
        ["parked:s2"] = new JsonObject { ["kind"] = "parked", ["id"] = "s2", ["session"] = "s2" },
    };

    [Fact]
    public void A_memory_wait_is_found_by_its_session_or_its_operation_and_no_other_row_is()
    {
        var attention = Attention(Rows());

        Assert.NotNull(NoticesShown.Find(attention, "s1"));
        Assert.NotNull(NoticesShown.Find(attention, "op9"));
        Assert.Null(NoticesShown.Find(attention, "s2"));
        Assert.Null(NoticesShown.Find(attention, "nothing"));
        Assert.Null(NoticesShown.Find(null, "s1"));
    }

    [Fact]
    public async Task A_sweeps_end_is_said_from_its_row_and_a_subject_with_no_wait_is_refused_without_the_service()
    {
        var attention = Attention(Rows());

        var end = await NoticesShown.SaidAsync(attention, "op9", null, CancellationToken.None);
        Assert.Equal(0, end.Code);
        Assert.Contains("  op9 · End of Engine:entry-2 at revision 4 · awaits your approval", end.Out);

        var none = await NoticesShown.SaidAsync(attention, "s2", null, CancellationToken.None);
        Assert.Equal(1, none.Code);
        Assert.Equal(MemoryWaitShown.None("s2").Error, none.Error);
    }
}
