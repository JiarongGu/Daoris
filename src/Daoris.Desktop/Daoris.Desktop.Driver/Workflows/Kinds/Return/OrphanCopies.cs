namespace Daoris.Driver;

/// <summary>
/// The look's tidy of a folder department's copies, by their runs' records: a run is over for its copy once its record says it ended and
/// no <c>return</c> of it waits, on the folder or on the person (<see cref="CopyTidy"/>).
/// </summary>
public sealed class OrphanCopies(string home) : ICopyTidying
{
    /// <inheritdoc/>
    public IReadOnlyList<CopySetAside> Tidy(IReadOnlySet<string> inUse, DateTimeOffset now)
    {
        var records = new NodeRecordFiles(home);
        return CopyTidy.Tidy(home, inUse, now, run => records.Read(RunUnits.IdOf(run)) is { Ended: not null } record
            && !record.Statuses.Values.Any(status => status.State == NodeStates.Active && status.Code is ReturnNode.PlaceBusy or ReturnNode.BothChanged));
    }
}
