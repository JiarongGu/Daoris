namespace Daoris.Driver;

/// <summary>What a place's work process is told at its start: nothing where it may start, else the words that refuse it.</summary>
public sealed record PlaceHand(string? Refusal);

/// <summary>
/// Whether a plugin's work process may start for a place: only where the place has an origin, the git identity serving it (where one
/// does) is not read only on its host, and the person's git access does not deny Write. The plugin carries its own credential, from
/// its own storage (<see cref="PluginPrivate"/>); no identity's token is handed to it.
/// </summary>
public static class PlaceHands
{
    /// <summary>The place's origin judged, for a repository and its workspace.</summary>
    public static PlaceHand Judge(string home, string? repository, string? workspace, string? origin)
    {
        var host = GitWords.HostOf(origin);
        if (origin is null || host is null) return Refuse("the place has no origin to serve");
        var way = origin.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? GitIdentity.Https : GitIdentity.Ssh;
        var serves = GitIdentityUse.Resolve(
            GitIdentities.Read(home), GitIdentityUse.Read(home), repository, workspace, ["origin"], GitHosts.Canonical(host), way);
        var value = GitAccessKeys.Resolve(home, repository, workspace).Of(GitAccessKeys.Write).Value;
        var readOnly = serves.Identity is { ReadOnly: true } identity ? identity.Label : null;
        var verdict = GitAccessRules.Judge(GitAct.Write, value, granted: true, readOnly);
        if (verdict.Allowed) return new(null);
        return Refuse(verdict.Code == "read-only" ? $"{readOnly} is read only on {host}" : "this repository's git access denies writes to origin");
    }

    /// <summary>What <see cref="HookSet"/> asks at each place's start: the place's origin, its push address, read from its checkout.</summary>
    public static Func<PluginEntry, WorkPlace, CancellationToken, Task<PlaceHand>> For(string home) =>
        async (_, place, ct) => Judge(
            home, place.Repository, place.Workspace, await GitRemotes.AddressOf(place.Root, "origin", push: true, ct).ConfigureAwait(false));

    private static PlaceHand Refuse(string why) => new($"{why}, so the plugin was not started");
}
