using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris ask list</c> and <c>daoris quest list</c> are reads, and an ask whose whole text is one word that names a verb of
/// <c>ask</c> is refused, naming the verb probably meant, never made an ask.
/// </summary>
public sealed class ListVerbsTests
{
    [Fact]
    public void Ask_list_is_a_read_with_its_flags_and_no_words()
    {
        var read = AskArguments.Read(["--list", "--workspace", "games", "--open", "--json"], out var problem)!;

        Assert.Null(problem);
        Assert.True(read.List);
        Assert.Equal(("games", true, true, false), (read.Workspace, read.Open, read.Json, read.All));
    }

    [Theory]
    [InlineData("--list", "--all", "--workspace", "games")]
    [InlineData("--list", "fix", "the", "stall")]
    public void Ask_list_refuses_a_workspace_with_all_and_any_words(params string[] args)
    {
        Assert.Null(AskArguments.Read(args, out var problem));
        Assert.Contains("`ask list`", problem);
    }

    [Fact]
    public void The_list_flags_alone_are_no_ask()
    {
        Assert.Null(AskArguments.Read(["--open", "fix it"], out var problem));
        Assert.Contains("go with `ask list`", problem);
    }

    [Theory]
    [InlineData("list", "daoris ask list")]
    [InlineData("show", "daoris ask list")]
    [InlineData("close", "daoris ask close <id>")]
    [InlineData("go-ahead", "daoris ask go-ahead <id>")]
    public void An_asks_whole_text_that_is_one_verbs_name_is_refused_with_the_verb_meant(string word, string meant)
    {
        Assert.Null(AskArguments.Read(["--workspace", "games", word], out var problem));
        Assert.Contains($"`{word}` alone is a verb of `daoris ask`", problem);
        Assert.Contains(meant, problem);
    }

    [Theory]
    [InlineData("list the failing builds")]
    [InlineData("fix")]
    public void Any_other_ask_is_read_as_before(string words)
    {
        Assert.NotNull(AskArguments.Read([words], out var problem));
        Assert.Null(problem);
    }

    [Fact]
    public void The_tree_hands_ask_list_on_as_the_read()
    {
        var route = CommandTree.Route(["ask", "list", "--workspace", "games"]);

        Assert.Equal(TreeRouteKind.Run, route.Kind);
        Assert.Equal(["ask", "--list", "--workspace", "games"], route.Words);
    }

    [Fact]
    public void Quest_list_reads_its_flags_and_refuses_any_other()
    {
        var read = QuestListCommand.Read(["list", "--repository", "engine", "--workspace", "games", "--open", "--json"], out var problem)!;

        Assert.Null(problem);
        Assert.Equal(new QuestListRead("engine", "games", true, true), read);
        Assert.Null(QuestListCommand.Read(["list", "--frobnicate"], out var unknown));
        Assert.Contains("`--frobnicate`", unknown);
        Assert.Null(QuestListCommand.Read(["list", "--repository"], out var bare));
        Assert.Contains("--repository needs a value", bare);
        Assert.Equal(TreeRouteKind.Run, CommandTree.Route(["quest", "list", "--open"]).Kind);
    }

    [Fact]
    public void A_line_says_the_id_the_state_and_the_words_cut_to_one_line()
    {
        Assert.Equal("#a3  Closed  games  fix the stall", AskListCommand.Line("a3", "Closed", "games", "fix\nthe stall"));
        Assert.EndsWith("…", AskListCommand.Line("a3", "Closed", "games", new string('x', 200)));
        Assert.Equal("#q7  Taken  engine  Stall", QuestListCommand.Line("q7", "Taken", "engine", "Stall"));
    }
}
