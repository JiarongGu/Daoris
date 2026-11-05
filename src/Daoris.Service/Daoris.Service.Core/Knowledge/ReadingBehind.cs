namespace Daoris.Knowledge;

/// <summary>
/// The index's reading kept current behind a read that only counts what it holds: the registry. Such a read waits for the index
/// only while it holds nothing; otherwise it answers what is held and the reading that judges the roots (and reads again the ones
/// that moved) runs beside it, one at a time, its answer the next read's. The repositories' summary still waits: what it answers is
/// the reading itself, when and from which commit each root was read.
/// </summary>
/// <remarks>
/// The registry is what every door resolves a workspace by, so a reading it waited for (a rescan, a root that moved, the first one
/// after a start) held every one of them for as long as the checkouts took to read. A count from a minute before is the same
/// count the reading would have answered had it been asked a minute before; a quest is addressed by the registrations, which are
/// read again on each read, never by the counts.
/// </remarks>
internal sealed class ReadingBehind(Func<CancellationToken, Task<bool>> holdsAny, Func<CancellationToken, Task> read)
{
    private int _running;

    /// <summary>The reading the caller may count from: awaited where the index holds nothing, else started behind it.</summary>
    public async Task ReadyAsync(CancellationToken ct)
    {
        if (!await holdsAny(ct).ConfigureAwait(false))
        {
            await read(ct).ConfigureAwait(false);
            return;
        }

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await read(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // The next read starts it again; what failed is said by the door that reads the index itself.
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        }, CancellationToken.None);
    }
}
