namespace Daoris.Driver;

/// <summary>
/// The <c>daoris-plugins</c> server one session is handed, as a plugin's declared server is: the desktop's program run as
/// <c>plugin tools</c> for that session, its place and language, the home named in its own variables (a harness may hand a server only
/// those). Handed only where the place lists a tool, and only where the program is found.
/// </summary>
public static class PluginToolsHand
{
    private const string Folder = "daoris-driver";

    /// <summary>The entry, or null where no tool is listed at the place or no program runs it.</summary>
    /// <param name="program">The desktop's program; <see cref="Program"/>'s answer where a caller has none of its own.</param>
    public static ServerEntry? Entry(string home, PluginToolsAt at, string? program)
    {
        if (program is null || PluginToolList.At(home, at.Place).Count == 0) return null;
        List<string> words = ["plugin", "tools", "--session", at.Session];
        if (at.Place.Repository is { Length: > 0 } repository) words.AddRange(["--repository", repository]);
        if (at.Place.Workspace is { Length: > 0 } workspace) words.AddRange(["--workspace", workspace]);
        words.AddRange(["--language", at.Language]);
        if (at.Plugin is { } plugin) words.AddRange(["--plugin", plugin]);
        if (at.Step is { } step) words.AddRange(["--step", step]);
        return new ServerEntry(
            PluginToolsServer.Name, program, words,
            new Dictionary<string, string>(StringComparer.Ordinal) { [DaorisHome.Variable] = Path.GetFullPath(home) });
    }

    /// <summary>
    /// A step's session's entry: <paramref name="plugin"/>'s tools alone and <c>step_answer</c> for the attempt <paramref name="step"/> it
    /// performs, listed or not, its calls made as the session <paramref name="at"/> names.
    /// </summary>
    public static ServerEntry StepEntry(string home, PluginToolsAt at, string program, string plugin, string step)
    {
        List<string> words = ["plugin", "tools", "--session", at.Session, "--repository", at.Place.Repository ?? "", "--language", at.Language, "--plugin", plugin, "--step", step];
        if (at.Place.Workspace is { Length: > 0 } workspace) words.AddRange(["--workspace", workspace]);
        return new ServerEntry(
            PluginToolsServer.Name, program, words,
            new Dictionary<string, string>(StringComparer.Ordinal) { [DaorisHome.Variable] = Path.GetFullPath(home) });
    }

    /// <summary>
    /// The desktop's program: the install's beside the home (<c>app/daoris-driver/</c>), else this process where it is that program; null
    /// where neither is (a development shell runs the library inside its own program).
    /// </summary>
    public static string? Program(string home)
    {
        var exe = OperatingSystem.IsWindows() ? Folder + ".exe" : Folder;
        if (DaorisHome.InstallBin(home) is { } bin && Path.GetDirectoryName(bin) is { } app && Path.Combine(app, Folder, exe) is var laid && File.Exists(laid))
            return laid;
        return Environment.ProcessPath is { } own && string.Equals(Path.GetFileName(own), exe, StringComparison.OrdinalIgnoreCase) ? own : null;
    }

    /// <summary>The words <c>plugin tools</c> takes, read back: the session, the place and the language; null where the session is not named.</summary>
    public static PluginToolsAt? Read(IReadOnlyList<string> words)
    {
        string? session = null, repository = null, workspace = null, language = null, plugin = null, step = null;
        for (var at = 0; at + 1 < words.Count; at += 2)
        {
            switch (words[at])
            {
                case "--session": session = words[at + 1]; break;
                case "--repository": repository = words[at + 1]; break;
                case "--workspace": workspace = words[at + 1]; break;
                case "--language": language = words[at + 1]; break;
                case "--plugin": plugin = words[at + 1]; break;
                case "--step": step = words[at + 1]; break;
                default: return null;
            }
        }

        return session is { Length: > 0 }
            ? new(session, new PluginPlace(repository is { Length: > 0 } ? repository : null, workspace), language is "zh" ? "zh" : "en") { Plugin = plugin, Step = step }
            : null;
    }
}
