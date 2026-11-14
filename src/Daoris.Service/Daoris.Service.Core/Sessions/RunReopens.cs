namespace Daoris.Knowledge;

/// <summary>What a run's reopen moved: the records now waiting on its next step.</summary>
public sealed record RunReopenOutcome(string Message, IReadOnlyList<Session> Reopened);

/// <summary>
/// A run's records that ended done under the older meaning, where the agent's done ended the session, carried on under the newer one: a
/// record of a run still going on waits on its next step. Once: a record the event journal shows waiting on a step before is left, so a
/// record ended since by its workflow, or by the person, is never reopened.
/// </summary>
internal sealed class RunReopens(SessionStore sessions)
{
    /// <summary>
    /// Moves each of <paramref name="named"/> that is <c>completed</c> and has never waited on a step to <c>awaiting-step</c> with
    /// <paramref name="note"/>, in one write; any other is left as it stands.
    /// </summary>
    public Task<RunReopenOutcome> ReopenAsync(
        string run, IReadOnlyList<string> named, string note, string? noteParts, DateTimeOffset now, CancellationToken ct) =>
        sessions.ExclusiveAsync(async inside =>
        {
            var reopened = new List<Session>();
            foreach (var id in named.Distinct(StringComparer.Ordinal))
            {
                if (await sessions.FindAsync(id, inside).ConfigureAwait(false) is not { Origin: null, State: SessionState.Completed }) continue;
                if (await WaitedOnAStepAsync(id, inside).ConfigureAwait(false)) continue;
                var moved = await sessions.SetStateAsync(id, SessionState.AwaitingStep, note, null, null, now, inside, noteParts: noteParts)
                    .ConfigureAwait(false);
                if (moved is not null) reopened.Add(moved);
            }

            return new RunReopenOutcome($"Run `{run}`: {reopened.Count} session(s) ended done wait on its next step.", reopened);
        }, ct);

    private Task<bool> WaitedOnAStepAsync(string id, CancellationToken ct) => sessions.Db.RunAsync(async () =>
    {
        await using var command = sessions.Db.Command();
        command.CommandText = """
            SELECT EXISTS (SELECT 1 FROM event_journal
                           WHERE record = $id AND family = 'sessions' AND change = 'state' AND state_new = $state)
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$state", nameof(SessionState.AwaitingStep));
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }, ct);
}
