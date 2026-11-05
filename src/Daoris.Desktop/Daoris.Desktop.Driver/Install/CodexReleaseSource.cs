namespace Daoris.Driver;

/// <summary>
/// Codex's releases as Install's source (PUSH1f2e): the release for this machine's target (<see cref="CodexReleases"/>,
/// <see cref="CodexReleaseDownloads"/>). A fetch's trust is Claude Code's signing key, which this channel does not read.
/// </summary>
public sealed class CodexReleaseSource : IReleaseSource
{
    public string Channel => CodexReleases.Channel;

    public void RefuseVersion(string version) => CodexReleases.RefuseVersion(version);

    public Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport) => CodexReleaseDownloads.LatestAsync(ct, transport);

    public Task<string> StageAsync(string staging, string version, Action<string> write, CancellationToken ct, ReleaseFetch fetch) =>
        CodexReleaseDownloads.StageAsync(staging, version, write, ct, fetch.Transport, fetch.Platform);
}
