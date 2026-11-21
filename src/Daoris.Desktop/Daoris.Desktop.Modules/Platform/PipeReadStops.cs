using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// The stop of each open read a terminal asked over the pipe: a read is work for its asker alone (a press is the instance's, journaled
/// and run to its end), so once the connection that last sent it ends, its work ends too, and its git walks with it. A read sent again
/// by another connection while it runs is that one's, as its progress is (<see cref="PipeState.Owners"/>).
/// </summary>
internal sealed class PipeReadStops(InstancePipe pipe)
{
    /// <summary>Whether the request is a driver read an asker waits on: an open route declaring no change. Every press runs on to its answer.</summary>
    internal static bool IsRead(IpcRequest request, PipeClass? @class) =>
        @class == PipeClass.Open && string.Equals(request.Module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) && DriverModule.Declared(request) is null;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, CancellationTokenSource> _stops = new(StringComparer.Ordinal);

    /// <summary>A token for the read under <paramref name="id"/>, ended with the instance and with its owner's connection.</summary>
    public CancellationToken Begin(string id)
    {
        CancellationTokenSource stop;
        try
        {
            stop = CancellationTokenSource.CreateLinkedTokenSource(pipe.Lifetime.Token);
        }
        catch (ObjectDisposedException)
        {
            return new CancellationToken(true);
        }

        lock (_gate) _stops[id] = stop;
        return stop.Token;
    }

    /// <summary>The read under <paramref name="id"/> answered: its stop is let go.</summary>
    public void End(string id)
    {
        CancellationTokenSource? stop;
        lock (_gate) _stops.Remove(id, out stop);
        stop?.Dispose();
    }

    /// <summary>A connection ended: every read it owns is stopped.</summary>
    public void ConnectionEnded(PipeConnection connection)
    {
        List<string> ids;
        lock (_gate) ids = [.. _stops.Keys];
        List<string> owned;
        lock (pipe.State.Gate) owned = [.. ids.Where(id => pipe.State.Owners.GetValueOrDefault(id) == connection)];
        foreach (var id in owned)
        {
            CancellationTokenSource? stop;
            lock (_gate) _stops.TryGetValue(id, out stop);
            try
            {
                stop?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Answered as the connection ended.
            }
        }
    }

    /// <summary>How many reads have a stop now: the tests' window.</summary>
    internal int Open
    {
        get
        {
            lock (_gate) return _stops.Count;
        }
    }
}
