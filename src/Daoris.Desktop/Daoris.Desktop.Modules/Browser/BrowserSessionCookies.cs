using System.Security.Cryptography;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>A cookie as the kept sign-in holds it (BRW10, BRW13): CDP's fields, in names this side owns.</summary>
/// <param name="SameSite"><c>None</c>, <c>Lax</c> or <c>Strict</c>, as CDP names them.</param>
/// <param name="Session">Whether it has no expiry, and so ends with the browser process.</param>
public sealed record BrowserCookie(
    string Name, string Value, string Domain, string Path, bool Secure, bool HttpOnly, string SameSite, bool Session);

/// <summary>
/// What seals the kept cookies to this machine's account: DPAPI, in the shell. <see cref="Open"/>
/// throws a <see cref="CryptographicException"/> for bytes it did not seal.
/// </summary>
public interface ICookieSeal
{
    byte[] Seal(byte[] plain);

    byte[] Open(byte[] sealedBytes);
}

/// <summary>
/// A sign-in in the person's Edge that survives Edge restarting (BRW13, first built as BRW10 for the
/// WebView2 window): its session cookies, read over CDP while it runs, kept under the home and sealed
/// to the account, and put back over CDP when Daoris starts it again.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> An identity server's session cookie has no expiry, so it ends with the browser
/// process. Edge restores none, even with its own <i>continue where you left off</i>
/// (`docs/2026-09-28-managed-edge-evidence.md` §3), and carrying them over CDP brought the session back
/// (§4). Daoris's own browser needs none of this: its engine keeps them itself. The profile already
/// keeps every cookie that has an expiry, so only session cookies are kept here, and never a second copy
/// of one the profile holds.</para>
///
/// <para>🔴 <b>A session cookie is a credential.</b> It is kept under the home (D63), which never leaves
/// the machine, and sealed to the Windows account, so the file copied anywhere else opens nothing. The
/// site's own session lifetime still governs: what is put back is what the browser had, and a site that
/// has ended the session refuses it.</para>
/// </remarks>
public sealed class BrowserSessionCookies(string home, ICookieSeal seal)
{
    private const int Version = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>What was last written or read, so the same set is not written again after every page.</summary>
    private byte[]? _kept;

    /// <summary>Where they are kept: beside the browser's profile, under the home.</summary>
    public static string FilePath(string home) => System.IO.Path.Combine(home, "browser", "edge-session-cookies.bin");

    /// <summary>Why the last <see cref="Load"/> restored nothing from a file that was there, or null.</summary>
    public string? Problem { get; private set; }

    /// <summary>Keep the session cookies among these, replacing what was kept. False when nothing changed.</summary>
    public bool Save(IEnumerable<BrowserCookie> cookies)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(new Kept(Version, [.. cookies.Where(cookie => cookie.Session)]), Json);
        if (_kept is not null && _kept.AsSpan().SequenceEqual(plain)) return false;

        var file = FilePath(home);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        AtomicFile.WriteBytes(file, seal.Seal(plain));
        _kept = plain;
        return true;
    }

    /// <summary>
    /// What was kept, or nothing, with <see cref="Problem"/> saying why when a file was there. A browser
    /// that cannot put a sign-in back still opens, signed out, which is where Edge alone would be.
    /// </summary>
    public IReadOnlyList<BrowserCookie> Load()
    {
        Problem = null;
        var file = FilePath(home);
        if (!File.Exists(file)) return [];

        byte[] plain;
        try
        {
            plain = seal.Open(File.ReadAllBytes(file));
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException)
        {
            // Sealed by another account or machine, or held: the next save replaces it.
            Problem = $"the kept sign-in could not be opened by this account: {error.Message}";
            return [];
        }

        try
        {
            var kept = JsonSerializer.Deserialize<Kept>(plain, Json);
            if (kept is not { Version: Version, Cookies: not null }) throw new JsonException("it is not a kept set of this version.");
            _kept = plain;
            return [.. kept.Cookies.Where(cookie => cookie.Session)];
        }
        catch (JsonException error)
        {
            Problem = $"the kept sign-in could not be read: {error.Message}";
            return [];
        }
    }

    /// <summary>
    /// The kept cookies the browser does not hold now, by name, domain and path: a window reopened in
    /// the same run can find its browser process still alive, with a value the site rotated since the
    /// last save, and the kept one must not overwrite it.
    /// </summary>
    public static IReadOnlyList<BrowserCookie> ToRestore(IReadOnlyList<BrowserCookie> kept, IEnumerable<BrowserCookie> present)
    {
        var held = present.Select(Identity).ToHashSet();
        return [.. kept.Where(cookie => !held.Contains(Identity(cookie)))];
    }

    /// <summary>What makes two cookies the same cookie: a site may set one name on two domains or paths.</summary>
    private static (string, string, string) Identity(BrowserCookie cookie) =>
        (cookie.Name, cookie.Domain.ToLowerInvariant(), cookie.Path);

    private sealed record Kept(int Version, IReadOnlyList<BrowserCookie>? Cookies);
}
