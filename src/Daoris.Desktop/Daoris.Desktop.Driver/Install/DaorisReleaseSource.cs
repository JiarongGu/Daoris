namespace Daoris.Driver;

/// <summary>
/// Daoris's own releases as Install's source: the published GitHub releases of its public repository, each one's desktop package held
/// to the checksum file beside it (<see cref="DaorisReleases"/>, <see cref="DaorisReleaseDownloads"/>). A fetch's trust is the
/// checksum, not a signing key, which this channel does not read.
/// </summary>
public sealed class DaorisReleaseSource : IReleaseSource
{
    public string Channel => DaorisReleases.Channel;

    public void RefuseVersion(string version) => DaorisReleases.RefuseVersion(version);

    public Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport) => DaorisReleaseDownloads.LatestAsync(ct, transport);

    public Task<string> StageAsync(string staging, string version, Action<string> write, CancellationToken ct, ReleaseFetch fetch) =>
        DaorisReleaseDownloads.StageAsync(staging, version, write, ct, fetch.Transport, fetch.Platform);
}
