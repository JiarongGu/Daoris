namespace Daoris.Driver;

public static class Repositories
{
    /// <summary>
    /// What the registry holds, as one string — equal when the same repositories stand in the same
    /// circles at the same roots with the same lanes, whatever the order. The shell forwards a tick to the page when this
    /// changes, as it does for <see cref="Asks.Signature"/>: a folder imported from a terminal moves nothing else a tick reports.
    /// </summary>
    public static string Signature(IEnumerable<RepoView> registry) =>
        string.Join("\n", registry
            .Select(repo => $"{repo.Repository}\t{repo.Adopted}\t{repo.Workspace}\t{repo.Root}\t{string.Join(',', repo.Lanes.Order(StringComparer.Ordinal))}")
            .OrderBy(line => line, StringComparer.Ordinal));
}
