namespace Daoris.Driver;

/// <summary>What each ruled count takes at a door: its window, its actions and its bounds.</summary>
public sealed record SupervisorCountRow(string Count, string Window, IReadOnlyList<string> Actions, int Least, int Most);

/// <summary>
/// The supervisor's rules as <c>ROLES</c> answers them: every place's set as the lines read it, the counts, and each place's wider places
/// before the machine as the judge reads them (<see cref="RuleWiderPlaceStore"/>), so the fold draws what a place replaces and inherits.
/// </summary>
public sealed record SupervisorRuleRows(
    IReadOnlyList<SupervisorRuleSet> Sets, IReadOnlyList<SupervisorCountRow> Counts, IReadOnlyDictionary<string, IReadOnlyList<string>> Wider)
{
    /// <summary>The rows of <paramref name="home"/> now; a store that does not read shows the seeded rules the lines decide by.</summary>
    public static SupervisorRuleRows Of(string home)
    {
        var book = SupervisorRuleBook.Read(home);
        return new(book.Sets,
        [
            .. SupervisorRules.Windows.Select(pair => new SupervisorCountRow(
                pair.Key, pair.Value, SupervisorRules.Actions(pair.Key), SupervisorRules.Bounds(pair.Key).Low, SupervisorRules.Bounds(pair.Key).High)),
        ], new RuleWiderPlaceStore(home).Read().Chains());
    }
}
