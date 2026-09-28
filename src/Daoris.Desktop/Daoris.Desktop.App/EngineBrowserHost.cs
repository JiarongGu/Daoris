using System.Diagnostics;
using Daoris.Driver;

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

    public async Task<string?> EnsureAsync(CancellationToken ct = default)
    {
        if (EdgeChosen)
        {
            // Edge chosen and none installed is no browser to hand, as a build without its own is.
            if (EdgeBrowser.Locate() is null) return null;
            return InAppBrowser.Endpoint(await BringUpEdgeAsync(activate: false, ct).ConfigureAwait(false));
        }

        // A build that carries no browser has none to hand: the driver withholds the server and says so.
        if (EngineBrowser.Locate(AppContext.BaseDirectory) is null) return null;
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

            var executable = EngineBrowser.Locate(AppContext.BaseDirectory)
                ?? throw new InvalidOperationException(
                    "Daoris's browser is not in this build. It looked for "
                    + string.Join(", ", EngineBrowser.Candidates(AppContext.BaseDirectory)) + ".");
            var options = new EngineBrowserOptions(EngineBrowser.ProfileFolder(home), _port, Environment.ProcessId, background);
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
            };
            foreach (var argument in options.ToArguments()) start.ArgumentList.Add(argument);
            _process = Process.Start(start) ?? throw new InvalidOperationException("Daoris's browser did not start.");

            var deadline = DateTime.UtcNow + BringUpLimit;
            while (DateTime.UtcNow < deadline)
            {
                if (_process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"Daoris's browser stopped as it started (exit {_process.ExitCode}). "
                        + $"Its log is engine.log in {options.Profile}.");
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
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (var argument in EdgeBrowser.Arguments(profile, port)) start.ArgumentList.Add(argument);
            Process.Start(start)?.Dispose();
            Daoris.Driver.AtomicFile.WriteText(record, EdgeBrowser.Record(port));

            using var edge = new EngineCdp(port);
            var deadline = DateTime.UtcNow + BringUpLimit;
            while (DateTime.UtcNow < deadline)
            {
                if (await edge.PagesAsync(ct).ConfigureAwait(false) is { Count: > 0 }) return port;
                await Task.Delay(100, ct).ConfigureAwait(false);
            }

            throw new TimeoutException($"Edge opened no window within {BringUpLimit.TotalSeconds:0} seconds.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
