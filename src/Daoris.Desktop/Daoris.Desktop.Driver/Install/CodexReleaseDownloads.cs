using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// Codex's release channel over HTTP (AGT2b; PUSH1f2e, ported from the CLI's <c>channels.ts</c>): the newest-release pointer, the
/// release's metadata (its own, else the GitHub release's), the SUMS file it vouches for, and the package both vouch for, streamed,
/// hashed and unpacked whole into a staging. What it checks against is <see cref="CodexReleases"/>'s; <see cref="ReleaseChannels"/>
/// stages and publishes.
/// </summary>
/// <remarks>
/// 🔴 <b>The package is installed and run as published</b>: the bytes that verified are the bytes unpacked, and nothing is patched,
/// wrapped or repacked. Setting the executable bit is the only change, and it is the file system's.
/// </remarks>
public static class CodexReleaseDownloads
{
    /// <summary>This machine's package target.</summary>
    public static string Current()
    {
        var os = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsLinux() ? "linux" : RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var other => other.ToString().ToLowerInvariant(),
        };
        return CodexReleases.Target(os, arch);
    }

    /// <summary>
    /// The exact version the channel names as its newest release (USE1a). 🔴 The pointer only chooses a version; it vouches for nothing,
    /// so it is read as one exact version or refused, and the version is then installed verified as a typed one would be.
    /// </summary>
    public static async Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport = null)
    {
        using var http = Downloads.Client(transport);
        var body = await BytesAsync(http, CodexReleases.Latest, ct).ConfigureAwait(false)
            ?? throw new DriverException(
                $"nothing answered at {CodexReleases.Latest}, where the channel names its newest release — nothing was fetched or pinned.");
        var said = CodexReleases.Tag(body);
        if (!CodexReleases.IsVersion(said))
        {
            throw new DriverException(
                $"the channel's newest-release pointer at {CodexReleases.Latest} answered `{TextBounds.Clip(said, 60)}`, which is not a "
                + "version — nothing was fetched or pinned.");
        }

        return said;
    }

    /// <summary>
    /// One release fetched, verified and unpacked into <paramref name="staging"/>'s <c>package</c> folder: the metadata, the SUMS file
    /// held to it, the package held to both. Every refusal throws before anything is unpacked.
    /// </summary>
    /// <param name="target">The package target: this machine's, unless a test names another.</param>
    /// <returns>The executable, relative to the package folder.</returns>
    internal static async Task<string> StageAsync(
        string staging, string version, Action<string> write, CancellationToken ct, HttpMessageHandler? transport = null,
        string? target = null)
    {
        CodexReleases.RefuseVersion(version);
        target ??= Current();
        var windows = target.EndsWith("windows-msvc", StringComparison.Ordinal);
        var packageName = CodexReleases.Package(target);
        using var http = Downloads.Client(transport);

        write($"fetching Codex {version}'s release metadata");
        var metadata = await MetadataAsync(http, version, ct).ConfigureAwait(false);
        CodexReleases.RefuseTag(metadata, version);

        var package = CodexReleases.Asset(metadata, packageName, version);
        var sumsAsset = CodexReleases.Asset(metadata, CodexReleases.Sums, version);

        var sums = await BytesAsync(http, sumsAsset.Url, ct).ConfigureAwait(false)
            ?? throw new DriverException($"nothing answered at {sumsAsset.Url}, which Codex {version}'s metadata names.");
        var sumsHash = Convert.ToHexStringLower(SHA256.HashData(sums));
        if (sumsHash != sumsAsset.Sha256)
        {
            throw new DriverException(
                $"Codex {version}'s {CodexReleases.Sums} is not the file its release metadata publishes — its SHA-256 is {sumsHash}, and "
                + $"the metadata says {sumsAsset.Sha256}. Nothing was installed.");
        }

        var listed = CodexReleases.Listed(sums, packageName)
            ?? throw new DriverException(
                $"Codex {version}'s {CodexReleases.Sums} lists no {packageName}, so there is no second hash to hold it to.");
        if (listed != package.Sha256)
        {
            throw new DriverException(
                $"Codex {version}'s two published hashes for {packageName} disagree — the metadata says {package.Sha256}, the "
                + $"{CodexReleases.Sums} file says {listed}. Nothing was downloaded.");
        }
        write($"the release publishes {packageName} as {package.Sha256}, in its metadata and its SUMS file both");

        write($"downloading {package.Url}");
        var archive = Path.Combine(staging, packageName);
        var saved = await Downloads.SaveAsync(http, package.Url, archive, ct, Refuse).ConfigureAwait(false)
            ?? throw new DriverException($"nothing answered at {package.Url}, which Codex {version}'s metadata names.");
        if (saved.Sha256 != package.Sha256)
        {
            throw new DriverException(
                $"the package downloaded from {package.Url} is not the one Codex {version} publishes — its SHA-256 is {saved.Sha256}, and "
                + $"the release says {package.Sha256}. It was deleted, and nothing was installed.");
        }
        write($"the package's SHA-256 matches: {saved.Sha256}");

        var executable = windows ? "bin/codex.exe" : "bin/codex";
        try
        {
            ToolArchives.Unpack(archive, "tar.gz", Path.Combine(staging, "package"), executable);
        }
        catch (ToolRefusal refusal) when (refusal.Check == "exe")
        {
            throw new DriverException(
                $"Codex {version}'s package holds no {executable} — its layout is not the one Daoris was written against, so nothing was "
                + "installed.");
        }
        catch (ToolRefusal refusal)
        {
            // The archive's own check: the hash vouched for the bytes, not for every name inside them.
            throw new DriverException($"Codex {version}'s package was not unpacked: {refusal.Message}. Nothing was installed.");
        }

        File.Delete(archive);
        return executable;
    }

    /// <summary>The release's own metadata, or the GitHub release when that does not answer. Only an answer stops the fallback.</summary>
    private static async Task<System.Text.Json.JsonElement> MetadataAsync(HttpClient http, string version, CancellationToken ct)
    {
        var own = $"{CodexReleases.Base}/{version}/release.json";
        var github = $"{CodexReleases.GitHub}/rust-v{version}";

        byte[]? body = null;
        var why = "nothing answered there";
        try
        {
            body = await BytesAsync(http, own, ct).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            why = error.Message;
        }

        // Metadata that answered and then fails a check is refused, never traded for a second opinion.
        body ??= await BytesAsync(http, github, ct).ConfigureAwait(false)
            ?? throw new DriverException(
                $"no release of Codex {version} was found — not at {own} ({why}), and not at {github}. A pin names a version the release "
                + "page lists.");
        return CodexReleases.Metadata(body, version);
    }

    private static async Task<byte[]?> BytesAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var response = await Downloads.GetAsync(http, url, ct, Refuse).ConfigureAwait(false);
        return response is null ? null : await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    private static Exception Refuse(string said) => new DriverException(said);
}
