namespace Daoris.Driver;

/// <summary>
/// A maker's release channel this build installs a pin from (AGENTPROVIDER1 row f; the agent-provider design §11): Install owns each,
/// a provider's program names one by its channel, and <see cref="ReleaseChannels"/> stages and publishes whatever it fetched, so adding a
/// channel is a source of its own and no switch on ids.
/// </summary>
public interface IReleaseSource
{
    /// <summary>The channel's name, what a program declares to pin from it.</summary>
    string Channel { get; }

    /// <summary>Refuse a version the channel cannot install verifiably, before anything is fetched.</summary>
    /// <exception cref="DriverException">It is not one exact release the channel installs.</exception>
    void RefuseVersion(string version);

    /// <summary>The exact version the channel names as its newest release (USE1a), what an update pins.</summary>
    Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport);

    /// <summary>The release fetched and verified into <paramref name="staging"/>'s <c>package</c> folder; the executable, relative to it.</summary>
    Task<string> StageAsync(string staging, string version, Action<string> write, CancellationToken ct, ReleaseFetch fetch);
}

/// <summary>How a release is fetched: the transport, the machine's build in the channel's own names, and who signs its manifests.</summary>
/// <param name="Transport">The HTTP transport: the network's, unless a test holds its own.</param>
/// <param name="Platform">The build: this machine's, unless a test names another.</param>
/// <param name="Trust">The signing key and its fingerprint, for a channel that signs: the vendor's pinned one, unless a test holds its own.</param>
public sealed record ReleaseFetch(HttpMessageHandler? Transport, string? Platform, (string Key, string Fingerprint)? Trust);
