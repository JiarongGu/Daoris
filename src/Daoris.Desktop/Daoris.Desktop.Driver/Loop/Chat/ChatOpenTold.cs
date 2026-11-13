using System.Text;

namespace Daoris.Driver;

/// <summary>
/// What a workspace's new conversation is told of the one before it: each request that conversation sent which nothing has answered, or
/// whose answer is kept and not yet handed, read as a brief reads a line's (<see cref="BriefWait"/>), so the new conversation does not
/// file it again. The last conversation is the workspace's newest chat record of the person's own.
/// </summary>
internal static class ChatOpenTold
{
    /// <summary>The most requests named; the rest are counted.</summary>
    internal const int Shown = 10;

    /// <summary>The words for the newest earlier conversation of <paramref name="workspace"/>; null where it left no request open.</summary>
    /// <param name="waits">What a conversation's open requests are read from; null reads them as the work orders do.</param>
    public static async Task<string?> ToldAsync(
        ServiceClient service, string home, string workspace, string record, CancellationToken ct,
        Func<IReadOnlyDictionary<string, PriorSession>, string, Task<IReadOnlyList<BriefWait>>>? waits = null)
    {
        var json = await LineCountsRead.RecordsJsonAsync(service, ct).ConfigureAwait(false);
        if (json is null) return null;
        var records = ServiceClient.ReadRecords(json);
        var last = Last(records.Values, workspace, record);
        if (last is null) return null;
        return Of(last.Session, await (waits?.Invoke(records, last.Session) ?? OpenAsync(service, home, records, last.Session, ct)).ConfigureAwait(false));
    }

    /// <summary>The requests <paramref name="session"/> left open, as the work orders read a line's.</summary>
    internal static Task<IReadOnlyList<BriefWait>> OpenAsync(
        ServiceClient service, string home, IReadOnlyDictionary<string, PriorSession> records, string session, CancellationToken ct) =>
        ContextWorkOrders.Current.WaitsAsync(service, home, records, session, ct);

    /// <summary>The newest conversation of the workspace other than <paramref name="record"/>: a chat of the person's own, no helper's or reviewer's.</summary>
    internal static PriorSession? Last(IEnumerable<PriorSession> records, string workspace, string record) =>
        records
            .Where(each => each.Kind == SessionKinds.Chat && !each.Teammate && each.Session != record
                           && each.Serves is null && each.Opinion is null && each.Piece is null && each.Decides is null && each.Performs is null
                           && string.Equals(each.Workspace ?? RemoteTarget.DefaultWorkspace, workspace, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(each => each.Updated ?? each.Created ?? DateTimeOffset.MinValue)
            .FirstOrDefault();

    /// <summary>The words for the requests <paramref name="session"/> left open; null for none.</summary>
    internal static string? Of(string session, IReadOnlyList<BriefWait> open)
    {
        if (open.Count == 0) return null;
        return List(
            $"The conversation before this one, `{session}`, left {(open.Count == 1 ? "a request" : $"{open.Count} requests")} open. "
            + "File no request again that is among them:", open);
    }

    /// <summary>The lead and then each open request, as a brief names them, the rest counted.</summary>
    internal static string List(string lead, IReadOnlyList<BriefWait> open)
    {
        var text = new StringBuilder(lead);
        foreach (var wait in open.Take(Shown))
        {
            var asked = wait.Detail is { Length: > 0 } detail ? $": {TextBounds.Clip(TextBounds.OneLine(detail), ContextBrief.StepLimit)}" : "";
            text.Append($"\n- `{wait.Id}` to {TextBounds.OneLine(wait.To)}; {ContextBrief.Delivered(wait)}{asked}");
        }

        if (open.Count > Shown) text.Append($"\n- … and {open.Count - Shown} more");
        return text.ToString();
    }
}
