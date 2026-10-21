namespace Daoris.Driver;

/// <summary>What the cleaner did with one receipt (<see cref="WorkPerformed.What"/>'s words for it).</summary>
public static class CleanedWhat
{
    /// <summary>Its plugin answered it removed, already gone, or retained: it ends there (<see cref="WorkPerformed.Code"/> is the disposition).</summary>
    public const string Cleaned = "cleaned";

    /// <summary>Its plugin answered a failure, or did not answer: asked again with the same key after its back-off.</summary>
    public const string Again = "clean-again";
}

/// <summary>
/// The durable, idempotent cleanup of what plugins created (FABRIC-PERFORM1d; the fabric events design §D3): at a unit's true end, and at
/// once for a superseded attempt's, each receipt whose role is temporary is asked of the plugin that created it through <c>work/cleanup</c>,
/// the request persisted with its key before it is sent, so a crash between the plugin's removal and the recording of its answer sends the
/// same key again and is answered <c>already-gone</c>. A failure is asked again on a back-off (1, 5, 15 and 60 minutes, then daily) and never
/// disappears: past seven days it reads as blocked, with the plugin's words. Daoris runs no command for it and hands it no path.
/// </summary>
/// <remarks>
/// <para><b>What it never cleans:</b> a result (kept at the end); a receipt whose schema its plugin does not declare (no rule says who cleans
/// it, so it blocks, never a guess); a receipt of a unit whose end is not over, or not read; one whose plugin's process is not up, asked
/// once it is (<see cref="Owed"/> keeps it wanted).</para>
/// <para><b>Every change is one locked edit</b> of the unit's resource record (<see cref="ResourceRecordFiles.Edit{T}"/>), the answer applied
/// after its await to the receipt as the record holds it then, and only while it is still requested with that key.</para>
/// </remarks>
public sealed class ResourceCleaner(ResourceRecordFiles receipts, IWorkPerformPort port, Func<DateTimeOffset> clock)
{
    /// <summary>How long a requested cleanup is tried before it reads as blocked (§D3): it is still tried, daily.</summary>
    public static readonly TimeSpan BlockedAfter = TimeSpan.FromDays(7);

    /// <summary>The back-off after each failed or unanswered delivery (§D3): 1, 5, 15 and 60 minutes, then daily.</summary>
    public static TimeSpan DelayAfter(int tries) => tries switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        4 => TimeSpan.FromMinutes(60),
        _ => TimeSpan.FromDays(1),
    };

    /// <summary>Whether a receipt's cleanup reads as blocked (§D3): requested, unsettled, and first asked at least <see cref="BlockedAfter"/> ago.</summary>
    public static bool Blocked(ResourceReceipt receipt, DateTimeOffset now) =>
        receipt.Cleanup is { State: CleanupStates.Requested } cleanup && now - cleanup.At >= BlockedAfter;

    /// <summary>
    /// Whether a receipt is Daoris's to ask its plugin to clean now: not settled, temporary (a superseded attempt's whatever it declared), its
    /// schema declared, its back-off passed, and its unit's end over (<paramref name="ended"/>) unless its attempt was superseded.
    /// </summary>
    public static bool Due(ResourceReceipt receipt, bool? ended, DateTimeOffset now) =>
        !receipt.Settled && receipt.Effective == PluginResourceRoles.Temporary
        && (receipt.Superseded || ended == true)
        && (receipt.Cleanup?.NextAt is not { } next || next <= now);

    /// <summary>
    /// The plugins owed a cleanup that is or will come due by itself (FABRIC-PERFORM1d): each receipt not settled and temporary, of a unit
    /// whose end is over or an attempt superseded. Their process is wanted though no unit awaits their steps (<see cref="IWorkRuntimePort.Owe"/>).
    /// </summary>
    public IReadOnlySet<string> Owed(Func<string, bool?> ended) =>
        receipts.All().Records
            .SelectMany(record => record.Receipts.Where(receipt => !receipt.Settled && receipt.Effective == PluginResourceRoles.Temporary
                && (receipt.Superseded || ended(record.Unit) == true)))
            .Select(receipt => receipt.Plugin).ToHashSet(StringComparer.Ordinal);

    /// <summary>When the next back-off ends among the receipts still requested, or null: the loop's timer.</summary>
    public DateTimeOffset? NextDue() =>
        receipts.All().Records.SelectMany(record => record.Receipts)
            .Where(receipt => !receipt.Settled).Select(receipt => receipt.Cleanup?.NextAt).OfType<DateTimeOffset>()
            .Order().Cast<DateTimeOffset?>().FirstOrDefault();

    /// <summary>
    /// Each receipt due asked of its plugin, within the pass's <paramref name="sends"/>: the request written first, then sent, then its answer
    /// kept. A plugin whose process is not up is not asked now, and takes no send.
    /// </summary>
    /// <param name="ended">Whether a unit's end is over, as the pass last read it; null where it was not read, and nothing of it is asked.</param>
    public async Task<IReadOnlyList<WorkPerformed>> CleanAsync(Func<string, bool?> ended, WorkSends sends, CancellationToken ct)
    {
        var done = new List<WorkPerformed>();
        var now = clock();
        var due = receipts.All().Records
            .SelectMany(record => record.Receipts.Where(receipt => Due(receipt, ended(record.Unit), now)).Select(receipt => (record.Unit, Receipt: receipt)))
            .ToList();
        foreach (var (unit, receipt) in due)
        {
            if (sends.Left <= 0) break;
            if (port.GenerationOf(receipt.Plugin) is null) continue;
            if (!sends.Take()) break;
            done.Add(await CleanOneAsync(unit, receipt.Id, ct).ConfigureAwait(false));
        }

        return done;
    }

    private async Task<WorkPerformed> CleanOneAsync(string unit, string id, CancellationToken ct)
    {
        // §D3: the request persisted before it is sent, its key kept for every later delivery.
        var now = clock();
        var (refused, asked) = receipts.Edit<ResourceReceipt?>(unit, record =>
        {
            if (record.Receipts.FirstOrDefault(each => each.Id == id) is not { Settled: false } receipt) return (null, null);
            var requested = receipt with { Cleanup = receipt.Cleanup ?? new ResourceCleanup(CleanupStates.Requested, WorkPerformer.Random("c-", 16), now, 0) };
            return (ReferenceEquals(requested.Cleanup, receipt.Cleanup) ? null : With(record, requested), requested);
        });
        if (refused is not null) return new(unit, id, PerformedWhat.Refused, refused);
        if (asked?.Cleanup is not { } cleanup) return new(unit, id, PerformedWhat.NotTaken, WorkPerformer.Moved);

        WorkCleanupAnswer? answer = null;
        try
        {
            (answer, _) = await port.CleanupAsync(asked.Plugin, new WorkCleanupCall(asked.Id, asked.Resource, cleanup.Key), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException
            && error is DriverException or IOException or TimeoutException or InvalidOperationException)
        {
            // Unanswered: asked again with the same key after its back-off.
        }

        // After the await, the receipt as the record holds it now, and only while it is still requested with this key.
        now = clock();
        var (unkept, result) = receipts.Edit<WorkPerformed>(unit, record =>
        {
            if (record.Receipts.FirstOrDefault(each => each.Id == id) is not { Cleanup: { State: CleanupStates.Requested } held } receipt || held.Key != cleanup.Key)
            {
                return (null, new WorkPerformed(unit, id, PerformedWhat.NotTaken, WorkPerformer.Moved));
            }

            if (answer is { } said && CleanupDispositions.Terminal(said.Disposition))
            {
                var settled = held with
                {
                    State = CleanupStates.Settled, Disposition = said.Disposition, Proof = said.Proof, Message = said.Message, SettledAt = now, NextAt = null,
                };
                return (With(record, receipt with { Cleanup = settled }), new WorkPerformed(unit, id, CleanedWhat.Cleaned, said.Disposition));
            }

            var tries = held.Tries + 1;
            var again = held with
            {
                Tries = tries, NextAt = now + DelayAfter(tries),
                Disposition = answer?.Disposition, Message = answer?.Message ?? held.Message, Proof = answer?.Proof ?? held.Proof,
            };
            return (With(record, receipt with { Cleanup = again }), new WorkPerformed(unit, id, CleanedWhat.Again, answer?.Disposition ?? PerformedWhat.Unanswered));
        });
        return unkept is null ? result! : new(unit, id, PerformedWhat.Refused, unkept);
    }

    private static ResourceRecord With(ResourceRecord record, ResourceReceipt receipt) => record with
    {
        Receipts = [.. record.Receipts.Select(each => each.Id == receipt.Id ? receipt : each)],
    };
}
