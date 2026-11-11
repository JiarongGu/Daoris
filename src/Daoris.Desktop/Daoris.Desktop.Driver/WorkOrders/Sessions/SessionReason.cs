namespace Daoris.Driver;

/// <summary>
/// The finer reasons inside a state that are neither a record's state nor a <see cref="ShownState"/>: each read from the planner's
/// verdict on the session's quest or from its run. A pending one's reason is its <see cref="WordsHold"/> code, and a run's wait its
/// <see cref="PersonWaits"/> code.
/// </summary>
public static class SessionReason
{
    /// <summary>Needs you: a park asks the person a question.</summary>
    public const string Question = "question";

    /// <summary>Needs you: every wait of its run is the person's Accept.</summary>
    public const string Review = "review";

    /// <summary>Needs you: its role's policy holds the next move for the person's yes (<see cref="StartVerdict.PolicyHeld"/>).</summary>
    public const string PolicyHeld = "policy-held";

    /// <summary>Needs you: its quest is taken with nothing carrying it on, for the person's done (<see cref="StartVerdict.Unclosed"/>).</summary>
    public const string Unclosed = "unclosed";

    /// <summary>Needs you: the steward's stop holds its quest for a Try again.</summary>
    public const string StewardStop = "steward-stop";

    /// <summary>Needs you: its next start waits on a sign-in, with no account to reset.</summary>
    public const string SignIn = "sign-in";

    /// <summary>Needs you: its quest closed done with part left (<see cref="QuestLeft"/>, <see cref="PersonWaits.Left"/>); the person's archive sets it aside.</summary>
    public const string Left = PersonWaits.Left;

    /// <summary>Failed: it ended past its transient bound (<see cref="StartVerdict.TransientHeld"/>).</summary>
    public const string Transient = "transient";

    /// <summary>Working: it failed, and the planner carries its quest on.</summary>
    public const string CarriedOn = "carried-on";

    /// <summary>Working: its quest's next step waits on a second opinion still at work (<see cref="StartVerdict.WaitsForOpinion"/>).</summary>
    public const string Opinion = "opinion";

    /// <summary>Working: another machine, or work outside Daoris, holds its quest (<see cref="StartVerdict.TakenElsewhere"/>).</summary>
    public const string Elsewhere = "elsewhere";
}
