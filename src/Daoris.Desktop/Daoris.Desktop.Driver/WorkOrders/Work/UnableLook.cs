namespace Daoris.Driver;

/// <summary>
/// The words a record could not go on with, read again at each look: the planner leaves them waiting. The session a reopen
/// names is one the planner goes on with too. Words held for the record's own account are read against that account each look
/// (<see cref="HeldAccountWords"/>): usable again, or past the bound, or chosen by the person for a new session, the planner goes on
/// with them, and the dock says which and why.
/// </summary>
[LookPass("unable", LookPhase.Read, "alone")]
internal sealed class UnableLook(ILookWiring wiring) : ILookPass
{
    public Task RunAsync(LookRound round, CancellationToken ct)
    {
        var snapshot = round.State.Snapshot;
        var records = snapshot.LastRun.Values.Concat(snapshot.NamedByReopen.Values).ToList();
        var unable = wiring.Marks.For(records);
        round.State.Snapshot = snapshot with { Unable = Lifted(records, unable, round.Events) };
        return Task.CompletedTask;
    }

    // The marks the planner still leaves waiting: a mark the look lifts is left on file, so the go-on reads it (its bound), and a try
    // that cannot go on marks the words anew.
    private IReadOnlyDictionary<string, GoOnMark> Lifted(IReadOnlyList<PriorSession> records, IReadOnlyDictionary<string, GoOnMark> unable, List<string> events)
    {
        if (!unable.Values.Any(mark => mark.Why == ContinueWhy.Account)) return unable;
        var now = wiring.Clock.GetUtcNow();
        var choices = new NewSessionChoices(wiring.Home);
        var kept = new Dictionary<string, GoOnMark>(unable, StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.Where(record => unable.GetValueOrDefault(record.Session) is { Why: ContinueWhy.Account }).DistinctBy(record => record.Session))
        {
            var mark = unable[record.Session];
            var own = record.Adapter is { Length: > 0 } adapter ? wiring.Harnesses.OwnHold(adapter, record.Profile) : NextHold.Missing;
            var lift = HeldAccountWords.Lift(record, mark, own, choices.Read(record.Session), now);
            if (lift == HeldLift.Stays) continue;
            kept.Remove(record.Session);
            events.Add(wiring.Lines.Progress("held", $"held  session {record.Session}: {Said(lift, now - mark.At)}"));
        }

        return kept;
    }

    private static string Said(HeldLift lift, TimeSpan held) => lift switch
    {
        HeldLift.Own => "the account it ran on can carry its held words again, so they go on in its own conversation.",
        HeldLift.Elsewhere =>
            $"its words waited {(int)held.TotalMinutes} minutes for the account it ran on, which still cannot carry them, so they go on in a new session on a ready account.",
        _ => "you chose a new session for its held words, so they go on now.",
    };
}
