using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Abandoning work ends its runs' waits: every live step of a run on the abandoned ask is stopped as the person's, with their reason, so
/// no review or landing of it stays offered; a run on another ask is left as it stands.
/// </summary>
public sealed class AbandonRunsTests : NodeRunFixture
{
    private const string ReviewedGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"look","kind":"human","version":1,"reach":"pressed","outcomes":["done"]},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"look"},
                  {"id":"l>h","kind":"when","from":"look","to":"handed","outcome":"done"}]}
        """;

    [Fact]
    public async Task An_abandoned_asks_run_has_its_waiting_steps_stopped_as_yours_and_another_asks_run_is_left()
    {
        BoundTo(ReviewedGraph, ask: "a1b2c3");
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("look").State);

        Assert.Empty(await AbandonRuns.EndAsync(Files, run => run.Binding.Ask == "d4e5f6", "not wanted", () => _now));
        var ended = await AbandonRuns.EndAsync(Files, run => run.Binding.Ask == "a1b2c3", "not wanted", () => _now);
        Assert.Equal([$"{RunId}/look", $"{RunId}/handed"], ended);

        await RunPassAsync(runtime, whole: false);
        var look = RunStatus("look");
        Assert.Equal((NodeStates.Stopped, NodeSource.Person, "not wanted"), (look.State, look.By, look.Words));
        Assert.Equal(NodeStates.Stopped, RunStatus("handed").State);
    }
}
