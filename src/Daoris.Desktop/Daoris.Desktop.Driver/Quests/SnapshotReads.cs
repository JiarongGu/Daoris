using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What one tick of the look decides from, read through the service's client (<see cref="ServiceClient"/>, whose reads of the quests, the
/// registry and the sessions' records are the service's): the planner's own input, the work orders', assembled here, so every caller keeps
/// <c>client.SnapshotAsync()</c> as spelled and the client names neither the snapshot nor the planner's rules it is assembled by.
/// </summary>
public static class SnapshotReads
{
    extension(ServiceClient client)
    {
        /// <summary>Everything one tick decides from, fetched together so the plan is coherent.</summary>
        public async Task<Snapshot> SnapshotAsync(CancellationToken ct = default)
        {
            // PARALLEL1b (D181 point 3): the four reads asked at once, so one door slow to answer holds its own read, not the reads after it.
            // Each is still the service's answer as it stood when asked; the plan was never one transaction across them.
            var questsRead = client.GetAsync("/api/quests", ct);
            var registryRead = client.GetAsync("/api/registry", ct);
            var activeRead = client.GetAsync("/api/sessions", ct);
            // A second read rather than deriving both from one (DRV6): `/api/sessions` means ACTIVE, the
            // planner's "is this repository busy" rests on that, and re-deriving active-ness here would
            // put a second opinion about it on this side of the wire.
            var recordsRead = client.GetAsync("/api/sessions?includeClosed=true", ct);
            await Task.WhenAll(questsRead, registryRead, activeRead, recordsRead).ConfigureAwait(false);
            var quests = ServiceClient.ReadQuests(await questsRead.ConfigureAwait(false));
            var repositories = ServiceClient.ReadRegistry(await registryRead.ConfigureAwait(false));
            var active = ServiceClient.ReadSessions(await activeRead.ConfigureAwait(false));
            var records = await recordsRead.ConfigureAwait(false);
            var lastRun = ServiceClient.ReadLastRun(records);
            // The stand-downs a bring-forward would carry on (SYNCMERGE1b), which the last run never holds.
            var stoodDown = ServiceClient.ReadStoodDown(records);
            // The closed quests a session written to served (MSG1b, D137 §2.2), read only where one waits: no open list holds them.
            var open = quests.Select(quest => quest.Id).ToList();
            var closed = lastRun.Any(run => run.Value.WordsWaiting && !open.Contains(run.Key, StringComparer.OrdinalIgnoreCase))
                ? ServiceClient.ReadClosed(await client.GetAsync("/api/quests?includeClosed=true", ct).ConfigureAwait(false), lastRun, open)
                : [];
            var (claims, whys, forwards, taken) = await CarryOnClaimsAsync(client, quests, lastRun, stoodDown, ct).ConfigureAwait(false);
            return new Snapshot(quests, repositories, active, ServiceClient.ReadStrikes(records))
            {
                LastRun = lastRun,
                StoodDown = stoodDown,
                // The session a taken quest's reopen names, where a later one is its last here (FABRIC-CLOSE1c9): its words go on in it.
                NamedByReopen = ServiceClient.ReadNamedByReopen(records, quests, lastRun),
                Started = ServiceClient.ReadStarted(records, active),
                Closed = closed,
                // Whose the take is on each taken quest a look would carry on (CARRY2b), the claim asked of the host for those alone.
                Takes = ServiceClient.ReadTakes(records, claims, whys, forwards, taken),
                // Each record's entry, which a quest's publisher names its line by (SESSONE1d).
                Entries = ServiceClient.ReadEntries(records),
            };
        }
    }

    /// <summary>
    /// This machine's claim on each taken quest a look would carry on (CARRY2b): one that waits on nothing, whose last session
    /// here ended as a carry-on goes on from (<see cref="Planner.CarriesOn"/>), a stop the person still holds included, which
    /// the planner holds by the stop first. A claim the host does not answer is unread, and the carry-on is planned as before. A
    /// step the person's reopen holds is not asked (FABRIC-CLOSE1c2): nothing carries it on until their go-on, which the next look reads.
    /// And one whose newest record here stood down (<paramref name="stoodDown"/>, SYNCMERGE1b): its claim names the person's bring-forward,
    /// where one made the take, which carries that record on.
    /// </summary>
    private static async Task<(IReadOnlyDictionary<string, string> Claims, IReadOnlyDictionary<string, string?> Whys,
        IReadOnlyDictionary<string, ForwardTake?> Forwards, IReadOnlyDictionary<string, DateTimeOffset?> Taken)> CarryOnClaimsAsync(
        ServiceClient client, IReadOnlyList<QuestView> quests, IReadOnlyDictionary<string, PriorSession> lastRun, IReadOnlyDictionary<string, PriorSession> stoodDown,
        CancellationToken ct)
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var whys = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var forwards = new Dictionary<string, ForwardTake?>(StringComparer.OrdinalIgnoreCase);
        var taken = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);
        var asked = quests
            .Where(quest => quest is { Status: "Taken", Awaits: null or "", HeldByReopen: false }
                            && ((lastRun.TryGetValue(quest.Id, out var last) && Planner.CarriesOn(last)) || (stoodDown.ContainsKey(quest.Id) && HeldHere(quest))))
            .ToArray();

        // PARALLEL1b (D181 point 3): asked at once, at most ClaimsAtOnce together, so a host slow to answer for one quest holds that
        // question alone, not the snapshot's every other.
        await Parallel.ForEachAsync(
            asked, new ParallelOptions { MaxDegreeOfParallelism = ClaimsAtOnce, CancellationToken = ct },
            async (quest, token) =>
            {
                try
                {
                    var (claim, why, forward, at) = await client.ClaimTakeAsync(quest.Id, token).ConfigureAwait(false);
                    lock (claims)
                    {
                        claims[quest.Id] = claim;
                        whys[quest.Id] = why;
                        forwards[quest.Id] = forward;
                        taken[quest.Id] = at;
                    }
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
                                                  // The client's own timeout (DEV3a): a host that took the question and stalled.
                                                  || (error is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    // Unread: the ledger judges the carry-on, as it did before the planner asked. A stand-down unread carries nothing.
                }
            }).ConfigureAwait(false);

        return (claims, whys, forwards, taken);
    }

    /// <summary>
    /// Whether the quest list says this machine holds the quest (SYNCMERGE1b1): its holder is this machine's id. A stood-down quest held
    /// elsewhere, or by no press, has no bring-forward of this machine's to read, so its claim is not asked.
    /// </summary>
    private static bool HeldHere(QuestView quest) =>
        quest.Holder is { } holder && string.Equals(holder, quest.Here, StringComparison.Ordinal);

    /// <summary>How many claim questions a snapshot asks the host together (PARALLEL1b): local, and each one small.</summary>
    private const int ClaimsAtOnce = 4;
}
