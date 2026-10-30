using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What one read of the journal brought a teller.</summary>
/// <param name="Store">The identity of the store the positions are of (§4.5: a position alone carries no store).</param>
/// <param name="Floor">The lowest position the journal keeps.</param>
/// <param name="Newest">The newest position issued.</param>
/// <param name="Rows">The rows after the cursor, in order; those from the floor where the cursor was below it.</param>
/// <param name="Missed">
/// Whether the teller's history is incomplete: its cursor was below the floor, or another store's. Its rows are what the journal still
/// holds; what lay between is gone, and the teller says so and tells none of it (§4.1, §19.6).
/// </param>
public sealed record JournalBatch(string Store, long Floor, long Newest, IReadOnlyList<JournalEntry> Rows, bool Missed = false);

/// <summary>One teller of the feed: what it does with a batch, and its kept cursor, if it keeps one.</summary>
public sealed record JournalTeller(Func<JournalBatch, Task> Told, Func<JournalPosition?>? Load = null, Action<JournalPosition>? Keep = null);

/// <summary>A teller's place in the journal: where it read to, in which store.</summary>
public sealed record JournalPosition(string Store, long Position);

/// <summary>
/// One process's reading of the service's event journal from its come-up (FOUNDATION1 l-i, the foundation design §4.1): the change stream asked
/// for its <c>journal</c> family, each move answered by the rows after the cursor, handed to the teller in order. A cursor that is kept is the
/// teller's own and advances only after the teller took the batch, so a teller that fails reads the rows again.
/// </summary>
/// <remarks>
/// <para><b>Never a hot loop</b>, as <see cref="LedgerChanges"/> is: the next ask is never sooner than <see cref="Floor"/> after the last
/// began, a refused or dropped ask is backed off, and a host with no such door (one from before) is asked again after <see cref="Absent"/>.</para>
/// <para><b>With no cursor</b> a teller begins at the journal's newest position: what ended before this process came up is the records' to say,
/// never a replay. Where the host answers another store than the cursor's, or a floor above it, the batch says it <see cref="JournalBatch.Missed"/>.</para>
/// </remarks>
public static class JournalFeed
{
    /// <summary>How long each wait asks to be held: under the client's own timeout.</summary>
    public const int WaitSeconds = 20;

    /// <summary>The least time between two asks.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromMilliseconds(250);

    /// <summary>The first pause after a refused or dropped ask.</summary>
    public static readonly TimeSpan FirstBackOff = TimeSpan.FromSeconds(1);

    /// <summary>The longest pause between failed asks.</summary>
    public static readonly TimeSpan LongestBackOff = TimeSpan.FromSeconds(30);

    /// <summary>How long a host with no journal is left before it is asked again.</summary>
    public static readonly TimeSpan Absent = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Read the journal until <paramref name="ct"/> ends, handing each batch to <paramref name="told"/>. <paramref name="load"/> is the teller's
    /// kept cursor, or null for none; <paramref name="keep"/> is called after each batch the teller took.
    /// </summary>
    public static Task RunAsync(
        ServiceClient service, Func<JournalBatch, Task> told, Func<JournalPosition?>? load, Action<JournalPosition>? keep, MachineLog? log,
        CancellationToken ct, TimeSpan? floor = null) =>
        RunAsync(service, [new JournalTeller(told, load, keep)], log, ct, floor);

    /// <summary>
    /// Read the journal for several tellers over one wait on the change stream, each from its own cursor (the lease's loop's and the shell's
    /// news tellers keep theirs; a reader from its come-up keeps none), so a process holds one wait however many tellers it has.
    /// </summary>
    public static async Task RunAsync(ServiceClient service, IReadOnlyList<JournalTeller> tellers, MachineLog? log, CancellationToken ct, TimeSpan? floor = null)
    {
        var cursors = tellers.Select(teller => teller.Load?.Invoke()).ToArray();
        string? since = null;
        var backOff = TimeSpan.Zero;
        var said = false;
        while (!ct.IsCancellationRequested)
        {
            var began = Stopwatch.GetTimestamp();
            TimeSpan pause;
            try
            {
                var path = $"/api/changes?journal=1&wait={WaitSeconds}" + (since is null ? "" : $"&since={Uri.EscapeDataString(since)}");
                var reply = await service.SendAsync(ServiceVerb.Get, path, null, ct).ConfigureAwait(false);
                if (reply.Status == 404)
                {
                    if (!said) log?.Info("journal.absent");
                    (said, since, pause) = (true, null, Absent);
                }
                else if (reply.Status != 200)
                {
                    pause = backOff = Next(backOff);
                    if (backOff == FirstBackOff) log?.Warn("journal.refused", ("status", reply.Status));
                }
                else
                {
                    var (next, place) = Read(reply.Payload);
                    since = next;
                    if (place is null)
                    {
                        // A host from before the journal, or one that did not read the parameter: asked again after a while.
                        if (!said) log?.Info("journal.absent");
                        (said, since, pause) = (true, null, Absent);
                    }
                    else
                    {
                        said = false;
                        for (var n = 0; n < tellers.Count; n++)
                        {
                            // A teller with no cursor begins at the newest position and keeps that, so its first restart finds its place.
                            if (cursors[n] is null)
                            {
                                cursors[n] = new JournalPosition(place.Store, place.Newest);
                                tellers[n].Keep?.Invoke(cursors[n]!);
                            }

                            cursors[n] = await CatchUpAsync(service, place, cursors[n]!, tellers[n].Told, tellers[n].Keep, ct).ConfigureAwait(false);
                        }

                        var took = Stopwatch.GetElapsedTime(began);
                        (pause, backOff) = ((floor ?? Floor) - took, TimeSpan.Zero);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failed)
            {
                pause = backOff = Next(backOff);
                if (backOff == FirstBackOff) log?.Warn("journal.dropped", ("error", failed.GetType().Name));
            }

            if (pause <= TimeSpan.Zero) continue;
            try
            {
                await Task.Delay(pause, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Read the rows after the cursor, page by page, until the teller has them all; the cursor it ends at.</summary>
    private static async Task<JournalPosition> CatchUpAsync(
        ServiceClient service, Place place, JournalPosition cursor, Func<JournalBatch, Task> told, Action<JournalPosition>? keep, CancellationToken ct)
    {
        var another = cursor.Store != place.Store;
        var position = another ? 0 : cursor.Position;
        var missed = another || position + 1 < place.Floor;
        if (!another && position >= place.Newest) return cursor;

        while (true)
        {
            var reply = await service.SendAsync(ServiceVerb.Get, $"/api/journal?since={position}&limit=500", null, ct).ConfigureAwait(false);
            if (reply.Status != 200) return cursor;
            var page = ReadPage(reply.Payload);
            if (page.Store != place.Store && page.Store.Length > 0) return cursor;
            var batch = new JournalBatch(page.Store, page.Floor, page.Newest, page.Rows, missed);
            if (page.Rows.Count > 0 || missed) await told(batch).ConfigureAwait(false);
            position = page.Rows.Count > 0 ? page.Rows[^1].Position : Math.Max(position, page.Newest);
            cursor = new JournalPosition(page.Store, position);
            keep?.Invoke(cursor);
            missed = false;
            if (page.Rows.Count == 0 || position >= page.Newest) return cursor;
        }
    }

    private static TimeSpan Next(TimeSpan backOff) =>
        backOff <= TimeSpan.Zero ? FirstBackOff
        : backOff * 2 > LongestBackOff ? LongestBackOff
        : backOff * 2;

    private sealed record Place(string Store, long Floor, long Newest);

    private static (string Since, Place? Place) Read(string payload)
    {
        using var answer = JsonDocument.Parse(payload);
        var root = answer.RootElement;
        var since = root.GetProperty("since").GetString() ?? throw new JsonException("the change stream answered no cursor");
        Place? place = root.TryGetProperty("journal", out var journal) && journal.ValueKind == JsonValueKind.Object
            ? new Place(journal.GetProperty("store").GetString() ?? "", journal.GetProperty("floor").GetInt64(), journal.GetProperty("newest").GetInt64())
            : null;
        return (since, place);
    }

    /// <summary>A page the journal's door answered, its rows read as the driver reads them.</summary>
    internal static JournalBatch ReadPage(string payload)
    {
        using var answer = JsonDocument.Parse(payload);
        var root = answer.RootElement;
        var rows = new List<JournalEntry>();
        foreach (var row in root.GetProperty("rows").EnumerateArray())
        {
            rows.Add(new JournalEntry(
                row.GetProperty("position").GetInt64(), row.GetProperty("at").GetInt64(), row.GetProperty("record").GetString() ?? "",
                Text(row, "family"), Text(row, "change") ?? "", Text(row, "stateOld"), Text(row, "stateNew"), Flag(row, "limitedOld"), Flag(row, "limitedNew"),
                Split(Text(row, "noteCodes")), Split(Text(row, "marks")) ?? [], Text(row, "workspace"), Text(row, "quest")));
        }

        return new JournalBatch(root.GetProperty("store").GetString() ?? "", root.GetProperty("floor").GetInt64(), root.GetProperty("newest").GetInt64(), rows);
    }

    private static string? Text(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? Flag(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static string[]? Split(string? joined) =>
        joined is null ? null : joined.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
