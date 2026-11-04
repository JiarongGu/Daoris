namespace Daoris.Driver;

/// <summary>What this machine's wiring is and where the answer came from: <c>environment</c> when the pair is set, <c>file</c> when the map named something, <c>none</c> otherwise.</summary>
public sealed record RemoteWiring(string Source, string? Path, IReadOnlyDictionary<string, RemoteTarget> Remotes)
{
    public const string Environment = "environment";
    public const string File = "file";
    public const string None = "none";

    /// <summary>The wiring over <paramref name="environment"/> and the map at <paramref name="path"/>, which is null on a machine with no home.</summary>
    public static RemoteWiring Read(Func<string, string?> environment, string? path)
    {
        if (RemoteTarget.FromEnvironment(environment) is { } fromEnvironment) return new(Environment, path, fromEnvironment);
        var remotes = RemoteTarget.Load(environment, path);
        return new(remotes.Count > 0 ? File : None, path, remotes);
    }
}

/// <summary>
/// What <c>remote</c> says (ONEWAY1u, D194 point 6): one words function per verb over what the command read and the operation's result,
/// the lines the TypeScript verb said (<c>fixtures/driver-files-text.json</c>). A key is never printed, only the audit prefix.
/// </summary>
[CommandGroup("remote", "list", "add", "remove", "sync", Nested = ["sync: status dismiss forward"])]
public static class RemoteWords
{
    public static FileSaid List(RemoteWiring wiring)
    {
        List<string> lines = [];
        // No home is no map: the same answer as no remote, and the sentence says where one would go.
        var where = wiring.Path ?? "the home's remotes.json, once DAORIS_HOME names one";
        if (wiring.Source == RemoteWiring.Environment)
        {
            lines.Add($"daoris: the environment names this machine's remote, whole — {where} is not read.");
            lines.Add($"  ({RemoteTarget.UrlVariable} / {RemoteTarget.KeyVariable}, for the workspace {RemoteTarget.WorkspaceVariable} names.)");
        }

        if (wiring.Remotes.Count == 0)
        {
            lines.Add("daoris: no remote on this machine — every workspace stays local, which is the default (D21).");
            lines.Add($"  `daoris remote add <workspace> --url https://… --key dk_…` wires one ({where}).");
            return FileSaid.Said(lines);
        }

        if (wiring.Source == RemoteWiring.File) lines.Add($"daoris: {wiring.Path}");
        foreach (var (workspace, remote) in wiring.Remotes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            lines.Add($"  {workspace.PadRight(16)} {remote.Url}  {RemoteTarget.Redact(remote.Key)}");
        }

        return FileSaid.Said(lines);
    }

    /// <summary>The line said before the key is asked for.</summary>
    public static string Wiring(string workspace) => $"daoris: wiring `{workspace}` on this machine";

    /// <summary>The line said when the key came from the environment.</summary>
    public static string KeyFromEnvironment() => $"  key from {RemoteTarget.KeyVariable}";

    public static IReadOnlyList<string> Added(RemoteSet set, string path)
    {
        List<string> lines = [$"  {set.Workspace}  {set.Url}  {RemoteTarget.Redact(set.Key)}"];
        if (set.Replacing is not null) lines.Add($"  (replacing {set.Replacing} — the previous key is untouched at its deployment)");
        lines.Add($"  written to {path} — machine-local, tracked by nothing (D47 §7).");
        lines.Add("  Repositories feed it where their own manifest says they may; the map only says where.");
        return lines;
    }

    public static IReadOnlyList<string> Removed(string workspace, bool removed) => removed
        ?
        [
            $"daoris: `{workspace}` is no longer wired on this machine.",
            "  Nothing at the deployment changed: the key stays valid there until an operator revokes",
            "  it, and what that workspace already fed stays where it is. This workspace now syncs",
            "  nowhere from here, silently — which is what having no remote has always meant.",
        ]
        : [$"daoris: `{workspace}` is not wired on this machine — nothing to remove."];
}
