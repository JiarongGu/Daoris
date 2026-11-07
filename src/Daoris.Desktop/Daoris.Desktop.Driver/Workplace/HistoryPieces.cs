namespace Daoris.Driver;

/// <summary>
/// The pieces a clear of finished work stays for. No clear touches a piece's
/// ref or its box. A piece only the person's act settles waits on them, and the clear would take the lead's record that the person's
/// <i>Discard…</i> finds the piece's checkout by, so a unit whose sessions asked for one stays until it is settled.
/// </summary>
internal static class HistoryPieces
{
    /// <summary>
    /// The keep naming the lead of the first unsettled piece one of <paramref name="sessions"/> asked for; null where none is. A settled
    /// piece's ref and box stay as they stand.
    /// </summary>
    public static HistoryKeep? Keep(IReadOnlyList<string> sessions, IReadOnlyList<HistoryPiece> pieces)
    {
        foreach (var piece in pieces)
        {
            if (piece.Settled || !sessions.Contains(piece.Lead, StringComparer.Ordinal)) continue;
            return new HistoryKeep(
                HistoryWords.NeedsYou,
                $"Session `{piece.Lead}`'s piece `{piece.Id}` is still {piece.State} and waits on you; take it in, or discard it, first.")
            {
                Context = HistoryContexts.Piece, Session = piece.Lead,
            };
        }

        return null;
    }
}
