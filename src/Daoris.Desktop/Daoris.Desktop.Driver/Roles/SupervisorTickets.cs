namespace Daoris.Driver;

/// <summary>
/// Admission by ticket (§6.3): the go-on's move to working, the single-use ticket's hand, and the two ends of an admitted decision. Moves
/// are made under the session's lock in the <see cref="SupervisorBook"/>; this type holds no state of its own.
/// </summary>
internal sealed class SupervisorTickets(SupervisorBook book, IDecisionAdmission admission)
{
    /// <summary>
    /// The go-on's move to working, under the decision's lock: a held decision moves nothing (null, and the next look plans again); a
    /// released one rechecks the generation (stale where a revoke came first) and calls <paramref name="move"/> with <c>expect</c>, the
    /// exact ids of the handable supervisor's words; the move succeeding makes it <c>admitted</c> with a single-use ticket. A session with
    /// no live decision moves with <c>expect</c> empty.
    /// </summary>
    /// <returns>Whether the record moved, and the ticket where a supervisor's word was admitted.</returns>
    public Task<(bool Moved, SupervisorTicket? Ticket)> AdmitAsync(
        string session, Func<IReadOnlyList<string>, Task<bool>> move, string goOn, DateTimeOffset now, CancellationToken ct = default) =>
        book.UnderAsync(session, async () =>
        {
            var live = book.Live.Values.Where(each => each.Session == session && !SupervisorStates.Final(each.State)).ToList();
            if (live.Any(each => each.State == SupervisorState.Held)) return (false, (SupervisorTicket?)null);
            if (live.FirstOrDefault(each => each.State == SupervisorState.Released) is not { } released)
            {
                return (await move([]).ConfigureAwait(false), (SupervisorTicket?)null);
            }

            if (admission.Admit(released.Repository) is not { Why: null, Generation: { } generation } || generation != released.Generation)
            {
                book.Move(released, SupervisorState.Stale, now, SupervisorWhy.Revoked);
                return (false, null);
            }

            if (!await move([released.Word!]).ConfigureAwait(false)) return (false, null);
            released.Ticket = new SupervisorTicket(session, released.Id, released.Word!, goOn);
            book.Move(released, SupervisorState.Admitted, now, null);
            return (true, released.Ticket);
        }, ct);

    /// <summary>
    /// The paragraph the ticket's prompt hands (§4.5), composed from the decision in memory, never the word's stored text; consuming the
    /// ticket, so a second build with it hands nothing. Null for a ticket already used or not this decision's.
    /// </summary>
    public string? Hand(SupervisorTicket ticket)
    {
        if (!book.Live.TryGetValue(ticket.Decision, out var decision) || decision.State != SupervisorState.Admitted) return null;
        lock (decision)
        {
            if (decision.Ticket is null || decision.Ticket != ticket) return null;
            decision.Ticket = null;
        }

        return SupervisorHanded.Text(decision.Choice);
    }

    /// <summary>The move after an admission (§6.3): <c>went-on</c> once the run is spawned, <c>not-handed</c> on a failure before the wire.</summary>
    public Task<bool> AfterAdmissionAsync(SupervisorTicket ticket, SupervisorState to, string? why, DateTimeOffset at, CancellationToken ct) =>
        book.UnderAsync(ticket.Session, () => Task.FromResult(
            book.Live.TryGetValue(ticket.Decision, out var decision) && decision.State == SupervisorState.Admitted
            && book.Move(decision, to, at, why) == to), ct);
}
