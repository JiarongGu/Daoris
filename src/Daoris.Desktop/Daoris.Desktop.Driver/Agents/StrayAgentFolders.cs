namespace Daoris.Driver;

/// <summary>
/// A folder under the account homes that no agent names (<see cref="HarnessAccountFolders.StrayAgents"/>), such as one a start
/// whose path carried an unexpanded variable left: named by <c>agent account list</c> and taken whole, every account in it, by
/// <c>agent account remove &lt;folder&gt;</c>. Only a folder that is in that list is ever removed.
/// </summary>
internal static class StrayAgentFolders
{
    /// <summary>Whether <paramref name="name"/> is a stray agent folder of <paramref name="home"/> now.</summary>
    internal static bool Is(string home, IReadOnlyCollection<string> agents, string name) =>
        HarnessAccountFolders.StrayAgents(home, agents).Contains(name, StringComparer.Ordinal);

    /// <summary>The stray folder at each place of the account homes it lies in, removed whole; false where none lies there.</summary>
    internal static bool Remove(string home, IReadOnlyCollection<string> agents, string name)
    {
        if (!Is(home, agents, name)) return false;
        var removed = false;
        foreach (var place in DaorisHome.Places(home, HomeEntry.AgentHomes, name))
        {
            if (!Directory.Exists(place)) continue;
            HarnessAccountFolders.DeleteFolder(place);
            removed = true;
        }

        return removed;
    }
}
