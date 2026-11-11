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

    /// <summary>
    /// The line's declaration of rooms and the path's standing on it, as git objects: the layout's facts (<c>daoris.json</c>'s <c>rooms</c>, the folders
    /// holding an <c>AGENTS.md</c>), the lanes file's rooms (<c>LaneRooms</c>), and each part of the path listed at the tip, so a link, a submodule's commit
    /// or a file on the way is found. The working files and the index are never asked.
    /// </summary>
    public async Task<PlaceRoomRead> RoomAsync(RepoView repository, DriverConfig config, string path, CancellationToken ct)
    {
        if (repository.Root is not { Length: > 0 } root || !Directory.Exists(root))
        {
            return PlaceRoomRead.Unread($"`{repository.Repository}` has no checkout on this machine to read.");
        }

        var line = await CanonicalLine.ResolveAsync(root, repository.Repository, repository.Workspace, config, ct).ConfigureAwait(false);
        if (line.Branch is not { } branch) return PlaceRoomRead.Unread($"`{repository.Repository}`'s line is not known here.");
        var layout = await LayoutGit.ReadAsync(root, branch, ct).ConfigureAwait(false);
        if (layout.Facts is not { } facts) return PlaceRoomRead.Unread($"`{repository.Repository}`: {layout.Problem}.");
        var files = await LineDeclarationReader.ReadAsync(root, branch, ct).ConfigureAwait(false);
        if (files.Problem is { } problem) return PlaceRoomRead.Unread($"`{repository.Repository}`: {problem}.");

        string? through = null;
        var isFile = false;
        var parts = path.Split('/');
        for (var depth = 1; depth <= parts.Length && through is null && !isFile; depth++)
        {
            var prefix = string.Join('/', parts.Take(depth));
            var (code, listed, _) = await WorkingTree.GitAsync(root, ["ls-tree", "-z", facts.Commit, "--", prefix], ct).ConfigureAwait(false);
            if (code != 0 || LineEntry.Parse(listed).FirstOrDefault(each => each.Path == prefix) is not { } entry) break;
            if (entry.Type == "tree") continue;
            if (entry.IsFile && depth == parts.Length) isFile = true;
            else through = prefix;
        }

        return new PlaceRoomRead(
            facts.Line, facts.Commit, facts.Adopted, facts.DeclaredRooms, [.. LaneRooms.Read(files.Lanes).Rooms.Select(room => room.Id)], facts.Rooms, through, isFile);
    }
}
