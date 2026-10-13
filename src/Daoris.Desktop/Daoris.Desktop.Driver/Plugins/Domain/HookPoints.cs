namespace Daoris.Driver;

/// <summary>A plugin's answer at a decision point (D64 §4): allow, or hold with the sentence a person reads.</summary>
public sealed record HookDecision(bool Allowed, string? Reason = null)
{
    public static readonly HookDecision Allow = new(true);

    public static HookDecision Hold(string reason) => new(false, reason);
}

/// <summary>
/// The points a plugin may listen on, named here once. A handshake naming one this build lacks is said once and never
/// asked there (PLUGHOOK1a, D148 point 8): a plugin that grew a point keeps the points this build has.
/// </summary>
public static class HookPoints
{
    /// <summary>A decision: one consideration the planner marked <i>Start</i>. The first hold in catalogue order ends the waterfall.</summary>
    public const string QuestConsider = "quest/consider";

    /// <summary>An observation: what a tick concluded. Contained — nothing a plugin says here changes anything.</summary>
    public const string SessionEnded = "session/ended";

    /// <summary>
    /// An act, after Daoris's own (WSR4, D100): a branch rule's landing has made its branch, and the plugin
    /// the rule names pushes it and opens the pull request for its platform. Spoken by the landing, never
    /// by the loop, and it can undo nothing: a failure here leaves the branch.
    /// </summary>
    public const string Land = "work/land";

    /// <summary>
    /// A query (PLUGHOOK1a, D148): a landed branch's pull request, asked of the plugin that pushed it, which only its platform
    /// knows. Asked only at an occasion that may remove something, and acted on only where git confirms the answer. An
    /// unanswered query is not known, never a default.
    /// </summary>
    public const string State = "work/state";

    public static readonly IReadOnlyList<string> All = [QuestConsider, SessionEnded, Land, State];

    /// <summary>The points the driver loop asks at — a plugin that speaks on none of them is not kept running beside it.</summary>
    public static readonly IReadOnlyList<string> Loop = [QuestConsider, SessionEnded];
}

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
    };

    /// <summary>
    /// <see cref="HookPoints.Land"/>: the branch a landing just made (D100), or moved on (LAND2c): the pull request already open
    /// from it, which a plugin grows rather than opening a second, and who accepted the work.
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
    };

    /// <summary>
    /// <see cref="HookPoints.State"/>: a landed branch, asked about (PLUGHOOK1a, design §2.2): where it is, the line, and the pull
    /// request and the commit the record holds, so a plugin finds the pull request by its address, else by its source branch.
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
    };
}
