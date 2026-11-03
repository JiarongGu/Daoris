using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ONEWAY1ai-i (D228; the oneway design §11): one name, <c>daoris</c>, and one command tree, its words held to the fixture in
/// <c>CommandWordsTests</c>. A person's words are routed once into the words each command reads; every old form is refused naming its
/// new one and does nothing; a doctrine command is the CLI's, and a word handed once is never handed back.
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

    /// <summary>Every old form is refused, exit 2, naming its new form; the new form routes.</summary>
    [Fact]
    public void ONEWAY1ai_i_every_old_form_is_refused_naming_its_new_one()
    {
        foreach (var move in CommandWords.Moved.Where(move => !move.Launcher))
        {
            var route = CommandTree.Route([.. move.From.Split(' '), "x"]);
            Assert.Equal(TreeRouteKind.Refused, route.Kind);
            Assert.Contains($"`daoris {move.From}`", route.Said);
            Assert.Contains($"`daoris {move.To}", route.Said);
            Assert.Contains("nothing was done", route.Said);
            if (move.To != "help") Assert.NotEqual(TreeRouteKind.Refused, CommandTree.Route([.. move.To.Split(' '), "x"]).Kind);
        }

        // The remotes' `sync` is the old launcher's alone: at `daoris` it is the doctrine's.
        Assert.Equal(TreeRouteKind.Doctrine, CommandTree.Route(["sync"]).Kind);
    }

    [Theory]
    [InlineData("sessions --json", "`daoris sessions` is `daoris session list` now")]
    [InlineData("sessions say s-1 hi", "`daoris sessions` is `daoris session` now, with the same words after it")]
    [InlineData("driver role set build x", "`daoris driver role` is `daoris role` now, with the same words after it")]
    [InlineData("meeting ask why --agents a,b", "`daoris meeting ask` is `daoris brainstorm … --kind meeting` now")]
    [InlineData("--once", "`daoris --once` is `daoris drive --once` now")]
    [InlineData("ask --publish a-1 --to engine", "`daoris ask --publish` is `daoris ask publish` now")]
    [InlineData("ask --to engine fix it --set-review x", "`ask --set-review` is `daoris ask review` now")]
    [InlineData("chat --repository engine --adapter codex", "`--adapter` is `--agent` now")]
    [InlineData("agent login claude-code --profile work", "`--profile` is `--account` now")]
    [InlineData("session opinion ask s-1 --reviewer codex", "`--reviewer` is `--agent` now")]
    [InlineData("place opinion engine --reviewers codex", "`--reviewers` is `--agents` now")]
    [InlineData("help mode full", "`daoris help mode` is `daoris ask-daoris mode` now")]
    public void ONEWAY1ai_i_an_old_form_says_its_new_one(string typed, string said)
    {
        var route = CommandTree.Route(typed.Split(' '));

        Assert.Equal(TreeRouteKind.Refused, route.Kind);
        Assert.Contains(said, route.Said);
    }

    /// <summary>ONEWAY1a (the oneway design §2 S1): a role's agents are the role door's, so each old staffing verb names `role set` and its role.</summary>
    [Theory]
    [InlineData("driver adapter claude-code", "`daoris driver adapter` is `daoris role set build` now")]
    [InlineData("settings adapter claude-code", "`daoris settings adapter` is `daoris role set build` now")]
    [InlineData("driver intake off", "`daoris driver intake` is `daoris role set intake` now")]
    [InlineData("settings intake off", "`daoris settings intake` is `daoris role set intake` now")]
    [InlineData("driver helper codex", "`daoris driver helper` is `daoris role set ask-daoris` now")]
    [InlineData("settings helper codex", "`daoris settings helper` is `daoris role set ask-daoris` now")]
    public void ONEWAY1a_an_old_staffing_verb_names_the_role_door(string typed, string said)
    {
        var route = CommandTree.Route(typed.Split(' '));

        Assert.Equal(TreeRouteKind.Refused, route.Kind);
        Assert.Contains(said, route.Said);
        Assert.DoesNotContain(CommandWords.Groups.Single(group => group.Name == "settings").Verbs, verb => verb is "adapter" or "intake" or "helper");
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
