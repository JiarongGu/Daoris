namespace Daoris.Driver;

/// <summary>
/// Whether a git may be ended once nobody waits for it: a read (<see cref="RefSnapshot.MayWrite"/> calls it one) that takes no lock.
/// A <c>status</c> refreshes the index under its lock unless told <c>--no-optional-locks</c>; a diff against the working tree refreshes
/// and locks it whatever that says, unless also told <c>-c diff.autoRefreshIndex=false</c>; <c>describe --dirty</c> refreshes it as a
/// status does. A command it does not know is not endable.
/// </summary>
internal static class GitEndable
{
    internal static bool Of(IReadOnlyList<string> arguments)
    {
        if (RefSnapshot.MayWrite(arguments)) return false;
        var command = GitLaunch.Command(arguments);
        if (command.Count == 0) return false;
        var options = arguments.Take(arguments.Count - command.Count).ToList();
        var unlocked = options.Contains("--no-optional-locks");
        return GitLaunch.KindOf(arguments) switch
        {
            GitKind.Status => unlocked,
            GitKind.WorktreeDiff => unlocked && Configured(options, "diff.autoRefreshIndex", "false"),
            _ when command[0] == "describe" && command.Any(word => word.StartsWith("--dirty", StringComparison.Ordinal)) => unlocked,
            _ => true,
        };
    }

    /// <summary>Whether git's own options set <paramref name="key"/> to <paramref name="value"/> (<c>-c key=value</c>, the key blind to case).</summary>
    internal static bool Configured(IReadOnlyList<string> options, string key, string value)
    {
        for (var at = 0; at + 1 < options.Count; at++)
        {
            if (options[at] == "-c" && options[at + 1].Split('=', 2) is [var named, var set]
                && named.Equals(key, StringComparison.OrdinalIgnoreCase) && set.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
