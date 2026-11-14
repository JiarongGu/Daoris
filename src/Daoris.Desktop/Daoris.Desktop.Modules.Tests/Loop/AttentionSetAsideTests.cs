using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NOTIFYDIET1: a row of <i>What needs you</i> the person set aside with no act (<see cref="Notices.Aside"/>) is not judged while it stands as
/// it did, by its kind, its id and the moment it had waited since; the same row waiting since a later moment, and any other row, is.
/// </summary>
public sealed class AttentionSetAsideTests
{
    private static JsonObject Parked(string id, string updated) => Session($$"""{"id":"{{id}}","state":"awaiting-person","updated":"{{updated}}"}""");

    [Fact]
    public void A_row_set_aside_is_hidden_as_it_stood_and_shown_again_once_it_waits_since_a_later_moment()
    {
        var other = Parked("e5f6a7b8", "2026-09-21T10:05:00Z");
        var all = new AttentionFacts { Sessions = [Parked("a1b2c3d4", "2026-09-21T10:00:00Z"), other] };
        Assert.Equal(["parked:a1b2c3d4", "parked:e5f6a7b8"], Keys(Rows(all)));

        var hidden = Notices.Hidden([Notices.SetAsideOf("parked", "a1b2c3d4", "2026-09-21T10:00:00Z", null, DateTimeOffset.UtcNow)]);
        Assert.Equal(["parked:e5f6a7b8"], Keys(Rows(all with { Hidden = hidden })));

        var moved = new AttentionFacts { Sessions = [Parked("a1b2c3d4", "2026-09-21T11:00:00Z"), other], Hidden = hidden };
        Assert.Equal(["parked:a1b2c3d4", "parked:e5f6a7b8"], Keys(Rows(moved)));
    }
}
