namespace Daoris.Driver;

/// <summary>
/// The ledger's memory of this process: the reservations taken and the agents marked not answering, each with an end. Its members are
/// the only writers of either, called under the ledger's lock, and each write that changes them moves <see cref="Revision"/>, which the
/// limit wait's stamp carries, so the look after it runs. An expiry moves nothing: the ledger's next change makes the look due by then.
/// </summary>
internal sealed class StaffHolds
{
    private readonly List<StaffReservation> _reserved = [];
    private readonly Dictionary<string, DateTimeOffset> _marks = new(StringComparer.OrdinalIgnoreCase);
    private long _revision;

    /// <summary>How many writes changed the holds: read without the ledger's lock.</summary>
    internal long Revision => Interlocked.Read(ref _revision);

    /// <summary>Take <paramref name="reservation"/>.</summary>
    internal void Reserve(StaffReservation reservation)
    {
        _reserved.Add(reservation);
        Moved();
    }

    /// <summary>Give <paramref name="reservation"/> back; nothing moves where it is not held.</summary>
    internal void Release(StaffReservation reservation)
    {
        if (_reserved.Remove(reservation)) Moved();
    }

    /// <summary>Move <paramref name="reservation"/> to the entry the settled selection took.</summary>
    internal void Settle(StaffReservation reservation, StaffKey choice)
    {
        reservation.Choice = choice;
        Moved();
    }

    /// <summary>Mark <paramref name="agent"/> not answering until <paramref name="until"/>.</summary>
    internal void Mark(string agent, DateTimeOffset until)
    {
        _marks[agent] = until;
        Moved();
    }

    /// <summary>End <paramref name="agent"/>'s mark; nothing moves where it had none.</summary>
    internal void Clear(string agent)
    {
        if (_marks.Remove(agent)) Moved();
    }

    /// <summary><paramref name="role"/>'s reservations standing at <paramref name="now"/>, by choice; those past their life are dropped.</summary>
    internal Dictionary<StaffKey, int> Reserved(string role, DateTimeOffset now)
    {
        _reserved.RemoveAll(each => now - each.At >= StaffLedger.ReservationLife);
        var reserved = new Dictionary<StaffKey, int>();
        foreach (var each in _reserved.Where(each => each.Role == role)) reserved[each.Choice] = reserved.GetValueOrDefault(each.Choice) + 1;
        return reserved;
    }

    /// <summary>The marks standing at <paramref name="now"/>.</summary>
    internal Dictionary<string, DateTimeOffset> Marks(DateTimeOffset now) =>
        new(_marks.Where(mark => mark.Value > now), StringComparer.OrdinalIgnoreCase);

    /// <summary>The earliest end after <paramref name="now"/> of a mark or a reservation, or null where none stands.</summary>
    internal DateTimeOffset? NextEnd(DateTimeOffset now) =>
        _marks.Values.Concat(_reserved.Select(each => each.At + StaffLedger.ReservationLife)).Where(end => end > now)
            .Select(end => (DateTimeOffset?)end).Min();

    private void Moved() => Interlocked.Increment(ref _revision);
}
