namespace Daoris.Driver;

/// <summary>
/// The move records a home keeps, as the workplace's delete lets them go with their record (FOUNDATION1f-i; the foundation design §3.3: a move is
/// deleted with the session's record by a delete or a clear, as the session's files are). The Course keeps the move store, so the workplace
/// names only these operations, which the store implements directly.
/// </summary>
internal interface IWorkplaceMoveRecords
{
    /// <summary>Where the desktop store, which holds the move records, lies in this home.</summary>
    string StoreLocation { get; }

    /// <summary>Which of these records left the store holds a move of, read without writing or making anything; throws what the store's file throws where it does not read.</summary>
    IReadOnlyList<string> MovesOf(IReadOnlyCollection<string> sessions);

    /// <summary>The moves of these deleted records gone from the store, with their words, those that held one named; a store that will not take the delete throws.</summary>
    IReadOnlyList<string> ForgetMoves(IReadOnlyCollection<string> sessions);
}

/// <summary>The Course constructs the move store over a home; the workplace holds that construction for its delete.</summary>
internal interface IWorkplaceMoveRecordStores
{
    IWorkplaceMoveRecords Open(string home);
}

internal static class WorkplaceMoveRecords
{
    private static IWorkplaceMoveRecordStores? _current;

    internal static IWorkplaceMoveRecordStores Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace move-record store factory is installed: the Course installs it when the driver loads");
        set => _current = value;
    }
}
