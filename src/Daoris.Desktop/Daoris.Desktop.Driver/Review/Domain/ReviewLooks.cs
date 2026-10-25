namespace Daoris.Driver;

/// <summary>
/// A review rule's environment put, as a standing choice looks for its procedure (PUSH1f2a, D194 point 4; REVIEWENV1a; FOUNDATION1
/// mod-iii): the review's judgement over the checkouts the registry names, which the settings' <see cref="StandingChoices"/> held until
/// the settings' own types named none of the review's. The window's route and <c>daoris-driver driver review</c> acting alone call this one.
/// </summary>
public static class ReviewLooks
{
    /// <summary>
    /// A review rule's environment put, its procedure looked for in each checkout <paramref name="registry"/> names that the edit
    /// reaches (REVIEWENV1a): a repository's checkout here that lacks it refuses the change; a workspace's names each one that lacks it.
    /// </summary>
    /// <exception cref="DriverException">The repository's checkout here does not hold the procedure; nothing is written.</exception>
    public static ReviewLooked Of(IReadOnlyList<(string Repository, string? Workspace, string? Root)> registry, ReviewEdit edit, string procedure)
    {
        var lacking = ReviewRules.Lacking(registry, edit, procedure);
        if (edit.Repository is { } repository)
        {
            if (lacking.Count > 0) throw new DriverException(ReviewRules.NotHeld(repository.Trim(), procedure));
            return new(lacking, !registry.Any(row => row.Root is { Length: > 0 }
                && string.Equals(row.Repository, repository.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        return new(lacking, !registry.Any(row => row.Root is { Length: > 0 }
            && string.Equals(WorkspaceNames.Of(row.Workspace), WorkspaceNames.Of(edit.Workspace), StringComparison.OrdinalIgnoreCase)));
    }
}
