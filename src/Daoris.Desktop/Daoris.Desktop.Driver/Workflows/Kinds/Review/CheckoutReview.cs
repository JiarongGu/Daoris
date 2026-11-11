namespace Daoris.Driver;

/// <summary>
/// A git checkout's review record, worked in place: each of the run's turns as the interval from its start's <c>HEAD</c> to its end's, kept
/// by the run's turn log so a record's later turn leaves its earlier interval read, and the line's own uncommitted paths at its newest end
/// with their hashes. A commit inside an interval came while its turn ran; one between two turns is not this work's.
/// </summary>
internal static class CheckoutReview
{
    public static ReviewReference Reference(IReadOnlyList<RunTurn> turns) => new()
    {
        Workspace = turns[^1].Workspace,
        Department = turns[^1].Department,
        Turns = [.. turns.Select(turn => new ReviewInterval(turn.Record, turn.Started, turn.Ended, turn.StartHead, turn.EndHead))],
        Uncommitted = turns[^1].Own,
        End = turns[^1].Record,
        EndDigest = turns[^1].Digest,
    };
}
