using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Every kind of work read as one list of <see cref="WorkUnit"/>s: the branch adapter
/// (<see cref="WorkInventory.Units"/>), the quest adapter (<see cref="QuestUnits"/>) and the ask adapter (<see cref="AskUnits"/>), and
/// the links between them. Read-only throughout: the home's records as they stand, and the service's by GET; no store, no file, no write.
/// </summary>
public static class WorkUnits
{
    /// <summary>
    /// The units of every kind, linked: a session that served a quest worked the branch unit of its tree, so that unit is produced by
    /// the quest; and each link with an inverse (<see cref="WorkRelation.Inverse"/>) is kept both ways where both ends were read. A link
    /// names the other unit by its id and copies nothing of it. Where a session's tree names no one unit alone, none is linked by it.
    /// </summary>
    public static IReadOnlyList<WorkUnit> Linked(
        IReadOnlyList<WorkUnit> branches, IReadOnlyList<WorkUnit> quests, IReadOnlyList<WorkUnit> asks, IReadOnlyList<SessionRecord> sessions)
    {
        var all = branches.Concat(quests).Concat(asks).ToList();
        var links = new Dictionary<string, List<WorkLink>>(StringComparer.Ordinal);
        foreach (var unit in all) links.TryAdd(unit.Id, [.. unit.Links]);

        foreach (var session in sessions)
        {
            if (session.Quest is not { Length: > 0 } quest || WorkedBy(branches, session) is not { } worked) continue;
            Add(links[worked.Id], new WorkLink(WorkRelation.ProducedBy, QuestUnits.IdOf(quest)));
        }

        foreach (var unit in all)
        {
            foreach (var link in links[unit.Id].ToList())
            {
                if (WorkRelation.Inverse(link.Relation) is { } inverse && links.TryGetValue(link.Unit, out var other))
                {
                    Add(other, new WorkLink(inverse, unit.Id));
                }
            }
        }

        return [.. all.Select(unit => unit with { Links = links[unit.Id] })];
    }

    /// <summary>
    /// Every unit this home and its service name, a branch executed by the sessions whose records name its tree. Without the service, or
    /// where it does not answer, the branch units alone, and <paramref name="unread"/> says so: never read as there being no quests.
    /// </summary>
    /// <param name="roots">Each repository's checkout, for the recovery refs (<see cref="WorkInventories.GatherAsync"/>).</param>
    /// <param name="inUse">The trees a session still running or waiting holds.</param>
    /// <param name="unlinked">
    /// Where given, why the links sessions make were not read: the service's lines of
    /// <paramref name="unread"/> alone, which the node runtime holds a run's branches on, never the inventory's.
    /// </param>
    public static async Task<IReadOnlyList<WorkUnit>> ReadAsync(
        string home, IReadOnlyDictionary<string, string> roots, IReadOnlySet<string> inUse, ServiceClient? service, ICollection<string> unread,
        CancellationToken ct = default, ICollection<string>? unlinked = null)
    {
        async Task<IReadOnlyList<WorkUnit>> BranchesAsync(IReadOnlyList<SessionRecord>? sessions) => WorkInventory.Units(await WorkInventories.GatherAsync(
            home, roots, inUse, unread, ct, recorded: sessions is null ? null : WorkInventories.RecordedOf(sessions)).ConfigureAwait(false));

        if (service is null)
        {
            var alone = await BranchesAsync(null).ConfigureAwait(false);
            unread.Add("without the service, no quest or ask is read: only this machine's branch work is");
            unlinked?.Add("without the service, no quest or ask is read");
            return alone;
        }

        IReadOnlyList<QuestView> quests;
        IReadOnlyList<AskView> asks;
        IReadOnlyList<SessionRecord> sessions;
        try
        {
            // Asked at once: each is a whole list, so together they cost the slowest of them rather than their sum.
            var questsRead = service.EveryQuestAsync(ct);
            var asksRead = service.EveryAskAsync(ct);
            var sessionsRead = service.SessionRecordsAsync(ct);
            await Task.WhenAll(questsRead, asksRead, sessionsRead).ConfigureAwait(false);
            (quests, asks, sessions) = (await questsRead.ConfigureAwait(false), await asksRead.ConfigureAwait(false), await sessionsRead.ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
            || (error is TaskCanceledException && !ct.IsCancellationRequested))
        {
            var alone = await BranchesAsync(null).ConfigureAwait(false);
            unread.Add($"the service did not answer, so its quests and asks were not read: {error.Message}");
            unlinked?.Add($"the service did not answer: {error.Message}");
            return alone;
        }

        var branches = await BranchesAsync(sessions).ConfigureAwait(false);
        // A quest's or an ask's receipts and attempts are its own, as a branch unit's are (WorkInventory's facts).
        var owed = WorkObligations.Read(home, DateTimeOffset.UtcNow);
        return Linked(branches, WorkObligations.Attach(QuestUnits.Read(quests, sessions), owed), WorkObligations.Attach(AskUnits.Read(asks), owed), sessions);
    }

    /// <summary>
    /// The branch units alone of the repositories <paramref name="only"/> holds: no quest, ask or session is asked of
    /// the service, and no link is made. For a pass whose only work is a branch's <c>integrate</c>, which reads nothing else of any repository.
    /// </summary>
    public static async Task<IReadOnlyList<WorkUnit>> BranchesAsync(
        string home, IReadOnlySet<string> inUse, Func<string, bool> only, ICollection<string> unread, CancellationToken ct = default) =>
        WorkInventory.Units(await WorkInventories.GatherAsync(home, new Dictionary<string, string>(), inUse, unread, ct, only: only).ConfigureAwait(false));

    /// <summary>
    /// The one branch unit a session's work is: the unit holding its tree, or the one on the session branch its tree's name makes in
    /// its repository; else the one unit naming it as an executor. Two candidates are none.
    /// </summary>
    private static WorkUnit? WorkedBy(IReadOnlyList<WorkUnit> branches, SessionRecord session)
    {
        if (session.Tree is { Length: > 0 } tree)
        {
            var full = Full(tree);
            var named = TreeFollows.SessionPrefix + Path.GetFileName(full);
            var byTree = branches.Where(unit =>
                unit.Entries.Any(entry => entry.Kind == InventoryKind.Tree && string.Equals(Full(entry.Resource), full, StringComparison.OrdinalIgnoreCase))
                || (unit.OwnerKnown && unit.Subject.Artifact == named
                    && string.Equals(unit.Owner.Repository, session.Repository, StringComparison.OrdinalIgnoreCase))).ToList();
            if (byTree.Count > 0) return byTree.Count == 1 ? byTree[0] : null;
        }

        var byExecutor = branches.Where(unit => unit.Executors.Contains(session.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        return byExecutor.Count == 1 ? byExecutor[0] : null;
    }

    private static void Add(List<WorkLink> links, WorkLink link)
    {
        if (!links.Contains(link)) links.Add(link);
    }

    private static string Full(string path)
    {
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return path; }
    }
}
