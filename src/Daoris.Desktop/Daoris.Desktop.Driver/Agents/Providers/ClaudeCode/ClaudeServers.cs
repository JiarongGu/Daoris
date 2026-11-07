namespace Daoris.Driver;

/// <summary>
/// The servers Claude Code's native door is handed, as a file in its own <c>.mcp.json</c> layout: a <c>mcpServers</c> object
/// keyed by name, each with <c>command</c>, <c>args</c> and <c>env</c>, verified against its own documentation. Its protocol door
/// carries them on <c>session/new</c> instead.
/// </summary>
/// <remarks>
/// <b>Under the home, never in the repository.</b> The whole point of handing them at spawn is that nothing of Daoris's is written into
/// a tree Daoris does not own, as for <c>--settings</c>; the file goes when the session does.
/// </remarks>
public static class ClaudeServers
{
    /// <summary>What the file is named by after its session's id under the home's <c>spawn/</c>, which the session home's inventory reads it by.</summary>
    public const string FileSuffix = ".mcp.json";

    /// <summary>Write the file for one session, or answer null when there is nothing to hand.</summary>
    public static string? Write(string home, ConnectorPlan plan)
    {
        if (plan.Servers.Count == 0) return null;

        var path = DaorisHome.Entry(home, HomeEntry.Spawn, $"{plan.SessionId}{FileSuffix}");

        // By name, so a name given twice is refused (the writer is reflection-free: JsonFile).
        var byName = plan.Servers.ToDictionary(server => server.Name, StringComparer.Ordinal);
        JsonFile.Write(path, writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartObject("mcpServers");
            foreach (var (name, server) in byName)
            {
                writer.WriteStartObject(name);
                writer.WriteString("command", server.Command);
                writer.WriteStartArray("args");
                foreach (var argument in server.Arguments) writer.WriteStringValue(argument);
                writer.WriteEndArray();
                writer.WriteStartObject("env");
                foreach (var (key, value) in server.Environment) writer.WriteString(key, value);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        return path;
    }
}
