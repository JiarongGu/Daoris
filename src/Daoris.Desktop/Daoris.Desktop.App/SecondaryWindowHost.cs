using Shenora;
using Shenora.Core.Shell;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// The shell's half of <see cref="ISecondaryWindows"/>: named windows carrying the same platform bundle at their own routes, which
/// the kit's <see cref="ChromiumWindowHost"/> opens, each on its own STA thread. This type keeps only Daoris's: each window's chrome
/// and the routes that reach it.
/// </summary>
/// <remarks>
/// <para><b>One window per name, and opening an open one activates it</b> — the kit's contract, which is what makes "open the monitor"
/// safe to press twice. Geometry is remembered per name by the kit's host.</para>
///
/// <para>🔴 <b>Nothing here may block.</b> These calls arrive on the IPC thread and every window runs its own message pump, so each
/// reaches a window by posting to it — a blocking <c>Invoke</c> from here would deadlock the UI.</para>
///
/// <para><b>These windows keep their native frame</b> (<see cref="SecondaryChrome"/> has the reason). Another main window, opened for a
/// later launch, wears the main window's own chrome instead.</para>
/// </remarks>
public sealed class SecondaryWindowHost : ISecondaryWindows, IDisposable
{
    private readonly ChromiumWindowHost _host;
    private readonly Shenora.Chromium.ChromiumEngine _engine;
    private readonly SecondaryWindows _windows;
    private readonly IColorScheme _colorScheme;
    private readonly Microsoft.Extensions.Logging.ILogger<ChromiumView> _log;
    private readonly MainWindows? _mains;
    private readonly WaitingCount? _waiting;
    private readonly DriverLoop? _focused;
    private readonly PressFlash? _flash;
    private readonly MainWindowScopes? _scopes;

    /// <summary>Each open window's chrome, by name; removed as it closes, so a closed window is never told anything.</summary>
    private readonly Dictionary<string, SecondaryChrome> _chromes = new(StringComparer.Ordinal);

    public SecondaryWindowHost(
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
        MainWindowScopes? scopes = null)
    {
        _engine = engine;
        _windows = windows;
        _colorScheme = colorScheme;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ChromiumView>.Instance;
        _mains = mains;
        _waiting = waiting;
        _focused = focused;
        _flash = flash;
        _scopes = scopes;
        _host = new ChromiumWindowHost(engine, windows, Path.Combine(paths.DataArea("config"), "windows"), Describe, log);
    }

    /// <summary>
    /// Every window open now, main or not, for the tray's reload: any thread.
    /// </summary>
    public IReadOnlyList<IReloadableWindow> Reloadable()
    {
        lock (_chromes) return [.. _chromes.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (IReloadableWindow)entry.Value)];
    }

    /// <summary>The open window opened under <paramref name="name"/> (the window to light), or null: any thread.</summary>
    public Form? FormNamed(string name) => _host.FormNamed(name);

    /// <summary>
    /// The main window opened under <paramref name="name"/> wears the count its own page last told, on its own button and
    /// in its title. Any thread; the window draws on its own.
    /// </summary>
    public void ShowWaiting(string name)
    {
        if (_waiting is null || !SecondaryWindow.IsMain(name) || Chrome(name) is not { } chrome) return;
        var (count, description) = _waiting.For(name);
        chrome.ShowWaiting(count, description);
    }

    /// <summary>
    /// Bring the main window opened under <paramref name="name"/> in front of the person: a notification's click about what
    /// its workspace holds. Un-minimized before it is activated, then brought forward on the OS's side too, on its own thread. False
    /// where it is not open. Any thread, never blocking.
    /// </summary>
    public bool BringForward(string name) => SecondaryWindow.IsMain(name) && _host.Post(name, form => MainForm.Forward(form));

    /// <summary>The workspace the page of the main window opened under <paramref name="name"/> holds, worn in its title. Any thread.</summary>
    public void ShowWorkspace(string name, string? workspace)
    {
        if (SecondaryWindow.IsMain(name)) Chrome(name)?.ShowWorkspace(workspace);
    }

    public bool Open(string name, string address) => _host.Open(name, address);

    public bool SetTheme(string name, bool dark)
    {
        // 🔴 Posted, never Invoked: this arrives on the IPC thread, and the window runs its own pump.
        return Chrome(name) is { } chrome && _host.Post(name, _ => chrome.FollowPage(dark));
    }

    public IReadOnlyList<string> Opened => _host.Opened;

    public void Dispose()
    {
        // Bounded, and it WAITS: the window threads are background, so an unwaited dispose kills
        // them before their FormClosed-driven geometry saves run.
        _windows.Dispose();
    }

    private SecondaryChrome? Chrome(string name)
    {
        lock (_chromes) return _chromes.GetValueOrDefault(name);
    }

    /// <summary>
    /// How the window opened under <paramref name="name"/> is made. A utility window is framed, which is the whole point, but the DWM
    /// dark-mode flag and the border colour are still ours to set, and a light title bar over a dark page is the most visible thing in the
    /// window. Another main window carries the whole platform, whose app strip IS the title bar, so it is frameless with the caption
    /// buttons it paints, as the main window is: the kit's window commands act on the window whose page sent them, its own maximize and
    /// caption buttons included, and only the theme has no route there, which its page says by name over <c>DAORIS.WINDOWS</c> as a
    /// utility window's does.
    /// </summary>
    private ChromiumFormOptions Describe(string name)
    {
        var main = SecondaryWindow.IsMain(name);
        var palette = ChromePalette.For(KeptScheme.StartsDark(_colorScheme.Scheme, MainForm.OperatingSystemPrefersDark));
        return new ChromiumFormOptions
        {
            // The taskbar's label and the window's own caption: a utility window HAS a caption, so it is read. Another main
            // window is named as the main one is, with no count until its own page tells it its workspace's.
            Title = name == SecondaryWindow.Monitor ? "Daoris — Monitor" : main ? WaitingBadge.Title(0) : $"Daoris — {name}",
            Icon = main ? MainForm.Face() : null,
            Chrome = new OptimizedFormOptions
            {
                FramelessChrome = main,
                NativeCaptionButtons = main,
                BackColor = palette.Page,
                DwmBorderColor = palette.Line,
                ImmersiveDarkMode = palette.IsDark,
            },
            // 🔴 `MinWidth`/`MinHeight` here are ALSO applied as the form's DPI-scaled `MinimumSize`, so they outrank anything the form
            // sets for itself. A utility window pinned to a main window's floor cannot be parked narrow beside something else on a
            // second screen, which is most of what a monitor is for. Another main window carries the whole platform, and keeps the
            // main window's floor.
            State = main
                ? new WindowStateOptions { DefaultWidth = 1280, DefaultHeight = 800, MinWidth = 960, MinHeight = 600 }
                : new WindowStateOptions { DefaultWidth = 1100, DefaultHeight = 760, MinWidth = 520, MinHeight = 360 },
            Created = form => Attach(name, form),
        };
    }

    /// <summary>Daoris's wiring of a window the kit made, on that window's own thread.</summary>
    private void Attach(string name, OptimizedForm form)
    {
        // Until its page speaks, the viewer's choice as the app's scheme holds it now.
        var chrome = new SecondaryChrome(
            form, name, () => KeptScheme.StartsDark(_colorScheme.Scheme, MainForm.OperatingSystemPrefersDark), _engine, _log);
        lock (_chromes) _chromes[name] = chrome;
        var main = SecondaryWindow.IsMain(name);

        // Another main window's focus is the person looking at the platform, as the first one's is; a utility
        // window's (the monitor, a detached session) is not. A hint that returns at once, so this window's thread waits on nothing.
        if (_focused is not null && main) form.Activated += (_, _) => _focused.Focused();
        // Another main window coming forward is the person at a main window, which stops a lit button and is the
        // one lit next; going behind leaves none in front. A utility window's focus moves nothing here.
        if (_flash is not null && main)
        {
            form.Activated += (_, _) => _flash.CameForward(name);
            form.Deactivate += (_, _) => _flash.WentBehind(name);
        }

        // The main window last in front is the one a notification about nobody's workspace opens.
        if (_scopes is not null && main) form.Activated += (_, _) => _scopes.CameForward(name);
        // Counted once on the screen: only a window that loaded is told closed, and one that failed would keep a put-away first main window hidden.
        form.Load += (_, _) =>
        {
            _mains?.Opened(name);
            // What its page told before the window came up, worn at once.
            ShowWaiting(name);
        };
        form.FormClosed += (_, _) =>
        {
            lock (_chromes)
            {
                if (_chromes.TryGetValue(name, out var held) && ReferenceEquals(held, chrome)) _chromes.Remove(name);
            }

            // Its count and workspace go with it: one opened again under its name holds nothing until its page tells it.
            _waiting?.Closed(name);
            _scopes?.Closed(name);
            // The last main window closing, the first put away, ends the application.
            _mains?.Closed(name);
        };
    }
}
