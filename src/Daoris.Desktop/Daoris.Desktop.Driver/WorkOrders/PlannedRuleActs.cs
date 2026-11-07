namespace Daoris.Driver;

/// <summary>
/// The rules' acts a look's plan made (the context counters design §4.7): a quest parked by its failed sessions, held on a transient
/// episode, moved to another choice by a rule, or held by the bound of moves, each as the <see cref="RuleActed"/> of the rule that acts by
/// it, read from the plan and the snapshot alone.
/// </summary>
internal static class PlannedRuleActs
{
    /// <summary>
    /// The acts <paramref name="plan"/> and <paramref name="snapshot"/> hold, each on its quest's last session. A quest whose last session,
    /// role or rule does not read acts nothing.
    /// </summary>
    /// <param name="lineOf">A quest's line as its rules read it.</param>
    /// <param name="movesOf">A quest's line's moves in 24 hours as the store answers them; null where it does not.</param>
    public static IReadOnlyList<RuleActed> Of(
        Snapshot snapshot, IReadOnlyList<Consideration> plan, SupervisorRuleBook book, Func<QuestView, PriorSession, RuledLine> lineOf,
        Func<string, int?> movesOf, DateTimeOffset at)
    {
        var acts = new List<RuleActed>();

        // Each act says the count its executor read; a count that did not read acts nothing said.
        void Add(QuestView quest, string count, Func<SupervisorRule, bool> acting, int? n, string point, string? session = null)
        {
            if (n is null || !snapshot.LastRun.TryGetValue(quest.Id, out var last)) return;
            var effective = lineOf(quest, last).In(book);
            if (!effective.TryGetValue(count, out var rules) || rules.Where(acting).MinBy(rule => rule.At) is not { } rule) return;
            acts.Add(new RuleActed(quest.Id, session ?? last.Session, rule, book.RevisionOf(rule.Place), n.Value, Known: true, point, at));
        }

        foreach (var held in plan)
        {
            switch (held)
            {
                case { Verdict: StartVerdict.Exhausted, FailedSince: { } failed }:
                    Add(held.Quest, SupervisorRules.Failures, rule => rule.Do is SupervisorRules.Hold or SupervisorRules.Refer, failed, "start", held.LastSession);
                    break;
                // A failed choice's ask is the person's yes as it always was; only a refer's is kept, the point the supervisor is asked at.
                case { Verdict: StartVerdict.PolicyHeld, Said.Code: SittingFact.PolicyAsk, HeldBy: { } ended } when StaffSnapshotCourse.FailedStrike(ended):
                    Add(held.Quest, SupervisorRules.FailuresOnChoice, rule => rule.Do == SupervisorRules.Refer, snapshot.FailedRuns.GetValueOrDefault(held.Quest.Id), "start", ended.Session);
                    break;
                case { Verdict: StartVerdict.TransientHeld, HeldBy: { } ended }:
                    Add(held.Quest, SupervisorRules.Tries, rule => rule.Do == SupervisorRules.Hold, LineCounts.TriesOf(ended), "episode", ended.Session);
                    break;
                case { Verdict: StartVerdict.PolicyHeld, Said.Code: SittingFact.MovesBound, HeldBy: { } ended }:
                    Add(held.Quest, SupervisorRules.Moves, rule => rule.Do == SupervisorRules.Ask, movesOf(held.Quest.Id), "start", ended.Session);
                    break;
            }
        }

        foreach (var (quest, moving) in snapshot.PolicyMoving)
        {
            if (snapshot.Quests.FirstOrDefault(each => string.Equals(each.Id, quest, StringComparison.OrdinalIgnoreCase)) is not { } asked) continue;
            if (CountOf(moving.Condition) is not { } count) continue;
            var ended = snapshot.LastRun.GetValueOrDefault(quest);
            int? n = moving.Condition switch
            {
                StaffConditions.Transient => LineCounts.TriesOf(ended),
                StaffConditions.Failed => snapshot.FailedRuns.GetValueOrDefault(quest),
                _ => 1,
            };
            Add(asked, count, rule => StaffActions.Moves(rule.Do), n, "episode", moving.Session);
        }

        return acts;
    }

    /// <summary>The count a condition's move is a rule on.</summary>
    private static string? CountOf(string condition) => condition switch
    {
        StaffConditions.Transient => SupervisorRules.Tries,
        StaffConditions.Failed => SupervisorRules.FailuresOnChoice,
        StaffConditions.Quiet => SupervisorRules.Quiet,
        StaffConditions.Limit => SupervisorRules.Limits,
        _ => null,
    };
}
