using System.Text.Json.Nodes;
using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The rows of one kind <i>What needs you</i> holds now, as the person's one press sets them all aside: each with the id and
/// the moment the judge hides it by (<see cref="Notices.HiddenKey"/>), read from the attention slice's rows as last judged.
/// </summary>
public static class AsideOfKind
{
    /// <summary>
    /// The notices that set aside every row of <paramref name="kind"/> the slice holds, in <paramref name="workspace"/>'s circle and the
    /// machine-wide ones, or every one where it names none. A press's own row, whose card is the press, is never set aside.
    /// </summary>
    public static IReadOnlyList<Notice> Of(SliceSnapshot? attention, string kind, string? workspace, DateTimeOffset at)
    {
        var rows = attention?.Partitions.FirstOrDefault(each => each.Partition == AttentionStateSlice.Rows)?.Data as JsonObject;
        if (kind == "confirmation" || rows is null) return [];

        List<Notice> asides = [];
        foreach (var (_, node) in rows)
        {
            if (node is not JsonObject row || AttentionJson.Text(row, "kind") != kind || AttentionJson.Text(row, "id") is not { } id) continue;
            var home = AttentionJson.Text(row, "workspace");
            if (workspace is not null && home is not null && MainWindowScopes.Circle(home) != MainWindowScopes.Circle(workspace)) continue;
            asides.Add(Notices.SetAsideOf(kind, id, AttentionJson.Text(row, "since"), home, at));
        }

        return asides;
    }
}
