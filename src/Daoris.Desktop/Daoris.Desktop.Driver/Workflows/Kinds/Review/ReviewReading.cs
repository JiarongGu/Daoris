namespace Daoris.Driver;

/// <summary>
/// A review's reading. Bound to the run itself, as work with no branch is, nothing it reads reaches it: only an Accept does. Bound to a
/// branch, the landing record reaches it where its landing is seen made or the line holds its commits, since nothing is left to decide. A
/// branch that committed nothing has nothing to accept: its Accept follows the risk policy like every review's.
/// </summary>
internal static class ReviewReading
{
    public static NodeObservation Observe(NodeReading reading) =>
        Read(reading) is var read && read is { Outcome: null, Refused: null } ? RiskStepGates.Passed(reading, PersonWaits.Accept) ?? read : read;

    private static NodeObservation Read(NodeReading reading)
    {
        if (reading.Unit.Kind == WorkUnitKind.Run)
        {
            return NodeObserving.NotBound(reading)
                ?? NodeObserving.Seen(reading, null, $"waits for the review of its record at {TextBounds.ShortCommit(reading.Binding.SubjectRevision ?? "", 8)}");
        }

        if (reading.Unit.Records.Branch is { Held.By: EndBy.Empty } empty && NodeObserving.LandingOf(reading.Unit) is null)
        {
            return NodeObserving.OfBranch(reading)
                ?? NodeObserving.Seen(reading, null, $"`{empty.Branch}` committed nothing past where it grew, so there is nothing to accept: the risk policy decides its review");
        }

        return LandReading.Observe(reading, NodeSource.Git);
    }
}
