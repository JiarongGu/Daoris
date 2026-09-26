using System.Net;
using System.Net.Sockets;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Daoris's own browser (D78): a window of the shell, in its own WebView2 environment, that the person
/// signs in to and sessions drive over CDP. The window is WinForms and lives in the shell; what is here
/// is every judgement it makes — where its profile is, what it listens on, and which addresses the
/// person's address bar will go to.
/// </summary>
public sealed class InAppBrowserTests : Bridge
{
    /// <summary>
    /// Under the home (D63), and never the app's own WebView2 folder: two folders are two browser
    /// processes, so the debug port on this one reaches nothing of the page that holds the bridge.
    /// </summary>
    [Fact]
    public void Its_profile_is_the_homes_and_not_the_apps_own()
    {
        var home = Path.Combine("C:", "somewhere", "data");

        Assert.Equal(Path.Combine(home, "browser", "profile"), InAppBrowser.ProfileFolder(home));
        Assert.DoesNotContain("webview2", InAppBrowser.ProfileFolder(home), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void It_listens_on_loopback_at_the_port_it_was_given()
    {
        Assert.Equal("--remote-debugging-port=9422", InAppBrowser.Arguments(9422));
        Assert.Equal("http://127.0.0.1:9422", InAppBrowser.Endpoint(9422));
    }

    [Fact]
    public void A_free_port_is_one_nothing_holds()
    {
        var port = InAppBrowser.FreePort();

        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Assert.InRange(port, 1024, 65535);
    }

    /// <summary>
    /// What the person types. A host with no scheme is a site, over HTTPS unless it is this machine;
    /// anything that is not a web page — a file, a script, a credential in the address — goes nowhere.
    /// An agent drives the page over CDP and never passes through this.
    /// </summary>
    [Theory]
    [InlineData("https://tickets.example/browse/T-1", "https://tickets.example/browse/T-1")]
    [InlineData("tickets.example/browse/T-1", "https://tickets.example/browse/T-1")]
    [InlineData("  tickets.example  ", "https://tickets.example/")]
    [InlineData("localhost:4200", "http://localhost:4200/")]
    [InlineData("127.0.0.1:5177/api/status", "http://127.0.0.1:5177/api/status")]
    [InlineData("http://localhost:4200/", "http://localhost:4200/")]
    [InlineData("about:blank", "about:blank")]
    public void An_address_the_person_types_becomes_a_page(string typed, string expected)
    {
        Assert.Equal(expected, InAppBrowser.Address(typed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("file:///C:/secrets.txt")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>1</script>")]
    [InlineData("https://someone:password@tickets.example/")]
    [InlineData("two words")]
    public void Anything_that_is_not_a_web_page_goes_nowhere(string typed)
    {
        Assert.Null(InAppBrowser.Address(typed));
    }

    // ——— The page's door (D50: a screen door; the browser has no terminal twin, because it IS a window).

    private sealed class Browser : IInAppBrowser
    {
        public int Shown;

        public Task<string?> EnsureAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);

        public void Show() => Shown++;
    }

    private sealed class NoWindows : ISecondaryWindows
    {
        public bool Open(string name, string address) => true;

        public IReadOnlyList<string> Opened => [];
    }

    [Fact]
    public async Task The_page_opens_the_browser_for_the_person()
    {
        var browser = new Browser();
        var module = new WindowsModule(Bus, new NoWindows(), new PlatformAddress("http://localhost:5177"), browser);

        await AnswerAsync(module, "OPEN_BROWSER", new { });

        Assert.Equal(1, browser.Shown);
    }
}
