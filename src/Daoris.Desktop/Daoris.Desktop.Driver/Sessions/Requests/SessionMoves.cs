namespace Daoris.Driver;

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
    /// <param name="note">
    /// What the record of a process stopped here says in place of the plain stop: a pause's names the pause (PAUSE1b). An
    /// orphan's record says nothing ran it, which is the fact.
    /// </param>
    /// <param name="standing">
    /// That the stop runs under the person's standing confirmation (TERMTRUST1a, D201 point 7): each record it ends, a live one's or an
    /// orphan's, keeps its own note and gains the coded part <see cref="NoteCodes.StandingTerminal"/> after it.
    /// </param>
    public static async Task<StopAnswer> StopAsync(
        SessionProcesses processes, ServiceClient? service, string id, CancellationToken ct = default, Noted? note = null,
        bool standing = false)
    {
        var stopped = processes.Stop(id, note: standing ? Standing(note ?? Observation.Stopped) : note);
        var orphan = !stopped
            && service is not null
            && (await Orphans.EndAsync(service, processes, only: id, ct: ct, noted: standing ? Standing(Orphans.Noted) : null)
                .ConfigureAwait(false)).Count > 0;
        var elsewhere = !stopped && !orphan && processes.AliveOnThisMachine(id);
        return new StopAnswer(stopped || orphan, orphan, elsewhere);
    }

    /// <summary>
    /// A parked session finished, declined or stopped (design §4): the process goes first, then the record moves, with the
    /// person's words or the sentence that says the person did it. A refusal is the ledger's, in its words.
    /// </summary>
    /// <param name="stop">How this door ends the process: its registry's stop. False is the common case, since a parked session usually has no process here.</param>
    /// <exception cref="DriverException">The ledger refused the move.</exception>
    /// <param name="note">The person's own words, a part of their own (LANG1a); null writes the sentence that says they moved it.</param>
    public static Task<string> ResolveAsync(
        Func<string, bool> stop, ServiceClient service, string id, string state, string? note, CancellationToken ct = default) =>
        ResolveAsync(stop, service, id, state, noted: note is null ? null : Noted.Said(note, NoteBy.Person), ct);

    /// <summary>The same move with the driver's own line for the record: a pause's or an abandon's (PAUSE1b, PAUSE1d).</summary>
    /// <param name="noted">The record's note with its parts; null writes the sentence that says the person moved it.</param>
    public static async Task<string> ResolveAsync(
        Func<string, bool> stop, ServiceClient service, string id, string state, Noted? noted, CancellationToken ct = default)
    {
        stop(id);
        return await service.AdvanceAsync(id, state, noted ?? ByThePersonNoted(state), ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A parked session finished under the standing confirmation (TERMTRUST1a, D201 point 2, the terminal-trust design §2.1): never
    /// through the request folder and with no process ended, since the verb found none here; moved by the ledger only from
    /// <c>awaiting-person</c>, which it compares in its own step, so one resumed since the verb's read is not ended. The record says the
    /// person finished it, then that it came from a terminal under their grant.
    /// </summary>
    /// <exception cref="DriverException">The ledger refused the move: the record is no longer waiting on the person.</exception>
    public static Task<string> FinishStandingAsync(ServiceClient service, string id, CancellationToken ct = default) =>
        service.AdvanceAsync(id, "completed", Standing(ByThePersonNoted("completed")), ct: ct, expected: "awaiting-person");

    /// <summary>The English of <see cref="NoteCodes.StandingTerminal"/>, as the record keeps it.</summary>
    public const string StandingText = "Sent from a terminal and run under the person's standing confirmation, without asking.";

    /// <summary>A move's own note, then the coded part that says it ran under the standing confirmation (the terminal-trust design §7.1).</summary>
    public static Noted Standing(Noted move) => move.Then(" ", Noted.Of(NoteCodes.StandingTerminal, StandingText));

    /// <summary>
    /// What the record says when the person wrote nothing — never translated: it is data. The store keeps an earlier note
    /// when a move carries none, so without this a session finished at a checkpoint would read its parked analysis.
    /// </summary>
    public static string ByThePerson(string state) => ByThePersonNoted(state).Note;

    /// <summary><see cref="ByThePerson"/> with its code (LANG1a, the language design §4 rows 56–58).</summary>
    public static Noted ByThePersonNoted(string state) => state switch
    {
        "completed" => Noted.Of(NoteCodes.StoppedCheckpointFinished, "The person finished this at a checkpoint."),
        "stopped" => Noted.Of(NoteCodes.StoppedCheckpointStopped, "The person stopped this at a checkpoint."),
        _ => Noted.Of(NoteCodes.StoppedCheckpointMoved, "The person moved this at a checkpoint."),
    };
}
