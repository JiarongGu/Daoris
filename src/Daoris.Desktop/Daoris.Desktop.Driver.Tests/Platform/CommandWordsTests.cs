using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The one command tree's words, collected at start; <c>CommandTreeTests</c> holds that every word routes.</summary>
public sealed class CommandWordsTests
{
    [CommandGroup("twice", "one")]
    private static class FirstOwner { }

    [CommandGroup("twice", "two", Nested = ["two: a b"])]
    private static class SecondOwner { }

    /// <summary>A group has one owner: a name two units declare fails the collection, naming both.</summary>
    [Fact]
    public void A_group_two_units_declare_is_refused_naming_both()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => CommandWords.Collect(typeof(CommandWordsTests).Assembly));

        Assert.Contains("`twice` is declared twice", refused.Message);
        Assert.Contains(nameof(FirstOwner), refused.Message);
        Assert.Contains(nameof(SecondOwner), refused.Message);
    }

    /// <summary>The groups are collected by name, each with what its mark declares, a nested sub-verb's own sub-verbs among it.</summary>
    [Fact]
    public void The_groups_are_the_marks_by_name()
    {
        Assert.Equal(CommandWords.Groups.Select(group => group.Name).Order(StringComparer.Ordinal), CommandWords.Groups.Select(group => group.Name));
        var agent = CommandWords.Group("agent")!;
        Assert.Contains("account", agent.Verbs);
        Assert.Equal(["list", "add", "remove", "default", "order", "use", "ready", "rename", "join"], agent.Nested["account"]);
        Assert.Equal("\"…\"", CommandWords.Group("ask")!.Takes);
        Assert.True(CommandWords.Group("drive")!.Whole);
    }

    [Theory]
    [InlineData("tree land s-1", "daoris tree land")]
    [InlineData("ask pause a-1", "daoris ask pause")]
    [InlineData("tree remove some/tree/path", "daoris tree remove")]
    [InlineData("repository register", "daoris repository register")]
    [InlineData("ask-daoris mode full", "daoris ask-daoris mode")]
    [InlineData("place heavy engine build=x", "daoris place heavy")]
    [InlineData("place drive engine", "daoris place drive")]
    [InlineData("quest retry 12 --at 2", "daoris quest retry")]
    [InlineData("workflow new docs --from merge-after-accept", "daoris workflow new")]
    [InlineData("tool use git system", "daoris tool use")]
    [InlineData("agent account order claude-code account-1", "daoris agent account")]
    [InlineData("plugin acme.gate", "daoris plugin")]
    [InlineData("workflow skip q7 go nobody raises it", "daoris workflow skip")]
    [InlineData("drive --once", "daoris drive")]
    public void ONEWAY1ai_i_a_lease_names_the_command_and_never_its_arguments(string typed, string named) =>
        Assert.Equal(named, CommandWords.Named(typed.Split(' ')));

    /// <summary>Where no Node.js runs, the doctrine command says so with the tools' reason, and does nothing.</summary>
    [Fact]
    public void ONEWAY1ai_i_no_node_says_why_and_that_nothing_was_done()
    {
        Assert.Equal(
            "the doctrine commands run on Node.js 22 or later, and none runs here: `node` is not on this machine's PATH; nothing was done.",
            CommandWords.NoNode(null));
        Assert.EndsWith("none runs here: the file is gone; nothing was done.", CommandWords.NoNode("the file is gone."));
    }
}
