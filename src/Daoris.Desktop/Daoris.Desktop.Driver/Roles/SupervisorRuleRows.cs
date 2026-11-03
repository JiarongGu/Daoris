namespace Daoris.Driver;

/// <summary>What each ruled count takes at a door: its window, its actions and its bounds.</summary>
public sealed record SupervisorCountRow(string Count, string Window, IReadOnlyList<string> Actions, int Least, int Most);

/// <summary>The supervisor's rules as <c>ROLES</c> answers them: whether this home is reached, every place's set as stored, and the counts.</summary>
public sealed record SupervisorRuleRows(bool Reached, IReadOnlyList<SupervisorRuleSet> Sets, IReadOnlyList<SupervisorCountRow> Counts)
{
    /// <summary>The rows of <paramref name="home"/> now; a store that does not read is a home not reached, holding no rule.</summary>
    public static SupervisorRuleRows Of(string home)
    {
        var book = SupervisorRuleBook.Read(home);
        return new(book.Reached, book.Sets,
        [
            .. SupervisorRules.Windows.Select(pair => new SupervisorCountRow(
                pair.Key, pair.Value, SupervisorRules.Actions(pair.Key), SupervisorRules.Bounds(pair.Key).Low, SupervisorRules.Bounds(pair.Key).High)),
        ]);
    }
}
