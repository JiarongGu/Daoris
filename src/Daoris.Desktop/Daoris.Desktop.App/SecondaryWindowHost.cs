using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using Shenora.Core.Shell;
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
/// the reason. Another main window, opened for a later launch (ONEINSTANCE1, D171 point 2), wears the
/// main window's own chrome instead.</para>
/// </remarks>
public sealed class SecondaryWindowHost(
    Shenora.Chromium.ChromiumEngine engine,
    ShenoraPaths paths,
    SecondaryWindows windows,
    IColorScheme colorScheme,
    Microsoft.Extensions.Logging.ILogger<ChromiumView>? log = null,
    // Each window's showing and closing, by its name, so the last main window ends the application (MAINWINDOWS2).
    MainWindows? mains = null,
    // The taskbar's count, worn by each other main window while the first is put away (MAINWINDOWS2b).
    WaitingCount? waiting = null) : ISecondaryWindows, IDisposable
{
    private bool _hooked;

    /// <summary>
    /// Every name asked for so far. <see cref="SecondaryWindows"/> answers whether a name is open but
    /// does not enumerate, so the names have to be remembered to be reported — and one whose window
    /// has since been closed simply drops out of <see cref="Opened"/>.
    /// </summary>
    private readonly HashSet<string> _asked = new(StringComparer.Ordinal);

    /// <summary>
    /// Each open window's form, by name, so its page can tell it the theme (WINDOW2). Removed as the
    /// form closes, so a closed window is never told anything.
    /// </summary>
    private readonly Dictionary<string, SecondaryForm> _forms = new(StringComparer.Ordinal);

    /// <summary>
    /// Tell each other main window what to show of the taskbar's count (MAINWINDOWS2b): the count while the first is put away and
    /// its button is gone, nothing while it is shown. Any thread; each window draws on its own.
    /// </summary>
    public void ShowWaiting()
    {
        if (waiting is null) return;
        SecondaryForm[] forms;
        lock (_forms) forms = [.. _forms.Where(entry => SecondaryWindow.IsMain(entry.Key)).Select(entry => entry.Value)];
        foreach (var form in forms)
        {
            var (count, description) = waiting.For(form.WindowName);
            form.ShowWaiting(count, description);
        }
    }

    public bool Open(string name, string address)
    {
        lock (_asked)
        {
            _asked.Add(name);
            // The first going away or coming back moves the count between buttons, so each other main window is told again.
            if (mains is not null && waiting is not null && !_hooked)
            {
                _hooked = true;
                mains.AwayChanged += ShowWaiting;
            }
        }

        return windows.Open(name, new SecondaryWindowOptions
        {
            // 🔴 Runs ON the new window's STA thread. Create it, do not show it: the pump shows it
            // once the geometry has been applied.
            CreateForm = () =>
            {
                // Until its page speaks, the viewer's choice as the app's scheme holds it now (THEMESTART1).
                var form = new SecondaryForm(
                    name, address, engine, log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ChromiumView>.Instance,
                    () => KeptScheme.StartsDark(colorScheme.Scheme, MainForm.OperatingSystemPrefersDark));
                lock (_forms) _forms[name] = form;
                // Counted once it is on the screen, since only a window that loaded is ever told closed: one whose making
                // failed would otherwise keep a put-away first main window hidden with nothing else open (MAINWINDOWS2).
                form.Load += (_, _) =>
                {
                    mains?.Opened(name);
                    // A window opened while the first is put away wears the count at once (MAINWINDOWS2b).
                    ShowWaiting();
                };
                form.FormClosed += (_, _) =>
                {
                    lock (_forms)
                    {
                        if (_forms.TryGetValue(name, out var held) && ReferenceEquals(held, form)) _forms.Remove(name);
                    }

                    // The last main window closing, the first put away, ends the application (MAINWINDOWS2).
                    mains?.Closed(name);
                };
                return form;
            },
            StateStore = new JsonFileWindowStateStore(
                Path.Combine(paths.DataArea("config"), "windows", SecondaryWindow.StateFile(name))),
            // 🔴 `MinWidth`/`MinHeight` here are ALSO applied as the form's DPI-scaled
            // `MinimumSize`, so they outrank anything the form sets for itself. A utility window
            // pinned to a main window's floor cannot be parked narrow beside something else on a
            // second screen, which is most of what a monitor is for. Another main window
            // (ONEINSTANCE1) carries the whole platform, and keeps the main window's floor.
            StateOptions = SecondaryWindow.IsMain(name)
                ? new WindowStateOptions { DefaultWidth = 1280, DefaultHeight = 800, MinWidth = 960, MinHeight = 600 }
                : new WindowStateOptions
                {
                    DefaultWidth = 1100,
                    DefaultHeight = 760,
                    MinWidth = 520,
                    MinHeight = 360,
                },
        });
    }

    public bool SetTheme(string name, bool dark)
    {
        SecondaryForm? form;
        lock (_forms) _forms.TryGetValue(name, out form);
        if (form is null || form.IsDisposed || !form.IsHandleCreated) return false;

        // 🔴 BeginInvoke, never Invoke: this arrives on the IPC thread, and the window runs its own
        // pump — a blocking marshal is the deadlock this class's remarks warn about.
        form.BeginInvoke(() => form.FollowPage(dark));
        return true;
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
