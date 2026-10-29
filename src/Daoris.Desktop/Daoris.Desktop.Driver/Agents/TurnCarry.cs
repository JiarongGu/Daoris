namespace Daoris.Driver;

/// <summary>
/// What a driven turn's end reads of its quest and its tree (LANDBACK1 part 2, D87's note): whether the quest stands closed, with how many
/// uncommitted paths its tree holds, and the words that ask its session to commit them by its repository's conventions.
/// </summary>
/// <param name="Eligible">
/// The quest is done, or taken and submitted, and the person did not stop the session: only that is carried on. A parked quest asks
/// the person, an ask-and-wait waits on another repository (D79), a stand-down or a failure is no work to commit.
/// </param>
/// <param name="Paths">How many paths the tree holds uncommitted; none where the tree could not be read (an unread tree is not a yes).</param>
/// <param name="Words">Daoris's own prompt (<see cref="TurnCarries.WordsOf"/>), written in the session's conversation as the go-on is.</param>
/// <param name="Holds">
/// Read again under the write lock as the prompt is written: false where the person stopped the session since the look was taken, and
/// nothing is written (<see cref="AcpTurnedAway"/>). Null always holds.
/// </param>
public sealed record TurnCarry(bool Eligible, int Paths, string Words, Func<bool>? Holds = null)
{
    /// <summary>Nothing is owed: the quest is not one a carry-on is owed for, or its tree is clean or unread.</summary>
    public static TurnCarry None { get; } = new(false, 0, "");

    /// <summary>A closed quest whose tree holds uncommitted work: what the session is carried on for.</summary>
    public bool Dirty => Eligible && Paths > 0;
}

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
    /// The look: closed quests only (<paramref name="status"/> is done, or taken with its result submitted), then the tree read as the landing
    /// reads it (<see cref="AutoLander.ReadTreeAsync"/>). A tree that cannot be read, or has no commit, reads as owing nothing.
    /// </summary>
    internal static async Task<TurnCarry> LookAsync(
        bool closed, Func<bool> stopped, string tree, CancellationToken ct)
    {
        if (!closed || stopped()) return TurnCarry.None;
        var (tip, _, paths) = await AutoLander.ReadTreeAsync(tree, ct).ConfigureAwait(false);
        return tip is null ? TurnCarry.None : new TurnCarry(true, paths, WordsOf(paths), () => !stopped());
    }
}
