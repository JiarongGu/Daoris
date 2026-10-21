using Microsoft.Extensions.Logging;
using Shenora.Chromium;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// A secondary window (D55 §b, SURF8): the same platform bundle, at its own route, in a window of
/// its own — the monitor on a second screen, or one session detached; or the whole platform again,
/// another main window a later launch opened (ONEINSTANCE1, D171 point 2).
/// </summary>
/// <remarks>
/// <para><b>Native chrome, deliberately, for a utility window.</b> The main window is frameless because its
/// app strip IS the title bar (SURF7); a monitor or a detached session is not: it is a utility, and OS chrome
/// is what a utility should wear (D55 §b). Another main window is frameless as the main one is
/// (<see cref="Options"/> has why it can be).</para>
///
/// <para><b>Its page on the same engine, the same dispatcher and the same bus</b> (D92): a
/// <c>ChromiumView</c> bridges its page's IPC itself, on this window's own thread. Requests go to the same
/// modules the main window talks to, and every event emitted on the bus reaches every page — which is
/// how a second window watches the same live console without the driver keeping a second buffer (SES1:
/// the buffer holds no cursor, so a reader is a position and not a registration). Its page's window
/// commands act on this window, not the main one.</para>
/// </remarks>
public sealed class SecondaryForm : OptimizedForm
{
    private readonly ChromiumView _view;
    private readonly Microsoft.Win32.UserPreferenceChangedEventHandler _themeChanged;
    private readonly Func<bool> _startsDark;
    private readonly bool _main;
    private bool _pageSaid;
    private bool _pageDark;
    private readonly TaskbarBadge? _badge;

    /// <summary>The name this window was opened under: <c>monitor</c>, <c>session:&lt;id&gt;</c> or <c>main:&lt;n&gt;</c>.</summary>
    public string WindowName { get; }

    /// <param name="startsDark">
    /// The theme to wear until the page says its own: the viewer's held choice, or the OS's at <i>System</i>, asked each
    /// time (THEMESTART1, <see cref="KeptScheme.StartsDark"/>).
    /// </param>
    public SecondaryForm(
        string name,
        string page,
        ChromiumEngine engine,
        ILogger<ChromiumView> log,
        Func<bool> startsDark)
        : base(Options(SecondaryWindow.IsMain(name), startsDark()))
    {
        _startsDark = startsDark;
        _main = SecondaryWindow.IsMain(name);
        // The taskbar's label and the window's own caption — a utility window HAS a caption, so it is read. Another main
        // window is named as the main one is, with no count until its own page tells it its workspace's (NOTIFY2c).
        Text = name == SecondaryWindow.Monitor ? "Daoris — Monitor" : _main ? WaitingBadge.Title(0) : $"Daoris — {name}";
        StartPosition = FormStartPosition.CenterScreen;
        if (_main)
        {
            if (MainForm.Face() is { } face) Icon = face;
            MinimumSize = new Size(960, 600);
            CaptionButtonColors = MainForm.CaptionColors(ChromePalette.For(startsDark()));
        }

        // The main window's taskbar identity (TASKBAR1, D108), or this window would be a button of its
        // own beside the main one's, grouped by the executable the main one no longer is.
        TaskbarWindow.Wear(this);
        WindowName = name;

        // And, for another main window, its own workspace's count on its own button (NOTIFY2c): the host tells it what its page
        // told (`SecondaryWindowHost.ShowWaiting`), and the button's overlay is put back each time Explorer makes the button, as
        // the main window's is.
        if (_main)
        {
            _badge = new TaskbarBadge(this);
            _badge.Wear(ChromePalette.For(startsDark()));
            WndProcHook = message =>
            {
                if (TaskbarBadge.ButtonCreated != 0 && message.Msg == TaskbarBadge.ButtonCreated) _badge.Apply();
                return null;
            };
        }

        // The viewer's held choice, or the OS theme at System, until the page says which one it is in (WINDOW2:
        // `FollowPage`, over `DAORIS.WINDOWS`; THEMESTART1). Shenora's window commands route a second window's
        // `SET_THEME` nowhere, so the page tells this form by its name instead. Before it has spoken — the moments
        // while the page comes up — that is the best guess, and a person on System who switches their OS theme
        // with the page not yet up still gets a matching title bar.
        _themeChanged = (_, changed) =>
        {
            if (changed.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
            // Once the page has said, it decides: it pushes again when the OS changes and its choice
            // is the system's, and an explicit choice must not be undone by the OS turning.
            if (_pageSaid) return;
            // BeginInvoke, never Invoke: this arrives on the system-events thread and every window
            // here runs its own pump — a blocking marshal is the deadlock the framework warns about.
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() => Theme(_startsDark()));
        };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += _themeChanged;

        // The same bytes the main window shows, with one query parameter saying which window this is —
        // a route into one bundle, never a second frontend (D38's one UI). Its browser opens as the
        // view's handle is created, on this window's own thread.
        _view = new ChromiumView(engine, log) { Dock = DockStyle.Fill, Path = page };
        Controls.Add(_view);

        Load += async (_, _) => await BringUpAsync();
    }

    /// <summary>
    /// How this window is made. A utility window is framed, which is the whole point (D55 §b) — but the DWM dark-mode flag
    /// and the border colour are still ours to set, and a light title bar over a dark page is the most visible thing in the
    /// window. Another main window (ONEINSTANCE1, D171 point 2) carries the whole platform, whose app strip IS the title bar
    /// (SURF7, D56), so it is frameless with the caption buttons it paints, as the main window is: the kit's window commands
    /// act on the window whose page sent them, its own maximize and caption buttons included, and only the theme has no route
    /// there, which its page says by name over <c>DAORIS.WINDOWS</c> as a utility window's does.
    /// </summary>
    private static OptimizedFormOptions Options(bool main, bool dark) => new()
    {
        FramelessChrome = main,
        NativeCaptionButtons = main,
        BackColor = ChromePalette.For(dark).Page,
        DwmBorderColor = ChromePalette.For(dark).Line,
        ImmersiveDarkMode = dark,
    };

    /// <summary>
    /// Wear the count this window's own page told on its button and in its title (NOTIFY2c), or none at none. From any thread; a
    /// window that is not a main window wears nothing.
    /// </summary>
    public void ShowWaiting(int count, string? description)
    {
        if (_badge is null || IsDisposed) return;
        if (InvokeRequired)
        {
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke(() => ShowWaiting(count, description));
            }
            catch (InvalidOperationException)
            {
                // The handle went between the look and the post.
            }

            return;
        }

        _waitingCount = count;
        Text = WaitingBadge.Title(count, _workspace);
        _badge.Show(count, description);
    }

    private int _waitingCount;
    private string? _workspace;

    /// <summary>
    /// The workspace this main window's page holds, worn in its title after the application's name (MAINWINDOWS3b). From any
    /// thread; a window that is not a main window keeps its own name.
    /// </summary>
    public void ShowWorkspace(string? workspace)
    {
        if (!_main || IsDisposed) return;
        if (InvokeRequired)
        {
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke(() => ShowWorkspace(workspace));
            }
            catch (InvalidOperationException)
            {
                // The handle went between the look and the post.
            }

            return;
        }

        _workspace = workspace;
        Text = WaitingBadge.Title(_waitingCount, workspace);
    }

    /// <summary>
    /// The theme the page in this window is in (WINDOW2) — the viewer's choice where there is one —
    /// painted on the caption and border the page cannot reach. Called on this window's thread.
    /// </summary>
    public void FollowPage(bool dark)
    {
        _pageSaid = true;
        _pageDark = dark;
        Theme(dark);
    }

    /// <summary>
    /// Paint the theme on this window's own frame: the background under the page, and the caption and
    /// border the OS draws.
    /// </summary>
    /// <remarks>
    /// 🔴 <c>OptimizedForm.ApplyChromeTheme</c> sets the DWM caption only for a FRAMELESS form (Shenora
    /// 0.16 and 0.17 alike: <c>if (_options.FramelessChrome &amp;&amp; IsHandleCreated)</c>), and this one is
    /// framed, so its caption never followed any theme — not the OS's either, whatever the handler
    /// above intended. Found by looking at WINDOW2's fix, which set a theme nothing painted. So the
    /// attribute is set here, on this window's own handle, and the frame is told to repaint: a caption
    /// already on screen keeps its old colours until the next activation otherwise.
    /// </remarks>
    private void Theme(bool dark)
    {
        var palette = ChromePalette.For(dark);
        ApplyChromeTheme(palette.Page, palette.Line, dark);
        if (_main)
        {
            // Frameless: the kit's theme reaches its caption, and the buttons are this window's to paint, as the main one's are.
            CaptionButtonColors = MainForm.CaptionColors(palette);
            _badge?.Wear(palette);
            return;
        }

        if (!IsHandleCreated) return;
        var flag = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(Handle, DwmImmersiveDarkMode, ref flag, sizeof(int));
        var border = palette.Line.R | (palette.Line.G << 8) | (palette.Line.B << 16);
        _ = DwmSetWindowAttribute(Handle, DwmBorderColor, ref border, sizeof(int));
        _ = SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, FrameChanged);
    }

    /// <summary>The held choice's theme or the OS's on arrival, painted as soon as there is a window to paint.</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme(_pageSaid ? _pageDark : _startsDark());
    }

    private const int DwmImmersiveDarkMode = 20;
    private const int DwmBorderColor = 34;
    /// <summary><c>SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED</c>.</summary>
    private const uint FrameChanged = 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    /// <summary>
    /// Unhook the system-events handler. <c>SystemEvents</c> holds a STATIC list, so a window that
    /// closed without unsubscribing is kept alive by it — and every monitor the person ever opened
    /// would still be handling theme changes.
    /// </summary>
    /// <remarks>
    /// 🔴 A closed window's page must stop hearing the bus (REV3: a WebView2 bridge left attached queued
    /// every later event, up to its cap, once per window ever opened). The view closes its browser, and
    /// its page's IPC with it, as its handle goes; disposing it here makes that happen with the window.
    /// </remarks>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= _themeChanged;
        _view.Dispose();
        _badge?.Dispose();
        base.OnFormClosed(e);
    }

    private async Task BringUpAsync()
    {
        try
        {
            if (_view.Browser is { } browser) await browser.Created;
        }
        catch (Exception error)
        {
            // Never a silent dark window. This one is a utility, so the failure is a sentence in it
            // rather than a dialog: closing it is the whole recovery, and the main window is
            // unaffected either way.
            Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = error.Message,
                ForeColor = ChromePalette.For(_pageSaid ? _pageDark : _startsDark()).Ink,
                Padding = new Padding(24),
                Font = new Font("Segoe UI", 10.5f),
            });
        }
    }
}
