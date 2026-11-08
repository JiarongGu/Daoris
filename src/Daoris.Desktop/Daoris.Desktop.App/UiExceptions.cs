using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// An exception raised on a window's thread (a dialog's callback, an event handler, a shell extension's managed
/// helper) is written to the machine log and the pump goes on, on the main window's thread and on every secondary
/// window's own pump alike. Without it Windows Forms shows its own dialog, or ends the process where none can show.
/// </summary>
internal static class UiExceptions
{
    /// <summary>Catch every window thread's exception from now on; call before the first window is made.</summary>
    public static void Watch(MachineLog log)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            log.Error("error", ("where", "a window's thread"), ("message", e.Exception.GetType().Name + ": " + e.Exception.Message));
    }
}
