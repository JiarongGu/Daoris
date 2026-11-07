using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>refer</c> on the counts only a driven quest has (CONTEXTDRIVE1j; the context counters design §4.6 point 3): failed sessions since
/// <i>Try again</i> and failures in a row on one choice. The line is held as <c>hold</c> parks it and <c>ask</c> asks, and the supervisor's
/// <c>go-on</c> is the quest's own <i>Try again</i> between sessions, through the same door the person's press is. The counts a
/// conversation shares (tries, limits, moves) and the quiet end do not offer it.
/// </summary>
public sealed class SupervisorQuestCourseTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-quest-course", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-07T09:00:00Z");

    private static readonly QuestView Quest = new("q1", "ask #a1", "engine", "The work", "", "Taken");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static PriorSession Last => new("s1", "trees/default/engine/s-1", "failed")
    {
        Quest = "q1", Actor = "build", Adapter = "acp-stub", Said = [], Updated = At.AddMinutes(-1), Repository = "engine",
    };

    private static SupervisorRuleBook Book(params SupervisorRule[] more) =>
        new([new SupervisorRuleSet(SupervisorRules.Machine, 2, [.. SupervisorRuleBook.Seeded(3).Where(rule => !more.Any(other => other.Count == rule.Count)), .. more])]);

    private static SupervisorRule Rule(string count, int at, string does) =>
        new($"r-{count}", count, SupervisorRules.Windows[count], at, does, null, SupervisorRules.Machine, null);

    private static Snapshot Looking() =>
        new([Quest], [], [], new Dictionary<string, int> { ["q1"] = 3 })
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = Last },
            FailedRuns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["q1"] = 2 },
        };

    private static IReadOnlyList<RuleActed> Acts(IReadOnlyList<Consideration> plan, SupervisorRuleBook book) =>
        PlannedRuleActs.Of(Looking(), plan, book, (quest, _) => new RuledLine(RoleWork.Build, quest.To, null), _ => 0, At);

    private static Consideration Parked => new(Quest, StartVerdict.Exhausted, "parked") { Failures = 3, FailedSince = 3, ParksAt = 3, LastSession = "s1" };

    private static Consideration Asked => new(Quest, StartVerdict.PolicyHeld, "asks") { HeldBy = Last, Said = new(SittingFact.PolicyAsk) { Session = "s1" } };

    private ReferredPoints Points(SupervisorRuleBook book) =>
        new(_home, new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), new GoOnMarks(_home), book);

    private RuleActed Kept(RuleActed act)
    {
        new SupervisorRuleActs(_home).Record(act);
        return act;
    }

    [Theory]
    [InlineData(SupervisorRules.Failures, SupervisorRules.Refer, 3, true)]
    [InlineData(SupervisorRules.FailuresOnChoice, SupervisorRules.Refer, 2, true)]
    [InlineData(SupervisorRules.Tries, SupervisorRules.Refer, 5, false)]
    [InlineData(SupervisorRules.Limits, SupervisorRules.Refer, 1, false)]
    [InlineData(SupervisorRules.Moves, SupervisorRules.Refer, 3, false)]
    [InlineData(SupervisorRules.Quiet, SupervisorRules.Refer, 1, false)]
    public void Refer_is_offered_on_the_quests_own_counts_alone_and_refused_do_elsewhere(string count, string does, int at, bool offered)
    {
        var world = new SupervisorRuleWorld(["build"]);
        var held = new SupervisorRuleSet(SupervisorRules.Machine, 1, SupervisorRuleBook.Seeded(3));
        var rules = new List<SupervisorRule>(held.Rules.Where(rule => rule.Count != count)) { Rule(count, at, does) };

        var refusal = SupervisorRules.Judge(SupervisorRules.Machine, 1, rules, [held], world);

        Assert.Equal(offered, SupervisorRules.Actions(count).Contains(SupervisorRules.Refer));
        Assert.Equal(offered ? null : "do", refusal?.Code);
    }

    [Fact]
    public void A_failures_rule_that_refers_parks_the_quest_as_hold_does_and_a_failed_choice_that_refers_asks_as_ask_does()
    {
        var parking = Book(Rule(SupervisorRules.Failures, 3, SupervisorRules.Refer)).For("build", "engine", null);
        Assert.Equal(3, SupervisorRuleBook.FailuresAt(parking));

        var asking = Book(Rule(SupervisorRules.FailuresOnChoice, 2, SupervisorRules.Refer)).For("build", "engine", null);
        var course = SupervisorRuleBook.Course(null, asking);
        Assert.Equal(new StaffMove(StaffActions.Ask, 2), course!.Moves[StaffConditions.Failed]);
    }

    [Fact]
    public void A_park_and_an_ask_a_refer_rule_made_are_kept_as_its_act()
    {
        var parked = Acts([Parked], Book(Rule(SupervisorRules.Failures, 3, SupervisorRules.Refer)));
        var park = Assert.Single(parked);
        Assert.Equal((SupervisorRules.Failures, SupervisorRules.Refer, 3, "s1"), (park.Rule.Count, park.Rule.Do, park.N, park.Session));

        var asked = Acts([Asked], Book(Rule(SupervisorRules.FailuresOnChoice, 2, SupervisorRules.Refer)));
        var ask = Assert.Single(asked);
        Assert.Equal((SupervisorRules.FailuresOnChoice, SupervisorRules.Refer, 2, "q1"), (ask.Rule.Count, ask.Rule.Do, ask.N, ask.Line));

        // A rule that asks, which no one is asked of, keeps no act here, as before.
        Assert.Empty(Acts([Asked], Book(Rule(SupervisorRules.FailuresOnChoice, 2, SupervisorRules.Ask))));
    }

    [Fact]
    public void A_point_stands_while_the_plan_still_holds_the_quest_on_the_session_the_rule_acted_on()
    {
        var book = Book(Rule(SupervisorRules.Failures, 3, SupervisorRules.Refer));
        var act = Kept(Acts([Parked], book).Single());
        var points = Points(book);

        Assert.Equal(act.Id, points.Open("s1", [Parked])!.Id);
        Assert.Null(points.Open("s1", []));
        Assert.Null(points.Open("s1", [new Consideration(Quest, StartVerdict.Start, "free")]));
        Assert.Equal(SupervisorWhy.RuleHolds, Points(Book(Rule(SupervisorRules.Failures, 3, SupervisorRules.Hold))).Why(Last, "s1", act.Id, [Parked]));
        Assert.Null(points.Why(Last, "s1", act.Id, [Parked]));
        Assert.Equal(SupervisorWhy.Answered, points.Why(Last, "s1", act.Id, []));
    }

    [Fact]
    public async Task The_supervisors_go_on_on_a_parked_quest_is_its_try_again_after_the_window_and_never_while_a_session_runs()
    {
        var book = Book(Rule(SupervisorRules.Failures, 3, SupervisorRules.Refer));
        var act = Kept(Acts([Parked], book).Single());
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var points = Points(book);

        var kept = await decisions.CourseAsync(
            new SupervisorCourse("s1", act.Id, "g-1", SupervisorCourseActs.GoOn, [new SupervisorReason("Its failures were the account's.", "the quest's failed sessions in its records")], new SupervisorHolder(RoleHolders.Terminal, null)),
            points.PointOf(Last, null, [Parked]), At);
        Assert.True(kept.Kept, kept.Why);

        var tried = new List<string>();
        CourseLine Line(bool running) =>
            points.Line(_ => Last, _ => running, [Parked], quest => { tried.Add(quest); return true; });

        Assert.Empty(await decisions.ReleaseCoursesAsync(Line(false), At.AddSeconds(30), [SupervisorRules.Failures]));
        Assert.Empty(await decisions.ReleaseCoursesAsync(Line(true), At.AddMinutes(2), [SupervisorRules.Failures]));
        Assert.Empty(tried);

        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseCoursesAsync(Line(false), At.AddMinutes(2), [SupervisorRules.Failures]));
        Assert.Equal(["q1"], tried);
    }

    private int Notes(string code) =>
        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events
            .Count(e => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true);

    /// <summary>The person sees who released the quest: a note on its line when the supervisor makes its <i>Try again</i>, and none where it did not.</summary>
    [Fact]
    public async Task The_supervisors_try_again_is_said_on_the_quests_line_and_a_refused_one_says_nothing()
    {
        var book = Book(Rule(SupervisorRules.Failures, 3, SupervisorRules.Refer));
        var act = Kept(Acts([Parked], book).Single());
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var points = Points(book);
        await decisions.CourseAsync(
            new SupervisorCourse("s1", act.Id, "g-1", SupervisorCourseActs.GoOn, [new SupervisorReason("Its failures were the account's.", "the quest's failed sessions in its records")], new SupervisorHolder(RoleHolders.Terminal, null)),
            points.PointOf(Last, null, [Parked]), At);

        Assert.Equal(0, Notes(HeldNoteCodes.SupervisorRetried.Code));
        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseCoursesAsync(points.Line(_ => Last, _ => false, [Parked], _ => true), At.AddMinutes(2), [SupervisorRules.Failures]));

        Assert.Equal(1, Notes(HeldNoteCodes.SupervisorRetried.Code));
        Assert.Equal(0, Notes(HeldNoteCodes.AnswersSupervised.Code));
    }

    [Fact]
    public async Task A_try_again_the_quests_door_refused_leaves_no_note()
    {
        var book = Book(Rule(SupervisorRules.Failures, 3, SupervisorRules.Refer));
        var act = Kept(Acts([Parked], book).Single());
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var points = Points(book);
        await decisions.CourseAsync(
            new SupervisorCourse("s1", act.Id, "g-1", SupervisorCourseActs.GoOn, [new SupervisorReason("Its failures were the account's.", "the quest's failed sessions in its records")], new SupervisorHolder(RoleHolders.Terminal, null)),
            points.PointOf(Last, null, [Parked]), At);

        Assert.Equal([SupervisorState.Stale], await decisions.ReleaseCoursesAsync(points.Line(_ => Last, _ => false, [Parked], _ => false), At.AddMinutes(2), [SupervisorRules.Failures]));
        Assert.Equal(0, Notes(HeldNoteCodes.SupervisorRetried.Code));
    }

    [Fact]
    public async Task A_quests_point_is_left_to_the_answers_alone_release_and_the_quests_to_its_own()
    {
        var book = Book(Rule(SupervisorRules.Failures, 3, SupervisorRules.Refer));
        var act = Kept(Acts([Parked], book).Single());
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var points = Points(book);
        await decisions.CourseAsync(
            new SupervisorCourse("s1", act.Id, "g-1", SupervisorCourseActs.GoOn, [new SupervisorReason("Its failures were the account's.", "the quest's failed sessions in its records")], new SupervisorHolder(RoleHolders.Terminal, null)),
            points.PointOf(Last, null, [Parked]), At);

        // The look before the plan releases answers alone's points and passes a quest's, which only the plan's look can judge.
        Assert.Empty(await decisions.ReleaseCoursesAsync(points.Line(_ => Last, _ => false), At.AddMinutes(5), [SupervisorRules.AnswersAlone]));
        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseCoursesAsync(points.Line(_ => Last, _ => false, [Parked], _ => true), At.AddMinutes(5), [SupervisorRules.Failures]));
    }

    [Fact]
    public void A_refer_hold_on_a_count_that_parks_the_line_is_a_hold_the_supervisors_answer_may_not_pass()
    {
        var counts = new[]
        {
            new CountFact(SupervisorRules.Failures, 3, "exact", null, "records") { Watch = new CountWatch(3, SupervisorRules.Refer, SupervisorRules.Machine) },
        };

        Assert.True(SupervisorRuleHold.Holds(counts));
    }

    private sealed class Admits(string generation) : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string repository) => new(generation, null) { Window = TimeSpan.FromMinutes(1) };
    }
}
