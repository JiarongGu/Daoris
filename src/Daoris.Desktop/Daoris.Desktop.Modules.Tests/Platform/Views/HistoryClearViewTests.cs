using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A clear of finished history as its press's card shows it: what would go, counted from the first press's list, refused at the ask
/// in the verb's own words where the list keeps it, and the command the press is.
/// </summary>
public sealed class HistoryClearViewTests
{
    private static object Unit(string kind, string id, int sessions = 2, int quests = 1, string[]? forgotten = null, object? keep = null, int kept = 0) => new
    {
        kind, id, workspace = "work", clearable = keep is null,
        quests = Enumerable.Range(0, quests).Select(at => $"q{at}").ToArray(), forgotten = forgotten ?? [],
        asks = kind == "ask" ? new[] { id } : [], sessions = Enumerable.Range(0, sessions).Select(at => $"s{at}").ToArray(),
        teammates = Array.Empty<string>(), keep, kept = Enumerable.Range(0, kept).Select(_ => new { code = "HISTORY_LIVE" }).ToArray(),
        bytes = new { total = 2048L },
    };

    /// <summary>The list the route answers: its words where the verb asks for them, its fields where the page does.</summary>
    private static StubGrounds Listing(object plan, int code = 0, string said = "")
    {
        var grounds = new StubGrounds();
        grounds.Reads["HISTORY_PLAN"] = payload => payload is { } body && body.TryGetProperty("said", out var words) && words.ValueKind == JsonValueKind.True
            ? new { said = new { @out = new[] { said }, error = Array.Empty<string>(), code } }
            : plan;
        return grounds;
    }

    private static Task<PressViewed> View(StubGrounds grounds, object payload) =>
        new HistoryClearView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_quests_clear_says_what_goes_and_the_command_it_is()
    {
        var grounds = Listing(new { scope = "quest", id = "q1", units = new[] { Unit("quest", "q1", forgotten: ["q1"], kept: 1) } });

        var viewed = await View(grounds, new { said = true, quest = "q1" });

        Assert.Equal("history-clear-quest", viewed.View!.Kind);
        Assert.Equal("q1", viewed.View.Facts["quest"]);
        Assert.Equal("2", viewed.View.Facts["sessions"]);
        Assert.Equal("2 KB", viewed.View.Facts["size"]);
        Assert.Equal("daoris quest clear q1 --yes", viewed.View.Command);
        Assert.Equal(["history-clear-remote", "history-clear-pieces"], viewed.View.Also!.Select(said => said.Kind));
    }

    [Fact]
    public async Task An_asks_and_a_quests_failed_sessions_are_their_own_sentences()
    {
        var ask = await View(Listing(new { scope = "ask", id = "a1", units = new[] { Unit("ask", "a1", quests: 3) } }), new { said = true, ask = "a1" });
        Assert.Equal("history-clear-ask", ask.View!.Kind);
        Assert.Equal("3", ask.View.Facts["quests"]);
        Assert.Equal("daoris ask clear a1 --yes", ask.View.Command);

        var failed = await View(Listing(new { scope = "failed", id = "q1", units = new[] { Unit("failed", "q1", quests: 0) } }),
            new { said = true, quest = "q1", failed = true });
        Assert.Equal("history-clear-failed", failed.View!.Kind);
        Assert.Equal("daoris quest clear q1 --failed --yes", failed.View.Command);

        var none = await View(Listing(new { scope = "failed", id = "q1", units = new[] { Unit("failed", "q1", sessions: 0, quests: 0) } }),
            new { said = true, quest = "q1", failed = true });
        Assert.Equal("history-clear-failed-none", none.View!.Kind);
    }

    [Fact]
    public async Task A_workspaces_clear_counts_what_its_list_takes_and_what_it_keeps()
    {
        var plan = new
        {
            scope = "workspace", id = "my team",
            units = new[] { Unit("quest", "q1", forgotten: ["q1"]), Unit("quest", "q2", keep: new { code = "HISTORY_OPEN" }) },
            reading = new { takes = new { quests = 4, asks = 1, sessions = 6, teammates = 0, bytes = 3L * 1024 * 1024 }, leftOver = new { count = 2, bytes = 10L } },
        };

        var viewed = await View(Listing(plan), new { said = true, workspace = "my team" });

        Assert.Equal("history-clear-workspace", viewed.View!.Kind);
        Assert.Equal("4", viewed.View.Facts["quests"]);
        Assert.Equal("3 MB", viewed.View.Facts["size"]);
        Assert.Equal("daoris history clear --workspace \"my team\" --yes", viewed.View.Command);
        Assert.Equal(["history-clear-leftover", "history-clear-workspace-keeps", "history-clear-workspace-remote"], viewed.View.Also!.Select(said => said.Kind));
    }

    [Fact]
    public async Task A_workspace_with_nothing_that_may_go_is_said_not_refused()
    {
        var plan = new
        {
            scope = "workspace", id = "work", units = Array.Empty<object>(),
            reading = new { takes = new { quests = 0, asks = 0, sessions = 0, teammates = 0, bytes = 0L }, leftOver = new { count = 0, bytes = 0L } },
        };

        Assert.Equal("history-clear-workspace-nothing", (await View(Listing(plan), new { said = true, workspace = "work" })).View!.Kind);
    }

    [Fact]
    public async Task What_the_list_keeps_is_refused_at_the_ask_in_the_verbs_words_with_no_card()
    {
        var kept = Listing(new { }, code: 1, said: "#q1 stays on this machine: it is still open.");

        Assert.Equal("#q1 stays on this machine: it is still open.", (await View(kept, new { said = true, quest = "q1" })).Refusal);
        Assert.Contains("one workspace, one quest or one ask", (await View(kept, new { said = true, quest = "q1", ask = "a1" })).Refusal);
        Assert.Contains("only beside a quest", (await View(kept, new { said = true, ask = "a1", failed = true })).Refusal);
    }

    [Fact]
    public void It_is_the_view_of_the_clear()
    {
        Assert.IsType<HistoryClearView>(PressViews.Find(DriverModule.Name, "HISTORY_CLEAR"));
    }
}
