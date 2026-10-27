using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDREBASE2 (D87's LANDREBASE2 note): <c>daoris-driver trees land &lt;session&gt;</c> refused a replay that conflicted and printed
/// it, but the session was handed nothing, so the work waited for someone to resolve it without its context. The window offers its
/// <i>Ask it to rebase</i>; a terminal has no second press, so its landing says the rebase words to the session itself through the say
/// door, as Daoris's, keyed by the session and its tree's tip, and the session rebases in its own tree and lands.
/// </summary>
/// <remarks>Real git in a scratch repository, a stand-in for the service's say door: nothing reaches a network or a harness.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class TerminalLandRebaseTests : LandedFixture
{
    private static readonly LandingSubject Subject = new("s1a2b3c4", "0fda18", "Fix the gap");

    private const string Branch = "feature/0fda18-fix-the-gap";

    private readonly List<(string Session, string Text, string Key)> _said = [];

    private Task<SayAnswer?> SayAsync(string session, string text, string key, CancellationToken ct)
    {
        _said.Add((session, text, key));
        return Task.FromResult<SayAnswer?>(new SayAnswer(true, "kept"));
    }

    [Fact]
    public async Task A_terminal_landing_that_conflicts_tells_its_session_to_rebase_and_lands_once_it_has()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "shared.txt", "one\nfrom the tree\n", "the work");
        await CommitAsync(root, "shared.txt", "one\nfrom the line\n", "the line's own");
        var tip = await HeadAsync(tree.Path);

        var refused = await trees.LandAsync(tree.Path, Subject);
        Assert.False(refused.Landed, refused.Message);
        var told = await RebaseTelling.TellTreeAsync(SayAsync, Subject.Session, tree.Path, refused.Conflict!);

        var said = Assert.Single(_said);
        Assert.Equal((Subject.Session, $"rebase/{Subject.Session}/{tip}"), (said.Session, said.Key));
        Assert.Equal(refused.Conflict!.Words, said.Text);
        Assert.Contains("shared.txt", said.Text);
        Assert.Equal($"Its session `{Subject.Session}` was asked to rebase in its own tree, in the words the review offers; its conversation goes on "
            + $"with them. Run `trees land {Subject.Session}` again once its tree has changed.", told);

        // The session goes on with the words in its own tree: onto the line, resolved, committed; the terminal's next landing lands it.
        await GitAsync(tree.Path, "-c", "core.editor=:", "rebase", "main");
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "shared.txt"), "one\nfrom the line\nfrom the tree\n");
        await GitAsync(tree.Path, "add", "shared.txt");
        await GitAsync(tree.Path, "-c", "core.editor=:", "rebase", "--continue");

        var landed = await trees.LandAsync(tree.Path, Subject);

        Assert.True(landed.Landed, landed.Message);
        Assert.Null(landed.Conflict);
        Assert.Equal(await HeadAsync(root), (await GitAsync(root, "rev-parse", $"{Branch}~1")).Trim());
        Assert.Single(_said);
    }

    /// <summary>A door that keeps nothing: the terminal says why and gives the person the words to pass on.</summary>
    [Fact]
    public async Task Words_the_door_refuses_are_given_to_the_person_with_why()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(tree.Path, "shared.txt", "one\nfrom the tree\n", "the work");
        await CommitAsync(root, "shared.txt", "one\nfrom the line\n", "the line's own");
        var refused = await trees.LandAsync(tree.Path, Subject);

        var told = await RebaseTelling.TellTreeAsync(
            (_, _, _, _) => Task.FromResult<SayAnswer?>(new SayAnswer(false, "the session is running.")), Subject.Session, tree.Path, refused.Conflict!);
        var thrown = await RebaseTelling.TellTreeAsync(
            (_, _, _, _) => throw new HttpRequestException("no service answered"), Subject.Session, tree.Path, refused.Conflict!);

        Assert.Equal($"Its session `{Subject.Session}` could not be asked to rebase (the session is running). Say this to it: {refused.Conflict!.Words}", told);
        Assert.StartsWith($"Its session `{Subject.Session}` could not be asked to rebase (no service answered). Say this to it: Rebase your work onto `main`", thrown);
    }

    private static async Task<string> HeadAsync(string place) => (await GitAsync(place, "rev-parse", "HEAD")).Trim();
}
