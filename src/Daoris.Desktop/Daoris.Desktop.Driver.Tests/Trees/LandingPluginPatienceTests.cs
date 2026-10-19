using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WSR4 (D100): a plugin that never answers is a timeout, and the branch stands. A class of its own (PROC1f): its three seconds
/// of patience are a wall-clock bound a neighbour's load could break, so it runs alone, after the rest of the Process half
/// (<see cref="Alone"/>, PROCPAR1), where <see cref="LandingPluginTests"/> run four at once with the git-only classes.
/// </summary>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class LandingPluginPatienceTests : LandingPluginFixture
{
    /// <summary>A real process that never answers is a timeout, and the branch stands.</summary>
    [Fact]
    public async Task A_plugin_that_never_answers_times_out_and_the_branch_stands()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        InstallRealPlugin(silent: true);
        Rule(new LandingRule(LandingForm.Branch, "feature/{quest}-{slug}", Plugin: Id));
        var trees = new SessionTrees(Home, new LandingPlugins(Home, patience: TimeSpan.FromSeconds(3)));
        var opened = await trees.OpenAsync(root, "engine", "aurora");
        await CommitAsync(opened.Path, "work.txt", "the session's work", "the work");

        var landed = await trees.LandAsync(opened.Path, new LandingSubject("s1a2b3c4", "0fda18", "Fix"));

        Assert.True(landed.Landed, landed.Message);
        Assert.True(landed.Plugin!.Failed);
        Assert.Contains("did not answer `hook/work/land`", landed.Message);
        Assert.NotEmpty((await GitAsync(root, "branch", "--list", "feature/0fda18-fix")).Trim());
    }
}
