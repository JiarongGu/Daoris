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

        var folder = Path.Combine(home, Folder);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{sessionId}.mcp.json");

        var document = new
        {
            mcpServers = servers.ToDictionary(
                server => server.Name,
                server => new
                {
                    command = server.Command,
                    args = server.Arguments,
                    env = server.Environment,
                },
                StringComparer.Ordinal),
        };

        var beside = path + ".tmp";
        File.WriteAllText(beside, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
        File.Move(beside, path, overwrite: true);
        return path;
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
