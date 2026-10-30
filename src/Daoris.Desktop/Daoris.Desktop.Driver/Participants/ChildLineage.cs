using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A session's lineage file, where its native children are bound to its part (FABRIC-C1a, D173): the connector writes a line
/// per call with the lineage its harness carried, the hook this driver hands Claude Code writes what it says of each agent, and
/// this driver writes each process's start and end (COWORKVIEW1a) and what Claude Code's stream and the protocol door say of each
/// subagent. Every line names the process it was written for where it has a token. <see cref="ChildBindings"/> reads them.
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
/// <para>It binds and records; it decides nothing a child may do. The connector does (FABRIC-C2a), reading this file's hook lines
/// itself (<c>HookJoin</c>); an agent id the hook could not read is <c>agentUnreadable</c>, which neither reader takes for the root's.</para>
/// </remarks>
public static class ChildLineage
{
    /// <summary>The file's name in the session's folder. 🔴 The connector's <c>LineageFile.FileName</c> is the same name.</summary>
    public const string FileName = "lineage.jsonl";

    /// <summary>The hook's script, under the home's <c>hooks/</c> beside the tree guard's.</summary>
    public const string ScriptName = "lineage-hook.mjs";

    /// <summary>The connector's tools, in the harness's own matcher shape: the calls whose agent the hook names.</summary>
    public const string Matcher = LineageHook.Matcher;

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
        if (File.Exists(path) && AtomicFile.ReadText(path) == Source) return path;

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
    /// A process's start, before its spawn (COWORKVIEW1a): it makes the file where there is none, so every line its process can
    /// write follows it.
    /// </summary>
    public static void Start(string home, string sessionId, string proc, string issuer, DateTimeOffset at)
    {
        if (ChildBindings.StartLine(proc, issuer, at) is { } line) Append(home, sessionId, line);
    }

    /// <summary>
    /// A process's end once its exit was read, where the session has lineage: clean, or killed, stopped or failed, naming the
    /// process where it has a token. A session with no file gets none.
    /// </summary>
    public static void End(string home, string sessionId, bool clean, DateTimeOffset at, string? proc = null)
    {
        if (!SessionShape.IsMatch(sessionId) || !File.Exists(PathOf(home, sessionId))) return;
        Append(home, sessionId, ChildBindings.EndLine(clean, at, proc));
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
    /// What one process's run tells its part's lineage (COWORKVIEW1a): each subagent's start and end Claude Code's stream says
    /// (<see cref="ChildBindings.StreamLine"/>), and each the protocol door announces and ends, every line naming
    /// <paramref name="proc"/> where it is a token.
    /// </summary>
    public static ILineageWriter Writer(string home, string sessionId, string? proc = null) => new ProcessWriter(home, sessionId, proc);

    private sealed class ProcessWriter(string home, string sessionId, string? proc) : ILineageWriter
    {
        private readonly HashSet<string> _agents = new(StringComparer.Ordinal);

        public void Stream(string line)
        {
            string? said;
            lock (_agents) said = ChildBindings.StreamLine(line, DateTimeOffset.UtcNow, _agents, proc);
            if (said is not null) Append(home, sessionId, said);
        }

        public void Spawned(LineageAnnouncement said)
        {
            if (ChildBindings.AcpStarted(said, DateTimeOffset.UtcNow, proc) is { } line) Append(home, sessionId, line);
        }

        public void Ended(string agent, string? state)
        {
            if (ChildBindings.AcpEnded(agent, state, DateTimeOffset.UtcNow, proc) is { } line) Append(home, sessionId, line);
        }
    }

    private static string ReadSource()
    {
        using var stream = typeof(ChildLineage).Assembly.GetManifestResourceStream("Daoris.Driver.lineage-hook.mjs")
            ?? throw new InvalidOperationException("the lineage hook's script is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
