namespace Daoris.Driver;

/// <summary>The contributing adapters and all declared account owners, with Plugins retaining skill composition.</summary>
public interface IAgentPlugins
{
    IEnumerable<ISessionAdapter> Adapters { get; }
    IEnumerable<string> DeclaredAgents { get; }
    (string? Folder, IReadOnlyList<string> Plugins) WriteSkills(string home, string sessionId, string to);
}

/// <summary>A declared adapter's provenance, independent of its implementation.</summary>
public interface IPluginAdapter : ISessionAdapter
{
    string Plugin { get; }
}

/// <summary>The catalogue load operation Agents needs at both account discovery and terminal construction.</summary>
public interface IAgentPluginReads
{
    IAgentPlugins Load(string home, IEnumerable<string> reservedHarnesses);
}

/// <summary>The required Plugins operation installed when the driver assembly loads.</summary>
public static class AgentPluginReads
{
    private static IAgentPluginReads? _current;

    public static IAgentPluginReads Current
    {
        get => _current ?? throw new InvalidOperationException("no agent plugin reads are installed: Plugins installs them when the driver loads");
        set => _current = value;
    }
}
