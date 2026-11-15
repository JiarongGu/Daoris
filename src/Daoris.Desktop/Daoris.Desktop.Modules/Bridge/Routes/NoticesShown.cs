using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris notices --show &lt;subject&gt;</c>: what the window shows for the memory wait of a session or a sweep's operation, read from the
/// <i>What needs you</i> row this process holds and, for a session, the service's memory rows the page asks for. It reads and decides nothing.
/// </summary>
public static class NoticesShown
{
    /// <summary>The memory row of <paramref name="subject"/> in the attention slice's rows, or null where none waits.</summary>
    public static JsonObject? Find(SliceSnapshot? attention, string subject)
    {
        var rows = attention?.Partitions.FirstOrDefault(each => each.Partition == AttentionStateSlice.Rows)?.Data as JsonObject;
        foreach (var (_, node) in rows ?? [])
        {
            if (node is not JsonObject row || Json.Text(row, "kind") != "memory") continue;
            var named = Json.Text(row, "session") ?? Json.Text(row, "quest") ?? Json.Text(row, "ask") ?? Json.Text(row, "id");
            if (string.Equals(named, subject, StringComparison.Ordinal) && row["memory"] is JsonObject) return row;
        }

        return null;
    }

    /// <summary>What the verb prints: the wait's rows, or the refusal where no memory waits on <paramref name="subject"/>.</summary>
    public static async Task<TreesSaid> SaidAsync(SliceSnapshot? attention, string subject, ServiceClient? service, CancellationToken ct)
    {
        if (Find(attention, subject) is not { } row) return MemoryWaitShown.None(subject);
        var counts = (JsonObject)row["memory"]!;
        if (counts["end"] is JsonObject end) return MemoryWaitShown.Said(subject, counts, null, end);
        if (service is null) return new([], [$"notices: the service is not reachable, so the memory rows of `{subject}` are not read."], 2);

        var json = await service.SessionMemoryAsync(subject, ct).ConfigureAwait(false);
        return MemoryWaitShown.Said(subject, counts, json is null ? null : JsonNode.Parse(json), null);
    }
}
