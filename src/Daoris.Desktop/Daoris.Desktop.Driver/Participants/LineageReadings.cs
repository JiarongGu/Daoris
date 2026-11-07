namespace Daoris.Driver;

/// <summary>
/// Participants' part of a run's sink: each child the run's door said started or ended,
/// as its <see cref="WireReading.ChildSeen"/> or <see cref="WireReading.ChildEnded"/>, is one line of the session's lineage
/// (<see cref="ChildBindings.ChildLine"/>), naming the process the run stamped on the reading. Both executors compose it, so no transport
/// names the lineage; a replay is no new child, and every other reading passes over.
/// </summary>
/// <remarks>Never throws: a line that cannot be written is a lineage unknown (<see cref="ChildLineage.Append"/>).</remarks>
internal sealed class LineageReadings(string home, string sessionId) : IReadingSink
{
    public bool Watched => false;

    public void Read(WireReading reading)
    {
        if (ChildBindings.ChildLine(reading, DateTimeOffset.UtcNow) is { } line) ChildLineage.Append(home, sessionId, line);
    }
}
