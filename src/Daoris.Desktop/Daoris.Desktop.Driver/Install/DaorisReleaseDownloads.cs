using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Daoris.Driver;

/// <summary>
/// Daoris's release channel over HTTP (<see cref="DaorisReleases"/>): the newest published release, one release's metadata, the
/// checksum file beside its package, and the package both vouch for, streamed, hashed and unpacked whole into a staging. What it
/// checks against is <see cref="DaorisReleases"/>'s; <see cref="ReleaseChannels"/> stages and <see cref="UpdateFetch"/> puts the
/// build where an install's update reads it.
/// </summary>
/// <remarks>
/// 🔴 <b>The package is unpacked as published</b>: the bytes that verified are the bytes unpacked. The unpacked build is then held to its own
/// manifest by <see cref="StagedBuild.Check"/>, the check every staged build meets before anything is replaced.
/// </remarks>
public static class DaorisReleaseDownloads
{
    /// <summary>This machine's package target, or the sentence saying the channel publishes none for it.</summary>
    public static string Current()
    {
        var target = OperatingSystem.IsWindows() && RuntimeInformation.OSArchitecture == Architecture.X64 ? DaorisReleases.Target : null;
        return target ?? throw new DriverException(
            $"Daoris publishes its desktop for {DaorisReleases.Target} only, and this machine is {RuntimeInformation.OSDescription} "
            + $"{RuntimeInformation.OSArchitecture}, so there is nothing to fetch for it.");
    }

    /// <summary>
    /// The exact version the channel names as its newest published release. 🔴 The answer only chooses a version; it vouches for
    /// nothing, so it is read as one exact version or refused, and the version is then fetched verified as a typed one would be.
    /// </summary>
    public static async Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport = null)
    {
        using var http = Downloads.Client(transport);
        var body = await BytesAsync(http, DaorisReleases.Latest, ct).ConfigureAwait(false)
            ?? throw new DriverException(
                $"Daoris has published no release at {DaorisReleases.Latest} — nothing was fetched.");
        var said = DaorisReleases.Tag(body);
        if (!DaorisReleases.IsVersion(said))
        {
            throw new DriverException(
                $"the newest release at {DaorisReleases.Latest} is tagged `{TextBounds.Clip(said, 60)}`, which is not a version — nothing was fetched.");
        }

        return said;
    }

    /// <summary>
    /// One release fetched, verified and unpacked into <paramref name="staging"/>'s <c>package</c> folder: the metadata, the checksum file,
    /// the package held to both where the API states a hash. Every refusal throws before anything is unpacked.
    /// </summary>
    /// <param name="target">The package target: this machine's, unless a test names another.</param>
    /// <returns>The executable, relative to the package folder.</returns>
    internal static async Task<string> StageAsync(
        string staging, string version, Action<string> write, CancellationToken ct, HttpMessageHandler? transport = null,
        string? target = null)
    {
        DaorisReleases.RefuseVersion(version);
        if ((target ?? Current()) != DaorisReleases.Target)
        {
            throw new DriverException($"Daoris publishes its desktop for {DaorisReleases.Target} only, not {target}, so nothing was fetched.");
        }

        var packageName = DaorisReleases.Package(version);
        var sumsName = DaorisReleases.Sums(version);
        using var http = Downloads.Client(transport);

        write($"fetching Daoris {version}'s release metadata");
        var url = $"{DaorisReleases.ByTag}/v{version}";
        var body = await BytesAsync(http, url, ct).ConfigureAwait(false)
            ?? throw new DriverException($"no published release of Daoris {version} was found at {url}.");
        var metadata = DaorisReleases.Metadata(body, $"Daoris {version}");
        DaorisReleases.RefuseTag(metadata, version);

        var package = DaorisReleases.Asset(metadata, packageName, version);
        var sumsAsset = DaorisReleases.Asset(metadata, sumsName, version);

        var sums = await BytesAsync(http, sumsAsset.Url, ct).ConfigureAwait(false)
            ?? throw new DriverException($"nothing answered at {sumsAsset.Url}, which Daoris {version}'s metadata names.");
        var listed = DaorisReleases.Listed(sums, packageName)
            ?? throw new DriverException(
                $"Daoris {version}'s {sumsName} lists no {packageName}, so there is no hash to hold the package to. Nothing was downloaded.");
        if (package.Sha256 is not null && package.Sha256 != listed)
        {
            throw new DriverException(
                $"Daoris {version}'s two published hashes for {packageName} disagree — the release says {package.Sha256}, the {sumsName} file "
                + $"says {listed}. Nothing was downloaded.");
        }

        write($"the release publishes {packageName} as {listed}");

        write($"downloading {package.Url}");
        var archive = Path.Combine(staging, packageName);
        var saved = await Downloads.SaveAsync(http, package.Url, archive, ct, Refuse).ConfigureAwait(false)
            ?? throw new DriverException($"nothing answered at {package.Url}, which Daoris {version}'s metadata names.");
        if (saved.Sha256 != listed)
        {
            throw new DriverException(
                $"the package downloaded from {package.Url} is not the one Daoris {version} publishes — its SHA-256 is {saved.Sha256}, and "
                + $"the release says {listed}. It was deleted, and nothing was installed.");
        }

        write($"the package's SHA-256 matches: {saved.Sha256}");

        try
        {
            ToolArchives.Unpack(archive, "zip", Path.Combine(staging, "package"), DaorisReleases.Executable);
        }
        catch (ToolRefusal refusal) when (refusal.Check == "exe")
        {
            throw new DriverException(
                $"Daoris {version}'s package holds no {DaorisReleases.Executable} — its layout is not a build's, so nothing was installed.");
        }
        catch (ToolRefusal refusal)
        {
            // The archive's own check: the hash vouched for the bytes, not for every name inside them.
            throw new DriverException($"Daoris {version}'s package was not unpacked: {refusal.Message}. Nothing was installed.");
        }

        File.Delete(archive);
        return DaorisReleases.Executable;
    }

    private static async Task<byte[]?> BytesAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var response = await Downloads.GetAsync(http, url, ct, Refuse).ConfigureAwait(false);
        return response is null ? null : await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    private static Exception Refuse(string said) => new DriverException(said);
}
