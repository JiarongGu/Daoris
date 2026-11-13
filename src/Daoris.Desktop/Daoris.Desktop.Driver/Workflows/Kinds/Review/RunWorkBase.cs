namespace Daoris.Driver;

/// <summary>
/// The commit a run's review compares its branch from: where its chain's first branch grew from the line. Kept on the review record with the
/// tip, so a landing that puts the tip on the line leaves the comparison as the person read it.
/// </summary>
public static class RunWorkBase
{
    /// <summary>
    /// <paramref name="grown"/>'s base, read up the branches it grew from among <paramref name="chain"/>; null where a branch it grew from is
    /// not among them, as no commit of the chain's is then known to be the line's.
    /// </summary>
    public static string? Of(GrownBranch grown, IReadOnlyList<GrownBranch> chain)
    {
        var at = grown;
        for (var step = 0; at.GrewFrom is { } grewFrom; step++)
        {
            if (step >= chain.Count) return null;
            var before = chain.FirstOrDefault(each => string.Equals(each.Branch, grewFrom, StringComparison.Ordinal));
            if (before is null) return null;
            at = before;
        }

        return at.From;
    }
}
