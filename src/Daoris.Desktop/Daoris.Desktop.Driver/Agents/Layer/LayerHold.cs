using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// The executor's steps around each process of a held <see cref="LayerRun"/>, the same as a driven run's through the same host
/// (<see cref="ITransportHost"/>): the downloads the start is handed kept on its record, each process's lineage token issued and set on
/// its environment before the layer starts it and handed the process it started, the process tracked and reaped,
/// each raw line stamped for the watch, and the wait to its exit under the start's bound and the watch's quiet.
/// </summary>
internal sealed class LayerHold(ITransportHost host, TransportStart start) : IDisposable
{
    private readonly object _gate = new();

    private readonly List<IDisposable> _kept = [];

    /// <summary>The first process's token, issued before the open; null before and once a process took it.</summary>
    private string? _bracketed;

    public ITransportHost Host => host;

    /// <summary>The start the run holds, completed by its executor before the open.</summary>
    public TransportStart Start { get; private set; } = start;

    /// <summary>Whether the run goes on with held words: its start names what it goes on in.</summary>
    public bool GoesOn => Start.Target is not null && Start.Selection is not null;

    public string Session => Start.Session ?? throw new InvalidOperationException("the run's start names no session: it is handed before the open.");

    /// <summary>The environment the shell composed for the start, the latest process's lineage token on it.</summary>
    public IReadOnlyDictionary<string, string?> Environment
    {
        get
        {
            lock (_gate) return new Dictionary<string, string?>(Start.Info.Environment, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Whether a stop is made of the session through its registry: the end that follows is the stop's.</summary>
    public bool StopAsked => Start.Session is { } session && host.Processes.WasStopRequested(session);

    public void Hand(TransportStart completed)
    {
        if (!ReferenceEquals(completed.Info, Start.Info) || completed.Whose != Start.Whose || completed.Piece != Start.Piece
            || completed.Moved != Start.Moved)
        {
            throw new InvalidOperationException("a start handed later must start the same process as the run's.");
        }

        Start = completed;
    }

    /// <summary>
    /// The first process made ready before the open: the downloads its start is handed kept on its record, then its lineage token issued, its
    /// <c>start</c> written and the token on its environment, or the token its move issued. The layer starts the process at the open.
    /// </summary>
    public async Task BracketAsync(string sessionId, CancellationToken ct)
    {
        if (_bracketed is not null) throw new InvalidOperationException("the run's first process is bracketed already.");
        await host.KeepHandedAsync(sessionId, Start.Info, ct).ConfigureAwait(false);
        lock (_gate) _bracketed = host.Bracketed(sessionId, Start.Info, Start.Moved);
    }

    /// <summary>A process's token: the first's, bracketed before the open where it is, or a later process's, a retry's or a go-on's, issued now.</summary>
    public async Task<string> TokenAsync(bool first, CancellationToken ct)
    {
        if (first && _bracketed is null) await BracketAsync(Session, ct).ConfigureAwait(false);
        lock (_gate)
        {
            if (!first) return host.Bracketed(Session, Start.Info, moved: null);
            var proc = _bracketed!;
            _bracketed = null;
            return proc;
        }
    }

    /// <summary>The process the token is issued for, handed it once the layer starts it, or the token let go where none started.</summary>
    public Task<Process?> SpawnedAsync(string proc, Task<Process?> started) => host.SpawnedAsync(proc, () => started);

    /// <summary>The process the launcher answered: tracked under the session and reaped with the run.</summary>
    public void Track(Process process)
    {
        var tracked = host.Processes.Track(Session, process, Start.RefusesInput, Start.DrivesBrowser);
        lock (_gate)
        {
            _kept.Add(process);
            _kept.Add(tracked);
            _kept.Add(new Disposer(() => SessionProcesses.EndIfRunning(process)));
        }
    }

    public void Heard() => host.Heard(Session);

    /// <summary>
    /// The exit, or null where a bound killed it, waited for under the start's bound and the watch's quiet for the first process, and under
    /// the machine's alone for a later one; <paramref name="killing"/> told before either kills it. Then its turn read to its end, bounded
    /// past the exit, and the process's lineage <c>end</c> written.
    /// </summary>
    public async Task<int?> EndedAsync(string proc, Process process, Task turn, bool bounded, Action killing, CancellationToken ct)
    {
        var start = Start;
        var exit = bounded
            ? await host.WaitAsync(
                    Session, proc, process, ct, start.Minutes,
                    minutes =>
                    {
                        killing();
                        start.TimingOut?.Invoke(minutes);
                    },
                    start.Quiet,
                    () =>
                    {
                        killing();
                        start.QuietEnding?.Invoke();
                    })
                .ConfigureAwait(false)
            : await host.WaitAsync(Session, proc, process, ct, null, null, default, null).ConfigureAwait(false);
        await host.DrainedAsync(Session, proc, turn).ConfigureAwait(false);
        host.Ended(Session, proc, exit);
        return exit;
    }

    /// <summary>The run let go before its end: its process ended, its end written unclean once its exit is read, and its turn observed.</summary>
    public Task AbandonedAsync(string proc, Process process, Task turn) => host.AbandonedAsync(Session, proc, process, turn);

    /// <summary>Each process ended if still running and untracked, the latest first; a token bracketed for a process never started let go.</summary>
    public void Dispose()
    {
        string? unspawned;
        IDisposable[] kept;
        lock (_gate)
        {
            unspawned = _bracketed;
            _bracketed = null;
            kept = [.. _kept];
            _kept.Clear();
        }

        if (unspawned is not null) LineageTokens.Current.Unspawned(unspawned);
        for (var at = kept.Length - 1; at >= 0; at--) kept[at].Dispose();
    }
}
