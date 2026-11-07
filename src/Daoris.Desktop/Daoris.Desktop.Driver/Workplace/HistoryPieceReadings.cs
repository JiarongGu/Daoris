namespace Daoris.Driver;

/// <summary>A piece on file as a clear reads it: its id, the session that leads it, where it stands, and whether nothing of it waits on anyone.</summary>
public sealed record HistoryPiece(string Id, string Lead, string State, bool Settled);

/// <summary>The home's pieces as a clear reads them, from the partners' files, which Participants keeps.</summary>
internal interface IHistoryPieceReading
{
    IReadOnlyList<HistoryPiece> Pieces(string home);
}

internal static class HistoryPieceReadings
{
    private static IHistoryPieceReading? _current;

    internal static IHistoryPieceReading Current
    {
        get => _current ?? throw new InvalidOperationException("no reading of the home's pieces is installed: Participants installs it when the driver loads");
        set => _current = value;
    }
}
