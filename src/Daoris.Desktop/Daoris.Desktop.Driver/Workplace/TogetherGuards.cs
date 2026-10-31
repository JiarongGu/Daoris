namespace Daoris.Driver;

/// <summary>
/// What a door that would land, discard or rebase a tree reads of the operations bringing sessions together (LINESTOGETHER1d, the
/// review-flow design §4): the binding, read under the record lock. A bound tree is refused, naming its operation, but to its own
/// combined Accept; a binding that cannot be read refuses too, since "cannot tell" is never "go on".
/// </summary>
internal static class TogetherGuard
{
    /// <summary>The words a bound tree is refused with; null where nothing binds it, or it binds it for <paramref name="landingFor"/>.</summary>
    public static string? Refusal(string home, string tree, string? landingFor = null)
    {
        try
        {
            return new TogetherOperations(home).Binding(tree) is { } bound && bound.Id != landingFor ? Bound(bound) : null;
        }
        catch (IOException error)
        {
            return Unread(error);
        }
    }

    /// <summary>The words a bound branch of <paramref name="repository"/> is left with; null where nothing binds it.</summary>
    public static string? BranchRefusal(string home, string repository, string branch)
    {
        try
        {
            return new TogetherOperations(home).BindingBranch(repository, branch) is { } bound ? Bound(bound) : null;
        }
        catch (IOException error)
        {
            return Unread(error);
        }
    }

    /// <summary>
    /// The words a member's own landing is refused with where a combined Accept brought its work in at the tip it stands at (or the one
    /// its door pinned); null where nothing did, or it moved on since.
    /// </summary>
    public static async Task<string?> BroughtAsync(string home, string tree, string? pinned, CancellationToken ct)
    {
        if (TogetherInclusion.OfTree(home, tree) is not { } brought) return null;
        var tip = pinned;
        if (tip is null)
        {
            var (code, head, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct)
                .ConfigureAwait(false);
            tip = code == 0 ? head.Trim() : null;
        }

        return string.Equals(tip, brought.Tip, StringComparison.OrdinalIgnoreCase) ? TogetherInclusion.Words(brought) : null;
    }

    internal static string Bound(TogetherOperation operation) =>
        $"it is being brought together with another session's work (operation {operation.Id}, {operation.Phase}), so it lands only by that "
        + "head's Accept and is left as it stands until then, or until they are let go";

    private static string Unread(IOException error) =>
        $"whether it is being brought together with another session's work could not be read ({error.Message}), so it was left as it stands";
}

/// <summary>
/// The door's lock order across processes (the review-flow design §4): (1) each bound tree's landing hold, ascending by its file; (2)
/// the repository's tree lock, shared as a start takes it; (3) the record lock, which each read and write of the operations takes under
/// these. Nothing holding (2) or (3) takes (1), and every wait is bounded.
/// </summary>
internal sealed class TogetherLocks : IDisposable
{
    private readonly IReadOnlyList<LandingHold> _holds;
    private readonly TreeLock _trees;

    private TogetherLocks(IReadOnlyList<LandingHold> holds, TreeLock trees) => (_holds, _trees) = (holds, trees);

    /// <summary>Take (1) for every tree, then (2); null with the words where one was held past <paramref name="within"/>.</summary>
    public static async Task<(TogetherLocks? Held, string? Refused)> TakeAsync(
        string home, string? workspace, string repository, IReadOnlyList<string> trees, TimeSpan within, CancellationToken ct)
    {
        var holds = new List<LandingHold>();
        try
        {
            foreach (var tree in trees.Select(Path.GetFullPath).DistinctBy(each => LandingHolds.PathOf(home, each))
                         .OrderBy(each => LandingHolds.PathOf(home, each), StringComparer.Ordinal))
            {
                if (await LandingHolds.TakeAsync(home, tree, ct, within).ConfigureAwait(false) is not { } hold)
                {
                    return (null, $"a landing of {tree} was still under way, so nothing was brought together: press again once it ends");
                }

                holds.Add(hold);
            }

            if (TreeLock.TryStarting(home, workspace, repository) is not { } shared) return (null, TreeLock.Replaying(repository));
            var held = new TogetherLocks([.. holds], shared);
            holds.Clear();
            return (held, null);
        }
        finally
        {
            foreach (var hold in holds) hold.Dispose();
        }
    }

    public void Dispose()
    {
        _trees.Dispose();
        foreach (var hold in _holds.Reverse()) hold.Dispose();
    }
}
