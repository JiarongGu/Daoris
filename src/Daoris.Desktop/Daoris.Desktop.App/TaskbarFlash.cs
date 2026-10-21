using System.Runtime.InteropServices;

namespace Daoris.Desktop;

/// <summary>
/// A main window's taskbar button lit while a terminal's press waits unshown, and stopped (NOTIFY2b, the notification design §3.3).
/// Which window and when is <see cref="PressFlash"/>'s judgement, tested in the modules; this is the shell call alone.
/// </summary>
/// <remarks>
/// <c>FLASHW_TRAY | FLASHW_TIMERNOFG</c>: the button alone, never the caption, lit until the window comes to the foreground, which
/// Windows stops by itself; <c>FLASHW_STOP</c> puts it back where a press ended or another main window came forward. Posted to the
/// window's own thread, never blocking: the pipe tells what waits on whatever thread changed it.
/// </remarks>
internal static class TaskbarFlash
{
    private const uint FLASHW_STOP = 0;
    private const uint FLASHW_TRAY = 0x2;
    private const uint FLASHW_TIMERNOFG = 0xC;

    /// <summary>Light <paramref name="form"/>'s taskbar button, or stop it, from any thread.</summary>
    public static void Set(Form? form, bool on)
    {
        if (form is null || form.IsDisposed || !form.IsHandleCreated) return;
        try
        {
            form.BeginInvoke(() =>
            {
                if (form.IsDisposed || !form.IsHandleCreated) return;
                var info = new FlashInfo
                {
                    Size = (uint)Marshal.SizeOf<FlashInfo>(),
                    Window = form.Handle,
                    Flags = on ? FLASHW_TRAY | FLASHW_TIMERNOFG : FLASHW_STOP,
                    Count = 0,
                    Timeout = 0,
                };
                FlashWindowEx(ref info);
            });
        }
        catch (InvalidOperationException)
        {
            // The handle went between the look and the post: nothing to light.
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashInfo info);
}
