namespace Daoris.Driver;

/// <summary>
/// Claude Code's verified install from its release channel, under the names its callers use (STRUCT3e, as D166's STRUCT3a
/// note cuts a mixed type): <c>ClaudeReleases.InstallAsync</c>, <c>LatestAsync</c> and <c>Current</c>, which Accounts' door
/// update and the tests call, over <see cref="ClaudeReleaseDownloads"/>. Another module reaches them here, in the
/// Application, never in the Infrastructure; the channel's constants and refusals are <see cref="ClaudeReleases"/>'s.
/// </summary>
public static class ClaudeReleaseInstalls
{
    extension(ClaudeReleases)
    {
        /// <summary>This machine's build (<see cref="ClaudeReleaseDownloads.Current"/>).</summary>
        public static string Current() => ClaudeReleaseDownloads.Current();

        /// <summary>The exact version the bucket names as its newest release (<see cref="ClaudeReleaseDownloads.LatestAsync"/>).</summary>
        public static Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport = null) =>
            ClaudeReleaseDownloads.LatestAsync(ct, transport);

        /// <summary>Install one version into <paramref name="where"/>, verified end to end (<see cref="ClaudeReleaseDownloads.InstallAsync"/>).</summary>
        /// <returns>The executable, inside <paramref name="where"/>.</returns>
        public static Task<string> InstallAsync(
            string where, string version, Action<string> write, CancellationToken ct,
            HttpMessageHandler? transport = null, string? platform = null, (string Key, string Fingerprint)? trust = null) =>
            ClaudeReleaseDownloads.InstallAsync(where, version, write, ct, transport, platform, trust);
    }
}
