using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The downloads' rule (KEEP1b, D195 points 2 and 6; the cleanup design §2.1, §2.3, §3.1): a version nothing uses goes once its days have
/// passed in its current unused stretch, through CACHECLEAR1's act (<see cref="DownloadClearing.ClearAsync"/>), each judged again under the
/// use files' locks and kept while a running session was handed it. The look's Keeping pass runs it, holding the home's run lock.
/// </summary>
/// <remarks>
/// <para><b>The current unused stretch</b> (Fable F2.1): each pass lists the versions without measuring them and records, in
/// <c>keeping.json</c>'s <c>unused</c>, when it first finds one nothing pins, uses or names; a pass that finds it pinned, used or named, or
/// finds it gone, clears that time, so the next that finds it unused starts a new stretch. A version a running session was handed does not
/// clear it: the session's own versions are the guard while it runs. One whose use cannot be read now (<c>unread</c>), or downloaded within
/// the hour (<c>recent</c>), is left as it stands.</para>
/// <para><b>Once a day</b> (§3.1 step 5): the versions whose stretch is past the policy's days are removed; none under <c>never</c>, and
/// none while <c>keeping.json</c> does not read, since then no stretch is known (§6.7). The run writes the kind's <c>last</c>.</para>
/// </remarks>
public static class DownloadKeeping
{
    /// <summary>A version's key in <c>keeping.json</c> and on a session's record: <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c>.</summary>
    public static string Key(string kind, string name, string version) => $"{kind}/{name}/{version}";

    /// <summary>A listed version's key.</summary>
    public static string Key(DownloadedVersion row) => Key(row.Kind, row.Name, row.Version);

    /// <summary>
    /// What a start of a session is handed of the downloads, kept on its record before it is spawned (KEEP1b, D195 point 6; the cleanup
    /// design §3.5, Fable F2.5): the rule keeps each version while the session runs. Every spawn takes it, a go-on as a first start, once
    /// its start is prepared; a start handed none of them asks nothing. Best-effort: a record the service did not keep it on reads as one
    /// from before, which keeps the start-time rule, so the start goes on either way.
    /// </summary>
    public static async Task KeepHandedAsync(ServiceClient service, string home, string sessionId, ProcessStartInfo info, CancellationToken ct)
    {
        var handed = DownloadClearing.Handed(home, info);
        if (handed.Count == 0) return;
        try
        {
            await service.KeepResolvedAsync(sessionId, handed, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException
                                          || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Unkept, the record says nothing of what it was handed, and the rule keeps what went unused after it opened.
        }
    }

    /// <summary>When a listed version's current unused stretch began, by the record; null where none is recorded.</summary>
    public static DateTimeOffset? UnusedSince(DownloadedVersion row, KeepingFile record) =>
        !row.Unfinished && record.Unused.TryGetValue(Key(row), out var since) ? since : null;

    /// <summary>
    /// When a listed version goes by the rule (the Downloads row's date): its stretch's start and the policy's days, for one nothing keeps;
    /// null for one kept, one no pass has found unused yet, and under <c>never</c>.
    /// </summary>
    public static DateTimeOffset? Goes(DownloadedVersion row, KeepingFile record, DownloadsPolicy policy) =>
        row.Keeps is null && policy.Days is { } days && UnusedSince(row, record) is { } since ? since + days : null;

    /// <summary>
    /// One pass of the rule: each version's unused stretch recorded or cleared, and, where <paramref name="daily"/>, the versions past the
    /// policy's days removed through the act and the kind's <c>last</c> written.
    /// </summary>
    /// <param name="door">The run's door, <see cref="KeepingRun"/>'s, for <c>last</c> and the machine log.</param>
    /// <param name="run">The run's id, for <c>last</c>.</param>
    public static async Task<DownloadsPass> PassAsync(
        string home, DownloadsPolicy policy, DownloadJudge judge, bool daily, string door, string run, MachineLog? log = null,
        CancellationToken ct = default)
    {
        var now = judge.Now();
        var listing = DownloadClearing.List(home, judge.Uses(), now, ct, measure: false);
        if (listing.Why is not null) return DownloadsPass.Nothing;

        var read = KeepingRecord.Read(home);
        if (read.Problem is { } problem) return new([], problem);

        var rows = listing.Versions.Where(row => !row.Unfinished).ToList();
        var unused = new Dictionary<string, DateTimeOffset>(read.Unused, StringComparer.Ordinal);
        var changes = new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = Key(row);
            if (row.Keeps is "pinned" or "used" or "named")
            {
                // Found used: the stretch ends, and the next pass that finds it unused starts another (F2.1).
                if (read.UnusedRaw.ContainsKey(key)) changes[key] = null;
                unused.Remove(key);
            }
            else if (row.Keeps is null && !read.UnusedRaw.ContainsKey(key))
            {
                changes[key] = now;
                unused[key] = now;
            }
        }

        // A version no longer there ends its stretch: one downloaded again later starts a new one.
        var listed = rows.Select(Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in read.UnusedRaw.Keys.Where(key => !listed.Contains(key)))
        {
            changes[key] = null;
            unused.Remove(key);
        }

        var unwritten = changes.Count > 0 ? KeepingRecord.WriteUnused(home, read, changes) : null;
        if (!daily || policy.Days is not { } days) return new([], unwritten);

        var due = rows
            .Where(row => row.Keeps is null && unused.TryGetValue(Key(row), out var since) && now - since >= days)
            .Select(row => new DownloadAsked(row.Kind, row.Name, row.Version))
            .ToList();
        if (due.Count == 0) return new([], unwritten);

        var cleared = await DownloadClearing.ClearAsync(
            home, due, judge with { FirstUnused = key => unused.TryGetValue(key, out var since) ? since : null }, log,
            cancellationToken: ct, door: door).ConfigureAwait(false);
        var went = cleared.Where(each => each.Outcome is "removed" or "unfinished").ToList();
        var kept = cleared.Where(each => each.Outcome == "kept")
            .GroupBy(each => each.Version?.Keeps ?? "unread", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var last = KeepingRecord.WriteLast(
            home, KeepingRecord.Read(home), KeepKinds.Downloads.Id,
            new KeepingLast(judge.Now(), door, run, went.Count, went.Sum(each => each.Version?.Bytes ?? 0), kept));
        return new(cleared, unwritten ?? last);
    }
}

/// <summary>What one pass of the downloads' rule did (<see cref="DownloadKeeping.PassAsync"/>).</summary>
/// <param name="Cleared">Each version past its days, and what became of it; none where the pass was not the day's.</param>
/// <param name="Unwritten">Why <c>keeping.json</c> was not written or could not be read; null where it was, or needed nothing.</param>
public sealed record DownloadsPass(IReadOnlyList<DownloadCleared> Cleared, string? Unwritten)
{
    /// <summary>Nothing listed, so nothing recorded and nothing removed.</summary>
    public static DownloadsPass Nothing { get; } = new([], null);
}
