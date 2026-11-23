using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// No worker answers its own gate: a yes to a hold from a working session the run's makers hold is refused <c>own-work</c>
/// at the settle door, read from the word's channel and never its words, whatever holder it speaks as; while the makers are not read, a
/// working session's yes to a hold is refused, since it may be one. Another agent's session holding the step answers it, and a task's own
/// Done, which is work and not a hold, is its worker's. Seen failing first: the refusal covered only the unchecked done's pass.
/// </summary>
public sealed class OwnWorkTests : IDisposable
{
    private const string Run = "q-own";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-ownwork-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly DateTimeOffset _now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private NodeRecordFiles Records => new(_home, () => true);

    /// <summary>A run standing at <paramref name="node"/> (a <paramref name="kind"/> step) under way at its first attempt, its role cast to <c>agent:claude</c>.</summary>
    private void Start(string node, string kind, string role, string extra = "", string holder = "agent:claude", string state = NodeStates.Active)
    {
        new PlaceCastFiles(_home, () => true).Cast("machine", role, holder, "the agents answer it", _now);
        var digest = RunHome.SaveGraph(_home, $$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"{{node}}","kind":"{{kind}}","version":1,"reach":"pressed","role":"{{role}}"{{extra}}},
                      {"id":"handed","kind":"land","version":1,"reach":"performed"}],
             "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"{{node}}"},{"id":"s>l","kind":"after","from":"{{node}}","to":"handed"}]}
            """);
        RunHome.Bind(_home, Run, digest, _now);
        var unit = RunUnits.IdOf(Run);
        Records.Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = NodeLifecycle.Start(unit, "claimed") with { State = NodeStates.Reached, Attempt = 1, Since = _now },
                [node] = NodeLifecycle.Start(unit, node) with
                {
                    State = state, Attempt = 1, Tries = 1, Since = _now, Binding = new NodeAcceptance(unit, "c1", digest, node, 1, 1),
                },
            },
        });
        AttemptTenures.Keep(Records, unit, new GraphRuns(Records).Find(Run)!.Version.Graph!.Nodes, "work", null, null, _now);
    }

    private NodeWorded Word(string node, string act, string session, IReadOnlyCollection<string>? makers) =>
        RunNodeWords.Word(Records, Run, node, act, "Read it: it does what was asked.", () => _now,
            settle: new SettleBy("agent:claude", 1) { Session = session }, workers: makers);

    [Fact]
    public void A_reviews_accept_from_the_works_own_session_is_refused_and_another_agents_is_kept()
    {
        Start("review", NodeKinds.Review, StepRoles.Acceptor);

        var own = Word("review", NodeWordActs.Accept, "s-worker", ["s-helper", "s-worker"]);
        var unread = Word("review", NodeWordActs.Accept, "s-reviewer", null);
        var other = Word("review", NodeWordActs.Accept, "s-reviewer", ["s-helper", "s-worker"]);

        Assert.Equal(SettleDoor.OwnWork, own.Refused);
        Assert.Contains("one of the work's makers", own.Detail, StringComparison.Ordinal);
        Assert.Equal(SettleDoor.OwnWork, unread.Refused);
        Assert.True(other.Done, other.Refused);
    }

    [Theory]
    [InlineData(NodeStates.Blocked)]
    [InlineData(NodeStates.Ready)]
    public void An_early_accept_on_a_review_not_begun_is_refused_from_the_works_own_session_and_kept_from_another(string state)
    {
        Start("review", NodeKinds.Review, StepRoles.Acceptor, state: state);

        var own = Word("review", NodeWordActs.Accept, "s-worker", ["s-worker"]);
        var unread = Word("review", NodeWordActs.Accept, "s-worker", null);
        var other = Word("review", NodeWordActs.Accept, "s-reviewer", ["s-worker"]);

        Assert.Equal(SettleDoor.OwnWork, own.Refused);
        Assert.Equal(SettleDoor.OwnWork, unread.Refused);
        Assert.Empty(Records.Read(RunUnits.IdOf(Run))!.Pending.Where(word => word.Node == "review" && word.Act == NodeWordActs.Accept && own.Done));
        Assert.True(other.Done, other.Refused);
    }

    [Fact]
    public void An_approval_from_the_works_own_session_is_refused()
    {
        Start("sign", NodeKinds.Task, StepRoles.Approver, ""","outcomes":["approved"]""");
        Assert.Equal(SettleDoor.OwnWork, Word("sign", NodeWordActs.Approve, "s-worker", ["s-worker"]).Refused);
    }

    [Fact]
    public void The_bare_agent_holder_is_refused_a_makers_session_and_ids_compare_without_case()
    {
        Start("review", NodeKinds.Review, StepRoles.Acceptor, holder: CastHolders.Agent);

        var own = RunNodeWords.Word(Records, Run, "review", NodeWordActs.Accept, "Read it.", () => _now,
            settle: new SettleBy(CastHolders.Agent, 1) { Session = "S-Worker" }, workers: ["s-worker"]);

        Assert.Equal(SettleDoor.OwnWork, own.Refused);
        Assert.Contains("one of the work's makers", own.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_helper_a_maker_started_and_its_own_helpers_are_makers()
    {
        var helpers = new HelperFiles(_home);
        var first = helpers.WriteAsk("s-worker", "Which door?", null, _now);
        helpers.WriteState(new HelperState(first, HelperStates.Answered, null, "s-helper", _now));
        var second = helpers.WriteAsk("S-Helper", "And its test?", null, _now);
        helpers.WriteState(new HelperState(second, HelperStates.Started, null, "s-deeper", _now));
        var other = helpers.WriteAsk("s-stranger", "Anything?", null, _now);
        helpers.WriteState(new HelperState(other, HelperStates.Answered, null, "s-unrelated", _now));

        var makers = RunWorkers.WithHelpers(_home, ["s-worker", "S-WORKER"]);

        Assert.Equal(["s-worker", "s-helper", "s-deeper"], makers);
    }

    [Fact]
    public async Task A_settle_while_the_makers_are_not_read_is_not_taken_and_stays_open()
    {
        Start("review", NodeKinds.Review, StepRoles.Acceptor, holder: CastHolders.Agent);
        var path = DaorisHome.Entry(_home, HomeEntry.Sessions, "s-reviewer" + StepSettleAsks.AskSuffix);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""{"id":"a1","run":"{{Run}}","node":"review","outcome":"accepted","words":"Read it.","epoch":1}""");
        var told = new List<(string Text, string Key)>();
        Task<SayAnswer> Say(string session, string text, string key, CancellationToken ct)
        {
            told.Add((text, key));
            return Task.FromResult(new SayAnswer(true, "kept"));
        }

        var unread = await StepSettleAsks.TakeAsync(Records, _ => null, Say, () => _now, CancellationToken.None, _ => null);

        Assert.Empty(unread);
        Assert.Single(StepSettleAsks.Waiting(_home));
        Assert.Null(StepSettleAsks.AnsweredId(_home, "s-reviewer"));
        Assert.Contains(SettleDoor.NotYet, Assert.Single(told).Text, StringComparison.Ordinal);

        var read = await StepSettleAsks.TakeAsync(Records, _ => null, Say, () => _now, CancellationToken.None, _ => ["s-worker"]);

        Assert.True(Assert.Single(read).Done);
        Assert.Empty(StepSettleAsks.Waiting(_home));
    }

    [Fact]
    public void A_tasks_own_done_is_work_and_its_workers()
    {
        Start("deploy", NodeKinds.Task, "operations", ""","outcomes":["done"]""");
        var done = Word("deploy", NodeWordActs.Done, "s-worker", ["s-worker"]);
        Assert.NotEqual(SettleDoor.OwnWork, done.Refused);
    }
}
