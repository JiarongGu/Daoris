namespace Daoris.Driver;

/// <summary>A <c>line</c>'s reading: the unit's end, as the end judgement's keeper holds it.</summary>
internal static class LineEndReading
{
    /// <summary>The line's holding of the whole work at the branch's tip this pass read, where git says it holds every commit; else null.</summary>
    private static LineHolding? OnTheLine(NodeReading reading) =>
        (reading.RunBranchTip ?? reading.Binding.SubjectRevision) is { } tip
        && reading.Git?.Content?.Invoke(reading.Unit.Id, tip)?.Line is { HoldsAll: true } held
            ? held : null;

    public static NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.OfBranch(reading) is { } refused) return refused;
        var unit = reading.Unit;
        var branch = unit.Records.Branch;
        if (unit.End.State != WorkEndState.Ended && OnTheLine(reading) is { } onLine)
        {
            return NodeObserving.ReachedAt(reading, NodeSource.Git, reading.Binding.SubjectRevision, onLine.Held.Onto, "git", $"refs/heads/{onLine.Name}", onLine.Said);
        }

        if (unit.End.State == WorkEndState.Unknown) return NodeObserving.Seen(reading, NodeStates.Unknown, unit.End.Said);
        if (unit.End.State != WorkEndState.Ended) return NodeObserving.Seen(reading, null, unit.End.Said);
        var holder = unit.Records.Landings.FirstOrDefault(each => each.Held is not null)
            ?? (branch?.Held is null ? unit.Records.Landings.FirstOrDefault(each => each.RemovedAs is not null) : null);
        return holder is not null
            ? NodeObserving.Reached(reading, NodeSource.Git, unit.End.Revision, LandedBranches.FileName, holder.Branch, unit.End.Said)
            : NodeObserving.Reached(reading, NodeSource.Git, unit.End.Revision, SessionBranches.FileName, branch?.Branch ?? unit.Subject.Artifact ?? unit.Id, unit.End.Said);
    }
}
