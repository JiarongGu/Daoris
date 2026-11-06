namespace Daoris.Driver;

/// <summary>
/// A conversation's line as the supervisor's rules read it (CONTEXTDRIVE1n; the context counters design §4.5): the tries its transient episode
/// makes, what follows past them, what its account's limit does, and the bound of its moves, each from the line's effective rules for its role
/// at its place, as a driven line's are (<see cref="StaffCourse"/>). The course decides over the fact this reads; a rule's act is said, kept
/// and logged (<see cref="SupervisorRuleActs"/>).
/// </summary>
internal sealed class ConversationRules(StaffLedger? staff, SupervisorRuleActs? acts = null, TimeProvider? clock = null)
{
    /// <summary>The rules of <paramref name="context"/>'s home, each act kept in its decision record.</summary>
    public static ConversationRules Of(ChatContext context) =>
        new(Staff(context), new SupervisorRuleActs(context.Home, context.Events), context.Harnesses.Clock is { } read ? new ReadClock(read) : null);

    /// <summary>The tries a conversation's transient episode makes: the lowest <c>at</c> of its line's effective tries rules, else D210's five.</summary>
    public int Tries(string role, string? repository, string? workspace, string line) =>
        StaffCourse.Tries(staff, role, new StaffPlace(repository, workspace, line));

    /// <summary>
    /// What the rules make of <paramref name="record"/>'s line at <paramref name="condition"/> (<see cref="StaffConditions.Transient"/> past its
    /// episode, or <see cref="StaffConditions.Limit"/>): the course's answer and the action it read, or null where no rule on the count moves,
    /// waits or asks, and the conversation ends its turn or walks as it always did.
    /// </summary>
    /// <param name="until">For a limit, the moment its account is back, which a <c>wait</c> waits for.</param>
    public ConversationRuled? Judge(PriorSession record, string line, string condition, DateTimeOffset? until = null)
    {
        if (staff is null) return null;
        var ruled = RuledLine.Of(staff.Home, record, line);
        var (role, place) = (ruled.Role, ruled.Place);
        StaffPolicy? policy;
        StaffMoved moved;
        try
        {
            policy = StaffCourse.CourseOf(staff, role, place);
            moved = staff.Moves.Of(line);
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (policy is null || !policy.Moves.ContainsKey(condition)) return null;
        var action = StaffJudge.OnCondition(policy, condition, new StaffLine(true, moved.Moves));
        // A limit's move to the next account or choice is the limit's own walk: the rule reads only its wait, its ask and the bound.
        if (condition == StaffConditions.Limit && StaffActions.Moves(action.Do) && !action.MovesBound) return null;
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var fact = new CoursePolicy(
            action, now, Met: StaffCourse.ChoiceOf(record, policy)?.Said, Until: until, Since: now, Approved: moved.Approved, Moves: moved.Moves);
        var head = new CourseHead(
            record.State, Limited: condition == StaffConditions.Limit, Role: role, Choice: record.Choice, Adapter: record.Adapter, Profile: record.Profile,
            Tree: record.Tree, Conversation: true, AskDaoris: record.HelpMode is not null, Session: record.Session, Protocol: true);
        var course = CourseDecision.Decide(
            new CourseFacts(head, CourseBoundary.BetweenTurns, Policy: fact),
            condition == StaffConditions.Limit ? CourseTrigger.LimitEnd : CourseTrigger.TransientPastBound);
        return new ConversationRuled(role, place, policy, action, course, moved);
    }

    /// <summary>
    /// The line's choices other than the one <paramref name="record"/> ran, in the order its role's rule walks them: where a move by the rules
    /// goes. None where the role holds one choice or its staffing does not read.
    /// </summary>
    public IReadOnlyList<ActorEffective> Others(PriorSession record, string role, DriverConfig config)
    {
        try
        {
            var ran = StaffCourse.ChoiceOf(record);
            return [.. RoleOrders.Arranged(staff, role, RoleWork.Chat, config).Where(entry => ran is null || StaffKey.Of(entry) != ran)];
        }
        catch (Exception error) when (error is ActorUnreadableException or DriverException or DesktopStoreException or IOException)
        {
            return [];
        }
    }

    /// <summary>
    /// A move by the rules counted on the line, against its bound: false where the bound refused it or the store did not take it, and nothing
    /// moves.
    /// </summary>
    public bool Counted(ConversationRuled ruled, string session, string condition, string? from) =>
        staff is not null
        && staff.Moves.Decided(
            ruled.Place.Line!, ruled.Role, session, condition, ruled.Action.Do, from,
            read => !StaffJudge.OnCondition(ruled.Policy, condition, new StaffLine(true, read.Moves)).MovesBound);

    /// <summary>
    /// A rule's act on the line, said on its session and kept as its decision: the rule on <paramref name="count"/> that acts by
    /// <paramref name="does"/>, with what the count stood at. Nothing is kept where no rule names the act (the seeded hold's end is today's).
    /// </summary>
    public void Said(ConversationRuled ruled, string session, string count, string does, int n, string point)
    {
        if (staff is null || acts is null) return;
        try
        {
            var book = staff.Rules();
            var effective = book.For(ruled.Role, ruled.Place.Repository, ruled.Place.Workspace, ruled.Place.Line);
            if (SupervisorRuleBook.Acting(effective, count, does) is not { } rule) return;
            acts.Record(new RuleActed(ruled.Place.Line!, session, rule, book.RevisionOf(rule.Place), n, true, point, (clock ?? TimeProvider.System).GetUtcNow()));
        }
        catch (Exception error) when (error is DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            // The act stands where its record could not be read or kept: the line's own events still say what followed.
        }
    }

    private static StaffLedger? Staff(ChatContext context)
    {
        try
        {
            return context.Harnesses.StaffFor(context.Home);
        }
        catch (Exception error) when (error is DesktopStoreException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // The roster's clock as a time provider, so a held roster clock holds the rules' moments too.
    private sealed class ReadClock(Func<DateTimeOffset> read) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => read();
    }
}

/// <summary>
/// What the rules made of a conversation's line at one condition (CONTEXTDRIVE1n): its role and place, the policy the rules make, the action
/// read, the course's answer, and its moves in the window.
/// </summary>
internal sealed record ConversationRuled(string Role, StaffPlace Place, StaffPolicy Policy, StaffAction Action, LineCourse Course, StaffMoved Moved);
