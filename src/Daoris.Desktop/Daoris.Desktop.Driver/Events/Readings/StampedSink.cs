namespace Daoris.Driver;

/// <summary>
/// A run's sink that says whose each reading is and where in the run it came (the agent-provider design §3.1, row l): the run it belongs to,
/// the generation of the process that said it (the first process is 1, a retry's or a go-on's the next), and its place in the run's order,
/// so a part that keeps readings from several runs, or a replay, can tell them apart without reading the wire again.
/// </summary>
/// <remarks>The sequence is the run's own count, never a clock: two readings of one run are ordered by it however fast they came.</remarks>
internal sealed class StampedSink(IReadingSink inner, Func<string?> run, Func<int> generation) : IReadingSink
{
    private long _sequence;

    public bool Watched => inner.Watched;

    public void Read(WireReading reading) =>
        inner.Read(reading with { Run = run(), Generation = generation(), Sequence = Interlocked.Increment(ref _sequence) });
}
