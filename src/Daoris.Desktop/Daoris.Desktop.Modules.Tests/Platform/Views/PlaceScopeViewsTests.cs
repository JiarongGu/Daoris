using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A line, a session language, a standing answer and reading and writing across as their presses' cards show them: refused with no card where a name is not registered here or the driver would refuse the value, and the command that sets it.</summary>
public sealed class PlaceScopeViewsTests
{
    private static StubGrounds Grounds()
    {
        var grounds = new StubGrounds();
        grounds.Placed["engine"] = "work";
        grounds.Placed["plugins"] = "work";
        grounds.Placed["my engine"] = "work";
        return grounds;
    }

    private static Task<PressViewed> View(IPressView view, string json) =>
        view.ViewAsync(Grounds(), JsonDocument.Parse(json).RootElement, CancellationToken.None);

    [Theory]
    [InlineData("""{"repository":"engine","branch":"develop"}""", "line-set", "daoris place line engine develop")]
    [InlineData("""{"repository":"engine","branch":null}""", "line-clear", "daoris place line engine --clear")]
    [InlineData("""{"workspace":"work","branch":"develop"}""", "line-workspace-set", "daoris place line --workspace work develop")]
    [InlineData("""{"workspace":"work","branch":""}""", "line-workspace-clear", "daoris place line --workspace work --clear")]
    [InlineData("""{"repository":"my engine","branch":"develop"}""", "line-set", "daoris place line \"my engine\" develop")]
    public async Task A_line_is_shown_with_its_command(string json, string kind, string command)
    {
        var viewed = await View(new PlaceLineView(), json);

        Assert.Equal((kind, command), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task A_line_names_one_registered_scope_and_a_branch_git_takes()
    {
        Assert.Contains("exactly one", (await View(new PlaceLineView(), """{"branch":"develop"}""")).Refusal);
        Assert.Contains("exactly one", (await View(new PlaceLineView(), """{"repository":"engine","workspace":"work","branch":"x"}""")).Refusal);
        Assert.Contains("`ghost` is not registered", (await View(new PlaceLineView(), """{"repository":"ghost","branch":"x"}""")).Refusal);
        Assert.Contains("no workspace `nowhere`", (await View(new PlaceLineView(), """{"workspace":"nowhere","branch":"x"}""")).Refusal);
        Assert.Contains("not a branch name", (await View(new PlaceLineView(), """{"repository":"engine","branch":"a b"}""")).Refusal);
    }

    [Fact]
    public async Task A_policy_press_with_no_branch_is_shown_by_its_values_alone()
    {
        var viewed = await View(new PlaceLineView(), """{"repository":"engine","policy":{"mode":"pr"}}""");

        Assert.Null(viewed.View);
        Assert.Null(viewed.Refusal);
    }

    [Theory]
    [InlineData("""{"repository":"engine","language":"zh"}""", "language-set", "daoris place language engine zh")]
    [InlineData("""{"workspace":"work","language":"en"}""", "language-workspace-set", "daoris place language --workspace work en")]
    [InlineData("""{"repository":"engine"}""", "language-clear", "daoris place language engine --clear")]
    [InlineData("""{"workspace":"work","language":null}""", "language-workspace-clear", "daoris place language --workspace work --clear")]
    public async Task A_session_language_is_shown_with_its_command(string json, string kind, string command)
    {
        var viewed = await View(new PlaceLanguageView(), json);

        Assert.Equal((kind, command), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task A_language_outside_the_closed_table_is_refused()
    {
        var viewed = await View(new PlaceLanguageView(), """{"repository":"engine","language":"klingon"}""");

        Assert.Null(viewed.View);
        Assert.Contains("klingon", viewed.Refusal);
    }

    [Fact]
    public async Task A_standing_answer_is_for_one_repository_and_quoted_in_its_command()
    {
        var shown = await View(new PlaceStandingView(), """{"repository":"engine","says":"say \"yes\" to tests"}""");

        Assert.Equal("standing-set", shown.View!.Kind);
        Assert.Equal("daoris place standing engine \"say \\\"yes\\\" to tests\"", shown.View.Command);
        Assert.Equal("daoris place standing engine --clear", (await View(new PlaceStandingView(), """{"repository":"engine"}""")).View!.Command);
        Assert.Contains("for a repository", (await View(new PlaceStandingView(), """{"workspace":"work","says":"x"}""")).Refusal);
        Assert.Contains("`ghost` is not registered", (await View(new PlaceStandingView(), """{"repository":"ghost","says":"x"}""")).Refusal);
    }

    [Theory]
    [InlineData("""{"repository":"engine","read":true}""", "across-read-on", "daoris place across engine read on")]
    [InlineData("""{"repository":"engine","read":false}""", "across-read-off", "daoris place across engine read off")]
    [InlineData("""{"repository":"engine"}""", "across-read-clear", "daoris place across engine read --clear")]
    [InlineData("""{"workspace":"work","read":false}""", "across-workspace-read-off", "daoris place across --workspace work read off")]
    public async Task Reading_across_is_shown_with_its_command(string json, string kind, string command)
    {
        var viewed = await View(PlaceAcrossView.Read(), json);

        Assert.Equal((kind, command), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task Writing_across_is_declared_from_one_repository_into_another_registered_one()
    {
        var shown = await View(PlaceAcrossView.WriteTo(), """{"repository":"plugins","to":"engine","allow":true}""");
        var taken = await View(PlaceAcrossView.WriteTo(), """{"repository":"plugins","to":"engine","allow":false}""");

        Assert.Equal(("across-write-to", "daoris place across plugins write-to engine"), (shown.View!.Kind, shown.View.Command));
        Assert.Equal(("across-write-clear", "daoris place across plugins write-to engine --clear"), (taken.View!.Kind, taken.View.Command));
        Assert.Equal("engine", shown.View.Facts["other"]);
        Assert.Contains("`ghost` is not registered", (await View(PlaceAcrossView.WriteTo(), """{"repository":"plugins","to":"ghost","allow":true}""")).Refusal);
        Assert.Contains("write-to", (await View(PlaceAcrossView.WriteTo(), """{"workspace":"work","to":"engine","allow":true}""")).Refusal);
        Assert.NotNull((await View(PlaceAcrossView.WriteTo(), """{"repository":"engine","to":"engine","allow":true}""")).Refusal);
    }
}
