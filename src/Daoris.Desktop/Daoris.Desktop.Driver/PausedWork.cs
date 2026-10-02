namespace Daoris.Driver;

/// <summary>
/// Whose pause holds a quest (PAUSE1b, D132 point 3): an ask's, over every quest of its work, or a quest's own, over its
/// work. What the planner's <see cref="StartVerdict.Paused"/> names, and what *Resume* takes away.
/// </summary>
/// <param name="Id">The ask's or the quest's id, as <c>driver.json</c> spells it.</param>
public sealed record PausedBy(WorkScope Scope, string Id)
{
    /// <summary><c>ask</c> or <c>quest</c>: the scope's public spelling, which the page and the machine log read.</summary>
    public string Word => Scope == WorkScope.Ask ? "ask" : "quest";

    /// <summary>The terminal's door that resumes it (D50, design §7.2).</summary>
    public string Door => Scope == WorkScope.Ask ? $"daoris-driver ask --resume {Id}" : $"daoris-driver quest resume {Id}";

    /// <summary>
    /// What the record of a session this pause stopped says (design §4.1): a person reads it, and nothing decides from it
    /// (D104). Never translated: a record's note is data.
    /// </summary>
    public string Note => Scope == WorkScope.Ask ? $"paused with ask `#{Id}`." : $"paused with quest `#{Id}`.";
}

/// <summary>
/// The look's half of a pause (PAUSE1b, design §2.3): which quests the pauses in <c>driver.json</c> hold now. The planner reads
/// the answer and never a second copy of the work's rule, which is <see cref="AskWork"/>'s.
/// </summary>
/// <remarks>
/// <b>Read again at every look</b> (design §1): a chain's step published by a close, or a question asked a second before the
/// pause, joins the work as it appears, so nothing of a paused work slips past a pause made a moment earlier.
/// </remarks>
public static class PausedWork
{
    /// <summary>
    /// Every quest of each paused work, against the pause that holds it. A quest's own pause is said before its ask's: it is
    /// the narrower, and the one the person made of that quest.
    /// </summary>
    public static IReadOnlyDictionary<string, PausedBy> Set(AskWorkLook look, DriverConfig config)
    {
        var paused = new Dictionary<string, PausedBy>(StringComparer.OrdinalIgnoreCase);
        foreach (var (scope, pauses) in new[] { (WorkScope.Quest, config.PausedQuests), (WorkScope.Ask, config.PausedAsks) })
        {
            // An id that names nothing once its `#` and spaces are off is no work: the file says so, and the look goes on.
            foreach (var id in pauses.Keys.Where(id => id.Trim().TrimStart('#').Trim().Length > 0))
            {
                // A pause names its work; the trees are not asked about, so no path is judged here.
                foreach (var quest in AskWork.Read(look, scope, id, _ => false).Quests)
                {
                    paused.TryAdd(quest.Quest.Id, new PausedBy(scope, id));
                }
            }
        }

        return paused;
    }

    /// <summary>
    /// The snapshot with its paused set, read from the service's quests and records where <c>driver.json</c> holds a pause,
    /// and as it was where it holds none, which asks nothing more of the service.
    /// </summary>
    /// <remarks>
    /// 🔴 A read that fails fails the look: a paused work read as no work would start what the person paused.
    /// </remarks>
    public static async Task<Snapshot> LookAsync(ServiceClient service, DriverConfig config, Snapshot snapshot, CancellationToken ct = default)
    {
        if (config.PausedAsks.Count == 0 && config.PausedQuests.Count == 0) return snapshot;
        var look = new AskWorkLook(
            await service.EveryQuestAsync(ct).ConfigureAwait(false), await service.SessionRecordsAsync(ct).ConfigureAwait(false));
        return snapshot with { Paused = Set(look, config) };
    }
}
