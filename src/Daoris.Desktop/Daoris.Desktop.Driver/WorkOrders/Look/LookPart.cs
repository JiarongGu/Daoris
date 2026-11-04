namespace Daoris.Driver;

/// <summary>
/// Which part of the look a look runs. The whole look's only further parts are the remote sync at its head and the lost-claim
/// stop after it, so a wake defers those to the <c>look</c> topic's deadline and skips no judgement the whole look would have made.
/// </summary>
public enum LookPart
{
    /// <summary>
    /// The <c>look</c> topic's: the remote sync and the lost-claim stop, then the local plan. At the topic's deadline
    /// (<c>pollSeconds</c> after the last whole look), at the loop's start, after a failed look's pace, and on a plain nudge.
    /// </summary>
    Look,

    /// <summary>
    /// A wake's (a <c>ledger</c> family moved, a terminal's words kept, a session's end): the local plan alone, every part of the
    /// look but the remote sync and the lost-claim stop, the sync after an ending it reports included. The stop is a plan's too
    /// where a sync beside the looks ended since the last look asked, while a session here held a quest.
    /// </summary>
    Plan,
}
