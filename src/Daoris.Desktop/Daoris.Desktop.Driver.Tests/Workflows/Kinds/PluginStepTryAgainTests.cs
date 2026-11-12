using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A failed plugin step's *Try again*: offered on the run's step where it failed, kept at the run's door as the person's word, and applied
/// as the table's failed to ready by the person, a new attempt under the next number; refused on a step of another kind, and where the
/// step did not fail.
/// </summary>
public sealed class PluginStepTryAgainTests : NodeRunFixture
{
    private const string ClaimedThenDeploy = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"deploy","kind":"acme.deploy/pull-request","version":1,"reach":"performed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"deploy"},{"id":"d>h","kind":"after","from":"deploy","to":"handed"}]}
        """;

    private static readonly NodeDeclaration Deploy = new("deploy", "acme.deploy/pull-request", 1, NodeReach.Performed);

    private NodeRecord Failed() => new(RunUnit)
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
                RecordedAt = _now, Generation = 1, Deliveries = 1, Outcome = WorkStates.Failed, Code = "no-token",
            },
        ],
    };

    [Fact]
    public void A_failed_plugin_step_offers_try_again_and_the_word_is_a_new_attempt()
    {
        BoundTo(ClaimedThenDeploy);
        Files.Write(Failed());

        Assert.Contains(NodeWordActs.TryAgain, RunNodeWords.Acts(Deploy, Failed().Statuses["deploy"]));
        Assert.DoesNotContain(NodeWordActs.TryAgain, RunNodeWords.Acts(Deploy, new NodeStatus(RunUnit, "deploy", NodeStates.Active) { Attempt = 1 }));

        var worded = RunNodeWords.Word(Files, RunId, "deploy", NodeWordActs.TryAgain, null, () => _now);
        Assert.True(worded.Done, worded.Refused);

        var (next, applied) = RunNodeWords.Apply(Files.Read(RunUnit)!, new Dictionary<string, NodeDeclaration> { ["deploy"] = Deploy }, new NodeScope("d1", RunId), _now);
        Assert.Null(Assert.Single(applied).Refused);
        var status = next!.Statuses["deploy"];
        Assert.Equal((NodeStates.Ready, 0), (status.State, status.Tries));
    }

    [Fact]
    public void Try_again_is_refused_on_a_step_of_another_kind_and_on_one_that_did_not_fail()
    {
        BoundTo(ClaimedThenDeploy);
        Files.Write(Failed() with
        {
            Statuses = new Dictionary<string, NodeStatus>(Failed().Statuses) { ["deploy"] = new(RunUnit, "deploy", NodeStates.Active) { Attempt = 1 } },
        });

        Assert.Equal(RunNodeWords.NotPluginStep, RunNodeWords.Word(Files, RunId, "claimed", NodeWordActs.TryAgain, null, () => _now).Refused);
        Assert.False(RunNodeWords.Word(Files, RunId, "deploy", NodeWordActs.TryAgain, null, () => _now).Done);
    }
}
