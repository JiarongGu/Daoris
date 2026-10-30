namespace Daoris.Driver;

/// <summary>A plugin's answer at a decision point (D64 §4): allow, or hold with the sentence a person reads.</summary>
public sealed record HookDecision(bool Allowed, string? Reason = null)
{
    public static readonly HookDecision Allow = new(true);

    public static HookDecision Hold(string reason) => new(false, reason);
}


/// <summary>
/// What <see cref="HookPoints.StateChanged"/> is told (STATEHUB1g): the shell's <c>STATE_CHANGED</c>, as every window hears it
/// (D169 point 3). What moved, never what it is now: no words, no answer, no machine path.
/// </summary>
/// <param name="Scopes">The families of state that moved, in the screen's words (<c>sessions</c>, <c>landing</c>, …).</param>
/// <param name="Sessions">The sessions the change was about, or null where it was not one session's: any.</param>
public sealed record StateChangedFrame(IReadOnlyList<string> Scopes, IReadOnlyList<string>? Sessions);

/// <summary>
/// What each point is sent, built in one place (PLUG8): the loop and the landing send these, and the
/// plugin kit's sample frames are built by the same functions, so a sample cannot say what the driver
/// does not. Every name is spelled here rather than left to a serializer's policy.
/// </summary>
public static class HookFrames
{
    /// <summary><see cref="HookPoints.QuestConsider"/>: one planned start.</summary>
    public static object Consider(Consideration start) => new
    {
        quest = new { id = start.Quest.Id, title = start.Quest.Title, from = start.Quest.From, to = start.Quest.To },
        repository = start.Quest.To,
        workspace = start.Workspace,
        root = start.Root,
    };

    /// <summary><see cref="HookPoints.SessionEnded"/>: what a tick concluded.</summary>
    public static object Ended(SessionEnded ended) => new
    {
        session = ended.Session,
        quest = ended.Quest,
        repository = ended.Repository,
        state = ended.State,
        adapter = ended.Adapter,
        account = ended.Account,
        byPerson = ended.ByPerson,
        note = ended.Note,
        reason = ended.Reason,
        @class = ended.Class,
    };

    /// <summary>
    /// <see cref="HookPoints.Land"/>: the branch a landing just made (D100), or moved on (LAND2c): the pull request already open
    /// from it, which a plugin grows rather than opening a second, who accepted the work, and the commit judged, which it pushes
    /// (LANDPIN2).
    /// </summary>
    public static object Land(LandingFrame frame) => new
    {
        repository = frame.Repository,
        workspace = frame.Workspace,
        root = frame.Root,
        branch = frame.Branch,
        @base = frame.Base,
        title = frame.Title,
        quest = frame.Quest is { } quest ? new { id = quest, title = frame.Title } : null,
        session = frame.Session,
        commits = frame.Commits.Select(commit => new { sha = commit.Sha, subject = commit.Subject }).ToArray(),
        pullRequest = frame.PullRequest,
        acceptedBy = frame.AcceptedBy,
        tip = frame.Tip,
        origin = frame.Origin,
    };

    /// <summary>
    /// <see cref="HookPoints.State"/>: a landed branch, asked about (PLUGHOOK1a, design §2.2): where it is, the line, and the pull
    /// request and the commit the record holds, so a plugin finds the pull request by its address, else by its source branch; and
    /// <c>origin</c>, as the land frame carries it (GITAUTH1b).
    /// </summary>
    public static object State(StateFrame frame) => new
    {
        repository = frame.Repository,
        workspace = frame.Workspace,
        root = frame.Root,
        branch = frame.Branch,
        line = frame.Line,
        pullRequest = frame.PullRequest,
        pushedTip = frame.PushedTip,
        origin = frame.Origin,
    };

    /// <summary>
    /// <see cref="HookPoints.StateChanged"/>: what moved (STATEHUB1g), the event's own two fields, <c>sessions</c> null where the
    /// change was about no one session.
    /// </summary>
    public static object Changed(StateChangedFrame frame) => new
    {
        scopes = frame.Scopes.ToArray(),
        sessions = frame.Sessions?.ToArray(),
    };
}
