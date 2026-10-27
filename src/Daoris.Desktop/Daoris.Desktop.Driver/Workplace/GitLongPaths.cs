namespace Daoris.Driver;

/// <summary>
/// A session's own git reads its tree with long paths, as Daoris's does (TREELONG1, D109's note): Daoris's own git says
/// <c>-c core.longpaths=true</c> on every call (<see cref="WorkingTree.GitStart"/>), but a tree under the trees home sits deeper than
/// its repository's checkout, so a file that fits there passes Windows' 260 characters in the tree, and a git without the setting
/// reads it as deleted. Handed in the command scope of every session's start beside the author's, so nothing is written into the
/// repository, unless the person's own configuration already says it, whichever way.
/// </summary>
public static class GitLongPaths
{
    /// <summary>The setting, and the value a session is handed.</summary>
    public const string Key = "core.longpaths";

    /// <summary>
    /// Whether a start in <paramref name="root"/> is handed the setting: false where the person's configuration (any scope but the
    /// command's, which is Daoris's own <c>-c</c> on this read) already names it.
    /// </summary>
    public static async Task<bool> NeededAsync(string root, CancellationToken ct)
    {
        var (code, scoped, _) = await WorkingTree.GitAsync(root, ["config", "--show-scope", "--get-all", Key], ct).ConfigureAwait(false);
        return code != 0 || !Says(scoped);
    }

    /// <summary>Whether <c>git config --show-scope</c>'s lines hold the setting from a scope of the person's, not the command's.</summary>
    public static bool Says(string scoped) =>
        scoped.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(line => !line.StartsWith("command\t", StringComparison.Ordinal));

    /// <summary>
    /// Hand the setting to a start's command scope where <paramref name="needed"/>, and where the environment's command scope does
    /// not already name it.
    /// </summary>
    public static void Hand(IDictionary<string, string?> environment, bool needed)
    {
        if (!needed || Scoped(environment)) return;
        GitRouting.Scope(environment, [(Key, "true")]);
    }

    private static bool Scoped(IDictionary<string, string?> environment)
    {
        var count = environment.TryGetValue("GIT_CONFIG_COUNT", out var held) && int.TryParse(held, out var number) && number > 0 ? number : 0;
        for (var index = 0; index < count; index++)
        {
            if (environment.TryGetValue($"GIT_CONFIG_KEY_{index}", out var key) && string.Equals(key, Key, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
