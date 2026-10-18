namespace Daoris.Driver;

/// <summary>
/// The result a landing and its gates judge (FABRIC-CLOSE1h, the fabric design §E5): a quest's done, or, in a closure graph, its taker's
/// current submission. Where a graph holds <c>quest.close</c> Daoris writes no Done before the graph's end, so what hangs on a done today
/// hangs there on the submission, unchanged otherwise: the holds the service computes on it (<see cref="QuestView.Held"/>,
/// <see cref="QuestView.Hold"/>), the landing at an unheld one, and the gates that read a chain's step as finished.
/// </summary>
/// <remarks>
/// A quest no closure graph holds has no submission, so it reads exactly as before: <see cref="Status"/> is its own status, and the
/// owner's install, which binds nothing, reads nothing more than it did.
/// </remarks>
public static class QuestResults
{
    /// <summary>Whether a quest a closure graph holds stands at its submission: taken, bound, with a current submission, held or not.</summary>
    public static bool Submitted(QuestView? quest) => false;

    /// <summary>Whether its result is in: closed done, or at a closure graph's submission. Held or not: the hold is read beside it.</summary>
    public static bool Judged(QuestView? quest) => quest is { Status: "Done" };

    /// <summary>
    /// The status the landing and its gates read: a submission reads as the done it stands for, every other quest as it stands. Null where
    /// there is no quest.
    /// </summary>
    public static string? Status(QuestView? quest) => quest?.Status;

    /// <summary>
    /// Whether two reads of one quest stand on the same result (FABRIC-CLOSE1h): the same submission, by its machine and sequence, or none
    /// on either. A later <c>ready</c> is another submission, judged again from the start (§E5), even at the same commit.
    /// </summary>
    public static bool Same(QuestView? judged, QuestView? now) => true;
}
