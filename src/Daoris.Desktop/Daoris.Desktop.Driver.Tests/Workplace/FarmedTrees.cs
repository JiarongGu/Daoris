using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The session trees a landed-branch test will land one after another, made up front by <see cref="TreeFarm"/> (one opened for real, the
/// rest its copies) where it would open each in turn at about ten git starts apiece. <see cref="LandedFixture.LandAsync"/> takes the
/// next one that is ready and opens its own when none is.
/// </summary>
public sealed class FarmedTrees
{
    private readonly Queue<string> _ready = new();

    /// <summary><paramref name="count"/> trees of <c>engine</c> in <c>aurora</c> made ready, each clean on a branch of its own at the line's tip.</summary>
    public async Task StockAsync(SessionTrees trees, string home, string root, int count)
    {
        foreach (var tree in await TreeFarm.OpenAsync(trees, home, root, "engine", "aurora", count).ConfigureAwait(false)) _ready.Enqueue(tree);
    }

    /// <summary>The next tree that is ready, or false when none is.</summary>
    public bool TryTake(out string tree) => _ready.TryDequeue(out tree!);
}
