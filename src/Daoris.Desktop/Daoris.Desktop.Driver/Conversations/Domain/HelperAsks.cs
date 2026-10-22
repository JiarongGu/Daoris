namespace Daoris.Driver;

/// <summary>Where one ask for a helper stands, one of <see cref="HelperStates.All"/> (MIXAGENT2a-i's shapes, D163 point 2).</summary>
public static class HelperStates
{
    public const string Waiting = "waiting";
    public const string Approved = "approved";
    public const string Started = "started";
    public const string Answered = "answered";
    public const string Refused = "refused";

    public static readonly IReadOnlyList<string> All = [Waiting, Approved, Started, Answered, Refused];
}

/// <summary>
/// Why no helper answered a question, by code (MIXAGENT2a-ii): what the state file's <c>code</c> says and the note's <c>why</c>
/// carries. The page words each as <c>note.helper.why.*</c>; <see cref="Said"/> is the terminal's one English.
/// </summary>
public static class HelperWhy
{
    /// <summary>The person's rule for helpers is off for the asker's repository.</summary>
    public const string Off = "off";

    /// <summary>The asking session is not on this machine's record.</summary>
    public const string AskerGone = "asker-gone";

    /// <summary>The asking session is a helper, a reviewer or an intake (the files may be old).</summary>
    public const string AskerUnfit = "asker-unfit";

    /// <summary>The asker already has two helpers at work.</summary>
    public const string TooMany = "too-many";

    /// <summary>The asker's work could not be copied for the helper to read.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>The helper could not be started.</summary>
    public const string NotStarted = "not-started";

    /// <summary>The helper ended with no answer and no last words.</summary>
    public const string NoAnswer = "no-answer";

    /// <summary>
    /// The asking session is a conversation. No longer written (MIXAGENT2a-iii: a conversation takes the answer as its next turn),
    /// but a state file from before may hold it and the page's catalogues word it, so it stays defined and read.
    /// </summary>
    public const string Conversation = "conversation";

    /// <summary>The asking session cannot take the answer: a teammate's, stood down, or gone.</summary>
    public const string CannotTake = "cannot-take";

    /// <summary>
    /// The person said no to an agent's ask that waited on their word under the rule's <c>ask</c> (HELPERNO1, D163 point 3): written
    /// by the bridge's <c>SESSION_HELPER_DECLINE</c> with this code's note, and read by the look as past judging.
    /// </summary>
    public const string Declined = "declined";

    /// <summary>
    /// The answer's hand began and its outcome was never written (a restart between the two), and nothing on the asker's line shows it
    /// taken (HELPERWAIT1; Codex's round-2 #2 on COWORKUX1): it is never handed again by itself, and stays on its ask for the person.
    /// </summary>
    public const string HandUncertain = "hand-uncertain";

    // COAGENT1a (D199 point 5; the co-agent design §2.5): the asker's work in progress could not be captured whole, so no helper
    // was handed part of it.

    /// <summary>The asker's tree holds an unresolved merge.</summary>
    public const string CaptureMerge = "capture-merge";

    /// <summary>A link in the asker's work leads out of its tree.</summary>
    public const string CaptureLink = "capture-link";

    /// <summary>The asker's work changes a submodule, or holds a repository of its own.</summary>
    public const string CaptureSubmodule = "capture-submodule";

    /// <summary>A file of the asker's work could not be read.</summary>
    public const string CaptureUnreadable = "capture-unreadable";

    /// <summary>The asker's work is past the capture's bounds (<see cref="HelperCaptures"/>).</summary>
    public const string CaptureBound = "capture-bound";

    /// <summary>The asker's work changed while it was captured, and again when it was captured once more.</summary>
    public const string CaptureMoved = "capture-moved";

    /// <summary>The reason in one English sentence's clause, for the terminal and the transcript.</summary>
    public static string Said(string why) => why switch
    {
        Declined => "the person said no to it",
        CaptureMerge => "its work holds an unresolved merge, so it could not be captured whole",
        CaptureLink => "a link in its work leads out of its tree, so it could not be captured whole",
        CaptureSubmodule => "its work changes a submodule or holds a repository of its own, so it could not be captured whole",
        CaptureUnreadable => "a file of its work could not be read, so it could not be captured whole",
        CaptureBound => "its uncommitted work is past what a helper is handed",
        CaptureMoved => "its work kept changing while it was captured",
        Off => "the rule for helpers is off for this repository",
        AskerGone => "the session that asked is not on this machine",
        AskerUnfit => "the session that asked is itself a helper, a reviewer or an intake",
        TooMany => "that session already has two helpers at work",
        Unreadable => "its work could not be copied for a helper to read",
        NotStarted => "the helper could not be started",
        NoAnswer => "the helper ended without an answer",
        Conversation => "that session is a conversation, which takes words only from the person",
        CannotTake => "that session cannot take the answer",
        HandUncertain => "its answer may or may not have reached the session, so it is not handed again; the helper's own conversation holds it",
        _ => why,
    };
}

/// <summary>One ask for a helper as <c>&lt;askId&gt;.ask.json</c> keeps it: written by the connector's <c>helper_ask</c> or the person's door, never changed.</summary>
/// <param name="Agent">The agent the asker or the person named, one word; null where none was.</param>
/// <param name="AskedBy"><c>agent</c> or <c>person</c>.</param>
public sealed record HelperAsk(string Id, string Asker, string Question, string? Agent, string AskedBy, DateTimeOffset At)
{
    public const string ByAgent = "agent";
    public const string ByPerson = "person";

    /// <summary>
    /// The asker asked for the answer at its next step (COWORK1d, D188 point 2; the message-urgency design §9): where its door takes
    /// words then, while it works. False for an ask with no level, every ask from before among them: today's delivery.
    /// </summary>
    public bool Urgent { get; init; }
}

/// <summary>
/// One ask's state as the driver keeps it, <c>&lt;askId&gt;.state.json</c> (the driver alone writes it; the service reads it as
/// <c>HelperState</c> and finds an ask by the one <c>started</c> naming its helper): the shape MIXAGENT2a-i settled, with three
/// driver-only marks it ignores: <see cref="LastWords"/>, <see cref="Handed"/> and <see cref="Agent"/>.
/// </summary>
/// <param name="Code">A refusal's code (<see cref="HelperWhy"/>), or why a waiting ask waits; null otherwise.</param>
/// <param name="Helper">The helper's session once started.</param>
public sealed record HelperState(string Id, string State, string? Code, string? Helper, DateTimeOffset At)
{
    /// <summary>The answer is the helper's last words, bounded, since it never used its tool.</summary>
    public bool LastWords { get; init; }

    /// <summary>The word id the answer went to the asker as, once handed.</summary>
    public string? Handed { get; init; }

    /// <summary>
    /// When the driver began handing the answer (HELPERWAIT1; Codex's round-2 #2 on COWORKUX1): written before the hand door is asked and
    /// cleared where it refused, so one still on file with nothing <see cref="Handed"/> is an attempt whose outcome nobody wrote, which is
    /// never made again without evidence of where it went.
    /// </summary>
    public DateTimeOffset? Attempted { get; init; }

    /// <summary>The agent that was started, for the notes.</summary>
    public string? Agent { get; init; }

    /// <summary>The helper's last words, kept here for the delivery where it never used its tool (<see cref="LastWords"/>); no answer file holds them.</summary>
    public string? Words { get; init; }
}

/// <summary>A helper's answer as <c>&lt;askId&gt;.answer.json</c> keeps it, written by <c>helper_answer</c> once.</summary>
public sealed record HelperAnswer(string Id, string Helper, string Answer, DateTimeOffset At);
