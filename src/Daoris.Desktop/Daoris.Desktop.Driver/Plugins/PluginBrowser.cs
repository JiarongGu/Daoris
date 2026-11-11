namespace Daoris.Driver;

/// <summary>
/// Daoris's own browser as a plugin's process is handed it: the loopback DevTools address, as <see cref="Variable"/>, to a plugin whose
/// manifest says <c>hooks.browser</c> and only while a shell carries a browser (D78). The plugin opens a tab of its own there and makes
/// its calls from the person's signed-in page; Daoris hands the address, never a cookie, a token or the page's content, and a
/// plugin that does not declare it is handed nothing, whatever the desktop's own environment held.
/// </summary>
public static class PluginBrowser
{
    /// <summary>The variable the address travels under.</summary>
    public const string Variable = "DAORIS_BROWSER";

    private static IInAppBrowser? _held;

    /// <summary>The shell's browser, held for every start of a plugin in this process; null where no shell carries one (a headless host).</summary>
    public static void Hand(IInAppBrowser? browser) => Volatile.Write(ref _held, browser);

    /// <summary>
    /// The address a start of <paramref name="plugin"/> is handed: null where it declares no browser, or no browser answers. Only
    /// the address is asked, as a session's server is (<see cref="InAppBrowserServers"/>): the browser comes up at its first connection.
    /// </summary>
    public static async Task<string?> EndpointAsync(PluginEntry plugin, CancellationToken ct)
    {
        if (plugin.Manifest.Hooks is not { Browser: true } || Volatile.Read(ref _held) is not { } browser) return null;
        try
        {
            return await browser.EndpointAsync(ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The plugin then fails as any process with no browser does, saying so in its own words.
            return null;
        }
    }
}
