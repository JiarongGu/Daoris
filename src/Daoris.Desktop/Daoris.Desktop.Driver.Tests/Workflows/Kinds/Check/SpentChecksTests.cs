using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check red past its rounds is the act <c>work.recover</c>: with nothing configured nobody else holds it, so it reaches the owner's final
/// exit, one notice with its story kept under its own key and one journaled line, then the person's own row, never stopped by Daoris; classed
/// <c>role</c> the role its failure is kept on holds it, by its cast or else its agent, and a holder no live session serves makes it a vacancy
/// that reaches the exit past its bound; classed <c>owner</c>, or with no policy read, the person holds it with no exit noticed. The decision
/// is judged again at each look until it reaches the exit.
/// </summary>
public sealed class SpentChecksTests : NodeRunFixture
{
    private const string Checked = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"check","kind":"check","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>k","kind":"after","from":"claimed","to":"check"},
                  {"id":"k>h","kind":"after","from":"check","to":"handed"},
                  {"id":"k>c","kind":"back","from":"check","to":"claimed","on":"failed"}]}
        """;

    private const string Role = """{"risk":{"machine":[{"kind":"work.recover","class":"role","by":"reviewer"}]}}""";

    private const string RoleBound = """{"risk":{"machine":[{"kind":"work.recover","class":"role"}]},"vacancyHours":2}""";

    private const string Seed = """{"risk":{}}""";

    private static readonly NodeDeclaration Check = new("check", NodeKinds.Check, 1, NodeReach.Performed);

    private PlaceCastFiles Casts => new(_home, () => true);

    private static NodeAttempt Red(int attempt) =>
        new("check", attempt, NodeKinds.Check, new WorkPerformCall(RunUnit, NodeKinds.Check, 1, $"i-{attempt}", $"k-{attempt}", Tip1, [], DateTimeOffset.UnixEpoch),
            AttemptStates.Settled) { Outcome = WorkStates.Failed };

    private void Spent()
    {
        BoundTo(Checked);
        Files.Write(new NodeRecord(RunUnit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = new(RunUnit, "claimed", NodeStates.Reached) { Attempt = 1 },
                ["check"] = new(RunUnit, "check", NodeStates.Failed) { Attempt = 3, Code = "gate-failed", Since = _now.AddHours(-1) },
            },
            Attempts = [Red(1), Red(2), Red(3)],
            Told = [new NodeTold("check", 1, "s1", null, _now.AddHours(-5))],
        });
    }

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    private Task<bool> SettleAsync(string? config, List<string> said, List<string>? told = null, List<LookLine>? noticed = null) =>
        SpentChecks.SettleAsync(Files, RunUnit, RunId, "check", 3, 3, "gate-failed", "the gate `cli` failed", config is null ? null : Config(config), "default",
            RunHome.Repository, _now, text =>
            {
                told?.Add(text);
                return Task.FromResult<(string?, string?)>(("s1", null));
            }, said, noticed ?? []);

    private static string Story(LookLine line)
    {
        Assert.Equal(LookSaid.FinalExit, line.Look.News?.Code);
        return line.Text;
    }

    [Fact]
    public async Task With_nothing_configured_the_spent_red_reaches_the_owners_final_exit_once_and_waits_on_the_person_never_stopped()
    {
        Spent();
        var said = new List<string>();
        var told = new List<string>();
        var noticed = new List<LookLine>();

        Assert.True(await SettleAsync(Seed, said, told, noticed), string.Join(" | ", said));

        var record = Files.Read(RunUnit)!;
        Assert.Empty(record.Pending);
        Assert.Equal(NodeStates.Failed, record.Statuses["check"].State);
        var kept = record.ToldOf("check", 3)!;
        Assert.Equal((true, NodeTold.Exit, "s1"), (kept.Spent, kept.Decided, kept.Session));
        var line = Assert.Single(noticed);
        Assert.Equal(FinalExits.Key(RunId, "check", 3), line.Look.News?.Subject);
        var notice = Story(line);
        Assert.StartsWith("final exit, to you:", notice, StringComparison.Ordinal);
        Assert.Contains("held it for 5 hours", notice, StringComparison.Ordinal);
        Assert.Contains("3 red rounds (`gate-failed`)", notice, StringComparison.Ordinal);
        Assert.Contains("the last red: the gate `cli` failed", notice, StringComparison.Ordinal);
        Assert.Contains("your one answer: mend it and run the check again, or end this work", notice, StringComparison.Ordinal);
        Assert.DoesNotContain(said, each => each.Contains("final exit", StringComparison.Ordinal) || each.Contains("gave it up", StringComparison.Ordinal));
        Assert.Contains("final exit", Assert.Single(told), StringComparison.Ordinal);
        var journaled = Assert.Single(RiskJournal.Read(_home));
        Assert.Equal((RiskActs.WorkRecover, RiskVerdict.Pass, RunId, "check", SpentChecks.Today), (journaled.Kind, journaled.Outcome, journaled.Run, journaled.Step, journaled.Today));
        var wait = StepWaiting.Read(record, Check, record.Statuses["check"], cast: _ => CastHolders.AgentPrefix + "claude");
        Assert.Equal((PersonWaits.Left, SpentChecks.HeldOn, StepHolders.Person), (wait?.Why, wait?.Role, wait?.Holder.Kind));
    }

    /// <summary>The exit's notice goes through the look's one notice path, kept once under its own key, naming its run and step; a restarted driver's story is said never again.</summary>
    [Fact]
    public async Task The_final_exits_notice_reaches_the_owner_in_done_and_failed_once_across_a_restart()
    {
        Spent();
        var first = await RunPassAsync(Runtime(), Seed);
        var look = new LookSaid(_home, () => null, clock: () => _now);
        foreach (var line in first.Noticed) look.Say(line);
        look.Stands("work", first.Standing);

        var restarted = await RunPassAsync(Runtime(), Seed);
        var again = new LookSaid(_home, () => null, clock: () => _now);
        foreach (var line in restarted.Noticed) again.Say(line);
        again.Stands("work", restarted.Standing);

        Assert.Single(first.Noticed);
        Assert.Empty(restarted.Noticed);
        Assert.DoesNotContain(first.Said.Concat(restarted.Said), line => line.Contains("final exit", StringComparison.Ordinal));
        var notice = Assert.Single(Notices.Read(_home, _now)!);
        Assert.Equal((Notices.Look, LookSaid.FinalExit, $"look-{LookSaid.FinalExit}-{FinalExits.Key(RunId, "check", 3)}", RunId, "check"),
            (notice.Kind, notice.Code, notice.Id, notice.Quest, notice.Act));
    }

    /// <summary>
    /// A notice the look never kept (a later unit's failure ended the look after the exit's keep, or the notices refused the write) is handed
    /// up again at each look while the exit stands, kept then, and never doubled; one the person dismissed is not brought back.
    /// </summary>
    [Fact]
    public async Task A_final_exits_notice_dropped_once_is_kept_at_the_next_look_and_never_doubled()
    {
        Spent();
        var runtime = Runtime();
        var dropped = await RunPassAsync(runtime, Seed);
        Assert.Single(dropped.Noticed);
        Assert.Null(Notices.Read(_home, _now) is { Count: > 0 } ? "kept" : null);

        var look = new LookSaid(_home, () => null, clock: () => _now);
        var next = await RunPassAsync(runtime, Seed);
        Assert.Empty(next.Noticed);
        var standing = Assert.Single(next.Standing);
        Assert.Equal((LookSaid.FinalExit, FinalExits.Key(RunId, "check", 3), RunId, "check"), (standing.Code, standing.Subject, standing.Quest, standing.Act));
        look.Stands("work", next.Standing);
        var kept = Assert.Single(Notices.Read(_home, _now)!);

        foreach (var line in dropped.Noticed) look.Say(line);
        look.Stands("work", (await RunPassAsync(runtime, Seed)).Standing);
        Assert.Equal([kept.Id], Notices.Read(_home, _now)!.Select(each => each.Id));

        Assert.Null(Notices.Dismiss(_home, [kept.Id], _now));
        look.Stands("work", (await RunPassAsync(runtime, Seed)).Standing);
        Assert.Empty(Notices.Read(_home, _now)!);
    }

    [Fact]
    public async Task A_final_exits_notice_the_owner_dismissed_is_not_kept_again_after_a_restart()
    {
        Spent();
        var first = await RunPassAsync(Runtime(), Seed);
        var look = new LookSaid(_home, () => null, clock: () => _now);
        foreach (var line in first.Noticed) look.Say(line);
        look.Stands("work", first.Standing);
        var kept = Assert.Single(Notices.Read(_home, _now)!);
        Assert.Null(Notices.Dismiss(_home, [kept.Id], _now));

        var restarted = await RunPassAsync(Runtime(), Seed);
        var again = new LookSaid(_home, () => null, clock: () => _now);
        Assert.Single(restarted.Standing);
        again.Stands("work", restarted.Standing);

        Assert.Empty(Notices.Read(_home, _now)!);
    }

    /// <summary>At the exit the step is the person's own row with the presses that answer it, and the readers the window uses read it as the person's.</summary>
    [Fact]
    public async Task At_the_final_exit_the_spent_red_is_the_persons_own_row_with_its_presses()
    {
        Spent();
        var runtime = Runtime();
        await RunPassAsync(runtime, Seed);

        var row = Assert.Single(runtime.HeldSteps(), step => step.Node == "check");
        Assert.Equal((StepHolders.Person, PersonWaits.Left, FinalExits.Key(RunId, "check", 3), FinalExits.Spent), (row.Holder, row.Why, row.Exit?.Key, row.Exit?.Kind));
        Assert.Contains(NodeWordActs.Again, row.Presses);
        Assert.Contains(NodeWordActs.Stop, row.Presses);
        var wait = Assert.Single(runtime.PersonWaits(), each => each.Node == "check");
        Assert.Equal((PersonWaits.Left, false), (wait.Why, wait.Vacant));
    }

    [Fact]
    public async Task Classed_role_the_role_its_failure_is_kept_on_holds_it_by_the_cast_or_else_its_agent_never_the_exit_at_once()
    {
        var verdict = SpentChecks.Verdict(Config(Role), "default", RunHome.Repository)!;
        var cast = new CastNow(SpentChecks.HeldOn, CastHolders.AgentPrefix + "claude", "cast", "machine");

        Assert.Equal(RiskVerdict.Gate, verdict.Outcome);
        Assert.Equal(NodeTold.RoleHeld, SpentChecks.Decide(verdict));
        var words = SpentChecks.Words("check", null, null, 3, NodeTold.RoleHeld, cast);
        Assert.Contains($"the role `{SpentChecks.HeldOn}` holds it now, held by {CastHolders.AgentPrefix}claude", words, StringComparison.Ordinal);
        Assert.DoesNotContain("reviewer", words, StringComparison.Ordinal);
        Assert.Contains("listed under Vacant", SpentChecks.Words("check", null, null, 3, NodeTold.RoleHeld, cast with { Why = CastWhy.Default }), StringComparison.Ordinal);

        Spent();
        var said = new List<string>();
        var noticed = new List<LookLine>();
        Assert.True(await SettleAsync(Role, said, noticed: noticed));
        Assert.Equal(NodeTold.RoleHeld, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.Empty(noticed);
    }

    [Fact]
    public void A_check_given_up_as_said_is_told_as_one_that_could_not_say_and_never_as_a_red_round()
    {
        var words = SpentChecks.Words("check", UnrunChecks.GaveUp, "the verification could not be kept (the log is in use)", CheckRounds.Bound, NodeTold.RoleHeld,
            new CastNow(SpentChecks.HeldOn, CastHolders.AgentPrefix + "claude", "cast", "machine"));

        Assert.Contains("could not say whether your work passes", words, StringComparison.Ordinal);
        Assert.Contains("the log is in use", words, StringComparison.Ordinal);
        Assert.DoesNotContain("red round", words, StringComparison.Ordinal);
        Assert.Contains("could not say whether the work passes", SpentChecks.Notice("1", "check", CheckRounds.Bound, UnrunChecks.GaveUp, "why", null, null, DateTimeOffset.UnixEpoch), StringComparison.Ordinal);
    }

    /// <summary>A role's holder no live session serves makes the spent red a vacancy: listed and announced once, then at the final exit past its bound.</summary>
    [Fact]
    public async Task A_role_held_spent_red_no_live_session_serves_is_a_vacancy_then_past_its_bound_the_final_exit()
    {
        Spent();
        Assert.Null(AgentSettles.CastAt(Casts, "machine", SpentChecks.HeldOn, CastHolders.AgentPrefix + "x", "x supervises", _now));
        var runtime = Runtime();
        runtime.Serving = (_, _) => false;

        var first = await RunPassAsync(runtime, RoleBound);
        Assert.Equal(NodeTold.RoleHeld, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.Single(first.Said, line => line.Contains("step `check`", StringComparison.Ordinal) && line.Contains("is vacant:", StringComparison.Ordinal));
        Assert.Empty(first.Noticed);
        var vacant = Assert.Single(runtime.PersonWaits(), each => each.Node == "check");
        Assert.True(vacant.Vacant);

        _now = _now.AddHours(3);
        var past = await RunPassAsync(runtime, RoleBound);
        var notice = Story(Assert.Single(past.Noticed));
        Assert.Contains("stood vacant", notice, StringComparison.Ordinal);
        Assert.Contains(RiskJournal.Read(_home), line => line.Key == $"vacant:{RunId}/check#3" && line.Today == SpentChecks.Today);
        Assert.Equal(FinalExits.Vacant, Assert.Single(runtime.HeldSteps(), step => step.Node == "check").Exit?.Kind);

        var after = await RunPassAsync(runtime, RoleBound);
        Assert.Empty(after.Noticed);
        Assert.DoesNotContain(after.Said, line => line.Contains("vacant", StringComparison.Ordinal));
    }

    /// <summary>The decision is judged again at its release: a cast removed leaves the role to its agent, which no session serves, so it stands vacant.</summary>
    [Fact]
    public async Task A_cast_removed_after_the_decision_is_judged_again_and_the_spent_red_stands_vacant()
    {
        Spent();
        Assert.Null(AgentSettles.CastAt(Casts, "machine", SpentChecks.HeldOn, CastHolders.AgentPrefix + "x", "x supervises", _now));
        var runtime = Runtime();
        runtime.Serving = (agent, _) => agent == "x";

        var served = await RunPassAsync(runtime, RoleBound);
        Assert.DoesNotContain(served.Said, line => line.Contains("vacant", StringComparison.Ordinal));
        Assert.DoesNotContain(runtime.PersonWaits(), each => each.Node == "check");

        Assert.Null(AgentSettles.CastAt(Casts, "machine", SpentChecks.HeldOn, null, "cast removed", _now));
        var removed = await RunPassAsync(runtime, RoleBound);
        Assert.Single(removed.Said, line => line.Contains("step `check`", StringComparison.Ordinal) && line.Contains("is vacant:", StringComparison.Ordinal));
        Assert.True(Assert.Single(runtime.PersonWaits(), each => each.Node == "check").Vacant);

        // And a policy lowered moves the decision itself: the seed asks nobody, so it reaches the exit at the next look.
        var lowered = await RunPassAsync(runtime, Seed);
        Assert.Equal(NodeTold.Exit, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.Single(lowered.Noticed);
    }

    [Fact]
    public async Task A_role_held_spent_red_a_live_session_serves_but_never_answers_reaches_the_final_exit_past_its_bound()
    {
        Spent();
        Assert.Null(AgentSettles.CastAt(Casts, "machine", SpentChecks.HeldOn, CastHolders.AgentPrefix + "x", "x supervises", _now));
        var runtime = Runtime();
        runtime.Serving = (agent, _) => agent == "x";

        await RunPassAsync(runtime, RoleBound);
        _now = _now.AddHours(3);
        var past = await RunPassAsync(runtime, RoleBound);

        Assert.Contains("stood unanswered", Story(Assert.Single(past.Noticed)), StringComparison.Ordinal);
        Assert.Equal(FinalExits.Unanswered, Assert.Single(runtime.HeldSteps(), step => step.Node == "check").Exit?.Kind);
    }

    [Fact]
    public async Task A_holder_whose_session_comes_and_goes_reaches_the_final_exit_at_its_bound_from_the_first_look()
    {
        Spent();
        Assert.Null(AgentSettles.CastAt(Casts, "machine", SpentChecks.HeldOn, CastHolders.AgentPrefix + "x", "x supervises", _now));
        var runtime = Runtime();
        var serving = false;
        runtime.Serving = (agent, _) => serving && agent == "x";

        await RunPassAsync(runtime, RoleBound);
        _now = _now.AddHours(1);
        serving = true;
        await RunPassAsync(runtime, RoleBound);
        _now = _now.AddMinutes(30);
        serving = false;
        var back = await RunPassAsync(runtime, RoleBound);
        Assert.Empty(back.Noticed);

        _now = _now.AddHours(1);
        var past = await RunPassAsync(runtime, RoleBound);

        Assert.Contains("held it for 2 hours", Story(Assert.Single(past.Noticed)), StringComparison.Ordinal);
        Assert.NotNull(Assert.Single(runtime.HeldSteps(), step => step.Node == "check").Exit);
    }

    [Fact]
    public async Task A_final_exit_keeps_its_row_when_the_cast_moves_the_step_to_the_person()
    {
        Spent();
        Assert.Null(AgentSettles.CastAt(Casts, "machine", SpentChecks.HeldOn, CastHolders.AgentPrefix + "x", "x supervises", _now));
        var runtime = Runtime();
        runtime.Serving = (_, _) => false;
        await RunPassAsync(runtime, RoleBound);
        _now = _now.AddHours(3);
        Assert.Single((await RunPassAsync(runtime, RoleBound)).Noticed);
        Assert.Equal(FinalExits.Vacant, Assert.Single(runtime.HeldSteps(), step => step.Node == "check").Exit?.Kind);

        Assert.Null(AgentSettles.CastAt(Casts, "machine", SpentChecks.HeldOn, CastHolders.Person, "the owner takes it", _now));
        var moved = await RunPassAsync(runtime, RoleBound);

        Assert.Empty(moved.Noticed);
        var row = Assert.Single(runtime.HeldSteps(), step => step.Node == "check");
        Assert.Equal((FinalExits.Vacant, FinalExits.Key(RunId, "check", 3), StepHolders.Person), (row.Exit?.Kind, row.Exit?.Key, row.Holder));
        Assert.True(Assert.Single(runtime.PersonWaits(), each => each.Node == "check").Exit);
    }

    [Fact]
    public async Task A_person_wait_at_the_final_exit_says_so()
    {
        Spent();
        var runtime = Runtime();
        await RunPassAsync(runtime, Seed);

        Assert.True(Assert.Single(runtime.PersonWaits(), each => each.Node == "check").Exit);
    }

    [Fact]
    public async Task A_decision_whose_write_is_refused_tells_and_keeps_nothing_is_said_once_and_is_decided_again_until_kept()
    {
        Spent();
        var unleased = new NodeRecordFiles(_home, () => false);
        var said = new List<string>();
        var told = new List<string>();
        var noticed = new List<LookLine>();

        Assert.False(await SpentChecks.SettleAsync(unleased, RunUnit, RunId, "check", 3, 3, null, null, Config(Seed), "default", RunHome.Repository, _now,
            text => { told.Add(text); return Task.FromResult<(string?, string?)>(("s1", null)); }, said, noticed));
        Assert.False(await SpentChecks.SettleAsync(unleased, RunUnit, RunId, "check", 3, 3, null, null, Config(Seed), "default", RunHome.Repository, _now,
            text => { told.Add(text); return Task.FromResult<(string?, string?)>(("s1", null)); }, said, noticed));

        Assert.Null(Files.Read(RunUnit)!.ToldOf("check", 3));
        Assert.Single(said, line => line.Contains("could not be kept", StringComparison.Ordinal));
        Assert.Empty(noticed);
        Assert.Empty(RiskJournal.Read(_home));

        Assert.True(await SettleAsync(Seed, said, noticed: noticed));
        Assert.Equal(NodeTold.Exit, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.Single(noticed);
    }

    [Fact]
    public async Task A_red_spent_and_told_before_this_build_is_decided_at_its_next_look_noticed_and_journaled_never_told_twice()
    {
        Spent();
        Files.Edit(RunUnit, record => (record with { Told = [.. record.Told, new NodeTold("check", 3, "s1", null, _now.AddHours(-1)) { Spent = true }] }, true));
        var said = new List<string>();
        var told = new List<string>();
        var noticed = new List<LookLine>();

        Assert.True(await SettleAsync(Seed, said, told, noticed));

        Assert.Empty(told);
        var kept = Files.Read(RunUnit)!.ToldOf("check", 3)!;
        Assert.Equal((NodeTold.Exit, "s1"), (kept.Decided, kept.Session));
        Assert.StartsWith("final exit, to you:", Story(Assert.Single(noticed)), StringComparison.Ordinal);
        Assert.Equal(SpentChecks.Today, Assert.Single(RiskJournal.Read(_home)).Today);
    }

    [Fact]
    public async Task A_journal_that_will_not_append_never_keeps_the_final_exit_from_the_owner()
    {
        Spent();
        Directory.CreateDirectory(RiskJournal.PathOf(_home));
        var noticed = new List<LookLine>();

        Assert.True(await SettleAsync(Seed, [], noticed: noticed));

        Assert.Equal(NodeTold.Exit, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.Single(noticed);
    }

    [Fact]
    public async Task Classed_owner_or_with_no_policy_read_the_person_holds_it_and_no_final_exit_is_noticed()
    {
        Spent();
        var said = new List<string>();
        var noticed = new List<LookLine>();

        Assert.True(await SettleAsync("""{"risk":{"machine":[{"kind":"work.recover","class":"owner"}]}}""", said, noticed: noticed));
        Assert.Equal(NodeTold.OwnerHeld, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.True(await SettleAsync(null, said, noticed: noticed));

        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Empty(noticed);
        Assert.Empty(RiskJournal.Read(_home));
        Assert.Contains("the person sees it too", SpentChecks.Words("check", null, null, 3, NodeTold.OwnerHeld, null), StringComparison.Ordinal);
    }
}
