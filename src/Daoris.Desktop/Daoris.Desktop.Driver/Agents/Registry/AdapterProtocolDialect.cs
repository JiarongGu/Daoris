namespace Daoris.Driver;

/// <summary>
/// What a protocol door's adapter class hands its agent's own readings (AGENTPROVIDER1 row d): the <c>_meta</c> its
/// <c>session/new</c> carries for the rules file and the plugins. Row i reads the rest of each door's dialect; row t takes this bridge
/// away with the classes.
/// </summary>
internal sealed class AdapterProtocolDialect(ISessionAdapter adapter) : IProtocolDialect
{
    public object? SessionMeta(string settingsFile, IReadOnlyList<string> plugins) => adapter.AcpSessionMeta(settingsFile, plugins);
}
