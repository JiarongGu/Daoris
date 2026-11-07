namespace Daoris.Driver;

/// <summary>
/// The chains of a list of quests, from an index of them made once: a reader that asks the chain of every quest of a list pays for the
/// index once, where a scan of the list for each ask grew with the square of the list.
/// </summary>
public sealed class QuestChains
{
    private readonly Dictionary<string, QuestView> _byId;

    private readonly ILookup<string, QuestView> _children;

    public QuestChains(IReadOnlyList<QuestView> quests)
    {
        _byId = quests.GroupBy(quest => quest.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _children = quests.Where(quest => quest.Parent is { Length: > 0 }).ToLookup(quest => quest.Parent!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The chain a quest is a step of: the first quest, then every step published after, in the order the quests are listed. A quest the list
    /// does not hold has no chain here.
    /// </summary>
    public IReadOnlyList<QuestView> Of(string id)
    {
        if (!_byId.TryGetValue(id.TrimStart('#'), out var first)) return [];

        // Up to the first quest; a parent seen twice, which no chain makes, ends the walk rather than looping.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { first.Id };
        while (first.Parent is { Length: > 0 } parent && seen.Add(parent) && _byId.TryGetValue(parent, out var above)) first = above;

        var chain = new List<QuestView> { first };
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { first.Id };
        for (var next = 0; next < chain.Count; next++)
        {
            foreach (var step in _children[chain[next].Id])
            {
                if (visited.Add(step.Id)) chain.Add(step);
            }
        }

        return chain;
    }
}
