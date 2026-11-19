using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A step cast to the role's agent is the run's working session's to take: it is told the step as its next turn, resumed where it
/// ended, and settles it through its one tool (<see cref="StepSettles"/>) at the settle door as its agent; a decision it holds is answered by
/// its outcome, no confirmation asked; a settle the door or the step's evidence refuses changes nothing and says why. Each seen failing first.
/// </summary>
public sealed class AgentStepSettleTests : NodeRunFixture
{
    private const string ReleaseTail = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"config-needed","kind":"task","version":1,"reach":"pressed","outcomes":["needed","not-needed"],"role":"release-manager"},
                  {"id":"config-change","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"config-changer",
                   "options":{"evidence":["version","capture"]}},
                  {"id":"check-release","kind":"task","version":1,"reach":"pressed","outcomes":["done"],"role":"operations"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>n","kind":"after","from":"claimed","to":"config-needed"},
                  {"id":"n>g","kind":"when","from":"config-needed","to":"config-change","outcome":"needed"},
                  {"id":"n>k","kind":"after","from":"config-needed","to":"check-release"},
                  {"id":"g>k","kind":"after","from":"config-change","to":"check-release"},
                  {"id":"k>h","kind":"when","from":"check-release","to":"handed","outcome":"done"}]}
        """;

    private const string Work = "s-work";

    private readonly List<(string Session, string Text, string Key)> _told = [];

    private IReadOnlyList<SessionRecord> _sessions = [];

    private PlaceCastFiles Casts => new(_home, () => true);

    private static SettleBy Claude(int epoch = 1) => new(CastHolders.AgentNamed("claude"), epoch);

    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")) with { Executors = [Work] }, RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeRuntime Telling()
    {
        var runtime = Runtime();
        runtime.Say = (session, text, key, _) =>
        {
            _told.Add((session, text, key));
            return Task.FromResult(new SayAnswer(true, "kept"));
        };
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(_sessions);
        return runtime;
    }

    /// <summary>The release tail bound, the decision and the change cast to the role's agent, its first step under way.</summary>
    private async Task<NodeRuntime> HandedAsync(bool waiting = true)
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, CastHolders.Agent, "the release path", _now);
        Casts.Cast("machine", "config-changer", CastHolders.Agent, "the release path", _now);
        _sessions = waiting
            ? [new SessionRecord(Work, RunHome.Repository, SessionStates.AwaitingStep) { NoteParts = new StepWait(RunId).Note().Parts }]
            : [new SessionRecord(Work, RunHome.Repository, "completed")];
        BoundTo(ReleaseTail);
        Read();
        var runtime = Telling();
        await RunPassAsync(runtime);
        Read();
        await RunPassAsync(runtime, whole: false);
        Assert.Equal(NodeStates.Active, RunStatus("config-needed").State);
        return runtime;
    }

    private NodeWorded Settle(string node, string? outcome, string? words, SettleBy by, params (string Name, string Value)[] evidence) =>
        StepSettles.Settle(Files, new StepSettleAsk(RunId, node, outcome, words, evidence.ToDictionary(each => each.Name, each => each.Value, StringComparer.Ordinal)), by,
            () => _now);

    private async Task PassesAsync(NodeRuntime runtime)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            Read();
            await RunPassAsync(runtime, whole: false);
        }
    }

    [Fact]
    public async Task A_decision_cast_to_the_agent_is_handed_to_the_working_session_and_its_tool_settles_it_not_needed()
    {
        var runtime = await HandedAsync();

        // Told once, as its next turn: what the step is, its outcomes and the tool that settles it.
        var told = Assert.Single(_told, each => each.Key.Contains("config-needed", StringComparison.Ordinal));
        Assert.Equal(Work, told.Session);
        Assert.Contains(StepSettles.Tool, told.Text, StringComparison.Ordinal);
        Assert.Contains("not-needed", told.Text, StringComparison.Ordinal);
        Read();
        await RunPassAsync(runtime, whole: false);
        Assert.Single(_told, each => each.Key.Contains("config-needed", StringComparison.Ordinal));

        Assert.True(Settle("config-needed", "not-needed", "no setting changes in this release", Claude()).Done);
        await PassesAsync(runtime);

        // The agent's answer settles the decision, no proposal held for a confirmation; the change is passed on the record.
        var decided = RunStatus("config-needed");
        Assert.Equal((NodeStates.Reached, "not-needed", (string?)null), (decided.State, decided.Acceptance!.Outcome, decided.Holds));
        var passed = RunStatus("config-change");
        Assert.Equal((NodeStates.Skipped, "Not needed: decided by agent:claude (no setting changes in this release)"), (passed.State, passed.Words));
        Assert.Equal(NodeStates.Active, RunStatus("check-release").State);
    }

    [Fact]
    public async Task A_refused_settle_changes_nothing_and_says_why()
    {
        var runtime = await HandedAsync();

        // A step not waiting yet takes no settle.
        Assert.Equal(RunNodeWords.NotWaiting, Settle("check-release", null, "checked", Claude()).Refused);

        Assert.True(Settle("config-needed", "needed", "the release adds a setting", Claude()).Done);
        await PassesAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("config-change").State);

        // Its evidence missing: refused naming what is missing, nothing kept.
        var bare = Settle("config-change", null, "changed", Claude(), ("version", "v42"));
        Assert.Equal((StepSettles.MissingEvidence, "capture"), (bare.Refused, bare.Detail));
        Assert.Empty(Files.Read(RunUnit)!.Pending);
        Assert.Contains("capture", StepSettles.Said(bare), StringComparison.Ordinal);

        // Handed to another agent: this one's word is refused by the door, naming who holds it, and nothing is kept.
        Assert.True(StepHolding.Act(Files, Casts, PlaceRiskBook.Unwritten, RunId, "config-change", CastActs.Hand, CastHolders.AgentNamed("alpha"), null, _now, epoch: 1).Done);
        var other = Settle("config-change", null, "changed", new SettleBy(CastHolders.AgentNamed("claude")), ("version", "v42"), ("capture", "https://example.test/c"));
        Assert.Equal(SettleDoor.NotHolder, other.Refused);
        Assert.Contains("agent:alpha", other.Detail, StringComparison.Ordinal);
        Assert.Equal(StepTenureFiles.EndedEpoch, Settle("config-change", null, "changed", Claude(), ("version", "v42"), ("capture", "c")).Refused);
        Assert.Empty(Files.Read(RunUnit)!.Pending);

        var kept = Settle("config-change", null, "changed", new SettleBy(CastHolders.AgentNamed("alpha"), 2), ("version", "v42"), ("capture", "https://example.test/c"));
        Assert.True(kept.Done, kept.Refused);
        await PassesAsync(runtime);
        Assert.Equal((NodeStates.Reached, "changed (version: v42; capture: https://example.test/c)"), (RunStatus("config-change").State, RunStatus("config-change").Words));
    }

    /// <summary>A settle as the connector's <c>step_settle</c> writes it (the service's <c>StepSettleBox</c>, held to the same shape by its tests).</summary>
    private void Asked(string id, string node, string? outcome, string evidence = "{}")
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Sessions, Work + StepSettleAsks.AskSuffix);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""
            {"id":"{{id}}","session":"{{Work}}","run":"{{RunId}}","node":"{{node}}"{{(outcome is null ? "" : $",\"outcome\":\"{outcome}\"")}},
             "words":"no setting changes","epoch":1,"evidence":{{evidence}},"at":"2026-11-18T10:00:00Z"}
            """);
    }

    [Fact]
    public async Task The_tools_settle_is_taken_once_as_the_sessions_agent_and_a_refusal_is_its_next_turn()
    {
        var runtime = await HandedAsync();
        _told.Clear();
        Task<SayAnswer> Say(string session, string text, string key, CancellationToken _)
        {
            _told.Add((session, text, key));
            return Task.FromResult(new SayAnswer(true, "kept"));
        }

        Asked("a1", "config-needed", "maybe");
        var refused = Assert.Single(await StepSettleAsks.TakeAsync(Files, _ => "claude", Say, () => _now, CancellationToken.None));
        Assert.Equal(RunNodeWords.NotOutcome, refused.Refused);
        var told = Assert.Single(_told);
        Assert.Equal((Work, "step-settle:a1"), (told.Session, told.Key));
        Assert.Contains("needed, not-needed", told.Text, StringComparison.Ordinal);
        Assert.Equal("a1", StepSettleAsks.AnsweredId(_home, Work));
        Assert.Empty(await StepSettleAsks.TakeAsync(Files, _ => "claude", Say, () => _now, CancellationToken.None));

        Asked("a2", "config-needed", "not-needed");
        Assert.True(Assert.Single(await StepSettleAsks.TakeAsync(Files, _ => "claude", Say, () => _now, CancellationToken.None)).Done);
        await PassesAsync(runtime);
        Assert.Equal("Not needed: decided by agent:claude (no setting changes)", RunStatus("config-change").Words);
    }

    [Fact]
    public async Task The_working_session_is_told_the_step_where_it_ended()
    {
        await HandedAsync(waiting: false);

        Assert.Equal(Work, Assert.Single(_told, each => each.Key.Contains("config-needed", StringComparison.Ordinal)).Session);
    }

    [Fact]
    public async Task Its_row_in_what_needs_you_offers_the_person_only_take_and_names_the_session()
    {
        await HandedAsync();
        var bound = new GraphRuns(Files).Find(RunId)!;
        var node = bound.Version.Graph!.Nodes.Single(each => each.Id == "config-needed");
        var held = new StepTenureFiles(_home, () => true).Current(RunUnit, "config-needed", RunStatus("config-needed").Attempt);

        var row = HeldSteps.Step(RunId, RunHome.Repository, "default", RunId, node, Files.Read(RunUnit)!, held)!;

        Assert.Equal((StepHolders.Agent, (IReadOnlyList<string>)[]), (row.Holder, row.Presses));
        Assert.Equal([CastActs.Take], row.Holds);
        Assert.Equal(PersonWaits.Task, row.Why);
    }

    [Fact]
    public async Task Its_step_in_the_runs_reading_offers_the_person_only_take_as_its_row_does()
    {
        await HandedAsync();
        var bound = new GraphRuns(Files).Find(RunId)!;
        var digest = bound.Version.GraphDigest!;
        var attempt = RunStatus("config-needed").Attempt;
        var held = new StepTenureFiles(_home, () => true).Current(RunUnit, "config-needed", attempt)!;
        var facts = new WorkflowRunFacts(RunHome.Repository, new CurrentWorkflow([], [], digest))
        {
            Process = WorkflowProcess.Declared(DriverConfig.Empty, RunHome.Repository, "default") with
            {
                Binding = new WorkflowRunBinding(RunId, RunHome.Repository, "default", "docs", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1, Digest = digest },
                Run = RunId,
            },
        };

        var run = WorkflowGraphRuns.Derive(new WorkflowGraphRunFacts(facts, RunId, bound.Version.Graph!, Files.Read(RunUnit)) { Holding = new Dictionary<string, StepTenure> { ["config-needed"] = held } }, "default");
        var step = run.Steps.Single(each => each.Step.Id == "config-needed");

        // The person's one press on a step the agent holds is Take, in the run's reading as in its row: none of the step's own (D318 point 5).
        Assert.Equal((WorkflowRunStates.Waiting, StepHolders.Agent), (step.State, step.Holder?.Kind));
        Assert.Null(step.Acts);
        Assert.Equal([CastActs.Take], step.Holds);
    }
}
