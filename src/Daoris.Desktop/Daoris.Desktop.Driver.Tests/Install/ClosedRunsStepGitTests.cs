using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The migration's end of the runs a closed quest left standing, over a real checkout: a run whose landing waits on a commit the line holds
/// by patch ends <c>on-line</c>, one whose branch is gone ends <c>branch-gone</c>, each counted; a run of open work and one whose session's
/// tree stands are left, and the step run again changes nothing.
/// </summary>
public sealed class ClosedRunsStepGitTests : NodeRunFixture
{
    private const string ClaimedThenHanded = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},{"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
        """;

    private readonly GitTree _checkout = new("closed-runs");

    private string Checkout => _checkout.Root;

    private void Store(IEnumerable<(string Id, string Status)> quests, string? servedBy = null, string? tree = null)
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE quests (id TEXT PRIMARY KEY, status TEXT NOT NULL, receiver TEXT, parent TEXT, set_up_in TEXT);
            CREATE TABLE sessions (id TEXT PRIMARY KEY, quest TEXT, repository TEXT, tree TEXT);
            CREATE TABLE registrations (repository TEXT, workspace TEXT, adopted INTEGER, root TEXT);
            INSERT INTO registrations VALUES ($repository, 'default', 1, $root);
            """;
        command.Parameters.AddWithValue("$repository", RunHome.Repository);
        command.Parameters.AddWithValue("$root", Checkout);
        command.ExecuteNonQuery();
        foreach (var (id, status) in quests)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO quests (id, status, receiver) VALUES ($id, $status, $repository)";
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$status", status);
            insert.Parameters.AddWithValue("$repository", RunHome.Repository);
            insert.ExecuteNonQuery();
        }

        if (servedBy is null) return;
        using var session = connection.CreateCommand();
        session.CommandText = "INSERT INTO sessions (id, quest, repository, tree) VALUES ('s-tree', $quest, $repository, $tree)";
        session.Parameters.AddWithValue("$quest", "#" + servedBy);
        session.Parameters.AddWithValue("$repository", RunHome.Repository);
        session.Parameters.AddWithValue("$tree", tree);
        session.ExecuteNonQuery();
    }

    /// <summary>A checkout whose line took a session branch's commit by cherry-pick, the branch then deleted: the commit is on no branch.</summary>
    private async Task<string> CherryPickedAsync()
    {
        await GitFixture.GitAsync(Checkout, "config", "user.email", "test@example.invalid");
        await GitFixture.GitAsync(Checkout, "config", "user.name", "Test");
        await GitFixture.GitAsync(Checkout, "checkout", "-q", "-b", "daoris/s1");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "work.txt"), "work\n");
        await GitFixture.GitAsync(Checkout, "add", ".");
        await GitFixture.GitAsync(Checkout, "commit", "-q", "-m", "the work");
        var tip = (await GitFixture.GitAsync(Checkout, "rev-parse", "HEAD")).Trim();
        await GitFixture.GitAsync(Checkout, "checkout", "-q", "main");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "other.txt"), "other\n");
        await GitFixture.GitAsync(Checkout, "add", ".");
        await GitFixture.GitAsync(Checkout, "commit", "-q", "-m", "the line moved");
        await GitFixture.GitAsync(Checkout, "cherry-pick", tip);
        await GitFixture.GitAsync(Checkout, "branch", "-q", "-D", "daoris/s1");
        return tip;
    }

    private void LandingActiveAt(string run, string tip) => Files.Write(new NodeRecord(RunUnits.IdOf(run))
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["claimed"] = new(RunUnits.IdOf(run), "claimed", NodeStates.Reached) { Attempt = 1, Tries = 1 },
            ["handed"] = new(RunUnits.IdOf(run), "handed", NodeStates.Active)
            {
                Attempt = 1, Tries = 1, Binding = new NodeAcceptance(RunUnits.IdOf(run), tip, "gph", "handed", 1, 1) { Subject = "unit-b1" },
            },
        },
    });

    private void ReadyUnbound(string run) => Files.Write(new NodeRecord(RunUnits.IdOf(run))
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["claimed"] = new(RunUnits.IdOf(run), "claimed", NodeStates.Reached) { Attempt = 1, Tries = 1 },
            ["handed"] = new(RunUnits.IdOf(run), "handed", NodeStates.Ready),
        },
    });

    private HomeMigrationRun Sweep()
    {
        var run = new HomeMigrationRun(_home, HomeMigration.Name);
        new ClosedRunsStep().Run(run);
        return run;
    }

    [Fact]
    public async Task Closed_quests_runs_that_cannot_move_end_counted_and_open_or_standing_work_is_left()
    {
        using var checkout = _checkout;
        var tip = await CherryPickedAsync();
        var digest = RunHome.SaveGraph(_home, ClaimedThenHanded);
        foreach (var run in new[] { "landed", "gone", "open", "standing" }) RunHome.Bind(_home, run, digest, _now);
        LandingActiveAt("landed", tip);
        ReadyUnbound("gone");
        ReadyUnbound("open");
        ReadyUnbound("standing");
        var tree = Directory.CreateDirectory(Path.Combine(_home, "tree-standing")).FullName;
        Store([("landed", "Done"), ("gone", "Done"), ("open", "Taken"), ("standing", "Declined")], servedBy: "standing", tree: tree);

        var swept = Sweep();

        Assert.Equal(ClosedRunEnds.OnLine, Files.Read(RunUnits.IdOf("landed"))!.Ended?.Why);
        Assert.Equal(NodeStates.Active, Files.Read(RunUnits.IdOf("landed"))!.Statuses["handed"].State);
        Assert.Equal(ClosedRunEnds.BranchGone, Files.Read(RunUnits.IdOf("gone"))!.Ended?.Why);
        Assert.Null(Files.Read(RunUnits.IdOf("open"))!.Ended);
        Assert.Null(Files.Read(RunUnits.IdOf("standing"))!.Ended);
        Assert.Equal(1, swept.Counts["closed-runs." + ClosedRunEnds.OnLine]);
        Assert.Equal(1, swept.Counts["closed-runs." + ClosedRunEnds.BranchGone]);
        Assert.Equal(2, swept.Changed.Count);
        Assert.Empty(Sweep().Changed);
    }

    [Fact]
    public void A_home_with_no_store_or_no_runs_is_left_as_it_is()
    {
        using (_checkout) Assert.Empty(Sweep().Changed);
    }

    [Fact]
    public void The_step_is_one_of_the_releases_migration_after_the_abandoned_runs()
    {
        using (_checkout)
        {
            Assert.True(HomeMigration.Steps.ToList().FindIndex(step => step is ClosedRunsStep) > HomeMigration.Steps.ToList().FindIndex(step => step is AbandonedRunsStep));
        }
    }
}
