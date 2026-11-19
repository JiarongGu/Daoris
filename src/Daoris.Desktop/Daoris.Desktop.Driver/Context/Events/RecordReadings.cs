namespace Daoris.Driver;

/// <summary>
/// Context's part of a run's sink: the session's event file keeps each event said and each child's and task's start and end
/// (<see cref="ChildLifetimes"/>), a subagent's own record each of its events (<see cref="ChildRecords"/>), and its console shows each
/// line and the streams beside it. Watched where a console is.
/// </summary>
/// <remarks>
/// An event the record cannot keep costs a console line, never the run: the conversation enriches a run, it does not run it.
/// A task's end the person asked for keeps them as who stopped it (<see cref="SessionProcesses.PersonStopped"/>, handed as
/// <c>stops</c>).
/// </remarks>
internal sealed class RecordReadings(
    string sessionId, ISessionOutput? output, ISessionEvents? events, ChildRecords? children = null, SessionProcesses? stops = null) : IReadingSink
{
    private readonly ChildLifetimes _lifetimes = new(stops is null ? null : task => stops.PersonStopped(sessionId, task));
    private ISessionStreams? _streams;

    public bool Watched => output is not null;

    public void Read(WireReading reading)
    {
        if (_lifetimes.Read(reading) is { } lifetime) events?.Keep(sessionId, lifetime, Show);
        switch (reading)
        {
            case WireReading.Said said:
                events?.Keep(sessionId, said.Event, Show);
                break;
            case WireReading.Shown shown:
                Show(shown.Text);
                break;
            case WireReading.StreamOpened opened:
                Streams()?.Open(opened.Stream);
                break;
            case WireReading.StreamSaid line:
                Streams()?.Line(line.Stream, line.Text);
                break;
            case WireReading.StreamEnded ended:
                Streams()?.End(ended.Stream, ended.State);
                break;
            case WireReading.ChildSaid { Replayed: false } own when ChildRecords.ChildOf(own.Stream) is { } child:
                children?.Keep(sessionId, child, own.Event);
                break;
        }
    }

    private void Show(string text) => output?.Append(sessionId, text);

    private ISessionStreams? Streams() => output is null ? null : _streams ??= output.ForSession(sessionId);
}
