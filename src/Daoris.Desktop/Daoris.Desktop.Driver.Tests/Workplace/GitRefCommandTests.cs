using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The terminal verb <c>git branch|tag &lt;repository&gt; &lt;name&gt; [&lt;commit&gt;] [--plan|--yes]</c>.
/// A plan says what it would make and writes nothing; <c>--yes</c> with a Daoris running is the person's press over the pipe, its words
/// the route's; with none it runs the journal's act itself, refused from a session's terminal.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitRefCommandTests : TreeSyncFixture
{
    private GitRefWorld World(string root) =>
        new((_, _) => Task.FromResult<string?>(root), () => new ActRuns(new ActJournal(Home, () => StoreSettled.Yes, null, "verb-run", StoreProcess.Shell)));

    private InstanceWorld Alone(StringWriter output, StringWriter error, string? session = null) => new()
    {
        Home = Home, Out = output, Error = error, Verb = "git", Environment = name => name == "DAORIS_SESSION_ID" ? session : null,
    };

    [Theory]
    [InlineData("branch|repo|name", "branch", "repo", "name", null, false)]
    [InlineData("tag|repo|v1|abc123|--yes", "tag", "repo", "v1", "abc123", true)]
    [InlineData("branch|repo|x|--plan", "branch", "repo", "x", null, false)]
    public void The_words_are_read_into_the_ask(string words, string kind, string repository, string name, string? at, bool yes)
    {
        var verb = GitRefCommand.Read(words.Split('|'), out var problem);

        Assert.Null(problem);
        Assert.Equal(new GitRefVerb(kind, repository, name, at, yes), verb);
    }

    [Theory]
    [InlineData("branch|repo")]
    [InlineData("branch|repo|n|c|extra")]
    [InlineData("tag|repo|n|--force")]
    [InlineData("tag|repo|n|--plan|--yes")]
    public void Words_that_are_not_the_verbs_are_refused_with_the_usage(string words)
    {
        Assert.Null(GitRefCommand.Read(words.Split('|'), out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void A_delete_is_read_from_its_word_and_holds_the_lease_only_with_a_yes()
    {
        Assert.Equal(new GitRefVerb("branch", "repo", "topic", "abc", true, true), GitRefCommand.Read(["branch", "repo", "topic", "abc", "--delete", "--yes"], out var problem));
        Assert.Null(problem);
        Assert.Equal(PersonPresses.GitRefDelete, GitRefCommand.RouteOf(new GitRefVerb("tag", "repo", "v1", null, true, true)));
        Assert.True(VerbLeases.Holds(["git", "branch", "repo", "n", "--delete", "--yes"]));
        Assert.False(VerbLeases.Holds(["git", "branch", "repo", "n", "--delete"]));
    }

    [Fact]
    public async Task A_delete_plan_says_what_it_would_delete_and_a_yes_deletes_it_through_the_journal_refusing_the_line()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "topic", "main");
        var world = World(root) with { LineOf = (_, _, _) => Task.FromResult<string?>("main") };
        var planned = new StringWriter();
        var refused = new StringWriter();
        var done = new StringWriter();

        var plan = await GitRefCommand.RunAsync(["branch", "engine", "topic", "--delete"], world, Alone(planned, new StringWriter()), CancellationToken.None);
        var line = await GitRefCommand.RunAsync(["branch", "engine", "main", "--delete", "--yes"], world, Alone(new StringWriter(), refused), CancellationToken.None);
        var yes = await GitRefCommand.RunAsync(["branch", "engine", "topic", "--delete", "--yes"], world, Alone(done, new StringWriter()), CancellationToken.None);

        Assert.Equal((0, 1, 0), (plan, line, yes));
        Assert.Contains("would delete the branch `topic`", planned.ToString());
        Assert.Contains("line", refused.ToString());
        Assert.Contains("deleted the branch `topic`", done.ToString());
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, "refs/daoris/discarded/topic"));
        Assert.Empty(new ActJournal(Home).Unclosed());
    }

    [Fact]
    public void A_rename_is_read_from_its_word_and_cannot_also_delete()
    {
        Assert.Equal(new GitRefVerb("branch", "repo", "topic", "abc", true, false, "work"), GitRefCommand.Read(["branch", "repo", "topic", "abc", "--rename", "work", "--yes"], out var problem));
        Assert.Null(problem);
        Assert.Equal(PersonPresses.GitRefRename, GitRefCommand.RouteOf(new GitRefVerb("tag", "repo", "v1", null, true, false, "v2")));
        Assert.Null(GitRefCommand.Read(["branch", "repo", "topic", "--rename"], out var missing));
        Assert.NotNull(missing);
        Assert.Null(GitRefCommand.Read(["branch", "repo", "topic", "--rename", "x", "--delete"], out var both));
        Assert.NotNull(both);
        Assert.True(VerbLeases.Holds(["git", "branch", "repo", "n", "--rename", "m", "--yes"]));
    }

    [Fact]
    public async Task A_rename_plan_says_what_it_would_move_and_a_yes_renames_it_through_the_journal_refusing_the_line()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        await GitAsync(root, "branch", "topic", "main");
        var world = World(root) with { LineOf = (_, _, _) => Task.FromResult<string?>("main") };
        var planned = new StringWriter();
        var refused = new StringWriter();
        var done = new StringWriter();

        var plan = await GitRefCommand.RunAsync(["branch", "engine", "topic", "--rename", "work"], world, Alone(planned, new StringWriter()), CancellationToken.None);
        var line = await GitRefCommand.RunAsync(["branch", "engine", "main", "--rename", "work", "--yes"], world, Alone(new StringWriter(), refused), CancellationToken.None);
        var yes = await GitRefCommand.RunAsync(["branch", "engine", "topic", "--rename", "work", "--yes"], world, Alone(done, new StringWriter()), CancellationToken.None);

        Assert.Equal((0, 1, 0), (plan, line, yes));
        Assert.Contains("would rename the branch `topic`", planned.ToString());
        Assert.Contains("new reflog", planned.ToString());
        Assert.Contains("line", refused.ToString());
        Assert.Contains("renamed the branch `topic` to `work`", done.ToString());
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, "work"));
        Assert.Empty(new ActJournal(Home).Unclosed());
    }

    [Fact]
    public void Only_a_yes_changes_the_home_and_so_holds_its_lease()
    {
        Assert.True(GitRefCommand.Changes(["branch", "repo", "n", "--yes"]));
        Assert.False(GitRefCommand.Changes(["branch", "repo", "n"]));
        Assert.False(GitRefCommand.Changes(["branches"]));
        Assert.True(VerbLeases.Holds(["git", "tag", "repo", "n", "--yes"]));
        Assert.False(VerbLeases.Holds(["git", "tag", "repo", "n", "--plan"]));
    }

    [Fact]
    public async Task A_plan_says_what_it_would_make_and_writes_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var output = new StringWriter();

        var code = await GitRefCommand.RunAsync(["branch", "engine", "topic"], World(root), Alone(output, new StringWriter()), CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Contains("would create the branch `topic`", output.ToString());
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "topic")).Trim());
        Assert.Empty(new ActJournal(Home).Unclosed());
    }

    [Fact]
    public async Task A_plan_of_a_name_that_stands_is_refused_with_exit_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var error = new StringWriter();

        var code = await GitRefCommand.RunAsync(["branch", "engine", "main"], World(root), Alone(new StringWriter(), error), CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Contains("already exists", error.ToString());
    }

    [Fact]
    public async Task With_no_daoris_running_a_yes_makes_the_ref_through_the_journal()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var output = new StringWriter();

        var code = await GitRefCommand.RunAsync(["tag", "engine", "v1", "--yes"], World(root), Alone(output, new StringWriter()), CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Contains("created the tag `v1`", output.ToString());
        Assert.Equal(await RevAsync(root, "main"), await RevAsync(root, "refs/tags/v1"));
        Assert.Empty(new ActJournal(Home).Unclosed());
    }

    [Fact]
    public async Task A_sessions_terminal_is_refused_and_nothing_is_made()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        var error = new StringWriter();

        var code = await GitRefCommand.RunAsync(["branch", "engine", "topic", "--yes"], World(root), Alone(new StringWriter(), error, session: "s-1"), CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Contains("session", error.ToString());
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "topic")).Trim());
    }

    [Fact]
    public async Task With_a_daoris_running_a_yes_is_the_persons_press_and_nothing_runs_here()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = await RepositoryAsync("engine");
        await using var home = new InstanceTestHome();
        var standIn = home.Serve();
        standIn.Confirming = true;
        standIn.Respond = (route, _) =>
        {
            Assert.Equal("GIT_REF_CREATE", route);
            return new { Said = new { Out = new[] { "created the branch `topic` in engine (act a1)." }, Error = Array.Empty<string>(), Code = 0 } };
        };

        var code = await GitRefCommand.RunAsync(["branch", "engine", "topic", "--yes"], World(root), home.World("git"), CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Contains("created the branch `topic`", home.Out.ToString());
        var press = Assert.Single(standIn.Presses).GetProperty("payload");
        Assert.Equal(("engine", "branch", "topic", true), (press.GetProperty("repository").GetString(), press.GetProperty("kind").GetString(), press.GetProperty("name").GetString(), press.GetProperty("said").GetBoolean()));
        Assert.Equal("", (await GitAsync(root, "branch", "--list", "topic")).Trim());
    }
}
