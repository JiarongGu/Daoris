using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shenora.Chromium;
using Shenora.Core.Events;
using Shenora.Core.Ipc;
using Shenora.Core.Shell;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// The one window: the platform the HTTP host serves — the same bytes a browser gets, which is the "one
/// UI, two shells" rule holding (D38) — on the Chromium the install ships (D92, CHR2). A
/// <c>ChromiumView</c> serves the bundle on the engine's app origin and bridges the page's IPC itself;
/// the page reaches the host at its loopback address.
/// </summary>
/// <remarks>
/// <para><b>It is frameless, and the page's app strip IS the title bar</b> (SURF7 / D56). No OS
/// caption: the strip drags the window, double-click maximizes it, and the room it reserves at its
/// right edge is handed to the OS as real caption buttons — which <b>this window paints</b>
/// (<c>NativeCaptionButtons</c>), not the page. D56 as amended has the reason.</para>
///
/// <para><b><see cref="OptimizedForm.AppPlacement"/> is the truth about this window's maximize.</b> Since
/// Shenora 0.22 the frameless maximize is the system's, so <see cref="Form.WindowState"/> reads
/// <c>Maximized</c> too; but a window minimized from maximized reads <c>Minimized</c> there and
/// <c>Maximized</c> in <c>AppPlacement</c>, which is what the kit's window-state stack saves and the IPC
/// module is handed. Un-minimizing with <c>WindowState = Normal</c> brings such a window back
/// un-maximized: <see cref="Unminimize"/> restores it as it was.</para>
/// </remarks>
public sealed class MainForm : OptimizedForm
{
    private readonly ChromiumEngine _engine;
    private readonly PlatformBundle _bundle;
    private readonly string _serviceUrl;
    private readonly ILogger<ChromiumView> _viewLog;
    private readonly SplashPanel _splash;
    private readonly DriverLoop _driver;
    private readonly HostSupervisor _supervisor;
    private readonly SessionNotifier _notifier;
    private readonly TaskbarBadge _badge;
    private readonly Daoris.Driver.MachineLog _log;
    // When the splash comes down (SPLASHDATA1): the page's word that its first screen has its data, or the bound.
    private readonly SplashLift _lift;
    // The start, timed stage by stage (STARTFAST1): the window's shown, its page's browser made, and through the lift the rest.
    private readonly StartStages? _stages;
    // Whether a close puts this window away or ends the application (MAINWINDOWS2): the others' count, and a close from code
    // that is for good, since the kit reports one as the person's.
    private readonly MainWindows? _mains;
    private bool _forGood;
    private ChromePalette _palette;
    private Label? _trouble;

    public MainForm(
        ChromiumEngine engine,
        PlatformBundle bundle,
        PlatformAddress platform,
        IMessageDispatcher dispatcher,
        IEventBus eventBus,
        DriverLoop driver,
        HostSupervisor supervisor,
        IColorScheme colorScheme,
        ILogger<ChromiumView>? viewLog = null,
        UiStallWatch? watch = null,
        Daoris.Driver.MachineLog? log = null,
        MainWindows? mains = null,
        PressFlash? flash = null,
        MainWindowScopes? scopes = null,
        BringForward? forward = null,
        NoticeWords? notices = null,
        StartStages? stages = null)
        : base(new OptimizedFormOptions
        {
            FramelessChrome = true,
            // The window owns the caption pixels and paints them (D56 as amended by SURF7's reading
            // of the framework). Inert until the page reports where it left the room.
            NativeCaptionButtons = true,
            BackColor = StartingPalette(colorScheme).Page,
            // The 1px Win11 border line. The system default is light, which is visibly wrong on the
            // dark theme and slightly wrong on the light one.
            DwmBorderColor = StartingPalette(colorScheme).Line,
            ImmersiveDarkMode = StartingPalette(colorScheme).IsDark,
        })
    {
        _engine = engine;
        _bundle = bundle;
        _serviceUrl = platform.Url;
        _viewLog = viewLog ?? NullLogger<ChromiumView>.Instance;
        _driver = driver;
        _supervisor = supervisor;
        _log = log ?? Daoris.Driver.MachineLog.None;
        _mains = mains;
        _palette = StartingPalette(colorScheme);
        _stages = stages;
        _lift = new SplashLift(stages);

        // Still set: it is the taskbar's label and the caption of the one MessageBox left. While anything
        // waits on the person it leads with the count (NOTIFY1b, ShowWaiting).
        Text = WaitingBadge.Title(0);

        // One taskbar button for a pinned Daoris (TASKBAR1, D108): an install's window names Daoris's
        // id and a relaunch command that starts the root launcher, so it groups under the person's pin
        // and a pin made from it survives a republish. A workspace build wears nothing.
        TaskbarWindow.Wear(this);

        // And on that button, how much waits on the person (NOTIFY1b): put back each time Explorer makes the
        // button, which it says with a message of its own, so a restarted taskbar still counts.
        _badge = new TaskbarBadge(this);
        _badge.Wear(_palette);
        WndProcHook = message =>
        {
            if (TaskbarBadge.ButtonCreated != 0 && message.Msg == TaskbarBadge.ButtonCreated) _badge.Apply();
            return null;
        };

        // It also fixes the tray: `SessionNotifier` reads `window.Icon ?? SystemIcons.Application`,
        // so every balloon this app raised wore a generic Windows glyph until now.
        if (Face() is { } face) Icon = face;
        MinimumSize = new Size(960, 600);
        CaptionButtonColors = CaptionColors(_palette);

        // The view comes once the host answers (BringUpAsync); the splash holds the window till then, and on until the
        // page's first screen has its data (SPLASHDATA1).
        _splash = new SplashPanel(new SplashPanelOptions { BackColor = _palette.Page });
        Controls.Add(_splash);
        _splash.BringToFront();

        // 🔴 MAPPED LATE, from where the window is created, because this facade needs the LIVE form —
        // never from the dispatcher's configure callback, which runs at provider-build time when no
        // form exists. Late mapping is safe while requests are in flight.
        dispatcher.MapModule(new WindowCommandModule(new WindowCommandOptions
        {
            Window = this,
            // The form's own members, both times: its toggle, and `AppPlacement`, which says maximized of a
            // window minimized from maximized where `Form.WindowState` says minimized.
            ToggleMaximize = ToggleMaximize,
            IsMaximized = () => AppPlacement == WindowPlacement.Maximized,
            ApplyTheme = ApplyTheme,
            // The page's rectangles are read against the view that sent them (ChromiumView), per
            // monitor, so no coordinate space is named here.
            SetCaptionButtons = SetCaptionButtons,
            // The page's CLOSE_SPLASH (SPLASHDATA1): its first screen has its data. Only the main window's page reaches it.
            CloseSplash = _lift.PageSaid,
        }));

        // The OS notification (SURF5b), which closes driver design open question 5: a session that
        // parks while nobody is looking at this window now reaches the person. It decides nothing —
        // `AttentionWatch` in the driver does, so a machine with no screen answers the same question.
        // Its double-click and a balloon's click take this window back where it was put away (MAINWINDOWS2) before showing it.
        // NOTIFY2c: a balloon's click opens the main window whose workspace holds what it was about, as the windows' records hold
        // it, or the main window last in front where none or several do; and only that window's page attends its session.
        // NOTIFY2d: each balloon in the window's language, from the words a main window's page handed over SET_NOTICE_WORDS.
        _notifier = new SessionNotifier(
            this, eventBus, takingBack: () => _mains?.TakeBackFirst(),
            picking: scopes is null ? null : (session, quest) => scopes.PickFor(driver.Slices, session, quest),
            forwarding: forward, words: notices);
        _notifier.Attend += (session, window) => eventBus.Emit(
            new EventMessage { Module = "DAORIS", Type = "ATTEND_SESSION", Payload = new { Session = session, Window = window } });

        Load += async (_, _) => await BringUpAsync();
        Shown += (_, _) => _stages?.Mark(StartStages.Shown);
        // Shown again while put away, by the kit's bringing it forward for a later launch: that launch's window (MAINWINDOWS2).
        VisibleChanged += (_, _) =>
        {
            if (Visible) _mains?.Shown();
        };
        // FABRIC-END6: the window's focus is a moment to ask about an open pull request (§A4.3); the driver bounds it to D148's minute.
        Activated += (_, _) => _driver.Focused();
        // NOTIFY2c: the main window last in front is the one a notification about nobody's workspace opens.
        if (scopes is not null) Activated += (_, _) => scopes.CameForward(MainWindows.First);
        // NOTIFY2b (the notification design §3.3): a terminal's press waiting unshown lights the taskbar button of the main window last
        // in front while none is; this window's coming forward stops it. What waits is told by the pipe, on the thread that changed it.
        if (flash is not null)
        {
            Activated += (_, _) => flash.CameForward(MainWindows.First);
            Deactivate += (_, _) => flash.WentBehind(MainWindows.First);
            var told = eventBus.Subscribe(StatePublisher.Module, PipeConfirmations.Event, message =>
            {
                if (message.Payload is PipeConfirmationsNow now) flash.Told(now);
                return Task.CompletedTask;
            });
            FormClosed += (_, _) => told.Dispose();
        }

        FormClosed += (_, _) => _notifier.Dispose();
        FormClosed += (_, _) => _badge.Dispose();

        // How late this window's thread answers, and what held it (FREEZE1, D56's note): a press on the strip is handed
        // to this thread to move the window, so a late thread is a frozen bar. Looked at from the moment it is shown.
        if (watch is not null)
        {
            Load += (_, _) => watch.Start(Post);
            FormClosed += (_, _) => watch.Dispose();
        }
    }

    /// <summary>
    /// The application ends with its last main window, not this one (MAINWINDOWS2, D171 point 2). The kit's message loop runs
    /// until this form closes, and the pickers, the taskbar's count, the tray and the update are resolved as this window, so
    /// the person's close of it while another main window is open hides it instead, its page and all of that kept, as the
    /// kit's own close-to-tray hides a window and keeps the loop. Cancelled before the base hears it, so nothing of the kit's
    /// reads a close that did not happen. Any other close (the machine's shutdown, the task manager, <see cref="CloseForGood"/>)
    /// ends the application, as one with no other main window does.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!e.Cancel && _mains?.FirstClosing(byPerson: e.CloseReason == CloseReason.UserClosing && !_forGood) == true)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    /// <summary>
    /// Close this window for good, which ends the application, from any thread, never blocking (MAINWINDOWS2): the update's
    /// close, and the last main window's. The kit reports a close from code as the person's, so this says it is not.
    /// </summary>
    public void CloseForGood() => Post(() =>
    {
        _forGood = true;
        Close();
    });

    /// <summary>
    /// Show this window again where it was put away, from any thread, never blocking (MAINWINDOWS2): a later launch's window.
    /// Un-minimized before it is activated, then brought forward on the OS's side too, as the tray's restore does.
    /// </summary>
    public void TakeBack() => Post(() => Forward(this));

    /// <summary>
    /// Bring <paramref name="form"/> in front of the person, on its own thread: the tray's restore, a later launch's window, and a
    /// notification's click (NOTIFY2c, on whichever main window it opens).
    /// </summary>
    /// <remarks>
    /// 🔴 THE ORDER IS THE PART PEOPLE GET WRONG, and the framework's own <c>WindowActivation</c> documents it: un-minimize BEFORE
    /// activating, because activating a minimized window leaves it minimized; then the managed show and activate; then
    /// <c>SetForegroundWindow</c> for the OS side, without which restoring while another app holds the foreground leaves the window
    /// behind everything, visible only in the taskbar. That type is internal to the package, so the sequence is written out here. The
    /// un-minimize restores a window minimized from maximized as maximized (Shenora 0.22), which <c>WindowState = Normal</c> does not.
    /// </remarks>
    internal static void Forward(Form form)
    {
        if (form.IsDisposed) return;
        Unminimize(form);
        form.Show();
        form.Activate();
        form.BringToFront();
        SetForegroundWindow(form.Handle);
    }

    /// <summary>
    /// Un-minimize <paramref name="form"/> to where it was minimized from, maximized if it was (Shenora 0.22): <c>WindowState =
    /// Normal</c> restores the windowed bounds whatever the window was. The kit's own <c>WindowActivation.Unminimize</c> is
    /// internal to it, so the tray's and a notification's restore call this. No-op when it is not minimized.
    /// </summary>
    internal static void Unminimize(Form form)
    {
        if (form.WindowState != FormWindowState.Minimized) return;
        if (form.IsHandleCreated) ShowWindow(form.Handle, SW_RESTORE);
        else form.WindowState = FormWindowState.Normal;
    }

    private const int SW_RESTORE = 9;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    /// <summary>Run <paramref name="action"/> on this window's thread later, or false where there is no window to run it on.</summary>
    private bool Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return false;
        try
        {
            BeginInvoke(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            // The handle went between the look and the post.
            return false;
        }
    }

    /// <summary>
    /// How much waits on the person, as the page counts it for Overview's *What needs you* (NOTIFY1b, <c>SET_BADGE</c>):
    /// the taskbar button's overlay and the title's lead. On this window's thread, posted from the bridge's, which waits
    /// on nothing here.
    /// </summary>
    public void ShowWaiting(int count, string? description)
    {
        if (InvokeRequired)
        {
            Post(() => ShowWaiting(count, description));
            return;
        }

        _waitingCount = count;
        Text = WaitingBadge.Title(count, _workspace);
        _badge.Show(count, description);
    }

    private int _waitingCount;
    private string? _workspace;

    /// <summary>
    /// The workspace this window's page holds, worn in the title after the application's name (MAINWINDOWS3b), so two main
    /// windows on two workspaces read apart in the taskbar and Alt-Tab. On this window's thread, posted from the bridge's.
    /// </summary>
    public void ShowWorkspace(string? workspace)
    {
        if (InvokeRequired)
        {
            Post(() => ShowWorkspace(workspace));
            return;
        }

        _workspace = workspace;
        Text = WaitingBadge.Title(_waitingCount, workspace);
    }

    /// <summary>
    /// The first paint's palette: the viewer's choice as the app's scheme starts at, kept from the last run (THEMESTART1),
    /// and the OS's at <i>System</i>. The page's word, a round trip later, is the one that stands.
    /// </summary>
    private static ChromePalette StartingPalette(IColorScheme colorScheme) =>
        ChromePalette.For(KeptScheme.StartsDark(colorScheme.Scheme, OperatingSystemPrefersDark));

    /// <summary>
    /// The page's effective theme, arriving over <c>SET_THEME</c> on every change.
    /// </summary>
    /// <remarks>
    /// Not optional for an app that follows the OS theme: the DWM border, the form fill and the
    /// caption buttons are painted natively and never see the page's CSS, so without this a runtime
    /// light↔dark switch leaves a window wearing half of each.
    /// </remarks>
    private void ApplyTheme(bool dark)
    {
        _palette = ChromePalette.For(dark);
        CaptionButtonColors = CaptionColors(_palette);
        ApplyChromeTheme(_palette.Page, _palette.Line, immersiveDarkMode: dark);

        if (!_splash.IsDisposed) _splash.BackColor = _palette.Page;
        _badge.Wear(_palette);
        if (_trouble is { IsDisposed: false })
        {
            _trouble.BackColor = _palette.Page;
            _trouble.ForeColor = _palette.Ink;
        }
    }

    /// <summary>
    /// 🔴 The WINDOW's icon, which a WinForms form does not take from anywhere by itself: it opens wearing the framework's
    /// default unless told. Read from the `daoris.ico` the build copies beside this assembly (D92): the running executable is
    /// CEF's launcher now, and wears CEF's face, so the old reading of it would put Chromium's picture on the taskbar. Null
    /// where it cannot be read: a face is not worth failing to open a window over, and the default is still a face.
    /// </summary>
    internal static Icon? Face()
    {
        try
        {
            var face = Path.Combine(AppContext.BaseDirectory, "daoris.ico");
            return File.Exists(face) ? new Icon(face) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The caption buttons this window paints, in a palette's colours: another main window wears the same (ONEINSTANCE1).</summary>
    internal static CaptionButtonColors CaptionColors(ChromePalette palette) => new()
    {
        Surface = palette.Surface,
        Hover = palette.Hover,
        Pressed = palette.Pressed,
        Glyph = palette.Ink,
        CloseHover = palette.CloseHover,
        ClosePressed = palette.ClosePressed,
        CloseGlyphHot = palette.CloseGlyphHot,
    };

    /// <summary>
    /// Whether the OS is in dark mode. Only for the first paint at <i>System</i>: the page is authoritative and says
    /// so within a round trip.
    /// </summary>
    /// <remarks>
    /// Dark is the fallback when the value is missing or the hive is unreadable — it was this shell's
    /// fill before any of this, and a light flash on a dark desktop is the more jarring mistake.
    /// </remarks>
    internal static bool OperatingSystemPrefersDark()
    {
        try
        {
            var value = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                null);

            return value is not int light || light == 0;
        }
        catch
        {
            // A policy-locked hive is not a reason to fail to open a window.
            return true;
        }
    }

    private async Task BringUpAsync()
    {
        try
        {
            // Never a silent empty window: the page is the bundle beside the host, so a bundle that is
            // not there is said before anything opens, with the likely cause named.
            if (!_bundle.Present)
            {
                ShowTrouble(
                    "The platform's page was not found beside the service host"
                    + (_bundle.Folder is { } folder ? $" ({folder})" : "") + ".\n\n"
                    + "Build it (npm --prefix src/Daoris.Web run build), or reinstall.");
                return;
            }

            // The page comes once the service answers: every call it makes is to that host, and a page
            // opened on a port nobody holds reads as "the app is broken" rather than "starting".
            if (!await _driver.HostReady)
            {
                ShowTrouble(_supervisor.Trouble ?? "The local service host did not come up.");
                return;
            }

            // The page on the engine's app origin, told where its host is (D92). Its browser opens as
            // the view's handle is created, which adding it to this shown window does.
            var view = new ChromiumView(_engine, _viewLog)
            {
                Dock = DockStyle.Fill,
                Path = DesktopPage.PathFor(_serviceUrl),
            };
            Controls.Add(view);
            _splash.BringToFront();

            if (view.Browser is { } browser) await browser.Created;
            _stages?.Mark(StartStages.Browser);

            // Down once the page says its first screen has its data (SPLASHDATA1), not as its browser is made, which
            // lifted it onto an empty frame; at the bound, said in the log, when the page never says.
            await _lift.WaitAsync(_log, SplashLift.Bound);
            if (!_splash.IsDisposed)
            {
                Controls.Remove(_splash);
                _splash.Dispose();
            }
        }
        catch (Exception error)
        {
            ShowTrouble(error.Message);
        }
    }

    /// <summary>
    /// Trouble is a page, never a modal. A MessageBox owned by this window DISABLES it — measured:
    /// the close button stops working, the app cannot be exited until someone finds the dialog, and
    /// posting WM_CLOSE reports success while doing nothing. The WebView2 runtime check, the one modal
    /// once kept, went with WebView2 (D92): the engine is the install's own.
    /// </summary>
    private void ShowTrouble(string message)
    {
        if (!_splash.IsDisposed)
        {
            Controls.Remove(_splash);
            _splash.Dispose();
        }

        _trouble = new Label
        {
            Dock = DockStyle.Fill,
            Text = message,
            // Text as it was said, the host's own lines among it (HOSTSTART1): a label reads `&` as a mnemonic marker,
            // and would drop or underline what the host printed.
            UseMnemonic = false,
            ForeColor = _palette.Ink,
            BackColor = _palette.Page,
            // Clear of the strip: the window has no title bar, so text docked to the top would start
            // under the caption buttons the window paints there.
            Padding = new Padding(28, 56, 28, 28),
            Font = new Font("Segoe UI", 10.5f),
        };
        Controls.Add(_trouble);
        _trouble.BringToFront();
    }

    // The folder and file pickers are `Pickers`', owned by the window whose page asked (MAINWINDOWS2c), not this one's alone.
}
