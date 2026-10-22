namespace Daoris.Driver;

/// <summary>
/// A piece's place in the common admission (COAGENT1b, D199 point 4; the co-agent design §2.3 step 4, §5): before its record opens, a
/// piece in <see cref="PartnerStates.Approved"/> or <see cref="PartnerStates.Preparing"/> holds a slot under the machine's cap, read by
/// every starter through <see cref="Snapshot.SlotsHeld"/>; once a record carrying its piece is active, that record holds it instead,
/// counted once. Its lead holds at most <see cref="OpenBound"/> open pieces across its <c>from</c> chain, counted from the boxes, never
/// from running records, so two looks cannot both pass the second.
/// </summary>
/// <param name="Id">The piece's id.</param>
/// <param name="Lead">The lead that asked for it.</param>
/// <param name="State">Where it stands (<see cref="PartnerStates"/>).</param>
public sealed record PieceReservation(string Id, string Lead, string State);

/// <summary>What the common admission answers a piece's look (the design §2.3 step 4).</summary>
public enum PieceAdmitted
{
    /// <summary>Reserved: its slot and its place in the lead's allowance, with <see cref="PartnerStates.Approved"/> written.</summary>
    Reserved,

    /// <summary>The machine's cap is full: the piece waits, said, and keeps its ask.</summary>
    CapFull,

    /// <summary>The lead already holds two open pieces across its <c>from</c> chain: refused <see cref="PartnerWhy.TooMany"/>.</summary>
    TooMany,

    /// <summary>The piece is past judging already: approved, started or ended, so nothing is reserved twice.</summary>
    Already,
}

public static class PartnerAdmission
{
    /// <summary>A lead's open pieces at most, across its <c>from</c> chain (the design §2.3 step 2; a first value, read again in COAGENT1f).</summary>
    public const int OpenBound = 2;

    /// <summary>
    /// The pieces whose reservation takes a slot now (the design §5): each <see cref="PartnerStates.Reserves"/> state but one an active
    /// record already carries as its <see cref="SessionView.Piece"/>, which <see cref="ActiveSessions.HoldsSlot"/> counts by its
    /// <c>serves</c> (the record opened before its state moved to <see cref="PartnerStates.Started"/>).
    /// </summary>
    public static int Reserving(IEnumerable<PieceReservation> pieces, IEnumerable<SessionView> active)
    {
        var opened = active.Select(session => session.Piece).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return pieces.Count(piece => PartnerStates.Reserves(piece.State) && !opened.Contains(piece.Id));
    }

    /// <summary>
    /// Every record of <paramref name="lead"/>'s <c>from</c> chain (the design §5: a hand-over does not reset its two): the lead, the
    /// records it continues, and those continuing any of them, from the records' <c>from</c> links by id.
    /// </summary>
    public static IReadOnlySet<string> Chain(string lead, IReadOnlyDictionary<string, string?> fromOf)
    {
        var chain = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { lead };
        for (var at = lead; fromOf.TryGetValue(at, out var from) && from is { Length: > 0 } && chain.Add(from);) at = from;
        // Those continuing any record of the chain, until no record joins: a chain is short, and a cycle stops at the set.
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var (id, from) in fromOf)
            {
                if (from is { Length: > 0 } && chain.Contains(from) && chain.Add(id)) grew = true;
            }
        }

        return chain;
    }

    /// <summary>How many pieces asked by any record of <paramref name="chain"/> are open (<see cref="PartnerStates.Open"/>), but <paramref name="except"/>.</summary>
    public static int OpenAcross(IEnumerable<PieceReservation> pieces, IReadOnlySet<string> chain, string? except = null) =>
        pieces.Count(piece => chain.Contains(piece.Lead) && PartnerStates.Open(piece.State)
                              && !string.Equals(piece.Id, except, StringComparison.Ordinal));

    /// <summary>
    /// The admission of one piece in one step (the design §2.3 step 4): a piece already past judging is never reserved again; its lead's
    /// allowance first, since a third piece is refused whatever the cap; then the cap, read as every starter reads it, with the slots
    /// this look already took.
    /// </summary>
    /// <param name="pieces">Every piece on file, as read inside the reservation's lock.</param>
    /// <param name="slotsHeld">The slots held now, the reservations included (<see cref="Snapshot.SlotsHeld"/>).</param>
    /// <param name="taken">The slots this look's other starters took since the snapshot.</param>
    public static PieceAdmitted Admit(
        PieceReservation piece, IReadOnlyList<PieceReservation> pieces, IReadOnlySet<string> chain, int cap, int slotsHeld, int taken)
    {
        if (piece.State is not (PartnerStates.Asked or PartnerStates.Waiting)) return PieceAdmitted.Already;
        if (OpenAcross(pieces, chain, except: piece.Id) >= OpenBound) return PieceAdmitted.TooMany;
        return cap - slotsHeld - taken > 0 ? PieceAdmitted.Reserved : PieceAdmitted.CapFull;
    }
}
