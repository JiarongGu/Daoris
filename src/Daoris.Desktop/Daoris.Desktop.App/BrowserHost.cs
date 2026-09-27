using Daoris.Driver;
using Shenora;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// The shell's half of <see cref="IInAppBrowser"/> (D78): Daoris's own browser, one window by name
/// among the shell's windows, on its own pump like the monitor.
/// </summary>
/// <remarks>
/// <para><b>One port for the process's life.</b> A window the person closed and a session reopens
/// joins the same profile with the same options, which is what lets WebView2 reuse a browser process
/// that is still winding down rather than refuse a second one with different arguments.</para>
///
/// <para><b>Asked by a session, it does not come forward.</b> A window that is already open is left
/// where it is and its endpoint answered. One that is not is opened without taking focus. Only the
/// person's own press brings it to the front.</para>
/// </remarks>
public sealed class BrowserHost(SecondaryWindows windows, ShenoraPaths paths, string home) : IInAppBrowser
{
    private static readonly TimeSpan BringUpLimit = TimeSpan.FromSeconds(45);

    private readonly int _port = InAppBrowser.FreePort();

    /// <summary>The kept sign-in (BRW10), one for the process, so a reopened window writes only what changed.</summary>
    private readonly BrowserSessionCookies _cookies = new(home, new DpapiSeal());

    private readonly object _gate = new();
    private TaskCompletionSource<BrowserForm>? _opened;

    public void Show() => Open(activate: true, force: true);

    public async Task<string?> EnsureAsync(CancellationToken ct = default)
    {
        var form = await Open(activate: false, force: false).WaitAsync(BringUpLimit, ct).ConfigureAwait(false);
        await form.Ready.WaitAsync(BringUpLimit, ct).ConfigureAwait(false);
        return InAppBrowser.Endpoint(_port);
    }

    /// <param name="force">Open even when a window is up — which, for an open one, brings it forward.</param>
    private Task<BrowserForm> Open(bool activate, bool force)
    {
        lock (_gate)
        {
            if (!force && _opened is { } live && windows.HasWindow(InAppBrowser.WindowName)) return live.Task;

            var opened = new TaskCompletionSource<BrowserForm>(TaskCreationOptions.RunContinuationsAsynchronously);
            var created = windows.Open(InAppBrowser.WindowName, new SecondaryWindowOptions
            {
                // Runs ON the window's own STA thread; the pump shows it once its geometry is applied.
                CreateForm = () =>
                {
                    var form = new BrowserForm(InAppBrowser.ProfileFolder(home), _port, activate, _cookies);
                    opened.TrySetResult(form);
                    return form;
                },
                StateStore = new JsonFileWindowStateStore(
                    Path.Combine(paths.DataArea("config"), "windows", SecondaryWindow.StateFile(InAppBrowser.WindowName))),
                StateOptions = new WindowStateOptions
                {
                    DefaultWidth = 1200,
                    DefaultHeight = 860,
                    MinWidth = 520,
                    MinHeight = 360,
                },
            });

            // Already open: the framework brought it forward, and the form it holds is the one to wait on.
            if (!created && _opened is { } existing) return existing.Task;
            _opened = opened;
            return opened.Task;
        }
    }
}
