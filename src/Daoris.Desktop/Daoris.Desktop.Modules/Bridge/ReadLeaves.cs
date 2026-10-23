namespace Daoris.Desktop;

/// <summary>
/// The page's door to stop a read it sent (GITAPP1a-iii, D202's round-4 point 3, the git-app design P1.6): each call that carries a
/// <c>read</c> id waits under a source of its own, linked to the token the route was handed, and <c>READ_LEAVE { read }</c> cancels
/// that source. The bridge's call carries no cancellation of its own (`bridge/call.ts`), so this is how a page that left is heard.
/// </summary>
/// <remarks>
/// <para><b>Bound to the window that asked.</b> The token a route is handed is its window's bridge lifetime (the kit's
/// <c>IpcHostBridge</c>, cancelled as the window is destroyed), so a read's source ends with its window too, with no page
/// script, and with the page's own leave while the window lives.</para>
///
/// <para><b>One caller's leaving, not the read's.</b> A cancelled source stops its caller waiting; a shared read
/// (<see cref="RouteFlights"/>) stops only once no other caller waits.</para>
///
/// <para><b>Dropped as the route answers.</b> A leave for an id already answered, or never asked, finds nothing and does nothing.</para>
/// </remarks>
public sealed class ReadLeaves
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CancellationTokenSource> _live = new(StringComparer.Ordinal);
    private int _waiting;

    /// <summary>
    /// The token a call carrying <paramref name="read"/> waits under, linked to <paramref name="route"/>; held until the returned
    /// lease is disposed. A call with no id waits under <paramref name="route"/> alone.
    /// </summary>
    public Lease Open(string? read, CancellationToken route)
    {
        if (string.IsNullOrEmpty(read)) return new Lease(this, null, null, route);
        var source = CancellationTokenSource.CreateLinkedTokenSource(route);
        lock (_gate) _live[read] = source;
        return new Lease(this, read, source, source.Token);
    }

    /// <summary>Cancel the call waiting under <paramref name="read"/>: false where none is, its answer given or never asked.</summary>
    public bool Leave(string read)
    {
        CancellationTokenSource? source;
        lock (_gate)
        {
            if (!_live.Remove(read, out source)) return false;
        }

        // Off the leaver's thread: the waiter's own callbacks run off it. Whoever takes a source out of the table disposes it, so
        // its link to the window's lifetime is never left registered.
        _ = source.CancelAsync().ContinueWith(_ => source.Dispose(), TaskScheduler.Default);
        return true;
    }

    /// <summary>
    /// How many calls wait on a shared read now, each counted once it has joined the read's flight (<see cref="Lease.Joined"/>) until
    /// it answers or leaves: the tests' window on who waits.
    /// </summary>
    public int Waiting => Volatile.Read(ref _waiting);

    private void Drop(string read, CancellationTokenSource source)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(read, out var held) || !ReferenceEquals(held, source)) return;
            _live.Remove(read);
        }

        source.Dispose();
    }

    /// <summary>One call's hold on its read: its token, and its id dropped as it is disposed.</summary>
    public sealed class Lease(ReadLeaves leaves, string? read, CancellationTokenSource? source, CancellationToken token) : IDisposable
    {
        private int _joined;

        /// <summary>What the call waits under.</summary>
        public CancellationToken Token { get; } = token;

        /// <summary>The call has joined its read's flight (<see cref="RouteFlights.JoinAsync"/>): counted in <see cref="Waiting"/>.</summary>
        public void Joined()
        {
            if (Interlocked.Exchange(ref _joined, 1) == 0) Interlocked.Increment(ref leaves._waiting);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _joined, 0) == 1) Interlocked.Decrement(ref leaves._waiting);
            if (read is not null && source is not null) leaves.Drop(read, source);
        }
    }
}
