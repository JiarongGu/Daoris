using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>
/// The reader's walk on a script: each reading answers the next scripted answer, or still queued where none is left, and every ask is
/// recorded.
/// </summary>
public sealed class ScriptedReader : IReaderWalk
{
    private readonly Lock _gate = new();
    private readonly Queue<ReaderAnswer> _answers = new();
    private readonly List<ReaderAsk> _asked = [];

    /// <summary>Every ask the walk took, in order.</summary>
    public IReadOnlyList<ReaderAsk> Asked
    {
        get
        {
            lock (_gate) return [.. _asked];
        }
    }

    /// <summary>Scripts the next reading's answer.</summary>
    public void Answer(ReaderAnswer answer)
    {
        lock (_gate) _answers.Enqueue(answer);
    }

    public Task<ReaderAnswer> ReadAsync(ReaderAsk ask, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(ask.Packet)) throw new ArgumentException("a reading is handed a packet, and this one is blank", nameof(ask));
        if (ask.To < ask.From) throw new ArgumentException($"the range {ask.From} to {ask.To} ends before it starts", nameof(ask));
        lock (_gate)
        {
            _asked.Add(ask);
            return Task.FromResult(_answers.TryDequeue(out var answer)
                ? answer
                : new ReaderAnswer(ReaderState.Queued, "nothing is scripted for the reader, so the reading waits in the queue"));
        }
    }
}
