namespace Daoris.Knowledge;

/// <summary>What happened to a quest — one per verb that moved it (D68 §2).</summary>
public enum QuestOperationKind
{
    /// <summary>Asked. Carries the quest's words and everything it carries; every history starts here.</summary>
    Published,

    /// <summary>A repository's agent accepted it.</summary>
    Taken,

    /// <summary>Finished, with an optional note.</summary>
    Done,

    /// <summary>Turned down, with the reason.</summary>
    Declined,

    /// <summary>
    /// A move another machine's reached the remote before (D68 §5): what was attempted, kept rather
    /// than dropped, and shown on the quest until a person acts. It moves no status.
    /// </summary>
    Conflict,

    /// <summary>
    /// A person dismissed one conflict (SYNC6c): it stops being shown, on every machine, because the
    /// dismissal travels like any other operation. It names the conflict and moves no status.
    /// </summary>
    Dismissed,

    /// <summary>
    /// Its taker asked another repository and waits on the answer (D79). Its note names the quest it
    /// waits on. It moves no status: the quest stays taken, because its work in progress is in the
    /// taker's tree, and nothing moves back to open.
    /// </summary>
    Waited,

    /// <summary>
    /// A person deleted a quest nobody had started on (D95). It applies only to an open quest and
    /// replays to no quest, so the quest is gone from every machine the operation reaches, and the
    /// remote keeping it is what stops a later fetch bringing the quest back. It carries nothing else.
    /// </summary>
    Deleted,

    /// <summary>
    /// The person accepted a done's departure from what they required (DRIFT1d, D133 §4): what it held, a chain's
    /// next step or a quest waiting on it, goes on. It applies only to a quest its departure holds, so two yeses are
    /// one, and it moves no status.
    /// </summary>
    Accepted,

    /// <summary>
    /// Daoris read a done's evidence (EVID1a, D144 point 3): the commit read, and what each item it waits on was found
    /// to be. It applies only to a done that waits on its evidence and reads exactly that, so a second machine's
    /// verdict on evidence already found is no move; found, it lets go what the done held, and it moves no status.
    /// </summary>
    Evidenced,

    /// <summary>
    /// A set-up step said where it showed the work for the person's review (REVIEWENV1b, D154 point 9): what to look at,
    /// how to show it again, and the commit its tree held, which the driver read. It applies only to a set-up step, and a
    /// second post of the same set-up is no move. It moves no status; a newer one holds the step for the person again.
    /// </summary>
    SetUp,

    /// <summary>
    /// The person's verdict on a set-up step (REVIEWENV1b, D154 point 8): <c>reviewed</c> or <c>not-yet</c> on its newest
    /// set-up, or <c>skipped</c> for the work. A <c>reviewed</c> or a skip lets go what the step's review held, and nothing
    /// else; it moves no status.
    /// </summary>
    Verdict,

    /// <summary>
    /// Its taker's driver bound it to a graph that closes its quest itself (FABRIC-CLOSE1a, the fabric events design §E5):
    /// its note is the graph's digest. It applies once, to a taken quest with no submission yet, so a quest follows a closure
    /// graph only from its first start, and quests already in flight keep today's done. From then on its taker's agent says
    /// <c>ready</c>, never <c>done</c>, and the graph's close door closes it. It moves no status.
    /// </summary>
    Bound,

    /// <summary>
    /// Its taker's agent submitted its work in a closure graph (FABRIC-CLOSE1a, §E5): the <c>ready</c> a done would have been,
    /// carrying a done's note and answers. The quest stays taken: a submission is not a status. The holds a done carries hang
    /// on the latest submission, and a later one replaces it, judged again from the start. It moves no status.
    /// </summary>
    Submitted,

    /// <summary>
    /// The person reopened a sealed submission (FABRIC-CLOSE1c, §E5): it names the submission by its machine and sequence. On the
    /// quest that submitted it, it applies while that submission is current, and the quest stays taken with none, so a close naming
    /// it is stale and a later <c>ready</c> is judged anew. On the step that submission published, it applies while the step is open
    /// or taken: an open step is withdrawn, never taken while so, and a taken one is held with its reason until the person chooses.
    /// It moves no status.
    /// </summary>
    Reopened,

    /// <summary>
    /// A step withdrawn or held by its source's reopen grows from the source's submission it names now (FABRIC-CLOSE1c, §E5): an
    /// untaken one as that submission publishes it, a started one on the person's word to go on. Its hold is let go, and the step
    /// keeps its id and its history. It moves no status.
    /// </summary>
    Rebound,

    /// <summary>
    /// The person chose the quest's kind and workflow on its page before it started (WORKFLOW1e2, the workflow design §4.1 row 1,
    /// §4.3): a quest published with no ask has no ask's choice, so its own is the task's. It applies only while the quest is open,
    /// since a run is bound at its first start (§4.5); the latest stands, and one naming neither clears it. It moves no status.
    /// </summary>
    Chosen,
}
