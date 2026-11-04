namespace Daoris.Driver;

/// <summary>
/// What woke a watch's loop and how often (DEV3, FABRIC-EVENT3a): the nudges counted, whether a plain one came, the last nudge's topic and
/// subject, and the source a nudge cancels to end the loop's wait. Any thread's nudge writes it; the loop and its look's wait read it.
/// </summary>
/// <remarks>
/// <para><b>No lock: the order is the guard.</b> A nudge sets the plain flag, then its wake, then counts, then swaps the pause source
/// (<see cref="DriverWatch"/>'s <c>Woken</c> calls them in that order); the loop reads the count, then takes the flag. So a look that
/// counts a plain nudge is whole whatever topic's wake replaced the last one after it, and a plain nudge the count missed makes the next
/// look whole and is looked at again.</para>
///
/// <para><b>What a race would break.</b> The swap is one exchange, so two racing nudges each cancel a source that is no longer current,
/// and each source is cancelled and disposed once; a wait that links to a source as it is swapped hears
/// <see cref="ObjectDisposedException"/>, which the loop reads as the nudge it is. Counted before it swaps, a nudge that lands while a
/// look runs is seen by the count the loop compares before it waits, so the loop looks again rather than sleep through it.</para>
/// </remarks>
internal sealed class WatchWakes
{
    private CancellationTokenSource _pause = new();

    // Every nudge counted, so one that lands while a look runs (before the wait it would have ended) still ends that wait (DEV3): looks
    // are short, and a person's control nudges the loop often.
    private int _nudges;

    // Whether a plain nudge came since the loop last took its nudges up (FABRIC-EVENT3a).
    private int _everything;

    // What the last nudge said woke the loop: null for a plain nudge, which means everything.
    private Wake? _woke;

    /// <summary>A nudge's topic and subject: a change source's wake, never a value.</summary>
    internal sealed record Wake(string Topic, string Subject);

    /// <summary>The nudges counted so far.</summary>
    internal int Count => Volatile.Read(ref _nudges);

    /// <summary>What the last nudge said woke the loop; null for a plain one.</summary>
    internal Wake? Woke => Volatile.Read(ref _woke);

    /// <summary>A plain nudge came: set before the nudge is counted.</summary>
    internal void Everything() => Interlocked.Exchange(ref _everything, 1);

    /// <summary>Whether a plain nudge came since the last take, cleared as it is read; taken after the count.</summary>
    internal bool TakeEverything() => Interlocked.Exchange(ref _everything, 0) == 1;

    /// <summary>The wake this nudge carries, written before it is counted.</summary>
    internal void Woken(Wake? wake) => Volatile.Write(ref _woke, wake);

    /// <summary>The nudge counted, then the pause source swapped, and the one swapped out cancelled and disposed.</summary>
    internal void Counted()
    {
        Interlocked.Increment(ref _nudges);

        // Swap first, atomically, so two racing nudges each cancel a source that is no longer current: the swapped-out source is
        // cancelled, disposed, and never waited on again.
        var paused = Interlocked.Exchange(ref _pause, new CancellationTokenSource());
        paused.Cancel();
        paused.Dispose();
    }

    /// <summary>The current pause source's token. Throws <see cref="ObjectDisposedException"/> where a nudge swapped it as this read.</summary>
    internal CancellationToken Paused => Volatile.Read(ref _pause).Token;

    /// <summary>
    /// The loop's wait, ended by <paramref name="ct"/> or the current pause source. Throws <see cref="ObjectDisposedException"/> where a
    /// nudge swapped the source as this began.
    /// </summary>
    internal CancellationTokenSource Wait(CancellationToken ct) => CancellationTokenSource.CreateLinkedTokenSource(ct, _pause.Token);
}
