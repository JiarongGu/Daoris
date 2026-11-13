namespace Daoris.Knowledge;

/// <summary>
/// An ask's close and the end of its intake waiting on the person, written as one step with the close's reason. Every close crosses
/// the service, so none leaves the intake waiting on the driver's next look. An intake still running is left to its own end, which
/// the look judges against the closed ask.
/// </summary>
internal sealed class AskIntakeClose(AskStore asks, SessionStore sessions)
{
    public Task CloseAsync(string id, string reason, DateTimeOffset now, CancellationToken ct) => sessions.ExclusiveAsync(async inside =>
    {
        await asks.RecordClosedAsync(id, reason, now, inside).ConfigureAwait(false);
        if ((await asks.FindAsync(id, inside).ConfigureAwait(false))?.Intake is not { } intake
            || await sessions.FindAsync(intake, inside).ConfigureAwait(false) is not { State: SessionState.AwaitingPerson } waiting)
        {
            return true;
        }

        var line = $"the person closed ask `#{id}`:";
        await sessions.SetStateAsync(
                waiting.Id, SessionState.Stopped, $"{line} {reason}", null, null, now, inside,
                noteParts: NoteParts.After(null, null, [NoteLine.Coded(LedgerNoteCodes.IntakeClosed, line, ("ask", id)), NoteLine.Said(reason, "person")]))
            .ConfigureAwait(false);
        return true;
    }, ct);
}
