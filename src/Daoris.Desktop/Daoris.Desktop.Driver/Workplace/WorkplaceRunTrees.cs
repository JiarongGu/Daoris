namespace Daoris.Driver;

/// <summary>What the workplace asks of the Workflows to end the trees of runs that are over: whether a run's tree is due, read from its record.</summary>
internal interface IWorkplaceRunTrees
{
    /// <summary>
    /// True where the run's record ended with no attempt unsettled, or none is kept at all; false where the run still stands or its
    /// record does not read, which ends nothing.
    /// </summary>
    bool Due(string home, string run);
}

internal static class WorkplaceRunTrees
{
    private static IWorkplaceRunTrees? _current;

    internal static IWorkplaceRunTrees Current
    {
        get => _current ?? throw new InvalidOperationException("no run-tree reader is installed: Workflows installs it when the driver loads");
        set => _current = value;
    }
}
