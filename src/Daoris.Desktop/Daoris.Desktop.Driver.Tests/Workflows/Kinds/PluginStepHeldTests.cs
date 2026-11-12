using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A failed plugin step is tried again one way: its run step carries the failed attempt as held, with <c>again</c>, the word the
/// person's door on a held attempt takes (<c>WORK_HELD</c>), as a check's step does; the run step offers no act of its own for it.
/// </summary>
public sealed class PluginStepHeldTests : NodeRunFixture
{
    private const string Digest = "gph-0123456789abcdef0123456789abcdef";

    private const string ClaimedThenDeploy = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/pull-request","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    private static readonly NodeDeclaration Deploy = new("deploy", "acme.deploy/pull-request", 1, NodeReach.Performed);

    private NodeRecord Failed(string? resolution = null) => new(RunUnit)
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["claimed"] = new(RunUnit, "claimed", NodeStates.Reached) { Attempt = 1 },
            ["deploy"] = new(RunUnit, "deploy", NodeStates.Failed) { Attempt = 1, Tries = 1, Code = "no-token" },
        },
        Attempts =
        [
            new("deploy", 1, Plugin, new WorkPerformCall("unit-b1", Plugin + "/pull-request", 1, "i-1", "k-1", Tip1, [], _now.AddMinutes(15)), AttemptStates.Settled)
            {
                RecordedAt = _now, Generation = 1, Deliveries = 1, Outcome = WorkStates.Failed, Code = "no-token", Resolution = resolution,
            },
        ],
    };

    private static WorkflowRunStep Step(NodeRecord record)
    {
        var (graph, problems) = NodeGraphs.Read(ClaimedThenDeploy);
        Assert.True(graph is not null, string.Join("; ", problems.Select(problem => problem.Words)));
        var facts = new WorkflowRunFacts("engine", new CurrentWorkflow([], [], Digest))
        {
            Process = WorkflowProcess.Declared(DriverConfig.Empty, "engine", "default") with
            {
                Binding = new WorkflowRunBinding(RunId, "engine", "default", "docs", WorkflowLevels.Repository, DateTimeOffset.UnixEpoch) { Version = 1, Digest = Digest },
                Run = RunId,
            },
        };
        var run = WorkflowGraphRuns.Derive(new WorkflowGraphRunFacts(facts, RunId, graph, record), "default");
        return run.Steps.Single(each => each.Step.Id == "deploy");
    }

    [Fact]
    public void A_failed_plugin_steps_attempt_is_held_for_the_persons_again()
    {
        var step = Step(Failed());

        var held = Assert.IsType<WorkflowRunHeld>(step.Held);
        Assert.Equal((RunUnit, 1), (held.Unit, held.Attempt));
        Assert.Equal([HeldResolutions.Again], held.Words);
        Assert.DoesNotContain("try-again", step.Acts ?? []);
    }

    [Fact]
    public void Once_the_person_said_again_the_step_offers_it_no_more()
    {
        Assert.Empty(Assert.IsType<WorkflowRunHeld>(Step(Failed(HeldResolutions.Again)).Held).Words);
    }

    [Fact]
    public void A_plugin_step_that_did_not_fail_holds_nothing()
    {
        var record = Failed() with
        {
            Statuses = new Dictionary<string, NodeStatus>(Failed().Statuses) { ["deploy"] = new(RunUnit, "deploy", NodeStates.Active) { Attempt = 1 } },
        };

        Assert.Null(Step(record).Held);
    }

    [Fact]
    public void The_run_step_has_no_try_again_of_its_own()
    {
        BoundTo(ClaimedThenDeploy);
        Files.Write(Failed());

        Assert.DoesNotContain("try-again", RunNodeWords.Acts(Deploy, Failed().Statuses["deploy"]));
        Assert.False(RunNodeWords.Word(Files, RunId, "deploy", "try-again", null, () => _now).Done);
    }
}
