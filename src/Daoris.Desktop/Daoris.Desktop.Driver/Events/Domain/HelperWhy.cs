namespace Daoris.Driver;

/// <summary>
/// Why no helper answered a question, by code: what the state file's <c>code</c> says and the note's <c>why</c>
/// carries. The page words each as <c>note.helper.why.*</c>; <see cref="Said"/> is the terminal's one English.
/// </summary>
public static class HelperWhy
{
    /// <summary>The person's rule for helpers is off for the asker's repository.</summary>
    public const string Off = "off";

    /// <summary>The asking session is not on this machine's record.</summary>
    public const string AskerGone = "asker-gone";

    /// <summary>The asking session is a helper, a reviewer, an intake or Ask Daoris (the files may be old).</summary>
    public const string AskerUnfit = "asker-unfit";

    /// <summary>The asker already has two helpers at work.</summary>
    public const string TooMany = "too-many";

    /// <summary>The asker's work cannot be copied for the helper to read.</summary>
    public const string Unreadable = "unreadable";

    /// <summary>The helper cannot be started.</summary>
    public const string NotStarted = "not-started";

    /// <summary>The helper ended with no answer and no last words.</summary>
    public const string NoAnswer = "no-answer";

    /// <summary>
    /// The asking session is a conversation. Nothing writes it, since a conversation takes the answer as its next turn,
    /// but a state file may hold it and the page's catalogues word it, so it stays defined and read.
    /// </summary>
    public const string Conversation = "conversation";

    /// <summary>The asking session cannot take the answer: a teammate's, stood down, or gone.</summary>
    public const string CannotTake = "cannot-take";

    /// <summary>
    /// The person said no to an agent's ask that waited on their word under the rule's <c>ask</c>: written
    /// by the bridge's <c>SESSION_HELPER_DECLINE</c> with this code's note, and read by the look as past judging.
    /// </summary>
    public const string Declined = "declined";

    /// <summary>
    /// The answer's hand began and its outcome is never written (a restart between the two), and nothing on the asker's line shows it
    /// taken: it is never handed again by itself, and stays on its ask for the person.
    /// </summary>
    public const string HandUncertain = "hand-uncertain";

    /// <summary>
    /// An account's limit ended the helper's turn with no answer, and the session that asked no longer waits on it:
    /// it is not run again, since an answer after the reset lands in a turn that no longer wants it.
    /// </summary>
    public const string Limited = "limited";

    /// <summary>Its provider kept failing past the retry's tries or the helper's minutes: not <i>no answer</i>.</summary>
    public const string Transient = "transient";

    /// <summary>
    /// Under the default rule, the agent the asker named is not one Daoris has set up and
    /// signed in on this machine, and only those are asked by default.
    /// </summary>
    public const string NotSetUp = "not-set-up";

    /// <summary>Under the default rule, no agent is set up and signed in for Daoris on this machine.</summary>
    public const string NoneSetUp = "none-set-up";

    // The asker's work in progress cannot be captured whole, so no helper is handed part of it.

    /// <summary>The asker's tree holds an unresolved merge.</summary>
    public const string CaptureMerge = "capture-merge";

    /// <summary>A link in the asker's work leads out of its tree.</summary>
    public const string CaptureLink = "capture-link";

    /// <summary>The asker's work changes a submodule, or holds a repository of its own.</summary>
    public const string CaptureSubmodule = "capture-submodule";

    /// <summary>A file of the asker's work cannot be read.</summary>
    public const string CaptureUnreadable = "capture-unreadable";

    /// <summary>The asker's work is past the capture's bounds (<see cref="HelperCaptures"/>).</summary>
    public const string CaptureBound = "capture-bound";

    /// <summary>The asker's work changes while it is captured, and again when it is captured once more.</summary>
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
        AskerUnfit => "the session that asked is itself a helper, a reviewer, an intake or Ask Daoris",
        TooMany => "that session already has two helpers at work",
        Unreadable => "its work could not be copied for a helper to read",
        NotStarted => "the helper could not be started",
        NoAnswer => "the helper ended without an answer",
        Conversation => "that session is a conversation, which takes words only from the person",
        CannotTake => "that session cannot take the answer",
        HandUncertain => "its answer may or may not have reached the session, so it is not handed again; the helper's own conversation holds it",
        Limited => "its account hit its limit before it answered, and the session no longer waits on it",
        Transient => "its provider kept failing, through every try or until its minutes ran out",
        NotSetUp => "it is not an agent Daoris has set up and signed in on this machine, and only those are asked by default",
        NoneSetUp => "no agent is set up and signed in for Daoris on this machine, and only those are asked by default",
        _ => why,
    };
}
