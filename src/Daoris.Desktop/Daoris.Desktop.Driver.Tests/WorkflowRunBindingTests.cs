using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1e (D157 point 11, the workflow design §4.5, §5.1, §2.6): a run bound at its first start to the workflow that chose it,
/// with what chose it, kept once under the home; and a workflow's file keeping every version a kept run names.
/// </summary>
public sealed class WorkflowRunBindingTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 9, 9, 12, 30, 250, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-workflow-runs-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DriverConfig Config(string json) => DriverConfig.Parse(json);

    private void Save(string id, params JsonNode[] steps)
    {
        foreach (var each in steps)
        {
            WorkflowStore.Add(_home, new WorkflowVersionAdded(id, "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z", each), kept: []);
        }
    }

    private static JsonNode Steps(string accept) => JsonNode.Parse(
        $$"""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"branch","accept":"{{accept}}","pattern":"docs/{quest}","plugin":"none"}]""")!;

    [Fact]
    public void Nothing_set_binds_Current_with_the_graph_it_drew()
    {
        var binding = WorkflowRunBindings.Plan(Config("{}"), _home, "a1b2c3", "web-app", "work", null, null, [], At);

        Assert.True(binding.IsCurrent);
        Assert.Equal(WorkflowLevels.Current, binding.Level);
        Assert.Null(binding.Version);
        var current = WorkflowCurrent.Derive(Config("{}"), "web-app", "work", []);
        Assert.Equal(current.Version, binding.Digest);
        Assert.Equal(current.Steps.Select(WorkflowCurrent.ToJson), binding.CurrentSteps);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 9, 12, 30, TimeSpan.Zero), binding.At);
    }

    [Fact]
    public void A_named_workflow_binds_its_newest_version_with_the_level_and_kind_that_chose_it()
    {
        Save("docs-to-pr", Steps("you"), Steps("automatic"));
        var config = Config("""
            {"workspaceWorkflows":{"work":{"kinds":{"docs":{"label":"Documentation","paths":["docs/**"],"workflow":"docs-to-pr"}}}}}
            """);

        var binding = WorkflowRunBindings.Plan(config, _home, "a1b2c3", "web-app", "work", "f00d01", new WorkflowTaskChoice("docs", null), [], At);

        Assert.Equal("docs-to-pr", binding.Workflow);
        Assert.Equal(2, binding.Version);
        Assert.Equal(WorkflowStore.Load(_home, "docs-to-pr")!.Read.Versions[^1].Digest, binding.Digest);
        Assert.Equal(WorkflowLevels.WorkspaceKind, binding.Level);
        Assert.Equal("docs", binding.Kind);
        Assert.Equal("Documentation", binding.Label);
        Assert.Equal(["docs/**"], binding.Paths);
        Assert.Equal("f00d01", binding.Ask);
        Assert.Null(binding.Problem);
        Assert.Empty(binding.CurrentSteps);
        Assert.Equal(
            "workflow  #a1b2c3 → web-app: its run follows `docs-to-pr` v2, chosen by the workspace `work` for Documentation.",
            WorkflowRunBindings.Said(binding, "a1b2c3"));
    }

    [Fact]
    public void A_workflow_chosen_and_not_saved_here_is_kept_with_why_never_swapped_for_another()
    {
        var binding = WorkflowRunBindings.Plan(
            Config("""{"workflows":{"web-app":{"default":"release-train"}}}"""), _home, "a1b2c3", "web-app", null, null, null, [], At);

        Assert.Equal("release-train", binding.Workflow);
        Assert.Equal(WorkflowLevels.Repository, binding.Level);
        Assert.Null(binding.Version);
        Assert.Equal("no workflow `release-train` is saved here.", binding.Problem);
        Assert.Equal(
            "workflow  #a1b2c3 → web-app: its run was bound to `release-train`, `web-app`'s default, which cannot start it: "
            + "no workflow `release-train` is saved here.",
            WorkflowRunBindings.Said(binding, "a1b2c3"));
    }

    [Fact]
    public void A_newest_version_that_does_not_read_here_is_bound_with_its_problem()
    {
        Save("docs-to-pr", Steps("you"));
        var path = WorkflowStore.PathOf(_home, "docs-to-pr");
        var file = JsonNode.Parse(File.ReadAllText(path))!;
        file["versions"]!.AsArray().Add(JsonNode.Parse("""{"version":2,"at":"2026-10-09T10:00:00Z","door":"terminal","steps":[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"queue","accept":"you"}]}"""));
        File.WriteAllText(path, file.ToJsonString());

        var binding = WorkflowRunBindings.Plan(
            Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}"""), _home, "a1b2c3", "web-app", null, null, null, [], At);

        Assert.Equal(2, binding.Version);
        Assert.Null(binding.Digest);
        Assert.StartsWith("`docs-to-pr` v2 cannot be read here: ", binding.Problem);
    }

    [Fact]
    public void The_ask_s_latest_choice_is_the_task_s_and_one_naming_neither_is_none()
    {
        var at = DateTimeOffset.UnixEpoch;
        var ask = new AskView("f00d01", "work", "words", "Published", "named")
        {
            WorkflowChoices = [new AskWorkflowChoiceView("docs", null, at), new AskWorkflowChoiceView("docs", "hotfix", at)],
        };

        Assert.Equal(new WorkflowTaskChoice("docs", "hotfix"), WorkflowRunBindings.TaskOf(ask));
        Assert.Null(WorkflowRunBindings.TaskOf(ask with { WorkflowChoices = [.. ask.WorkflowChoices, new AskWorkflowChoiceView(null, null, at)] }));
        Assert.Null(WorkflowRunBindings.TaskOf(ask with { WorkflowChoices = [] }));
        Assert.Null(WorkflowRunBindings.TaskOf(null));
    }

    [Fact]
    public void A_run_is_named_by_its_chain_s_first_quest_in_the_repository()
    {
        QuestView Quest(string id, string to, string? parent) => new(id, "ask #f00d01", to, "t", "b", "Open") { Parent = parent };
        var chain = new[] { Quest("a1", "web-app", null), Quest("b2", "api", "a1"), Quest("c3", "web-app", "b2") };

        Assert.Equal("a1", WorkflowRunBindings.RunOf(chain, "WEB-APP"));
        Assert.Equal("b2", WorkflowRunBindings.RunOf(chain, "api"));
        Assert.Null(WorkflowRunBindings.RunOf(chain, "notes-site"));
    }

    [Fact]
    public void A_run_is_bound_once_and_read_back_whole()
    {
        Save("docs-to-pr", Steps("automatic"));
        var config = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        var first = WorkflowRunBindings.Plan(config, _home, "a1b2c3", "web-app", "work", null, null, [], At);

        Assert.True(WorkflowRunBindings.Bind(_home, first));
        Assert.False(WorkflowRunBindings.Bind(_home, first with { Workflow = "other" }));

        var read = WorkflowRunBindings.Read(_home, "a1b2c3")!;
        Assert.Equal(first with { Paths = read.Paths, CurrentSteps = read.CurrentSteps }, read);
        var text = File.ReadAllText(WorkflowRunBindings.PathOf(_home, "a1b2c3"));
        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("}\n", text);
        Assert.Contains("\"at\": \"2026-10-09T09:12:30Z\"", text);
    }

    [Fact]
    public void A_Current_binding_reads_back_its_graph()
    {
        var binding = WorkflowRunBindings.Plan(Config("{}"), _home, "a1b2c3", "web-app", "work", null, null, [], At);
        WorkflowRunBindings.Bind(_home, binding);

        var read = WorkflowRunBindings.Read(_home, "a1b2c3")!;

        Assert.Equal(binding.CurrentSteps.Select(step => JsonNode.Parse(step)!.ToJsonString()), read.CurrentSteps.Select(step => JsonNode.Parse(step)!.ToJsonString()));
        Assert.Equal(binding.Digest, read.Digest);
    }

    [Fact]
    public void A_run_s_id_names_no_path_and_a_file_that_does_not_read_is_no_binding()
    {
        Assert.False(WorkflowRunBindings.IsRunId("../a1"));
        Assert.Throws<DriverException>(() => WorkflowRunBindings.PathOf(_home, "../a1"));
        Assert.Null(WorkflowRunBindings.Read(_home, "../a1"));

        Directory.CreateDirectory(WorkflowRunBindings.FolderOf(_home));
        File.WriteAllText(WorkflowRunBindings.PathOf(_home, "a1b2c3"), "{ not json");
        Assert.Null(WorkflowRunBindings.Read(_home, "a1b2c3"));
        Assert.Empty(WorkflowRunBindings.KeptVersions(_home, "docs-to-pr"));
    }

    /// <summary>§2.6: a workflow keeps 20 versions, a version a kept run names always among them (WORKFLOW1d's plan, now handed the runs).</summary>
    [Fact]
    public void A_version_a_kept_run_names_outlives_the_newest_twenty()
    {
        Save("docs-to-pr", Steps("you"));
        var config = Config("""{"workflows":{"web-app":{"default":"docs-to-pr"}}}""");
        WorkflowRunBindings.Bind(_home, WorkflowRunBindings.Plan(config, _home, "a1b2c3", "web-app", null, null, null, [], At));

        for (var n = 0; n < 21; n++)
        {
            WorkflowStore.Add(_home, new WorkflowVersionAdded("docs-to-pr", null, "terminal", "2026-10-09T09:00:00Z", Steps(n % 2 == 0 ? "automatic" : "you")));
        }

        var versions = WorkflowStore.Load(_home, "docs-to-pr")!.Read.Versions.Select(each => each.Version).ToList();
        Assert.Equal(1, versions[0]);
        Assert.Equal(20, versions.Count);
        Assert.Equal(4, versions[1]);
        Assert.Equal(22, versions[^1]);
        Assert.Equal([1], WorkflowRunBindings.KeptVersions(_home, "docs-to-pr"));
    }
}
