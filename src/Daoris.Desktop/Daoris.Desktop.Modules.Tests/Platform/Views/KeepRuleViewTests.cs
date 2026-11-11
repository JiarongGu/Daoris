using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A keep rule as its press's card shows it: the terminal's one judgement at the ask, its clause and its command.</summary>
public sealed class KeepRuleViewTests
{
    private static Task<PressViewed> View(string json) =>
        new KeepRuleView().ViewAsync(new StubGrounds(), JsonDocument.Parse(json).RootElement, CancellationToken.None);

    [Theory]
    [InlineData("""{"kind":"set-aside","days":30}""", "daoris settings keep set-aside 30")]
    [InlineData("""{"kind":"set-aside","whole":true}""", "daoris settings keep set-aside whole")]
    [InlineData("""{"kind":"set-aside","whole":false}""", "daoris settings keep set-aside slim")]
    [InlineData("""{"kind":"downloads","never":true}""", "daoris settings keep downloads never")]
    [InlineData("""{"kind":"logs","default":true}""", "daoris settings keep logs default")]
    [InlineData("""{"kind":"history","days":90}""", "daoris settings keep history 90")]
    [InlineData("""{"kind":"floor","gb":20}""", "daoris settings keep floor 20GB")]
    [InlineData("""{"kind":"floor","percent":10}""", "daoris settings keep floor 10%")]
    [InlineData("""{"kind":"floor","off":true}""", "daoris settings keep floor off")]
    public async Task A_rule_is_shown_with_its_clause_and_its_command(string json, string command)
    {
        var viewed = await View(json);

        Assert.Equal(("keep", command), (viewed.View!.Kind, viewed.View.Command));
        Assert.NotEmpty(viewed.View.Facts["rule"]);
    }

    [Theory]
    [InlineData("""{"kind":"logs","days":0}""")]
    [InlineData("""{"kind":"logs","never":true}""")]
    [InlineData("""{"kind":"nothing","days":3}""")]
    [InlineData("""{"kind":"logs"}""")]
    public async Task A_rule_out_of_its_kinds_bounds_is_refused_with_no_card(string json)
    {
        var viewed = await View(json);

        Assert.Null(viewed.View);
        Assert.NotEmpty(viewed.Refusal!);
    }

    [Fact]
    public async Task A_rule_is_said_as_the_route_merges_it_into_what_the_machine_keeps_now()
    {
        var grounds = new StubGrounds { Config = Daoris.Driver.DriverConfig.Empty with { Keep = new Daoris.Driver.KeepChoices { SetAsideDays = 14 } } };

        var viewed = await new KeepRuleView().ViewAsync(grounds, JsonDocument.Parse("""{"kind":"set-aside","whole":true}""").RootElement, CancellationToken.None);

        Assert.Contains("14 days, whole", viewed.View!.Facts["rule"]);
    }

    [Fact]
    public async Task Days_and_a_size_together_have_no_one_command_and_are_refused()
    {
        var viewed = await View("""{"kind":"set-aside","days":30,"gb":5}""");

        Assert.Null(viewed.View);
        Assert.Contains("one at a time", viewed.Refusal);
    }
}
