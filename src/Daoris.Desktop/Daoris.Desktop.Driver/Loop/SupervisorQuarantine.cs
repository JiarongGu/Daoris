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
