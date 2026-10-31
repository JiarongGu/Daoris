using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GITAPP2f's terminal verb (D204; the git-writes design §9): <c>git fetch &lt;repository&gt; [--plan|--yes]</c>. A plan says what it would fetch and
/// writes nothing; <c>--yes</c> with no Daoris running fetches itself through the journal's act, refused from a session's terminal.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitFetchCommandTests : TreeSyncFixture
{
    private GitRefWorld World(string root) =>
        new((_, _) => Task.FromResult<string?>(root), () => new ActRuns(new ActJournal(Home, () => StoreSettled.Yes, null, "verb-run", StoreProcess.Shell)));

    private InstanceWorld Alone(StringWriter output, StringWriter error, string? session = null) => new()
    {
        Home = Home, Out = output, Error = error, Verb = "git", Environment = name => name == "DAORIS_SESSION_ID" ? session : null,
    };

    private void LayDoors()
    {
        var bin = Path.Combine(Home, "app", "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, GitWiring.SshDoor), "");
        File.WriteAllText(Path.Combine(bin, GitWiring.RemoteHelper), "");
    }

    [Theory]
    [InlineData("fetch|repo", "repo", false)]
    [InlineData("fetch|repo|--plan", "repo", false)]
    [InlineData("fetch|--yes|repo", "repo", true)]
    public void The_words_are_read_into_the_ask(string words, string repository, bool yes)
    {
        Assert.Equal(new GitFetchVerb(repository, yes), GitFetchCommand.Read(words.Split('|'), out var problem));
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("fetch")]
    [InlineData("fetch|repo|other")]
    [InlineData("fetch|repo|--prune")]
    [InlineData("fetch|repo|--plan|--yes")]
    public void Words_that_are_not_the_verbs_are_refused_with_the_usage(string words)
    {
        Assert.Null(GitFetchCommand.Read(words.Split('|'), out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void A_fetch_holds_the_lease_only_with_a_yes_and_is_a_person_s_press()
    {
        Assert.True(VerbLeases.Holds(["git", "fetch", "repo", "--yes"]));
        Assert.False(VerbLeases.Holds(["git", "fetch", "repo", "--plan"]));
        Assert.Contains(PersonPresses.Presses, press => press.Route == PersonPresses.GitFetch && press.Verb == "git fetch <repository> --yes");
    }

    [Fact]
    public async Task A_plan_names_the_command_and_changes_nothing_and_a_yes_fetches_refusing_a_session_s_terminal()
    {
        if (!OperatingSystem.IsWindows()) return;
        LayDoors();
        var (root, origin) = await RepositoryWithOriginAsync("engine");
        var ahead = await CommitOnOriginAsync(origin, "ahead", ("a.txt", "a"));
        var before = await RevAsync(root, "refs/remotes/origin/main");

        var planned = new StringWriter();
        var planError = new StringWriter();
        var planCode = await GitFetchCommand.RunAsync(["fetch", "engine", "--plan"], World(root), Alone(planned, planError), CancellationToken.None);
        Assert.Equal((0, before), (planCode, await RevAsync(root, "refs/remotes/origin/main")));
        Assert.Contains("--refmap=", planned.ToString());
        Assert.Contains("refs/remotes/origin/*", planned.ToString());

        var session = new StringWriter();
        var sessionError = new StringWriter();
        var refused = await GitFetchCommand.RunAsync(["fetch", "engine", "--yes"], World(root), Alone(session, sessionError, session: "s-1"), CancellationToken.None);
        Assert.Equal((1, before), (refused, await RevAsync(root, "refs/remotes/origin/main")));

        var done = new StringWriter();
        var doneError = new StringWriter();
        var code = await GitFetchCommand.RunAsync(["fetch", "engine", "--yes"], World(root), Alone(done, doneError), CancellationToken.None);
        Assert.True(code == 0, doneError.ToString());
        Assert.Equal(ahead, await RevAsync(root, "refs/remotes/origin/main"));
        Assert.Contains("fetched origin's branches", done.ToString());
    }

    [Fact]
    public async Task Where_the_doors_are_not_installed_the_plan_and_the_press_are_refused_with_exit_one()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var output = new StringWriter();
        var error = new StringWriter();

        var plan = await GitFetchCommand.RunAsync(["fetch", "engine", "--plan"], World(root), Alone(output, error), CancellationToken.None);

        Assert.Equal(1, plan);
        Assert.Contains("doors", error.ToString());
    }
}
