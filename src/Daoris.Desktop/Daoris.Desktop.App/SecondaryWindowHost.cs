using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// The shell's half of <see cref="ISecondaryWindows"/> (SURF8): named windows, each on its own STA
/// thread, each carrying the same platform bundle at its own route.
/// </summary>
/// <remarks>
/// <para><b>One window per name, and opening an open one activates it</b> — the framework's own
/// contract, which is what makes "open the monitor" safe to press twice. Geometry is remembered per
/// name through the same window-state stack the main window uses.</para>
///
/// <para>🔴 <b>Nothing here may block.</b> These calls arrive on the IPC thread and every window runs
/// its own message pump, so the framework marshals with a non-blocking <c>BeginInvoke</c> — a
/// blocking <c>Invoke</c> from here would deadlock the UI.</para>
///
/// <para><b>These windows keep their native frame</b> (D55 §b); <see cref="SecondaryForm"/> carries
/// the reason.</para>
/// </remarks>
public sealed class SecondaryWindowHost(
    IMessageDispatcher dispatcher,
    IEventBus events,
    WebViewEnvironmentOptions environment,
    ShenoraPaths paths,
    SecondaryWindows windows) : ISecondaryWindows, IDisposable
{
    /// <summary>
    /// Every name asked for so far. <see cref="SecondaryWindows"/> answers whether a name is open but
    /// does not enumerate, so the names have to be remembered to be reported — and one whose window
    /// has since been closed simply drops out of <see cref="Opened"/>.
    /// </summary>
    private readonly HashSet<string> _asked = new(StringComparer.Ordinal);

    public bool Open(string name, string address)
    {
        lock (_asked) _asked.Add(name);

        return windows.Open(name, new SecondaryWindowOptions
        {
            // 🔴 Runs ON the new window's STA thread. Create it, do not show it: the pump shows it
            // once the geometry has been applied.
            CreateForm = () => new SecondaryForm(name, address, dispatcher, events, environment),
            StateStore = new JsonFileWindowStateStore(
                Path.Combine(paths.DataArea("config"), "windows", SecondaryWindow.StateFile(name))),
            // 🔴 `MinWidth`/`MinHeight` here are ALSO applied as the form's DPI-scaled
            // `MinimumSize`, so they outrank anything the form sets for itself. A utility window
            // pinned to a main window's floor cannot be parked narrow beside something else on a
            // second screen, which is most of what a monitor is for.
            StateOptions = new WindowStateOptions
            {
                DefaultWidth = 1100,
                DefaultHeight = 760,
                MinWidth = 520,
                MinHeight = 360,
            },
        });
    }

    public IReadOnlyList<string> Opened
    {
        get
        {
            lock (_asked) return [.. _asked.Where(windows.HasWindow).Order(StringComparer.Ordinal)];
        }
    }

    public void Dispose()
    {
        // Bounded, and it WAITS: the window threads are background, so an unwaited dispose kills
        // them before their FormClosed-driven geometry saves run.
        windows.Dispose();
    }
}
