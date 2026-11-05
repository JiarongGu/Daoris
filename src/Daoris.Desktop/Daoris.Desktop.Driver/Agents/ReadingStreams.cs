namespace Daoris.Driver;

/// <summary>
/// The streams a run's agent runs beside itself (CONSOLE3c), written as readings to the run's sink (row l): what the protocol session and the
/// native mapper are handed where they keep a subagent's or a task's lines apart, so neither names the console that shows them.
/// </summary>
internal sealed class ReadingStreams(IReadingSink sink) : ISessionStreams
{
    /// <summary>The streams over <paramref name="sink"/> where someone watches it; none otherwise, so their lines stay the run's own.</summary>
    public static ISessionStreams? Of(IReadingSink sink) => sink.Watched ? new ReadingStreams(sink) : null;

    public void Open(SessionStream stream) => sink.Read(new WireReading.StreamOpened(stream));

    public void Line(string streamId, string text) => sink.Read(new WireReading.StreamSaid(streamId, text));

    public void End(string streamId, string? state) => sink.Read(new WireReading.StreamEnded(streamId, state));
}
