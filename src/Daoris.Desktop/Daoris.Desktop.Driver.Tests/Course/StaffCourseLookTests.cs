using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1e2 (D226; the staff policy design §4): what the course decides at the look for a quest whose last part ended failed or quiet,
/// and how many tries a driven run's episode makes against a conversation's five. The quiet case is the decision over an ended record: a
/// quiet run is never ended by a policy (D200), so a record still running has nothing to decide, and only an ended one carries on.
/// </summary>
public sealed class StaffCourseLookTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-course-look", Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset Now = new(2026, 11, 2, 14, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private StaffLedger Staffed(string policy)
    {
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file,
            """{ "adapter": "acp-stub", "actors": { "build": [ { "agent": "acp-stub", "model": "sonnet", "effort": "low" }, { "agent": "acp-other", "model": "codex-mini", "effort": "high" } ] } }""");
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.True(staffing.ImportFile().How is StaffingImported.Done or StaffingImported.Already);
        RoleDoor.Apply(staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(StaffPolicy.Parse(policy).Policy!));
        return new StaffLedger(_home, () => StoreSettled.Yes);
    }

    private static string Policy(string moves, int? most = 3) =>
        $$"""
        {"rule":"order","choices":[{"agent":"acp-stub","model":"sonnet","effort":"low"},{"agent":"acp-other","model":"codex-mini","effort":"high"}],
         "moves":{{moves}}{{(most is { } bound ? $",\"most\":{bound}" : "")}}}
        """;

    private static PriorSession Ended(string code, string state = "failed") => new("s1", "trees/default/engine/s-1", state)
    {
        Quest = "q1", Actor = "build", Adapter = "acp-stub", Said = [], Updated = Now.AddMinutes(-1),
        NoteParts = code == "none" ? [] : [new NotePart { Code = code, Text = "a line." }],
    };

    private static Snapshot Looking(PriorSession last, int failedRuns = 1, int strikes = 1) =>
        new([new QuestView("q1", "ask #a1", "engine", "The work", "", "Taken")], [], [], new Dictionary<string, int> { ["q1"] = strikes })
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = last },
            FailedRuns = new Dictionary<string, int> { ["q1"] = failedRuns },
        };

    private static readonly DriverConfig Config = DriverConfig.Empty with { Strikes = 3 };

    [Fact]
    public void The_course_reads_the_policy_of_the_scoped_role_that_took_the_work_never_seeded_builds_by_the_records_actor()
    {
        var staff = Staffed(Policy("""{"quiet":"retry"}"""));
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        var scoped = RoleCatalog.Seed[0] with { Id = "engine-build", Scope = new RoleScope(RoleScope.Repository, "engine") };
        var choices = new[] { new ActorChoice("acp-stub", "sonnet", "low"), new ActorChoice("acp-other", "codex-mini", "high") };
        RoleDoor.Apply(
            staffing, RoleChange.Set(scoped), choices, null,
            new RolePolicyPress(StaffPolicy.Parse(Policy("""{"quiet":"next"}""")).Policy!));

        // The record's actor is the seeded id, as on every driven record: the scoped role that takes the quest's work is the one asked.
        var snapshot = Looking(Ended("ended.quiet"));
        Assert.Equal("engine-build", StaffCourse.RoleOf(staff, snapshot, snapshot.Quests[0], snapshot.LastRun["q1"]));
        var (moving, _) = StaffCourse.Look(staff, snapshot, Config, Now);

        Assert.Equal("s1", Assert.Single(moving).Value.Session);
    }

    [Fact]
    public void A_quiet_end_moves_the_line_when_the_role_moves_on_it_and_a_role_that_retries_decides_nothing()
    {
        var moves = Staffed(Policy("""{"quiet":"next"}""")).Moves;
        var staff = new StaffLedger(_home, () => StoreSettled.Yes);

        var (moving, holds) = StaffCourse.Look(staff, Looking(Ended("ended.quiet")), Config, Now);

        Assert.Empty(holds);
        var move = Assert.Single(moving);
        Assert.Equal(("q1", "s1", StaffConditions.Quiet), (move.Key, move.Value.Session, move.Value.Condition));
        Assert.Equal(1, moves.Of("q1").Moves);

        var (again, _) = StaffCourse.Look(staff, Looking(Ended("ended.quiet")), Config, Now);
        Assert.Equal("s1", Assert.Single(again).Value.Session);
        Assert.Equal(1, moves.Of("q1").Moves);
    }

    [Fact]
    public void A_quiet_end_under_a_retry_and_an_unset_role_decides_nothing_and_writes_nothing()
    {
        var staff = Staffed(Policy("""{"quiet":"retry"}"""));
        var (moving, holds) = StaffCourse.Look(staff, Looking(Ended("ended.quiet")), Config, Now);
        Assert.Empty(moving);
        Assert.Empty(holds);
        Assert.Equal(0, staff.Moves.Of("q1").Moves);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void A_fatal_end_moves_after_its_failures_on_the_one_choice_and_not_before(int failures, bool moves)
    {
        var staff = Staffed(Policy("""{"failed":{"do":"next","after":2}}"""));

        var (moving, _) = StaffCourse.Look(staff, Looking(Ended("none"), failedRuns: failures, strikes: failures), Config, Now);

        Assert.Equal(moves, moving.ContainsKey("q1"));
        Assert.Equal(moves ? 1 : 0, staff.Moves.Of("q1").Moves);
    }

    [Fact]
    public void A_quest_the_strikes_parked_is_not_moved_and_a_limit_a_transient_end_or_a_refused_account_is_no_failed_end()
    {
        var staff = Staffed(Policy("""{"failed":{"do":"next","after":1}}"""));

        var (parked, _) = StaffCourse.Look(staff, Looking(Ended("none"), failedRuns: 3, strikes: 3), Config, Now);
        Assert.Empty(parked);

        Assert.False(StaffCourse.FailedStrike(Ended("ended.transient")));
        Assert.False(StaffCourse.FailedStrike(Ended("ended.quiet")));
        Assert.False(StaffCourse.FailedStrike(Ended("none", state: "completed")));
        Assert.False(StaffCourse.FailedStrike(Ended("none") with { Limit = true }));
        Assert.True(StaffCourse.FailedStrike(Ended("none")));
        Assert.True(StaffCourse.EndedQuiet(Ended("ended.quiet")));
    }

    [Fact]
    public void An_ask_on_a_fatal_end_holds_the_quest_typed_and_the_bound_makes_the_next_move_an_ask()
    {
        var staff = Staffed(Policy("""{"failed":{"do":"ask","after":1}}"""));

        var (moving, holds) = StaffCourse.Look(staff, Looking(Ended("none")), Config, Now);

        Assert.Empty(moving);
        Assert.Equal(SittingFact.PolicyAsk, holds["q1"]);

        var bounded = Staffed(Policy("""{"quiet":"next"}""", most: 1));
        Assert.True(bounded.Moves.Record("q1", "build", null, StaffConditions.ModelRefused, StaffActions.Next, null));
        var (none, atBound) = StaffCourse.Look(bounded, Looking(Ended("ended.quiet")), Config, Now);
        Assert.Empty(none);
        Assert.Equal(SittingFact.MovesBound, atBound["q1"]);
    }

    [Fact]
    public void A_driven_runs_tries_are_the_roles_after_where_it_moves_on_a_service_that_does_not_answer_else_the_five()
    {
        Assert.Equal(2, StaffCourse.Tries(Staffed(Policy("""{"transient":{"do":"next","after":2}}""")), "build"));
        Assert.Equal(TransientRetry.Bound, StaffCourse.Tries(Staffed(Policy("""{"failed":{"do":"retry"}}""")), "build"));
        Assert.Equal(TransientRetry.Bound, StaffCourse.Tries(null, "build"));
        Assert.Equal(5, TransientRetry.Bound);
    }

    [Fact]
    public void A_moved_off_choice_is_passed_for_thirty_minutes_and_then_not()
    {
        var staff = Staffed(Policy("""{"failed":{"do":"next","after":1}}"""));
        var left = StaffKey.Read("""{"agent":"acp-stub","model":"sonnet","effort":"low"}""")!.Value;
        Assert.True(staff.Moves.Record("q1", "build", "s1", StaffConditions.Failed, StaffActions.Next, left.Text));

        var line = staff.Moves.LineFacts("q1");
        Assert.Equal(left, Assert.Single(line.MovedOff.Keys));

        IReadOnlyList<ActorEffective> order = [new("acp-stub", "sonnet", "low", "1"), new("acp-other", "codex-mini", "high", "2")];
        var take = staff.Preview("build", RoleWork.Build, order, line);
        Assert.Equal([("1", StaffPasses.MovedOff)], take.Arrangement.Passed.Select(each => (each.Entry.From, each.Code)));
        Assert.Equal(["2"], take.Arrangement.Walk.Select(each => each.Entry.From));
        Assert.Empty(staff.Moves.LineFacts("another").MovedOff);
    }
}
