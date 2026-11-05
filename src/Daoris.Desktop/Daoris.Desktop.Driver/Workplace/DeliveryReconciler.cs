using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>What a delivery's settlement by phase came to (LANDNODE1b3), one of these words.</summary>
public static class DeliveryStandings
{
    /// <summary>Its method's reaching phase is done, and it is settled delivered: its <c>land</c> is reached.</summary>
    public const string Delivered = "delivered";

    /// <summary>It was settled before, as <see cref="DeliveryRecord.Settled"/> says; nothing was written.</summary>
    public const string Settled = "settled";

    /// <summary>What it did so far is certain, and its landing goes on from <see cref="DeliveryReconciled.Next"/> under this run's claim.</summary>
    public const string GoesOn = "goes-on";

    /// <summary>A merge whose request waits in its line's queue: the line takes it at a look, and nothing is pressed again.</summary>
    public const string Waits = "waits";

    /// <summary>Its git phases are done, and what follows is its plugin's, which the plugin frame settles.</summary>
    public const string Plugin = "plugin";

    /// <summary>Not taken up now: another run delivers it, a newer build wrote it, or the store or git could not say. Asked again at the next look.</summary>
    public const string Held = "held";

    /// <summary>Read as neither done nor withdrawn: held for the person to settle, and nothing repeats its effect.</summary>
    public const string Unknown = "unknown";
}

/// <summary>A unit's newest delivery as the landing's plan shows it (LANDNODE1c2): the record, and the phase an unresolved one last wrote ahead.</summary>
public sealed record LandingDelivery(DeliveryRecord Record, string? Phase)
{
    /// <summary>The newest delivery of <paramref name="unit"/> in <paramref name="home"/>'s store, or null where it holds none or does not read.</summary>
    public static LandingDelivery? Newest(string home, string unit)
    {
        try
        {
            var store = new DeliveryStore(home);
            return store.OfUnit(unit).FirstOrDefault() is { } newest
                ? new LandingDelivery(newest, newest.Unresolved ? store.Phases(newest.Key).LastOrDefault()?.Phase : null)
                : null;
        }
        catch (Exception error) when (error is SqliteException or DesktopStoreException or IOException)
        {
            return null;
        }
    }
}

/// <summary>One delivery's settlement by phase: its key, what it came to, and its words.</summary>
public sealed record DeliveryReconciled(string Key, string Standing, string Said)
{
    /// <summary>The claim this run holds it under, where it took or held it: the one its landing goes on with.</summary>
    public DeliveryClaim? Claim { get; init; }

    /// <summary>The git phase its landing goes on from, for <see cref="DeliveryStandings.GoesOn"/>.</summary>
    public string? Next { get; init; }
}

/// <summary>
/// A delivery's reconciliation by phase (LANDNODE1b3; the land node design §3, <i>A restart settles by phase, before any new start</i>):
/// every unresolved delivery, or a unit's before its next start, taken from its dead run as <see cref="DeliveryStore.Take"/> allows and
/// settled by its git phases. A phase's recorded outcome goes on from the next phase; an entry written ahead without its outcome is read from
/// the ref and the landing record, done where they hold the intended tip and withdrawn where they hold nothing of it; anything else is
/// unknown, held and said for the person. Neither a timeout nor another Accept repeats an uncertain effect, and nothing here writes git.
/// </summary>
/// <remarks>
/// <para><b>A merge</b> keeps no landing record: the line holding its commit is its record. One whose press left its request waiting was
/// withdrawn by its landing; the line may take it later, so it waits while the request does, and is reached once the line holds the commit.</para>
/// <para><b>A plugin's phases</b> (<c>plugin-dispatched</c>, <c>plugin-answered</c>) are a delivery the retired <c>work/land</c> wire wrote before G8: they are
/// read, never sent again, and a delivery whose git phases are done is left as it stands, said. G8-CONVERT1 deletes the reading.</para>
/// </remarks>
/// <param name="checkout">The checkout of a delivery's repository, which its refs are read in; null where it is not here.</param>
public sealed class DeliveryReconciler(DeliveryStore store, string home, Func<DeliveryRecord, string?> checkout)
{

    /// <summary>A delivery's checkout among those the registry last kept for <paramref name="home"/> (<see cref="GitFolders"/>), or null.</summary>
    public static Func<DeliveryRecord, string?> Checkouts(string home) => record =>
        GitFolders.Read(home).Where(each => string.Equals(each.Repository, record.Intent.Repository, StringComparison.OrdinalIgnoreCase))
            .Select(each => each.Root).FirstOrDefault(Directory.Exists);

    /// <summary>Every unresolved delivery, oldest first, each settled by phase: the driver's start, and a look before any new start.</summary>
    public async Task<IReadOnlyList<DeliveryReconciled>> StartAsync(CancellationToken ct)
    {
        IReadOnlyList<DeliveryRecord> open;
        try
        {
            open = store.Unresolved();
        }
        catch (Exception error) when (error is SqliteException or DesktopStoreException or IOException)
        {
            return [new("", DeliveryStandings.Held, $"the delivery record could not be read ({error.Message.Trim()}), so its deliveries are settled at the next look")];
        }

        var settled = new List<DeliveryReconciled>(open.Count);
        foreach (var delivery in open)
        {
            ct.ThrowIfCancellationRequested();
            settled.Add(await OneAsync(delivery, ct).ConfigureAwait(false));
        }

        return settled;
    }

    /// <summary>
    /// After a line's run closed a request (LANDNODE1l): each merge into <paramref name="line"/> whose press left its request waiting settled by
    /// phase now, so the one the line took is recorded reached as it is taken rather than at the next start. One still waiting, or a live run's,
    /// stands as it was.
    /// </summary>
    public static async Task TakenAsync(string home, string root, string repository, string line, CancellationToken ct)
    {
        try
        {
            var store = new DeliveryStore(home);
            var reconciler = new DeliveryReconciler(store, home, _ => root);
            foreach (var delivery in store.Unresolved().Where(each => each.Intent.Method == DeliveryMethods.Merge && each.Intent.Line == line
                && string.Equals(each.Intent.Repository, repository, StringComparison.OrdinalIgnoreCase)))
            {
                if (store.Phases(delivery.Key).LastOrDefault() is { Phase: DeliveryPhases.RefWritten, Outcome: DeliveryOutcomes.Withdrawn })
                {
                    await reconciler.OneAsync(delivery, ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception error) when (error is SqliteException or DesktopStoreException or IOException)
        {
            // A store that cannot be read now leaves them to the next start's settlement, as before.
        }
    }

    /// <summary>Before a new start of <paramref name="unit"/>: its unresolved delivery settled by phase; null where it has none.</summary>
    public async Task<DeliveryReconciled?> BeforeStartAsync(string unit, CancellationToken ct) =>
        store.UnresolvedFor(unit) is { } open ? await OneAsync(open, ct).ConfigureAwait(false) : null;

    /// <summary>One delivery settled by phase, held in this process while it is, outside any landing's memo scope.</summary>
    public async Task<DeliveryReconciled> OneAsync(DeliveryRecord delivery, CancellationToken ct)
    {
        using var facts = GitFacts.Outside();
        using var refs = RefSnapshot.Outside();
        if (!delivery.Unresolved) return Said(delivery, DeliveryStandings.Settled, $"it is settled {delivery.Settled}");
        if (delivery.Held) return Said(delivery, DeliveryStandings.Held, "it was written by a newer Daoris, which settles it");
        using var here = DeliveryRuns.Hold(home, delivery.Key);
        if (here is null) return Said(delivery, DeliveryStandings.Held, "it is being landed or settled here now, and is settled at the next look");

        try
        {
            DeliveryClaim claim;
            if (string.Equals(delivery.ClaimRun, store.Run, StringComparison.Ordinal))
            {
                claim = new DeliveryClaim(delivery.Key, delivery.ClaimRun, delivery.ClaimGeneration);
            }
            else
            {
                var taken = store.Take(delivery.Key, delivery.ClaimGeneration);
                if (taken.Claim is null)
                {
                    return store.Delivery(delivery.Key) is { Unresolved: false } now
                        ? Said(now, DeliveryStandings.Settled, $"it is settled {now.Settled}")
                        : Said(delivery, DeliveryStandings.Held, $"it is not settled and {taken.Refused}");
                }

                claim = taken.Claim;
            }

            return await SettleAsync(delivery, claim, ct).ConfigureAwait(false);
        }
        catch (DesktopStoreBusyException)
        {
            return Said(delivery, DeliveryStandings.Held, "the desktop's store was busy, so it is settled at the next look");
        }
        catch (DesktopStoreNewerException)
        {
            return Said(delivery, DeliveryStandings.Held, "a newer Daoris stamped the desktop's store, so it is left to it");
        }
        catch (DeliveryRefusedException lost)
        {
            return store.Delivery(delivery.Key) is { Unresolved: false } now
                ? Said(now, DeliveryStandings.Settled, $"it is settled {now.Settled}")
                : Said(delivery, DeliveryStandings.Held, $"it was not settled here ({lost.Why})");
        }
    }

    /// <summary>The delivery's phases settled under <paramref name="claim"/>: an open entry read and ended, then rule 1 from what is done.</summary>
    private async Task<DeliveryReconciled> SettleAsync(DeliveryRecord delivery, DeliveryClaim claim, CancellationToken ct)
    {
        var phases = store.Phases(delivery.Key);
        if (phases.FirstOrDefault(each => !DeliveryPhases.All.Contains(each.Phase, StringComparer.Ordinal)
                || each.Outcome is { } outcome && !DeliveryOutcomes.All.Contains(outcome, StringComparer.Ordinal)) is { } newer)
        {
            return Said(delivery, DeliveryStandings.Held, $"its entry {newer.Ordinal} was written by a newer Daoris, which settles it", claim);
        }

        if (phases.FirstOrDefault(each => each.Outcome == DeliveryOutcomes.Unknown) is { } unknown)
        {
            return Said(delivery, DeliveryStandings.Unknown, $"its `{unknown.Phase}` was read as unknown ({unknown.Said}): the person settles it, and nothing repeats it", claim);
        }

        if (phases.FirstOrDefault(each => each.Open) is { } open)
        {
            if (open.Phase is DeliveryPhases.PluginDispatched or DeliveryPhases.PluginAnswered) return await PluginAsync(delivery, claim, ct).ConfigureAwait(false);
            if (checkout(delivery) is not { } root) return Said(delivery, DeliveryStandings.Held, $"`{delivery.Intent.Repository}`'s checkout is not here, so its refs are read at the next look", claim);
            if (await ReadAsync(delivery, phases, open.Phase, root, ct).ConfigureAwait(false) is not { } read)
            {
                return Said(delivery, DeliveryStandings.Held, $"git could not read what its `{open.Phase}` came to, so it is settled at the next look", claim);
            }

            store.End(claim, open.Phase, read.Outcome, read.Evidence, read.Said);
            if (read.Outcome == DeliveryOutcomes.Unknown) return Said(delivery, DeliveryStandings.Unknown, $"{read.Said}: the person settles it, and nothing repeats it", claim);
            phases = store.Phases(delivery.Key);
        }

        return await GoOnAsync(delivery, claim, phases, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Rule 1, every entry's outcome recorded: delivered where its method's reaching phase is done; a merge's line read where its press got as
    /// far as the line; else its landing goes on from the first git phase not done, or its plugin's phases follow.
    /// </summary>
    private async Task<DeliveryReconciled> GoOnAsync(DeliveryRecord delivery, DeliveryClaim claim, IReadOnlyList<DeliveryPhase> phases, CancellationToken ct)
    {
        var intent = delivery.Intent;
        var done = phases.Where(each => each.Outcome == DeliveryOutcomes.Done).Select(each => each.Phase).ToHashSet(StringComparer.Ordinal);
        if (done.Contains(DeliveryPhases.ReachOf(intent.Method))) return Delivered(delivery, claim);
        var next = TreeDelivering.GitPhases.FirstOrDefault(each => !done.Contains(each));
        if (next is null) return await PluginAsync(delivery, claim, ct).ConfigureAwait(false);
        if (intent.Method != DeliveryMethods.Merge || next == DeliveryPhases.Prepared) return GoesOn(delivery, claim, next);

        if (checkout(delivery) is not { } root) return Said(delivery, DeliveryStandings.Held, $"`{intent.Repository}`'s checkout is not here, so its line is read at the next look", claim);
        var commit = Prepared(phases) ?? intent.Tip;
        var (holds, tip) = await LineAsync(root, intent.Line, commit, ct).ConfigureAwait(false);
        if (holds is null) return Said(delivery, DeliveryStandings.Held, $"git could not read whether `{intent.Line}` holds {Short(commit)}, so it is read at the next look", claim);
        if (next == DeliveryPhases.RefWritten)
        {
            if (holds == false)
            {
                return Waiting(intent, commit) switch
                {
                    true => Said(delivery, DeliveryStandings.Waits, $"its request waits in `{intent.Line}`'s queue, which takes it at a look: nothing is pressed again", claim),
                    false => GoesOn(delivery, claim, next),
                    null => Said(delivery, DeliveryStandings.Held, "whether its request still waits could not be read, so it is read at the next look", claim),
                };
            }

            // The line took the press's request after its press stopped waiting: what the press was refused with is reached now.
            store.Begin(claim, DeliveryPhases.RefWritten, $"`refs/heads/{intent.Line}` to hold {commit}");
            store.End(claim, DeliveryPhases.RefWritten, DeliveryOutcomes.Done, tip, $"`{intent.Line}` holds {commit}, taken by its line after its press");
        }
        else if (holds == false)
        {
            store.Begin(claim, DeliveryPhases.Recorded, $"`{intent.Line}` recorded holding {commit}");
            store.End(claim, DeliveryPhases.Recorded, DeliveryOutcomes.Unknown, tip, $"`{intent.Line}` no longer holds {commit}, which it held when its merge was read done");
            return Said(delivery, DeliveryStandings.Unknown, $"`{intent.Line}` no longer holds {Short(commit)}, which its merge was read holding: the person settles it, and nothing repeats it", claim);
        }

        store.Begin(claim, DeliveryPhases.Recorded, $"`{intent.Line}` recorded holding {commit}");
        store.End(claim, DeliveryPhases.Recorded, DeliveryOutcomes.Done, tip, $"`{intent.Line}` holds the commit at {tip}, read after its press");
        return Delivered(delivery, claim);
    }

    /// <summary>
    /// Rule 2, the open entry of <paramref name="phase"/> read: a preparation cut before any ref was written ahead is withdrawn, since nothing of
    /// it stands; a ref done where it stands at the intended tip, withdrawn where it is absent or stands where its record had it or at the tip
    /// judged, else unknown; a record done where it holds the branch at the tip its ref was written at, else withdrawn, to be written again. Null
    /// where git cannot say.
    /// </summary>
    private async Task<(string Outcome, string? Evidence, string Said)?> ReadAsync(
        DeliveryRecord delivery, IReadOnlyList<DeliveryPhase> phases, string phase, string root, CancellationToken ct)
    {
        var intent = delivery.Intent;
        if (phase == DeliveryPhases.Prepared)
        {
            return (DeliveryOutcomes.Withdrawn, null, "it stopped while it was prepared, before any ref was written ahead of it, so nothing of it stands");
        }

        var commit = Prepared(phases) ?? intent.Tip;
        if (intent.Method == DeliveryMethods.Merge)
        {
            var (holds, tip) = await LineAsync(root, intent.Line, commit, ct).ConfigureAwait(false);
            return holds switch
            {
                null => null,
                true => (DeliveryOutcomes.Done, tip, $"`{intent.Line}` holds {commit}, read after a stop"),
                false when phase == DeliveryPhases.RefWritten => (DeliveryOutcomes.Withdrawn, tip, $"`{intent.Line}` does not hold {commit}, read after a stop"),
                false => (DeliveryOutcomes.Unknown, tip, $"`{intent.Line}` no longer holds {commit}, which it held when its merge was read done"),
            };
        }

        var reference = $"refs/heads/{intent.Branch}";
        if (phase == DeliveryPhases.Recorded)
        {
            var wrote = phases.LastOrDefault(each => each.Phase == DeliveryPhases.RefWritten && each.Outcome == DeliveryOutcomes.Done)?.Evidence;
            var (read, at) = Recorded(intent);
            if (!read) return null;
            return wrote is not null && string.Equals(at, wrote, StringComparison.OrdinalIgnoreCase)
                ? (DeliveryOutcomes.Done, $"{LandedBranches.FileName}: `{intent.Branch}` at {wrote}", $"the landing record holds `{intent.Branch}` at {wrote}, read after a stop")
                : (DeliveryOutcomes.Withdrawn, null, $"the landing record does not hold `{intent.Branch}` at {wrote}, read after a stop, so it is written again");
        }

        var (known, stands) = await StandsAsync(root, reference, ct).ConfigureAwait(false);
        var (recordRead, recorded) = Recorded(intent);
        if (!known || !recordRead) return null;
        if (stands is not null && string.Equals(stands, commit, StringComparison.OrdinalIgnoreCase))
        {
            return (DeliveryOutcomes.Done, stands, $"`{reference}` stands at {commit}, read after a stop");
        }

        // A tree's own branch replayed in place is moved from the tip judged (LANDNODE1l), as an advance is from where its record had it.
        var stood = stands is null ? "absent"
            : string.Equals(stands, recorded, StringComparison.OrdinalIgnoreCase) ? $"at {stands}, where its landing record had it"
            : string.Equals(stands, intent.Tip, StringComparison.OrdinalIgnoreCase) ? $"at {stands}, the tip its landing was judged at"
            : null;
        if (stood is not null)
        {
            return (DeliveryOutcomes.Withdrawn, stands ?? "absent", $"`{reference}` stands {stood}, read after a stop: nothing of its write stands");
        }

        return (DeliveryOutcomes.Unknown, stands,
            $"`{reference}` stands at {stands}, neither at {commit}, which its delivery wrote ahead, nor where it stood: Daoris cannot tell what made it");
    }

    private Task<DeliveryReconciled> PluginAsync(DeliveryRecord delivery, DeliveryClaim claim, CancellationToken ct) =>
        Task.FromResult(Said(delivery, DeliveryStandings.Plugin,
            "its branch is made and recorded, and what followed was a landing plugin's, a wire Daoris no longer has: the branch is pushed by a workflow's `push` step or the person's press", claim));

    private DeliveryReconciled Delivered(DeliveryRecord delivery, DeliveryClaim claim)
    {
        store.Settle(claim, DeliverySettlements.Delivered, $"{delivery.Intent.Method} of {delivery.Intent.Tip} delivered, settled by phase");
        return Said(delivery, DeliveryStandings.Delivered, $"its {DeliveryPhases.ReachOf(delivery.Intent.Method)} is done, so it is delivered", claim);
    }

    private static DeliveryReconciled GoesOn(DeliveryRecord delivery, DeliveryClaim claim, string next) =>
        Said(delivery, DeliveryStandings.GoesOn, $"what it did so far is certain, and its landing goes on from `{next}`", claim) with { Next = next };

    private static DeliveryReconciled Said(DeliveryRecord delivery, string standing, string said, DeliveryClaim? claim = null) =>
        new(delivery.Key,
            standing,
            $"delivery {delivery.Key[..12]} of `{delivery.Intent.Unit}` ({delivery.Intent.Method} of {Short(delivery.Intent.Tip)} in `{delivery.Intent.Repository}`): {said}.")
        {
            Claim = claim,
        };

    /// <summary>The commit its done preparation names: the one its ref is written at, or the one its merge takes.</summary>
    private static string? Prepared(IReadOnlyList<DeliveryPhase> phases) =>
        phases.LastOrDefault(each => each.Phase == DeliveryPhases.Prepared && each.Outcome == DeliveryOutcomes.Done)?.Evidence;

    /// <summary>The tip the landing record has the intent's branch at: read and null where it has none; not read where it could not be.</summary>
    private (bool Read, string? At) Recorded(DeliveryIntent intent)
    {
        try
        {
            return (true, new LandedBranches(home).Of(intent.Repository, intent.Branch!)?.Tip);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return (false, null);
        }
    }

    /// <summary>Whether a merge request of <paramref name="commit"/> into the intent's line is still open; null where the store could not say.</summary>
    private bool? Waiting(DeliveryIntent intent, string commit)
    {
        try
        {
            return new IntegrationStore(home).Open().Any(request =>
                string.Equals(request.Repository, intent.Repository, StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.Tip, commit, StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.Line, intent.Line, StringComparison.Ordinal));
        }
        catch (Exception error) when (error is SqliteException or DesktopStoreException or IOException)
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="line"/> holds <paramref name="commit"/>, and where it stands; null where git cannot say.</summary>
    private static async Task<(bool? Holds, string? Tip)> LineAsync(string root, string line, string commit, CancellationToken ct)
    {
        var (_, tip) = await StandsAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false);
        var (code, _, _) = await WorkingTree.GitAsync(root, ["merge-base", "--is-ancestor", commit, $"refs/heads/{line}"], ct).ConfigureAwait(false);
        return (code switch { 0 => true, 1 => false, _ => null }, tip);
    }

    /// <summary>Where <paramref name="reference"/> stands: read and absent (null), read at a commit, or not read.</summary>
    private static async Task<(bool Read, string? At)> StandsAsync(string root, string reference, CancellationToken ct)
    {
        var (code, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
        return code switch { 0 => (true, at.Trim()), 1 => (true, null), _ => (false, null) };
    }

    private static string Short(string commit) => TextBounds.ShortCommit(commit, 8);
}
