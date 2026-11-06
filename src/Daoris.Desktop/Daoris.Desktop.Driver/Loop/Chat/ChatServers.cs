namespace Daoris.Driver;

/// <summary>The servers a conversation is handed when its agent opens: the machine's knowledge connector carrying who the conversation is, and the plugins' servers.</summary>
internal sealed class ChatServers(ChatContext context)
{
    /// <summary>
    /// What a conversation on the protocol door is handed on <c>session/new</c>, as a driven session on that door is: the machine's knowledge
    /// connector, carrying who the session is, and every server the plugins declare. Located per conversation, because a machine can gain
    /// either between two.
    /// </summary>
    /// <param name="circle">The workspace its tools are bound to (<see cref="ChatPlace.Circle"/>); null for none.</param>
    /// <param name="proc">The lineage token of the process it is composed for, on the protocol door alone.</param>
    public IReadOnlyList<ServerEntry> For(string sessionId, string? circle, IReadOnlyList<ServerEntry> plugins, string? proc = null)
    {
        var offered = new List<ServerEntry>();
        if (KnowledgeConnector.Offer(
                Environment.GetEnvironmentVariable(KnowledgeConnector.PathVariable), DaorisHome.Resolve(),
                AppContext.BaseDirectory, scope: ConnectorScope(context.Home, sessionId, circle, proc, KnowledgeConnector.KeptRole(context.Conversations.RoleOf, sessionId))) is { } connector)
        {
            offered.Add(connector);
        }

        offered.AddRange(plugins);
        return offered;
    }

    /// <summary>
    /// What a conversation's knowledge connector carries beyond the store: who it is and where its rules live, and the workspace it is bound
    /// to, as a driven session's circle is named for its connector; none for a conversation bound to none.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ConnectorScope(
        string home, string sessionId, string? circle, string? proc = null, (string Role, long Revision)? role = null) =>
        KnowledgeConnector.SessionScope(home, sessionId, HelpCircle.Scope(circle), proc, role);

    /// <summary>
    /// The plugins' servers a conversation is handed, with Daoris's own browser brought up for one that drives it, or that one withheld and
    /// the sentence why; and a <c>plugin.served</c> line for each, handed or withheld, read from the catalogue the servers came from, as a
    /// driven session's are.
    /// </summary>
    /// <remarks>
    /// The catalogue is read as the driver loop reads it, reserving the build's names. Never the roster's live set: that holds the plugins'
    /// own agents, so a plugin declaring an agent and a server was refused as one this build carries, and its servers were withheld from every
    /// conversation.
    /// </remarks>
    /// <param name="to">Whose servers: those declared for sessions, or for Ask Daoris.</param>
    /// <param name="every">
    /// Every server the plugins here declare, for whomever and approved or not, each said once in the log (Ask Daoris in full access); false
    /// takes those <paramref name="to"/> takes.
    /// </param>
    public static async Task<(IReadOnlyList<ServerEntry> Handed, Noted? Notice, bool Drives, IReadOnlyList<string> Drivers)> HandAsync(
        string home, IInAppBrowser? browser, PluginLog plugins, string sessionId, CancellationToken ct, string to = PluginServerTo.Sessions,
        bool every = false)
    {
        var catalog = PluginCatalog.Load(home, ProviderRegistry.Built().Names);
        // Ask Daoris takes only the servers the person approved for it; a session, its own.
        var servers = every ? [.. PluginServerTo.All.SelectMany(catalog.ServersTo).DistinctBy(server => server.Name)]
            : to == PluginServerTo.Help ? HelpGrants.Read(home, catalog).Servers : catalog.ServersTo(to);
        using var log = new MachineLog(home, "driver");
        var handed = await SessionServers.HandNotedAsync(home, servers, browser, log, ct).ConfigureAwait(false);
        if (every) ServedEvery(plugins, catalog, sessionId, handed.Handed);
        else plugins.Served(catalog, sessionId, handed.Handed, to);
        return handed;
    }

    /// <summary>A <c>plugin.served</c> line for every server the plugins here declare, once each, handed or withheld.</summary>
    private static void ServedEvery(PluginLog plugins, PluginCatalog catalog, string sessionId, IReadOnlyList<ServerEntry> handed)
    {
        var said = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plugin in catalog.Contributing)
        {
            foreach (var server in plugin.Manifest.Servers.Where(server => said.Add(server.Name)))
            {
                plugins.Served(plugin.Manifest.Id, server.Name, sessionId, handed.Any(each => string.Equals(each.Name, server.Name, StringComparison.Ordinal)));
            }
        }
    }
}
