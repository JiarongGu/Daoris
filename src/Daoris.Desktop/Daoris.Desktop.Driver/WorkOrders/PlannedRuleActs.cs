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
    /// <param name="roleOf">The role a quest's line goes on under.</param>
    public static IReadOnlyList<RuleActed> Of(
        Snapshot snapshot, IReadOnlyList<Consideration> plan, SupervisorRuleBook book, Func<QuestView, PriorSession, string> roleOf, DateTimeOffset at)
    {
        var acts = new List<RuleActed>();

        void Add(QuestView quest, string count, Func<SupervisorRule, bool> acting, int? n, string point, string? session = null)
        {
            if (!snapshot.LastRun.TryGetValue(quest.Id, out var last)) return;
            var effective = book.For(roleOf(quest, last), quest.To, WorkspaceOf(snapshot, quest), quest.Id);
            if (!effective.TryGetValue(count, out var rules) || rules.Where(acting).MinBy(rule => rule.At) is not { } rule) return;
            acts.Add(new RuleActed(quest.Id, session ?? last.Session, rule, book.RevisionOf(rule.Place), n ?? rule.At, Known: true, point, at));
        }

        foreach (var held in plan)
        {
            switch (held)
            {
                case { Verdict: StartVerdict.Exhausted, Failures: { } failures }:
                    Add(held.Quest, SupervisorRules.Failures, rule => rule.Do == SupervisorRules.Hold, failures, "start");
                    break;
                case { Verdict: StartVerdict.TransientHeld, HeldBy: { } ended }:
                    Add(held.Quest, SupervisorRules.Tries, rule => rule.Do == SupervisorRules.Hold, null, "episode", ended.Session);
                    break;
                case { Verdict: StartVerdict.PolicyHeld, Said.Code: SittingFact.MovesBound, HeldBy: { } ended }:
                    Add(held.Quest, SupervisorRules.Moves, rule => rule.Do == SupervisorRules.Ask, null, "start", ended.Session);
                    break;
            }
        }

        foreach (var (quest, moving) in snapshot.PolicyMoving)
        {
            if (snapshot.Quests.FirstOrDefault(each => string.Equals(each.Id, quest, StringComparison.OrdinalIgnoreCase)) is not { } asked) continue;
            if (CountOf(moving.Condition) is not { } count) continue;
            Add(asked, count, rule => StaffActions.Moves(rule.Do), null, "episode", moving.Session);
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

    private static string? WorkspaceOf(Snapshot snapshot, QuestView quest) =>
        snapshot.Repositories.FirstOrDefault(repo => string.Equals(repo.Repository, quest.To, StringComparison.OrdinalIgnoreCase))?.Workspace;
}
