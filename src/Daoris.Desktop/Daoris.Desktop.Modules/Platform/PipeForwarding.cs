namespace Daoris.Desktop;

/// <summary>
/// What the pipe forwards to the connection that holds a conversation (D238 point 3, the terminal-conversation design §3.2), in the order
/// told, through a queue of its own: the bus never waits on a connection. Past <c>bound</c> lines waiting, the oldest is dropped and a
/// <see cref="PipeHolding.GapType"/> naming the last console sequence dropped goes before the next line, so the terminal reads the backlog
/// again from its last printed line.
/// </summary>
internal sealed class PipeForwarding(PipeConnection connection, string session, int bound)
{
    private readonly Lock _gate = new();
    private readonly Queue<(string Line, long? Sequence)> _queue = new();
    private bool _gap;
    private long? _dropped;
    private bool _pumping;
    private bool _closed;

    /// <summary>One line to forward, <paramref name="sequence"/> the last console sequence it carries; nothing once closed.</summary>
    public void Tell(string line, long? sequence)
    {
        lock (_gate)
        {
            if (_closed) return;
            Enqueue(line, sequence);
            if (!Pump()) return;
        }

        _ = Task.Run(PumpAsync);
    }

    /// <summary>The last line, after everything already waiting, then nothing more: none where the connection has gone.</summary>
    public void Close(string? last)
    {
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            if (last is null) return;
            Enqueue(last, null);
            if (!Pump()) return;
        }

        _ = Task.Run(PumpAsync);
    }

    private void Enqueue(string line, long? sequence)
    {
        if (_queue.Count >= Math.Max(1, bound))
        {
            var (_, dropped) = _queue.Dequeue();
            _gap = true;
            if (dropped is { } at) _dropped = at;
        }

        _queue.Enqueue((line, sequence));
    }

    private bool Pump()
    {
        if (_pumping) return false;
        _pumping = true;
        return true;
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            string line;
            lock (_gate)
            {
                if (_gap)
                {
                    line = PipeHolding.Told(PipeHolding.GapType, new { session, sequence = _dropped });
                    _gap = false;
                    _dropped = null;
                }
                else if (_queue.TryDequeue(out var next))
                {
                    line = next.Line;
                }
                else
                {
                    _pumping = false;
                    return;
                }
            }

            await connection.WriteAsync(line).ConfigureAwait(false);
        }
    }
}
