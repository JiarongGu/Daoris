namespace Daoris.Knowledge;

/// <summary>Why a response did not move a quest — or <see cref="None"/> when it did.</summary>
public enum QuestRespondRefusal
{
    None,

    /// <summary>Not take, done, decline or wait — or <c>whileOpen</c> on anything but a decline.</summary>
    UnknownAction,

    /// <summary>Declining needs a reason: it is the part the asker can act on.</summary>
    MissingReason,

    /// <summary>No quest under that id.</summary>
    NotFound,

    /// <summary>
    /// Somebody got there first — the losing side of the race stands down. Also a done, a decline or a wait this
    /// machine makes on a quest whose take it lost: made on a claim it never held.
    /// </summary>
    AlreadyTaken,

    /// <summary>Done and Declined are terminal: one title is one quest forever.</summary>
    Closed,

    /// <summary>A wait that waits on nothing: no question named, an unknown or answered one, itself, or a quest nobody has taken.</summary>
    CannotWait,

    /// <summary>A done that leaves one of the quest's requirements unanswered — the answer names each.</summary>
    Unanswered,

    /// <summary>
    /// An answer that is not one: it names no requirement the quest carries, or one twice; it says both met
    /// and departed, or neither; a departure lacks its reason or the person's words; or it comes with no done to answer.
    /// </summary>
    BadAnswer,

    /// <summary>A departure quotes words the person never said — the answer names them.</summary>
    NotQuoted,

    /// <summary>A yes to a quest nothing holds: not closed done held for the person, or already accepted.</summary>
    NotHeld,

    /// <summary>
    /// A verdict on a quest whose done waits on no evidence: not closed done, none of its met requirements names
    /// any, its evidence is already found, or the person accepted the done as it stands.
    /// </summary>
    NotAwaitingEvidence,

    /// <summary>
    /// A verdict that is not one: not one in shape, by the judge the wire calls too
    /// (<see cref="QuestEvidenceVerdict.JudgeShape"/>) — no full commit id, a way of reading or a result nobody wrote, a
    /// spelling off a <c>case</c> read — or an item the quest does not wait on, one read twice, or an item it waits on
    /// left unread. The answer names which.
    /// </summary>
    BadVerdict,

    /// <summary>
    /// A set-up step's done before it said what it showed: a done always has a set-up, so the
    /// person's review has something to look at.
    /// </summary>
    NotShown,

    /// <summary>A set-up or a review's verdict on a quest that is no set-up step: only a set-up step is shown.</summary>
    NotSetUpStep,

    /// <summary>
    /// A set-up that is not one in shape (<see cref="Reviews.JudgeSetUp"/>), or a set-up step's environment that
    /// is not one: no full commit, no kind, no address to look at, words past a bound. The answer names which.
    /// </summary>
    BadSetUp,

    /// <summary>
    /// A review's verdict that is not one in shape: not <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>, a
    /// <c>not-yet</c> without the person's words, or words past their bound.
    /// </summary>
    BadReviewVerdict,

    /// <summary>
    /// A verdict, or a set-up step, the review's state refuses: nothing shown yet, a newer set-up than the one
    /// named, one already reviewed or skipped; a set-up step after work that is not done, or after one already composed.
    /// </summary>
    ReviewRefused,

    /// <summary>
    /// An agent's done on a quest a closure graph holds: it says <c>ready</c>, and the graph's close
    /// writes the Done at the work's end.
    /// </summary>
    ClosedByGraph,

    /// <summary>
    /// A <c>ready</c> where no closure graph holds the quest: not taken, or never bound by its taker's
    /// driver. A bind on a quest that is not taken, already submitted, or bound to another graph.
    /// </summary>
    NotInGraph,

    /// <summary>
    /// A <c>ready</c> on a submission that already published its chain's next step: sealed while that
    /// step stands, and reopened only by the person.
    /// </summary>
    Sealed,

    /// <summary>
    /// A take, done or <c>ready</c> on a chain's step whose source submission the person reopened: withdrawn
    /// while open, held while taken, until the person chooses to go on with it or to abandon it.
    /// </summary>
    SourceReopened,

    /// <summary>
    /// The person's reopen of a submission that is not the quest's current sealed one, or whose step has closed; or their word to
    /// go on with a step no reopen holds, or whose source has no unheld submission yet. The answer says which.
    /// </summary>
    NotSealed,

    /// <summary>
    /// The person's bring-forward of a conflict that is not this machine's lost take still standing: another
    /// machine's or none, settled already here or elsewhere, a dismissal of it that reached the remote first, or a step this machine holds
    /// again by a take of its own. The answer says which.
    /// </summary>
    NoLostTake,

    /// <summary>
    /// The person's bring-forward while a session here still runs on the quest: it waits until that process
    /// has ended, so the old session's late moves cannot stand on the fresh take. The answer names the session.
    /// </summary>
    StillRunning,

    /// <summary>
    /// A kind or a workflow on a quest that is not one in shape (<see cref="WorkflowChoices.Judge"/>), or words past their
    /// bound. The answer names which.
    /// </summary>
    BadWorkflow,

    /// <summary>
    /// A kind or a workflow on a quest an ask published: the ask's choice is the task's,
    /// chosen on the ask. The answer names it.
    /// </summary>
    AskChooses,

    /// <summary>
    /// A session's done, <c>ready</c> or decline through the connector whose note only points elsewhere
    /// (<see cref="HandbackNotes"/>): the note is the handback itself, and the answer quotes the pointer and says what one holds.
    /// </summary>
    PointsElsewhere,
}
