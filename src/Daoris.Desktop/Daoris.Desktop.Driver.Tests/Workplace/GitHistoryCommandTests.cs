using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The terminal's history-edit verbs: <c>git pick|revert &lt;repository&gt; &lt;branch&gt; &lt;commit|A..B&gt;</c> and
/// <c>git rebase &lt;repository&gt; &lt;branch&gt; &lt;onto&gt;</c>, each <c>[--plan|--yes]</c>. A plan says what it would do and writes nothing;
/// <c>--yes</c> says the plan, then with a Daoris running is the person's press over the pipe carrying the ids the plan pinned; with none
/// it runs the journal's act itself, refused from a session's terminal.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitHistoryCommandTests : TreeSyncFixture
{
    private GitHistoryWorld World(string root) => new(
        (_, _) => Task.FromResult<string?>(root), (_, _, _) => Task.FromResult<string?>("main"),
        () => new ActRuns(new ActJournal(Home, () => StoreSettled.Yes, null, "verb-run", StoreProcess.Shell)), Home);

    private InstanceWorld Alone(StringWriter output, StringWriter error, string? session = null) => new()
    {
        Home = Home, Out = output, Error = error, Verb = "git", Environment = name => name == "DAORIS_SESSION_ID" ? session : null,
    };

    private async Task<string> TopicAsync()
    {
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "topic", "main");
        await CommitOnAsync(root, "topic", "t.txt", "t\n", "topic one");
        await CommitAsync(root, "m.txt", "m\n", "main one");
        return root;
    }

    [Theory]
    [InlineData("pick|repo|topic|abc123", "pick", "repo", "topic", "abc123", false)]
    [InlineData("revert|repo|topic|a..b|--yes", "revert", "repo", "topic", "a..b", true)]
    [InlineData("rebase|repo|topic|main|--plan", "rebase", "repo", "topic", "main", false)]
    public void The_words_are_read_into_the_ask(string words, string edit, string repository, string branch, string target, bool yes)
    {
        var verb = GitHistoryCommand.Read(words.Split('|'), out var problem);

        Assert.Null(problem);
        Assert.Equal(new GitHistoryVerb(edit, repository, branch, target, yes), verb);
    }

    [Theory]
    [InlineData("pick|repo|topic")]
    [InlineData("rebase|repo|topic|main|extra")]
    [InlineData("revert|repo|topic|c|--force")]
    [InlineData("pick|repo|topic|c|--plan|--yes")]
    public void Words_that_are_not_the_verbs_are_refused_with_the_usage(string words)
    {
        Assert.Null(GitHistoryCommand.Read(words.Split('|'), out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void Only_a_yes_changes_the_home_and_so_holds_its_lease()
    {
        Assert.True(GitHistoryCommand.Changes(["rebase", "repo", "topic", "main", "--yes"]));
        Assert.False(GitHistoryCommand.Changes(["pick", "repo", "topic", "c"]));
        Assert.True(VerbLeases.Holds(["git", "revert", "repo", "topic", "c", "--yes"]));
        Assert.False(VerbLeases.Holds(["git", "pick", "repo", "topic", "c", "--plan"]));
    }

    [Fact]
    public async Task A_plan_says_what_it_would_do_and_writes_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var tip = await RevAsync(root, "topic");
        var output = new StringWriter();

        var code = await GitHistoryCommand.RunAsync(["rebase", "engine", "topic", "main"], World(root), Alone(output, new StringWriter()), CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Contains("would rebase `topic`", output.ToString());
        Assert.Contains("topic one", output.ToString());
        Assert.Contains("git rebase main topic", output.ToString());
        Assert.Equal(tip, await RevAsync(root, "topic"));
        Assert.Empty(new ActJournal(Home).Unclosed());
    }

    [Fact]
    public async Task A_plan_the_edit_refuses_is_said_with_exit_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var error = new StringWriter();

        var code = await GitHistoryCommand.RunAsync(["rebase", "engine", "main", "topic"], World(root), Alone(new StringWriter(), error), CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Contains("line", error.ToString());
    }

    [Fact]
    public async Task With_no_daoris_running_a_yes_makes_the_edit_through_the_journal_with_its_progress()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var output = new StringWriter();

        var code = await GitHistoryCommand.RunAsync(["rebase", "engine", "topic", "main", "--yes"], World(root), Alone(output, new StringWriter()), CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Contains("made 1 of 1", output.ToString());
        Assert.Contains("rebased `topic`", output.ToString());
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, "topic^"));
        Assert.Empty(new ActJournal(Home).Unclosed());
    }

    [Fact]
    public async Task A_sessions_terminal_is_refused_and_nothing_moves()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var tip = await RevAsync(root, "topic");
        var error = new StringWriter();

        var code = await GitHistoryCommand.RunAsync(["rebase", "engine", "topic", "main", "--yes"], World(root), Alone(new StringWriter(), error, session: "s-1"), CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Contains("session", error.ToString());
        Assert.Equal(tip, await RevAsync(root, "topic"));
    }

    [Fact]
    public async Task With_a_daoris_running_a_yes_is_the_persons_press_carrying_the_plans_ids_and_nothing_runs_here()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await TopicAsync();
        var tip = await RevAsync(root, "topic");
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Respond = (route, _) =>
        {
            Assert.Equal("GIT_HISTORY_EDIT", route);
            return new { Said = new { Out = new[] { "rebased `topic` in engine (act a1)." }, Error = Array.Empty<string>(), Code = 0 } };
        };

        var code = await GitHistoryCommand.RunAsync(["rebase", "engine", "topic", "main", "--yes"], World(root), home.World("git"), CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Contains("rebased `topic`", home.Out.ToString());
        var press = Assert.Single(standIn.Presses).GetProperty("payload");
        Assert.Equal(("engine", "rebase", "topic", "main", true), (press.GetProperty("repository").GetString(), press.GetProperty("edit").GetString(),
            press.GetProperty("branch").GetString(), press.GetProperty("target").GetString(), press.GetProperty("said").GetBoolean()));
        Assert.Equal(tip, press.GetProperty("pins").GetProperty("tip").GetString());
        Assert.Equal(tip, await RevAsync(root, "topic"));
    }
}
