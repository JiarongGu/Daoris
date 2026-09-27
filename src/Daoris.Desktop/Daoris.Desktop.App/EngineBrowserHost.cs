using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The shell's half of <see cref="IInAppBrowser"/> on the engine Daoris ships (D85, CHR3): it starts
/// `daoris-browser`, a process of its own showing the engine's own Chromium window, and answers its
/// loopback endpoint.
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

    public void Show() => _ = Task.Run(async () =>
    {
        try
        {
            await BringUpAsync(background: false, activate: true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) when (error is InvalidOperationException or TimeoutException)
        {
            // A module call has no screen to say this on, and a press that does nothing reads as broken.
            MessageBox.Show(error.Message, "Daoris", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    });

    public async Task<string?> EnsureAsync(CancellationToken ct = default)
    {
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
}
