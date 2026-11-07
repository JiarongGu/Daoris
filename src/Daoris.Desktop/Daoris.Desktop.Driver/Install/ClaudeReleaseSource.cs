namespace Daoris.Driver;

/// <summary>
/// Claude Code's signed release bucket as Install's source: its manifest verified against the pinned key, then the binary by the
/// hash the manifest vouches for, both properties of this source (<see cref="ClaudeReleases"/>, <see cref="ClaudeReleaseDownloads"/>).
/// </summary>
public sealed class ClaudeReleaseSource : IReleaseSource
{
    public string Channel => ClaudeReleases.Channel;

    public void RefuseVersion(string version) => ClaudeReleases.RefuseVersion(version);

    public Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport) => ClaudeReleaseDownloads.LatestAsync(ct, transport);

    public Task<string> StageAsync(string staging, string version, Action<string> write, CancellationToken ct, ReleaseFetch fetch) =>
        ClaudeReleaseDownloads.StageAsync(staging, version, write, ct, fetch.Transport, fetch.Platform, fetch.Trust);
}
