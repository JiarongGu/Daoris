namespace Daoris.Driver;

/// <summary>
/// The one place a start's connectors become a <see cref="ConnectorPlan"/> (CONNECTORS1, the design §4.4): once per workspace's start, with
/// the sources that workspace permits and the servers it requires, read by the protocol door's open and by a file door's handing alike.
/// </summary>
internal static class ConnectorPlans
{
    /// <summary>What a session's start draws from: Daoris's connector, the plugins' servers and the browser's.</summary>
    public static IReadOnlyList<ConnectorSource> Sessions { get; } = [ConnectorSource.Daoris, ConnectorSource.Plugin, ConnectorSource.Browser];

    /// <summary>What a reviewer, a helper or a partner draws from: Daoris's connector and nothing else, since a plugin's server could write where it may not.</summary>
    public static IReadOnlyList<ConnectorSource> ConnectorOnly { get; } = [ConnectorSource.Daoris];

    /// <summary>Where a server a start chose comes from.</summary>
    public static ConnectorSource SourceOf(AcpMcpServer server) =>
        server.Name == KnowledgeConnector.ServerName ? ConnectorSource.Daoris
        : InAppBrowserServers.Needs(server) ? ConnectorSource.Browser
        : ConnectorSource.Plugin;

    /// <summary>
    /// The plan of one start in <paramref name="workspace"/>: its servers, each from a permitted source, and Daoris's connector among those it
    /// requires where it was found. A server from a source the start does not permit is refused, naming it.
    /// </summary>
    /// <param name="permitted">The sources the start draws from; the session's by default.</param>
    public static ConnectorPlan For(
        string sessionId, string? workspace, IReadOnlyList<AcpMcpServer> servers, IReadOnlyList<ConnectorSource>? permitted = null)
    {
        permitted ??= Sessions;
        if (servers.FirstOrDefault(server => !permitted.Contains(SourceOf(server))) is { } barred)
        {
            throw new DriverException($"the connector `{barred.Name}` comes from {SourceOf(barred).ToString().ToLowerInvariant()}, which this start's workspace does not permit");
        }

        var plan = new ConnectorPlan(sessionId, servers)
        {
            Workspace = workspace,
            Permitted = permitted,
            Includes = servers.Any(server => server.Name == KnowledgeConnector.ServerName) ? [KnowledgeConnector.ServerName] : [],
        };
        return plan.Fault() is { } fault ? throw new DriverException(fault) : plan;
    }

    /// <summary>The plan, or a refusal naming <paramref name="adapter"/> where its door cannot honour what the start requires of it.</summary>
    public static ConnectorPlan Admit(ISessionAdapter adapter, ConnectorPlan plan) =>
        plan.Refusal(adapter.Name, adapter.Abilities()) is { } refusal ? throw new DriverException(refusal) : plan;
}
