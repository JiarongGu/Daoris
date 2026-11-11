using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Daoris's own browser handed to a plugin's process: its DevTools address as <c>DAORIS_BROWSER</c>, to a plugin whose manifest
/// says <c>hooks.browser</c> and while a shell carries one, and to no other; an address the desktop itself inherited never travels on.
/// </summary>
/// <remarks>It holds the shell's browser in the process and plants the variable in its environment, which every class beside it reads, so it runs alone.</remarks>
[Collection(Alone.Name)]
public sealed class PluginBrowserTests : IDisposable
{
    private const string Address = "http://127.0.0.1:41999";

    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-plugin-browser-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        PluginBrowser.Hand(null);
        Environment.SetEnvironmentVariable(PluginBrowser.Variable, null);
        GitSeed.Discard(_root);
    }

    private PluginEntry Plugin(bool browser) => new(
        new PluginManifest("pr", 1, "pr", "1.0.0", "", [], new PluginHooks(["node", "plugin.mjs"], ["work/perform"], browser), []),
        _root, Path.Combine(_root, ".data"), Enabled: true, Problem: null);

    private sealed class Shell(string? endpoint, bool throws = false) : IInAppBrowser
    {
        public int Asked { get; private set; }

        public int Ensured { get; private set; }

        public Task<string?> EnsureAsync(CancellationToken ct = default)
        {
            Ensured++;
            return Task.FromResult(endpoint);
        }

        public Task<string?> EndpointAsync(CancellationToken ct = default)
        {
            Asked++;
            return throws ? throw new InvalidOperationException("the shell is closing") : Task.FromResult(endpoint);
        }

        public void Show() { }

        public void Open(string address) { }
    }

    [Fact]
    public async Task A_plugin_that_declares_the_browser_is_handed_its_address_and_one_that_does_not_is_handed_nothing()
    {
        var shell = new Shell(Address);
        PluginBrowser.Hand(shell);

        Assert.Equal(Address, await PluginBrowser.EndpointAsync(Plugin(browser: true), CancellationToken.None));
        Assert.Null(await PluginBrowser.EndpointAsync(Plugin(browser: false), CancellationToken.None));
        // The address only: the browser comes up at its first connection, never because a plugin started.
        Assert.Equal((1, 0), (shell.Asked, shell.Ensured));

        Environment.SetEnvironmentVariable(PluginBrowser.Variable, "http://127.0.0.1:1");
        Assert.Equal(Address, HookProcess.StartInfo(Plugin(browser: true), _root, Address).Environment[PluginBrowser.Variable]);
        Assert.False(HookProcess.StartInfo(Plugin(browser: false), _root, Address).Environment.ContainsKey(PluginBrowser.Variable));
        Assert.False(HookProcess.StartInfo(Plugin(browser: true), _root).Environment.ContainsKey(PluginBrowser.Variable));
    }

    [Fact]
    public async Task With_no_shell_or_a_shell_that_cannot_answer_none_is_handed_and_the_start_goes_on()
    {
        Assert.Null(await PluginBrowser.EndpointAsync(Plugin(browser: true), CancellationToken.None));

        PluginBrowser.Hand(new Shell(null));
        Assert.Null(await PluginBrowser.EndpointAsync(Plugin(browser: true), CancellationToken.None));

        PluginBrowser.Hand(new Shell(Address, throws: true));
        Assert.Null(await PluginBrowser.EndpointAsync(Plugin(browser: true), CancellationToken.None));
    }
}
