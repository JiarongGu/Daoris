using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A new ask as its press's card shows it (<c>ASK_NEW</c>): where it is made, its words, what it carries and the command it is; what the
/// verb would refuse is refused at the ask in its words, so the person never sees it.
/// </summary>
public sealed class AskNewViewTests
{
    private static StubGrounds Machine()
    {
        var grounds = new StubGrounds { Facts = new HelpMachineFacts(["engine"], ["default", "games"], []) };
        grounds.Placed["engine"] = "games";
        grounds.Registered.Add("engine");
        return grounds;
    }

    private static Task<PressViewed> View(object payload) =>
        new AskNewView().ViewAsync(Machine(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_new_ask_says_where_it_is_made_what_it_carries_and_the_command_it_is()
    {
        var viewed = await View(new
        {
            workspace = "games", sentence = "fix the stall", to = "engine", @short = "Stall", review = "off",
            links = new[] { "https://example.test/1" }, said = true,
        });

        Assert.Equal("ask-new-to", viewed.View!.Kind);
        Assert.Equal(("games", "fix the stall", "engine"), (viewed.View.Facts["workspace"], viewed.View.Facts["sentence"], viewed.View.Facts["to"]));
        Assert.Equal(["ask-new-short", "ask-new-review-off", "ask-new-links"], viewed.View.Also!.Select(said => said.Kind));
        Assert.Equal("daoris ask --workspace games --to engine --short Stall --review off --url https://example.test/1 \"fix the stall\"", viewed.View.Command);
    }

    [Fact]
    public async Task The_review_rule_choice_sends_no_choice_so_its_card_names_no_environment()
    {
        var viewed = await View(new { workspace = "default", sentence = "tidy the docs", review = "rule" });

        Assert.Null(viewed.View!.Also);
        Assert.Equal("daoris ask --workspace default --review rule \"tidy the docs\"", viewed.View.Command);
    }

    [Fact]
    public async Task A_file_this_machine_lacks_is_refused_at_the_ask_as_the_verb_refuses_it()
    {
        var missing = Path.Combine(AppContext.BaseDirectory, "no-such-file.txt");

        var viewed = await View(new { workspace = "default", sentence = "tidy the docs", files = new[] { missing } });

        Assert.Null(viewed.View);
        Assert.Equal($"`{missing}` is not a file on this machine. Nothing was asked.", viewed.Refusal);
        var present = await View(new { workspace = "default", sentence = "tidy the docs", files = new[] { typeof(AskNewViewTests).Assembly.Location } });
        Assert.Contains("ask-new-files", present.View!.Also!.Select(said => said.Kind));
    }

    [Fact]
    public async Task An_ask_its_intake_answers_names_no_receiver()
    {
        var viewed = await View(new { workspace = "default", sentence = "tidy the docs", said = true });

        Assert.Equal("ask-new", viewed.View!.Kind);
        Assert.Null(viewed.View.Also);
        Assert.Equal("daoris ask --workspace default \"tidy the docs\"", viewed.View.Command);
    }

    [Fact]
    public async Task What_the_verb_would_refuse_is_refused_at_the_ask_with_no_card()
    {
        Assert.Contains("needs its words", (await View(new { workspace = "games", sentence = " " })).Refusal);
        Assert.Contains("no workspace `nowhere`", (await View(new { workspace = "nowhere", sentence = "x" })).Refusal);
        Assert.Contains("`ghost`", (await View(new { workspace = "games", sentence = "x", to = "ghost" })).Refusal);
        Assert.Contains("No review environment is declared in workspace `games`", (await View(new { workspace = "games", sentence = "x", review = "lab" })).Refusal);
        Assert.Contains("--review-words go with a review choice", (await View(new { workspace = "games", sentence = "x", reviewWords = "why" })).Refusal);
    }

    [Fact]
    public void It_is_the_view_of_the_new_ask()
    {
        Assert.IsType<AskNewView>(PressViews.Find(DriverModule.Name, "ASK_NEW"));
    }
}
