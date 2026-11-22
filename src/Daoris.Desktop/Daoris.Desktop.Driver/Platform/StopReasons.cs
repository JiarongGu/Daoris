using System.Runtime.InteropServices;

namespace Daoris.Driver;

/// <summary>
/// Why the desktop stops: the closed list of doors an <c>app.stopped</c> line names in its <c>reason</c> field, and the one door
/// an operating system's termination signal comes through. A door tells <see cref="MachineLog.StopBecause"/> before the stop goes
/// on; the first told is the reason, so an update's close, which closes the window from code, stays the update's.
/// </summary>
/// <remarks>
/// A stop no door told is <see cref="Unknown"/>, said as such: a process ended from outside (a kill, a crash of the machine) leaves
/// no <c>app.stopped</c> at all, which the next start's missing stop already says.
/// </remarks>
public static class StopReasons
{
    /// <summary>The person closed the only main window.</summary>
    public const string WindowClosed = "window-closed";

    /// <summary>The person closed the last of several main windows, the first having been put away.</summary>
    public const string LastWindowClosed = "last-window-closed";

    /// <summary>An update's close: the staged <c>build</c>, and <c>by</c> what asked (<c>now</c>, a terminal's <c>daoris update --now</c>, or <c>idle</c>).</summary>
    public const string Update = "update";

    /// <summary>The system is shutting down or the account logging off.</summary>
    public const string SystemShutdown = "system-shutdown";

    /// <summary>The task manager closed the window.</summary>
    public const string TaskManager = "task-manager";

    /// <summary>A terminal or a session closed the window from outside (<see cref="StopAsk"/>): <c>by</c> what asked and, where known, its <c>session</c>.</summary>
    public const string TerminalAsk = "terminal-ask";

    /// <summary>A terminal asked the process to end: the <c>signal</c> it sent (interrupt, quit, hang-up, terminate).</summary>
    public const string Signal = "signal";

    /// <summary>An exception nothing caught ended the process: its <c>type</c>.</summary>
    public const string Failure = "unhandled-failure";

    /// <summary>The shell that started the process has gone, so it stops with it.</summary>
    public const string ShellGone = "shell-gone";

    /// <summary>No door told why.</summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// The reason a signal from a terminal is told as: written and the stop said at once, since a process the runtime ends
    /// on a signal may never reach the host's own stop.
    /// </summary>
    public static void Told(MachineLog log, PosixSignal signal)
    {
        log.StopBecause(Signal, ("signal", signal.ToString()));
        log.Stopped();
    }

    /// <summary>Listen for a terminal's asks to end this process, until the returned registrations are disposed.</summary>
    public static IDisposable Listen(MachineLog log)
    {
        List<PosixSignalRegistration> held = [];
        foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGHUP, PosixSignal.SIGTERM })
        {
            try
            {
                held.Add(PosixSignalRegistration.Create(signal, context => Told(log, context.Signal)));
            }
            catch (Exception error) when (error is PlatformNotSupportedException or InvalidOperationException)
            {
                // A signal this system cannot deliver to the process is one no terminal can ask with.
            }
        }

        return new Listening(held);
    }

    private sealed class Listening(List<PosixSignalRegistration> held) : IDisposable
    {
        public void Dispose() => held.ForEach(each => each.Dispose());
    }
}
