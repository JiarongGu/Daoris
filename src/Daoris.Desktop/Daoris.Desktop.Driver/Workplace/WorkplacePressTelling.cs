namespace Daoris.Driver;

/// <summary>
/// The note a terminal's press leaves for the loops watching the home (STATEHUB1e): Course keeps the note's store and folder, so the
/// workplace tells through this required operation, which Course installs when the driver loads.
/// </summary>
internal interface IWorkplacePressTelling
{
    /// <summary>Tell the press once it has acted: best effort, false where the note could not be written or names no route.</summary>
    bool Tell(string home, string route, string? session, DateTimeOffset at);
}

internal static class WorkplacePressTelling
{
    private static IWorkplacePressTelling? _current;

    internal static IWorkplacePressTelling Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace press telling is installed: Course installs it when the driver loads");
        set => _current = value;
    }
}
