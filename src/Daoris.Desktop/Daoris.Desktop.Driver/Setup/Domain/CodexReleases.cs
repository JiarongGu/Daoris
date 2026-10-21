using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Codex's own release channel (AGT2b), what a pin of <c>codex</c> installs from (PUSH1f2e, ported from the CLI's <c>channels.ts</c>):
/// the release's package tarball, by the digest its metadata publishes AND the line its SUMS file publishes, the SUMS file itself
/// checked against the metadata first, then unpacked whole, since its executable finds its helpers through the package's layout.
/// <c>docs/2026-09-24-agt2b-channel-evidence.md</c> is where every address, shape and name below was checked. What it checks and says
/// is here; <see cref="CodexReleaseDownloads"/> fetches and stages.
/// </summary>
public static partial class CodexReleases
{
    /// <summary>What a toolchain declares to pin from this channel, the CLI's name for it too.</summary>
    public const string Channel = "codex-releases";

    public const string Base = "https://releases.openai.com/codex/releases";

    /// <summary>The documented fallback when a release's own metadata does not answer.</summary>
    public const string GitHub = "https://api.github.com/repos/openai/codex/releases/tags";

    /// <summary>Where the channel names its newest release (USE1a): the newest release's own metadata.</summary>
    public const string Latest = "https://releases.openai.com/codex/channels/latest";

    /// <summary>The file beside each release that lists every package's SHA-256.</summary>
    public const string Sums = "codex-package_SHA256SUMS";

    /// <summary>The package target for a machine, in the release's own names. Linux builds are musl only.</summary>
    public static string Target(string os, string arch)
    {
        var system = os switch { "win32" => "pc-windows-msvc", "darwin" => "apple-darwin", "linux" => "unknown-linux-musl", _ => null };
        var cpu = arch switch { "x64" => "x86_64", "arm64" => "aarch64", _ => null };
        if (system is null || cpu is null)
        {
            throw new DriverException(
                $"Codex publishes no package for {os}-{arch}, so there is nothing to pin here. Install it with its own tooling if it runs "
                + "on this machine; unpinned, Daoris runs whatever is on PATH.");
        }

        return $"{cpu}-{system}";
    }

    /// <summary>The package's name for a target.</summary>
    public static string Package(string target) => $"codex-package-{target}.tar.gz";

    /// <summary>Refuse what is not one exact Codex release, before anything is fetched: a tag and a pointer are not versions.</summary>
    public static void RefuseVersion(string version)
    {
        if (!Version().IsMatch(version))
        {
            throw new DriverException(
                $"`{version}` is not a Codex version — a pin names one exact release, like 0.156.1, not its tag and not a pointer.");
        }
    }

    /// <summary>Whether <paramref name="version"/> is one Codex names a release by.</summary>
    public static bool IsVersion(string version) => Version().IsMatch(version);

    /// <summary>The version a release's metadata is tagged with (<c>rust-v&lt;version&gt;</c>), or what it said instead.</summary>
    public static string Tag(byte[] body)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(body).RootElement;
        }
        catch (JsonException)
        {
            return Encoding.UTF8.GetString(body).Trim();
        }

        var tag = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tag_name", out var said) && said.ValueKind == JsonValueKind.String
            ? said.GetString()!
            : "";
        return tag.StartsWith("rust-v", StringComparison.Ordinal) ? tag["rust-v".Length..] : tag;
    }

    /// <summary>A release's metadata as an object, or the refusal that it is not one.</summary>
    internal static JsonElement Metadata(byte[] body, string version)
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

        throw new DriverException($"Codex {version}'s release metadata is not JSON — nothing was installed.");
    }

    /// <summary>The metadata's own tag held to the version asked: metadata for another release is refused as that release.</summary>
    internal static void RefuseTag(JsonElement metadata, string version)
    {
        var tag = metadata.TryGetProperty("tag_name", out var said) && said.ValueKind == JsonValueKind.String ? said.GetString() : null;
        if (tag != $"rust-v{version}")
        {
            throw new DriverException(
                $"the release metadata answered for Codex {version} is for `{tag ?? "undefined"}` — metadata for another release. Nothing was "
                + "installed.");
        }
    }

    /// <summary>One named asset of a release: its HTTPS address and its published SHA-256.</summary>
    internal static (string Url, string Sha256) Asset(JsonElement metadata, string name, string version)
    {
        var found = metadata.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array
            ? assets.EnumerateArray().FirstOrDefault(each => each.ValueKind == JsonValueKind.Object
                && each.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String && named.GetString() == name)
            : default;
        if (found.ValueKind != JsonValueKind.Object)
        {
            throw new DriverException($"Codex {version}'s release publishes no {name}, so there is nothing to install.");
        }

        var digest = found.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? Digest().Match(d.GetString()!) : null;
        if (digest is not { Success: true })
        {
            throw new DriverException(
                $"Codex {version}'s release publishes {name} with no SHA-256, and a download with no hash to hold it to is not installed.");
        }

        var url = found.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString()! : "";
        if (!url.StartsWith("https://", StringComparison.Ordinal))
        {
            throw new DriverException(
                $"Codex {version}'s release names {name} at `{url}`, which is not HTTPS — refused before anything was fetched from it.");
        }

        return (url, digest.Groups[1].Value.ToLowerInvariant());
    }

    /// <summary>The SHA-256 a SUMS file lists for <paramref name="package"/>, or null where it lists none.</summary>
    internal static string? Listed(byte[] sums, string package) =>
        Encoding.UTF8.GetString(sums).Split('\n')
            .Select(line => SumsLine().Match(line.Trim()))
            .FirstOrDefault(match => match.Success && match.Groups[2].Value == package)?.Groups[1].Value.ToLowerInvariant();

    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:-(?:alpha|beta)\.\d+)?$")]
    private static partial Regex Version();

    [GeneratedRegex("^sha256:([0-9a-f]{64})$", RegexOptions.IgnoreCase)]
    private static partial Regex Digest();

    [GeneratedRegex(@"^([0-9a-f]{64})\s+\*?(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SumsLine();
}
