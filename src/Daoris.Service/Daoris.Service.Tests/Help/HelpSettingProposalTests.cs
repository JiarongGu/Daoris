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

    /// <summary>HELP9: reading and writing across (D107), a cap and an adapter pass the shape as the terminal spells them.</summary>
    [Theory]
    [InlineData("across", "engine", null, "read off")]
    [InlineData("across", null, "work", "read --clear")]
    [InlineData("across", "plugins", null, "write-to engine")]
    [InlineData("across", "plugins", null, "write-to engine --clear")]
    [InlineData("cap", null, null, "3")]
    [InlineData("adapter", null, null, "claude-code-acp")]
    public void Across_a_cap_and_an_adapter_are_written_as_the_terminal_spells_them(string door, string? target, string? workspace, string value)
    {
        var (id, _) = Box().ProposeSetting(new SettingChange(door, target, workspace, value), "the person asked", session: "h1", Now);

        Assert.NotNull(id);
        var file = Written(id!);
        Assert.Equal(door, file.GetProperty("door").GetString());
        Assert.Equal(value, file.GetProperty("value").GetString());
    }

    /// <summary>HELP10: a retry names its quest as the target, and nothing else; whether it is parked is the driver's to judge.</summary>
    [Fact]
    public void A_retry_is_written_with_its_quest_as_the_target()
    {
        var (id, _) = Box().ProposeSetting(new SettingChange("retry", "#q1a2b3c4", null, null), "the person asked", session: "h1", Now);

        var file = Written(id!);
        Assert.Equal("retry", file.GetProperty("door").GetString());
        Assert.Equal("#q1a2b3c4", file.GetProperty("target").GetString());
        Assert.Equal(JsonValueKind.Null, file.GetProperty("value").ValueKind);
    }

    /// <summary>HELP9: the connector's tool names every door the box takes, so the helper is told of each.</summary>
    [Fact]
    public void The_tool_names_every_door()
    {
        var method = typeof(Daoris.Knowledge.Mcp.KnowledgeTools).GetMethod(nameof(Daoris.Knowledge.Mcp.KnowledgeTools.ProposeSetting))!;
        var door = method.GetParameters().Single(parameter => parameter.Name == "door")
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        var tool = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;

        foreach (var name in HelpProposalBox.Doors)
        {
            Assert.Contains(name, door);
            Assert.Contains(name, tool);
        }
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
    [InlineData("across", null, null, "read off", "a repository or a workspace")]
    [InlineData("across", "engine", "work", "read off", "a repository or a workspace")]
    [InlineData("across", "engine", null, "read maybe", "`read on|off|--clear`")]
    [InlineData("across", null, "work", "write-to engine", "declared from one repository")]
    [InlineData("across", "plugins", null, "write-to", "`write-to <other>`")]
    [InlineData("across", "plugins", null, "write-to two words", "`write-to <other>`")]
    [InlineData("across", "engine", null, "peek", "`read on|off|--clear` or `write-to <other> [--clear]`")]
    [InlineData("cap", null, null, "0", "a whole number, 1 or more")]
    [InlineData("adapter", null, null, null, "an agent")]
    [InlineData("adapter", null, null, "two words", "an agent")]
    [InlineData("retry", null, null, null, "names the quest its failed sessions parked")]
    [InlineData("retry", "q1 q2", null, null, "names the quest its failed sessions parked")]
    [InlineData("retry", "q1a2b3c4", "work", null, "names the quest its failed sessions parked")]
    [InlineData("retry", "q1a2b3c4", null, "--at 2", "names the quest its failed sessions parked")]
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
