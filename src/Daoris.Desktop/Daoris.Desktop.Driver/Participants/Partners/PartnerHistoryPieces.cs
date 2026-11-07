using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The home's pieces as a clear of finished work reads them (D199 point 3): each reservation on the partners' files, settled once it is
/// taken in, discarded or refused, when nothing of it waits on anyone.
/// </summary>
internal sealed class PartnerHistoryPieces : IHistoryPieceReading
{
    [ModuleInitializer]
    internal static void Install() => HistoryPieceReadings.Current = new PartnerHistoryPieces();

    public IReadOnlyList<HistoryPiece> Pieces(string home) =>
        [.. new PartnerFiles(home).Reservations().Select(piece => new HistoryPiece(piece.Id, piece.Lead, piece.State, Settled(piece.State)))];

    private static bool Settled(string state) =>
        state is PartnerStates.TakenIn or PartnerStates.Discarded or PartnerStates.Refused;
}
