using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's user-scope servers, the global file's <c>mcpServers</c>, written as written into the <c>own</c> plugin's
/// <c>.mcp.json</c> (<see cref="ClaudeOwnHome"/>), which both of Claude Code's doors load with the plugin. Nothing else of the file is
/// read: its sign-in and its projects' entries stay where they are.
/// </summary>
/// <remarks>
/// The floor keeps two out. One named as Daoris's connector: every session is handed Daoris's own, carrying its identity. And one whose
/// environment names a Daoris variable (<c>DAORIS_</c>, the prefix of its identity and its keys): it would act as the person on
/// Daoris's records from a session's process, which the spawn's own environment never lets a session do (agents rule 12).
/// </remarks>
internal static class ClaudeOwnServers
{
    private const string Servers = "mcpServers";

    /// <summary>Write the servers <paramref name="globalFile"/> holds into <paramref name="plugin"/>; whether it wrote any.</summary>
    public static bool Write(string globalFile, string plugin)
    {
        var kept = Read(globalFile);
        if (kept.Count == 0) return false;

        Directory.CreateDirectory(plugin);
        JsonFile.Write(Path.Combine(plugin, ".mcp.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartObject(Servers);
            foreach (var (name, server) in kept)
            {
                writer.WritePropertyName(name);
                server.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        return true;
    }

    private static List<(string Name, JsonElement Server)> Read(string globalFile)
    {
        try
        {
            if (!File.Exists(globalFile)) return [];
            using var file = JsonDocument.Parse(File.ReadAllText(globalFile));
            if (!file.RootElement.TryGetProperty(Servers, out var servers) || servers.ValueKind != JsonValueKind.Object) return [];
            return [.. servers.EnumerateObject().Where(server => Kept(server.Name, server.Value)).Select(server => (server.Name, server.Value.Clone()))];
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file the person's own run cannot read either hands nothing; the session starts as it would have.
            return [];
        }
    }

    private static bool Kept(string name, JsonElement server) =>
        server.ValueKind == JsonValueKind.Object
        && !string.Equals(name, KnowledgeConnector.ServerName, StringComparison.OrdinalIgnoreCase)
        && !(server.TryGetProperty("env", out var environment) && environment.ValueKind == JsonValueKind.Object
             && environment.EnumerateObject().Any(variable => variable.Name.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)));
}
