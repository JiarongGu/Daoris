using System.ComponentModel;
using System.Diagnostics;
using Daoris.Driver;
using Shenora.Chromium;

namespace Daoris.Desktop;

/// <summary>
/// The shell's half of <see cref="IInAppBrowser"/> (D78): the browser the settings choose, either
/// Daoris's own on the engine it ships (D85, CHR3) — `daoris-browser`, a process of its own showing the
/// engine's own window — or the person's Edge on a profile under the home (BRW12). It answers the
/// chosen one's loopback endpoint.
/// </summary>
/// <remarks>
/// <para><b>A process of its own, by measurement.</b> The engine's debug port reaches every page in
/// its process (`docs/2026-09-28-chromium-embedding-evidence.md` §1), so the port lives in that
/// process and this one, which holds the bridge, has none.</para>
///
/// <para><b>This executable, started through the kit</b> (CHR8, D99). The browser is the application
/// started with <see cref="EngineBrowser.Argument"/>, so every build carries it, and it is started with
/// <see cref="ChromiumBrowserProcess.Start"/>, never <c>Process.Start</c>: the latter hands the child every
/// handle inheritable at that moment, a pipe end of Chromium's among them, and a browser that outlives
/// the app by design then kept the app from finishing its exit.</para>
///
/// <para><b>It lives and dies with the shell.</b> The browser is started with this process's id and
/// closes its windows when this process ends, as the shell's own window did. One port for the shell's
/// life, so a browser the person closed and a session reopens answers where the last one did.</para>
///
/// <para><b>Focus is Windows' decision.</b> A session asks for the first window in the background,
/// and the person's press asks for it in front. Measured both ways: Windows' own foreground rules
/// decided, not the ask (CHR3, 2026-09-28).</para>
/// </remarks>
public sealed class EngineBrowserHost(string home) : IInAppBrowser
{
    private static readonly TimeSpan BringUpLimit = TimeSpan.FromSeconds(45);

    /// <summary>How long a browser whose last window just closed is given to finish going.</summary>
    private static readonly TimeSpan Going = TimeSpan.FromSeconds(10);

    private readonly int _port = InAppBrowser.FreePort();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;

    /// <summary>How often Edge's session cookies are read while it runs: its closing is the person's, and unannounced.</summary>
    private static readonly TimeSpan KeepEvery = TimeSpan.FromSeconds(30);

    /// <summary>Edge's kept sign-in (BRW13), sealed to the account. Daoris's own engine keeps its own.</summary>
    private readonly BrowserSessionCookies _edgeSignIn = new(home, new DpapiSeal());
    private System.Threading.Timer? _keeper;

    /// <summary>Which browser the settings choose (BRW12), read each time: a terminal may have changed it.</summary>
    private bool EdgeChosen => BrowserSettings.Read(home).Browser == BrowserChoice.Edge;

    public void Show() => _ = Task.Run(async () =>
    {
        try
        {
            if (EdgeChosen) await BringUpEdgeAsync(activate: true, CancellationToken.None).ConfigureAwait(false);
            else await BringUpAsync(background: false, activate: true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is InvalidOperationException or TimeoutException)
        {
            // A module call has no screen to say this on, and a press that does nothing reads as broken.
            MessageBox.Show(error.Message, "Daoris", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    });

    /// <summary>
    /// A link on Daoris's page, in whichever browser the settings choose (BRW7): brought up as the
    /// person's press brings it up, then the page in a tab of its own, in front. A browser that was not
    /// running opens on its start page first, and the link's tab beside it.
    /// </summary>
    public void Open(string address) => _ = Task.Run(async () =>
    {
        try
        {
            var port = _port;
            if (EdgeChosen) port = await BringUpEdgeAsync(activate: false, CancellationToken.None).ConfigureAwait(false);
            else await BringUpAsync(background: false, activate: false, CancellationToken.None).ConfigureAwait(false);

            using var engine = new EngineCdp(port);
            var tab = await engine.NewTabAsync(address).ConfigureAwait(false);
            await engine.ActivateAsync(tab).ConfigureAwait(false);
        }
        catch (Exception error) when (error is InvalidOperationException or TimeoutException
                                          or HttpRequestException or System.Net.WebSockets.WebSocketException)
        {
            // A module call has no screen to say this on, and a link that opens nothing reads as broken.
            MessageBox.Show(error.Message, "Daoris", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    });

    public async Task<string?> EnsureAsync(CancellationToken ct = default)
    {
        if (EdgeChosen)
        {
            // Edge chosen and none installed is no browser to hand, as a build without its own is.
            if (EdgeBrowser.Locate() is null) return null;
            return InAppBrowser.Endpoint(await BringUpEdgeAsync(activate: false, ct).ConfigureAwait(false));
        }

        // Every build carries Daoris's own, being this executable (CHR8).
        await BringUpAsync(background: true, activate: false, ct).ConfigureAwait(false);
        return InAppBrowser.Endpoint(_port);
    }

    private async Task BringUpAsync(bool background, bool activate, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var engine = new EngineCdp(_port);
            if (_process is { HasExited: false } running)
            {
                if (await engine.PagesAsync(ct).ConfigureAwait(false) is { Count: > 0 } pages)
                {
                    if (activate) await engine.ActivateAsync(pages[0], ct).ConfigureAwait(false);
                    return;
                }

                // Its last window closed a moment ago and it is on its way out: let it go, then start
                // again, rather than ask a closing engine for a window.
                await running.WaitForExitAsync(ct).WaitAsync(Going, ct).ConfigureAwait(false);
            }

            var options = new EngineBrowserOptions(EngineBrowser.ProfileFolder(home), _port, Environment.ProcessId, background);
            try
            {
                _process = ChromiumBrowserProcess.Start(options.ToArguments());
            }
            catch (Win32Exception error)
            {
                throw new InvalidOperationException($"Daoris's browser did not start: {error.Message}", error);
            }

            var deadline = DateTime.UtcNow + BringUpLimit;
            while (DateTime.UtcNow < deadline)
            {
                if (_process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"Daoris's browser stopped as it started (exit {_process.ExitCode}). "
                        + $"Its log is {EngineBrowser.LogName} in {options.Profile}.");
                }

                if (await engine.PagesAsync(ct).ConfigureAwait(false) is { Count: > 0 }) return;
                await Task.Delay(100, ct).ConfigureAwait(false);
            }

            throw new TimeoutException($"Daoris's browser opened no window within {BringUpLimit.TotalSeconds:0} seconds.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The person's Edge (BRW12): the one Daoris started before when it still answers as an Edge, or a
    /// new one on the profile under the home, its port recorded beside it. Its port is the endpoint:
    /// Edge announces its own tabs as pages, so it needs no relay (CHR6).
    /// </summary>
    private async Task<int> BringUpEdgeAsync(bool activate, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var record = EdgeBrowser.RecordPath(home);
            if (EdgeBrowser.RecordedPort(File.Exists(record) ? File.ReadAllText(record) : null) is { } recorded)
            {
                using var running = new EngineCdp(recorded);
                if (await running.VersionAsync(ct).ConfigureAwait(false) is { } version && EdgeBrowser.IsEdge(version))
                {
                    if (await running.PagesAsync(ct).ConfigureAwait(false) is { Count: > 0 } pages)
                    {
                        if (activate) await running.ActivateAsync(pages[0], ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await running.NewWindowAsync("edge://newtab/", background: !activate, ct).ConfigureAwait(false);
                    }

                    await KeepEdgeSignInAsync(recorded).ConfigureAwait(false);
                    return recorded;
                }
            }

            var executable = EdgeBrowser.Locate()
                ?? throw new InvalidOperationException(
                    "Edge is not installed on this machine, and it is the browser Settings → Browser chooses. "
                    + "Choose Daoris's own there, or run `daoris browser use daoris`.");
            var port = InAppBrowser.FreePort();
            var profile = EdgeBrowser.ProfileFolder(home);
            Directory.CreateDirectory(profile);
            // Edge is a windowed program and opens no console either way; said anyway, because every
            // spawn the desktop makes says it (NoConsoleWindowTests), and a rule with exceptions is a list.
            // Not the tools' environment (TOOLS5): Edge is the system's own program, at its fixed location, and
            // starts none of the tools.
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in EdgeBrowser.Arguments(profile, port)) start.ArgumentList.Add(argument);
            Process.Start(start)?.Dispose();
            Daoris.Driver.AtomicFile.WriteText(record, EdgeBrowser.Record(port));

            using var edge = new EngineCdp(port);
            var deadline = DateTime.UtcNow + BringUpLimit;
            while (DateTime.UtcNow < deadline)
            {
                if (await edge.PagesAsync(ct).ConfigureAwait(false) is { Count: > 0 })
                {
                    // A fresh Edge holds none of the session cookies it had (the Edge evidence, §3): put
                    // back the kept ones it does not hold, before anyone navigates, then keep them again.
                    await RestoreEdgeSignInAsync(edge, ct).ConfigureAwait(false);
                    await KeepEdgeSignInAsync(port).ConfigureAwait(false);
                    return port;
                }

                await Task.Delay(100, ct).ConfigureAwait(false);
            }

            throw new TimeoutException($"Edge opened no window within {BringUpLimit.TotalSeconds:0} seconds.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Keep Edge's session cookies now, and every <see cref="KeepEvery"/> after, for as long as the
    /// recorded port answers as an Edge. Each look reads the record again: Edge restarted is another port.
    /// </summary>
    private async Task KeepEdgeSignInAsync(int port)
    {
        await SaveEdgeSignInAsync(port).ConfigureAwait(false);
        _keeper ??= new System.Threading.Timer(_ => _ = SaveRecordedEdgeSignInAsync(), null, KeepEvery, KeepEvery);
    }

    private async Task SaveRecordedEdgeSignInAsync()
    {
        var record = EdgeBrowser.RecordPath(home);
        if (EdgeBrowser.RecordedPort(File.Exists(record) ? File.ReadAllText(record) : null) is { } port)
        {
            await SaveEdgeSignInAsync(port).ConfigureAwait(false);
        }
    }

    private async Task SaveEdgeSignInAsync(int port)
    {
        try
        {
            using var edge = new EngineCdp(port);
            if (await edge.VersionAsync().ConfigureAwait(false) is not { } version || !EdgeBrowser.IsEdge(version)) return;
            var answer = await edge.BrowserCallAsync("Storage.getCookies", new { }).ConfigureAwait(false);
            _edgeSignIn.Save(CdpCookies.FromCdp(answer));
        }
        catch (Exception error) when (error is HttpRequestException or System.Net.WebSockets.WebSocketException
                                          or InvalidOperationException or IOException or TaskCanceledException)
        {
            // Edge closed between the look and the read, or a moment's miss: the next look keeps them.
        }
    }

    private async Task RestoreEdgeSignInAsync(EngineCdp edge, CancellationToken ct)
    {
        var kept = _edgeSignIn.Load();
        if (kept.Count == 0) return;
        try
        {
            var present = CdpCookies.FromCdp(await edge.BrowserCallAsync("Storage.getCookies", new { }, ct).ConfigureAwait(false));
            var missing = BrowserSessionCookies.ToRestore(kept, present);
            if (missing.Count > 0)
            {
                await edge.BrowserCallAsync("Storage.setCookies", CdpCookies.ToSetCookies(missing), ct).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is HttpRequestException or System.Net.WebSockets.WebSocketException or InvalidOperationException)
        {
            // An Edge that will not take them back opens signed out, where it would have been anyway.
        }
    }
}
