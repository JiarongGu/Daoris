using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Daoris's own release channel, what an install's update reads (RELEASEFEED1): the GitHub releases of Daoris's public repository, each
/// carrying the desktop package of the build (<see cref="Package"/>, a zip of one staged build) and the checksum file beside it
/// (<see cref="Sums"/>). A draft or a prerelease is no release of the channel: <see cref="Latest"/> names only a published one.
/// What the channel says is here; <see cref="DaorisReleaseDownloads"/> fetches and stages.
/// </summary>
public static partial class DaorisReleases
{
    /// <summary>What a program declares to pin from this channel.</summary>
    public const string Channel = "daoris-releases";

    /// <summary>Where the channel names its newest published release.</summary>
    public const string Latest = "https://api.github.com/repos/JiarongGu/Daoris/releases/latest";

    /// <summary>One release by its tag, which the tag's version completes.</summary>
    public const string ByTag = "https://api.github.com/repos/JiarongGu/Daoris/releases/tags";

    /// <summary>The build the package holds: the desktop is a Windows program, so the channel publishes one.</summary>
    public const string Target = "win-x64";

    /// <summary>The package's name: one staged build, zipped (the release workflow's <c>desktop-package</c> job writes it).</summary>
    public static string Package(string version) => $"Daoris-{version}-{Target}.zip";

    /// <summary>The checksum file beside the package: <c>sha256sum</c>'s line, the hash and the package's name.</summary>
    public static string Sums(string version) => Package(version) + ".sha256";

    /// <summary>The package's executable, relative to the unpacked build: the launcher, which every build carries.</summary>
    public const string Executable = "Daoris.exe";

    /// <summary>Refuse what is not one exact Daoris version, before anything is fetched: a tag and a pointer are not versions.</summary>
    public static void RefuseVersion(string version)
    {
        if (!Version().IsMatch(version))
        {
            throw new DriverException($"`{version}` is not a Daoris version — a release is named by one exact version, like 0.1.0, not its tag and not a pointer.");
        }
    }

    /// <summary>Whether <paramref name="version"/> is one a Daoris release is named by.</summary>
    public static bool IsVersion(string version) => Version().IsMatch(version);

    /// <summary>
    /// Whether <paramref name="version"/> is newer than <paramref name="running"/>, the build's own version, which may carry a
    /// commit (<c>0.1.0+abc1234</c>) or <c>+dev</c>. A running version that is not a version reads as older than any release.
    /// </summary>
    public static bool IsNewer(string version, string? running)
    {
        var release = Parts(version);
        var current = running is null ? null : Parts(running.Split('+', '-')[0]);
        if (release is null) return false;
        if (current is null) return true;
        for (var at = 0; at < 3; at++)
        {
            if (release[at] != current[at]) return release[at] > current[at];
        }

        return false;
    }

    /// <summary>The version a release's metadata is tagged with (<c>v&lt;version&gt;</c>), or what it said instead.</summary>
    public static string Tag(byte[] body)
    {
        var tag = TagOf(Metadata(body, "the newest release"));
        return tag is not null && tag.StartsWith('v') ? tag[1..] : tag ?? "";
    }

    /// <summary>A release's metadata as an object, or the refusal that it is not one.</summary>
    internal static JsonElement Metadata(byte[] body, string which)
    {
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            if (root.ValueKind == JsonValueKind.Object) return root;
        }
        catch (JsonException)
        {
            // Refused below.
        }

        throw new DriverException($"{which}'s release metadata is not JSON — nothing was installed.");
    }

    /// <summary>The metadata's own tag held to the version asked: metadata for another release is refused as that release.</summary>
    internal static void RefuseTag(JsonElement metadata, string version)
    {
        var tag = TagOf(metadata);
        if (tag != $"v{version}")
        {
            throw new DriverException(
                $"the release metadata answered for Daoris {version} is for `{tag ?? "undefined"}` — metadata for another release. Nothing was installed.");
        }
    }

    /// <summary>One named asset of a release: its HTTPS address and the SHA-256 the API itself states for it, where it states one.</summary>
    internal static (string Url, string? Sha256) Asset(JsonElement metadata, string name, string version)
    {
        var found = metadata.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array
            ? assets.EnumerateArray().FirstOrDefault(each => each.ValueKind == JsonValueKind.Object
                && each.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String && named.GetString() == name)
            : default;
        if (found.ValueKind != JsonValueKind.Object)
        {
            throw new DriverException($"Daoris {version}'s release publishes no {name}, so there is nothing to install.");
        }

        var url = found.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString()! : "";
        if (!url.StartsWith("https://", StringComparison.Ordinal))
        {
            throw new DriverException(
                $"Daoris {version}'s release names {name} at `{url}`, which is not HTTPS — refused before anything was fetched from it.");
        }

        var digest = found.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? Digest().Match(d.GetString()!) : null;
        return (url, digest is { Success: true } ? digest.Groups[1].Value.ToLowerInvariant() : null);
    }

    /// <summary>The SHA-256 a checksum file lists for <paramref name="package"/>, or null where it lists none.</summary>
    internal static string? Listed(byte[] sums, string package) =>
        Encoding.UTF8.GetString(sums).Split('\n')
            .Select(line => SumsLine().Match(line.Trim()))
            .FirstOrDefault(match => match.Success && match.Groups[2].Value == package)?.Groups[1].Value.ToLowerInvariant();

    private static string? TagOf(JsonElement metadata) =>
        metadata.TryGetProperty("tag_name", out var said) && said.ValueKind == JsonValueKind.String ? said.GetString() : null;

    private static int[]? Parts(string version)
    {
        var match = Version().Match(version);
        return match.Success ? [int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)] : null;
    }

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)$")]
    private static partial Regex Version();

    [GeneratedRegex("^sha256:([0-9a-f]{64})$", RegexOptions.IgnoreCase)]
    private static partial Regex Digest();

    [GeneratedRegex(@"^([0-9a-f]{64})\s+\*?(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SumsLine();
}
