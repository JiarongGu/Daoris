using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The person's own Codex servers, the <c>mcp_servers</c> of the <c>config.toml</c> in their Codex home, as the servers a session's open
/// hands <c>session/new</c> (<see cref="AcpProtocol.Offer(ConnectorPlan?, IReadOnlyList{ServerEntry}?)"/>): the adapter replaces the
/// configuration's <c>mcp_servers</c> with the open's whenever the open names any, so they cannot ride <see cref="CodexOwnHome"/>'s
/// configuration. Only a local program is a <see cref="ServerEntry"/>; a remote server and one switched off are left out.
/// </summary>
/// <remarks>
/// The floor is the Claude servers' (<see cref="ClaudeOwnServers"/>): one named as Daoris's connector, and one whose environment names a
/// <c>DAORIS_</c> variable, stay out. Nothing is handed where the servers' default is off, a pass is held to the connector alone
/// (the family is off then), or the session runs in the person's own home, whose program reads it all itself.
/// </remarks>
internal static class CodexOwnServers
{
    public static IReadOnlyList<ServerEntry> Read(OwnTools tools)
    {
        var own = CodexOwnHome.Place(tools);
        if (!tools.Families.HasFlag(OwnFamilies.Servers) || !Directory.Exists(own) || CodexOwnHome.InOwnHome(tools)) return [];

        var file = Path.Combine(own, "config.toml");
        JsonObject? config;
        try
        {
            config = File.Exists(file) ? TomlReader.Read(File.ReadAllText(file)) : null;
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or FormatException)
        {
            // A file their own run cannot read hands nothing; the session starts as it would have.
            return [];
        }

        if (config?["mcp_servers"] is not JsonObject servers) return [];
        List<ServerEntry> kept = [];
        foreach (var (name, node) in servers)
        {
            if (node is not JsonObject server || server["command"] is not JsonValue command || !command.TryGetValue<string>(out var program)
                || string.Equals(name, KnowledgeConnector.ServerName, StringComparison.OrdinalIgnoreCase)
                || (server["enabled"] is JsonValue enabled && enabled.TryGetValue<bool>(out var on) && !on))
            {
                continue;
            }

            var environment = (server["env"] as JsonObject)?
                .Where(pair => pair.Value is JsonValue)
                .ToDictionary(pair => pair.Key, pair => pair.Value!.ToString(), StringComparer.Ordinal) ?? [];
            if (environment.Keys.Any(variable => variable.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase))) continue;

            var arguments = (server["args"] as JsonArray)?.OfType<JsonValue>().Select(argument => argument.ToString()).ToList() ?? [];
            kept.Add(new ServerEntry(name, program, arguments, environment));
        }

        return kept;
    }
}
