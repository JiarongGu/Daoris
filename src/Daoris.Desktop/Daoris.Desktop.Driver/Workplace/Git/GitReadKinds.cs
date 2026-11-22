namespace Daoris.Driver;

/// <summary>
/// The read kind of a git ask as the tally counts it: the git verb past git's own options, a walk that marks equivalent patches told
/// apart as <c>cherry</c>, a start with no command (a version) as <c>meta</c>. Finer than <see cref="GitKind"/>, which classes a start for its launch count.
/// </summary>
internal static class GitReadKinds
{
    internal static string Of(IReadOnlyList<string> arguments)
    {
        var command = GitLaunch.Command(arguments);
        if (command.Count == 0) return GitRunAsk.DefaultKind;
        var verb = command[0];
        return verb is "log" or "rev-list" && command.Any(word => word.StartsWith("--cherry", StringComparison.Ordinal)) ? "cherry" : verb;
    }
}
