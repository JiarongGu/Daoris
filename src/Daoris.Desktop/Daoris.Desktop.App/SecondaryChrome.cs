using Microsoft.Extensions.Logging;
using Shenora.Chromium;
using Shenora.Windows;

namespace Daoris.Desktop;

/// <summary>
/// What Daoris wears on a secondary window the kit's host made: the theme its page is in, and for another main window its
/// workspace's count on the button and in its title, its identity, and its page reloaded. Made on the window's own thread by the host's
/// <c>Created</c> hook, once the form exists and before it is shown.
/// </summary>
/// <remarks>
/// <para><b>Native chrome, deliberately, for a utility window.</b> The main window is frameless because its app strip IS the title
/// bar; a monitor or a detached session is not: it is a utility, and OS chrome is what a utility should wear. Another main window is
/// frameless as the main one is, with the caption buttons it paints (<see cref="SecondaryWindowHost"/> describes it).</para>
///
/// <para><b>The theme is the page's to say.</b> The kit's window commands route a second window's <c>SET_THEME</c> nowhere, so its
/// page tells this window by name over <c>DAORIS.WINDOWS</c> (<see cref="FollowPage"/>); until it does, the viewer's held choice or
/// the OS's at <i>System</i>, and a person who switches their OS theme before the page is up still gets a matching title bar.
/// <c>ApplyChromeTheme</c> reaches the caption of a frameless form alone, so a framed window's is set here.</para>
/// </remarks>
internal sealed class SecondaryChrome : IReloadableWindow
{
    private readonly OptimizedForm _form;
    private readonly bool _main;
    private readonly ChromiumEngine _engine;
    private readonly ILogger<ChromiumView> _viewLog;
    private readonly TaskbarOverlay? _badge;
    private readonly Microsoft.Win32.UserPreferenceChangedEventHandler _themeChanged;
    private ChromiumView? _view;
    private volatile bool _failed;
    private readonly PageThemeOrder _theme;
    private int _waitingCount;
    private string? _workspace;

    /// <param name="startsDark">
    /// The theme to wear until the page says its own: the viewer's held choice, or the OS's at <i>System</i>, asked each time
    /// (<see cref="KeptScheme.StartsDark"/>).
    /// </param>
    public SecondaryChrome(OptimizedForm form, string name, Func<bool> startsDark, ChromiumEngine engine, ILogger<ChromiumView> viewLog)
    {
        _form = form;
        Name = name;
        _main = SecondaryWindow.IsMain(name);
        _engine = engine;
        _viewLog = viewLog;
        _view = form.Controls.OfType<ChromiumView>().SingleOrDefault();
        _theme = new PageThemeOrder(startsDark, work => form.Post(work), Theme);

        // The main window's taskbar identity, or this would be a button beside the main one's, grouped by the executable it no longer is.
        TaskbarWear.Wear(form);

        if (_main)
        {
            form.MinimumSize = new Size(960, 600);
            var palette = ChromePalette.For(startsDark());
            form.CaptionButtonColors = MainForm.CaptionColors(palette);
            // For another main window, its own workspace's count on its own button, which the kit's overlay puts back each time Explorer makes it.
            _badge = new TaskbarOverlay(form).Wear(palette);
        }

        // A person on System who switches their OS theme before the page is up still gets a matching title bar.
        _themeChanged = (_, changed) =>
        {
            if (changed.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
            _theme.SystemChanged();
        };
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += _themeChanged;

        form.HandleCreated += (_, _) => Theme(_theme.Current);
        form.Load += async (_, _) => await WatchBrowserAsync();
        // `SystemEvents` holds a STATIC list, so a window that closed without unsubscribing is kept alive by it, and every window the
        // person ever opened would still be handling theme changes.
        form.FormClosed += (_, _) =>
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= _themeChanged;
            _badge?.Dispose();
        };
    }

    /// <summary>The name the window is opened under: <c>monitor</c>, <c>session:&lt;id&gt;</c> or <c>main:&lt;n&gt;</c>.</summary>
    public string Name { get; }

    /// <summary>
    /// The tray's reload: the page this window shows is opened again, on this window's thread. The kit's browser exposes no reload, so
    /// the view is made again on the same path. False where no page came up: nothing is left to reload.
    /// </summary>
    public bool ReloadPage()
    {
        if (_failed) return false;
        return _form.Post(() =>
        {
            if (_view is not { } old || _failed) return;
            var fresh = _view = new ChromiumView(_engine, _viewLog) { Name = old.Name, Dock = DockStyle.Fill, Path = old.Path };
            _form.Controls.Remove(old);
            old.Dispose();
            _form.Controls.Add(fresh);
        });
    }

    /// <summary>
    /// Wear the count this window's own page told on its button and in its title, or none at none. From any thread; a
    /// window that is not a main window wears nothing.
    /// </summary>
    public void ShowWaiting(int count, string? description)
    {
        if (_badge is null) return;
        _form.Post(() =>
        {
            _waitingCount = count;
            _form.Text = WaitingBadge.Title(count, _workspace);
            _badge.ShowWaiting(count, description);
        });
    }

    /// <summary>
    /// The workspace this main window's page holds, worn in its title after the application's name. From any thread; a window that is
    /// not a main window keeps its own name.
    /// </summary>
    public void ShowWorkspace(string? workspace)
    {
        if (!_main) return;
        _form.Post(() =>
        {
            _workspace = workspace;
            _form.Text = WaitingBadge.Title(_waitingCount, workspace);
        });
    }

    /// <summary>The theme the page in this window is in, the viewer's choice where there is one. Called on this window's thread.</summary>
    public void FollowPage(bool dark)
    {
        _theme.PageSaid(dark);
    }

    /// <summary>Paint the theme on this window's own frame: the background under the page, and the caption and border the OS draws.</summary>
    private void Theme(bool dark)
    {
        var palette = ChromePalette.For(dark);
        _form.ApplyChromeTheme(palette.Page, palette.Line, dark);
        _form.ForeColor = palette.Ink;
        if (_main)
        {
            // Frameless: the kit's theme reaches its caption, and the buttons are this window's to paint, as the main one's are.
            _form.CaptionButtonColors = MainForm.CaptionColors(palette);
            _badge?.Wear(palette);
            return;
        }

        if (!_form.IsHandleCreated) return;
        var flag = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(_form.Handle, DwmImmersiveDarkMode, ref flag, sizeof(int));
        var border = palette.Line.R | (palette.Line.G << 8) | (palette.Line.B << 16);
        _ = DwmSetWindowAttribute(_form.Handle, DwmBorderColor, ref border, sizeof(int));
        // A caption already on screen keeps its old colours until the next activation otherwise.
        _ = SetWindowPos(_form.Handle, IntPtr.Zero, 0, 0, 0, 0, FrameChanged);
    }

    /// <summary>Whether the page's browser came up, which the kit's window says in a sentence of its own where it did not.</summary>
    private async Task WatchBrowserAsync()
    {
        try
        {
            if (_view?.Browser is { } browser) await browser.Created;
        }
        catch (Exception)
        {
            _failed = true;
        }
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
}
