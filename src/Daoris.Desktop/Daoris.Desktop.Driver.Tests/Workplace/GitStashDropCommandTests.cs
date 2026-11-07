using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The terminal verb of the Changes tab's stash Drop: <c>git stash drop &lt;repository&gt; &lt;commit&gt; [--plan|--yes]</c>.
/// A plan says what it would drop and the command that stores it again, and writes nothing; <c>--yes</c> with no Daoris running drops through the
/// journal's act, refused from a session's terminal.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitStashDropCommandTests : TreeSyncFixture
{
    private ActJournal Journal() => new(Home, () => StoreSettled.Yes, null, "verb-run", StoreProcess.Shell);

    private GitStashWorld World(string root) =>
        new((_, _) => Task.FromResult<string?>(root), Journal, (repository, checkout) => new StageContext(repository, checkout, () => true, _ => Task.FromResult<IReadOnlyList<TreeHolder>>([])));

    private InstanceWorld Alone(StringWriter output, StringWriter error, string? session = null) => new()
    {
        Home = Home, Out = output, Error = error, Verb = "git", Environment = name => name == "DAORIS_SESSION_ID" ? session : null,
    };

    [Theory]
    [InlineData("stash|drop|repo|abc", "repo", "abc", false)]
    [InlineData("stash|drop|repo|abc|--plan", "repo", "abc", false)]
    [InlineData("stash|drop|--yes|repo|abc", "repo", "abc", true)]
    public void The_words_are_read_into_the_ask(string words, string repository, string id, bool yes)
    {
        Assert.Equal(new GitStashDropVerb(repository, id, yes), GitStashDropCommand.Read(words.Split('|'), out var problem));
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("stash")]
    [InlineData("stash|pop|repo|abc")]
    [InlineData("stash|drop|repo")]
    [InlineData("stash|drop|repo|abc|extra")]
    [InlineData("stash|drop|repo|abc|--all")]
    [InlineData("stash|drop|repo|abc|--plan|--yes")]
    public void Words_that_are_not_the_verbs_are_refused_with_the_usage(string words)
    {
        Assert.Null(GitStashDropCommand.Read(words.Split('|'), out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void A_drop_holds_the_lease_only_with_a_yes_and_is_a_person_s_press()
    {
        Assert.True(VerbLeases.Holds(["git", "stash", "drop", "repo", "abc", "--yes"]));
        Assert.False(VerbLeases.Holds(["git", "stash", "drop", "repo", "abc", "--plan"]));
        Assert.Contains(PersonPresses.Presses, press => press.Route == PersonPresses.GitStashDrop && press.Verb == "git stash drop <repository> <commit> --yes");
    }

    [Fact]
    public async Task A_plan_names_the_command_that_stores_it_again_and_changes_nothing_and_a_yes_drops_refusing_a_session_s_terminal()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "two\n");
        var stashes = new StashActs(Journal());
        var shown = (await stashes.PlanAsync(root, CancellationToken.None)).Value!;
        var saved = await stashes.RunAsync(new StageContext("engine", root, () => true, _ => Task.FromResult<IReadOnlyList<TreeHolder>>([])),
            new StashPress(false, "work", shown.Head!, shown.Stash), CancellationToken.None);
        Assert.False(saved.Refused, saved.Said);
        var id = (await GitAsync(root, "rev-parse", "refs/stash")).Trim();

        var planned = new StringWriter();
        var planError = new StringWriter();
        var planCode = await GitStashDropCommand.RunAsync(["stash", "drop", "engine", id, "--plan"], World(root), Alone(planned, planError), CancellationToken.None);
        Assert.Equal((0, id), (planCode, (await GitAsync(root, "rev-parse", "refs/stash")).Trim()));
        Assert.Contains("git stash store -m", planned.ToString());
        Assert.Contains(id, planned.ToString());

        var session = new StringWriter();
        var sessionError = new StringWriter();
        var refused = await GitStashDropCommand.RunAsync(["stash", "drop", "engine", id, "--yes"], World(root), Alone(session, sessionError, session: "s-1"), CancellationToken.None);
        Assert.Equal((1, id), (refused, (await GitAsync(root, "rev-parse", "refs/stash")).Trim()));

        var done = new StringWriter();
        var doneError = new StringWriter();
        var code = await GitStashDropCommand.RunAsync(["stash", "drop", "engine", id, "--yes"], World(root), Alone(done, doneError), CancellationToken.None);
        Assert.True(code == 0, doneError.ToString());
        Assert.Contains("dropped the stash", done.ToString());
        Assert.Equal("", (await GitAsync(root, "stash", "list")).Trim());
    }

    [Fact]
    public async Task A_stash_that_is_not_on_the_list_refuses_the_plan_with_exit_one()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "a.txt", "one\n", "first");
        var head = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var output = new StringWriter();
        var error = new StringWriter();

        var plan = await GitStashDropCommand.RunAsync(["stash", "drop", "engine", head, "--plan"], World(root), Alone(output, error), CancellationToken.None);

        Assert.Equal(1, plan);
        Assert.Contains("not in the stash list", error.ToString());
    }
}
