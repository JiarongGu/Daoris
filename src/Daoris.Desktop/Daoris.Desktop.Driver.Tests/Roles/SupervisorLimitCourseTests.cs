using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>refer</c> on a driven line's limits and moves, and its failures on one choice: the line asks as <c>ask</c> does, and the limit
/// supervisor's <c>move</c> is the quest's one approval, its <c>wait</c> the part's wait for its choice's reset to the rule's minutes, each
/// held for the role's window with <i>Overrule</i>, and a leave the person's at once. A conversation's line keeps the person's word, and
/// a line the person stopped or paused is never the supervisor's to release.
/// </summary>
public sealed class SupervisorLimitCourseTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-limit-course", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-08T09:00:00Z");

    private static readonly QuestView Quest = new("q1", "ask #a1", "engine", "The work", "", "Taken");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private static PriorSession Limited => new("s1", "trees/default/engine/s-1", "failed")
    {
        Quest = "q1", Actor = "build", Adapter = "acp-stub", Said = [], Updated = At.AddMinutes(-1), Repository = "engine", Limit = true,
    };

    private static Consideration Asked => new(Quest, StartVerdict.PolicyHeld, "asks") { HeldBy = Limited, Said = new(SittingFact.PolicyAsk) { Session = "s1" } };

    private static Consideration Bounded => new(Quest, StartVerdict.PolicyHeld, "bound") { HeldBy = Limited, Said = new(SittingFact.MovesBound) { Session = "s1" } };

    private static SupervisorRule Refer(string count, int at, int? upTo = null, IReadOnlyList<string>? roles = null) =>
        new($"r-{count}", count, SupervisorRules.Windows[count], at, SupervisorRules.Refer, upTo, SupervisorRules.Machine, roles ?? ["build"]);

    private static IReadOnlyList<SupervisorRule> Seeded(params SupervisorRule[] more) =>
        [.. SupervisorRuleBook.Seeded(3), .. more];

    private static SupervisorRuleBook Book(params SupervisorRule[] more) => new([new SupervisorRuleSet(SupervisorRules.Machine, 2, Seeded(more))]);

    private static IReadOnlyList<RuleActed> Acts(IReadOnlyList<Consideration> plan, SupervisorRuleBook book) =>
        PlannedRuleActs.Of(
            new Snapshot([Quest], [], [], new Dictionary<string, int>())
            {
                LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = Limited },
            },
            plan, book, (quest, _) => new RuledLine(RoleWork.Build, quest.To, null), _ => 3, At);

    private ReferredPoints Points(SupervisorRuleBook book) =>
        new(_home, new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), new GoOnMarks(_home), book);

    private RuleActed Kept(RuleActed act)
    {
        new SupervisorRuleActs(_home).Record(act);
        return act;
    }

    private sealed class Admits(string generation) : IDecisionAdmission
    {
        public List<(string Kind, string? Act)> Asked { get; } = [];

        public DecisionAdmitted Admit(string kind, RolePlace place, string? act)
        {
            Asked.Add((kind, act));
            return new(generation, null) { Window = TimeSpan.FromMinutes(1) };
        }
    }

    private static SupervisorCourse Limit(string point, string does) =>
        new("s1", point, "g-1", does, [new SupervisorReason("The other choice's accounts have room.", "the line's limit in its records")],
            new SupervisorHolder(RoleHolders.Terminal, null)) { Kind = SupervisorKinds.Limit };

    private int Notes(string code) =>
        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events
            .Count(e => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true);

    [Fact]
    public void A_refer_on_limits_or_moves_names_driven_roles_alone_and_moves_stay_bounded()
    {
        var world = new SupervisorRuleWorld(["build", "chat"], Driven: ["build"]);
        var held = new SupervisorRuleSet(SupervisorRules.Machine, 1, SupervisorRuleBook.Seeded(3));
        RuleRefusal? Judged(params SupervisorRule[] more) => SupervisorRules.Judge(SupervisorRules.Machine, 1, Seeded(more), [held], world);

        Assert.Null(Judged(Refer(SupervisorRules.Limits, 1, upTo: 30)));
        Assert.Null(Judged(Refer(SupervisorRules.Moves, 3)));
        Assert.Equal("do", Judged(Refer(SupervisorRules.Limits, 1, upTo: 30, roles: ["chat"]))?.Code);
        Assert.Equal("do", Judged(Refer(SupervisorRules.Limits, 1, upTo: 30) with { Roles = null })?.Code);
        Assert.Equal("do", Judged(Refer(SupervisorRules.Moves, 3) with { Roles = null })?.Code);
        Assert.Equal("upTo", Judged(Refer(SupervisorRules.Limits, 1))?.Code);
        Assert.Equal("upTo", Judged(Refer(SupervisorRules.Moves, 3) with { UpTo = 30 })?.Code);

        // The every-role moves rule replaced by a driven role's refer leaves a conversation's limit moving with no bound.
        var next = new SupervisorRule("r-next", SupervisorRules.Limits, SupervisorRules.Windows[SupervisorRules.Limits], 1, SupervisorRules.Next, null, SupervisorRules.Machine, null);
        var rules = SupervisorRuleBook.Seeded(3).Where(rule => rule.Count != SupervisorRules.Moves).Append(next).Append(Refer(SupervisorRules.Moves, 3)).ToList();
        Assert.Equal("moves-unbounded", SupervisorRules.Judge(SupervisorRules.Machine, 1, rules, [held], world)?.Code);
    }

    [Fact]
    public void A_limited_line_under_a_refer_asks_and_its_act_is_the_limit_kinds_point()
    {
        var book = Book(Refer(SupervisorRules.Limits, 1, upTo: 30));
        var policy = SupervisorRuleBook.Course(null, book.For("build", "engine", null))!;
        Assert.Equal((StaffActions.Ask, (int?)30), (policy.Moves[StaffConditions.Limit].Do, policy.Moves[StaffConditions.Limit].UpTo));

        var act = Kept(Acts([Asked], book).Single());
        Assert.Equal((SupervisorRules.Limits, SupervisorRules.Refer, 1), (act.Rule.Count, act.Rule.Do, act.N));
        Assert.Equal(SupervisorKinds.Limit, SupervisorRules.KindOf(act.Rule.Count));
        Assert.Equal(act.Id, Points(book).Open("s1", [Asked])!.Id);

        var next = new SupervisorRule("r-next", SupervisorRules.Limits, SupervisorRules.Windows[SupervisorRules.Limits], 1, SupervisorRules.Next, null, SupervisorRules.Machine, ["build"]);
        var bound = Book(Refer(SupervisorRules.Moves, 3), next);
        Assert.Equal(3, SupervisorRuleBook.Course(null, bound.For("build", "engine", null))!.Most);
        Assert.Equal(SupervisorRules.Moves, Acts([Bounded], bound).Single().Rule.Count);
    }

    [Fact]
    public async Task The_supervisors_move_is_held_for_the_window_overruled_inside_it_and_left_to_the_person()
    {
        var book = Book(Refer(SupervisorRules.Limits, 1, upTo: 30));
        var act = Kept(Acts([Asked], book).Single());
        var admits = new Admits("g-1");
        var decisions = new QuestionSupervisorDecisions(_home, admits);
        var points = Points(book);

        var kept = await decisions.CourseAsync(Limit(act.Id, SupervisorLimitActs.Move), points.PointOf(Limited, null, [Asked]), At);
        Assert.True(kept.Kept, kept.Why);
        Assert.Contains((SupervisorKinds.Limit, (string?)SupervisorLimitActs.Move), admits.Asked);
        Assert.Equal(SupervisorKinds.Limit, decisions.CoursesOf("s1").Single().Kind);

        Assert.Equal((SupervisorState?)SupervisorState.Overruled, await decisions.OverruleAsync("s1", kept.Decision!, (_, _, _) => Task.FromResult(true), At.AddSeconds(20)));
        var released = new List<string>();
        Assert.Empty(await decisions.ReleaseCoursesAsync(points.Line(_ => Limited, _ => false, [Asked], (_, does) => { released.Add(does); return true; }), At.AddMinutes(2)));
        Assert.Empty(released);

        // Overruled, the point is the person's: a second decide is refused, and a leave keeps it theirs.
        Assert.False((await decisions.CourseAsync(Limit(act.Id, SupervisorLimitActs.Move), points.PointOf(Limited, null, [Asked]), At.AddMinutes(3))).Kept);
    }

    [Fact]
    public async Task The_supervisors_move_goes_on_after_the_window_as_the_quests_one_approval_said_on_its_line()
    {
        var book = Book(Refer(SupervisorRules.Limits, 1, upTo: 30));
        var act = Kept(Acts([Asked], book).Single());
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var points = Points(book);
        Assert.True((await decisions.CourseAsync(Limit(act.Id, SupervisorLimitActs.Move), points.PointOf(Limited, null, [Asked]), At)).Kept);

        var released = new List<(string Quest, string Act)>();
        var line = points.Line(_ => Limited, _ => false, [Asked], (quest, does) => { released.Add((quest, does)); return true; });
        Assert.Empty(await decisions.ReleaseCoursesAsync(line, At.AddSeconds(30), SupervisorRules.QuestCounts));
        Assert.Equal([SupervisorState.WentOn], await decisions.ReleaseCoursesAsync(line, At.AddMinutes(2), SupervisorRules.QuestCounts));
        Assert.Equal([("q1", SupervisorLimitActs.Move)], released);
        Assert.Equal(1, Notes(HeldNoteCodes.SupervisorMoved.Code));
    }

    [Fact]
    public async Task A_leave_is_the_persons_at_once_and_a_course_decide_on_a_limit_point_is_not_referred()
    {
        var book = Book(Refer(SupervisorRules.Limits, 1, upTo: 30));
        var act = Kept(Acts([Asked], book).Single());
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var point = Points(book).PointOf(Limited, null, [Asked]);

        var goOn = Limit(act.Id, SupervisorCourseActs.GoOn) with { Kind = SupervisorKinds.Course };
        Assert.Equal(SupervisorWhy.NotReferred, (await decisions.CourseAsync(goOn, point, At)).Why);

        var left = await decisions.CourseAsync(Limit(act.Id, SupervisorCourseActs.Hold) with { Bound = "preference" }, point, At);
        Assert.True(left.Kept, left.Why);
        Assert.Null(left.GoesOnAt);
        Assert.Equal(SupervisorState.Left, decisions.CoursesOf("s1").Single().State);
    }

    [Fact]
    public async Task A_wait_is_a_limited_ends_alone()
    {
        var limits = Book(Refer(SupervisorRules.Limits, 1, upTo: 30));
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var act = Kept(Acts([Asked], limits).Single());
        Assert.True((await decisions.CourseAsync(Limit(act.Id, SupervisorLimitActs.Wait), Points(limits).PointOf(Limited, null, [Asked]), At)).Kept);

        var moves = Book(Refer(SupervisorRules.Moves, 3));
        var bound = Kept(Acts([Bounded], moves).Single());
        Assert.Equal(SupervisorWhy.NoWait, (await decisions.CourseAsync(Limit(bound.Id, SupervisorLimitActs.Wait), Points(moves).PointOf(Limited, null, [Bounded]), At)).Why);
    }

    [Fact]
    public void The_supervisors_wait_holds_the_part_for_its_choices_reset_to_the_rules_minutes_then_asks_the_person_again()
    {
        var store = new SupervisorRuleStore(_home);
        Assert.True(store.Write(
            SupervisorRules.Machine, store.Of(SupervisorRules.Machine).Revision, Seeded(Refer(SupervisorRules.Limits, 1, upTo: 30)),
            SupervisorRuleBook.SeedDoor, SupervisorRuleDoor.World(_home)).Stored);
        var staff = new StaffLedger(_home, () => StoreSettled.Yes);
        var snapshot = new Snapshot([Quest], [], [], new Dictionary<string, int>())
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = Limited },
        };
        var place = new StaffPlace("engine", null);

        Assert.Equal(SittingFact.PolicyAsk, StaffSnapshotCourse.LimitHolds(staff, snapshot, At)["q1"]);
        Assert.Null(StaffCourse.LimitWait(staff, "build", Limited, At, place, "q1"));

        Assert.True(staff.Moves.Wait("q1", "build", "s1"));
        Assert.Empty(StaffSnapshotCourse.LimitHolds(staff, snapshot, At));
        Assert.IsType<LineCourse.Wait>(StaffCourse.LimitWait(staff, "build", Limited, At, place, "q1"));
        Assert.Equal(0, staff.Moves.Of("q1").Moves);

        // Past the rule's minutes after the cut-off the wait is spent, and the ask is the person's again.
        Assert.Equal(SittingFact.PolicyAsk, StaffSnapshotCourse.LimitHolds(staff, snapshot, At.AddMinutes(30))["q1"]);
        Assert.Null(StaffCourse.LimitWait(staff, "build", Limited, At.AddMinutes(30), place, "q1"));

        // A later limit, on another part, is a new ask.
        Assert.Equal(SittingFact.PolicyAsk, StaffSnapshotCourse.LimitHolds(staff, snapshot with
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = Limited with { Session = "s2" } },
        }, At)["q1"]);
    }

    [Fact]
    public async Task A_move_on_a_line_the_person_stopped_or_paused_is_refused_owner_held_at_the_keeping_and_at_the_release()
    {
        var book = Book(Refer(SupervisorRules.Limits, 1, upTo: 30));
        var act = Kept(Acts([Asked], book).Single());
        var decisions = new QuestionSupervisorDecisions(_home, new Admits("g-1"));
        var points = Points(book);
        var stopped = Limited with { State = "stopped" };

        Assert.Equal(SupervisorWhy.OwnerHeld, (await decisions.CourseAsync(Limit(act.Id, SupervisorLimitActs.Move), points.PointOf(stopped, null, [Asked]), At)).Why);
        Assert.True(OwnerHolds.Holds(Limited, [new Consideration(Quest, StartVerdict.Paused, "paused")]));
        Assert.False(OwnerHolds.Holds(Limited, [Asked]));

        Assert.True((await decisions.CourseAsync(Limit(act.Id, SupervisorLimitActs.Move), points.PointOf(Limited, null, [Asked]), At)).Kept);
        var released = 0;
        Assert.Equal(
            [SupervisorState.Stale],
            await decisions.ReleaseCoursesAsync(points.Line(_ => stopped, _ => false, [Asked], (_, _) => ++released > 0), At.AddMinutes(2), SupervisorRules.QuestCounts));
        Assert.Equal(0, released);
        Assert.Equal(SupervisorWhy.OwnerHeld, decisions.CoursesOf("s1").Single().Why);
    }
}
