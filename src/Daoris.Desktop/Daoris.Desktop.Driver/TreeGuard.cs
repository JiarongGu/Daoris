namespace Daoris.Driver;

/// <summary>What one session's tree guard is: the script the hook runs, and the tree it guards.</summary>
/// <param name="Script">The script, written under the Daoris home by <see cref="TreeGuard.Install"/>.</param>
/// <param name="Tree">The session's own working tree — the executor's, never re-derived by the hook.</param>
public sealed record TreeGuardHook(string Script, string Tree)
{
    /// <summary>
    /// The checkouts the person declared this session's repository may also write into (D107), each one more
    /// argument after the tree. Empty — the default — is the tree alone.
    /// </summary>
    public IReadOnlyList<string> Also { get; init; } = [];

    /// <summary>
    /// The node the harness starts the script with (TOOLS5, D121 §2.4): the file Tools resolves, by its whole path, so
    /// the guard runs on the node the person chose. The bare name only where the system's node is on no PATH, as before.
    /// </summary>
    public string Node { get; init; } = "node";
}

/// <summary>
/// The tree guard (PERM3): a PreToolUse hook Daoris ships, refusing a file write outside the session's
/// own tree — the one boundary a permission rule cannot say (design §3).
/// </summary>
/// <remarks>
/// <para><b>A script carried in the assembly, written under the home.</b> The hook runs on the
/// harness's side, in every session, so it is a small node script with no dependency. It travels
/// inside this assembly and is written to <c>&lt;home&gt;/hooks/</c> on first use — found the same way
/// in a scratch run, a test and an install, whatever the publish layout — and put back as shipped if
/// anybody changed it, because the hook runs what the file says.</para>
///
/// <para><b>What it covers, and what it does not.</b> It judges the tools that name a file: Write,
/// Edit, MultiEdit and NotebookEdit, by their path fields, resolved through links. A shell command's
/// writes cannot be judged by reading the command, so Bash is not its call. There the harness's own
/// working-directory boundary stands, and a command runs only if a rule allowed it.</para>
///
/// <para>🔴 <b>Structurally, never by an exit code</b> — the harness reads any exit but 2 as a
/// non-blocking error, and HELP3's probe 4 saw PowerShell collapse a native one. And a hook that fails
/// or times out does not block at all, so this is defence beside the working-directory boundary, never
/// instead of it.</para>
/// </remarks>
public static class TreeGuard
{
    /// <summary>Where under the home the script is written.</summary>
    public const string Folder = "hooks";

    public const string ScriptName = "tree-guard.mjs";

    /// <summary>The tools it judges — exact alternatives, the harness's own matcher shape.</summary>
    public const string Matcher = "Edit|Write|MultiEdit|NotebookEdit";

    /// <summary>The script as shipped, from the assembly, with LF line endings.</summary>
    public static string Source { get; } = ReadSource();

    /// <summary>The script under the home, written if absent or not as shipped; answers its path.</summary>
    public static string Install(string home)
    {
        var folder = Path.Combine(home, Folder);
        var path = Path.Combine(folder, ScriptName);
        if (File.Exists(path) && File.ReadAllText(path) == Source) return path;

        Directory.CreateDirectory(folder);
        AtomicFile.WriteText(path, Source);
        return path;
    }

    /// <summary>The guard for one session: the script, installed, the tree it guards, the declared targets, and its node.</summary>
    /// <exception cref="DriverException">
    /// Node.js is run a way that cannot run (TOOLS5): the session is not started rather than handed a guard that would
    /// start whatever node <c>PATH</c> finds, or none — and whether a harness then lets the write through is its own rule.
    /// </exception>
    public static TreeGuardHook For(string home, string tree, IEnumerable<string>? also = null)
    {
        var node = Tools.Resolve(home, "node");
        if (node.Refused) throw new DriverException($"the tree guard runs on Node.js, and {node.Problem}");
        return new(Install(home), Path.GetFullPath(tree)) { Also = [.. (also ?? []).Select(Path.GetFullPath)], Node = node.File ?? "node" };
    }

    private static string ReadSource()
    {
        using var stream = typeof(TreeGuard).Assembly.GetManifestResourceStream("Daoris.Driver.tree-guard.mjs")
            ?? throw new InvalidOperationException("the tree guard's script is missing from the build");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
