using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's release bucket over HTTP (AGT2b), and the verified install it writes to the disk (STRUCT3e): the
/// newest-release pointer, the manifest and its signature, the binary streamed and hashed, and this machine's build. What
/// it checks against is <see cref="ClaudeReleases"/>'s and <see cref="OpenPgp"/>'s; other modules call it as
/// <c>ClaudeReleases.InstallAsync</c>, <c>LatestAsync</c> and <c>Current</c>, through the Application
/// (<see cref="ClaudeReleaseInstalls"/>). The twin of the CLI's <c>channels.ts</c>.
/// </summary>
/// <remarks>
/// 🔴 <b>The binary is installed and run as published</b> (the vendor's terms): the bytes that verified
/// are the bytes that land. Setting the executable bit is the only change, and it is the file
/// system's, not the binary's.
/// </remarks>
public static class ClaudeReleaseDownloads
{
    /// <summary>This machine's build.</summary>
    public static string Current()
    {
        var os = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsLinux() ? "linux" : RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var other => other.ToString().ToLowerInvariant(),
        };
        return ClaudeReleases.Platform(os, arch, musl: RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The exact version the bucket names as its newest release (USE1a) — what Update pins.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>The pointer only chooses a version; it vouches for nothing.</b> It is not signed, so it is
    /// read as one exact version or refused, and the version it names is then installed by
    /// <see cref="InstallAsync"/>, verified exactly as a typed one. A pin never names the pointer
    /// itself: a pin meaning "whatever is newest today" would change under a running arrangement.
    /// </remarks>
    public static async Task<string> LatestAsync(CancellationToken ct, HttpMessageHandler? transport = null)
    {
        using var http = transport is null ? new HttpClient() : new HttpClient(transport, disposeHandler: false);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("daoris");

        var body = await BytesAsync(http, ClaudeReleases.Latest, ct).ConfigureAwait(false)
            ?? throw new DriverException(
                $"nothing answered at {ClaudeReleases.Latest}, where the release channel names its newest Claude Code — nothing was "
                + "fetched or pinned.");
        var said = Encoding.UTF8.GetString(body).Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(said, @"^\d+\.\d+\.\d+$"))
        {
            var shown = said.Length > 60 ? said[..60] + "…" : said;
            throw new DriverException(
                $"the release channel's newest-release pointer at {ClaudeReleases.Latest} answered `{shown}`, which is not a version — "
                + "nothing was fetched or pinned.");
        }

        return said;
    }

    /// <summary>
    /// Install one version into <paramref name="where"/>, verified end to end. Staged at
    /// <c>&lt;where&gt;.part</c> and moved into place only once it verified, so the managed directory
    /// holds a verified install or nothing — <see cref="HarnessSettings.ManagedBinary"/> finding one is
    /// the proof.
    /// </summary>
    /// <returns>The executable, inside <paramref name="where"/>.</returns>
    public static async Task<string> InstallAsync(
        string where, string version, Action<string> write, CancellationToken ct,
        HttpMessageHandler? transport = null, string? platform = null, (string Key, string Fingerprint)? trust = null)
    {
        ClaudeReleases.RefuseVersion(version);
        platform ??= Current();
        var binary = platform.StartsWith("win32", StringComparison.Ordinal) ? "claude.exe" : "claude";
        var (key, pinned) = trust ?? (ClaudeReleases.ReleaseKey, ClaudeReleases.Fingerprint);

        // A 200 MB download outlives any fixed timeout; the person's stop is the token.
        using var http = transport is null
            ? new HttpClient { Timeout = Timeout.InfiniteTimeSpan }
            : new HttpClient(transport, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("daoris");

        var staging = where + ".part";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        var into = Path.Combine(staging, "package", "bin");
        Directory.CreateDirectory(into);

        try
        {
            var manifestUrl = $"{ClaudeReleases.Base}/{version}/manifest.json";
            write($"fetching Claude Code {version}'s release manifest for {platform}, and its signature");
            var manifest = await BytesAsync(http, manifestUrl, ct).ConfigureAwait(false)
                ?? throw new DriverException(
                    $"the release bucket holds no Claude Code {version} — nothing answered at {manifestUrl}. Versions "
                    + "skip numbers; the release notes name the ones that exist.");
            var signature = await BytesAsync(http, manifestUrl + ".sig", ct).ConfigureAwait(false)
                ?? throw new DriverException(
                    $"Claude Code {version}'s manifest has no signature beside it, so Daoris cannot verify it and did "
                    + "not install it. Nothing was downloaded beyond the manifest.");

            var verdict = OpenPgp.VerifyDetached(manifest, Encoding.UTF8.GetString(signature), key, pinned);
            if (!verdict.Ok)
            {
                throw new DriverException(
                    $"Claude Code {version}'s manifest did not verify: {verdict.Reason}. Nothing was installed — a "
                    + "manifest that does not verify vouches for no binary.");
            }
            write($"the signature verifies: signed by the release key {OpenPgp.Spaced(verdict.Fingerprint!)}");

            var (checksum, size) = ClaudeReleases.Entry(manifest, manifestUrl, version, platform, binary);

            var url = $"{ClaudeReleases.Base}/{version}/{platform}/{binary}";
            write($"downloading {url} ({size / 1024.0 / 1024.0:0.0} MB)");
            var saved = await SaveAsync(http, url, Path.Combine(into, binary), ct).ConfigureAwait(false)
                ?? throw new DriverException($"nothing answered at {url}, which Claude Code {version}'s signed manifest names.");
            if (saved.Sha256 != checksum || saved.Size != size)
            {
                throw new DriverException(
                    $"the binary downloaded from {url} is not the one the signed manifest names — its SHA-256 is "
                    + $"{saved.Sha256} ({saved.Size} bytes), and the manifest says {checksum} ({size} bytes). It was "
                    + "deleted, and nothing was installed.");
            }
            write($"the binary's SHA-256 matches the signed manifest: {saved.Sha256}");

            if (!OperatingSystem.IsWindows() && !platform.StartsWith("win32", StringComparison.Ordinal))
            {
                File.SetUnixFileMode(Path.Combine(into, binary),
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            if (Directory.Exists(where))
            {
                // Daoris's own directory, holding nothing that runs (the caller asked ManagedBinary first).
                write($"replacing {where}, which held nothing that runs");
                Directory.Delete(where, recursive: true);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(where)!);
            // The scanner opens the executables just unpacked, and the folder cannot move until it lets go (FIX-LOG 2026-10-07).
            AtomicFile.MoveFolder(Path.Combine(staging, "package"), where);
            return Path.Combine(where, "bin", binary);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static async Task<byte[]?> BytesAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var response = await GetAsync(http, url, ct).ConfigureAwait(false);
        return response is null ? null : await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Stream a download to disk, hashing on the way — never held whole.</summary>
    private static async Task<(string Sha256, long Size)?> SaveAsync(HttpClient http, string url, string to, CancellationToken ct)
    {
        using var response = await GetAsync(http, url, ct).ConfigureAwait(false);
        if (response is null) return null;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = File.Create(to))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                size += read;
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }
        }

        return (Convert.ToHexStringLower(hash.GetHashAndReset()), size);
    }

    /// <summary>The response, or null for a 404 — "that version is not there" is an answer the caller words.</summary>
    private static async Task<HttpResponseMessage?> GetAsync(HttpClient http, string url, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException error)
        {
            throw new DriverException($"could not reach {new Uri(url).Host} — {error.Message}. Nothing was installed.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new DriverException($"{url} answered {status} — nothing was installed.");
        }

        return response;
    }
}
