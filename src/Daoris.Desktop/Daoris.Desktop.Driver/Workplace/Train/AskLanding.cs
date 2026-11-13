namespace Daoris.Driver;

/// <summary>
/// The performer of an ask delivery run's <c>land</c>: the ask's branch, at the tip the person accepted, merged into the repository's line by
/// the integration the session landings merge through. No session or tree stands behind the branch, so the checkout's own line is read and the
/// branch is requested by its name; what the try came to is kept for the Accept that waits on it (<see cref="LandOutcomes"/>).
/// </summary>
public sealed class AskLanding(string home)
{
    /// <summary>Merges the run's branch where it still stands at the start's tip; the line said for the try, or null where nothing here lands.</summary>
    public async Task<string?> PerformAsync(LandStart start, CancellationToken ct = default)
    {
        if (WorkflowRunBindings.Read(home, start.Run) is not { } binding || !AskRunKey.Is(binding)) return null;
        var root = GitFolders.Read(home).FirstOrDefault(checkout => string.Equals(checkout.Repository, binding.Repository, StringComparison.OrdinalIgnoreCase)).Root;
        if (root is null) return Kept(start, LandingCode.Refused, "its repository has no checkout here to land into.");
        var config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        if ((await CanonicalLine.ResolveAsync(root, binding.Repository, binding.Workspace, config, ct).ConfigureAwait(false)).Branch is not { } line)
        {
            return Kept(start, LandingCode.Refused, "its repository's line is not set, so there is nothing to merge into.");
        }

        var name = TrainBranch.Of(binding.Ask!);
        var (code, stands, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{name}^{{commit}}"], ct).ConfigureAwait(false);
        if (code != 0 || !string.Equals(stands.Trim(), start.Tip, StringComparison.OrdinalIgnoreCase))
        {
            return Kept(start, LandingCode.Moved, $"`{name}` moved since it was judged; press Accept again.");
        }

        var (merged, refused, refusal) = await IntegrationPresses.MergeAsync(
            home, config, root, binding.Repository, binding.Workspace, line, name, start.Tip, ct, delivered: true, automatic: !start.Reviewed).ConfigureAwait(false);
        if (!merged) return Kept(start, refusal ?? LandingCode.Refused, refused!);
        RegistryFollowing.Moved(home, binding.Repository, DateTimeOffset.UtcNow);
        return Kept(start, LandingCode.Landed, $"merged `{name}` into `{line}`.", landed: true);
    }

    private static string Kept(LandStart start, string code, string said, bool landed = false)
    {
        LandOutcomes.Kept(start.Unit, start.Tip, LandEnds.Of(code, new TreeLanding(landed, said) { Refusal = landed ? null : code }, said));
        return $"landing  ask delivery {start.Run}: {said}";
    }
}
