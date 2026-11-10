namespace Daoris.Driver;

/// <summary>
/// The parts of an entry that are one run's work. A line keyed by an ask holds the sessions of every quest the ask sent to its repository,
/// and each quest is a run of its own unless its chain leads back to an earlier quest there (<see cref="WorkflowRunBindings.RunOf"/>), so
/// a part joins the branch of its run's first part, never another run's.
/// </summary>
public static class RunParts
{
    /// <summary>The most parent steps a chain is walked up, as the landing's name walks it (<see cref="BranchPatterns.SubjectAsync"/>).</summary>
    private const int Steps = 20;

    /// <summary>
    /// The first part of <paramref name="session"/>'s entry that is of its run, where its run has another part there; null where it is alone in
    /// its run, or has no record. A conversation's parts, serving no quest, are one run.
    /// </summary>
    /// <param name="find">A quest by id, as the service holds it, for the chain's walk.</param>
    public static async Task<SessionRecord?> LeadOfAsync(
        IReadOnlyList<SessionRecord> records, string session, Func<string, Task<QuestView?>> find)
    {
        if (records.FirstOrDefault(record => record.Id == session) is not { } own) return null;
        var run = await RunOfAsync(own.Quest, find).ConfigureAwait(false);
        var parts = new List<SessionRecord>();
        foreach (var part in records.Where(record => string.Equals(record.EntryKey, own.EntryKey, StringComparison.Ordinal)))
        {
            if (string.Equals(await RunOfAsync(part.Quest, find).ConfigureAwait(false), run, StringComparison.Ordinal)) parts.Add(part);
        }

        var ordered = parts.OrderBy(record => record.Created).ThenBy(record => record.Id, StringComparer.Ordinal).ToList();
        return ordered.Count > 1 ? ordered[0] : null;
    }

    /// <summary>
    /// The run a quest's work is: its chain's first quest in its repository, the chain read up its parents while the service answers for
    /// them; the quest itself where it is not found; null for none.
    /// </summary>
    public static async Task<string?> RunOfAsync(string? quest, Func<string, Task<QuestView?>> find)
    {
        if (quest?.TrimStart('#') is not { Length: > 0 } id) return null;
        if (await find(id).ConfigureAwait(false) is not { } current) return id;
        var chain = new List<QuestView> { current };
        for (var step = 0; step < Steps && chain[0].Parent is { Length: > 0 } parent; step++)
        {
            if (await find(parent).ConfigureAwait(false) is not { } above) break;
            chain.Insert(0, above);
        }

        return WorkflowRunBindings.RunOf(chain, current.To) ?? id;
    }
}
