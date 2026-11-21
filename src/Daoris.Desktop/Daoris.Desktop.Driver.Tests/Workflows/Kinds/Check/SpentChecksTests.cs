using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A check red past its rounds is the act <c>work.recover</c>: with nothing configured nobody else holds it, so it reaches the owner's final
/// exit, one notice with its story and one journaled line, then the person's row, never stopped by Daoris; classed <c>role</c> the role its
/// failure is kept on holds it, named with its holder, and the final exit follows only where no one is cast to it; classed <c>owner</c>, or
/// with no policy read, the person holds it with no exit noticed.
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

    private static readonly NodeDeclaration Check = new("check", NodeKinds.Check, 1, NodeReach.Performed);

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
                ["check"] = new(RunUnit, "check", NodeStates.Failed) { Attempt = 3, Code = "gate-failed" },
            },
            Attempts = [Red(1), Red(2), Red(3)],
            Told = [new NodeTold("check", 1, "s1", null, _now.AddHours(-5))],
        });
    }

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    private Task<bool> SettleAsync(string? config, List<string> said, List<string>? told = null) =>
        SpentChecks.SettleAsync(Files, RunUnit, RunId, "check", 3, 3, "gate-failed", "the gate `cli` failed", config is null ? null : Config(config), "default",
            RunHome.Repository, _now, text =>
            {
                told?.Add(text);
                return Task.FromResult<(string?, string?)>(("s1", null));
            }, said);

    [Fact]
    public async Task With_nothing_configured_the_spent_red_reaches_the_owners_final_exit_once_and_waits_on_the_person_never_stopped()
    {
        Spent();
        var said = new List<string>();
        var told = new List<string>();

        Assert.True(await SettleAsync("""{"risk":{}}""", said, told), string.Join(" | ", said));

        var record = Files.Read(RunUnit)!;
        Assert.Empty(record.Pending);
        Assert.Equal(NodeStates.Failed, record.Statuses["check"].State);
        var kept = record.ToldOf("check", 3)!;
        Assert.Equal((true, NodeTold.Exit, "s1"), (kept.Spent, kept.Decided, kept.Session));
        var notice = Assert.Single(said, line => line.StartsWith("final exit, to you:", StringComparison.Ordinal));
        Assert.Contains("held it for 5 hours", notice, StringComparison.Ordinal);
        Assert.Contains("3 red rounds (`gate-failed`)", notice, StringComparison.Ordinal);
        Assert.Contains("the last red: the gate `cli` failed", notice, StringComparison.Ordinal);
        Assert.Contains("your one answer: mend it and run the check again, or end this work", notice, StringComparison.Ordinal);
        Assert.DoesNotContain(said, line => line.Contains("gave it up", StringComparison.Ordinal));
        Assert.Contains("final exit", Assert.Single(told), StringComparison.Ordinal);
        var line = Assert.Single(RiskJournal.Read(_home));
        Assert.Equal((RiskActs.WorkRecover, RiskVerdict.Pass, RunId, "check", SpentChecks.Today), (line.Kind, line.Outcome, line.Run, line.Step, line.Today));
        var wait = StepWaiting.Read(record, Check, record.Statuses["check"], cast: _ => CastHolders.AgentPrefix + "claude");
        Assert.Equal((PersonWaits.Left, SpentChecks.HeldOn, StepHolders.Person), (wait?.Why, wait?.Role, wait?.Holder.Kind));
    }

    [Fact]
    public async Task Classed_role_the_role_its_failure_is_kept_on_holds_it_by_the_cast_and_the_final_exit_follows_only_where_none_is_cast()
    {
        var verdict = SpentChecks.Verdict(Config(Role), "default", RunHome.Repository)!;
        var cast = new CastNow(SpentChecks.HeldOn, CastHolders.AgentPrefix + "claude", "cast", "machine");

        Assert.Equal(RiskVerdict.Gate, verdict.Outcome);
        Assert.Equal(NodeTold.RoleHeld, SpentChecks.Decide(verdict, cast));
        Assert.Equal(NodeTold.Exit, SpentChecks.Decide(verdict, cast with { Why = CastWhy.Default }));
        var words = SpentChecks.Words("check", null, null, 3, NodeTold.RoleHeld, cast);
        Assert.Contains($"the role `{SpentChecks.HeldOn}` holds it now, held by {CastHolders.AgentPrefix}claude", words, StringComparison.Ordinal);
        Assert.DoesNotContain("reviewer", words, StringComparison.Ordinal);

        Spent();
        var said = new List<string>();
        Assert.True(await SettleAsync(Role, said));
        Assert.Equal(NodeTold.Exit, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.Single(said, line => line.Contains($"the role `{SpentChecks.HeldOn}` held it and could not: no cast names a holder", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_decision_whose_write_is_refused_tells_and_keeps_nothing_is_said_once_and_is_decided_again_until_kept()
    {
        Spent();
        var unleased = new NodeRecordFiles(_home, () => false);
        var said = new List<string>();
        var told = new List<string>();

        Assert.False(await SpentChecks.SettleAsync(unleased, RunUnit, RunId, "check", 3, 3, null, null, Config("""{"risk":{}}"""), "default", RunHome.Repository, _now,
            text => { told.Add(text); return Task.FromResult<(string?, string?)>(("s1", null)); }, said));
        Assert.False(await SpentChecks.SettleAsync(unleased, RunUnit, RunId, "check", 3, 3, null, null, Config("""{"risk":{}}"""), "default", RunHome.Repository, _now,
            text => { told.Add(text); return Task.FromResult<(string?, string?)>(("s1", null)); }, said));

        Assert.Null(Files.Read(RunUnit)!.ToldOf("check", 3));
        Assert.Single(said, line => line.Contains("could not be kept", StringComparison.Ordinal));
        Assert.DoesNotContain(said, line => line.Contains("final exit, to you:", StringComparison.Ordinal));
        Assert.Empty(RiskJournal.Read(_home));

        Assert.True(await SettleAsync("""{"risk":{}}""", said));
        Assert.Equal(NodeTold.Exit, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.Single(said, line => line.Contains("final exit, to you:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_red_spent_and_told_before_this_build_is_decided_at_its_next_look_noticed_and_journaled_never_told_twice()
    {
        Spent();
        Files.Edit(RunUnit, record => (record with { Told = [.. record.Told, new NodeTold("check", 3, "s1", null, _now.AddHours(-1)) { Spent = true }] }, true));
        var said = new List<string>();
        var told = new List<string>();

        Assert.True(await SettleAsync("""{"risk":{}}""", said, told));

        Assert.Empty(told);
        var kept = Files.Read(RunUnit)!.ToldOf("check", 3)!;
        Assert.Equal((NodeTold.Exit, "s1"), (kept.Decided, kept.Session));
        Assert.Single(said, line => line.StartsWith("final exit, to you:", StringComparison.Ordinal));
        Assert.Equal(SpentChecks.Today, Assert.Single(RiskJournal.Read(_home)).Today);
    }

    [Fact]
    public async Task A_journal_that_will_not_append_never_keeps_the_final_exit_from_the_owner()
    {
        Spent();
        Directory.CreateDirectory(RiskJournal.PathOf(_home));
        var said = new List<string>();

        Assert.True(await SettleAsync("""{"risk":{}}""", said));

        Assert.Equal(NodeTold.Exit, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.Single(said, line => line.Contains("final exit, to you:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Classed_owner_or_with_no_policy_read_the_person_holds_it_and_no_final_exit_is_noticed()
    {
        Spent();
        var said = new List<string>();

        Assert.True(await SettleAsync("""{"risk":{"machine":[{"kind":"work.recover","class":"owner"}]}}""", said));
        Assert.Equal(NodeTold.OwnerHeld, Files.Read(RunUnit)!.ToldOf("check", 3)!.Decided);
        Assert.True(await SettleAsync(null, said));

        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.DoesNotContain(said, line => line.Contains("final exit", StringComparison.Ordinal));
        Assert.Empty(RiskJournal.Read(_home));
        Assert.Contains("the person sees it too", SpentChecks.Words("check", null, null, 3, NodeTold.OwnerHeld, null), StringComparison.Ordinal);
    }
}
