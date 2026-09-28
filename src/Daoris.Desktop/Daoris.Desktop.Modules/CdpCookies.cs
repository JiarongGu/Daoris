using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// Cookies as the Chrome DevTools Protocol carries them, and as the kept sign-in holds them (BRW13):
/// what <c>Storage.getCookies</c> answers, and what <c>Storage.setCookies</c> is given. The carry the
/// Edge evidence measured (`docs/2026-09-28-managed-edge-evidence.md` §4), as code.
/// </summary>
public static class CdpCookies
{
    private static readonly string[] SameSites = ["Strict", "Lax", "None"];

    /// <summary>
    /// The cookies in a <c>Storage.getCookies</c> result. One without a name or a domain is skipped,
    /// and a same-site the protocol does not name is left unsaid.
    /// </summary>
    public static IReadOnlyList<BrowserCookie> FromCdp(JsonElement result)
    {
        if (!result.TryGetProperty("cookies", out var cookies) || cookies.ValueKind != JsonValueKind.Array) return [];

        var read = new List<BrowserCookie>();
        foreach (var cookie in cookies.EnumerateArray())
        {
            if (Text(cookie, "name") is not { Length: > 0 } name || Text(cookie, "domain") is not { Length: > 0 } domain) continue;
            read.Add(new BrowserCookie(
                name,
                Text(cookie, "value") ?? "",
                domain,
                Text(cookie, "path") ?? "/",
                Flag(cookie, "secure"),
                Flag(cookie, "httpOnly"),
                Text(cookie, "sameSite") is { } sameSite && SameSites.Contains(sameSite) ? sameSite : "",
                Flag(cookie, "session")));
        }

        return read;
    }

    /// <summary>The parameters of <c>Storage.setCookies</c> for these: no expiry, since they are session cookies.</summary>
    public static object ToSetCookies(IEnumerable<BrowserCookie> cookies) => new
    {
        cookies = cookies.Select(cookie => cookie.SameSite.Length > 0
            ? (object)new { name = cookie.Name, value = cookie.Value, domain = cookie.Domain, path = cookie.Path, secure = cookie.Secure, httpOnly = cookie.HttpOnly, sameSite = cookie.SameSite }
            : new { name = cookie.Name, value = cookie.Value, domain = cookie.Domain, path = cookie.Path, secure = cookie.Secure, httpOnly = cookie.HttpOnly })
            .ToList(),
    };

    private static string? Text(JsonElement cookie, string name) =>
        cookie.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement cookie, string name) =>
        cookie.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
