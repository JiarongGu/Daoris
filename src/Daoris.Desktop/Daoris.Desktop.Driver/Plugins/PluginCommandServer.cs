namespace Daoris.Driver;

/// <summary>
/// A plugin's tool commands as the one server Ask Daoris is handed, <c>daoris-plugins</c>, for the person to approve as any server's tools
/// are approved (<see cref="HelpGrants"/>): its reads, and its writes each on its own. It carries no command of its own, since the
/// program that serves it is built per session (<see cref="PluginToolsHand"/>); a server with no command is the marker.
/// </summary>
public static class PluginCommandServer
{
    /// <summary>The server of a plugin's tool commands that ask nothing of the person, or null where it has none or speaks no hooks.</summary>
    public static PluginServer? Of(PluginManifest manifest)
    {
        if (manifest.Hooks is null) return null;
        var tools = manifest.Interfaces.Commands.Where(command => command.Tool && !command.Interactive).ToList();
        if (tools.Count == 0) return null;
        string Named(PluginCommandDeclared command) => manifest.Id + "_" + command.Name;
        return new PluginServer(PluginToolsServer.Name, [], new Dictionary<string, string>(StringComparer.Ordinal))
        {
            To = [PluginServerTo.Help],
            Allow = [.. tools.Where(command => !command.Writes).Select(Named)],
            Writes = [.. tools.Where(command => command.Writes).Select(Named)],
        };
    }

    /// <summary>Whether a server is this one's marker: it has no command of its own.</summary>
    public static bool Is(PluginServer server) => server.Command.Count == 0 && server.Name == PluginToolsServer.Name;

    /// <summary>The servers a plugin offers Ask Daoris: those it declares for it, and its tool commands as one.</summary>
    public static IReadOnlyList<PluginServer> OfferedToHelp(PluginManifest manifest) =>
        [.. manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Help)), .. Of(manifest) is { } own ? [own] : (IEnumerable<PluginServer>)[]];
}
