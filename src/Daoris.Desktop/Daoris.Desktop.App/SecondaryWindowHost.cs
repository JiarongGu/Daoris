using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using Shenora.Core.Shell;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// The shell's half of <see cref="ISecondaryWindows"/>: named windows, each on its own STA
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
/// <para><b>These windows keep their native frame</b>; <see cref="SecondaryForm"/> carries
/// the reason. Another main window, opened for a later launch, wears the
/// main window's own chrome instead.</para>
/// </remarks>
public sealed class SecondaryWindowHost(
    Shenora.Chromium.ChromiumEngine engine,
    ShenoraPaths paths,
    SecondaryWindows windows,
    IColorScheme colorScheme,
    Microsoft.Extensions.Logging.ILogger<ChromiumView>? log = null,
    // Each window's showing and closing, by its name, so the last main window ends the application.
    MainWindows? mains = null,
    // The count each other main window's own page told, worn on its own button and in its title.
    WaitingCount? waiting = null,
    // The loop each other main window's focus is told to, as the first one's is: a moment to ask about an open pull
    // request, bounded by the driver's minute. The container hands it, as it hands the first window its own.
    DriverLoop? focused = null,
    // Which main window's taskbar button a terminal's press waiting unshown lights: each other main window's focus, told.
    PressFlash? flash = null,
    // Which main window a notification's click opens: each other main window's focus and close, told.
    MainWindowScopes? scopes = null) : ISecondaryWindows, IDisposable
{

    /// <summary>
    /// Every name asked for so far: <see cref="SecondaryWindows"/> answers whether a name is open but does not enumerate, so
    /// the names are remembered to be reported, and one whose window has since closed drops out of <see cref="Opened"/>.
    /// </summary>
    private readonly HashSet<string> _asked = new(StringComparer.Ordinal);

    /// <summary>
    /// Each open window's form, by name, so its page can tell it the theme; removed as it closes, so a closed window is never told anything.
    /// </summary>
    private readonly Dictionary<string, SecondaryForm> _forms = new(StringComparer.Ordinal);

    /// <summary>
    /// The other main windows now open, for the picker's owner where the page that asked has none: any thread.
    /// </summary>
    public IReadOnlyList<Form> MainForms()
    {
        lock (_forms) return [.. _forms.Where(entry => SecondaryWindow.IsMain(entry.Key)).OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (Form)entry.Value)];
    }

    /// <summary>Every window open now, main or not, for the tray's reload: any thread.</summary>
    public IReadOnlyList<IReloadableWindow> Reloadable()
    {
        lock (_forms) return [.. _forms.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (IReloadableWindow)entry.Value)];
    }

    /// <summary>The open window opened under <paramref name="name"/> (a picker's owner), or null: any thread.</summary>
    public Form? FormNamed(string name)
    {
        lock (_forms) return _forms.TryGetValue(name, out var form) ? form : null;
    }

    /// <summary>
    /// The main window opened under <paramref name="name"/> wears the count its own page last told, on its own button and
    /// in its title. Any thread; the window draws on its own.
    /// </summary>
    public void ShowWaiting(string name)
    {
        if (waiting is null || !SecondaryWindow.IsMain(name)) return;
        SecondaryForm? form;
        lock (_forms) _forms.TryGetValue(name, out form);
        if (form is null) return;
        var (count, description) = waiting.For(name);
        form.ShowWaiting(count, description);
    }

    /// <summary>
    /// Bring the main window opened under <paramref name="name"/> in front of the person: a notification's click about what
    /// its workspace holds. Un-minimized before it is activated, then brought forward on the OS's side too, on its own thread. False
    /// where it is not open. Any thread, never blocking.
    /// </summary>
    public bool BringForward(string name)
    {
        if (!SecondaryWindow.IsMain(name)) return false;
        SecondaryForm? form;
        lock (_forms) _forms.TryGetValue(name, out form);
        return form is not null && form.Post(() => MainForm.Forward(form));
    }

    /// <summary>The workspace the page of the main window opened under <paramref name="name"/> holds, worn in its title. Any thread.</summary>
    public void ShowWorkspace(string name, string? workspace)
    {
        if (!SecondaryWindow.IsMain(name)) return;
        SecondaryForm? form;
        lock (_forms) _forms.TryGetValue(name, out form);
        form?.ShowWorkspace(workspace);
    }

    public bool Open(string name, string address)
    {
        lock (_asked) _asked.Add(name);

        return windows.Open(name, new SecondaryWindowOptions
        {
            // 🔴 Runs ON the new window's STA thread. Create it, do not show it: the pump shows it
            // once the geometry has been applied.
            CreateForm = () =>
            {
                // Until its page speaks, the viewer's choice as the app's scheme holds it now.
                var form = new SecondaryForm(
                    name, address, engine, log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ChromiumView>.Instance,
                    () => KeptScheme.StartsDark(colorScheme.Scheme, MainForm.OperatingSystemPrefersDark));
                lock (_forms) _forms[name] = form;
                // Another main window's focus is the person looking at the platform, as the first one's is; a utility
                // window's (the monitor, a detached session) is not. A hint that returns at once, so this window's thread waits on nothing.
                if (focused is not null && SecondaryWindow.IsMain(name)) form.Activated += (_, _) => focused.Focused();
                // Another main window coming forward is the person at a main window, which stops a lit button and is the
                // one lit next; going behind leaves none in front. A utility window's focus moves nothing here.
                if (flash is not null && SecondaryWindow.IsMain(name))
                {
                    form.Activated += (_, _) => flash.CameForward(name);
                    form.Deactivate += (_, _) => flash.WentBehind(name);
                }
                // The main window last in front is the one a notification about nobody's workspace opens.
                if (scopes is not null && SecondaryWindow.IsMain(name)) form.Activated += (_, _) => scopes.CameForward(name);
                // Counted once on the screen: only a window that loaded is told closed, and one that failed would keep a put-away first main window hidden.
                form.Load += (_, _) =>
                {
                    mains?.Opened(name);
                    // What its page told before the window came up, worn at once.
                    ShowWaiting(name);
                };
                form.FormClosed += (_, _) =>
                {
                    lock (_forms)
                    {
                        if (_forms.TryGetValue(name, out var held) && ReferenceEquals(held, form)) _forms.Remove(name);
                    }

                    // Its count and workspace go with it: one opened again under its name holds nothing until its page tells it.
                    waiting?.Closed(name);
                    scopes?.Closed(name);
                    // The last main window closing, the first put away, ends the application.
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
            // carries the whole platform, and keeps the main window's floor.
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
        if (form is null) return false;

        // 🔴 Posted, never Invoked: this arrives on the IPC thread, and the window runs its own
        // pump — a blocking marshal is the deadlock this class's remarks warn about.
        return form.Post(() => form.FollowPage(dark));
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
