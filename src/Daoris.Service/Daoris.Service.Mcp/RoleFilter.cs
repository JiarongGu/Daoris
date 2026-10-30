using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Daoris.Knowledge.Mcp;

/// <summary>
/// Where the call layer places a connector's session (FOUNDATION1r; the foundation design §2.2, §2.3): the work its record is read as,
/// a brainstorm's turn, or why it could not be placed. Read from the session the driver named on the connector it offers
/// (<see cref="IntakeScope.Session"/>), never from anything a call says.
/// </summary>
/// <param name="Work">A <see cref="ParticipantWork"/> code, or one of the placements below.</param>
/// <param name="Turn">A brainstorm's turn, <see cref="Generator"/> or <see cref="Synthesizer"/>; null for every other work.</param>
public sealed record SessionWork(string Work, string? Turn = null)
{
    public const string Generator = "generator";
    public const string Synthesizer = "synthesizer";

    /// <summary>A connector that names no session, a person's own agent's: kept to its scope before the call layer.</summary>
    public static readonly SessionWork Unnamed = new("unnamed");

    /// <summary>A named session no record of this machine places: none is kept, or it is another machine's.</summary>
    public static readonly SessionWork Unplaced = new("unplaced");

    /// <summary>A named session whose record could not be read.</summary>
    public static readonly SessionWork Unread = new("unread");

    /// <summary>A brainstorm generator's pass, known by its turn (BRAINSTORM1a2), which keeps no record.</summary>
    public static readonly SessionWork Generating = new(ParticipantWork.Brainstorm, Generator);

    /// <summary>A brainstorm synthesis's pass (<c>s1</c>, <c>s1r</c>).</summary>
    public static readonly SessionWork Synthesizing = new(ParticipantWork.Brainstorm, Synthesizer);

    public override string ToString() => Turn is null ? Work : $"{Work}:{Turn}";
}

/// <summary>
/// The connector's session's work (FOUNDATION1r): a brainstorm's turn placed first, else the record's work by the one rule, read once it
/// is found (the marks are set at the open and never change), and read again while it is not.
/// </summary>
/// <param name="ledger">The ledger the record is read from; none reads a named session as unread.</param>
/// <param name="intake">Which session this connector speaks for, when the driver started it.</param>
/// <param name="brainstorm">The brainstorm turn the driver named on this connector, where it offers a pass one; none names none.</param>
public sealed class SessionWorks(SessionLedger? ledger, IntakeScope? intake, BrainstormScope? brainstorm = null)
{
    private SessionWork? _found;

    /// <summary>The session's work, or its placement where no work places it.</summary>
    public async Task<SessionWork> ReadAsync(CancellationToken ct)
    {
        if (_found is { } found) return found;
        // A brainstorm pass is known by its turn, never by a record: none is opened for it (BRAINSTORM1a2).
        if (brainstorm is { Active: true } turn) return turn.Synthesis ? SessionWork.Synthesizing : SessionWork.Generating;
        if (intake?.Session is not { Length: > 0 } session) return SessionWork.Unnamed;
        if (ledger is null) return SessionWork.Unread;

        var record = await ledger.FindAsync(session, ct).ConfigureAwait(false);
        if (record is null) return SessionWork.Unplaced;
        _found = ParticipantWork.Of(record) is { } work ? new SessionWork(work) : SessionWork.Unplaced;
        return _found;
    }
}

/// <summary>
/// Every <c>tools/call</c> judged by the session's work before its tool runs (FOUNDATION1r, by <see cref="CallTable"/>): a refused call
/// is answered with its sentence, its code in <c>_meta</c>, and never reaches the tool. A native child that inherits the connector's
/// environment inherits its refusals.
/// </summary>
public static class RoleFilter
{
    /// <summary>The <c>_meta</c> key a refusal's code is answered under.</summary>
    public const string RefusalKey = "daoris/refusal";

    /// <summary>Each call refused where the table refuses it to the session's work, else passed on; a work that cannot be read is unread.</summary>
    public static IMcpServerBuilder RefuseOutsideRole(this IMcpServerBuilder builder, Func<CancellationToken, Task<SessionWork>> role) =>
        builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
        {
            var name = request.Params?.Name;
            SessionWork work;
            try
            {
                work = await role(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                work = SessionWork.Unread;
            }

            return CallTable.Refusal(work, name) is { } refusal
                ? new CallToolResult { Content = [new TextContentBlock { Text = refusal.Text }], Meta = new JsonObject { [RefusalKey] = refusal.Code } }
                : await next(request, cancellationToken).ConfigureAwait(false);
        }));
}
