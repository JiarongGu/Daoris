namespace Daoris.Driver;

/// <summary>
/// Why no run is read for what was asked (CHATFLOW1a; the chat-flow design §10, D157's CHATFLOW1 note): a code the page words in en and
/// zh, and its English sentence, which the terminal says. The page's en catalogue says the same words, and both doors are held to the
/// twin table's <c>none</c> rows (<c>fixtures/workflow-run-said.json</c>, D50).
/// </summary>
/// <remarks>
/// A record's id and a workflow's id are set in backticks, as every name from a record is. No path is ever said (D48 §7).
/// </remarks>
public static class WorkflowNoRun
{
    /// <summary>No record by the asked session's id.</summary>
    public const string NoSession = "no-session";

    /// <summary>No quest by the asked id.</summary>
    public const string NoQuest = "no-quest";

    /// <summary>An ask whose chains are empty, an intake's included: it has published no quest yet.</summary>
    public const string AskNoQuest = "ask-no-quest";

    /// <summary>A chat whose tree is not one this home opened: it works in the person's checkout.</summary>
    public const string ChatCheckout = "chat-checkout";

    /// <summary>A chat in a tree of its own, before a conversation's work has a run of its own (CHATFLOW1c).</summary>
    public const string ChatOwnTree = "chat-own-tree";

    /// <summary>A chat in a tree of its own whose repository's workflow is a graph version, before CHATFLOW1f.</summary>
    public const string ChatGraphLater = "chat-graph-later";

    /// <summary>A chat whose tree is gone and was never bound.</summary>
    public const string ChatTreeGone = "chat-tree-gone";

    /// <summary>Ask Daoris's record (<see cref="HelpRoom.Repository"/>).</summary>
    public const string AskDaoris = "ask-daoris";

    /// <summary>A reviewer's record, reading another's work for a second opinion.</summary>
    public const string Reviewer = "reviewer";

    /// <summary>A helper's record.</summary>
    public const string Helper = "helper";

    /// <summary>A partner's record, writing one piece its lead lands.</summary>
    public const string Partner = "partner";

    /// <summary>A teammate's record, which ran on another machine.</summary>
    public const string Teammate = "teammate";

    /// <summary>The service did not answer, so nothing could be read.</summary>
    public const string Unanswered = "unanswered";

    /// <summary>Every code, in the design's order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        NoSession, NoQuest, AskNoQuest, ChatCheckout, ChatOwnTree, ChatGraphLater, ChatTreeGone, AskDaoris, Reviewer, Helper, Partner,
        Teammate, Unanswered,
    ];

    /// <summary>
    /// The code's English sentence: <paramref name="id"/> is the record's own id (a session's, a quest's or an ask's, as the code names),
    /// <paramref name="workflow"/> the workflow's id. A code these words do not hold is said as itself, never dropped.
    /// </summary>
    public static string Said(string code, string? id = null, string? workflow = null) => code switch
    {
        NoSession => $"No session `{id}` is recorded here.",
        NoQuest => $"No quest `#{id}` is here.",
        AskNoQuest => $"Nothing runs here yet: ask `#{id}` has published no quest.",
        ChatCheckout =>
            "This chat works in your checkout, not in a tree of its own, so its work runs no workflow: what it changes is yours to commit. "
            + "To give work a workflow, start a conversation with a tree of its own.",
        ChatOwnTree => "This chat works in a tree of its own, but this build reads no workflow for a conversation's work yet.",
        ChatGraphLater =>
            $"This chat's repository follows the workflow `{workflow}`, which this build does not run for a conversation's work yet, "
            + "so its work is landed as before.",
        ChatTreeGone => "This chat's tree is gone, and its work was never bound to a workflow, so there is no run to show.",
        AskDaoris =>
            "Ask Daoris works in its workspace's room, not in a repository's tree, and a repository's work is asked for through a quest, "
            + "so no workflow runs for its conversations.",
        Reviewer => "This session reads another's work for a second opinion and changes nothing of its own, so no workflow runs for it.",
        Helper => "This session helps another and changes nothing of its own, so no workflow runs for it: the session it helps has the run.",
        Partner => "This session writes one piece that the session that asked it lands, so that session's run is the run.",
        Teammate => "This session ran on a teammate's machine, which keeps its run.",
        Unanswered => "The service did not answer, so where this work stands could not be read. Try again.",
        _ => code,
    };
}
