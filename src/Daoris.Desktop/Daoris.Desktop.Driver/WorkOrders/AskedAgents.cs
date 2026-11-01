namespace Daoris.Driver;

/// <summary>The agents a brainstorm or a meeting is asked of, checked against the adapters the driver resolves turns by.</summary>
internal static class AskedAgents
{
    /// <summary>The adapters this machine runs: the build's and its plugins'.</summary>
    public static AdapterSet Current(string home)
    {
        var built = AdapterSet.Built();
        return built.WithPlugins(AgentPluginReads.Current.Load(home, built.Names));
    }

    /// <summary>The refusal naming the set's ids when an agent is no adapter, else null.</summary>
    public static string? Refusal(AdapterSet set, IEnumerable<string> agents)
    {
        var unknown = agents.Where(agent => !set.Names.Contains(agent, StringComparer.OrdinalIgnoreCase)).ToList();
        return unknown.Count == 0
            ? null
            : $"daoris-driver: no adapter is named {string.Join(", ", unknown.Select(name => $"`{name}`"))}; the agents there are {string.Join(", ", set.Names)}.";
    }
}
