namespace Daoris.Driver;

/// <summary>What a promote run answered (<see cref="IntegrationPromoteRuns"/>), in words, with the candidate and its members as they now stand.</summary>
internal sealed record IntegrationPromoteRun(string Kind, string Said)
{
    public IntegrationCandidate? Candidate { get; init; }

    public IReadOnlyList<IntegrationMember> Members { get; init; } = [];

    /// <summary>The verdict's action the run acted on (<see cref="VerdictActions"/>); null where none was asked.</summary>
    public string? Verdict { get; init; }
}

/// <summary>What a promote run answers.</summary>
internal static class IntegrationPromoteRuns
{
    /// <summary>The line holds the candidate's tip, every member is settled, the landed branches are tidied and the close ended the candidate <c>settled</c>.</summary>
    public const string Settled = "settled";

    /// <summary>The line holds the tip, and git could not judge a member or keep its work on a recovery ref: the member stands, the candidate stands, and the settle is asked again at the next look.</summary>
    public const string Unsettled = "unsettled";

    /// <summary>A requester's authority no longer stood at promotion: its member is held, nothing was moved, and the candidate is formed again without it.</summary>
    public const string Lapsed = "lapsed";

    /// <summary>The step could not go on and ended <c>held</c>, said why (a hold verdict, the fence, a checkout that is not clean): tried again at the next look.</summary>
    public const string Held = "held";

    /// <summary>The line moved under the candidate: nothing was moved and the candidate is formed again.</summary>
    public const string Moved = "moved";

    /// <summary>The verdict was unknown, busy, not a pass or not for this tip, or the gates could not run: nothing was written, and it is asked again at the next look.</summary>
    public const string Waiting = "waiting";

    /// <summary>A hand back: each named member was handed back and the rest are formed again.</summary>
    public const string HandedBack = "handed-back";

    /// <summary>A split: the failing members were handed back and the rest are formed again, one at a time.</summary>
    public const string Split = "split";

    /// <summary>The line's integration stopped, and the candidate with it.</summary>
    public const string Stopped = "stopped";

    /// <summary>The candidate was dropped and its requests closed.</summary>
    public const string Dropped = "dropped";

    /// <summary>A resume found something other than a step to run (the candidate ended, git could not say, the line was moved back): nothing more was written.</summary>
    public const string Resumed = "resumed";

    /// <summary>git could not say where the line stands: nothing was written.</summary>
    public const string Unread = "unread";

    /// <summary>The candidate's members are not all replayed yet, or it has no tip: its replay goes first.</summary>
    public const string Replaying = "replaying";
}
