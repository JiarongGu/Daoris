using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// An agent supervisor's attempt, with a stub turn and a stand-in door: answering, leaving, failing and timing out, answering late after
/// a revoke, a limited end moving to the next choice, no choice ready leaving the subject to the person, and the person's word ending a
/// running turn, its result dropped.
/// </summary>
public sealed class SupervisePassTests : IDisposable
{
    private static readonly ActorEffective Opus = new("claude-code-acp", "opus", "high", "1");

    private static readonly ActorEffective Sonnet = new("claude-code-acp", "sonnet", "medium", "2");

    private static readonly SupervisorWaiting Point =
        new(SupervisorKinds.Course, SupervisorSubject.Point("s1", "p-1"), "reports", "agent:supervisor@0", ["go-on"]) { Point = ("answers-alone", "3", "r1", "machine") };

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervise-pass", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private sealed class Desk(string? refuse = null, string? facts = "") : ISupervisorDesk
    {
        public List<(SupervisorBlock Block, SupervisorHolder Holder)> Kept { get; } = [];

        public Task<IReadOnlyList<SupervisorWaiting>> WaitingAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SupervisorWaiting>>([Point]);

        public Task<string?> FactsAsync(SupervisorWaiting waiting, string fence, CancellationToken ct) => Task.FromResult(facts);

        public Task<SupervisorBlockKept> KeepAsync(SupervisorWaiting waiting, SupervisorBlock block, SupervisorHolder holder, CancellationToken ct)
        {
            if (refuse is not null) return Task.FromResult(new SupervisorBlockKept(false, refuse));
            Kept.Add((block, holder));
            return Task.FromResult(new SupervisorBlockKept(true, null));
        }
    }

    private static string GoOn => $"{SupervisorBlock.Opening}\n{{ \"kind\": \"course\", \"subject\": {{ \"session\": \"s1\", \"point\": \"p-1\" }}, \"do\": \"go-on\", \"reasons\": [ {{ \"says\": \"The tests pass.\", \"source\": \"count:failures\" }} ] }}\n{SupervisorBlock.Closing}";

    private static string Leave => GoOn.Replace("\"do\": \"go-on\"", "\"leave\": \"preference\"", StringComparison.Ordinal);

    private (Daoris.Driver.Driver Driver, List<SuperviseRunAsk> Asked, List<IReadOnlyList<ActorEffective>> Walked) Driver(
        Func<SuperviseRunAsk, CancellationToken, Task<SuperviseRunEnd>> run, Func<ReviewerChoice>? choose = null)
    {
        var host = new OpinionStandIn().Session("s1", "awaiting-person", tree: "trees/default/reports/s-1", kind: "drive");
        // The machine's supervisor held by an agent through the role door, its two choices its own row.
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "claude-code-acp" }""");
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        staffing.ImportFile();
        HomeMigration.Run(_home, [new SupervisorStep(() => null)]);
        var seed = RoleCatalog.Seed.Single(role => role.Id == AppointedRoles.Supervisor);
        RoleDoor.Apply(staffing, RoleChange.Set(seed with { Hold = seed.Hold! with { Holder = RoleHolders.Agent } }),
            [new("claude-code-acp", "opus", "high"), new("claude-code-acp", "sonnet", "medium")]);
        var driver = new Daoris.Driver.Driver(host.Client(), StaffingProjection.Config(staffing.Read()!), ProviderRegistry.Built(), _home);
        var (asked, walked) = (new List<SuperviseRunAsk>(), new List<IReadOnlyList<ActorEffective>>());
        driver.Context.SuperviseRuns = (ask, ct) =>
        {
            asked.Add(ask);
            return run(ask, ct);
        };
        driver.Context.SuperviseChoices = (_, left, _) =>
        {
            walked.Add(left);
            return Task.FromResult(choose?.Invoke() ?? new ReviewerChoice(left[0].Agent, "1", null, "ready") { Effective = left[0] });
        };
        return (driver, asked, walked);
    }

    private static Task<SuperviseRunEnd> Ended(string state, string? words = null) =>
        Task.FromResult(new SuperviseRunEnd(state, words, "t-1", OpinionPosture.RulesAndCopy, 1200, "opus"));

    private static async Task Until(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < until, "the attempt did not end");
            await Task.Delay(20);
        }
    }

    private static SupervisorAttempt? Last(Daoris.Driver.Driver driver) => driver.Context.Supervisor.Attempts.Of(Point.Subject).LastOrDefault();

    [Fact]
    public async Task An_answer_is_kept_through_the_kind_s_keeping_with_the_tier_that_ran_and_what_held_it()
    {
        var (driver, asked, _) = Driver((_, _) => Ended("completed", GoOn));
        var desk = new Desk();

        Assert.NotNull(await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default));
        await Until(() => Last(driver)?.Outcome is not null);

        Assert.Equal(SupervisorAttemptOutcomes.Decided, Last(driver)!.Outcome);
        var (block, holder) = Assert.Single(desk.Kept);
        Assert.Equal("go-on", block.Do);
        Assert.Equal((RoleHolders.Agent, "supervisor", "claude-code-acp", "opus", OpinionPosture.RulesAndCopy, 1200L), (holder.Kind, holder.Agent!.Role, holder.Agent.Agent, holder.Agent.Ran, holder.Agent.Posture, holder.Agent.Tokens));
        Assert.Contains("answers-alone", asked[0].Prompt);
        Assert.Contains(SupervisorBlock.Opening, asked[0].Prompt);
        Assert.Null(await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default));
    }

    [Fact]
    public async Task ROLESREVIEW1a_a_files_subject_takes_a_turn_handed_the_facts_its_kind_composed_and_none_where_it_no_longer_waits()
    {
        var files = new SupervisorWaiting(SupervisorKinds.Files, SupervisorSubject.Ask("engine", "2026-11-09T09:00:00Z", "run(npm ci)", "npm ci"), "engine", "agent:supervisor@0", ["allow-run"]);
        var (driver, asked, _) = Driver((_, _) => Ended("completed", Leave));

        Assert.Null(await new SupervisePass(driver.Context, driver).StartAsync(files, new Desk(facts: null), default));
        Assert.Empty(asked);

        Assert.NotNull(await new SupervisePass(driver.Context, driver).StartAsync(files, new Desk(facts: "act: run(npm ci)"), default));
        await Until(() => asked.Count > 0);
        Assert.Contains("act: run(npm ci)", Assert.Single(asked).Prompt);
    }

    [Fact]
    public async Task A_limit_point_is_handed_the_point_s_facts_and_takes_a_turn()
    {
        var limit = new SupervisorWaiting(
            SupervisorKinds.Limit, SupervisorSubject.Point("s1", "p-1", SupervisorKinds.Limit), "reports", "agent:supervisor@0", ["move"])
        {
            Point = ("limits", "1", "r1", "machine"),
        };
        var (driver, asked, _) = Driver((_, _) => Ended("completed", GoOn.Replace("\"course\"", "\"limit\"", StringComparison.Ordinal).Replace("go-on", "move", StringComparison.Ordinal)));
        var desk = new Desk();

        Assert.NotNull(await new SupervisePass(driver.Context, driver).StartAsync(limit, desk, default));
        await Until(() => driver.Context.Supervisor.Attempts.Of(limit.Subject).LastOrDefault()?.Outcome is not null);

        Assert.Contains("count: limits", Assert.Single(asked).Prompt);
        Assert.Equal("move", Assert.Single(desk.Kept).Block.Do);
    }

    [Fact]
    public async Task A_leave_is_a_decision_and_final()
    {
        var (driver, _, _) = Driver((_, _) => Ended("completed", Leave));
        var desk = new Desk();

        await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default);
        await Until(() => Last(driver)?.Outcome is not null);

        Assert.Equal(SupervisorAttemptOutcomes.Left, Last(driver)!.Outcome);
        Assert.Equal("preference", Assert.Single(desk.Kept).Block.Leave);
        Assert.True(driver.Context.Supervisor.Attempts.Ended(Point.Subject));
    }

    [Theory]
    [InlineData("failed", null)]
    [InlineData("completed", "no block, only words")]
    public async Task A_failed_end_a_timeout_or_a_block_that_does_not_read_ends_the_subject_s_attempts(string state, string? words)
    {
        var (driver, _, _) = Driver((_, _) => Ended(state, words));
        var desk = new Desk();

        await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default);
        await Until(() => Last(driver)?.Outcome is not null);

        Assert.Equal(SupervisorAttemptOutcomes.Failed, Last(driver)!.Outcome);
        Assert.Empty(desk.Kept);
        Assert.Null(await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default));
    }

    [Fact]
    public async Task An_answer_after_a_revoke_or_a_role_change_is_dropped_at_the_keeping()
    {
        var (driver, _, _) = Driver((_, _) => Ended("completed", GoOn));
        var desk = new Desk(SupervisorWhy.NotGranted);

        await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default);
        await Until(() => Last(driver)?.Outcome is not null);

        Assert.Equal(SupervisorAttemptOutcomes.Failed, Last(driver)!.Outcome);
        Assert.Empty(desk.Kept);
    }

    [Fact]
    public async Task ROLESREVIEW1f_a_dropped_block_is_said_on_the_line()
    {
        var (driver, _, _) = Driver((_, _) => Ended("completed", GoOn));

        await new SupervisePass(driver.Context, driver).StartAsync(Point, new Desk(SupervisorWhy.Form), default);
        await Until(() => Last(driver)?.Outcome is not null);

        Assert.Equal(SupervisorAttemptOutcomes.Failed, Last(driver)!.Outcome);
        Assert.Contains(driver.Context.Events.After("s1", 0).Events, e => e.Kind == SessionEventKind.Note && e.Text!.Contains("not kept (form)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ROLESREVIEW1f_a_block_dropped_for_a_role_change_spends_no_choice_and_the_subject_begins_again()
    {
        var (driver, asked, _) = Driver((_, _) => Ended("completed", GoOn));

        await new SupervisePass(driver.Context, driver).StartAsync(Point, new Desk(SupervisorWhy.RoleChanged), default);
        await Until(() => Last(driver)?.Outcome is not null);

        Assert.Equal(SupervisorAttemptOutcomes.RoleChanged, Last(driver)!.Outcome);
        Assert.False(driver.Context.Supervisor.Attempts.Ended(Point.Subject));
        Assert.Empty(driver.Context.Supervisor.Attempts.Spent(Point.Subject));
        Assert.Contains(driver.Context.Events.After("s1", 0).Events, e => e.Kind == SessionEventKind.Note && e.Text!.Contains("role changed", StringComparison.Ordinal));

        var desk = new Desk();
        Assert.NotNull(await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default));
        await Until(() => driver.Context.Supervisor.Attempts.Of(Point.Subject).Count == 2 && Last(driver)!.Outcome is not null);

        Assert.Equal([1, 1], asked.Select(ask => ask.Attempt.Choice));
        Assert.Equal(SupervisorAttemptOutcomes.Decided, Last(driver)!.Outcome);
        Assert.Single(desk.Kept);
    }

    [Fact]
    public async Task A_limited_end_is_a_pass_and_the_next_attempt_takes_the_next_choice()
    {
        var turns = 0;
        var (driver, asked, walked) = Driver((_, _) => ++turns == 1 ? Ended("limited") : Ended("completed", GoOn));
        var desk = new Desk();

        await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default);
        await Until(() => Last(driver)?.Outcome is not null);
        Assert.Equal(SupervisorAttemptOutcomes.Limited, Last(driver)!.Outcome);

        await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default);
        await Until(() => driver.Context.Supervisor.Attempts.Of(Point.Subject).Count == 2 && Last(driver)!.Outcome is not null);

        Assert.Equal([1, 2], driver.Context.Supervisor.Attempts.Spent(Point.Subject));
        Assert.Equal([Sonnet.Model], walked[1].Select(each => each.Model));
        Assert.Equal(2, asked[1].Attempt.Choice);
        Assert.Single(desk.Kept);
    }

    [Fact]
    public async Task With_no_choice_ready_nothing_starts_the_subject_waits_for_the_person_and_it_is_taken_once_one_is()
    {
        var ready = false;
        var (driver, asked, _) = Driver(
            (_, _) => Ended("completed", GoOn),
            () => ready ? new ReviewerChoice(Opus.Agent, "1", null, "ready") { Effective = Opus } : new ReviewerChoice(null, null, "cooling", "Claude Code's accounts cool until 14:20."));
        var desk = new Desk();

        var said = await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default);

        Assert.Contains("cool until 14:20", said);
        Assert.Empty(driver.Context.Supervisor.Attempts.Of(Point.Subject));
        ready = true;
        await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default);
        await Until(() => Last(driver)?.Outcome is not null);
        Assert.Single(asked);
        Assert.Equal(SupervisorAttemptOutcomes.Decided, Last(driver)!.Outcome);
    }

    [Fact]
    public async Task ROLESREVIEW2a_an_outcome_that_cannot_be_written_still_frees_the_subject_and_its_line()
    {
        var (driver, _, _) = Driver((_, _) =>
        {
            Directory.Delete(DaorisHome.Entry(_home, HomeEntry.Sessions, Point.Subject.Session + QuestionSupervisorDecisions.DecisionsSuffix), recursive: true);
            return Ended("completed", GoOn);
        });

        await new SupervisePass(driver.Context, driver).StartAsync(Point, new Desk(), default);
        await Until(() => !driver.Context.Supervisor.Runs.Running(Point.Subject));

        Assert.DoesNotContain(Point.Subject.Session, driver.Context.Supervisor.Runs.Lines);
    }

    [Fact]
    public async Task The_person_s_word_ends_a_running_turn_and_drops_its_result()
    {
        var (driver, _, _) = Driver(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new SuperviseRunEnd("completed", GoOn, "t-1", OpinionPosture.RulesAndCopy);
        });
        var desk = new Desk();

        await new SupervisePass(driver.Context, driver).StartAsync(Point, desk, default);
        await Until(() => driver.Context.Supervisor.Runs.Running(Point.Subject));
        Assert.Equal(1, driver.Context.Supervisor.Runs.StopUnwaited([]));
        await Until(() => Last(driver)?.Outcome is not null);

        Assert.Equal(SupervisorAttemptOutcomes.Failed, Last(driver)!.Outcome);
        Assert.Empty(desk.Kept);
    }
}
