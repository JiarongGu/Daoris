namespace Daoris.Driver;

/// <summary>
/// Where a run writes its readings (the agent-provider design §3.1, row l): one port per run, composed where the run's executor is built of
/// its owners' parts, each keeping the readings that are its own and passing over the rest. A part's failure is its own to say; one that
/// throws reaches the transport, which says it on the transcript and goes on, as today's callbacks did.
/// </summary>
public interface IReadingSink
{
    /// <summary>
    /// Whether someone watches the run's console, so the streams its agent runs beside itself are kept apart from its own lines
    /// (CONSOLE3c); where nobody does, their lines are the run's own.
    /// </summary>
    bool Watched { get; }

    void Read(WireReading reading);
}

/// <summary>The sinks every executor composes from: none, its owners' parts together, and one reading's keeper.</summary>
public static class ReadingSinks
{
    /// <summary>A sink that keeps nothing and is not watched.</summary>
    public static IReadingSink None { get; } = new Parts([]);

    /// <summary>The parts as one sink, each told every reading in the order given; watched where any part is.</summary>
    public static IReadingSink Of(params IReadingSink?[] parts) => new Parts([.. parts.OfType<IReadingSink>()]);

    /// <summary>A part that keeps one kind of reading with <paramref name="keep"/>, never watched.</summary>
    public static IReadingSink On<T>(Action<T> keep)
        where T : WireReading => new One<T>(keep);

    private sealed class Parts(IReadOnlyList<IReadingSink> parts) : IReadingSink
    {
        public bool Watched => parts.Any(part => part.Watched);

        public void Read(WireReading reading)
        {
            foreach (var part in parts) part.Read(reading);
        }
    }

    private sealed class One<T>(Action<T> keep) : IReadingSink
        where T : WireReading
    {
        public bool Watched => false;

        public void Read(WireReading reading)
        {
            if (reading is T kept) keep(kept);
        }
    }
}
