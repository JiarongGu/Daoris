using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One session's installed permission ask: the script, the requests box it asks through, the session, and the node it runs on.</summary>
public sealed record AskHook(string Script, string Requests, string Session)
{
    /// <summary>Every tool: the script judges which call its harness would ask about.</summary>
    public const string Matcher = "*";

    /// <summary>The node the harness starts the script with: the file Tools resolves, as the tree guard's.</summary>
    public string Node { get; init; } = "node";

    /// <summary>How long the script waits for the person's answer: the hold's own bound (<see cref="HeldPermissions.Bound"/>).</summary>
    public static int BoundSeconds => (int)HeldPermissions.Bound.TotalSeconds;
}

/// <summary>
/// Claude Code's permission ask on the native door: a script run before every tool call, which asks Daoris through the session
/// requests box (<see cref="SessionMove.Permit"/>) and answers the harness allow or refuse. The protocol door holds on its wire.
/// </summary>
public static class ClaudeAskHook
{
    /// <summary>The hook's script, under the home's <c>hooks/</c> beside the tree guard's and the lineage hook's.</summary>
    public const string ScriptName = "permission-ask.mjs";

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
    /// The ask for one session, or null where none can be handed: an id that names no session, a node that cannot run, a home that will
    /// not take the script. Then the harness refuses what its rules do not allow, as it does under <c>-p</c>.
    /// </summary>
    public static AskHook? For(string home, string sessionId)
    {
        if (!SessionShape.IsMatch(sessionId)) return null;
        var node = Tools.Resolve(home, "node");
        if (node.Refused) return null;
        try
        {
            return new AskHook(Install(home), DaorisHome.Entry(home, HomeEntry.Requests), sessionId) { Node = node.File ?? "node" };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ReadSource()
    {
        using var stream = typeof(ClaudeAskHook).Assembly.GetManifestResourceStream("Daoris.Driver.permission-ask.mjs")
            ?? throw new InvalidOperationException("the permission ask's script is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
