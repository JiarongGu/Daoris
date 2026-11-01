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

    /// <summary>The typed hold a policy's <c>ask</c> leaves (<c>held.policy-ask</c>).</summary>
    internal const string HoldAsk = "policy-ask";

    /// <summary>The typed hold the policy's bound of moves leaves (<c>held.moves-bound</c>).</summary>
    internal const string HoldMovesBound = "moves-bound";
}
