using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Daoris.Desktop;

/// <summary>
/// How much waits on the person, drawn on the main window's taskbar button (NOTIFY1b): a number on a circle, the
/// button's overlay icon through the shell's <c>ITaskbarList3</c>, as <see cref="TaskbarWindow"/> writes the button's
/// identity through the window's property store.
/// </summary>
/// <remarks>
/// <para><b>Put back whenever the button is made.</b> An overlay belongs to a taskbar button, not to the window: Explorer
/// makes the button again when it restarts and says so with <c>TaskbarButtonCreated</c>, and a recreated handle gets a
/// button of its own. So the last count is kept and applied on each, and one told before the button exists lands when
/// the message says it does (the window forwards it, <see cref="ButtonCreated"/>).</para>
///
/// <para><b>Drawn here, at the size the shell shows.</b> The kit has no overlay (Shenora.Windows 0.21.1), and the icon
/// is a number, so it is drawn at run time: a small icon at the window's DPI, the platform's red count with the page's
/// close-button red (<c>--st-declined</c>, the palette's copy), the digits centred by their own outline rather than the
/// font's line box. What it says is <see cref="WaitingBadge"/>'s, tested in the modules; the drawing and the shell call
/// have no test, as nothing in this project does.</para>
///
/// <para>A taskbar that cannot be told shows no count; the window works as before. On the window's thread only, the one
/// the shell's object was made on.</para>
/// </remarks>
internal sealed class TaskbarBadge : IDisposable
{
    /// <summary>The message Explorer sends a window once its taskbar button exists, and again after Explorer restarts.</summary>
    public static readonly int ButtonCreated = (int)RegisterWindowMessage("TaskbarButtonCreated");

    private readonly Form _form;

    /// <summary>Badge <paramref name="form"/>'s button, from each handle it is given.</summary>
    public TaskbarBadge(Form form)
    {
        _form = form;
        form.HandleCreated += (_, _) => Apply();
    }

    private ITaskbarList3? _taskbar;
    private bool _unavailable;
    private int _count;
    private string? _description;
    private Color _fill = ChromePalette.Light.CloseHover;
    private Color _ink = ChromePalette.Light.CloseGlyphHot;

    /// <summary>The icon on the button now, kept until another replaces it.</summary>
    private IntPtr _icon;

    /// <summary>Show <paramref name="count"/> on the button, or nothing at none; <paramref name="description"/> is the screen reader's words.</summary>
    public void Show(int count, string? description)
    {
        _count = count;
        _description = description;
        Apply();
    }

    /// <summary>Draw in the palette's red, the page's theme's (SET_THEME).</summary>
    public void Wear(ChromePalette palette)
    {
        _fill = palette.CloseHover;
        _ink = palette.CloseGlyphHot;
        if (_count > 0) Apply();
    }

    /// <summary>Put the last count on the button as it is now: after its handle or its button was made again.</summary>
    public void Apply()
    {
        if (!_form.IsHandleCreated || _form.IsDisposed || Taskbar() is not { } taskbar) return;

        var label = WaitingBadge.Label(_count);
        IntPtr drawn;
        try
        {
            drawn = label is null ? IntPtr.Zero : Draw(label, SmallIcon(_form.DeviceDpi), _fill, _ink);
        }
        catch (Exception error) when (error is ExternalException or ArgumentException)
        {
            // A canvas that cannot be drawn on leaves the button as it was.
            return;
        }

        try
        {
            // An error before the button exists is the message's to mend, so the answer is not read.
            _ = taskbar.SetOverlayIcon(_form.Handle, drawn, label is null ? null : _description ?? label);
        }
        catch (Exception error) when (error is ExternalException or InvalidCastException)
        {
            // A taskbar that cannot be told keeps what it shows.
            if (drawn != IntPtr.Zero) DestroyIcon(drawn);
            return;
        }

        // Ours to free, and only once another has replaced it on the button.
        if (_icon != IntPtr.Zero) DestroyIcon(_icon);
        _icon = drawn;
    }

    public void Dispose()
    {
        if (_icon != IntPtr.Zero) DestroyIcon(_icon);
        _icon = IntPtr.Zero;
        if (_taskbar is not null) Marshal.ReleaseComObject(_taskbar);
        _taskbar = null;
    }

    /// <summary>The shell's taskbar object, made once on this thread; null for good once it would not be made.</summary>
    private ITaskbarList3? Taskbar()
    {
        if (_taskbar is not null || _unavailable) return _taskbar;
        try
        {
            var taskbar = (ITaskbarList3)new TaskbarList();
            if (taskbar.HrInit() >= 0) return _taskbar = taskbar;
            Marshal.ReleaseComObject(taskbar);
        }
        catch (Exception error) when (error is ExternalException or InvalidCastException or UnauthorizedAccessException)
        {
            // A shell with no taskbar (a server's, a replacement shell's) has none to badge.
        }

        _unavailable = true;
        return null;
    }

    /// <summary>A small icon's side at <paramref name="dpi"/>: 16 at 96, as the shell asks of an overlay.</summary>
    private static int SmallIcon(int dpi) => Math.Max(16, (int)Math.Round(16 * dpi / 96.0));

    /// <summary>
    /// <paramref name="label"/> in <paramref name="ink"/> on a circle of <paramref name="fill"/>, as an icon the caller
    /// frees. The text is an outline, centred by its own bounds and shrunk to fit the circle, so <c>9+</c> and <c>1</c>
    /// both sit in the middle.
    /// </summary>
    private static IntPtr Draw(string label, int side, Color fill, Color ink)
    {
        using var bitmap = new Bitmap(side, side, PixelFormat.Format32bppArgb);
        using (var canvas = Graphics.FromImage(bitmap))
        {
            canvas.Clear(Color.Transparent);
            canvas.SmoothingMode = SmoothingMode.AntiAlias;
            using (var circle = new SolidBrush(fill)) canvas.FillEllipse(circle, 0.5f, 0.5f, side - 1f, side - 1f);

            using var family = Family();
            using var text = new GraphicsPath();
            text.AddString(label, family, (int)FontStyle.Bold, side * 0.72f, PointF.Empty, StringFormat.GenericTypographic);
            var bounds = text.GetBounds();
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                var scale = Math.Min(1f, Math.Min(side * 0.66f / bounds.Width, side * 0.56f / bounds.Height));
                using var place = new Matrix();
                place.Translate(side / 2f, side / 2f);
                place.Scale(scale, scale);
                place.Translate(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2));
                text.Transform(place);
                using var digits = new SolidBrush(ink);
                canvas.FillPath(digits, text);
            }
        }

        return bitmap.GetHicon();
    }

    /// <summary>The system's face, or any sans-serif where it is missing.</summary>
    private static FontFamily Family()
    {
        try
        {
            return new FontFamily("Segoe UI");
        }
        catch (ArgumentException)
        {
            return new FontFamily(GenericFontFamilies.SansSerif);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    /// <summary>The shell's taskbar list (<c>CLSID_TaskbarList</c>).</summary>
    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
    private class TaskbarList
    {
    }

    /// <summary>
    /// <c>ITaskbarList3</c> (<c>shobjidl_core.h</c>), with <c>ITaskbarList</c>'s and <c>ITaskbarList2</c>'s methods first
    /// in their order: the table is read by position, so each method up to the overlay is declared though only two are
    /// called.
    /// </summary>
    [ComImport, Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        [PreserveSig] int HrInit();
        [PreserveSig] int AddTab(IntPtr window);
        [PreserveSig] int DeleteTab(IntPtr window);
        [PreserveSig] int ActivateTab(IntPtr window);
        [PreserveSig] int SetActiveAlt(IntPtr window);
        [PreserveSig] int MarkFullscreenWindow(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        [PreserveSig] int SetProgressValue(IntPtr window, ulong completed, ulong total);
        [PreserveSig] int SetProgressState(IntPtr window, int flags);
        [PreserveSig] int RegisterTab(IntPtr tab, IntPtr window);
        [PreserveSig] int UnregisterTab(IntPtr tab);
        [PreserveSig] int SetTabOrder(IntPtr tab, IntPtr before);
        [PreserveSig] int SetTabActive(IntPtr tab, IntPtr window, uint reserved);
        [PreserveSig] int ThumbBarAddButtons(IntPtr window, uint count, IntPtr buttons);
        [PreserveSig] int ThumbBarUpdateButtons(IntPtr window, uint count, IntPtr buttons);
        [PreserveSig] int ThumbBarSetImageList(IntPtr window, IntPtr images);
        [PreserveSig] int SetOverlayIcon(IntPtr window, IntPtr icon, [MarshalAs(UnmanagedType.LPWStr)] string? description);
    }
}
