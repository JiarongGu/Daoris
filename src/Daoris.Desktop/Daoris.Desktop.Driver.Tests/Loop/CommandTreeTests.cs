using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1ai-i (D228; the oneway design §11): one name, <c>daoris</c>, and one command tree, its words held to the fixture in
/// <c>CommandWordsTests</c>. A person's words are routed once into the words each command reads; a doctrine command is the CLI's, and a
/// word handed once is never handed back.
/// </summary>
public sealed class CommandTreeTests
{
    /// <summary>Each group's words, routed into the words its command reads: the one place the two spellings meet.</summary>
    [Theory]
    [InlineData("session say s-1 hello", "sessions say s-1 hello")]
    [InlineData("session list --json", "sessions --json")]
    [InlineData("session opinion ask s-1 --agent codex", "opinion ask s-1 --agent codex")]
    [InlineData("tree land s-1 --plan", "trees land s-1 --plan")]
    [InlineData("place drive engine", "driver drive engine")]
    [InlineData("place list", "driver list")]
    [InlineData("place access engine full", "access engine full")]
    [InlineData("place heavy engine", "heavy engine")]
    [InlineData("place checks engine light", "checks engine light")]
    [InlineData("place engine --preset keep --plan", "place engine --preset keep --plan")]
    [InlineData("settings notify off", "driver notify off")]
    [InlineData("role", "driver role")]
    [InlineData("role set build", "driver role set build")]
    [InlineData("role brief", "roles brief")]
    [InlineData("quest retry 12", "driver retry 12")]
    [InlineData("quest done 12", "quest done 12")]
    [InlineData("workflow new docs --from x", "driver workflow new docs --from x")]
    [InlineData("workflow run --session s-1", "workflow run --session s-1")]
    [InlineData("convert --plan", "convert --plan")]
    [InlineData("gate run --look", "run-heavy --look")]
    [InlineData("gate verify engine", "verify engine")]
    [InlineData("press result r-1", "result r-1")]
    [InlineData("press withdraw r-1", "withdraw r-1")]
    [InlineData("repository register", "register")]
    [InlineData("repository connect --workspace work", "repository connect --workspace work")]
    [InlineData("remote sync status", "sync status")]
    [InlineData("remote list", "remote list")]
    [InlineData("ask-daoris mode full", "help mode full")]
    [InlineData("ask publish a-1 --to engine", "ask --publish a-1 --to engine")]
    [InlineData("ask review a-1 off", "ask --set-review a-1 off")]
    [InlineData("ask channel a-1", "ask channel a-1")]
    [InlineData("ask --to engine fix it", "ask --to engine fix it")]
    [InlineData("agent account order claude-code a b", "agent profile order claude-code a b")]
    [InlineData("agent login claude-code --account work", "agent login claude-code --account work")]
    [InlineData("plugin add D:/p", "plugin add D:/p")]
    [InlineData("plugin new acme --point gate", "plugins new acme --point gate")]
    [InlineData("brainstorm why --agents a,b", "brainstorm why --agents a,b")]
    [InlineData("brainstorm why --kind meeting --agents a,b", "meeting ask why --agents a,b")]
    [InlineData("brainstorm show m-1 --kind meeting", "meeting show m-1")]
    [InlineData("drive --until-idle", "drive --until-idle")]
    [InlineData("chat --repository engine --agent codex", "chat --repository engine --agent codex")]
    public void ONEWAY1ai_i_a_groups_words_route_into_its_commands(string typed, string inward)
    {
        var route = CommandTree.Route(typed.Split(' '));

        Assert.Equal(TreeRouteKind.Run, route.Kind);
        Assert.Equal(inward.Split(' '), route.Words);
    }

    [Fact]
    public void ONEWAY1ai_i_doctrine_is_the_clis_and_is_never_handed_back()
    {
        Assert.Equal(TreeRouteKind.Doctrine, CommandTree.Route(["check"]).Kind);
        Assert.Equal(TreeRouteKind.Doctrine, CommandTree.Route(["--version"]).Kind);
        Assert.Equal(TreeRouteKind.Doctrine, CommandTree.Route(["sync", "--dry-run"]).Kind);
        Assert.Equal(TreeRouteKind.Refused, CommandTree.Route(["check"], handedBy: "cli").Kind);
        // A group's word the CLI handed is this program's own.
        Assert.Equal(TreeRouteKind.Run, CommandTree.Route(["session", "list"], handedBy: "cli").Kind);
    }

    [Fact]
    public void ONEWAY1ai_i_help_is_the_trees_usage_or_a_groups()
    {
        var tree = CommandTree.Route(["help"]);
        Assert.Equal((TreeRouteKind.Usage, null, true), (tree.Kind, tree.Group, tree.Asked));
        var group = CommandTree.Route(["help", "session"]);
        Assert.True(group.Asked);
        Assert.Equal("session", group.Group);
        Assert.Contains("session say <id>", DriverCommand.UsageOf("session"));
        Assert.DoesNotContain("tree land", DriverCommand.UsageOf("session"));
        Assert.True(CommandTree.Route(["--help"]).Asked);
        Assert.False(CommandTree.Route([]).Asked);

        // A group that needs a sub-verb says its own lines, exit 2; a word no group has says so.
        var bare = CommandTree.Route(["session"]);
        Assert.Equal((TreeRouteKind.Usage, "session", false), (bare.Kind, bare.Group, bare.Asked));
        var unknown = CommandTree.Route(["sessionz"]);
        Assert.Equal(TreeRouteKind.Usage, unknown.Kind);
        Assert.Contains("no command `sessionz`", unknown.Said);
    }
}
