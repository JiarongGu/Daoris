namespace Daoris.Knowledge;

/// <param name="Fetched">How many of the team's maps came down, new or moved.</param>
/// <param name="Forgotten">How many held here the remote no longer holds.</param>
/// <param name="Problem">The wall, when the remote could not be reached; null otherwise.</param>
public sealed record CodeMapSyncReport(int Fetched, int Forgotten, string? Problem);

/// <summary>
/// One pass of the team's code maps for one workspace (MAP3e): each repository of the circle that
/// this machine holds only a teammate's copy of — no checkout here — has its map brought down as the
/// remote holds it, so this machine answers for it instead of saying it keeps none.
/// </summary>
/// <remarks>
/// <para><b>The remote's holding is the order.</b> A shared deployment already took each map by
/// ancestry and kept the first reading of a commit (SYNC5a), so this machine holds exactly what the
/// remote holds, at the commit it holds it: a newer commit replaces the map here, a commit that keeps
/// none is held with none, and a map the remote no longer holds is forgotten here.</para>
///
/// <para><b>A checkout here is the authority</b> on its own map and is read from disk, so a repository
/// with a root is never asked for; and a copy another circle keeps is that circle's pass's.</para>
///
/// <para><b>It runs in the host</b>, beside the quests and the records (D69): pulling a map needs no
/// git, and the host is what answers the page. The commit is asked first, so a map that has not moved
/// is not sent again.</para>
/// </remarks>
public static class CodeMapSync
{
    public static async Task<CodeMapSyncReport> RunAsync(
        KnowledgeService service, IRemote remote, string workspace, CancellationToken ct = default)
    {
        var circle = Workspaces.Normalize(workspace);
        var team = (await service.RegistryAsync(ct: ct).ConfigureAwait(false))
            .Where(r => string.IsNullOrWhiteSpace(r.Root) && Workspaces.Same(r.InWorkspace, circle))
            .Select(r => r.Repository)
            .ToList();

        var fetched = 0;
        var forgotten = 0;
        try
        {
            foreach (var repository in team)
            {
                var held = await remote.HeldCodeMapAsync(repository, ct).ConfigureAwait(false);
                var here = await service.FedCodeMapCommitAsync(repository, ct).ConfigureAwait(false);
                if (held is null)
                {
                    if (here is not null && await service.ForgetFedCodeMapAsync(repository, ct).ConfigureAwait(false)) forgotten++;
                    continue;
                }

                if (string.Equals(held, here, StringComparison.OrdinalIgnoreCase)) continue;

                // Null when the remote's answer is no fed map this machine can check — moved away between
                // the two questions, or broken on the way. The next pass asks again.
                if (await remote.FetchCodeMapAsync(repository, ct).ConfigureAwait(false) is not { } map) continue;

                await service.HoldFedCodeMapAsync(repository, map, ct).ConfigureAwait(false);
                fetched++;
            }

            return new(fetched, forgotten, null);
        }
        catch (RemoteException wall)
        {
            return new(fetched, forgotten, wall.Message);
        }
    }
}
