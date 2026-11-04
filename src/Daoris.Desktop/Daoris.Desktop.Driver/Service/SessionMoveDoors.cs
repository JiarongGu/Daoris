using System.Text;
using System.Text.Json;
using Daoris.Contracts.Sessions;
using static Daoris.Driver.RefusalWords;

namespace Daoris.Driver;

/// <summary>
/// The doors that move a session's record (the service's session routes): a move to a state with its note and evidence, and the put-back of a
/// park the driver took up and could not hand back. A class over the client, reached as its <see cref="ServiceClient.Moves"/> and by the
/// client's own doors of the same names; what it moved is told to the client's <see cref="ServiceClient.Moved"/> watchers.
/// </summary>
public sealed class SessionMoveDoors
{
    /// <summary>The states a session's record ends in: where its staged memory's models are named (the memory coordinator design §1).</summary>
    private static readonly string[] Ending = ["completed", "stopped", "failed", "declined", "stood-down"];

    private readonly ServiceClient _service;

    internal SessionMoveDoors(ServiceClient service) => _service = service;

    /// <summary>Move a session's record. The ledger judges; the driver reports what it observed.</summary>
    /// <param name="interrupted">
    /// That a stop was not the person's (D104): the orphan sweep's, or this driver's shutdown — so a take it ended is carried on. A host older
    /// than the field ignores it, and the stop reads as the person's.
    /// </param>
    /// <param name="limit">
    /// That an account's limit refused the turn of a session moved to <c>failed</c> (TOOL4d, D125 §5.2) — which is never a strike. Sent only
    /// where true; the ledger refuses it on a move to any other state (TOOL4c). It names no account.
    /// </param>
    /// <param name="parts">
    /// The note's parts beside its English (LANG1a, D142 point 2), sent only with a note: a note sent without them clears the record's parts, so
    /// an older driver's note never sits beside stale ones.
    /// </param>
    /// <param name="expected">
    /// The state the record must be in, which the ledger compares in its step (TERMTRUST1a, D201 point 2): a finish under the standing
    /// confirmation sends <c>awaiting-person</c>.
    /// </param>
    /// <param name="unbindAsk">
    /// That an intake's end lets its ask go for the next intake (LIMITRESUME1h, D206 design §3.8), in the same step, only where the ask still
    /// names it; a move to <c>failed</c> alone.
    /// </param>
    /// <param name="expect">
    /// The exact ids of the supervisor's words a go-on's move to working admits (SUPERVISOR1a-i, D208; the supervisor design §6.3): the ledger
    /// refuses the move (<c>words-changed</c>) where the record's waiting supervisor's words are not exactly these. Null sends none.
    /// </param>
    public async Task<string> AdvanceAsync(
        string id, string state, string? note = null, string? evidence = null, string? transcript = null,
        CancellationToken ct = default, bool interrupted = false, bool limit = false, IReadOnlyList<NotePart>? parts = null,
        string? expected = null, bool unbindAsk = false, IReadOnlyList<string>? expect = null)
    {
        var turns = Ending.Contains(state) ? await TurnLinesForAsync(id, ct).ConfigureAwait(false) : null;
        var request = new AdvanceSession(
            id, state, note, evidence, transcript, interrupted ? true : null, limit ? true : null,
            note is not null && parts is { Count: > 0 } ? PartsElement(parts) : null,
            expected, unbindAsk ? true : null, expect is null ? null : [.. expect],
            turns?.ToDictionary(
                pair => pair.Key,
                pair => new MemoryTurnsRequest(
                    [.. pair.Value.Select(line => new MemoryTurnLine(line.Seq, line.Started, line.Ended, line.Model))], []),
                StringComparer.Ordinal));

        var sent = await _service.SendAsync(request, ct).ConfigureAwait(false);

        // The refusal sentence is the contract; losing it would make the driver's log say less than the service said.
        if (!sent.Reply.Ok) throw new DriverException($"the service refused moving session `{id}` to {state}: {ErrorWords(sent.Reply.Payload) ?? sent.Reply.Payload}");

        // TREELOCK2: the successful move's local record owns the lock, including a person's end of a park after its run returned.
        // Use the returned state, never the requested one: a refused move releases nothing, and a teammate supplies no machine path.
        if (sent.Answer?.Session is { } record && !SessionReads.IsTeams(record) && record.Id == id)
        {
            await SessionEnds.Ended(SessionReads.Prior(record)).ConfigureAwait(false);
        }

        _service.RecordMoved(new SessionMoved(id, sent.Answer?.Session?.State ?? state));
        return sent.Answer?.Message ?? "";
    }

    /// <summary>Move a session's record with a composed note: its English and its parts together (LANG1a).</summary>
    public Task<string> AdvanceAsync(
        string id, string state, Noted noted, string? evidence = null, string? transcript = null,
        CancellationToken ct = default, bool interrupted = false, bool limit = false, string? expected = null,
        IReadOnlyList<string>? expect = null) =>
        AdvanceAsync(id, state, noted.Note, evidence, transcript, ct, interrupted, limit, noted.Parts, expected, expect: expect);

    /// <summary>
    /// An intake refused its choice at the open (D205 §3.5(f), MODELROLE1d): one write ends its record <paramref name="conclusion"/>
    /// (<c>failed</c>, <c>ended.choice-not-offered</c>) and lets its ask go, only where the ask's binding still names
    /// <paramref name="session"/>; the record stays, naming its ask, so the ask's history keeps its line.
    /// </summary>
    public Task EndRefusedIntakeAsync(string session, SessionConclusion conclusion, CancellationToken ct = default) =>
        AdvanceAsync(session, conclusion.State, note: conclusion.Note, ct: ct, parts: conclusion.Parts, unbindAsk: true);

    /// <summary>
    /// Put a park the driver took up and could not hand back to waiting on the owner (SUPERVISOR1a-i, D208; the supervisor design §6.3), keeping
    /// every waiting word but <paramref name="takeBack"/>, the supervisor's word it admitted, with the note the look read. Refused where the
    /// record is not the working one a go-on took up; a refusal throws as a move's does.
    /// </summary>
    public async Task RestoreParkAsync(string id, IReadOnlyList<string> takeBack, Noted? noted, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(
            new RestoreSessionPark(id, [.. takeBack], noted?.Note, noted is { Parts: { Count: > 0 } parts } ? PartsElement(parts) : null),
            ct).ConfigureAwait(false);
        if (!sent.Reply.Ok) throw new DriverException($"the service refused restoring session `{id}`'s park: {ErrorWords(sent.Reply.Payload) ?? sent.Reply.Payload}");

        _service.RecordMoved(new SessionMoved(id, "awaiting-person"));
    }

    /// <summary>
    /// The turn lines of <paramref name="id"/> and of each session it continues, as its record names them; null where none is known or the
    /// records did not read, which costs the rows their models and never the move.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<ServiceClient.TurnLine>>?> TurnLinesForAsync(string id, CancellationToken ct)
    {
        if (_service.TurnLines is null) return null;
        var chain = new List<string> { id };
        try
        {
            var records = SessionReads.Records(await _service.SessionList.ListedAsync(true, ct).ConfigureAwait(false));
            // A chain longer than any hand-over makes is cut: the lines are evidence, never a reason to fail.
            while (chain.Count < 16 && records.GetValueOrDefault(chain[^1])?.From is { Length: > 0 } from && !chain.Contains(from, StringComparer.OrdinalIgnoreCase))
            {
                chain.Add(from);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The session's own lines still go.
        }

        var lines = _service.TurnLines(chain);
        return lines.Count == 0 ? null : lines;
    }

    /// <summary>The note's parts as the record's JSON spells them, for a route that carries them beside the note.</summary>
    private static JsonElement PartsElement(IReadOnlyList<NotePart> parts)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            NotePart.Write(writer, parts);
        }

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        return document.RootElement.Clone();
    }
}
