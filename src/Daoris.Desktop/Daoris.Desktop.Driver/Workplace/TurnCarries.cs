namespace Daoris.Driver;

/// <summary>The driver's half of LANDBACK1 part 2: the look a turn's end takes, and the words it carries a session on with.</summary>
public static class TurnCarries
{
    /// <summary>
    /// The words that ask a session whose quest is done to clean its tree. Part 1's are an Accept's (<see cref="LandCarry.DirtyWords"/>:
    /// "Your work is accepted"), which nobody has said here: nothing is accepted, the session simply has not finished. They keep part 1's
    /// asks, that the session commits by its repository's conventions and creates, switches or pushes no branch, and say that Daoris
    /// commits nothing for it (D164's rejection).
    /// </summary>
    public static string WordsOf(int paths) =>
        $"Your quest is done, but your tree has {paths} uncommitted path(s). {LandCarry.CommitWords}. Daoris never runs `git add` or "
        + "`git commit` for you: whatever you leave uncommitted stays so.";

    /// <summary>
    /// The look: closed quests only (<paramref name="closed"/>: done, or taken with its result submitted), then the tree read as the landing
    /// reads it (<see cref="AutoLander.ReadTreeAsync"/>). A tree that cannot be read, or has no commit, reads as owing nothing.
    /// </summary>
    internal static async Task<TurnCarry> LookAsync(bool closed, Func<bool> stopped, string tree, CancellationToken ct)
    {
        if (!closed || stopped()) return TurnCarry.None;
        var (tip, _, paths) = await AutoLander.ReadTreeAsync(tree, ct).ConfigureAwait(false);
        return tip is null ? TurnCarry.None : new TurnCarry(true, paths, WordsOf(paths), () => !stopped());
    }
}
