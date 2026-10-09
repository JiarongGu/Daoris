using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The strip's review chip over the bridge (REVIEWENV1d; the review environment design §3.3): <c>STATE</c> answers the set-ups
/// waiting for the person here, each with whether Daoris serves its tab now, which the chip says; and <c>SHOW_REVIEW_AGAIN</c>
/// is *Show it again*, refused in the driver's words where nothing could show it. The verdict presses are REVIEWENV1g's.
/// </summary>
/// <remarks>What the showing serves and when it lets go is held by the driver's <c>ReviewShowingTests</c>; no service runs here.</remarks>
public sealed class DriverModuleReviewShowingTests : DriverModuleBridge
{
    private static readonly QuestView Waiting = new("q2", "ask #a1", "web-app", "Show #q1 in `local` for review", "", "Done")
    {
        SetUpIn = "local",
        Held = true,
        SetUps =
        [
            new QuestSetUpView("0123456789abcdef0123456789abcdef01234567")
            {
                Look = "http://localhost:4200/reports", Shows = "the new column", Again = "open reports", Served = "dist/app",
                Session = "s7", Local = true, Machine = "desk", Sequence = 41,
            },
        ],
    };

    private readonly Tabs _tabs = new();

    [Fact]
    public async Task The_state_says_each_set_up_waiting_here_and_whether_its_tab_is_served()
    {
        var loop = new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", reviews: _tabs);
        await loop.Reviews!.LookAsync([Waiting], (_, _) => Task.FromResult<QuestView?>(null), CancellationToken.None);
        var module = new DriverModule(Bus, loop);

        var unserved = (await AnswerAsync(module, "STATE")).GetProperty("inReview");
        _tabs.Served.Add("q2");
        var served = (await AnswerAsync(module, "STATE")).GetProperty("inReview");

        var row = Assert.Single(unserved.EnumerateArray());
        Assert.Equal("q2", row.GetProperty("quest").GetString());
        Assert.Equal("Show #q1 in `local` for review", row.GetProperty("title").GetString());
        Assert.Equal("local", row.GetProperty("environment").GetString());
        Assert.Equal("http://localhost:4200/reports", row.GetProperty("look").GetString());
        Assert.Equal("the new column", row.GetProperty("shows").GetString());
        Assert.False(row.GetProperty("served").GetBoolean());
        Assert.True(Assert.Single(served.EnumerateArray()).GetProperty("served").GetBoolean());
    }

    [Fact]
    public async Task A_loop_with_no_browser_says_no_review_at_all()
    {
        var state = await AnswerAsync(Module(), "STATE");

        // Null, which the bridge leaves out: a loop with no browser shows nothing, so it says nothing.
        Assert.True(!state.TryGetProperty("inReview", out var rows) || rows.ValueKind == System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Show_it_again_waits_for_the_driver_s_service()
    {
        var module = new DriverModule(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0", reviews: _tabs));

        var refused = await RefusalAsync(module, "SHOW_REVIEW_AGAIN", new { quest = "q2" });

        Assert.StartsWith(Refusals.DriverNotReady, refused, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Show_it_again_with_no_browser_is_refused_in_the_driver_s_words()
    {
        var refused = await RefusalAsync(Module(), "SHOW_REVIEW_AGAIN", new { quest = "q2" });

        Assert.StartsWith(Refusals.DriverRefused, refused, StringComparison.Ordinal);
        Assert.Contains("browser", refused, StringComparison.Ordinal);
    }

    /// <summary>The shell's half standing in: which quests it serves, as the page asks.</summary>
    private sealed class Tabs : IReviewTabs
    {
        public HashSet<string> Served { get; } = [];

        public IReadOnlyList<ReviewServe> Serving =>
            [.. Served.Select(quest => new ReviewServe(quest, ReviewDesk.TabTitle(quest), "X:/tree/dist/app", "http://localhost:4200", "/", "http://localhost:4200/"))];

        public Task OpenAsync(string quest, string title, CancellationToken ct = default) => Task.CompletedTask;

        public Task ServeAsync(ReviewServe serve, CancellationToken ct = default) => Task.CompletedTask;

        public void Stop(string quest) => Served.Remove(quest);
    }
}
