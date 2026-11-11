using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>An agent supervisor's turns running this run, one per subject, each ended by the person's word on its subject.</summary>
public sealed class SupervisorRuns
{
    private readonly ConcurrentDictionary<SupervisorSubject, CancellationTokenSource> _running = new();

    public bool Running(SupervisorSubject subject) => _running.ContainsKey(subject);

    /// <summary>The sessions a turn runs for, one at a time per line.</summary>
    public IReadOnlyCollection<string> Lines => [.. _running.Keys.Select(subject => subject.Session)];

    public CancellationTokenSource? Start(SupervisorSubject subject, CancellationToken ct)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_running.TryAdd(subject, source)) return source;
        source.Dispose();
        return null;
    }

    public void Ended(SupervisorSubject subject)
    {
        if (_running.TryRemove(subject, out var source)) source.Dispose();
    }

    /// <summary>Ends each turn whose subject no longer waits for it, the person's word having answered it, never a reading of Ask Daoris's acts.</summary>
    public int StopUnwaited(IReadOnlyCollection<SupervisorSubject> waiting)
    {
        var stopped = 0;
        foreach (var (_, source) in _running.Where(each => !waiting.Contains(each.Key) && each.Key.Kind != SupervisorKinds.AskDaoris).ToList())
        {
            try
            {
                source.Cancel();
                stopped++;
            }
            catch (ObjectDisposedException)
            {
                continue;
            }
        }

        return stopped;
    }
}
