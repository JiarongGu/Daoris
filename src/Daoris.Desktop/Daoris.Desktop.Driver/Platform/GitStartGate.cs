namespace Daoris.Driver;

/// <summary>
/// What the git gate refuses before it places a start, read from the ask alone: under the tests' strict run, a start whose command or verb
/// the table of git's commands has no row for.
/// </summary>
internal static class GitStartGate
{
    /// <summary>Whether a start the table cannot read is refused: set by the tests, never at run time.</summary>
    internal static bool Strict { get; set; }

    /// <summary>Throws where <paramref name="ask"/> may not start; returns where it may.</summary>
    /// <exception cref="GitUnplacedException">Said as <c>unrowed</c>.</exception>
    public static void Check(GitRunAsk ask)
    {
        if (ask.Command is { Rowed: false } && Strict)
        {
            throw new GitUnplacedException("unrowed", "this git's command has no row in the table of git's commands, which the strict run refuses");
        }
    }
}
