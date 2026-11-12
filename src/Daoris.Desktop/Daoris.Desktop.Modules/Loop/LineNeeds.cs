using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>
/// Which session's wait is its line's in <i>What needs you</i>: the line's state (<see cref="LineStanding"/>) is read from every part and
/// co-worker, so a line is one row, the one of the part or co-worker that decides it, never one per part that ended waiting.
/// </summary>
internal static class LineNeeds
{
    /// <summary>
    /// Whether <paramref name="session"/>'s own wait is its line's: where no row of its line is placed, or the line needs the person
    /// through this session. A line whose newest part works, or whose co-worker is the one waiting, is not listed for another part.
    /// </summary>
    public static Func<string?, bool> Decides(IEnumerable<JsonObject> groups)
    {
        var through = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var row in groups)
        {
            if (Text(row, "session") is { } session && row["line"] is JsonObject line) through.TryAdd(session, line);
        }

        return session => session is null || !through.TryGetValue(session, out var line)
            || (Text(line, "state") == SessionGroup.NeedsYou && Text(line, "by") == session);
    }
}
