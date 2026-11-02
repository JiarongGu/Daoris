namespace Daoris.Driver;

/// <summary>
/// What the driver reads of a role's policy to hand the course its met condition (STAFFPOLICY1e, D226; the staff policy design §3.3, §4): the
/// facts are read once at the look, the course decides, and a move the course made is written down (<see cref="StaffMoves"/>) before anything
/// acts on it, so the planner, the start and a restart all read the one decision. With no policy that applies every answer is today's.
/// </summary>
internal static class StaffCourse
{
    /// <summary>The tries a driven run's transient episode makes: the role's <c>after</c> where it moves on <c>transient</c>, else D210's five.</summary>
    internal static int Tries(StaffLedger? staff, string? role)
    {
        if (staff is null || role is null || staff.Policy(role).Applied is not { } policy) return TransientRetry.Bound;
        return policy.Moves.TryGetValue(StaffConditions.Transient, out var move) && move.After is { } after
            ? Math.Clamp(after, 1, TransientRetry.Bound)
            : TransientRetry.Bound;
    }

    /// <summary>The choice a record ran, as the policy keys it.</summary>
    internal static StaffKey? ChoiceOf(PriorSession record) =>
        record.Adapter is { Length: > 0 } agent ? new StaffKey(agent, record.Asked?.NamedModel, record.Asked?.Effort.Value) : null;

    /// <summary>
    /// The planner's policy facts at a look: for each open or taken quest whose last session ended past its transient bound under a role that
    /// moves on <c>transient</c>, the course decides, once. A move is counted and written; an ask or the bound's hold is the quest's typed hold.
    /// </summary>
    internal static (IReadOnlyDictionary<string, StaffMoving> Moving, IReadOnlyDictionary<string, string> Holds) Look(
        StaffLedger? staff, Snapshot snapshot, DriverConfig config, DateTimeOffset now)
    {
        var moving = new Dictionary<string, StaffMoving>(StringComparer.OrdinalIgnoreCase);
        var holds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (staff is null) return (moving, holds);
        IReadOnlyDictionary<string, StaffMoving>? written = null;
        foreach (var quest in snapshot.Quests)
        {
            if (quest.Status is not ("Open" or "Taken") || !snapshot.LastRun.TryGetValue(quest.Id, out var last)) continue;
            if (!Planner.EndedTransient(last) || config.ReleasesTransient(quest.Id, last.Session, last.Updated)) continue;
            // A record the service kept no role for was started by Build's walk, as a quest's session is.
            var role = last.Actor is { Length: > 0 } kept ? kept : RoleWork.Build;
            if (snapshot.Handed.TryGetValue(quest.Id, out var handed) && string.Equals(handed.From, last.Session, StringComparison.Ordinal)) continue;
            if (staff.Policy(role).Applied is not { } policy || !policy.Moves.TryGetValue(StaffConditions.Transient, out _)) continue;

            written ??= staff.Moves.Moving();
            if (written.TryGetValue(quest.Id, out var done) && done.Session == last.Session)
            {
                moving[quest.Id] = done;
                continue;
            }

            var moved = staff.Moves.Of(quest.Id);
            var action = StaffJudge.OnCondition(policy, StaffConditions.Transient, new StaffLine(true, moved.Moves));
            var fact = new CoursePolicy(action, now, Met: ChoiceOf(last)?.Said, Approved: moved.Approved, Moves: moved.Moves);
            switch (DrivenCourse.PastBound(last, quest.Status, released: false, fact))
            {
                case LineCourse.Move:
                    var from = ChoiceOf(last)?.Text;
                    if (staff.Moves.Decided(
                            quest.Id, role, last.Session, StaffConditions.Transient, action.Do, from,
                            read => !StaffJudge.OnCondition(policy, StaffConditions.Transient, new StaffLine(true, read.Moves)).MovesBound))
                    {
                        if (last.Adapter is { } agent) staff.MarkNotAnswering(agent);
                        moving[quest.Id] = new StaffMoving(last.Session, StaffConditions.Transient, from);
                    }
                    else
                    {
                        holds[quest.Id] = HoldMovesBound;
                    }

                    break;
                case LineCourse.Hold { Why: CourseHoldWhy.PolicyAsk or CourseHoldWhy.MovesBound } hold:
                    holds[quest.Id] = hold.Why == CourseHoldWhy.MovesBound ? HoldMovesBound : HoldAsk;
                    break;
            }
        }

        return (moving, holds);
    }

    /// <summary>
    /// Whether a driven line an account's limit cut off waits for the choice's own reset rather than walking the role's choices: its role's
    /// policy says <c>wait</c> for a limit, and the <c>upTo</c> minutes after the cut-off it names have not passed. D206's own resume is
    /// asked first either way, and the accounts of the choice's agent are still walked inside the choice (D197).
    /// </summary>
    internal static LineCourse? LimitWait(StaffLedger? staff, string role, PriorSession cut, DateTimeOffset now)
    {
        if (staff?.Policy(role).Applied is not { } policy || !policy.Moves.TryGetValue(StaffConditions.Limit, out var move) || move.Do != StaffActions.Wait) return null;
        var action = StaffJudge.OnCondition(policy, StaffConditions.Limit, new StaffLine(true, 0));
        return CourseDecision.Decide(
            new CourseFacts(
                DrivenCourse.HeadOf(cut, "Taken") with { Protocol = true }, CourseBoundary.ProcessExited,
                Policy: new CoursePolicy(action, now, Until: now.AddYears(1), Since: cut.Updated ?? now)),
            CourseTrigger.LimitEnd);
    }

    /// <summary>
    /// A limit's walk took <paramref name="taken"/>, another choice than the record <paramref name="cut"/> ran: the move is counted against
    /// the line's bound, and one approval standing is spent by it. Nothing is written for a role whose policy sets no move on a limit.
    /// </summary>
    internal static void LimitMoved(StaffLedger? staff, string role, string line, PriorSession cut, ActorEffective? taken)
    {
        if (staff is null || taken is null || ChoiceOf(cut) is not { } was || was == StaffKey.Of(taken)) return;
        if (staff.Policy(role).Applied is not { } policy || !policy.Moves.TryGetValue(StaffConditions.Limit, out var move)) return;
        staff.Moves.Record(line, role, cut.Session, StaffConditions.Limit, move.Do, was.Text);
    }

    /// <summary>
    /// The quests an account's limit cut off whose role's policy holds them for the person's yes (STAFFPOLICY1e): an <c>ask</c> on a limit, or the
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
            var role = last.Actor is { Length: > 0 } kept ? kept : RoleWork.Build;
            if (staff.Policy(role).Applied is not { } policy || !policy.Moves.ContainsKey(StaffConditions.Limit)) continue;
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
