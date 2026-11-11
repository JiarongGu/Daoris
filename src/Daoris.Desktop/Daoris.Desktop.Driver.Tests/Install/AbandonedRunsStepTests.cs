using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The migration's sweep of runs abandoned before an abandon stopped them: a run whose quest is declined, or whose ask is closed, has
/// each live step stopped as the person's through the run's node door, said on the run by the pass that applies it; a run of open work is
/// left, and the sweep run again changes nothing.
/// </summary>
public sealed class AbandonedRunsStepTests : NodeRunFixture
{
    private const string TaskGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"look","kind":"human","version":1,"reach":"pressed","outcomes":["done"]},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>l","kind":"after","from":"claimed","to":"look"},
                  {"id":"l>h","kind":"when","from":"look","to":"handed","outcome":"done"}]}
        """;

    private void Store(string questStatus, string askState = "Published")
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS quests (id TEXT PRIMARY KEY, status TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS asks (id TEXT PRIMARY KEY, state TEXT NOT NULL);
            INSERT OR REPLACE INTO quests (id, status) VALUES ($quest, $status);
            INSERT OR REPLACE INTO asks (id, state) VALUES ('a1b2c3', $state);
            """;
        command.Parameters.AddWithValue("$quest", RunId);
        command.Parameters.AddWithValue("$status", questStatus);
        command.Parameters.AddWithValue("$state", askState);
        command.ExecuteNonQuery();
    }

    private async Task<NodeRuntime> WaitingAsync()
    {
        BoundTo(TaskGraph, ask: "a1b2c3");
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("look").State);
        return runtime;
    }

    private IReadOnlyList<string> Sweep()
    {
        var run = new HomeMigrationRun(_home, HomeMigration.Name);
        new AbandonedRunsStep().Run(run);
        return run.Changed;
    }

    [Fact]
    public async Task A_declined_quests_run_has_its_live_steps_ended_once_and_the_sweep_again_changes_nothing()
    {
        var runtime = await WaitingAsync();
        Store("Done");
        Assert.Empty(Sweep());
        Assert.Empty(Files.Read(RunUnit)!.Pending);

        Store("Declined");
        Assert.Single(Sweep());
        Assert.Equal(["look", "handed"], Files.Read(RunUnit)!.Pending.Select(word => word.Node));
        Assert.Empty(Sweep());

        await RunPassAsync(runtime, whole: false);
        var look = RunStatus("look");
        Assert.Equal((NodeStates.Stopped, NodeSource.Person, AbandonedRunsStep.DeclinedWords), (look.State, look.By, look.Words));
        Assert.Equal(NodeStates.Stopped, RunStatus("handed").State);
        Assert.Empty(Sweep());
    }

    [Fact]
    public async Task A_closed_asks_run_has_its_live_steps_ended_though_its_quest_is_done()
    {
        await WaitingAsync();
        Store("Done", askState: "Closed");

        Assert.Single(Sweep());
        var words = Files.Read(RunUnit)!.Pending;
        Assert.All(words, word => Assert.Equal((NodeWordActs.Stop, AbandonedRunsStep.ClosedWords), (word.Act, word.Words)));
    }

    [Fact]
    public void A_home_with_no_store_or_no_runs_is_left_as_it_is()
    {
        Assert.Empty(Sweep());
        Store("Declined");
        Assert.Empty(Sweep());
    }

    [Fact]
    public void The_sweep_is_one_step_of_the_releases_migration() =>
        Assert.Single(HomeMigration.Steps, step => step is AbandonedRunsStep);
}
