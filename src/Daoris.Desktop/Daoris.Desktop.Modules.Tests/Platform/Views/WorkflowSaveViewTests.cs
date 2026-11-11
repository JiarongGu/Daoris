using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A workflow's draft saved or applied (<c>daoris workflow apply|import|new</c>) as its press's card shows it: the route's own plan
/// judges it at the ask, a refusal answered to the terminal in its words, and the card draws the graph with its changes against the version
/// it replaces.
/// </summary>
public sealed class WorkflowSaveViewTests
{
    private static readonly object Before = new
    {
        schemaVersion = 2, entries = new[] { "landing" }, end = "close",
        nodes = new object[]
        {
            new { id = "landing", kind = "land", version = 1, reach = "performed", options = new { method = "merge" } },
            new { id = "close", kind = "quest.close", version = 1, reach = "performed" },
        },
        wires = new object[] { new { id = "b", kind = "after", from = "landing", to = "close" } },
    };

    private static readonly object After = new
    {
        schemaVersion = 2, entries = new[] { "qa" }, end = "close",
        nodes = new object[]
        {
            new { id = "qa", kind = "human", version = 1, reach = "pressed", options = new { brief = "Verify the release" } },
            new { id = "landing", kind = "land", version = 1, reach = "performed", options = new { method = "merge" } },
            new { id = "close", kind = "quest.close", version = 1, reach = "performed" },
        },
        wires = new object[]
        {
            new { id = "a", kind = "after", from = "qa", to = "landing" },
            new { id = "b", kind = "after", from = "landing", to = "close" },
        },
    };

    /// <summary>The route's plan, as <c>WORKFLOW_SAVE</c> answers <c>plan: true</c>, and the base version as <c>WORKFLOW_SHOW</c> reads it.</summary>
    private static StubGrounds Saving(object plan)
    {
        var grounds = new StubGrounds();
        grounds.Reads["WORKFLOW_SAVE"] = payload => payload is { } body && body.TryGetProperty("plan", out var asked) && asked.ValueKind == JsonValueKind.True
            ? plan
            : throw new InvalidOperationException("the view pressed the save");
        grounds.Reads["WORKFLOW_SHOW"] = _ => new { id = "docs", name = "Docs", version = 1, body = Before };
        return grounds;
    }

    private static Task<PressViewed> View(StubGrounds grounds, object payload) =>
        new WorkflowSaveView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_draft_applied_draws_its_graph_and_its_changes_against_the_version_it_replaces()
    {
        var grounds = Saving(new { id = "docs", done = true, version = 2, appended = true, written = false });

        var viewed = await View(grounds, new { id = "docs", graph = After, @base = 1, authorize = Array.Empty<string>() });

        Assert.Equal("workflow-save-edit", viewed.View!.Kind);
        Assert.Equal("docs", viewed.View.Facts["id"]);
        Assert.Equal("Docs", viewed.View.Facts["name"]);
        Assert.Equal("1", viewed.View.Facts["base"]);
        Assert.Equal("2", viewed.View.Facts["version"]);
        Assert.Equal("daoris workflow apply docs <file> --base 1", viewed.View.Command);
        Assert.Equal(3, viewed.View.Workflow!.Graph.GetProperty("nodes").GetArrayLength());
        Assert.Contains(viewed.View.Workflow.Changes!, change => change.Id == "step:+qa" && change.Kind == "human");
        Assert.Contains(viewed.View.Workflow.Changes!, change => change.What == "way" && change.Change == "added");
    }

    [Fact]
    public async Task A_new_workflow_draws_its_graph_with_nothing_it_replaces()
    {
        var grounds = Saving(new { id = "docs", done = true, version = 1, appended = false, written = false });

        var viewed = await View(grounds, new { id = "docs", name = "Docs to a pull request", graph = After });

        Assert.Equal("workflow-save-new", viewed.View!.Kind);
        Assert.Equal("Docs to a pull request", viewed.View.Facts["name"]);
        Assert.Equal("daoris workflow import <file>", viewed.View.Command);
        Assert.Null(viewed.View.Workflow!.Changes);
    }

    [Fact]
    public async Task A_draft_that_says_what_its_base_says_changes_nothing()
    {
        var grounds = Saving(new { id = "docs", done = true, version = 1, appended = true, written = false, message = "`docs` v1 already says this: nothing was written." });

        Assert.Equal("workflow-save-same", (await View(grounds, new { id = "docs", graph = Before, @base = 1 })).View!.Kind);
    }

    [Fact]
    public async Task What_the_save_would_refuse_is_refused_at_the_ask_in_its_words_with_no_card()
    {
        var stale = Saving(new { id = "docs", done = false, refused = "stale", message = "`docs` is at v3 now, not v1: read it again." });
        Assert.Equal("`docs` is at v3 now, not v1: read it again.", (await View(stale, new { id = "docs", graph = After, @base = 1 })).Refusal);

        var problems = Saving(new
        {
            id = "docs", done = false, refused = "refused", message = "the graph does not hold.",
            problems = new[] { new { code = "loop", node = "qa", words = "`qa` waits on itself." } },
        });
        Assert.Equal("the graph does not hold. `qa` waits on itself.", (await View(problems, new { id = "docs", graph = After, @base = 1 })).Refusal);

        Assert.Contains("`id`", (await View(new StubGrounds(), new { graph = After })).Refusal);
    }

    [Fact]
    public void It_is_the_view_of_the_save()
    {
        Assert.IsType<WorkflowSaveView>(PressViews.Find(DriverModule.Name, "WORKFLOW_SAVE"));
    }
}
