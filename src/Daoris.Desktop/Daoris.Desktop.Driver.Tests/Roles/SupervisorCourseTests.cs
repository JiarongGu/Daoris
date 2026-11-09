using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The answers-alone rule's <c>refer</c>: at its count's point the line is
/// held as the rule's hold holds it, and the agent supervisor, where a terminal holds it for the line's repository, decides its course: a
/// go-on held for the role's window and then released through the count's own door between turns, or the point left to the person. With
/// no holder the point waits for the person and says so.
/// </summary>
public sealed class SupervisorCourseTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-course", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-07T09:00:00Z");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private GoOnMarks Marks => new(_home);

    private ContinuationAdmissions Admissions => new(_home, () => StoreSettled.Yes);

    private static SupervisorRuleBook Book(string does) =>
        new([new SupervisorRuleSet(SupervisorRules.Machine, 1, [new SupervisorRule("aa", SupervisorRules.AnswersAlone, "in-a-row", 2, does, null, SupervisorRules.Machine, null)])]);

    private AnswersAloneHold Hold(SupervisorRuleBook book, Func<string?, bool>? referable = null) =>
        new(Events, Marks, Admissions, book, new SupervisorRuleActs(_home, Events), referable: referable);

    private ReferredPoints Points(SupervisorRuleBook book) => new(_home, Events, Marks, book);

    private sealed class Admits(string? generation, string? why = null) : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) =>
            generation is null ? DecisionAdmitted.Refused(why ?? SupervisorWhy.NotStaffed) : new(generation, null) { Window = TimeSpan.FromMinutes(1) };
    }

    private void GoneOn(string session, int times)
    {
        var events = Events;
        events.Append(session, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "The quest's instruction." });
        events.Append(session, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        for (var each = 0; each < times; each++)
        {
            events.Append(session, new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = "h1", Id = $"{session}-a{each}", Text = "Answered." });
            events.Append(session, new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        }
    }

    private static readonly SaidWordView Answer = new("w9", "Another agent answered.", At, [], true) { Helper = "h1" };

    private static readonly PriorSession Asker = new("s1", "trees/default/reports/s-1", "completed") { Said = [Answer], Adapter = "claude-code-acp", Repository = "reports" };

    private static readonly PriorSession HelperRecord = new("h1", null, "completed") { Adapter = "codex-acp", Serves = "s1", Kind = "chat" };

    private static Dictionary<string, PriorSession> Records => new(StringComparer.Ordinal) { ["s1"] = Asker, ["h1"] = HelperRecord };

    private int Notes(string code) =>
        Events.After("s1", 0).Events.Count(e => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true);

    private static SupervisorCourse Course(string point, string does = SupervisorCourseActs.GoOn, string generation = "g-1", string source = "the helper's answer w9") =>
        new("s1", point, generation, does, [new SupervisorReason("The helper's answers are on the brief's question.", source)], new SupervisorHolder(RoleHolders.Terminal, null));

    // The point held at a refer rule's count: the line marked for the person and its act kept as the rule's refer.
    private RuleActed Referred(SupervisorRuleBook book, Func<string?, bool>? referable = null)
    {
        GoneOn("s1", 2);
        Assert.True(Hold(book, referable).Holds(Asker, Records, At));
        return Points(book).Open("s1")!;
    }

    [Fact]
    public void A_refer_rules_count_reaching_n_holds_the_line_behind_its_door_and_keeps_its_act_as_refer()
    {
        var act = Referred(Book(SupervisorRules.Refer), _ => true);

        Assert.Equal((SupervisorRules.AnswersAlone, SupervisorRules.Refer, 2), (act.Rule.Count, act.Rule.Do, act.N));
        Assert.Equal(GoOnMarks.AnswersAlone, Marks.Read("s1")!.Why);
        Assert.Equal(1, Notes(HeldNoteCodes.SupervisorRule.Code));
        Assert.Equal(0, Notes(HeldNoteCodes.SupervisorUnreferred.Code));
        Assert.Null(Points(Book(SupervisorRules.Hold)).Open("s2"));
    }

    [Fact]
    public async Task The_supervisors_go_on_is_kept_held_for_its_window_then_released_through_the_counts_door()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var point = Points(book).PointOf(Asker, null);

        var kept = await decisions.CourseAsync(Course(act.Id), point, At);

        Assert.True(kept.Kept, kept.Why);
        Assert.Equal(At.AddMinutes(1), kept.GoesOnAt);
        var file = File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + QuestionSupervisorDecisions.DecisionsSuffix), kept.Decision + ".json"));
        Assert.Contains("\"course\"", file);
        Assert.DoesNotContain("\"question\"", file);
        Assert.Empty(new SupervisorRuleActs(_home).Of("s1").Where(each => each.Id == kept.Decision));

        var line = Points(book).Line(_ => Asker, _ => false);
        Assert.Empty(await decisions.ReleaseCoursesAsync(line, At.AddSeconds(30)));
        Assert.Equal(GoOnMarks.AnswersAlone, Marks.Read("s1")?.Why);

        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseCoursesAsync(line, At.AddMinutes(2)));
        Assert.Null(Marks.Read("s1"));
        Assert.Equal(1, Notes(HeldNoteCodes.AnswersSupervised.Code));
        Assert.Equal(0, Notes(HeldNoteCodes.AnswersPassed.Code));
        // The count's door resets it, as the person's press does: the same answer now goes on.
        Assert.False(Hold(book, _ => true).Holds(Asker, Records, At.AddMinutes(3)));
    }

    [Fact]
    public async Task The_supervisor_answers_within_its_bounds_alone()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var point = Points(book).PointOf(Asker, null);

        Assert.Equal(SupervisorWhy.Form, (await decisions.CourseAsync(Course(act.Id, SupervisorRules.Next), point, At)).Why);
        Assert.Equal(SupervisorWhy.Form, (await decisions.CourseAsync(Course(act.Id, "stop"), point, At)).Why);
        Assert.Equal(SupervisorWhy.NoSource, (await decisions.CourseAsync(Course(act.Id, source: " "), point, At)).Why);
        Assert.Equal(SupervisorWhy.NoSource, (await decisions.CourseAsync(Course(act.Id, source: "count:answers-alone=2 (exact)"), point, At)).Why);
        Assert.Equal(SupervisorWhy.NotReferred, (await decisions.CourseAsync(Course("rule-elsewhere"), point, At)).Why);
        Assert.Equal(SupervisorWhy.RoleChanged, (await decisions.CourseAsync(Course(act.Id, generation: "g-0"), point, At)).Why);

        var left = await decisions.CourseAsync(Course(act.Id, SupervisorCourseActs.Hold), point, At);
        Assert.True(left.Kept, left.Why);
        Assert.Null(left.GoesOnAt);
        Assert.Equal(SupervisorWhy.Answered, (await decisions.CourseAsync(Course(act.Id), point, At)).Why);
        Assert.Empty(await decisions.ReleaseCoursesAsync(Points(book).Line(_ => Asker, _ => false), At.AddHours(1)));
        Assert.Equal(GoOnMarks.AnswersAlone, Marks.Read("s1")?.Why);
    }

    [Fact]
    public async Task A_running_session_is_never_ended_and_its_go_on_waits_for_the_turn_to_end()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        await decisions.CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At);

        Assert.Empty(await decisions.ReleaseCoursesAsync(Points(book).Line(_ => Asker, _ => true), At.AddMinutes(5)));
        Assert.Equal(GoOnMarks.AnswersAlone, Marks.Read("s1")?.Why);
        Assert.Equal(SupervisorWhy.Form, (await decisions.CourseAsync(Course(act.Id, "end"), Points(book).PointOf(Asker, null), At)).Why);

        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseCoursesAsync(Points(book).Line(_ => Asker, _ => false), At.AddMinutes(6)));
    }

    [Fact]
    public async Task A_go_on_a_rule_would_now_hold_is_moved_stale_at_its_release_and_the_owners_overrule_takes_one_back()
    {
        var act = Referred(Book(SupervisorRules.Refer), _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var kept = await decisions.CourseAsync(Course(act.Id), Points(Book(SupervisorRules.Refer)).PointOf(Asker, null), At);

        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseCoursesAsync(Points(Book(SupervisorRules.Hold)).Line(_ => Asker, _ => false), At.AddMinutes(2)));
        Assert.Equal(GoOnMarks.AnswersAlone, Marks.Read("s1")?.Why);
        Assert.Contains("rule-holds", File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + QuestionSupervisorDecisions.DecisionsSuffix), kept.Decision + ".states")));
        Assert.True(decisions.Closed(SupervisorSubject.Point("s1", act.Id, SupervisorKinds.Course)));
        Assert.Equal(SupervisorWhy.Overruled, (await decisions.CourseAsync(Course(act.Id), Points(Book(SupervisorRules.Refer)).PointOf(Asker, null), At.AddMinutes(3))).Why);
    }

    [Fact]
    public async Task The_owners_overrule_inside_the_window_keeps_the_point_theirs()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var kept = await decisions.CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At);

        Assert.Equal(SupervisorState.Overruled, await decisions.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddSeconds(10)));
        Assert.Empty(await decisions.ReleaseCoursesAsync(Points(book).Line(_ => Asker, _ => false), At.AddMinutes(2)));
        Assert.Equal(GoOnMarks.AnswersAlone, Marks.Read("s1")?.Why);
        Assert.True(decisions.Closed(SupervisorSubject.Point("s1", act.Id, SupervisorKinds.Course)));
        Assert.Equal(SupervisorWhy.Overruled, (await decisions.CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At.AddMinutes(3))).Why);
    }

    /// <summary>
    /// A course decision reads back for the page and the print: the point's rule, its count and value, the act, who held it and when, and
    /// where it stands, live from memory and, after a restart, from its files. The questions' history passes it by.
    /// </summary>
    [Fact]
    public async Task A_course_decision_reads_back_with_its_rule_count_and_state_live_and_after_a_restart()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var kept = await decisions.CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At);

        var held = Assert.Single(decisions.CoursesOf("s1"));
        Assert.Equal((kept.Decision, act.Id, SupervisorRules.AnswersAlone, 2, "aa", SupervisorRules.Machine, SupervisorCourseActs.GoOn),
            (held.Id, held.Point, held.Count, held.N, held.Rule, held.Place, held.Do));
        Assert.Equal((SupervisorState.Held, false, RoleHolders.Terminal, At, At.AddMinutes(1)), (held.State, held.Earlier, held.Holder.Kind, held.KeptAt, held.GoesOnAt));
        Assert.Empty(decisions.History("s1"));
        Assert.Empty(decisions.CoursesOf("s2"));

        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseCoursesAsync(Points(book).Line(_ => Asker, _ => false), At.AddMinutes(2)));
        var gone = Assert.Single(decisions.CoursesOf("s1"));
        Assert.Equal((SupervisorState.WentOn, At.AddMinutes(2), (string?)null), (gone.State, gone.StateAt, gone.Why));

        var after = Assert.Single(new QuestionSupervisorDecisions(_home, new Admits("g-1")).CoursesOf("s1"));
        Assert.Equal((kept.Decision, act.Id, SupervisorState.WentOn, true, "aa", 2), (after.Id, after.Point, after.State, after.Earlier, after.Rule, after.N));
    }

    [Fact]
    public async Task A_course_decision_overruled_or_stale_says_where_it_stands_and_why()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var kept = await decisions.CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At);
        await decisions.ReleaseCoursesAsync(Points(Book(SupervisorRules.Hold)).Line(_ => Asker, _ => false), At.AddMinutes(2));
        var stale = Assert.Single(decisions.CoursesOf("s1"));
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.RuleHolds), (stale.State, stale.Why));

        var again = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.RuleHolds, true),
            (again.CoursesOf("s1").Single().State, again.CoursesOf("s1").Single().Why, again.CoursesOf("s1").Single().Earlier));
        Assert.Equal(kept.Decision, again.CoursesOf("s1").Single().Id);
    }

    [Fact]
    public async Task The_owners_overrule_is_read_back_as_overruled_at_its_moment()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var kept = await decisions.CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At);

        await decisions.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddSeconds(10));

        var read = Assert.Single(decisions.CoursesOf("s1"));
        Assert.Equal((SupervisorState.Overruled, At.AddSeconds(10)), (read.State, read.StateAt));
    }

    [Fact]
    public async Task A_hold_is_read_back_as_left_to_the_owner_with_no_moment_it_goes_on()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        await decisions.CourseAsync(Course(act.Id, SupervisorCourseActs.Hold), Points(book).PointOf(Asker, null), At);

        var read = Assert.Single(decisions.CoursesOf("s1"));
        Assert.Equal((SupervisorCourseActs.Hold, SupervisorState.Left), (read.Do, read.State));
    }

    [Fact]
    public async Task Off_with_no_holder_the_point_waits_for_the_person_and_says_so_and_on_a_started_terminal_takes_it()
    {
        var act = Referred(Book(SupervisorRules.Refer), _ => false);
        Assert.Equal(1, Notes(HeldNoteCodes.SupervisorUnreferred.Code));
        var off = new QuestionSupervisorDecisions(_home, NoDecisionAdmission.Instance);
        Assert.False(off.Admits("reports"));
        Assert.Equal(SupervisorWhy.NotStaffed, (await off.CourseAsync(Course(act.Id), Points(Book(SupervisorRules.Refer)).PointOf(Asker, null), At)).Why);
        Assert.Equal(SupervisorWhy.NotStarted, (await new QuestionSupervisorDecisions(_home, new Admits(null, SupervisorWhy.NotStarted))
            .CourseAsync(Course(act.Id), Points(Book(SupervisorRules.Refer)).PointOf(Asker, null), At)).Why);

        var on = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        Assert.True(on.Admits("reports"));
        Assert.True((await on.CourseAsync(Course(act.Id), Points(Book(SupervisorRules.Refer)).PointOf(Asker, null), At)).Kept);
    }

    [Fact]
    public async Task One_point_is_referred_once_while_a_question_decision_on_the_line_is_open()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        Assert.True(decisions.Refers("s1", act.Id));

        await decisions.CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At);
        Assert.False(decisions.Refers("s1", act.Id));
    }

    [Fact]
    public async Task A_decide_naming_an_earlier_point_of_the_line_is_refused_answered_and_one_it_never_reached_not_referred()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var now = Points(book).PointOf(Asker, null);
        Assert.Equal([act.Id], now.Passed);

        // The line moved on to a later point before the decide arrived.
        var later = act with { At = act.At.AddMinutes(5) };
        var moved = now with { Open = later, Passed = [act.Id, later.Id] };
        Assert.Equal(SupervisorWhy.Answered, (await decisions.CourseAsync(Course(act.Id), moved, At)).Why);
        Assert.Equal(SupervisorWhy.NotReferred, (await decisions.CourseAsync(Course("rule-elsewhere"), moved, At)).Why);
        Assert.True((await decisions.CourseAsync(Course(later.Id), moved, At)).Kept);
    }

    [Fact]
    public async Task A_course_decision_overruled_before_a_restart_leaves_its_point_the_persons_after_it()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var before = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var kept = await before.CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At);
        Assert.Equal(SupervisorState.Overruled, await before.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddSeconds(10)));
        var file = File.ReadAllText(Path.Combine(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1" + QuestionSupervisorDecisions.DecisionsSuffix), kept.Decision + ".json"));
        Assert.Contains("\"subject\"", file);

        var after = new QuestionSupervisorDecisions(_home, new Admits("g-2"));
        Assert.True(after.Refers("s1", act.Id));
        after.SweepAtStart(At.AddMinutes(1));

        Assert.False(after.Refers("s1", act.Id));
        Assert.Equal(SupervisorWhy.Overruled, (await after.CourseAsync(Course(act.Id, generation: "g-2"), Points(book).PointOf(Asker, null), At.AddMinutes(2))).Why);
    }

    [Fact]
    public async Task A_held_go_on_whose_overrule_line_was_not_written_is_closed_as_the_persons_at_the_next_start()
    {
        var book = Book(SupervisorRules.Refer);
        var act = Referred(book, _ => true);
        var kept = await new QuestionSupervisorDecisions(_home, new Admits("g-1")).CourseAsync(Course(act.Id), Points(book).PointOf(Asker, null), At);
        Assert.True(kept.Kept, kept.Why);

        // The overrule's line never reached the file: the decision reads held, and the start closes it stale as the person's.
        var after = new QuestionSupervisorDecisions(_home, new Admits("g-2"));
        after.SweepAtStart(At.AddMinutes(1));

        Assert.False(after.Refers("s1", act.Id));
        var read = Assert.Single(after.CoursesOf("s1"));
        Assert.Equal((SupervisorState.Stale, SupervisorWhy.Restarted), (read.State, read.Why));
        Assert.Empty(await after.ReleaseCoursesAsync(Points(book).Line(_ => Asker, _ => false), At.AddHours(1)));
    }
}
