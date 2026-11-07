using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's lineage hook: a script its settings file runs on the connector's calls and each subagent's start
/// and stop, which appends what Claude Code says of each agent to the session's lineage file. It is Claude Code's asset, handed in
/// its settings by its rule language (<see cref="ClaudeRules"/>); an agent whose calls carry their thread needs none.
/// </summary>
public static class ClaudeLineageHook
{
    /// <summary>The hook's script, under the home's <c>hooks/</c> beside the tree guard's.</summary>
    public const string ScriptName = "lineage-hook.mjs";

    /// <summary>🔴 The connector's session shape, copied: only a session's own id names a hook.</summary>
    private static readonly Regex SessionShape = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);

    /// <summary>The script as shipped, from the assembly, with LF line endings.</summary>
    public static string Source { get; } = ReadSource();

    /// <summary>The script under the home, written if absent or not as shipped; answers its path.</summary>
    public static string Install(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Hooks);
        var path = Path.Combine(folder, ScriptName);
        if (File.Exists(path) && AtomicFile.ReadText(path) == Source) return path;

        Directory.CreateDirectory(folder);
        AtomicFile.WriteText(path, Source);
        return path;
    }

    /// <summary>
    /// The hook for one session appending to <paramref name="file"/>, or null where none can be handed: an id that names no session,
    /// a node that cannot run, a home that will not take the script. Then the session's children are known by the stream and the
    /// calls alone.
    /// </summary>
    public static LineageHook? For(string home, string sessionId, string file)
    {
        if (!SessionShape.IsMatch(sessionId)) return null;
        var node = Tools.Resolve(home, "node");
        if (node.Refused) return null;
        try
        {
            return new LineageHook(Install(home), file) { Node = node.File ?? "node" };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ReadSource()
    {
        using var stream = typeof(ClaudeLineageHook).Assembly.GetManifestResourceStream("Daoris.Driver.lineage-hook.mjs")
            ?? throw new InvalidOperationException("the lineage hook's script is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
