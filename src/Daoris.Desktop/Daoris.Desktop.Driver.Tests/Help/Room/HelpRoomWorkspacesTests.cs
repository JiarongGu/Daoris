using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomWorkspaces"/>: each workspace's repositories, or where to begin when there are none.</summary>
public sealed class HelpRoomWorkspacesTests
{
    [Fact]
    public void A_machine_with_nothing_registered_says_so_and_where_to_begin()
    {
        var agents = HelpRoom.Render(new HelpMachine());

        Assert.Contains("No repository is registered on this machine", agents);
        Assert.Contains("daoris connect", agents);
        Assert.Contains("no agent answers asks", agents);
    }

    /// <summary>
    /// WORKFLOW1h-b (D157 point 13, design §8.4): the room's table gains each repository's workflow, its name, version and what
    /// chose it, Current included, and each workspace's kinds after it, so the helper answers *how does work in engine land* from
    /// the drawn workflow.
    /// </summary>
    [Fact]
    public void Each_repository_says_the_workflow_it_follows_and_each_workspace_its_kinds()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-help-room-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            WorkflowStore.Add(home, new WorkflowVersionAdded("docs-to-pr", "Documentation to a pull request", "terminal", "2026-10-09T09:00:00Z",
                JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"land","kind":"landing","form":"merge","accept":"you"}]""")), kept: []);
            var config = WorkflowSelection.Apply(DriverConfig.Empty, new WorkflowDeclare("docs", "work", "Documentation", ["docs/**", "**/*.md"]));
            config = WorkflowSelection.Apply(config, new WorkflowDeclare("feature", "work", "Feature", []));
            config = WorkflowSelection.Apply(config, new WorkflowUse("docs-to-pr", null, "work", "docs"));
            config = WorkflowSelection.Apply(config, new WorkflowUse("docs-to-pr", "engine", null, null));
            config = WorkflowSelection.Apply(config, new WorkflowUse("gone-flow", "tools", null, null));
            var snapshot = new Snapshot(
                [], [new RepoView("engine", true, null, "work"), new RepoView("game", true, null, "work"), new RepoView("tools", true, null, "work")], []);

            var room = HelpRoom.Render(HelpRoom.Describe(config, snapshot, [], [], _ => null, asks: 0, workflows: WorkflowStore.List(home)));

            Assert.Contains("| Second opinion before landing | Workflow |\n", room);
            Assert.Contains("| `docs-to-pr` v1, Documentation to a pull request (set for it) |\n", room);
            Assert.Contains("| Current (the default) |\n", room);
            Assert.Contains("| `gone-flow`, not saved here (set for it) |\n", room);
            Assert.Contains(
                "Kinds of work `work` declares (`daoris driver workflow kind <workspace> <kind> --label \"…\" [--paths …]`):\n\n"
                + "- `docs` (Documentation), paths `docs/**`, `**/*.md`: `docs-to-pr`\n"
                + "- `feature` (Feature): the level's default\n",
                room);
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }
}
