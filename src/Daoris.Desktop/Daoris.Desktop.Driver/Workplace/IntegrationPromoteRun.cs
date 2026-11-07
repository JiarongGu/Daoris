namespace Daoris.Driver;

/// <summary>What a promote run answered (<see cref="IntegrationPromoteRuns"/>), in words, with the candidate and its members as they now stand.</summary>
internal sealed record IntegrationPromoteRun(string Kind, string Said)
{
    public IntegrationCandidate? Candidate { get; init; }

    public IReadOnlyList<IntegrationMember> Members { get; init; } = [];

    /// <summary>The verdict's action the run acted on (<see cref="VerdictActions"/>); null where none is asked.</summary>
    public string? Verdict { get; init; }
}

/// <summary>What a promote run answers.</summary>
internal static class IntegrationPromoteRuns
{
    /// <summary>The line holds the candidate's tip, every member is settled, the landed branches are tidied and the close ended the candidate <c>settled</c>.</summary>
    public const string Settled = "settled";

    /// <summary>The line holds the tip, and git could not judge a member or keep its work on a recovery ref: the member stands, the candidate stands, and the settle is asked again at the next look.</summary>
    public const string Unsettled = "unsettled";

    /// <summary>A requester's authority no longer stood at promotion: its member is held, nothing is moved, and the candidate is formed again without it.</summary>
    public const string Lapsed = "lapsed";

    /// <summary>The step could not go on and ended <c>held</c>, said why (a hold verdict, the fence, a checkout that is not clean): tried again at the next look.</summary>
    public const string Held = "held";

    /// <summary>The line moved under the candidate: nothing is moved and the candidate is formed again.</summary>
    public const string Moved = "moved";

    /// <summary>The verdict is unknown, busy, not a pass or not for this tip, or the gates could not run: nothing is written, and it is asked again at the next look.</summary>
    public const string Waiting = "waiting";

    /// <summary>A hand back: each named member is handed back and the rest are formed again.</summary>
    public const string HandedBack = "handed-back";

    /// <summary>A split: the failing members were handed back and the rest are formed again, one at a time.</summary>
    public const string Split = "split";

    /// <summary>The line's integration stopped, and the candidate with it.</summary>
    public const string Stopped = "stopped";

    /// <summary>The candidate is dropped and its requests are closed.</summary>
    public const string Dropped = "dropped";

    /// <summary>A resume found something other than a step to run (the candidate ended, git could not say, the line moved back): nothing more is written.</summary>
    public const string Resumed = "resumed";

    /// <summary>git could not say where the line stands: nothing is written.</summary>
    public const string Unread = "unread";

    /// <summary>The candidate's members are not all replayed yet, or it has no tip: its replay goes first.</summary>
    public const string Replaying = "replaying";
}
