using System.Text.Json;
using static Daoris.Driver.BranchTidying;

namespace Daoris.Driver;

/// <summary>The trees sessions hold, which the look's tidy and a checkout's writer leave alone: running, waiting, spoken to, reviewed.</summary>
internal static class TidyHolders
{
    /// <summary>
    /// The trees the look's tidy keeps whatever they hold: a session running or waiting holds its tree; so does one that ended with the
    /// person's words waiting on its record, which the next loop goes on with there; one the person spoke to
    /// within <see cref="SpokenWithin"/>, whose next word goes on there; and a review step in progress, by the folder it shows
    /// from. The pass's and link 2's alike.
    /// </summary>
    /// <param name="now">What time it is, for <see cref="SpokenWithin"/>; the system's where none is said.</param>
    public static async Task<IReadOnlySet<string>> InUseAsync(
        string home, ServiceClient service, Func<CancellationToken, Task<IReadOnlyCollection<string>>>? reviewing, CancellationToken ct,
        DateTimeOffset? now = null)
    {
        var held = (await ReadHoldersAsync(home, service, writing: false, ct, now ?? DateTimeOffset.UtcNow).ConfigureAwait(false))
            .Select(each => each.Tree).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (reviewing is not null) held.UnionWith(TreesOf(new SessionTrees(home).TreesRoot, await reviewing(ct).ConfigureAwait(false)));
        return held;
    }

    /// <summary>
    /// The sessions of this machine the person spoke to within <see cref="SpokenWithin"/> of <paramref name="now"/>, with their trees. A record
    /// whose last move is older is passed without reading its events, since a word said to it moved it.
    /// </summary>
    private static IEnumerable<TreeHolder> SpokenTo(string home, IEnumerable<PriorSession> records, DateTimeOffset now)
    {
        var events = WorkplaceEventRecords.Current.Open(DaorisHome.Entry(home, HomeEntry.Sessions));
        return records
            .Where(record => !record.Teammate && record.Tree is { Length: > 0 } && record.Updated is { } moved && now - moved < SpokenWithin)
            .Where(record => events.LastPersonAt(record.Session) is { } said && now - said < SpokenWithin)
            .Select(record => new TreeHolder(record.Tree!, record.Session));
    }

    /// <summary>
    /// The trees sessions hold, for every door that removes or rebases one: a session running or waiting on the person, a
    /// record the person's words or its own helper wait on, and the newest part of each quest still open, whatever that part's state but
    /// declined, since its line goes on in that tree (a retry waiting on a limit among them). A tree is judged by every session of its quest, never only by
    /// the run that opened it. A tree a step's hand-off reserves is held too, by that step.
    /// </summary>
    public static async Task<HashSet<string>> HeldTreesAsync(string home, ServiceClient service, CancellationToken ct) =>
        (await HoldersAsync(home, service, ct).ConfigureAwait(false)).Select(each => each.Tree).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The trees sessions hold, as <see cref="HeldTreesAsync"/> reads them, each with a session that holds it: what a writer of
    /// a checkout names when it waits for the session working there. Thrown where the service does not answer, which a writer reads as held.
    /// </summary>
    public static Task<IReadOnlyList<TreeHolder>> HoldersAsync(string home, ServiceClient service, CancellationToken ct) =>
        ReadHoldersAsync(home, service, writing: false, ct);

    /// <summary>
    /// The sessions a writer of a checkout waits for: <see cref="HoldersAsync"/>'s, except that a quest's newest part counts only
    /// while it will go on (never one completed, declined, or failed with no limit to wait out), since deferring a write to the person's
    /// checkout is not keeping a tree from deletion.
    /// </summary>
    public static Task<IReadOnlyList<TreeHolder>> BesideAsync(string home, ServiceClient service, CancellationToken ct) =>
        ReadHoldersAsync(home, service, writing: true, ct);

    /// <summary>The trees open hand-offs reserve, each held by its step, so every door that reads the holders keeps off it.</summary>
    private static IEnumerable<TreeHolder> Reserved(string home) =>
        new TreeHandOffs(home).Open().Select(row => new TreeHolder(row.Tree, row.Session ?? "") { Step = $"run `{row.Run}`'s `resolve`" });

    /// <param name="spoken">The time the look's tidy keeps the sessions the person spoke to by; null for every other door.</param>
    private static async Task<IReadOnlyList<TreeHolder>> ReadHoldersAsync(
        string home, ServiceClient service, bool writing, CancellationToken ct, DateTimeOffset? spoken = null)
    {
        // Asked at once, as the snapshot asks its reads: each is the whole list on a home of thousands of records, so one after another
        // the three cost their sum where together they cost the slowest.
        var activeRead = service.ActiveSessionsAsync(ct);
        var recordsRead = service.RecordsAsync(ct);
        var questsRead = service.EveryQuestAsync(ct);
        await Task.WhenAll(activeRead, recordsRead, questsRead).ConfigureAwait(false);
        var held = (await activeRead.ConfigureAwait(false))
            .Where(session => session.Tree is { Length: > 0 })
            .Select(session => new TreeHolder(session.Tree!, session.Id)
            {
                Leases = PlaceLeases.Holds(session), Runs = PlaceLeases.Holds(session) && !SessionStates.IsParked(session.State),
            }).ToList();
        var records = await recordsRead.ConfigureAwait(false);
        held.AddRange(records.Where(record => !record.Teammate && record.WordsWaiting && record.Tree is { Length: > 0 })
            .Select(record => new TreeHolder(record.Tree!, record.Session)));
        if (spoken is { } now) held.AddRange(SpokenTo(home, records, now));
        held.AddRange(Reserved(home));
        var waiting = records.Where(record => HelperWaitTrees.HoldsTree(record) && record.Tree is { Length: > 0 } && record.Quest is { Length: > 0 }).ToList();
        var quests = await questsRead.ConfigureAwait(false);
        var taken = quests.Where(quest => quest.Status == "Taken").Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        held.AddRange(waiting.Where(record => taken.Contains(record.Quest!)).Select(record => new TreeHolder(record.Tree!, record.Session)));
        held.AddRange(LineHolders(records, quests, goingOn: writing));
        return held;
    }

    /// <summary>Whether a part that is not running will still go on: a limit it waits out, words it waits for, or a stop held for the person.</summary>
    private static bool GoesOn(PriorSession record) =>
        record.WordsWaiting || record.Limit
        || !(record.State.Equals("completed", StringComparison.OrdinalIgnoreCase) || record.State.Equals("declined", StringComparison.OrdinalIgnoreCase)
             || record.State.Equals("failed", StringComparison.OrdinalIgnoreCase));

    private static bool Declined(PriorSession record) => record.State.Equals("declined", StringComparison.OrdinalIgnoreCase);

    /// <summary>The tree of the newest part of each quest still open or taken, unless it declined (never carried on); a teammate's is none.</summary>
    public static IEnumerable<string> LineTrees(IEnumerable<PriorSession> records, IEnumerable<QuestView> quests) =>
        LineHolders(records, quests).Select(each => each.Tree);

    /// <summary>The trees of the newest part of each open or taken quest that will still go on: what <see cref="BesideAsync"/> adds to the running sessions.</summary>
    public static IEnumerable<string> LineWaiters(IEnumerable<PriorSession> records, IEnumerable<QuestView> quests) =>
        LineHolders(records, quests, goingOn: true).Select(each => each.Tree);

    private static IEnumerable<TreeHolder> LineHolders(IEnumerable<PriorSession> records, IEnumerable<QuestView> quests, bool goingOn = false)
    {
        var open = quests.Where(quest => quest.Status is "Open" or "Taken").Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return records
            .Where(record => !record.Teammate && record.Quest is { Length: > 0 } quest && open.Contains(quest))
            .GroupBy(record => record.Quest!, StringComparer.OrdinalIgnoreCase)
            .Select(line => line.MaxBy(record => record.Created ?? DateTimeOffset.MinValue)!)
            .Where(record => record.Tree is { Length: > 0 } && !Declined(record) && (!goingOn || GoesOn(record)))
            .Select(record => new TreeHolder(record.Tree!, record.Session));
    }

    /// <summary>
    /// The trees of this machine's sessions that ended waiting on their own helper while their quest is still taken: each goes on in its tree with the answer, or parks for the person, so it keeps it as a park does. One whose quest
    /// closed meanwhile holds nothing.
    /// </summary>
    /// <remarks>
    /// 🔴 Never read off an ask being open. Its last ask refused and its move back to the person failed,
    /// the record is still completed and waiting, with no ask open, and the look tries the move again; until that move lands (it is then
    /// active), it goes on, or its quest is no longer taken, its tree and its lock are kept. The service's holder reads the same
    /// (<c>SessionLedger.HolderAsync</c>), as the planner's does (<see cref="Planner.WaitingInTree"/>).
    /// </remarks>
    public static async Task<IEnumerable<string>> WaitingOnHelpersAsync(ServiceClient service, CancellationToken ct)
    {
        var waiting = (await service.RecordsAsync(ct).ConfigureAwait(false))
            .Where(record => HelperWaitTrees.HoldsTree(record) && record.Tree is { Length: > 0 } && record.Quest is { Length: > 0 })
            .ToList();
        if (waiting.Count == 0) return [];
        var taken = (await service.EveryQuestAsync(ct).ConfigureAwait(false))
            .Where(quest => quest.Status == "Taken").Select(quest => quest.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return waiting.Where(record => taken.Contains(record.Quest!)).Select(record => record.Tree!);
    }
}
