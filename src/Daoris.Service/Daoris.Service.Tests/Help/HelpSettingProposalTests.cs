using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>The <c>setting</c> kind's writer (HELP1c): one of the driver's doors, spelled as the CLI's verbs are.</summary>
public sealed class HelpSettingProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void A_setting_is_written_as_a_file_the_driver_judges_with_who_proposed_it_and_why()
    {
        var (id, message) = Box().ProposeSetting(
            new SettingChange("landing", Target: null, Workspace: "work", Value: "branch feature/{quest}-{slug} --tidy"),
            "the person asked for work to land on feature branches", session: "h1e1p000", Now);

        Assert.NotNull(id);
        Assert.Contains($"#{id}", message);
        Assert.Contains("Apply", message);
        var file = Written(id!);
        Assert.Equal("setting", file.GetProperty("kind").GetString());
        Assert.Equal("landing", file.GetProperty("door").GetString());
        Assert.Equal("work", file.GetProperty("workspace").GetString());
        Assert.Equal(JsonValueKind.Null, file.GetProperty("target").ValueKind);
        Assert.Equal("branch feature/{quest}-{slug} --tidy", file.GetProperty("value").GetString());
        Assert.Equal("h1e1p000", file.GetProperty("by").GetProperty("session").GetString());
        Assert.Equal("proposed", file.GetProperty("state").GetString());
        Assert.Equal("the person asked for work to land on feature branches", file.GetProperty("why").GetString());
    }

    /// <summary>HELP8: a branch rule naming a plugin passes the shape; whether the plugin lands work here is the driver's to judge.</summary>
    [Fact]
    public void A_landing_naming_a_plugin_is_written_as_the_terminal_spells_it()
    {
        var (id, _) = Box().ProposeSetting(
            new SettingChange("landing", Target: "engine", Workspace: null, Value: "branch feature/{quest}-{slug} --plugin example.lands"),
            "the person wants a pull request opened for each landing", session: "h1e1p000", Now);

        Assert.Equal("branch feature/{quest}-{slug} --plugin example.lands", Written(id!).GetProperty("value").GetString());
        Assert.Contains("`--plugin <id>`", Box().ProposeSetting(
            new SettingChange("landing", Target: "engine", Workspace: null, Value: "rebase"), "why", session: "h1e1p000", Now).Message);
    }

    /// <summary>The shape is checked here, and nothing more: what the route would say is the driver's.</summary>
    [Theory]
    [InlineData("push", "engine", null, null, "is not a door")]
    [InlineData("drive", null, null, null, "names the repository")]
    [InlineData("trees", "engine", null, "sometimes", "`on` or `off`")]
    [InlineData("line", null, null, "develop", "a repository or a workspace")]
    [InlineData("line", "engine", "work", "develop", "a repository or a workspace")]
    [InlineData("line", "engine", null, null, "a branch, or `--clear`")]
    [InlineData("landing", "engine", null, "rebase", "`merge`, `branch <pattern>`")]
    [InlineData("intake", null, null, null, "an agent, or `off`")]
    [InlineData("strikes", null, null, "many", "a whole number")]
    [InlineData("timeout", null, null, "0", "a whole number of minutes, 1 or more")]
    [InlineData("notify", null, null, "loud", "`on` or `off`")]
    public void A_setting_that_is_no_door_s_shape_is_refused_with_nothing_written(
        string door, string? target, string? workspace, string? value, string says)
    {
        var (id, message) = Box().ProposeSetting(new SettingChange(door, target, workspace, value), "a reason", "h1", Now);

        Assert.Null(id);
        Assert.Contains(says, message);
        Assert.Contains("Nothing was proposed", message);
        Assert.False(Directory.Exists(Path.Combine(_home, "help", "proposals")));
    }
}
