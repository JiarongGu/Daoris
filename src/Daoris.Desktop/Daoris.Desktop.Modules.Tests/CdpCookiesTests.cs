using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// BRW13: Edge's cookies as the protocol carries them. Read from <c>Storage.getCookies</c> while it
/// runs, and given back to <c>Storage.setCookies</c> when Daoris starts it again — the carry the Edge
/// evidence measured bringing a session back (§4).
/// </summary>
public sealed class CdpCookiesTests : Bridge
{
    private const string Answer = """
        { "cookies": [
            { "name": "sid", "value": "a1b2", "domain": "127.0.0.1", "path": "/", "expires": -1, "size": 7,
              "httpOnly": true, "secure": false, "session": true, "sameSite": "Lax", "priority": "Medium" },
            { "name": "remember", "value": "me", "domain": ".site.example", "path": "/app", "expires": 1893456000,
              "httpOnly": false, "secure": true, "session": false },
            { "name": "", "value": "no name", "domain": "site.example", "session": true },
            { "name": "odd", "value": "v", "domain": "site.example", "session": true, "sameSite": "Weird" }
          ] }
        """;

    [Fact]
    public void A_get_cookies_answer_becomes_cookies_with_their_session_said()
    {
        var cookies = CdpCookies.FromCdp(JsonDocument.Parse(Answer).RootElement);

        Assert.Equal(
        [
            new BrowserCookie("sid", "a1b2", "127.0.0.1", "/", Secure: false, HttpOnly: true, SameSite: "Lax", Session: true),
            new BrowserCookie("remember", "me", ".site.example", "/app", Secure: true, HttpOnly: false, SameSite: "", Session: false),
            new BrowserCookie("odd", "v", "site.example", "/", Secure: false, HttpOnly: false, SameSite: "", Session: true),
        ], cookies);
    }

    [Fact]
    public void An_answer_with_no_cookies_is_none() =>
        Assert.Empty(CdpCookies.FromCdp(JsonDocument.Parse("{}").RootElement));

    /// <summary>What is put back carries no expiry, and a same-site only where one was said.</summary>
    [Fact]
    public void What_is_put_back_is_what_was_read_without_an_expiry()
    {
        var parameters = JsonSerializer.SerializeToElement(CdpCookies.ToSetCookies(
        [
            new BrowserCookie("sid", "a1b2", "127.0.0.1", "/", Secure: false, HttpOnly: true, SameSite: "Lax", Session: true),
            new BrowserCookie("odd", "v", "site.example", "/", Secure: false, HttpOnly: false, SameSite: "", Session: true),
        ]));

        var first = parameters.GetProperty("cookies")[0];
        Assert.Equal("sid", first.GetProperty("name").GetString());
        Assert.Equal("Lax", first.GetProperty("sameSite").GetString());
        Assert.False(first.TryGetProperty("expires", out _));
        Assert.False(parameters.GetProperty("cookies")[1].TryGetProperty("sameSite", out _));
    }
}
