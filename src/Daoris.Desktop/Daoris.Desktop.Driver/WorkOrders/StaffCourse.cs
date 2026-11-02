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

    /// <summary>Whether a record ended on the watcher's quiet end (<see cref="NoteCodes.EndedQuiet"/>), by its code: failed, and the quiet policy's, never the failed one's.</summary>
    internal static bool EndedQuiet(PriorSession record) =>
        string.Equals(record.State, "failed", StringComparison.OrdinalIgnoreCase) && record.NoteParts?.Any(part => part.Code == RecordNotes.EndedQuiet) == true;

    /// <summary>Whether a record is a failure that counts a strike and is no other condition's: not a limit, a refused account, a choice not offered, a service that did not answer or a quiet end.</summary>
    internal static bool FailedStrike(PriorSession record) =>
        string.Equals(record.State, "failed", StringComparison.OrdinalIgnoreCase)
        && !record.Limit && !record.NotOffered && !Planner.EndedTransient(record) && !EndedQuiet(record)
        && record.NoteParts?.Any(part => part.Code is { } code && (code == RecordNotes.UpdateClosed || RecordNotes.AccountsOwn.Contains(code))) != true;

    /// <summary>The action the policy names for <paramref name="condition"/>, else the walk's own <c>next</c>.</summary>
    internal static string ActionOf(StaffPolicy? policy, string condition) =>
        policy is not null && policy.Moves.TryGetValue(condition, out var move) ? move.Do : ActorMoves.Next;

    /// <summary>
    /// Why a part moved, as the opening note says it (design §5): the condition, and the role's moves in 24 hours against its bound.
    /// </summary>
    internal static string MovedSaid(string condition, StaffPolicy? policy, int moves, string roleName)
    {
        var why = condition switch
        {
            StaffConditions.Transient => "its service did not answer",
            StaffConditions.Failed => "it failed again",
            StaffConditions.Quiet => "it stopped after a quiet spell",
            StaffConditions.Budget => "it was over its budget",
            _ => $"the {condition} condition was met",
        };
        var count = policy?.Bound is { } most ? $"{moves} of {most} moves today" : $"{moves} move{(moves == 1 ? "" : "s")} today";
        return $"{why}. {roleName}'s policy moves on that; {count}";
    }

    /// <summary>
    /// The role a record's line goes on under, whose policy the course reads: the role its start was bound to (<c>binding_roles</c>) while the
    /// catalogue holds it, else the role taking the quest's work at its place now. The record's <c>actor</c> is the seeded role's id on every
    /// driven record, so a scoped added role's policy is never read from it.
    /// </summary>
    internal static string RoleOf(StaffLedger staff, Snapshot snapshot, QuestView quest, PriorSession record) =>
        RoleOf(staff, snapshot.Repositories.FirstOrDefault(repo => string.Equals(repo.Repository, quest.To, StringComparison.OrdinalIgnoreCase))?.Workspace, quest, record);

    /// <inheritdoc cref="RoleOf(StaffLedger, Snapshot, QuestView, PriorSession)"/>
    /// <param name="workspace">The circle the quest's repository is in; null for none.</param>
    /// <param name="record">The record the line goes on from; null for a first start, which takes the role of the work at its place.</param>
    internal static string RoleOf(StaffLedger staff, string? workspace, QuestView quest, PriorSession? record)
    {
        try
        {
            var bound = record is null ? null : new BindingStore(staff.Home, null, _ => null).RoleOf(record.Session)?.Role;
            var work = string.Equals(quest.Weight, ParticipantWork.Design, StringComparison.Ordinal) ? ParticipantWork.Design : ParticipantWork.Build;
            return RoleOrders.Bound(bound, work, RoleCatalog.ReadHome(staff.Home), new RolePlace(workspace, quest.To));
        }
        catch (Exception error) when (error is DriverException or DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            return record?.Actor is { Length: > 0 } kept ? kept : RoleWork.Build;
        }
    }

    /// <summary>The choice a record ran, as the policy keys it.</summary>
    internal static StaffKey? ChoiceOf(PriorSession record, StaffPolicy? policy) =>
        record.Asked is null && policy?.Choices.FirstOrDefault(each => string.Equals(each.Key.Agent, record.Adapter, StringComparison.OrdinalIgnoreCase)) is { } listed
            ? listed.Key
            : ChoiceOf(record);

    /// <summary>The choice a record ran, as the policy keys it.</summary>
    internal static StaffKey? ChoiceOf(PriorSession record) =>
        record.Adapter is { Length: > 0 } agent ? new StaffKey(agent, record.Asked?.NamedModel, record.Asked?.Effort.Value) : null;

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
            var role = RoleOf(staff, snapshot, quest, last);
            if (snapshot.Handed.TryGetValue(quest.Id, out var handed) && string.Equals(handed.From, last.Session, StringComparison.Ordinal)) continue;
            if (staff.Policy(role).Applied is not { } policy || !policy.Moves.TryGetValue(condition, out _)) continue;

            // The strikes park a quest as they always did: a parked line is the person's, and a move would only be counted against it.
            var strikes = snapshot.Strikes.GetValueOrDefault(quest.Id) - config.ForgivenAt(quest.Id);
            if (condition != StaffConditions.Transient && config.Strikes > 0 && strikes >= config.Strikes) continue;

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
            var fact = new CoursePolicy(action, now, Met: ChoiceOf(last, policy)?.Said, Approved: moved.Approved, Moves: moved.Moves);
            var course = condition == StaffConditions.Transient
                ? DrivenCourse.PastBound(last, quest.Status, released: false, fact)
                : DrivenCourse.PolicyEnd(last, quest.Status, fact);
            switch (course)
            {
                // A read of the holds alone (a plan nobody looked for) decides nothing and writes no move.
                case LineCourse.Move when !write:
                    break;
                case LineCourse.Move:
                    var from = ChoiceOf(last, policy)?.Text;
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
        var (_, holds) = Look(staff, snapshot, config, now, write: false);
        var limit = LimitHolds(staff, snapshot);
        return holds.Count == 0 && limit.Count == 0
            ? snapshot
            : snapshot with { PolicyHolds = holds.Concat(limit).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase) };
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
            var role = RoleOf(staff, snapshot, quest, last);
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
