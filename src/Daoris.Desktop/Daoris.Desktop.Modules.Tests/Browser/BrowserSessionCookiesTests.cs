using System.Security.Cryptography;
using System.Text;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// BRW13 (first BRW10's): a sign-in in the person's Edge survives Edge restarting. An identity server's
/// session cookie ends with the browser process, and Edge restores none (the Edge evidence, §3), as the
/// WebView2 window restored none before it, when the owner's sign-in was gone after every republish
/// (FG5). Daoris keeps Edge's session cookies under the home, sealed to the Windows account, and puts
/// them back over CDP when it starts Edge again.
/// </summary>
/// <remarks>
/// The seal here is a stand-in: the real one is DPAPI, in the shell, which is Windows-only and this
/// suite also runs on Linux. What is held here is everything around it.
/// </remarks>
public sealed class BrowserSessionCookiesTests : Bridge
{
    /// <summary>Reverses the bytes, which is enough to prove nothing reaches the disk as it was.</summary>
    private sealed class StandInSeal : ICookieSeal
    {
        public byte[] Seal(byte[] plain) => [.. plain.Reverse()];

        public byte[] Open(byte[] sealedBytes) => [.. sealedBytes.Reverse()];
    }

    /// <summary>A seal made for another account: it opens nothing.</summary>
    private sealed class ForeignSeal : ICookieSeal
    {
        public byte[] Seal(byte[] plain) => plain;

        public byte[] Open(byte[] sealedBytes) => throw new CryptographicException("the data is invalid.");
    }

    private static BrowserCookie Session(string name, string value, string domain = "id.example.com") =>
        new(name, value, domain, "/", Secure: true, HttpOnly: true, SameSite: "Lax", Session: true);

    [Fact]
    public void Kept_session_cookies_come_back_whole()
    {
        var store = new BrowserSessionCookies(Home, new StandInSeal());
        BrowserCookie[] cookies =
        [
            Session("idsrv.session", "a1b2c3"),
            new("tenant", "north", ".example.com", "/app", Secure: false, HttpOnly: false, SameSite: "None", Session: true),
        ];

        store.Save(cookies);

        Assert.Equal(cookies, new BrowserSessionCookies(Home, new StandInSeal()).Load());
    }

    /// <summary>
    /// 🔴 A session cookie is a credential. What reaches the disk is the seal's, under the home, and the
    /// value is nowhere in it.
    /// </summary>
    [Fact]
    public void What_reaches_the_disk_is_sealed_under_the_home()
    {
        new BrowserSessionCookies(Home, new StandInSeal()).Save([Session("idsrv.session", "secret-session-value")]);

        var file = BrowserSessionCookies.FilePath(Home);
        Assert.Equal(Path.Combine(Home, "browser", "edge-session-cookies.bin"), file);
        var bytes = File.ReadAllBytes(file);
        Assert.DoesNotContain("secret-session-value", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.DoesNotContain(Directory.GetFiles(Path.GetDirectoryName(file)!), path => path != file);
    }

    /// <summary>
    /// Only session cookies are kept. A cookie with an expiry is the profile's own, which Edge keeps
    /// already, and keeping it here too would be a second copy that could disagree with the first.
    /// </summary>
    [Fact]
    public void Only_session_cookies_are_kept()
    {
        var store = new BrowserSessionCookies(Home, new StandInSeal());

        store.Save([Session("idsrv.session", "a1"), Session("remember", "me") with { Session = false }]);

        Assert.Equal(["idsrv.session"], store.Load().Select(cookie => cookie.Name));
    }

    /// <summary>The same set is not written twice: the browser asks to save after every page.</summary>
    [Fact]
    public void Saving_what_is_already_kept_writes_nothing()
    {
        var store = new BrowserSessionCookies(Home, new StandInSeal());

        Assert.True(store.Save([Session("idsrv.session", "a1")]));
        Assert.False(store.Save([Session("idsrv.session", "a1")]));
        Assert.True(store.Save([Session("idsrv.session", "a2")]));
        Assert.True(store.Save([]));
        Assert.Empty(store.Load());
    }

    /// <summary>
    /// What is put back is only what the browser does not hold. A window reopened in the same run can
    /// find its browser process still alive, holding a session cookie the site has rotated since the
    /// last save, and the kept, older value must not overwrite it.
    /// </summary>
    [Fact]
    public void Only_what_the_browser_does_not_hold_is_put_back()
    {
        BrowserCookie[] kept = [Session("idsrv.session", "old"), Session("tenant", "north", ".example.com")];
        BrowserCookie[] present = [Session("idsrv.session", "rotated"), Session("tenant", "north", "other.example.com")];

        var restored = BrowserSessionCookies.ToRestore(kept, present);

        Assert.Equal([kept[1]], restored);
    }

    /// <summary>
    /// Nothing kept, a file this account cannot open (copied from another machine or account), or one
    /// that is not what was written, restores nothing and says why. A browser that cannot put a sign-in
    /// back still opens, signed out, which is where it was before BRW10.
    /// </summary>
    [Fact]
    public void A_file_that_is_missing_foreign_or_broken_restores_nothing_and_says_why()
    {
        Assert.Empty(new BrowserSessionCookies(Home, new StandInSeal()).Load());

        new BrowserSessionCookies(Home, new StandInSeal()).Save([Session("idsrv.session", "a1")]);
        var foreign = new BrowserSessionCookies(Home, new ForeignSeal());
        Assert.Empty(foreign.Load());
        Assert.Contains("could not be opened", foreign.Problem);

        File.WriteAllBytes(BrowserSessionCookies.FilePath(Home), [.. Encoding.UTF8.GetBytes("not what was written").Reverse()]);
        var broken = new BrowserSessionCookies(Home, new StandInSeal());
        Assert.Empty(broken.Load());
        Assert.Contains("could not be read", broken.Problem);
    }
}
