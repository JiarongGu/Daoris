using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A review, second-opinion, helpers and commit rule as their presses' cards show them: refused with no card in the rule table's words, and the card's sentence and command.</summary>
public sealed class PlaceRuleViewsTests
{
    private static StubGrounds Grounds()
    {
        var grounds = new StubGrounds();
        grounds.Placed["engine"] = "work";
        grounds.Placed["game"] = "work";
        return grounds;
    }

    /// <summary>The machine with <c>engine</c> checked out at <paramref name="root"/>, or with no checkout.</summary>
    private static StubGrounds Rooted(string? root)
    {
        var grounds = Grounds();
        grounds.Facts = grounds.Facts with { Registered = [("engine", "work", root), ("game", "work", null)] };
        return grounds;
    }

    private static Task<PressViewed> View(IPressView view, string json, StubGrounds? grounds = null) =>
        view.ViewAsync(grounds ?? Grounds(), JsonDocument.Parse(json).RootElement, CancellationToken.None);

    [Fact]
    public async Task A_review_environment_is_shown_with_what_it_lets_a_step_do_and_its_command()
    {
        var viewed = await View(new PlaceReviewView(),
            """{"repository":"engine","put":{"name":"dev","kind":"local","procedure":"README.md","address":"http://localhost:4200"},"required":true}""",
            Rooted(null));

        Assert.Equal("review", viewed.View!.Kind);
        Assert.Equal("daoris place review engine dev --kind local --procedure README.md --address http://localhost:4200 --required", viewed.View.Command);
        Assert.Contains("Declares the review environment `dev` for `engine`.", viewed.View.Facts["describe"]);
        Assert.Contains("waits for you", viewed.View.Facts["describe"]);
    }

    [Fact]
    public async Task A_workspace_review_clear_names_the_workspace()
    {
        var viewed = await View(new PlaceReviewView(), """{"workspace":"work","clear":true}""");

        Assert.Equal("daoris place review --workspace work --clear", viewed.View!.Command);
        Assert.Contains("Clears workspace `work`'s review rule", viewed.View.Facts["describe"]);
    }

    [Theory]
    [InlineData("""{"repository":"engine","workspace":"work","clear":true}""", "exactly one")]
    [InlineData("""{"repository":"ghost","clear":true}""", "`ghost` is not registered")]
    [InlineData("""{"repository":"engine"}""", "declares an environment")]
    [InlineData("""{"repository":"engine","required":true}""", "no review rule of its own")]
    [InlineData("""{"repository":"engine","put":{"name":"prod","kind":"deployed","procedure":"README.md"}}""", "production")]
    public async Task A_review_the_table_refuses_is_refused_with_no_card(string json, string says)
    {
        var viewed = await View(new PlaceReviewView(), json);

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Fact]
    public async Task A_review_procedure_the_checkout_does_not_hold_is_refused_and_one_with_no_checkout_is_said_unchecked()
    {
        var put = """{"repository":"engine","put":{"name":"dev","kind":"local","procedure":"nowhere.md","address":"http://localhost:4200"}}""";
        var root = Path.Combine(AppContext.BaseDirectory, "review-view-checkout");

        Directory.CreateDirectory(root);
        var held = await View(new PlaceReviewView(), put, Rooted(root));
        var unchecked_ = await View(new PlaceReviewView(), put, Rooted(null));

        Assert.Contains("is not a file in `engine`'s checkout", held.Refusal);
        Assert.Contains("Not checked", unchecked_.View!.Facts["describe"]);
    }

    [Fact]
    public async Task An_opinion_is_shown_with_its_reviewers_and_its_command()
    {
        var viewed = await View(new PlaceOpinionView(), """{"repository":"engine","set":{"reviewers":["codex-acp"],"required":true}}""");

        Assert.Equal("daoris place opinion engine --agents codex-acp --required", viewed.View!.Command);
        Assert.Contains("Sets the second opinion for `engine`.", viewed.View.Facts["describe"]);
    }

    [Theory]
    [InlineData("""{"repository":"engine"}""", "names `--agents`")]
    [InlineData("""{"repository":"engine","set":{"required":true}}""", "no second-opinion rule of its own")]
    [InlineData("""{"workspace":"work","set":{"required":true}}""", "no second-opinion rule of its own")]
    public async Task An_opinion_the_table_refuses_is_refused_with_no_card(string json, string says)
    {
        var viewed = await View(new PlaceOpinionView(), json);

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Theory]
    [InlineData("""{"repository":"engine","set":{"mode":"on","agents":["codex-acp"]}}""", "daoris place helpers engine --mode on --agents codex-acp")]
    [InlineData("""{"workspace":"work","clear":true}""", "daoris place helpers --workspace work --clear")]
    public async Task A_helpers_rule_is_shown_with_its_command(string json, string command)
    {
        var viewed = await View(new PlaceHelpersView(), json);

        Assert.Equal(("helpers", command), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task A_helpers_rule_the_table_refuses_is_refused_with_no_card()
    {
        var viewed = await View(new PlaceHelpersView(), """{"repository":"engine","set":{"mode":"sometimes"}}""");

        Assert.Null(viewed.View);
        Assert.NotEmpty(viewed.Refusal!);
    }

    [Theory]
    [InlineData("""{"repository":"engine","set":{"mode":"ask","convention":"Imperative subject line."}}""", "daoris place commits engine --mode ask --convention \"Imperative subject line.\"")]
    [InlineData("""{"repository":"engine","set":{"convention":""}}""", "daoris place commits engine --no-convention")]
    [InlineData("""{"repository":"engine","clear":true}""", "daoris place commits engine --clear")]
    public async Task A_commit_rule_is_shown_with_its_command(string json, string command)
    {
        var viewed = await View(new PlaceCommitsView(), json);

        Assert.Equal(("commits", command), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task A_commit_rule_says_how_work_is_committed_and_quotes_its_convention()
    {
        var viewed = await View(new PlaceCommitsView(), """{"repository":"engine","set":{"mode":"at-end","convention":"Short."}}""");

        Assert.Contains("Commit messages follow this convention: \"Short.\"", viewed.View!.Facts["describe"]);
    }
}
