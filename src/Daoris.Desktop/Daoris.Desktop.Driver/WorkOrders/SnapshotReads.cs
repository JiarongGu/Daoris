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
            // The four reads asked at once, so one door slow to answer holds its own read, not the reads after it.
            // Each is still the service's answer as it stood when asked; the plan is not one transaction across them.
            var questsRead = client.SendAsync(new Daoris.Contracts.Quests.ListQuests(), ct);
            var registryRead = client.RegistryAsync(ct);
            var activeRead = client.SessionList.ListedAsync(false, ct);
            // A second read rather than deriving both from one: `/api/sessions` means ACTIVE, the
            // planner's "is this repository busy" rests on that, and re-deriving active-ness here would
            // put a second opinion about it on this side of the wire.
            var recordsRead = client.SessionList.ListedAsync(true, ct);
            await Task.WhenAll(questsRead, registryRead, activeRead, recordsRead).ConfigureAwait(false);
            IReadOnlyList<QuestView> quests = [.. (await questsRead.ConfigureAwait(false)).Answered().Select(QuestViews.View)];
            var repositories = await registryRead.ConfigureAwait(false);
            var active = SessionReads.Views(await activeRead.ConfigureAwait(false));
            var records = await recordsRead.ConfigureAwait(false);
            var lastRun = SessionReads.LastRun(records);
            // The stand-downs a bring-forward would carry on, which the last run never holds.
            var stoodDown = SessionReads.StoodDown(records);
            // The closed quests a session written to served, read only where one waits: no open list holds them.
            var open = quests.Select(quest => quest.Id).ToList();
            var closed = lastRun.Any(run => run.Value.WordsWaiting && !open.Contains(run.Key, StringComparer.OrdinalIgnoreCase))
                ? SessionReads.Closed(await client.EveryQuestAsync(ct).ConfigureAwait(false), lastRun, open)
                : [];
            var (claims, whys, forwards, taken) = await CarryOnClaimsAsync(client, quests, lastRun, stoodDown, ct).ConfigureAwait(false);
            return new Snapshot(quests, repositories, active, SessionReads.Strikes(records))
            {
                LastRun = lastRun,
                FailedRuns = SessionReads.FailedRuns(records),
                StoodDown = stoodDown,
                // The session a taken quest's reopen names, where a later one is its last here: its words go on in it.
                NamedByReopen = SessionReads.NamedByReopen(records, quests, lastRun),
                Started = SessionReads.Started(records, active),
                Closed = closed,
                // Whose the take is on each taken quest a look would carry on, the claim asked of the host for those alone.
                Takes = SessionReads.Takes(records, claims, whys, forwards, taken),
                // Each record's entry, which a quest's publisher names its line by.
                Entries = SessionReads.Entries(records),
            };
        }
    }

    /// <summary>
    /// This machine's claim on each taken quest a look would carry on: one that waits on nothing, whose last session
    /// here ended as a carry-on goes on from (<see cref="Planner.CarriesOn"/>), a stop the person still holds included, which
    /// the planner holds by the stop first. A claim the host does not answer is unread, and the carry-on is planned without it. A
    /// step the person's reopen holds is not asked: nothing carries it on until their go-on, which the next look reads.
    /// And one whose newest record here stood down (<paramref name="stoodDown"/>): its claim names the person's bring-forward,
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

        // Asked at once, at most ClaimsAtOnce together, so a host slow to answer for one quest holds that
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
                                                  // The client's own timeout: a host that took the question and stalled.
                                                  || (error is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    // Unread: the ledger judges the carry-on. A stand-down unread carries nothing.
                }
            }).ConfigureAwait(false);

        return (claims, whys, forwards, taken);
    }

    /// <summary>
    /// Whether the quest list says this machine holds the quest: its holder is this machine's id. A stood-down quest held
    /// elsewhere, or by no press, has no bring-forward of this machine's to read, so its claim is not asked.
    /// </summary>
    private static bool HeldHere(QuestView quest) =>
        quest.Holder is { } holder && string.Equals(holder, quest.Here, StringComparison.Ordinal);

    /// <summary>How many claim questions a snapshot asks the host together: local, and each one small.</summary>
    private const int ClaimsAtOnce = 4;
}
