namespace Daoris.Driver;

/// <summary>
/// Why an agent's ask to hand its session over came to nothing, by code (MIXAGENT1c): the state file's <c>code</c> and the note's
/// <c>why</c>. The page words each as <c>note.handover.why.*</c>; <see cref="Said"/> is the terminal's one English.
/// </summary>
public static class HandOverAskWhy
{
    /// <summary>The person's hand-over rule is off for the session's repository.</summary>
    public const string Off = "off";

    /// <summary>The person said no to it.</summary>
    public const string Declined = "declined";

    /// <summary>The session is a part a hand-over started, and its first turn had not ended: no second hand-over without a turn between.</summary>
    public const string JustHanded = "just-handed";

    /// <summary>No other agent in the person's order can take the session.</summary>
    public const string NoAgent = "no-agent";

    /// <summary>The session is not on this machine's record.</summary>
    public const string AskerGone = "asker-gone";

    /// <summary>
    /// The session is a helper, a reviewer, an intake or Ask Daoris's own conversation (the files may be old). Ask Daoris's goes on with
    /// another agent only where its turn met a limit with no account of its agent left (LIMITGOON2, D205 §3.5(e)), never on an ask.
    /// </summary>
    public const string AskerUnfit = "asker-unfit";

    /// <summary>The hand-over could not be taken for a reason none of the codes below names.</summary>
    public const string Cannot = "cannot";

    // HANDOVERSAID1: the take's own refusals, each said in its words rather than as `cannot`. They are the codes of the take's
    // refusals as well, which <c>HandOver</c> takes from here, since the note's vocabulary is the lower of the two.

    /// <summary>The session's quest is closed: no work is left to hand over.</summary>
    public const string QuestClosed = "quest-closed";

    /// <summary>The session's quest is no longer this machine's to carry on.</summary>
    public const string QuestElsewhere = "quest-elsewhere";

    /// <summary>The session's quest lost its take to the person's reopen of the work it grew from.</summary>
    public const string QuestReopened = "quest-reopened";

    /// <summary>A later session here carries the quest on.</summary>
    public const string NotLastRun = "not-last-run";

    /// <summary>The agent it was to go to is not one this machine has.</summary>
    public const string UnknownAgent = "unknown-agent";

    /// <summary>It already runs on that agent.</summary>
    public const string SameAgent = "same-agent";

    /// <summary>That agent's door cannot work in the session's repository.</summary>
    public const string NoDoor = "no-door";

    /// <summary>Another Daoris process on this machine runs it.</summary>
    public const string Running = "running";

    /// <summary>The tree it ran in is gone.</summary>
    public const string TreeGone = "tree-gone";

    /// <summary>Another hand-over of it waits already.</summary>
    public const string Waiting = "waiting";

    public static readonly IReadOnlyList<string> All =
    [
        Off, Declined, JustHanded, NoAgent, AskerGone, AskerUnfit, Cannot,
        QuestClosed, QuestElsewhere, QuestReopened, NotLastRun, UnknownAgent, SameAgent, NoDoor, Running, TreeGone, Waiting,
    ];

    /// <summary>A take's refusal as a code of these: its own where it is one, else <see cref="Cannot"/>.</summary>
    public static string Of(string? code) => code is not null && All.Contains(code) ? code : Cannot;

    /// <summary>
    /// Whether a hand-over not taken for this reason leaves nothing to go on with (HANDOVERSAID1): its quest closed or carried elsewhere,
    /// or its tree gone. Otherwise the session goes on with its own agent.
    /// </summary>
    public static bool Ends(string why) => why is QuestClosed or QuestElsewhere or QuestReopened or NotLastRun or TreeGone;

    /// <summary>The reason in one English sentence's clause, for the terminal and the transcript.</summary>
    public static string Said(string why) => why switch
    {
        Off => "the rule for hand-overs is off for this repository",
        Declined => "the person said no to it",
        JustHanded => "this session was itself handed over and has not had a turn of its own since",
        NoAgent => "no other agent in the person's order can take it",
        AskerGone => "the session is not on this machine",
        AskerUnfit => "the session is a helper, a reviewer, an intake or Ask Daoris's own conversation",
        Cannot => "the hand-over could not be taken",
        QuestClosed => "its quest is closed, so no work is left to hand over",
        QuestElsewhere => "its quest is no longer this machine's to carry on",
        QuestReopened => "its quest lost its take when the work it grew from was reopened",
        NotLastRun => "a later session here carries its quest on",
        UnknownAgent => "the agent it was to go to is not available on this machine",
        SameAgent => "it already runs on that agent",
        NoDoor => "that agent cannot work in this repository",
        Running => "it runs in another Daoris process on this machine",
        TreeGone => "the tree it ran in is gone",
        Waiting => "another hand-over of it already waits",
        _ => why,
    };
}
