namespace Daoris.Driver;

/// <summary>
/// What the planner's look reads of a role's policy over its snapshot: for each open or
/// taken quest, the failures that park it, the transient or quiet end whose move the course decides once, and the typed holds a policy leaves.
/// The reads of a policy at one line's place are <see cref="StaffCourse"/>'s.
/// </summary>
internal static class StaffSnapshotCourse
{
    /// <summary>A driven quest's line as its rules read it (<see cref="RuledLine"/>), the registry placing the quest's repository in its workspace.</summary>
    internal static RuledLine LineOf(StaffLedger staff, Snapshot snapshot, QuestView quest, PriorSession? record) =>
        RuledLine.OfQuest(
            staff.Home, quest, record, snapshot.Repositories.FirstOrDefault(repo => string.Equals(repo.Repository, quest.To, StringComparison.OrdinalIgnoreCase))?.Workspace);

    /// <summary>The failed sessions that park each open quest: its line's effective failures rule, 0 where none holds.</summary>
    internal static IReadOnlyDictionary<string, int> FailuresAt(StaffLedger? staff, Snapshot snapshot)
    {
        var at = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (staff is null) return at;
        var book = staff.Rules();
        foreach (var quest in snapshot.Quests.Where(quest => quest.Status is "Open" or "Taken"))
        {
            at[quest.Id] = SupervisorRuleBook.FailuresAt(LineOf(staff, snapshot, quest, snapshot.LastRun.GetValueOrDefault(quest.Id)).In(book));
        }

        return at;
    }

    /// <summary>The failed sessions that park <paramref name="quest"/>: its rule's as the look read it, 0 where none holds.</summary>
    internal static int FailuresBound(Snapshot snapshot, string quest) => snapshot.FailuresAt.GetValueOrDefault(quest);

    /// <summary>Whether a record ended on the watcher's quiet end (<see cref="EndNoteCodes.EndedQuiet"/>), by its code: failed, and the quiet policy's, never the failed one's.</summary>
    internal static bool EndedQuiet(PriorSession record) =>
        string.Equals(record.State, "failed", StringComparison.OrdinalIgnoreCase) && record.NoteParts?.Any(part => part.Code == RecordNotes.EndedQuiet) == true;

    /// <summary>Whether a record is a failure that counts a strike and is no other condition's: not a limit, a refused account, a choice not offered, a service that did not answer or a quiet end.</summary>
    internal static bool FailedStrike(PriorSession record) =>
        string.Equals(record.State, "failed", StringComparison.OrdinalIgnoreCase)
        && !record.Limit && !record.NotOffered && !Planner.EndedTransient(record) && !EndedQuiet(record)
        && record.NoteParts?.Any(part => part.Code is { } code && (code == RecordNotes.UpdateClosed || RecordNotes.AccountsOwn.Contains(code))) != true;

    /// <summary>
    /// The planner's policy facts at a look: for each open or taken quest whose last session ended past its transient bound under a role that
    /// moves on <c>transient</c>, the course decides, once. A move is counted and written; an ask or the bound's hold is the quest's typed hold.
    /// </summary>
    internal static (IReadOnlyDictionary<string, StaffMoving> Moving, IReadOnlyDictionary<string, string> Holds) Look(
        StaffLedger? staff, Snapshot snapshot, DriverConfig config, DateTimeOffset now, bool write = true)
    {
        var moving = new Dictionary<string, StaffMoving>(StringComparer.OrdinalIgnoreCase);
        var holds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (staff is null) return (moving, holds);
        IReadOnlyDictionary<string, StaffMoving>? written = null;
        foreach (var quest in snapshot.Quests)
        {
            if (quest.Status is not ("Open" or "Taken") || !snapshot.LastRun.TryGetValue(quest.Id, out var last)) continue;
            var condition = Planner.EndedTransient(last) ? StaffConditions.Transient : EndedQuiet(last) ? StaffConditions.Quiet : FailedStrike(last) ? StaffConditions.Failed : null;
            if (condition is null || (condition == StaffConditions.Transient && config.ReleasesTransient(quest.Id, last.Session, last.Updated))) continue;
            var line = LineOf(staff, snapshot, quest, last);
            var role = line.Role;
            if (snapshot.Handed.TryGetValue(quest.Id, out var handed) && string.Equals(handed.From, last.Session, StringComparison.Ordinal)) continue;
            if (StaffCourse.CourseOf(staff, role, line.Place) is not { } policy || !policy.Moves.TryGetValue(condition, out _)) continue;

            // The strikes park a quest as they always did: a parked line is the person's, and a move would only be counted against it.
            var strikes = snapshot.Strikes.GetValueOrDefault(quest.Id) - config.ForgivenAt(quest.Id);
            var parks = FailuresBound(snapshot, quest.Id);
            if (condition != StaffConditions.Transient && parks > 0 && strikes >= parks) continue;

            written ??= staff.Moves.Moving();
            if (written.TryGetValue(quest.Id, out var done) && done.Session == last.Session)
            {
                moving[quest.Id] = done;
                continue;
            }

            var moved = staff.Moves.Of(quest.Id);
            var action = StaffJudge.OnCondition(policy, condition, new StaffLine(true, moved.Moves));
            // A fatal error moves a line after its `after` failures on the one choice; before them it is today's retry.
            // A retry is today's carry-on, which the planner makes without the course.
            if (condition != StaffConditions.Transient && !StaffActions.Moves(action.Do) && action.Do != StaffActions.Ask) continue;
            if (condition == StaffConditions.Failed && snapshot.FailedRuns.GetValueOrDefault(quest.Id) < (action.After ?? 1)) continue;
            var fact = new CoursePolicy(action, now, Met: StaffCourse.ChoiceOf(last, policy)?.Said, Approved: moved.Approved, Moves: moved.Moves);
            var course = condition == StaffConditions.Transient
                ? DrivenCourse.PastBound(last, quest.Status, released: false, fact)
                : DrivenCourse.PolicyEnd(last, quest.Status, fact);
            switch (course)
            {
                // A read of the holds alone (a plan nobody looked for) decides nothing and writes no move.
                case LineCourse.Move when !write:
                    break;
                case LineCourse.Move:
                    var from = StaffCourse.ChoiceOf(last, policy)?.Text;
                    if (staff.Moves.Decided(
                            quest.Id, role, last.Session, condition, action.Do, from,
                            read => !StaffJudge.OnCondition(policy, condition, new StaffLine(true, read.Moves)).MovesBound))
                    {
                        // A service that did not answer marks its agent; a fatal error or a quiet stop is the choice's alone, held off by the 30 minutes.
                        if (condition == StaffConditions.Transient && last.Adapter is { } agent) staff.MarkNotAnswering(agent);
                        moving[quest.Id] = new StaffMoving(last.Session, condition, from);
                    }
                    else if (StaffJudge.OnCondition(policy, condition, new StaffLine(true, staff.Moves.Of(quest.Id).Moves)).MovesBound)
                    {
                        holds[quest.Id] = HoldMovesBound;
                    }

                    // Otherwise the store would not take the move: nothing is decided, and the next look asks again. It is no bound reached.

                    break;
                case LineCourse.Hold { Why: CourseHoldWhy.PolicyAsk or CourseHoldWhy.MovesBound } hold:
                    holds[quest.Id] = hold.Why == CourseHoldWhy.MovesBound ? HoldMovesBound : HoldAsk;
                    break;
            }
        }

        return (moving, holds);
    }

    /// <summary>
    /// <paramref name="snapshot"/> with the typed holds a role's policy leaves (<c>policy-ask</c>, <c>moves-bound</c>), read without deciding or
    /// writing anything: what a plan nobody looked for (a terminal's, the retry door's, a window with no loop) answers, so it says the same hold
    /// the loop's look does and <i>Try again</i> approves it as one move.
    /// </summary>
    internal static Snapshot Held(StaffLedger? staff, Snapshot snapshot, DriverConfig config, DateTimeOffset now)
    {
        if (staff is null) return snapshot;
        snapshot = snapshot with { FailuresAt = FailuresAt(staff, snapshot) };
        var (_, holds) = Look(staff, snapshot, config, now, write: false);
        var limit = LimitHolds(staff, snapshot);
        return holds.Count == 0 && limit.Count == 0
            ? snapshot
            : snapshot with { PolicyHolds = holds.Concat(limit).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase) };
    }

    /// <summary>
    /// The quests an account's limit cut off whose role's policy holds them for the person's yes: an <c>ask</c> on a limit, or the
    /// bound of moves a limit's move would pass. The same typed hold a transient end leaves; a yes (one approval) lets the line walk.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> LimitHolds(StaffLedger? staff, Snapshot snapshot)
    {
        var holds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (staff is null) return holds;
        foreach (var quest in snapshot.Quests)
        {
            if (quest.Status is not ("Open" or "Taken") || !snapshot.LastRun.TryGetValue(quest.Id, out var last) || !last.Limit || last.State != "failed") continue;
            if (snapshot.Handed.ContainsKey(quest.Id)) continue;
            var line = LineOf(staff, snapshot, quest, last);
            if (StaffCourse.CourseOf(staff, line.Role, line.Place) is not { } policy || !policy.Moves.ContainsKey(StaffConditions.Limit)) continue;
            var moved = staff.Moves.Of(quest.Id);
            var action = StaffJudge.OnCondition(policy, StaffConditions.Limit, new StaffLine(true, moved.Moves));
            if (action.Do == StaffActions.Ask && !moved.Approved) holds[quest.Id] = action.MovesBound ? HoldMovesBound : HoldAsk;
        }

        return holds;
    }

    // The typed holds a policy leaves (held.policy-ask, held.moves-bound), as the planner says them.
    private const string HoldAsk = SittingFact.PolicyAsk;

    private const string HoldMovesBound = SittingFact.MovesBound;
}
