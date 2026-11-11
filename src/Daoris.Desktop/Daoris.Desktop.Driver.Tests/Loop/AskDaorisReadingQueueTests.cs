using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The reading queue over the one walk with a stub turn: a range read packet-only and kept with its reader and how that reader stands
/// to the range's writer, a writer not known never independent, Codex passed while it is not enforced, a range no choice can take
/// staying queued with the walk's sentence and closing unread at 24 hours, a malformed block or one for another range closing it unread,
/// a limited end taking the next choice, the check turned off closing the queue, one reading at a time, and a restart closing a running
/// reading.
/// </summary>
public sealed class AskDaorisReadingQueueTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "ask-daoris-reading", Guid.NewGuid().ToString("N")[..8]);

    private readonly CancellationTokenSource _release = new();

    public void Dispose()
    {
        _release.Cancel();
        _release.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    internal AskDaorisReports Reports => new(_home);

    internal sealed record Harness(Daoris.Driver.Driver Driver, List<SuperviseRunAsk> Asked, List<(SupervisorWaiting Waiting, IReadOnlyList<ActorEffective> Left)> Walked);

    internal Harness Set(
        Func<SuperviseRunAsk, CancellationToken, Task<SuperviseRunEnd>> run, Func<IReadOnlyList<ActorEffective>, ReviewerChoice>? choose = null,
        string? writer = "claude-code-acp", string holder = RoleHolders.Agent, bool decisions = false)
    {
        var host = new OpinionStandIn();
        if (writer is not null) host.Session("s1", "completed", adapter: writer, kind: "help");
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "claude-code-acp" }""");
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        staffing.ImportFile();
        HomeMigration.Run(_home, [new SupervisorStep(() => null)]);
        var seed = RoleCatalog.Seed.Single(role => role.Id == SecondCheck.Role);
        if (holder == RoleHolders.Agent)
        {
            RoleDoor.Apply(staffing, RoleChange.Set(seed with { Hold = seed.Hold! with { Holder = holder } }), [new(CodexAccounts.Agent), new("claude-code-acp", "opus", "high")]);
        }

        if (decisions)
        {
            host.Session("s2", "awaiting-person", tree: "trees/default/reports/s-2", kind: "drive");
            var generic = RoleCatalog.Seed.Single(role => role.Id == AppointedRoles.Supervisor);
            RoleDoor.Apply(staffing, RoleChange.Set(generic with { Hold = generic.Hold! with { Holder = RoleHolders.Agent } }), [new("claude-code-acp", "sonnet", "medium")]);
        }

        var driver = new Daoris.Driver.Driver(host.Client(), StaffingProjection.Config(staffing.Read()!), ProviderRegistry.Built(), _home);
        var (asked, walked) = (new List<SuperviseRunAsk>(), new List<(SupervisorWaiting, IReadOnlyList<ActorEffective>)>());
        driver.Context.SuperviseRuns = (ask, ct) =>
        {
            lock (asked) asked.Add(ask);
            return run(ask, ct);
        };
        driver.Context.SuperviseChoices = (waiting, left, _) =>
        {
            lock (walked) walked.Add((waiting, left));
            return Task.FromResult(waiting.Kind == SupervisorKinds.AskDaoris && choose is not null ? choose(left) : Another(left));
        };
        return new Harness(driver, asked, walked);
    }

    internal static ReviewerChoice Another(IReadOnlyList<ActorEffective> left) =>
        new(left[0].Agent, ReviewerLabels.AnotherMaker, null, $"`{left[0].Agent}` reads it, another maker's agent than the one that did the work.") { Effective = left[0] };

    internal AskDaorisReport Queued(string range = "r-1", DateTimeOffset? opened = null)
    {
        var call = new SelectedCall("toolu_push", SelectedWhy.Act, "execute", "git push", "Bash", "completed", ["engine"], "{\"command\":\"git push\"}", "pushed", []);
        var turn = new ReadTurn("w-a", 1, 4, Start, Start, false, "Push the fix.", [call], [], [], new Dictionary<string, int>(), []);
        var report = new AskDaorisReport(range, "s1", AskDaorisStates.Queued, opened ?? DateTimeOffset.UtcNow, [turn], []);
        Reports.Write(report);
        return report;
    }

    internal static string Answer(string range = "r-1", string findings = "[ { \"severity\": \"flag\", \"certainty\": \"likely\", \"says\": \"A push the person did not ask for.\", \"call\": \"c1\", \"instruction\": \"w1\" } ]") =>
        $"{SupervisorBlock.Opening}\n{{ \"kind\": \"ask-daoris\", \"subject\": {{ \"session\": \"s1\", \"range\": \"{range}\" }}, \"do\": \"report\", \"findings\": {findings} }}\n{SupervisorBlock.Closing}";

    internal static Task<SuperviseRunEnd> Ended(string state, string? words = null) =>
        Task.FromResult(new SuperviseRunEnd(state, words, "t-1", OpinionPosture.PacketOnly, 900, "claude-opus-4"));

    internal static async Task ReadAsync(Daoris.Driver.Driver driver)
    {
        var reading = new AskDaorisReadingQueue(driver.Context, driver).Offer(CancellationToken.None);
        Assert.NotNull(reading);
        await reading.WaitAsync(Poll.Patience);
    }

    private AskDaorisReport Report(string range = "r-1") => Reports.Find("s1", range)!;

    [Fact]
    public async Task A_range_is_read_packet_only_and_kept_with_its_findings_its_reader_and_how_it_stands_to_the_writer()
    {
        var harness = Set((_, _) => Ended("completed", Answer()));
        Queued();

        await ReadAsync(harness.Driver);

        var report = Report();
        Assert.Equal(AskDaorisStates.Read, report.State);
        var finding = Assert.Single(report.Findings);
        Assert.Equal(("flag", "c1", "w1"), (finding.Severity, finding.Call, finding.Instruction));
        Assert.Equal(("second-check", "claude-code-acp", "opus", "claude-opus-4", OpinionPosture.PacketOnly, 900L),
            (report.Reader!.Role, report.Reader.Agent, report.Reader.Model, report.Reader.Ran, report.Reader.Posture, report.Reader.Tokens));
        Assert.Equal("claude-code-acp", report.Writer);
        Assert.Equal(ReadIndependence.AnotherMaker, report.Independence);
        var ask = Assert.Single(harness.Asked);
        Assert.True(ask.Waiting.PacketOnly);
        Assert.Contains("<<<packet", ask.Prompt);
        Assert.Contains("git push", ask.Prompt);
        Assert.Equal(["claude-code-acp"], Assert.Single(harness.Walked).Waiting.Writers);
        Assert.Contains("git push", Reports.Packet("s1", "r-1"));
    }

    [Fact]
    public async Task Codex_is_passed_while_its_reader_is_not_enforced()
    {
        var harness = Set((_, _) => Ended("completed", Answer()));
        Queued();

        await ReadAsync(harness.Driver);

        Assert.DoesNotContain(Assert.Single(harness.Walked).Left, each => each.Agent == CodexAccounts.Agent);
    }

    [Fact]
    public async Task The_same_maker_is_kept_not_independent_and_a_writer_not_known_is_never_presumed_independent()
    {
        var same = Set((_, _) => Ended("completed", Answer()), left => new ReviewerChoice(left[0].Agent, ReviewerLabels.SameAgent, ReviewerUnavailable.NotIndependent, "not an independent reading.") { Effective = left[0] });
        Queued();
        await ReadAsync(same.Driver);
        Assert.Equal(ReadIndependence.NotIndependent, Report().Independence);
        Assert.Contains("not an independent reading", Report().Said);

        var unknown = Set((_, _) => Ended("completed", Answer("r-2")), writer: null);
        Queued("r-2");
        await ReadAsync(unknown.Driver);
        Assert.Equal(ReadIndependence.Unknown, Report("r-2").Independence);
        Assert.Empty(Assert.Single(unknown.Walked).Waiting.Writers);
    }

    [Fact]
    public async Task A_range_no_choice_can_take_stays_queued_with_the_walk_s_sentence_and_closes_unread_at_24_hours()
    {
        var harness = Set((_, _) => Ended("completed", Answer()), _ => new ReviewerChoice(null, null, ReviewerUnavailable.Cooling, "Claude Code's accounts cool until 14:20."));
        var queued = Queued();

        await ReadAsync(harness.Driver);

        Assert.Equal(AskDaorisStates.Queued, Report().State);
        Assert.Contains("cool until 14:20", Report().Waiting);
        Assert.Empty(harness.Asked);
        Reports.Write(Report() with { Opened = DateTimeOffset.UtcNow.AddHours(-25) });

        await ReadAsync(harness.Driver);

        Assert.Equal(AskDaorisStates.Unread, Report().State);
        Assert.Contains("24 hours", Report().Why);
        Assert.Contains("cool until 14:20", Report().Why);
        Assert.Empty(harness.Asked);
        Assert.Equal(queued.Range, Report().Range);
    }

    [Theory]
    [InlineData("completed", "only words, no block", "no supervisor decision block")]
    [InlineData("completed", "OTHER", "another range")]
    [InlineData("failed", null, "ended failed")]
    public async Task A_malformed_block_a_block_for_another_range_or_a_failed_turn_closes_the_range_unread_with_why(string state, string? words, string why)
    {
        var harness = Set((_, _) => Ended(state, words == "OTHER" ? Answer("r-other") : words));
        Queued();

        await ReadAsync(harness.Driver);

        Assert.Equal(AskDaorisStates.Unread, Report().State);
        Assert.Contains(why, Report().Why);
        Assert.Null(Report().Reader);
    }

    [Fact]
    public async Task A_limited_end_takes_the_next_choice_and_with_none_left_the_range_closes_unread()
    {
        var harness = Set((_, _) => Ended("limited"));
        Queued();

        await ReadAsync(harness.Driver);

        Assert.Single(harness.Asked);
        Assert.Equal(AskDaorisStates.Unread, Report().State);
        Assert.Contains("no choice left of its order", Report().Why);
    }

    [Fact]
    public async Task With_the_check_turned_off_the_queued_ranges_close_unread()
    {
        var harness = Set((_, _) => Ended("completed", Answer()), holder: RoleHolders.Rules);
        Queued();

        await ReadAsync(harness.Driver);

        Assert.Equal(AskDaorisStates.Unread, Report().State);
        Assert.Contains("turned off", Report().Why);
        Assert.Empty(harness.Asked);
    }

    [Fact]
    public async Task One_reading_runs_at_a_time_and_a_restart_closes_a_running_one_unread()
    {
        var harness = Set(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, CancellationTokenSource.CreateLinkedTokenSource(ct, _release.Token).Token);
            return new SuperviseRunEnd("completed", null, null, OpinionPosture.PacketOnly);
        });
        Queued();

        var first = new AskDaorisReadingQueue(harness.Driver.Context, harness.Driver).Offer(CancellationToken.None);
        await Until(() => Report().State == AskDaorisStates.Running);

        Assert.NotNull(first);
        Assert.Null(new AskDaorisReadingQueue(harness.Driver.Context, harness.Driver).Offer(CancellationToken.None));
        new QuestionSupervisorDecisions(_home, NoDecisionAdmission.Instance).SweepAtStart(DateTimeOffset.UtcNow);
        Assert.Equal(AskDaorisStates.Unread, Report().State);
        Assert.Equal(AskDaorisReports.Restarted, Report().Why);
    }

    internal static async Task Until(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < until, "the reading did not get there");
            await Task.Delay(20);
        }
    }
}
