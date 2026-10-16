using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The MCP servers a pipe-door session is handed, as a file (D65 §1f). The protocol door carries
/// servers on `session/new`; a pipe-door harness that can take servers at spawn takes a file, and
/// this writes it — under Daoris's home, per session, and removed when the session ends.
/// </summary>
/// <remarks>
/// <para><b>Under the home, never in the repository.</b> The shape a harness reads here is the same
/// as its own `.mcp.json`, and the whole point of handing it at spawn is that nothing of Daoris's
/// is written into a tree Daoris does not own (D32, the shape HELP3 measured for `--settings`).</para>
///
/// <para>The layout is Claude Code's, verified against its own documentation: a `mcpServers` object
/// keyed by name, each with `command`, `args` and `env`.</para>
/// </remarks>
public static class SpawnServers
{
    /// <summary>The folder under the home that holds the per-session files.</summary>
    public const string Folder = "spawn";

    /// <summary>Write the file for one session, or answer null when there is nothing to hand.</summary>
    public static string? Write(string home, string sessionId, IReadOnlyList<AcpMcpServer> servers)
    {
        if (servers.Count == 0) return null;

        var path = DaorisHome.Entry(home, HomeEntry.Spawn, $"{sessionId}.mcp.json");

        // By name, so a name given twice is refused as it always was (the writer is reflection-free: JsonFile).
        var byName = servers.ToDictionary(server => server.Name, StringComparer.Ordinal);
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

    /// <summary>
    /// Hand a pipe-door harness its servers: write the file and point the harness at it. Null, with
    /// nothing handed, on the protocol door, which carries servers on the wire, or when there are none.
    /// Which servers a session gets is its caller's decision; this is only how a pipe receives them.
    /// </summary>
    public static string? Hand(
        ISessionAdapter adapter, System.Diagnostics.ProcessStartInfo info, string home, string sessionId,
        IReadOnlyList<AcpMcpServer> servers)
    {
        if (adapter.Wire != SessionWire.Pipe) return null;
        var file = Write(home, sessionId, servers);
        if (file is not null) adapter.HandServers(info, file);
        return file;
    }

    /// <summary>The file is the session's; it goes when the session does.</summary>
    public static void Remove(string? path)
    {
        if (path is null) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A file something still holds open is left for the next start to overwrite.
        }
    }
}

/// <summary>Runs one action on disposal — for a cleanup that has to ride a `using` beside a process's own.</summary>
internal sealed class Disposer(Action onDispose) : IDisposable
{
    public void Dispose() => onDispose();
}
