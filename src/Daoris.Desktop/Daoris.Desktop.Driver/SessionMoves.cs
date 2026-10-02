namespace Daoris.Driver;

/// <summary>What a stop did (2026-09-25, REV3): ended a process here, ended an orphan's record, or found another Daoris process here running it.</summary>
/// <param name="Stopped">A process here was stopped, or a record nothing ran was ended.</param>
/// <param name="Orphan">What it ended was a record nothing on this machine ran: its ending is the person's, and says nothing ran it.</param>
/// <param name="Elsewhere">Another Daoris process on this machine runs it, which this one can neither stop nor call ended.</param>
public sealed record StopAnswer(bool Stopped, bool Orphan, bool Elsewhere);

/// <summary>
/// The person's moves on a session, as the screen's routes make them (<c>STOP_SESSION</c>, <c>RESOLVE_SESSION</c>) and a
/// loop makes them for a terminal's request (SESSUX1g, D126 §7.1): one implementation, whichever door asked.
/// </summary>
public static class SessionMoves
{
    /// <summary>
    /// A stop: this registry's process, ended as the person's; else, where nothing on this machine runs the record, that
    /// record ended as the person's stop (<see cref="Orphans"/>); else it is another Daoris process's here, said so.
    /// </summary>
    /// <param name="service">The loop's service, or null before it answers: an orphan cannot be ended then.</param>
    public static async Task<StopAnswer> StopAsync(
        SessionProcesses processes, ServiceClient? service, string id, CancellationToken ct = default)
    {
        var stopped = processes.Stop(id);
        var orphan = !stopped
            && service is not null
            && (await Orphans.EndAsync(service, processes, only: id, ct: ct).ConfigureAwait(false)).Count > 0;
        var elsewhere = !stopped && !orphan && processes.AliveOnThisMachine(id);
        return new StopAnswer(stopped || orphan, orphan, elsewhere);
    }

    /// <summary>
    /// A parked session finished, declined or stopped (design §4): the process goes first, then the record moves, with the
    /// person's words or the sentence that says the person did it. A refusal is the ledger's, in its words.
    /// </summary>
    /// <param name="stop">How this door ends the process: its registry's stop. False is the common case, since a parked session usually has no process here.</param>
    /// <exception cref="DriverException">The ledger refused the move.</exception>
    public static async Task<string> ResolveAsync(
        Func<string, bool> stop, ServiceClient service, string id, string state, string? note, CancellationToken ct = default)
    {
        stop(id);
        return await service.AdvanceAsync(id, state, note ?? ByThePerson(state), ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// What the record says when the person wrote nothing — never translated: it is data. The store keeps an earlier note
    /// when a move carries none, so without this a session finished at a checkpoint would read its parked analysis.
    /// </summary>
    public static string ByThePerson(string state) => state switch
    {
        "completed" => "The person finished this at a checkpoint.",
        "stopped" => "The person stopped this at a checkpoint.",
        _ => "The person moved this at a checkpoint.",
    };
}
