using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDREBASE1 (D87's LANDREBASE1 note): an automatic landing at done has nobody to press the review's <i>Ask it to rebase</i>, so where
/// replaying the work onto the line conflicts, Daoris says the rebase words to the session itself, through the say door, once for the
/// tip that conflicted. Its own conversation goes on with them in its own tree, so the session that knows the work resolves it, and the
/// look lands the work once its tree has changed.
/// </summary>
/// <remarks>Real git, a stand-in for the service's quests, records and say door: nothing reaches a network or a harness.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AutoLandingRebaseTests : AutoLandingFixture
{
    [Fact]
    public async Task A_done_whose_replay_conflicts_tells_its_session_to_rebase_once_and_lands_once_it_has()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await CommitAsync(tree, "README.md", "# engine\nfrom the session", "the session's readme");
        await CommitAsync(root, "README.md", "# engine\nfrom the line", "the line's readme");
        var tip = await HeadAsync(tree);

        var lines = await LookAsync();

        Assert.False(await BranchAsync(root, PushBranch), string.Join("\n", lines));
        var said = Assert.Single(_world.Said);
        Assert.Equal(("s1", $"rebase/s1/{tip}"), (said.Session, said.Key));
        Assert.StartsWith("Rebase your work onto `main`: the line moved since your work began, and landing it conflicts", said.Text);
        Assert.Contains("README.md", said.Text);
        var note = Notes("s1").Single(each => each.Parts?[0].Code == NoteCodes.LandingRebase.Code);
        Assert.StartsWith("not accepted automatically: replaying its work onto the line conflicts, so its session was asked to rebase", note.Text);
        Assert.Contains(lines, line => line.Contains("its session was asked to rebase", StringComparison.Ordinal));
        Assert.Equal(AutoLandingCode.Refused, new AutoLandings(_home).Of("s1")!.Tries[^1].Code);

        // Its tree as it was, the next look tries nothing and says nothing more to it.
        await LookAsync();
        Assert.Single(_world.Said);

        // The session rebases in its own tree as it was told, resolves and commits; the next look lands its work on the line's tip.
        await GitAsync(tree, "-c", "core.editor=:", "rebase", "main");
        await File.WriteAllTextAsync(Path.Combine(tree, "README.md"), "# engine\nfrom the line\nfrom the session\n");
        await GitAsync(tree, "add", "README.md");
        await GitAsync(tree, "-c", "core.editor=:", "rebase", "--continue");
        var landed = await LookAsync();

        Assert.True(await BranchAsync(root, PushBranch), string.Join("\n", landed));
        Assert.Equal(await HeadAsync(root), (await GitAsync(root, "rev-parse", $"{PushBranch}~2")).Trim());
        Assert.Single(_world.Said);
    }

    /// <summary>A refusal for any other reason says nothing to the session: only a conflict is its to resolve.</summary>
    [Fact]
    public async Task A_refusal_that_is_no_conflict_says_nothing_to_the_session()
    {
        var root = await RepositoryAsync("engine");
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", AutoAccept: true));
        await GitAsync(root, "branch", PushBranch);
        await DoneSessionAsync(root, "s1", "q1");

        await LookAsync();

        Assert.Empty(_world.Said);
        Assert.DoesNotContain(Notes("s1"), each => each.Parts?[0].Code == NoteCodes.LandingRebase.Code);
    }
}
