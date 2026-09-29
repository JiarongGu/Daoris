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

        public Task<string?> EnsureAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);

        public void Show() => Shown++;
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
    }
}
