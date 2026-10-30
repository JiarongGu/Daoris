namespace Daoris.Driver;

/// <summary>
/// What a driven turn's end reads of its quest and its tree (LANDBACK1 part 2, D87's note): whether the quest stands closed, with how many
/// uncommitted paths its tree holds, and the words that ask its session to commit them by its repository's conventions. The driver
/// takes the look (<c>TurnCarries</c>); the protocol door only reads it.
/// </summary>
/// <param name="Eligible">
/// The quest is done, or taken and submitted, and the person did not stop the session: only that is carried on. A parked quest asks
/// the person, an ask-and-wait waits on another repository (D79), a stand-down or a failure is no work to commit.
/// </param>
/// <param name="Paths">How many paths the tree holds uncommitted; none where the tree could not be read (an unread tree is not a yes).</param>
/// <param name="Words">Daoris's own prompt, written in the session's conversation as the go-on is.</param>
/// <param name="Holds">
/// Read again under the write lock as the prompt is written: false where the person stopped the session since the look was taken, and
/// nothing is written (<see cref="AcpTurnedAway"/>). Null always holds.
/// </param>
/// <param name="Limit">
/// How many times this run carries its session on, counting a carry-on that changed nothing: the look's own bound, which travels with
/// it. A turn's end is carried on <see cref="Bound"/> times; a closed quest's resume once (part 3).
/// </param>
public sealed record TurnCarry(bool Eligible, int Paths, string Words, Func<bool>? Holds = null, int Limit = TurnCarry.Bound)
{
    /// <summary>
    /// How many times one run carries its session on for a done quest's uncommitted tree at a turn's end, the default <see cref="Limit"/>.
    /// One number with the Accept's (<c>LandCarry.Bound</c>, part 1): a session is asked twice to clean its tree, by whichever door.
    /// </summary>
    public const int Bound = 2;

    /// <summary>Nothing is owed: the quest is not one a carry-on is owed for, or its tree is clean or unread.</summary>
    public static TurnCarry None { get; } = new(false, 0, "");

    /// <summary>A closed quest whose tree holds uncommitted work: what the session is carried on for.</summary>
    public bool Dirty => Eligible && Paths > 0;
}
