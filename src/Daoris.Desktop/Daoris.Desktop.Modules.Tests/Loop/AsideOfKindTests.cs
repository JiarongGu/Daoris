using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The person's one press that sets aside every row of one kind of <i>What needs you</i>: the rows read from the attention slice, the
/// press's own row left, the window's circle kept to, and all of them kept in one write the judge then hides by.
/// </summary>
public sealed class AsideOfKindTests : DriverModuleBridge
{
    private readonly HeldClock _clock = new();

    private static JsonObject Row(string kind, string id, string? workspace, string since = "2026-10-22T07:00:00.000Z")
    {
        var row = new JsonObject { ["kind"] = kind, ["id"] = id, ["since"] = since };
        if (workspace is not null) row["workspace"] = workspace;
        return row;
    }

    private static SliceSnapshot Held(params JsonObject[] rows)
    {
        var data = new JsonObject();
        foreach (var row in rows) data[$"{row["kind"]}:{row["id"]}"] = row;
        return new(AttentionStateSlice.Name, "e1", StateSlice.Ready, [new SlicePartition(AttentionStateSlice.Rows, 1, StateSlice.Ready, data)]);
    }

    /// <summary>Each row of the kind, by the id and moment the judge hides it by; another kind's row and a press's own row are left.</summary>
    [Fact]
    public void Every_row_of_the_kind_is_named_by_its_hiding_key_and_no_other()
    {
        var snapshot = Held(Row("check-failed", "q1", "games"), Row("check-failed", "q2", null), Row("parked", "s1", "games"), Row("confirmation", "p1", null));

        var asides = AsideOfKind.Of(snapshot, "check-failed", null, _clock.GetUtcNow());

        Assert.Equal(["q1", "q2"], asides.Select(each => each.Code).Order(StringComparer.Ordinal));
        Assert.All(asides, each => Assert.Equal((Notices.Aside, true, "check-failed", "2026-10-22T07:00:00.000Z"), (each.Kind, each.Read, each.Route, each.Act)));
        Assert.Empty(AsideOfKind.Of(snapshot, "confirmation", null, _clock.GetUtcNow()));
        Assert.Empty(AsideOfKind.Of(null, "check-failed", null, _clock.GetUtcNow()));
    }

    /// <summary>A window's scope keeps its own circle's rows and the machine-wide ones, and leaves another workspace's.</summary>
    [Fact]
    public void A_window_s_scope_leaves_another_workspace_s_rows()
    {
        var snapshot = Held(Row("check-failed", "q1", "games"), Row("check-failed", "q2", "work"), Row("check-failed", "q3", null));

        var asides = AsideOfKind.Of(snapshot, "check-failed", "games", _clock.GetUtcNow());

        Assert.Equal(["q1", "q3"], asides.Select(each => each.Code).Order(StringComparer.Ordinal));
    }

    /// <summary>The slice keeps them all in one write: each is hidden by the judge's key afterwards, and a second press changes nothing.</summary>
    [Fact]
    public void The_slice_keeps_the_whole_kind_aside_in_one_write()
    {
        var notices = new NoticesStateSlice(Home, Bus, clock: _clock, notify: () => true);
        var snapshot = Held(Row("check-failed", "q1", "games"), Row("check-failed", "q2", "games"), Row("parked", "s1", "games"));

        Assert.Equal(2, notices.SetAsideKind(snapshot, "check-failed", null));
        Assert.Equal(0, notices.SetAsideKind(snapshot, "unclosed", null));

        var hidden = Notices.Hidden(Notices.Read(Home, _clock.GetUtcNow())!);
        Assert.Contains(Notices.HiddenKey("check-failed", "q1", "2026-10-22T07:00:00.000Z"), hidden);
        Assert.Contains(Notices.HiddenKey("check-failed", "q2", "2026-10-22T07:00:00.000Z"), hidden);
        Assert.DoesNotContain(Notices.HiddenKey("parked", "s1", "2026-10-22T07:00:00.000Z"), hidden);
    }
}
