namespace Daoris.Driver;

/// <summary>A <c>line</c>'s reading: the unit's end, as the end judgement's keeper holds it.</summary>
internal static class LineEndReading
{
    public static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.OfBranch(reading) is { } refused) return refused;
        var unit = reading.Unit;
        var branch = unit.Records.Branch;
        if (unit.End.State == WorkEndState.Unknown) return NodeObserving.Seen(reading, NodeStates.Unknown, unit.End.Said);
        if (unit.End.State != WorkEndState.Ended) return NodeObserving.Seen(reading, null, unit.End.Said);
        var holder = unit.Records.Landings.FirstOrDefault(each => each.Held is not null)
            ?? (branch?.Held is null ? unit.Records.Landings.FirstOrDefault(each => each.RemovedAs is not null) : null);
        return holder is not null
            ? NodeObserving.Reached(reading, NodeSource.Git, unit.End.Revision, LandedBranches.FileName, holder.Branch, unit.End.Said)
            : NodeObserving.Reached(reading, NodeSource.Git, unit.End.Revision, SessionBranches.FileName, branch?.Branch ?? unit.Subject.Artifact ?? unit.Id, unit.End.Said);
    }
}
