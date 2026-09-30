using System.Net;
using System.Net.Sockets;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Daoris's own browser (D78), whichever engine shows it: the endpoint a server attaches to, the port
/// it listens on, the address rule its favorites and history keep, and the page's door onto it. Where
/// its profile is and how it starts is the engine's (<see cref="EngineBrowserTests"/>).
/// </summary>
public sealed class InAppBrowserTests : Bridge
{
    [Fact]
    public void It_is_reached_on_loopback_at_the_port_it_was_given()
    {
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
        public readonly List<string> Opened = [];

        public Task<string?> EnsureAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);

        public void Show() => Shown++;

        public void Open(string address) => Opened.Add(address);
    }

    private sealed class NoWindows : ISecondaryWindows
    {
        public bool Open(string name, string address) => true;

        public IReadOnlyList<string> Opened => [];

        public bool SetTheme(string name, bool dark) => false;
    }

    [Fact]
    public async Task The_page_opens_the_browser_for_the_person()
    {
        var browser = new Browser();
        var module = new WindowsModule(Bus, new NoWindows(), new PlatformAddress("http://localhost:5177"), browser);

        await AnswerAsync(module, "OPEN_BROWSER", new { });

        Assert.Equal(1, browser.Shown);
        Assert.Empty(browser.Opened);
    }

    /// <summary>
    /// BRW7: a link on the page, opened in Daoris's browser where the person chose that — on the page
    /// it names, in its parsed form, rather than on the browser's start page.
    /// </summary>
    [Fact]
    public async Task The_page_opens_a_link_in_the_browser_on_the_page_it_names()
    {
        var browser = new Browser();
        var module = new WindowsModule(Bus, new NoWindows(), new PlatformAddress("http://localhost:5177"), browser);

        var state = await AnswerAsync(module, "OPEN_BROWSER", new { url = "https://Tickets.Example/browse/T-1" });

        Assert.Equal(["https://tickets.example/browse/T-1"], browser.Opened);
        Assert.Equal(0, browser.Shown);
        Assert.True(state.GetProperty("opened").GetBoolean());
    }

    /// <summary>What is no web page — a script, a file, a credential in the address — is refused by code and opens nothing.</summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/secrets.txt")]
    [InlineData("https://someone:password@tickets.example/")]
    [InlineData("about:blank")]
    public async Task A_link_that_is_no_web_page_is_refused_by_code_and_opens_nothing(string url)
    {
        var browser = new Browser();
        var module = new WindowsModule(Bus, new NoWindows(), new PlatformAddress("http://localhost:5177"), browser);

        var refusal = await RefusalAsync(module, "OPEN_BROWSER", new { url });

        Assert.Contains(Refusals.BrowserLinkNotAPage, refusal);
        Assert.Empty(browser.Opened);
        Assert.Equal(0, browser.Shown);
    }

    /// <summary>A host that carries no browser answers that nothing opened, rather than a failure.</summary>
    [Fact]
    public async Task With_no_browser_a_link_opens_nothing_and_says_so()
    {
        var module = new WindowsModule(Bus, new NoWindows(), new PlatformAddress("http://localhost:5177"));

        var state = await AnswerAsync(module, "OPEN_BROWSER", new { url = "https://tickets.example/" });

        Assert.False(state.GetProperty("opened").GetBoolean());
    }
}
