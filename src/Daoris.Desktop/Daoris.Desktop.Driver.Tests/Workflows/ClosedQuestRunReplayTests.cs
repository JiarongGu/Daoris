using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The install's case replayed through whole passes: runs whose quests are closed Done, each held at the quest's holds on a done nothing
/// reads, are ended together with their sessions as stopped by Daoris.
/// </summary>
public sealed class ClosedQuestRunReplayTests : NodeRunFixture
{
    private const string ClaimedHoldsHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"holds","kind":"quest.holds","version":1,"reach":"observed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"holds"},{"id":"h>l","kind":"after","from":"holds","to":"handed"}]}
        """;

    [Fact]
    public async Task Runs_whose_quests_are_done_and_held_at_the_quests_holds_end_stopped_by_daoris_with_their_sessions()
    {
        var digest = RunHome.SaveGraph(_home, ClaimedHoldsHanded);
        var runs = Enumerable.Range(0, 14).Select(each => $"r{each:D2}").ToList();
        foreach (var run in runs)
        {
            RunHome.Bind(_home, run, digest, _now);
            var unit = RunUnits.IdOf(run);
            Files.Write(new NodeRecord(unit)
            {
                Statuses = new Dictionary<string, NodeStatus>
                {
                    ["claimed"] = new(unit, "claimed", NodeStates.Reached) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(unit, Tip1, "gph", "claimed", 1, 1) },
                    ["holds"] = new(unit, "holds", NodeStates.Active) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(unit, Tip1, "gph", "holds", 1, 1) },
                },
            });
        }

        _units = [.. runs.Select(run => RunHome.QuestUnit(RunHome.Quest(run, "Done") with { Held = true, Hold = "failed" }) with { Executors = ["s-" + run] })];
        var finished = new List<RunFinish>();
        var runtime = Runtime();
        runtime.Finish = (end, _) =>
        {
            finished.Add(end);
            return Task.FromResult<RunEndResponse?>(new RunEndResponse(null, "ended", [], end.Sessions));
        };
        runtime.SessionsRead = _ => Task.FromResult<IReadOnlyList<SessionRecord>?>(
            [.. runs.Select(run => new SessionRecord("s-" + run, "Owner", SessionStates.AwaitingStep) { NoteParts = new StepWait(run).Note().Parts })]);

        for (var pass = 0; pass < 20 && runs.Any(run => Files.Read(RunUnits.IdOf(run))?.Ended is null); pass++) await RunPassAsync(runtime);

        Assert.All(runs, run =>
        {
            var ended = Files.Read(RunUnits.IdOf(run))!.Ended;
            Assert.Equal((RunOutcomes.Stopped, "holds", NodeSource.Daoris, ClosedRunEnds.QuestClosed), (ended?.Outcome, ended?.Node, ended?.By, ended?.Why));
        });
        Assert.Equal(runs.Count, finished.Count);
        Assert.All(finished, end =>
        {
            Assert.Equal("stopped", end.State);
            Assert.Empty(end.Quests);
            Assert.Equal(["s-" + end.Run], end.Sessions);
            Assert.Contains("Daoris stopped", end.Note, StringComparison.Ordinal);
        });
    }
}
