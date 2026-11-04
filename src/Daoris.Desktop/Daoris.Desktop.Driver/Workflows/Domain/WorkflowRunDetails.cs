namespace Daoris.Driver;

/// <summary>
/// Which row of a state's table a step stands at (WORKFLOW1c; design §5.2's <i>Said</i>), a code each, for the page's words. Read
/// with the step's kind: <see cref="Agent"/> on the work is its session, on the opinion its reviewer.
/// </summary>
public static class WorkflowRunDetails
{
    // The work.
    /// <summary>A quest of it is open, and no session works on it yet.</summary>
    public const string Queued = "queued";

    /// <summary>A session works on it, or a reviewer reads it.</summary>
    public const string Agent = "agent";

    /// <summary>The person answered a park, and the same session goes on at the driver's next look (ANSWER1c).</summary>
    public const string Answered = "answered";

    /// <summary>A session parked asking the person (D46 §3).</summary>
    public const string Park = "park";

    /// <summary>A go-ahead a session of the work asked, unanswered (D135).</summary>
    public const string GoAhead = "go-ahead";

    /// <summary>A done held for the person's yes: a departure or missing evidence (D133, D144); its code is the quest's hold.</summary>
    public const string Held = "held";

    /// <summary>Its taker waits on a question another repository answers (D79).</summary>
    public const string Awaits = "awaits";

    /// <summary>Its session finished and its quest stays taken: the person's done closes it (QUESTCLOSE1).</summary>
    public const string Unclosed = "unclosed";

    /// <summary>A chat run's conversation between turns with nothing beyond the line (CHATFLOW1c): it is the person's turn to talk.</summary>
    public const string Talk = "talk";

    /// <summary>A chat run's conversation between turns, or ended, with commits beyond the line, which its count names (CHATFLOW1c).</summary>
    public const string Land = "land";

    /// <summary>Its last session failed.</summary>
    public const string Failed = "failed";

    /// <summary>The person stopped its last session.</summary>
    public const string Stopped = "stopped";

    /// <summary>A quest of it was declined.</summary>
    public const string Declined = "declined";

    /// <summary>Every quest of it here is done, and none is held.</summary>
    public const string Finished = "finished";

    /// <summary>
    /// Every quest of it here is finished, and some by a submission (FABRIC-WORKFLOW2d, §E5): bound to a closure graph, its current
    /// submission unheld and sealed. The graph's close writes their Done at its end.
    /// </summary>
    public const string Submitted = "submitted";

    /// <summary>
    /// The person reopened the submission of a quest of it (FABRIC-CLOSE1c, FABRIC-WORKFLOW2d): its step stands withdrawn or held, and the
    /// quest, taken with no submission, waits for its agent's next <c>ready</c>.
    /// </summary>
    public const string Reopened = "reopened";

    /// <summary>
    /// A submission the person reopened is made ready again, and the started step it held waits on their go-on or abandon (FABRIC-CLOSE1c7,
    /// D173's WORKFLOW2d note, choice 2): its quest is that step's, whose door they choose at.
    /// </summary>
    public const string GoOn = "go-on";

    // The second opinion: its details are its gate's own states (OpinionGateStates, XAGENT1f), and two more.
    /// <summary>
    /// The work landed, and its landing record keeps no word of the opinion that let it go. On a chat run's work (CHATFLOW1c), a landing is
    /// kept for the tip its tree stands at.
    /// </summary>
    public const string Landed = "landed";

    /// <summary>
    /// A chat run's opinion before its press (CHATFLOW1d, the chat-flow design §4.2, §5.2): nothing has asked it, and the person's
    /// <i>Accept…</i> asks it, so it waits on that press; the step names the reviewer the press asks. It replaces CHATFLOW1c's
    /// <c>chat-not-asked</c>, which said a conversation's press landed its work with none.
    /// </summary>
    public const string ChatAtAccept = "chat-at-accept";

    // The look (the review's states, ReviewStates, where they stand at a step).
    public const string NotShown = "not-shown";
    public const string BeingSetUp = "being-set-up";
    public const string Shown = "shown";
    public const string NotYet = "not-yet";
    public const string NotHeld = "not-held";
    public const string Reviewed = "reviewed";

    /// <summary>The person skipped it for this work.</summary>
    public const string Skip = "skip";

    /// <summary>The chain's or the ask's choice says no review for this work: its code is the level, its words the choice's.</summary>
    public const string Off = "off";

    // The landing.
    /// <summary>It waits for the person's <i>Accept…</i>.</summary>
    public const string Accept = "accept";

    /// <summary>Accepted automatically at the driver's next look (D145).</summary>
    public const string Automatic = "automatic";

    /// <summary>Landed on a branch (D102): its branch and when.</summary>
    public const string Branch = "branch";

    /// <summary>Landed into its line, as the session's record keeps the acceptance (D100).</summary>
    public const string Merge = "merge";

    /// <summary>
    /// Brought into another session's landing by its combined Accept (LINESTOGETHER1d): its branch, or the line where the code says
    /// <c>line</c>; never an acceptance of its own.
    /// </summary>
    public const string BroughtInto = "brought-into";

    /// <summary>Its work reached the line or a branch of the person's another way (LAND2b's <c>already</c> and <c>carried</c>).</summary>
    public const string Already = "already";

    /// <summary>The work made no commits, so nothing lands.</summary>
    public const string Nothing = "nothing";

    /// <summary>The automatic landing's last try could not land it: its code is the try's.</summary>
    public const string Refused = "refused";

    /// <summary>Its tree was discarded before it landed.</summary>
    public const string Gone = "gone";

    /// <summary>The work ran on another machine, which keeps its landing (D47 §4).</summary>
    public const string Elsewhere = "elsewhere";

    // The pull request.
    /// <summary>Open, or not read since it opened: the person merges it on the platform.</summary>
    public const string Merging = "merge";

    /// <summary>The platform said it completed (D148).</summary>
    public const string Merged = "merged";

    /// <summary>The platform said it was abandoned.</summary>
    public const string Abandoned = "abandoned";

    /// <summary>The plugin did not push the branch, so no pull request opened: the hand-off is the door (WSR5b).</summary>
    public const string NotPushed = "not-pushed";

    /// <summary>The last ask of its plugin failed: its code is the failure's (PLUGHOOK1a).</summary>
    public const string AskFailed = "ask-failed";

    /// <summary>The work landed into its line, so no branch was pushed and no pull request opened.</summary>
    public const string NoBranch = "no-branch";

    /// <summary>A store this step reads did not answer: its words say why.</summary>
    public const string Unread = "unread";
}
