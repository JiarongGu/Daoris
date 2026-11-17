using Daoris.Assistant.Ports;

namespace Daoris.Assistant.Testing;

/// <summary>Turns in memory: the test appends each event and ends each turn, and a listener hears the end at once, on the test's thread.</summary>
public sealed class ScriptedTurnEvents : ITurnEvents
{
    private readonly Lock _gate = new();
    private readonly List<TurnEvent> _events = [];
    private readonly List<Action<TurnEnded>> _hearing = [];
    private long _index;

    public IDisposable OnTurnEnded(Action<TurnEnded> heard)
    {
        lock (_gate) _hearing.Add(heard);
        return new Hearing(() =>
        {
            lock (_gate) _hearing.Remove(heard);
        });
    }

    public Task<IReadOnlyList<TurnEvent>> AfterAsync(string conversation, long index, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyList<TurnEvent>>([.. _events.Where(each => each.Conversation == conversation && each.Index > index)]);
    }

    /// <summary>Adds an event to <paramref name="conversation"/> and answers its index, above every index before it.</summary>
    public long Append(string conversation, string kind, string text, string? name = null)
    {
        lock (_gate)
        {
            _index++;
            _events.Add(new TurnEvent(conversation, _index, kind, text) { Name = name });
            return _index;
        }
    }

    /// <summary>Ends <paramref name="conversation"/>'s turn at its last event, told to each listener.</summary>
    public void EndTurn(string conversation, TurnOutcome outcome = TurnOutcome.Answered)
    {
        TurnEnded ended;
        Action<TurnEnded>[] hearing;
        lock (_gate)
        {
            ended = new TurnEnded(conversation, _events.LastOrDefault(each => each.Conversation == conversation)?.Index ?? 0, outcome);
            hearing = [.. _hearing];
        }

        foreach (var heard in hearing) heard(ended);
    }

    private sealed class Hearing(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }
}
