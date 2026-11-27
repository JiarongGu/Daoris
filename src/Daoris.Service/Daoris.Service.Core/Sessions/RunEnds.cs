namespace Daoris.Knowledge;

/// <summary>
/// A workflow run's end, the one place the runtime writes quest state: the run's quests Done on the submissions their agents' dones made,
/// and the sessions waiting on the run ended, in one write, so no reader sees the work done while its session still waits, or the reverse.
/// </summary>
/// <param name="Refusal">Why the quests stay unclosed, which then moves no session; <see cref="QuestCloseRefusal.None"/> where the close is made.</param>
/// <param name="Closed">The quests written Done.</param>
/// <param name="Ended">The sessions moved to their end; one no longer waiting on a step is left as it stands.</param>
public sealed record RunEndOutcome(QuestCloseRefusal Refusal, string Message, IReadOnlyList<Quest> Closed, IReadOnlyList<Session> Ended);

internal sealed class RunEnds(QuestStore quests, SessionStore sessions, SessionAdvances advances)
{
    /// <summary>
    /// Ends run <paramref name="run"/>: where it came to its graph's end (<c>completed</c>), each of <paramref name="named"/>, its chain's
    /// quests here in order with the submission each stands on, is closed Done through the close door, under a key no other end shares, each
    /// of <paramref name="passed"/> (its holds passed over by the person, their yes) accepted first; then each of <paramref name="ended"/>
    /// still awaiting a step moves to <paramref name="state"/>. A close refused moves no session.
    /// </summary>
    public Task<RunEndOutcome> EndAsync(
        string run, IReadOnlyList<(string Id, QuestOperationRef Submission)> named, IReadOnlyList<string> ended, string state, string? note,
        string? noteParts, DateTimeOffset now, CancellationToken ct, IReadOnlyCollection<string> passed) =>
        sessions.ExclusiveAsync(async inside =>
        {
            IReadOnlyList<Quest> closed = [];
            if (named.Count > 0 && state == Session.Spell(SessionState.Completed))
            {
                foreach (var (id, _) in named.Where(each => passed.Contains(each.Id, StringComparer.Ordinal)))
                {
                    await quests.AcceptAsync(id, now, inside).ConfigureAwait(false);
                }

                var at = named[^1].Id;
                var closing = await quests.CloseAsync(at, named, new QuestClose($"run-end/{run}/{now.UtcTicks}", run), now, inside)
                    .ConfigureAwait(false);
                if (closing.Refusal != QuestCloseRefusal.None)
                {
                    return new RunEndOutcome(closing.Refusal,
                        $"Run `{run}`'s quests were not closed ({closing.Refusal} at `#{closing.On ?? at}`), so no session of it moved.", [], []);
                }

                closed = closing.Quests;
            }

            var moved = new List<Session>();
            foreach (var id in ended.Distinct(StringComparer.Ordinal))
            {
                var advanced = await advances.AdvanceAsync(
                    id, state, note, null, null, now, inside, noteParts: note is null ? null : noteParts,
                    expected: Session.Spell(SessionState.AwaitingStep), witness: $"run-end {run}").ConfigureAwait(false);
                if (advanced.Refusal == SessionAdvanceRefusal.None) moved.Add(advanced.Session!);
            }

            return new RunEndOutcome(QuestCloseRefusal.None,
                $"Run `{run}` ended: {closed.Count} quest(s) Done, {moved.Count} session(s) {state}.", closed, moved);
        }, ct);
}
