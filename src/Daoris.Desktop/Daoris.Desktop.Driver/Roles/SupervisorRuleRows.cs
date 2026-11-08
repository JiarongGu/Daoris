namespace Daoris.Driver;

/// <summary>What each ruled count takes at a door: its window, its actions and its bounds, and whether its <c>refer</c> names driven roles alone.</summary>
public sealed record SupervisorCountRow(string Count, string Window, IReadOnlyList<string> Actions, int Least, int Most, bool DrivenRefer);

/// <summary>
/// The supervisor's rules as <c>ROLES</c> answers them: every place's set as the lines read it, the counts, each place's wider places as
/// the judge reads them (<see cref="RuleWiderPlaceStore"/>), and the roles a refer on limits or moves may name.
/// </summary>
public sealed record SupervisorRuleRows(
    IReadOnlyList<SupervisorRuleSet> Sets, IReadOnlyList<SupervisorCountRow> Counts, IReadOnlyDictionary<string, IReadOnlyList<string>> Wider,
    IReadOnlyList<string> Driven)
{
    /// <summary>The rows of <paramref name="home"/> now; a store that does not read shows the seeded rules the lines decide by.</summary>
    public static SupervisorRuleRows Of(string home)
    {
        var book = SupervisorRuleBook.Read(home);
        return new(book.Sets,
        [
            .. SupervisorRules.Windows.Select(pair => new SupervisorCountRow(
                pair.Key, pair.Value, SupervisorRules.Actions(pair.Key), SupervisorRules.Bounds(pair.Key).Low, SupervisorRules.Bounds(pair.Key).High,
                SupervisorRules.DrivenRefers.Contains(pair.Key))),
        ], new RuleWiderPlaceStore(home).Read().Chains(), SupervisorRuleDoor.Driven(home));
    }
}
