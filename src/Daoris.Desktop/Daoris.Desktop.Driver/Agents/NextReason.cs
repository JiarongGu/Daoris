namespace Daoris.Driver;

/// <summary>
/// Why the next start of a scope would take its account: a closed set
/// the screen translates, the walk's steps among them by the log's own names. Read without starting anything.
/// </summary>
public enum NextReason
{
    /// <summary>No default and no list name an account: the tool's own sign-in.</summary>
    Own,

    /// <summary>The scope names one account: a default and no list, or a list of one.</summary>
    Named,

    /// <summary>The list names others, and every other is held.</summary>
    OnlyReady,

    /// <summary>A conversation: the kept account first where it is the default, or the kept account taken last.</summary>
    Kept,

    /// <summary>
    /// The account it is weighed against is near its limit (<see cref="AccountReadings.Near"/>), so it goes last.
    /// </summary>
    Near,

    /// <summary>It runs fewer of Daoris's sessions (step 3).</summary>
    Fewest,

    /// <summary>
    /// Within its week's last day, more of its week would lapse at its reset, per hour left, than of the other's.
    /// </summary>
    Lapsing,

    /// <summary>It has more room under its curve on its tightest window, or under <c>switch: off</c> is further behind its week's pace (step 5).</summary>
    Pace,

    /// <summary>Daoris started on it less recently (step 6).</summary>
    LeastRecent,

    /// <summary>Daoris has not started on it yet.</summary>
    NotStarted,

    /// <summary>The list's order, begun at its default: nothing else told the two apart (step 7, or <c>order</c>'s walk).</summary>
    List,

    /// <summary>No account the scope may use is ready: the start waits.</summary>
    Waits,

    /// <summary>No Daoris account of the agent's exists for the scope to take: its starts wait until a sign-in.</summary>
    NoAccount,
}
