using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>The machine's facts in memory: a topic reads not read yet until the test answers it, and each answer is told.</summary>
public sealed class MemoryMachineFacts : IMachineFacts
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, IReadOnlyList<MachineFact>> _answered = new(StringComparer.Ordinal);

    public event Action<string>? Changed;

    public FactsReading Read(string topic)
    {
        Known(topic);
        lock (_gate)
        {
            return _answered.TryGetValue(topic, out var facts)
                ? new FactsReading(topic, FactsState.Read, facts)
                : new FactsReading(topic, FactsState.NotReadYet, []);
        }
    }

    /// <summary>Answers <paramref name="topic"/> with <paramref name="facts"/>, replacing its reading, and tells it.</summary>
    public void Answer(string topic, params IReadOnlyList<MachineFact> facts)
    {
        Known(topic);
        lock (_gate) _answered[topic] = [.. facts];
        Changed?.Invoke(topic);
    }

    private static void Known(string topic)
    {
        if (!FactTopics.All.Contains(topic)) throw new ArgumentException($"the room reads no topic '{topic}'", nameof(topic));
    }
}
