using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.GitFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// KEEPSIZE1d over real git (D195's keep-size note): the set-aside folders over their size limit lose the oldest recorded one first, and
/// never a folder whose commits no ref holds nor one whose record is not kept. The limit is counted in a few kilobytes here
/// (<see cref="SpaceLimits.PassAsync"/>'s <c>unit</c>); the downloads' and the choices' proofs are <see cref="SpaceLimitsTests"/>'.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class SpaceLimitsGitTests : KeepSizeFixture
{
    private const long Unit = 15_000;

    private async Task<string> RecordedAsideAsync(SessionTrees trees, string root, string line)
    {
        var tree = await TreeWithWorkAsync(trees, root);
        await File.WriteAllTextAsync(Path.Combine(tree.Path, "probe.db"), new string('x', 10_000));
        await GitAsync(root, "checkout", "--quiet", "-b", line);
        await SquashAsync(root, tree.Branch);
        await GitAsync(root, "checkout", "--quiet", "main");
        await GitAsync(root, "merge", "--no-ff", "--no-edit", line);
        var aside = await SetAsideAsync(trees, root);
        await RecordAsync(trees, root, DateTimeOffset.UtcNow);
        return aside;
    }

    private (SpaceFreeWorld World, SpaceSizes Sizes) WorldOf(string root, int gb)
    {
        var sizes = new SpaceSizes();
        sizes.MeasureNow(_home, "set-aside", WiringBudget.Default, CancellationToken.None);
        var world = new SpaceFreeWorld(_home)
        {
            Sizes = sizes,
            Policy = _ => KeepPolicy.Default with { Limits = new LimitsPolicy(gb, null) },
            Registry = _ => Task.FromResult<IReadOnlyList<RepoView>>([new RepoView("engine", true, root)]),
            PostRecord = PostAsync,
        };
        return (world, sizes);
    }

    /// <summary>🔴 Over its limit the set-aside kind loses its oldest recorded folder, and no more than brings it within.</summary>
    [Fact]
    public async Task Set_aside_folders_over_their_limit_lose_the_oldest_recorded_first()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var older = await RecordedAsideAsync(trees, root, "feature/one");
        var newer = await RecordedAsideAsync(trees, root, "feature/two");

        var (world, _) = WorldOf(root, 1);
        var pass = await SpaceLimits.PassAsync(world, KeepingRun.Look, "run-1", CancellationToken.None, Unit);

        var kind = Assert.Single(pass.Kinds);
        Assert.Equal(("set-aside", 1), (kind.Id, kind.Removed));
        Assert.False(Directory.Exists(older));
        Assert.True(Directory.Exists(newer));
    }

    /// <summary>
    /// 🔴 A tree whose commits no ref holds is never let go by a size: its ref dropped by hand, the folder stays though the kind is over its
    /// limit, and so does a folder with no record at all.
    /// </summary>
    [Fact]
    public async Task A_tree_no_ref_holds_and_a_folder_with_no_record_are_never_let_go_by_a_size()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(_home);
        var aside = await RecordedAsideAsync(trees, root, "feature/one");
        await GitAsync(root, "update-ref", "-d", Manifest(aside).Finished!.Ref!);
        var moved = DateTimeOffset.UtcNow.AddHours(-2);
        var stray = Path.Combine(trees.SetAsideRoot, "default", "engine",
            $"devtools-{moved.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", System.Globalization.CultureInfo.InvariantCulture)}");
        Directory.CreateDirectory(stray);
        await File.WriteAllTextAsync(Path.Combine(stray, "a.txt"), new string('y', 10_000));

        var (world, _) = WorldOf(root, 1);
        var pass = await SpaceLimits.PassAsync(world, KeepingRun.Look, "run-1", CancellationToken.None, 1_000);

        // The recorded folder was asked for, and its holding check, run right before it would go, kept it.
        var kind = Assert.Single(pass.Kinds);
        Assert.Equal(0, kind.Removed);
        Assert.NotEmpty(kind.Kept);
        Assert.True(Directory.Exists(aside));
        Assert.True(Directory.Exists(stray));
    }
}
