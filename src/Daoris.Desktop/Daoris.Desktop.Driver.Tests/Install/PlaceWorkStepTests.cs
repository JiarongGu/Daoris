using System.Text.Json.Nodes;
using Daoris.Driver;
using Microsoft.Data.Sqlite;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The release's conversion of the trees switch and the line policies' <c>overlap</c> into an <c>isolate</c> step: a workflow whose followers
/// all had trees on gains a version that starts with the step; where they differ, those with trees on follow a copy with it, one per overlap;
/// <c>trees</c> and <c>overlap</c> leave <c>driver.json</c>, and a second run changes nothing.
/// </summary>
public sealed class PlaceWorkStepTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "place-work-step", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private string DriverFile => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    private IWorkflowStore Store => WorkflowPorts.Store(_home);

    private void Home(string[] trees, string? waitsIn, params string[] repositories) => HomeOf("flow", trees, waitsIn, repositories);

    private void HomeOf(string flow, string[] trees, string? waitsIn, params string[] repositories)
    {
        var config = repositories.Aggregate(DriverConfig.Empty, (each, repository) => WorkflowSelection.Apply(each, new WorkflowUse(flow, repository, null, null)));
        Directory.CreateDirectory(Path.GetDirectoryName(DriverFile)!);
        config.Save(DriverFile);
        var root = JsonNode.Parse(File.ReadAllText(DriverFile))!.AsObject();
        root["trees"] = new JsonArray([.. trees.Select(name => JsonValue.Create(name))]);
        if (waitsIn is not null) root["linePolicies"] = new JsonObject { [waitsIn] = new JsonObject { ["integrate"] = new JsonObject { ["overlap"] = "wait" } } };
        File.WriteAllText(DriverFile, root.ToJsonString());
        Store.Write(flow, JsonNode.Parse("""
            {"id":"__FLOW__","name":"Flow","versions":[{"version":1,"at":"2026-11-01T09:00:00Z","door":"screen","schema":2,"graph":{"schemaVersion":2,"entries":["work"],"end":"line",
             "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"landing","kind":"land","version":1,"reach":"performed","options":{"method":"merge"}},
                      {"id":"line","kind":"line","version":1,"reach":"observed"}],
             "wires":[{"id":"a","kind":"after","from":"work","to":"landing"},{"id":"b","kind":"after","from":"landing","to":"line"}]}}]}
            """.Replace("__FLOW__", flow))!);
        Registered(repositories);
    }

    private void Registered(string[] repositories)
    {
        var path = DaorisHome.Entry(_home, HomeEntry.Knowledge);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE registrations (repository TEXT, workspace TEXT, adopted INTEGER, root TEXT);";
        command.ExecuteNonQuery();
        foreach (var repository in repositories)
        {
            command.CommandText = $"INSERT INTO registrations VALUES ('{repository}', 'default', 1, NULL);";
            command.ExecuteNonQuery();
        }
    }

    private IsolateStep? Newest(string workflow) => IsolateSteps.In(Store.Load(workflow)?.Read.Versions.LastOrDefault()?.Graph);

    private HomeMigrationRun Run()
    {
        var run = new HomeMigrationRun(_home, HomeMigration.Name);
        new PlaceWorkStep().Run(run);
        return run;
    }

    [Fact]
    public void A_workflow_whose_followers_all_had_trees_gains_the_step_and_the_switch_leaves_the_file_once()
    {
        Home(["engine", "site"], null, "engine", "site");

        Run();

        Assert.Null(Store.Load("flow")!.Read.Versions[^1].Problem);
        Assert.Equal(IsolateStep.Trees, Newest("flow"));
        Assert.Equal(2, Store.Load("flow")!.Read.Versions.Count);
        Assert.Null(JsonNode.Parse(File.ReadAllText(DriverFile))!["trees"]);
        Assert.Empty(Run().Changed);
        Assert.Equal(2, Store.Load("flow")!.Read.Versions.Count);
    }

    [Fact]
    public void Where_followers_differ_those_with_trees_follow_a_copy_with_the_step_its_overlap_the_policys()
    {
        Home(["engine"], waitsIn: "engine", "engine", "site");

        Run();

        Assert.Null(Newest("flow"));
        Assert.Equal(IsolateStep.Trees with { OverlapWaits = true }, Newest("flow-apart-wait"));
        var config = DriverConfig.Load(DriverFile);
        Assert.Equal("flow-apart-wait", WorkflowSelection.Resolve(config, "engine", "default", null).Workflow);
        Assert.Equal("flow", WorkflowSelection.Resolve(config, "site", "default", null).Workflow);
        var file = JsonNode.Parse(File.ReadAllText(DriverFile))!;
        Assert.Null(file["trees"]);
        Assert.Null(file["linePolicies"]?["engine"]?["integrate"]?["overlap"]);
    }

    [Fact]
    public void A_home_with_trees_off_everywhere_keeps_its_workflows_in_place()
    {
        Home([], null, "engine");

        Run();

        Assert.Null(Store.Load("flow")!.Read.Versions[^1].Problem);
        Assert.Null(Newest("flow"));
        Assert.Null(JsonNode.Parse(File.ReadAllText(DriverFile))!["trees"]);
    }

    [Fact]
    public void A_repository_s_own_line_policy_replaces_its_workspace_s_so_its_overlap_is_not_borrowed()
    {
        Home(["engine"], null, "engine");
        var root = JsonNode.Parse(File.ReadAllText(DriverFile))!.AsObject();
        root["workspaceLinePolicies"] = new JsonObject { ["default"] = new JsonObject { ["integrate"] = new JsonObject { ["overlap"] = "wait" } } };
        root["linePolicies"] = new JsonObject { ["engine"] = new JsonObject { ["integrate"] = new JsonObject { ["commit"] = "merge" } } };
        File.WriteAllText(DriverFile, root.ToJsonString());

        Run();

        Assert.Equal(IsolateStep.Trees, Newest("flow"));
    }

    [Fact]
    public void A_copy_for_a_long_workflow_id_keeps_the_id_within_its_limit_and_is_chosen()
    {
        var flow = "a-workflow-whose-name-is-thirty-eight";
        Assert.True(WorkflowNamed.IsId(flow));
        HomeOf(flow, ["engine"], null, "engine", "site");

        Run();

        var config = DriverConfig.Load(DriverFile);
        var chosen = WorkflowSelection.Resolve(config, "engine", "default", null).Workflow;
        Assert.NotEqual(flow, chosen);
        Assert.True(WorkflowNamed.IsId(chosen));
        Assert.Equal(IsolateStep.Trees, Newest(chosen));
        Assert.Equal(flow, WorkflowSelection.Resolve(config, "site", "default", null).Workflow);
    }

    [Fact]
    public void A_repository_whose_workflow_cannot_take_the_step_is_not_pointed_at_a_copy_that_was_never_saved()
    {
        Home(["engine"], null, "engine", "site");
        Store.Write("flow", JsonNode.Parse("""
            {"id":"flow","name":"Flow","versions":[{"version":1,"at":"2026-11-01T09:00:00Z","door":"screen","schema":2,"graph":{"schemaVersion":2,"entries":["work","other"],"end":"work",
             "nodes":[{"id":"work","kind":"claimed","version":1,"reach":"observed"},{"id":"other","kind":"claimed","version":1,"reach":"observed"}],"wires":[]}}]}
            """)!);

        Run();

        var config = DriverConfig.Load(DriverFile);
        Assert.Equal("flow", WorkflowSelection.Resolve(config, "engine", "default", null).Workflow);
    }
}

