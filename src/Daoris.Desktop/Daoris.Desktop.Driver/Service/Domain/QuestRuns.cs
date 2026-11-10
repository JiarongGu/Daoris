namespace Daoris.Driver;

/// <summary>
/// The run a quest's work is: its chain's first quest in its repository, which every later quest of the chain there shares, so a chain's steps
/// in one repository are one run and every other quest is a run of its own. The one rule the runs' bindings, an entry's landing and the
/// planner's placing of a line's quests read.
/// </summary>
public static class QuestRuns
{
    /// <summary>The most parent steps a chain is walked up.</summary>
    public const int Steps = 20;

    /// <summary>The run of a chain's work in <paramref name="repository"/>: its first quest there; null where the chain has none there.</summary>
    public static string? FirstIn(IEnumerable<QuestView> chain, string repository) =>
        chain.FirstOrDefault(quest => string.Equals(quest.To, repository, StringComparison.OrdinalIgnoreCase))?.Id;

    /// <summary>The run <paramref name="quest"/> is, its chain read up its parents while <paramref name="find"/> answers for them.</summary>
    public static string Of(QuestView quest, Func<string, QuestView?> find)
    {
        var chain = new List<QuestView> { quest };
        for (var step = 0; step < Steps && chain[0].Parent is { Length: > 0 } parent && find(parent) is { } above; step++) chain.Insert(0, above);
        return FirstIn(chain, quest.To) ?? quest.Id;
    }
}
