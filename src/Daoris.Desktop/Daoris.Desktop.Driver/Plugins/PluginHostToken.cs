namespace Daoris.Driver;

/// <summary>What a plugin's start is told of the API token it may carry: the token, or the words that refuse the start.</summary>
public sealed record HostTokenHand(string? Token, string? Refusal);

/// <summary>
/// The API token of the identity serving a place's origin, handed to one plugin process (D241 point 5): <c>DAORIS_HOST_TOKEN</c>, set by
/// <see cref="HookProcess"/> around that one start and never in any other process's environment. The plugin starts only where an
/// identity serves origin, that identity is not read only on its host, the person's git access does not deny Write, and the identity
/// holds an API token. The value is never said in a refusal.
/// </summary>
public static class PluginHostToken
{
    /// <summary>The variable the token travels under.</summary>
    public const string Variable = "DAORIS_HOST_TOKEN";

    /// <summary>The identity serving <paramref name="origin"/> for a repository and its workspace, judged and read.</summary>
    public static HostTokenHand Hand(string home, string? repository, string? workspace, string? origin)
    {
        var host = GitWords.HostOf(origin);
        if (origin is null || host is null) return Refuse("the place has no origin to serve");
        var way = origin.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? GitIdentity.Https : GitIdentity.Ssh;
        var serves = GitIdentityUse.Resolve(
            GitIdentities.Read(home), GitIdentityUse.Read(home), repository, workspace, ["origin"], GitHosts.Canonical(host), way);
        if (serves.Kind == GitServeKind.Own) return Refuse($"{host} is served by your own sign-ins, which carry git alone: set an API token on an identity for it");
        if (serves.Identity is not { } identity) return Refuse($"no git identity serves {host}");

        var value = GitAccessKeys.Resolve(home, repository, workspace).Of(GitAccessKeys.Write).Value;
        var verdict = GitAccessRules.Judge(GitAct.Write, value, granted: true, identity.ReadOnly ? identity.Label : null);
        if (!verdict.Allowed)
        {
            return Refuse(verdict.Code == "read-only"
                ? $"{identity.Label} is read only on {host}"
                : "this repository's git access denies writes to origin");
        }

        return GitTokens.Read(home, identity.Id, GitTokens.Api) is { Length: > 0 } token
            ? new HostTokenHand(token, null)
            : Refuse($"{identity.Label} has no API token set for {host}");
    }

    private static HostTokenHand Refuse(string why) => new(null, $"{why}, so the plugin was not started");
}
