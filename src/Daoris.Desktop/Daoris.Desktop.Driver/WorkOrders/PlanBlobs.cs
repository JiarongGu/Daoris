using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>
/// The blob each file a shown plan's release orders cite holds on the repository's line in its checkout here, keyed <c>repository:file</c>,
/// which the service compares with the lead's to name an order stale. What cannot be read here is left out, and so is not judged.
/// </summary>
public static class PlanBlobs
{
    /// <summary>The blob of every file the plans' orders cite that can be read here.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> SeenAsync(
        AskPlanResponse plan, IEnumerable<RepoView> registry, DriverConfig config, CancellationToken ct = default)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var cited = plan.Plans.SelectMany(each => each.Releases).Select(release => release.Evidence).OfType<AskPlanEvidenceResponse>()
            .Select(evidence => (evidence.Repository, evidence.File)).Distinct().ToList();
        foreach (var group in cited.GroupBy(each => each.Repository, StringComparer.OrdinalIgnoreCase))
        {
            if (registry.FirstOrDefault(row => string.Equals(row.Repository, group.Key, StringComparison.OrdinalIgnoreCase) && Directory.Exists(row.Root)) is not { Root: { } root } row) continue;

            var line = await CanonicalLine.ResolveAsync(root, row.Repository, row.Workspace, config, ct).ConfigureAwait(false);
            if (line.Branch is null) continue;

            foreach (var (repository, file) in group)
            {
                var (code, listed, _) = await WorkingTree.GitAsync(root, ["ls-tree", "--full-tree", "-z", line.Branch, "--", file], ct).ConfigureAwait(false);
                if (code == 0 && LineEntry.Parse(listed).FirstOrDefault(entry => entry.Path == file && entry.Type == "blob") is { } entry)
                {
                    seen[$"{repository}:{file}"] = entry.Object;
                }
            }
        }

        return seen;
    }
}
