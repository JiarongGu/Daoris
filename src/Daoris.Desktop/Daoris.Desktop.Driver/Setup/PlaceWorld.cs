namespace Daoris.Driver;

/// <summary>
/// The world a place's plan reads on this machine: the service's registry and quests, and each repository's line as git objects
/// in its checkout here, with its <c>origin</c>'s host. No fetch, no tool's version, no account and no plugin is asked.
/// </summary>
public sealed class PlaceWorld(ServiceClient service) : IPlaceWorld
{
    public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct) => service.RegistryAsync(ct);

    public Task<IReadOnlyList<QuestView>> QuestsAsync(CancellationToken ct) => service.EveryQuestAsync(ct);

    public async Task<PlaceTip> TipAsync(RepoView repository, DriverConfig config, CancellationToken ct)
    {
        if (repository.Root is not { Length: > 0 } root || !Directory.Exists(root))
        {
            return new PlaceTip(null, null, null, null, null, $"`{repository.Repository}` has no checkout on this machine to read.");
        }

        var host = GitWords.HostOf(await GitRemotes.AddressOf(root, "origin", ct: ct).ConfigureAwait(false));
        var line = await CanonicalLine.ResolveAsync(root, repository.Repository, repository.Workspace, config, ct).ConfigureAwait(false);
        if (line.Branch is not { } branch) return new PlaceTip(null, null, null, null, host, $"`{repository.Repository}`'s line is not known here.");
        var (tip, problem) = await LandedGates.ReadAsync(root, branch, ct).ConfigureAwait(false);
        if (tip is null) return new PlaceTip(branch, null, null, null, host, $"`{repository.Repository}`: {problem}.");
        return new PlaceTip(tip.Line, tip.Commit, tip.Gates, await LandedGates.TextAsync(root, tip.Gates, ct).ConfigureAwait(false), host);
    }
}
