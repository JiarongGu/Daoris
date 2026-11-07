namespace Daoris.Driver;

/// <summary>
/// Context's part of a run's sink: the session's event file keeps each event said, and its console
/// shows each line and the streams beside it. Watched where a console is.
/// </summary>
/// <remarks>
/// An event the record cannot keep costs a console line, never the run: the conversation enriches a run, it does not run it.
/// </remarks>
internal sealed class RecordReadings(string sessionId, ISessionOutput? output, ISessionEvents? events) : IReadingSink
{
    private ISessionStreams? _streams;

    public bool Watched => output is not null;

    public void Read(WireReading reading)
    {
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
        }
    }

    private void Show(string text) => output?.Append(sessionId, text);

    private ISessionStreams? Streams() => output is null ? null : _streams ??= output.ForSession(sessionId);
}
