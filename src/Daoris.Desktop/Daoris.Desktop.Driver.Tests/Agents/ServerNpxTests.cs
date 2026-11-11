using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A server started by node that hands an <c>npx</c> command on after its <c>--</c> is handed as a server declared through
/// <c>npx</c> is: the package installed once into the toolchain and started as node and its script in npx's place, and, where the
/// server drives Daoris's browser, that start under the keeper, which bounds the process that grows.
/// </summary>
public sealed class ServerNpxTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "server-npx", Guid.NewGuid().ToString("N")[..8], "a home");

    public ServerNpxTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(Path.GetDirectoryName(_home)!);

    private static ServerEntry Wrapped(params string[] npx) =>
        new("browser", "node", ["server.mjs", "--quiet", "--", "npx", .. npx], new Dictionary<string, string>());

    private static void IsNode(string word)
    {
        Assert.True(Path.IsPathFullyQualified(word), $"`{word}` is no whole path");
        Assert.Equal("node", Path.GetFileNameWithoutExtension(word), ignoreCase: true);
    }

    [Fact]
    public async Task A_node_start_handing_npx_on_that_drives_Daoris_browser_hands_on_the_packages_script_under_the_keeper()
    {
        var script = SessionServersTests.Lay(_home, "@playwright/mcp", "0.0.82", bin: "playwright-mcp");

        var (handed, _, drives, drivers) = await SessionServers.HandNotedAsync(
            _home, [Wrapped("-y", "@playwright/mcp@0.0.82", "--cdp-endpoint", InAppBrowserServers.Placeholder)],
            new InAppBrowserServersTests.LazyBrowser("http://127.0.0.1:9"), log: null, CancellationToken.None);

        Assert.True(drives);
        Assert.Equal(["browser"], drivers);
        var server = Assert.Single(handed);
        Assert.Equal("node", server.Command);
        Assert.Equal(["server.mjs", "--quiet", "--"], server.Arguments.Take(3));
        IsNode(server.Arguments[3]);
        Assert.Equal(
            [ServerKeeper.Install(_home), ServerKeeper.CeilingMb.ToString(), ServerKeeper.EveryMs.ToString(), script, "--cdp-endpoint", "http://127.0.0.1:9"],
            server.Arguments.Skip(4));
        Assert.DoesNotContain(server.Arguments, word => NpxCommand.Names(word));
    }

    [Fact]
    public async Task A_node_start_handing_npx_on_that_drives_no_browser_hands_on_the_packages_script_alone()
    {
        var script = SessionServersTests.Lay(_home, "@acme/tickets", "1.2.3");

        var (handed, _, drives, _) = await SessionServers.HandNotedAsync(
            _home, [Wrapped("-y", "@acme/tickets@1.2.3", "--port", "7")], browser: null, log: null, CancellationToken.None);

        Assert.False(drives);
        var server = Assert.Single(handed);
        IsNode(server.Arguments[3]);
        Assert.Equal(["server.mjs", "--quiet", "--", server.Arguments[3], script, "--port", "7"], server.Arguments);
    }

    [Fact]
    public async Task A_node_start_whose_npx_cannot_be_read_is_not_handed_and_one_handing_no_npx_on_is_handed_as_declared()
    {
        var plain = new ServerEntry("plain", "node", ["server.mjs", "--", "serve"], new Dictionary<string, string>());

        var handed = await SessionServers.DirectAsync(_home, [Wrapped("-c", "serve"), plain], log: null, CancellationToken.None);

        Assert.Equal([plain], handed);
    }

    [Theory]
    [InlineData("node", new[] { "s.mjs", "--", "npx", "-y", "pkg@1", "a" }, new[] { "s.mjs", "--" }, "pkg", new[] { "a" })]
    [InlineData(@"C:\tools\node.exe", new[] { "--", "npx.cmd", "pkg" }, new[] { "--" }, "pkg", new string[0])]
    [InlineData("npx", new[] { "-y", "pkg@1", "a" }, null, "pkg", new[] { "a" })]
    public void Where_a_start_runs_npx_is_read_with_the_words_before_it(
        string command, string[] arguments, string[]? before, string package, string[] rest)
    {
        var (start, refusal) = ServerNpx.Read(command, arguments);

        Assert.Null(refusal);
        Assert.Equal(before, start!.Before?.ToArray());
        Assert.Equal(package, start.Npx.Package);
        Assert.Equal(rest, start.Npx.Arguments);
    }

    [Theory]
    [InlineData("node", new[] { "s.mjs", "npx", "pkg" })]
    [InlineData("node", new[] { "s.mjs", "--", "serve" })]
    [InlineData("deno", new[] { "s.mjs", "--", "npx", "pkg" })]
    public void A_start_that_runs_no_npx_is_none(string command, string[] arguments)
    {
        Assert.Equal((null, null), ServerNpx.Read(command, arguments));
    }
}
