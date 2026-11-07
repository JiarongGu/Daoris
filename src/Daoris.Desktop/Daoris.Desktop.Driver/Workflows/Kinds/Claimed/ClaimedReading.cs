namespace Daoris.Driver;

/// <summary>
/// A <c>claimed</c>'s reading: the quest's done, by the ledger. On a run's subject (a binding naming its subject, which only a run's record does) it
/// waits for the chain's last step here: a done whose steps name a work step still to come in its
/// repository is pending, as the close and the opinion's gate read a chain, since a reach there would lapse at the next quest and run again
/// all that rests on it. A look's set-up step is the review's, never a step of the work.
/// </summary>
internal static class ClaimedReading
{
    public static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.Unbound(reading, WorkUnitKind.Quest) is { } refused) return refused;
        var unit = reading.Unit;
        var onRun = reading.Binding.Subject is not null;
        return unit.Records.Quest?.Status switch
        {
            "Done" when onRun && unit.Records.Quest.Then.Any(step => step.SetUpIn is null
                && string.Equals(step.To, unit.Records.Quest.To, StringComparison.OrdinalIgnoreCase)) =>
                NodeObserving.Seen(reading, null, $"its done is in, and its chain has a step in `{unit.Records.Quest.To}` still to come: the run's work is claimed at its last"),
            "Done" => NodeObserving.Reached(reading, NodeSource.Ledger, unit.Subject.Revision, WorkRecord.Quests, unit.Records.Quest.Id,
                unit.Records.Quest.Held ? "its done is in, and held for the person's yes or its evidence" : "its done is in"),
            "Open" or "Taken" or "Declined" => NodeObserving.Seen(reading, null, unit.End.Said),
            _ => NodeObserving.Seen(reading, NodeStates.Unknown, unit.End.Said),
        };
    }
}
