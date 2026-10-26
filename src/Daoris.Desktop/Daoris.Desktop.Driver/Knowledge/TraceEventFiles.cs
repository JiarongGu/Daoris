namespace Daoris.Driver;

/// <summary>
/// What a trace reads of a session's events (TRACE1, D143; FOUNDATION1 mod-viii, the design §14.3): the events a session kept under the
/// sessions' folder, in order, or none where it kept no file. The trace names no type of the context, which keeps the file; the context
/// answers (<c>SessionEventReads</c>), installed when the driver's assembly loads, since the trace is read by the host's terminal, the
/// shell's bridge and the tests, none of them built by Loop.
/// </summary>
internal interface ITraceEventFiles
{
    /// <summary>
    /// <paramref name="sessionId"/>'s events as <paramref name="folder"/> keeps them; null where it keeps none. A file that cannot be read
    /// throws as the read does (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>, <see cref="DriverException"/>), and the
    /// trace says it could not be read.
    /// </summary>
    IReadOnlyList<SessionEvent>? Read(string folder, string sessionId);
}

/// <summary>The events' reading in force: the context's, installed when the assembly loads; a test may put another in and puts it back.</summary>
internal static class TraceEventFiles
{
    private static ITraceEventFiles? _current;

    /// <summary>The reading in force.</summary>
    /// <exception cref="InvalidOperationException">None was installed: the assembly's own initializer has not run.</exception>
    internal static ITraceEventFiles Current
    {
        get => _current ?? throw new InvalidOperationException("no reading of a session's events is installed: the context installs it when the driver loads");
        set => _current = value;
    }
}
