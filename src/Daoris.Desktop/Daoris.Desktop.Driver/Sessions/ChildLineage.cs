using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One session's lineage hook: the script Claude Code runs, the file it appends to, and the node it runs on.</summary>
/// <param name="Script">The script, written under the Daoris home by <see cref="ChildLineage.Install"/>.</param>
/// <param name="File">The session's lineage file (<see cref="ChildLineage.PathOf"/>).</param>
public sealed record LineageHook(string Script, string File)
{
    /// <summary>The node the harness starts the script with: the file Tools resolves (TOOLS5), as the tree guard's.</summary>
    public string Node { get; init; } = "node";
}

/// <summary>
/// A session's lineage file, where its native children are bound to its part (FABRIC-C1a, D173): the connector writes a line
/// per call with the lineage its harness carried, the hook this driver hands Claude Code writes what it says of each agent, and
/// this driver writes what Claude Code's stream says of each subagent and the part's end. <see cref="ChildBindings"/> reads them.
/// </summary>
/// <remarks>
/// <para><b>The file is the contract</b> (<c>.claude/knowledge/twins.md</c>): <c>&lt;home&gt;/sessions/&lt;id&gt;/lineage.jsonl</c>,
/// one JSON object a line, LF, UTF-8 with no BOM. 🔴 <see cref="FileName"/> and the folder are the connector's
/// <c>LineageFile</c>'s, copied; its call lines are held word for word by <c>ChildBindingsTests</c>. It sits in the session's own
/// folder, so every door that takes a session's files (<see cref="SessionHomeFiles"/>'s <c>files</c>) takes it.</para>
///
/// <para><b>Several writers.</b> Each line is appended holding the file alone and tried again while another writer holds it, as
/// the connector appends; the hook, which node runs, tries again while it is refused. A line that cannot be written is a
/// lineage unknown, never a failed session.</para>
///
/// <para>It binds and records; it decides nothing a child may do (FABRIC-C2a's).</para>
/// </remarks>
public static class ChildLineage
{
    /// <summary>The file's name in the session's folder. 🔴 The connector's <c>LineageFile.FileName</c> is the same name.</summary>
    public const string FileName = "lineage.jsonl";

    /// <summary>The hook's script, under the home's <c>hooks/</c> beside the tree guard's.</summary>
    public const string ScriptName = "lineage-hook.mjs";

    /// <summary>The connector's tools, in the harness's own matcher shape: the calls whose agent the hook names.</summary>
    public const string Matcher = "mcp__" + KnowledgeConnector.ServerName + "__.*";

    /// <summary>🔴 The connector's session shape, copied: only a session's own id names a folder here.</summary>
    private static readonly Regex SessionShape = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The script as shipped, from the assembly, with LF line endings.</summary>
    public static string Source { get; } = ReadSource();

    /// <summary>Where one session's lineage is kept.</summary>
    public static string PathOf(string home, string sessionId) => DaorisHome.Entry(home, HomeEntry.Sessions, sessionId, FileName);

    /// <summary>The script under the home, written if absent or not as shipped; answers its path.</summary>
    public static string Install(string home)
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Hooks);
        var path = Path.Combine(folder, ScriptName);
        if (File.Exists(path) && File.ReadAllText(path) == Source) return path;

        Directory.CreateDirectory(folder);
        AtomicFile.WriteText(path, Source);
        return path;
    }

    /// <summary>
    /// The hook for one session, or null where none can be handed: an id that names no folder, a node that cannot run, a home
    /// that will not take the script. Then the session's children are known by the stream and the calls alone, as said.
    /// </summary>
    public static LineageHook? HookFor(string home, string sessionId)
    {
        if (!SessionShape.IsMatch(sessionId)) return null;
        var node = Tools.Resolve(home, "node");
        if (node.Refused) return null;
        try
        {
            return new LineageHook(Install(home), PathOf(home, sessionId)) { Node = node.File ?? "node" };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>One line appended to the session's file. Never throws.</summary>
    public static void Append(string home, string sessionId, string line)
    {
        if (!SessionShape.IsMatch(sessionId)) return;
        var path = PathOf(home, sessionId);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = Utf8.GetBytes(line + "\n");
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                    stream.Write(bytes);
                    return;
                }
                catch (IOException) when (attempt < 80 && !Directory.Exists(path))
                {
                    Thread.Sleep(25);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A line lost is a lineage unknown.
        }
    }

    /// <summary>
    /// The part's end, where the session has lineage: clean, or killed, stopped or failed, which leaves each child its harness
    /// said nothing more of lost. A session with no file gets none.
    /// </summary>
    public static void End(string home, string sessionId, bool clean, DateTimeOffset at)
    {
        if (!SessionShape.IsMatch(sessionId) || !File.Exists(PathOf(home, sessionId))) return;
        Append(home, sessionId, ChildBindings.EndLine(clean, at));
    }

    /// <summary>The part's children, as its lineage says; none where it has no file.</summary>
    public static ChildBindings Read(string home, string sessionId)
    {
        if (!SessionShape.IsMatch(sessionId)) return ChildBindings.Fold([]);
        var path = PathOf(home, sessionId);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Utf8);
            return ChildBindings.Fold(reader.ReadToEnd().Split('\n'));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return ChildBindings.Fold([]);
        }
    }

    /// <summary>
    /// What reads Claude Code's stream for one run (<see cref="ChildBindings.StreamLine"/>): told every line, it appends each
    /// subagent's start and end. Every other harness's lines say none.
    /// </summary>
    public static Action<string> Watch(string home, string sessionId)
    {
        var agents = new HashSet<string>(StringComparer.Ordinal);
        return line =>
        {
            if (ChildBindings.StreamLine(line, DateTimeOffset.UtcNow, agents) is { } said) Append(home, sessionId, said);
        };
    }

    private static string ReadSource()
    {
        using var stream = typeof(ChildLineage).Assembly.GetManifestResourceStream("Daoris.Driver.lineage-hook.mjs")
            ?? throw new InvalidOperationException("the lineage hook's script is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
