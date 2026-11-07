namespace Daoris.Driver;

/// <summary>
/// What the driver reads of a role's policy to hand the course its met condition: the
/// facts are read once at the look, the course decides, and a move the course made is written down (<see cref="StaffMoves"/>) before anything
/// acts on it, so the planner, the start and a restart all read the one decision. With no policy that applies every answer is the plain one.
/// What a look reads of the planner's snapshot is <see cref="StaffSnapshotCourse"/>'s.
/// </summary>
internal static class StaffCourse
{
    /// <summary>The tries a driven run's transient episode makes: the lowest <c>at</c> of the line's effective tries rules, else five.</summary>
    internal static int Tries(StaffLedger? staff, string? role, StaffPlace? place = null) =>
        staff is null || role is null
            ? TransientRetry.Bound
            : SupervisorRuleBook.TriesLimit(staff.Rules().For(role, place?.Repository, place?.Workspace));

    /// <summary>
    /// The policy the course reads for <paramref name="role"/>'s line at <paramref name="place"/>:
    /// the applied policy's moves and bound are the line's effective rules'.
    /// </summary>
    internal static StaffPolicy? CourseOf(StaffLedger staff, string role, StaffPlace place) =>
        SupervisorRuleBook.Course(staff.Policy(role).Applied, staff.Rules().For(role, place.Repository, place.Workspace));

    /// <summary>
    /// The facts a driven quest's next start arranges by: the choices its line moved off within the hold, and the moves its rules set, which
    /// decide whether those passes apply. A store that does not answer arranges by none.
    /// </summary>
    internal static StaffFacts? LineFacts(StaffLedger staff, QuestView quest, string? workspace, string? role)
    {
        try
        {
            var facts = staff.Moves.LineFacts(quest.Id);
            var named = role ?? RuledLine.OfQuest(staff.Home, quest, null, workspace).Role;
            return facts with { Moves = CourseOf(staff, named, new StaffPlace(quest.To, workspace))?.Moves ?? new Dictionary<string, StaffMove>() };
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The policy a part's move is made under and the opening note's sentence for it, in the rule's words.</summary>
    internal static (StaffPolicy? Policy, string Said) MoveSaid(StaffLedger staff, string role, string line, PriorSession left, string condition, int moves)
    {
        var policy = CourseOf(staff, role, new StaffPlace(left.Repository, left.Workspace));
        return (policy, MovedSaid(condition, policy, moves, ActorWords.Name(role)));
    }

    /// <summary>The action the policy names for <paramref name="condition"/>, else the walk's own <c>next</c>.</summary>
    internal static string ActionOf(StaffPolicy? policy, string condition) =>
        policy is not null && policy.Moves.TryGetValue(condition, out var move) ? move.Do : ActorMoves.Next;

    /// <summary>
    /// Why a part moved, as the opening note says it: the condition, and the role's moves in 24 hours against its bound.
    /// </summary>
    internal static string MovedSaid(string condition, StaffPolicy? policy, int moves, string roleName)
    {
        var bound = policy?.Bound is { } at ? $"{moves} of {at} moves today" : $"{moves} move{(moves == 1 ? "" : "s")} today";
        return $"{Why(condition)}. Your supervisor rule for {roleName}'s lines moves on that; {bound}";
    }

    private static string Why(string condition) => condition switch
    {
        StaffConditions.Transient => "its service did not answer",
        StaffConditions.Failed => "it failed again",
        StaffConditions.Quiet => "it stopped after a quiet spell",
        StaffConditions.Budget => "it was over its budget",
        _ => $"the {condition} condition was met",
    };

    /// <summary>The choice a record ran, as the policy keys it.</summary>
    internal static StaffKey? ChoiceOf(PriorSession record, StaffPolicy? policy) =>
        record.Asked is null && policy?.Choices.FirstOrDefault(each => string.Equals(each.Key.Agent, record.Adapter, StringComparison.OrdinalIgnoreCase)) is { } listed
            ? listed.Key
            : ChoiceOf(record);

    /// <summary>The choice a record ran, as the policy keys it.</summary>
    internal static StaffKey? ChoiceOf(PriorSession record) =>
        record.Adapter is { Length: > 0 } agent ? new StaffKey(agent, record.Asked?.NamedModel, record.Asked?.Effort.Value) : null;

    /// <summary>
    /// Whether a driven line an account's limit cut off waits for the choice's own reset rather than walking the role's choices: its role's
    /// policy says <c>wait</c> for a limit, and the <c>upTo</c> minutes after the cut-off it names have not passed. The own account's resume is
    /// asked first either way, and the accounts of the choice's agent are still walked inside the choice.
    /// </summary>
    internal static LineCourse? LimitWait(StaffLedger? staff, string role, PriorSession cut, DateTimeOffset now, StaffPlace? place = null)
    {
        if (staff is null || CourseOf(staff, role, place ?? new(cut.Repository, cut.Workspace)) is not { } policy
            || !policy.Moves.TryGetValue(StaffConditions.Limit, out var move) || move.Do != StaffActions.Wait) return null;
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
        if (CourseOf(staff, role, new(cut.Repository, cut.Workspace)) is not { } policy || !policy.Moves.TryGetValue(StaffConditions.Limit, out var move)) return;
        staff.Moves.Record(line, role, cut.Session, StaffConditions.Limit, move.Do, was.Text);
    }
}

/// <summary>A line as the supervisor's rules place it: its repository and its workspace where known.</summary>
internal sealed record StaffPlace(string? Repository, string? Workspace);
