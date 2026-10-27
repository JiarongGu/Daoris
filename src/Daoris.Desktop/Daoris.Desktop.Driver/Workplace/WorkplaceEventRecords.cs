namespace Daoris.Driver;

/// <summary>Context constructs standalone records; supplied records retain their original instance.</summary>
internal interface IWorkplaceEventRecords
{
    IWorkplaceEventHistory Open(string directory);
}

internal static class WorkplaceEventRecords
{
    private static IWorkplaceEventRecords? _current;

    internal static IWorkplaceEventRecords Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace event record factory is installed: Context installs it when the driver loads");
        set => _current = value;
    }
}
