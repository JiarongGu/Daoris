namespace Daoris.Knowledge;

/// <summary>Whether the lanes an address names are the receiver's, and the words that say which are.</summary>
internal static class QuestLanes
{
    /// <summary>
    /// Whether the lanes an address names are the receiver's: each must be one its
    /// registration declares, matched in any case and kept in the declared spelling, once, sorted. Any
    /// lane of a repository that declares none is refused, saying to address the repository; an
    /// unknown lane is refused, naming the lanes there are and how to address one.
    /// </summary>
    /// <returns>The lanes to keep on the quest, or why the address cannot be taken.</returns>
    public static (IReadOnlyList<string> Lanes, string? Refusal) Judge(QuestAddress address, Registration target)
    {
        if (!address.Named) return ([], null);

        var asked = $"{address.Repository}:{string.Join('+', address.Lanes)}";
        var declared = target.DeclaredLanes;
        if (declared.Count == 0)
        {
            return ([], $"`{target.Repository}` declares no lanes, so a quest to it addresses the repository: "
                        + $"`{target.Repository}`, not `{asked}`.");
        }

        var theirs = LaneList(declared);
        var how = HowToAddress(target.Repository, declared);
        if (address.Lanes.Count == 0)
        {
            return ([], $"`{asked}` names no lane after the colon. `{target.Repository}`'s lanes: {theirs}. {how}");
        }

        var kept = new List<string>();
        var unknown = new List<string>();
        foreach (var name in address.Lanes)
        {
            if (declared.FirstOrDefault(lane => string.Equals(lane.Id, name, StringComparison.OrdinalIgnoreCase)) is { } lane)
            {
                if (!kept.Contains(lane.Id, StringComparer.Ordinal)) kept.Add(lane.Id);
            }
            else if (!unknown.Contains(name, StringComparer.Ordinal))
            {
                unknown.Add(name);
            }
        }

        if (unknown.Count > 0)
        {
            return ([], $"`{target.Repository}` declares no lane {string.Join(" or ", unknown.Select(name => $"`{name}`"))}. "
                        + $"Its lanes: {theirs}. {how}");
        }

        return (kept.Order(StringComparer.Ordinal).ToList(), null);
    }

    /// <summary>
    /// What would let a repository that declares no lanes hand its own work out: the
    /// same words at the self-address refusal and on its registry row.
    /// </summary>
    public static string NoLanesWay(string repository) =>
        $"`{repository}` declares no lanes: a `daoris.lanes.json` with a steward lane is what lets it dispatch its own "
        + $"work as Daoris sessions, each a quest to `{repository}:<lane>`.";

    public static string LaneList(IReadOnlyList<DeclaredLane> declared) =>
        string.Join(", ", declared.Select(lane => $"`{lane.Id}`" + (lane.Title.Length > 0 ? $" ({lane.Title})" : "")));

    public static string HowToAddress(string repository, IReadOnlyList<DeclaredLane> declared) =>
        $"Address one as `{repository}:{declared[0].Id}`, or several as `{repository}:<lane>+<lane>`.";
}
