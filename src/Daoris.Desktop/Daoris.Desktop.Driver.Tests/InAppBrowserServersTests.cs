using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A plugin server that drives Daoris's own browser (D78): `${browser}` is filled in when a session is
/// handed its servers, from the shell's answer — and where there is no answer, the server is not handed
/// and the session is told why, never handed a placeholder that fails inside the harness unexplained.
/// </summary>
public sealed class InAppBrowserServersTests
{
    private static readonly AcpMcpServer Knowledge =
        new("daoris-knowledge", "daoris-knowledge.exe", [], new Dictionary<string, string>());

    private static readonly AcpMcpServer Attached = new(
        "browser", "npx", ["-y", "@playwright/mcp@0.0.82", "--cdp-endpoint", "${browser}"],
        new Dictionary<string, string> { ["BROWSER"] = "${browser}" });

    private sealed class Browser(Func<string?> answer) : IInAppBrowser
    {
        public int Asked;

        public Task<string?> EnsureAsync(CancellationToken ct = default)
        {
            Asked++;
            return Task.FromResult(answer());
        }

        public void Show() { }

        public void Open(string address) { }
    }

    [Fact]
    public void A_server_that_attaches_is_handed_the_endpoint_in_its_arguments_and_environment()
    {
        var (handed, withheld) = InAppBrowserServers.Resolve([Knowledge, Attached], "http://127.0.0.1:4810");

        Assert.Empty(withheld);
        var browser = Assert.Single(handed, server => server.Name == "browser");
        Assert.Equal(["-y", "@playwright/mcp@0.0.82", "--cdp-endpoint", "http://127.0.0.1:4810"], browser.Arguments);
        Assert.Equal("http://127.0.0.1:4810", browser.Environment["BROWSER"]);
        Assert.Contains(handed, server => server.Name == "daoris-knowledge");
    }

    [Fact]
    public void With_no_endpoint_it_is_withheld_by_name_and_the_rest_are_handed()
    {
        var (handed, withheld) = InAppBrowserServers.Resolve([Knowledge, Attached], null);

        Assert.Equal(["browser"], withheld);
        Assert.Equal(["daoris-knowledge"], handed.Select(server => server.Name));
    }

    /// <summary>A machine with no such plugin never has a window opened for it.</summary>
    [Fact]
    public async Task The_browser_is_asked_for_only_when_a_server_needs_it()
    {
        var browser = new Browser(() => "http://127.0.0.1:4810");

        var (handed, notice) = await InAppBrowserServers.HandAsync([Knowledge], browser, CancellationToken.None);

        Assert.Equal(0, browser.Asked);
        Assert.Null(notice);
        Assert.Single(handed);
    }

    [Fact]
    public async Task A_host_with_no_browser_withholds_the_server_and_says_so()
    {
        var (handed, notice) = await InAppBrowserServers.HandAsync([Knowledge, Attached], null, CancellationToken.None);

        Assert.DoesNotContain(handed, server => server.Name == "browser");
        Assert.Contains("`browser`", notice);
        Assert.Contains("only the desktop shell", notice);
    }

    [Fact]
    public async Task A_browser_that_would_not_come_up_is_named_with_its_reason()
    {
        var browser = new Browser(() => throw new InvalidOperationException("the in-app browser was closed."));

        var (handed, notice) = await InAppBrowserServers.HandAsync([Attached], browser, CancellationToken.None);

        Assert.Empty(handed);
        Assert.Contains("the in-app browser was closed.", notice);
    }

    [Fact]
    public async Task A_browser_that_answers_is_asked_once_and_the_server_is_handed_its_endpoint()
    {
        var browser = new Browser(() => "http://127.0.0.1:4810");

        var (handed, notice) = await InAppBrowserServers.HandAsync([Knowledge, Attached], browser, CancellationToken.None);

        Assert.Equal(1, browser.Asked);
        Assert.Null(notice);
        Assert.Contains("http://127.0.0.1:4810", Assert.Single(handed, server => server.Name == "browser").Arguments);
    }
}
