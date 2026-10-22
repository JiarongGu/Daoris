namespace Daoris.Driver;

/// <summary>
/// The reservation of a piece in the common admission, in one step (COAGENT1b, D199 point 4; the co-agent design §2.3 step 4, §5): the
/// boxes read, the lead's allowance and the cap judged, and <see cref="PartnerStates.Approved"/> written, all under one lock of this
/// process's, so two looks, or a look and the person's press, never both take the last slot or the lead's second place. The approved
/// state is the reservation itself: every starter counts it through <see cref="Snapshot.SlotsHeld"/> until the partner's record opens,
/// and a failure or an end that moves the piece out of <see cref="PartnerStates.Reserves"/> releases it.
/// </summary>
public static class PartnerReservations
{
    private static readonly object Gate = new();

    /// <summary>Reserve <paramref name="id"/>'s slot and place, or say why not; <see cref="PieceAdmitted.Reserved"/> wrote its state.</summary>
    /// <param name="active">The active records as this look read them: what holds a slot beside the reservations.</param>
    /// <param name="fromOf">Each record's <c>from</c> by id, for the lead's chain (<see cref="PartnerAdmission.Chain"/>).</param>
    /// <param name="taken">The slots this look's other starters took since <paramref name="active"/> was read.</param>
    public static PieceAdmitted TryReserve(
        PartnerFiles files, string id, IReadOnlyList<SessionView> active, IReadOnlyDictionary<string, string?> fromOf, int cap, int taken,
        DateTimeOffset now)
    {
        lock (Gate)
        {
            var pieces = files.Reservations();
            if (pieces.FirstOrDefault(piece => piece.Id == id) is not { } piece) return PieceAdmitted.Already;
            var held = ActiveSessions.Held(active) + PartnerAdmission.Reserving(pieces, active);
            var admitted = PartnerAdmission.Admit(piece, pieces, PartnerAdmission.Chain(piece.Lead, fromOf), cap, held, taken);
            if (admitted == PieceAdmitted.Reserved) files.WriteState(new PartnerState(id, PartnerStates.Approved, null, now));
            return admitted;
        }
    }
}
