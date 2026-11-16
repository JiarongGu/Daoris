namespace Daoris.Driver;

/// <summary>
/// Why a run's <c>land</c> did nothing at a look, by a code: each exit of its keeping and its judgement (<see cref="LandPerformer"/>) that
/// says nothing else, written to the machine log once per change of reason (<see cref="NodeWaits"/>).
/// </summary>
public static class LandWaits
{
    public const string Event = "landing.waits";

    /// <summary>No landing door is wired in this loop.</summary>
    public const string NoDoor = "no-door";

    /// <summary>The run has no branch to land.</summary>
    public const string NoBranch = "no-branch";

    /// <summary>The node is bound at no commit.</summary>
    public const string NoTip = "no-tip";

    /// <summary>No session of the run has a tree and a quest.</summary>
    public const string NoSession = "no-session";

    /// <summary>The session's record is a partner's, which lands nothing, said already.</summary>
    public const string Partner = "partner";

    /// <summary>Its work landed at this tip already.</summary>
    public const string Landed = "landed";

    /// <summary>Its tree's slot is another line's until the tree is brought back.</summary>
    public const string Away = "tree-away";

    /// <summary>A live session other than the run's own waiting part uses its tree (<see cref="LandTreeUse"/>).</summary>
    public const string InUse = "tree-in-use";

    /// <summary>A newer session went on in its tree, said already.</summary>
    public const string Superseded = "superseded";

    /// <summary>The quest is held for the person's yes or evidence's verdict, said already.</summary>
    public const string Held = "held";

    /// <summary>The graph holds it, said already.</summary>
    public const string Refused = "refused";

    /// <summary>A refusal, tried again only once its tree or the tip it started at moves.</summary>
    public const string Unchanged = "unchanged";

    /// <summary>A second opinion or a review it waits on, said already.</summary>
    public const string Waiting = "waiting";

    public const string Busy = "busy";

    public const string Climbed = "climbed";

    public static LandingLine Line(LandStart start, string reason) => new(Event,
        [("run", start.Run), ("node", start.Node), ("repository", start.Repository), ("workspace", start.Workspace), ("reason", reason)]);

    public static string Sentence(string run, string node, string reason) => $"run {run}: `{node}` waits ({reason}): {Words(reason)}";

    private static string Words(string reason) => reason switch
    {
        NoDoor => "no landing door is wired in this loop, so nothing lands it.",
        NoBranch => "the run has no branch to land.",
        NoTip => "its binding names no commit to land.",
        _ => reason,
    };
}
