namespace Daoris.Driver;

/// <summary>
/// The owed opinions a run's node begun reaches at the gate (<c>node.begun</c>): a graph's opinion node holds its own run's ask until it
/// begins, so a node begun reaches the owed opinions whose work is that run's, never the rest. Every node of every run begins one, and
/// a round of every owed opinion for each, one judgment of a repository at a time, kept the gate about 42 minutes on a node begun on a
/// loaded install, past its deadline.
/// </summary>
internal static class NodeBegunReach
{
    /// <summary>
    /// The owed opinions of <paramref name="open"/> a node begun on <paramref name="unit"/> reaches: a chat run's its own; a quest's run,
    /// the first quest of its chain in a repository, those whose quest sits in that chain, a superset of the opinions it is the run of.
    /// A run no listed quest is (an ask's delivery run) reaches only an opinion owed on its own id. Null where it cannot be named, and
    /// every owed opinion is judged: a unit that is no run's, or a quest list that did not read.
    /// </summary>
    public static IReadOnlyList<OpinionDue>? Reached(string unit, IReadOnlyList<OpinionDue> open, IReadOnlyList<QuestView>? quests)
    {
        if (!RunUnits.Is(unit) || unit.Length == RunUnits.Prefix.Length) return null;
        var run = unit[RunUnits.Prefix.Length..];
        if (WorkplaceRunFiles.IsChatRun(run)) return [.. open.Where(due => due.OnRun && string.Equals(due.Run, run, StringComparison.OrdinalIgnoreCase))];
        if (quests is null) return null;

        var chain = new HashSet<string>(new QuestChains(quests).Of(run).Select(quest => quest.Id), StringComparer.OrdinalIgnoreCase) { run };
        return [.. open.Where(due => !due.OnRun && chain.Contains(due.Quest.TrimStart('#')))];
    }
}
