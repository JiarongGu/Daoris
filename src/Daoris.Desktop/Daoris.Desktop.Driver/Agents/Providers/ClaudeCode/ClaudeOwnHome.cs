namespace Daoris.Driver;

/// <summary>
/// Claude Code's own tool home as the person's own run reads it, laid out for one session (D253 point 7, census row 23): the folder
/// <c>CLAUDE_CONFIG_DIR</c> names, else <c>.claude</c> in the profile. What a session is handed of it is one harness plugin, <c>own</c>,
/// beside Daoris's plugins under the session's spawn folder (<see cref="ClaudeSkills"/>), named by the door's session-only way, so
/// its pieces are named <c>own:&lt;name&gt;</c>. The person's folder is read and never written; nothing is laid out for a session that
/// runs in it, whose harness loads it all by itself.
/// </summary>
/// <remarks>
/// <b>Copied, never linked</b>: the copy goes with the session's folder, and a link's removal must never reach the person's own files.
/// A piece the person linked in at its top (a skill installed as a link) is copied from where it points; a link deeper in is not.
/// </remarks>
internal static class ClaudeOwnHome
{
    /// <summary>The harness plugin's name, which its pieces are named under.</summary>
    public const string Name = "own";

    /// <summary>Its folder beside Daoris's plugins: a name no plugin id takes.</summary>
    private const string Folder = "_own";

    /// <summary>The variable naming a Claude Code tool home, the person's own run's and the session's account's alike.</summary>
    private const string HomeVariable = "CLAUDE_CONFIG_DIR";

    /// <summary>The folders a by-hand run loads its skills, subagents and commands from.</summary>
    private static readonly string[] Pieces = ["skills", "agents", "commands"];

    /// <summary>The tool home the person's own run reads.</summary>
    public static string Place(OwnTools tools) =>
        tools.Environment(HomeVariable) is { Length: > 0 } named ? named : Path.Combine(tools.UserHome, ".claude");

    /// <summary>The session's <c>own</c> plugin under <paramref name="home"/>'s spawn folder; none where nothing of the person's is handed.</summary>
    public static (string? Folder, IReadOnlyList<string> Plugins) Write(string home, string sessionId, OwnTools tools)
    {
        var own = Place(tools);
        if (tools.Families == OwnFamilies.None || !Directory.Exists(own)) return (null, []);
        if (Same(tools.Session(HomeVariable) ?? Path.Combine(tools.UserHome, ".claude"), own)) return (null, []);

        var folder = DaorisHome.Entry(home, HomeEntry.Spawn, $"{sessionId}.plugins");
        var plugin = Path.Combine(folder, Folder);
        if (Directory.Exists(plugin)) Directory.Delete(plugin, recursive: true);

        var laid = tools.Families.HasFlag(OwnFamilies.Skills) && CopyPieces(own, plugin);
        if (!laid) return (null, []);

        Directory.CreateDirectory(Path.Combine(plugin, ".claude-plugin"));
        JsonFile.Write(Path.Combine(plugin, ".claude-plugin", "plugin.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("name", Name);
            writer.WriteString("description", "What your own tool home holds, as Daoris handed it to this session.");
            writer.WriteEndObject();
        });
        return (folder, [plugin]);
    }

    private static bool CopyPieces(string own, string plugin)
    {
        var copied = false;
        foreach (var piece in Pieces.Where(piece => Directory.Exists(Path.Combine(own, piece))))
        {
            foreach (var entry in new DirectoryInfo(Path.Combine(own, piece)).EnumerateFileSystemInfos())
            {
                var target = Path.Combine(plugin, piece, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (entry is DirectoryInfo directory) TreeCopy.Copy(directory.FullName, target);
                else if (entry is FileInfo file) file.CopyTo(target);
                copied = true;
            }
        }

        return copied;
    }

    private static bool Same(string one, string other) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(one)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(other)),
        StringComparison.OrdinalIgnoreCase);
}
