namespace Daoris.Driver;

/// <summary>
/// The supervisor's rules for Ask Daoris's room, read from the home as the room is written
/// beside the roles: each place's set in the terminal's words, so <i>why is this waiting?</i> is answered from the person's rules. A store that does
/// not read says none, since the room is written at a start and holds no start for it.
/// </summary>
internal static class HelpRuleFacts
{
    /// <summary>The lines of every place's rules, the machine's first then the narrower places', each as <c>`place` (revision n): rule; rule</c>.</summary>
    internal static IReadOnlyList<string> Read(string home)
    {
        try
        {
            return
            [
                .. SupervisorRuleBook.Read(home).Sets
                    .Where(set => set.Rules.Count > 0)
                    .OrderBy(set => set.Place == SupervisorRules.Machine ? 0 : 1).ThenBy(set => set.Place, StringComparer.Ordinal)
                    .Select(set => $"`{set.Place}` (revision {set.Revision}): {string.Join("; ", set.Rules.Select(SupervisorRuleDoor.Said))}"),
            ];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            return [];
        }
    }
}
