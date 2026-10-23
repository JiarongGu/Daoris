namespace Daoris.Driver;

/// <summary>
/// A word Daoris cannot attribute is quarantined, never handed (SUPERVISOR1a-i, D208; the supervisor design §6.1, §3.6): at each look,
/// before it plans, every waiting <c>from: supervisor</c> word whose id the decisions' memory did not record from its own release
/// (<see cref="QuestionSupervisorDecisions.Authored"/>), or whose decision is final, is taken back through SAIDBACK1a's door on a record
/// that does not run, and logged <c>supervisor.unattributed</c>; and it is left out of the snapshot the look plans from, so it wakes no
/// park and starts no turn. A record that runs keeps it, not handable, until its run ends and a later look takes it back.
/// </summary>
internal static class SupervisorQuarantine
{
    /// <summary>
    /// The look's moves of each decision not yet final, before the quarantine and before anything plans (§3.4, §3.6, §6.3): its files
    /// compared with memory (a difference is <c>stale</c>, tampered), then the record read (a held one with the owner's word waiting,
    /// or a released one whose word is gone, <c>superseded</c>; a released one on a record no longer parked, <c>stale</c>), then the
    /// release of each whose <c>goesOnAt</c> came, a lost response repeated under its receipt first. Returns the snapshot with the
    /// sessions a held decision holds (<see cref="Snapshot.SupervisorHeld"/>).
    /// </summary>
    public static async Task<Snapshot> DecideAsync(
        Snapshot snapshot, QuestionSupervisorDecisions decisions, SupervisorSay say, SupervisorTakeBack takeBack, DateTimeOffset now,
        CancellationToken ct)
    {
        var records = snapshot.LastRun.Values.Concat(snapshot.NamedByReopen.Values)
            .Where(record => !record.Teammate && decisions.Moving(record.Session))
            .DistinctBy(record => record.Session, StringComparer.Ordinal)
            .ToList();
        foreach (var record in records)
        {
            await decisions.CompareAsync(record.Session, takeBack, now, ct).ConfigureAwait(false);
            var waiting = (record.Said ?? []).Select(word => word.Id).OfType<string>().ToList();
            await decisions.SettleAsync(record.Session, record.Parked, record.Waiting.Any(word => word.Persons), waiting, takeBack, now, ct)
                .ConfigureAwait(false);
            await decisions.ReleaseAsync(record.Session, say, now, ct).ConfigureAwait(false);
        }

        var held = records.Where(record => decisions.HeldUntil(record.Session) is not null).Select(record => record.Session);
        var chose = records
            .SelectMany(record => decisions.LiveOf(record.Session).Where(each => each.State == SupervisorState.Released))
            .DistinctBy(each => each.Session, StringComparer.Ordinal)
            .ToDictionary(each => each.Session, each => each.Choice, StringComparer.Ordinal);
        return snapshot with { SupervisorHeld = new HashSet<string>(held, StringComparer.Ordinal), SupervisorChose = chose };
    }

    /// <summary>The waiting words of <paramref name="record"/> that claim the supervisor and are not handable now.</summary>
    public static IReadOnlyList<string> Unhandable(PriorSession record, QuestionSupervisorDecisions decisions) =>
        [.. (record.Said ?? []).Where(word => word.Supervisors && word.Id is { } id && !decisions.Handable(record.Session, id))
            .Select(word => word.Id!)];

    /// <summary>
    /// Take back every unhandable supervisor's word on this machine's records the look read, and leave each out of what it plans from:
    /// a running record's word only out of the plan. Returns the snapshot the look plans from.
    /// </summary>
    public static async Task<Snapshot> LookAsync(
        Snapshot snapshot, QuestionSupervisorDecisions decisions, SupervisorTakeBack takeBack, MachineLog? log, CancellationToken ct)
    {
        async Task<IReadOnlyDictionary<string, PriorSession>> Cleared(IReadOnlyDictionary<string, PriorSession> records)
        {
            Dictionary<string, PriorSession>? changed = null;
            foreach (var (quest, record) in records)
            {
                if (record.Teammate || Unhandable(record, decisions) is not { Count: > 0 } quarantined) continue;
                if (!record.Running)
                {
                    foreach (var word in quarantined)
                    {
                        if (!decisions.Authored(record.Session, word)) log?.Warn("supervisor.unattributed", ("session", record.Session));
                        await takeBack(record.Session, word, ct).ConfigureAwait(false);
                    }
                }

                changed ??= new Dictionary<string, PriorSession>(records, StringComparer.Ordinal);
                changed[quest] = record with { Said = [.. record.Said!.Where(word => word.Id is null || !quarantined.Contains(word.Id))] };
            }

            return changed ?? records;
        }

        return snapshot with { LastRun = await Cleared(snapshot.LastRun).ConfigureAwait(false), NamedByReopen = await Cleared(snapshot.NamedByReopen).ConfigureAwait(false) };
    }
}
