using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HistoryStandIn;
using StandIn = Daoris.Desktop.Driver.Tests.HistoryStandIn;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A workspace's clear of a history longer than one answer of the service's fits within the client's bound: the units go to the
/// service in presses that each answer within it, and the outcome counts every unit and every file of them.
/// </summary>
/// <remarks>A scratch home and the in-process stand-in (<see cref="HistoryStandIn"/>), whose <c>Bound</c> stalls a press naming more.</remarks>
public sealed class HistoryClearBatchesTests : IDisposable
{
    private const int Bound = 20;

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-history-batches-" + Guid.NewGuid().ToString("N")[..8]);

    public HistoryClearBatchesTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    [Fact]
    public async Task A_workspaces_clear_of_a_history_longer_than_the_clients_bound_finishes_in_one_press()
    {
        var ids = Enumerable.Range(1, 3 * Bound).Select(at => $"q{at}").ToArray();
        var service = new StandIn
        {
            Bound = Bound,
            Home = _home,
            Quests = [.. ids.Select(id => Quest(id, workspace: "aurora"))],
            Records = [.. ids.Select(id => Record("s" + id, quest: id, workspace: "aurora"))],
        };
        service.Listings["workspace=aurora"] = [.. ids.Select(id => Unit("quest", id, "aurora", quests: [id], sessions: ["s" + id]))];
        var sessions = DaorisHome.Entry(_home, HomeEntry.Sessions);
        foreach (var id in ids)
        {
            File.WriteAllText(Path.Combine(sessions, $"s{id}.log"), "the transcript\n");
            Directory.CreateDirectory(Path.Combine(_home, "quests", id, "attachments"));
            File.WriteAllText(Path.Combine(_home, "quests", id, "attachments", "abc123-spec.md"), "a spec");
        }

        var world = new HistoryWorld(service.Client(), _home, DaorisHome.Entry(_home, HomeEntry.DriverConfig), new SessionProcesses(sessions));
        var plan = await HistoryClearing.PlanAsync(world, HistoryScope.Workspace, "aurora");
        var outcome = await HistoryClearing.ClearAsync(
            world, HistoryScope.Workspace, "aurora", [.. plan.Units.Where(unit => unit.Clearable).Select(unit => unit.Name)], PluginEvents.Terminal);

        Assert.Equal(ids.Length, outcome.Cleared.Count);
        Assert.Empty(outcome.Changed);
        Assert.Equal(0, outcome.Failed);
        Assert.Equal(ids.Length, outcome.Quests);
        Assert.Equal(ids.Length, outcome.Sessions);
        Assert.Empty(service.Quests);
        Assert.Empty(service.Records);
        Assert.Equal(ids.Length, service.Pressed.Count);
        Assert.All(service.Presses, size => Assert.True(size <= Bound, $"a press named {size} units"));
        Assert.All(ids, id => Assert.False(File.Exists(Path.Combine(sessions, $"s{id}.log"))));
        Assert.All(ids, id => Assert.False(Directory.Exists(Path.Combine(_home, "quests", id))));
    }
}
