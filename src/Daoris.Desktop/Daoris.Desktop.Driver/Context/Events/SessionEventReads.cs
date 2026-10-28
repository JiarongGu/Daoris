using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The context's answer to the port the trace declares for a session's events (FOUNDATION1 mod-viii, the design §14.3): the events file
/// <see cref="SessionEvents"/> keeps, read from its first line. Installed when the driver's assembly loads, before any trace can be read, so
/// the host's terminal, the shell's route and every test read what they always read and no construction site hands the trace anything.
/// </summary>
internal sealed class SessionEventReads : ITraceEventFiles
{
    [ModuleInitializer]
    internal static void Install() => TraceEventFiles.Current = new SessionEventReads();

    public IReadOnlyList<SessionEvent>? Read(string folder, string sessionId)
    {
        var events = new SessionEvents(folder);
        return File.Exists(events.PathOf(sessionId)) ? events.After(sessionId, 0).Events : null;
    }
}
