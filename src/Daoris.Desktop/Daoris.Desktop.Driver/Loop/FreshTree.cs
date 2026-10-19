namespace Daoris.Driver;

/// <summary>
/// The tree a start opened for a session whose record is not open yet (D51; GOONTREE1; QUEUEDTREE1, D181's PARALLEL1f note): let go
/// by the clean path on each way out that starts nothing, and on the driver's close before the record's open is asked.
/// </summary>
/// <remarks>
/// <para><b>On no token</b>, as the put-back's move is (<c>DriverContinuations.PutBackAsync</c>). The close is what cancelled the
/// loop's token, and a removal on it threw at its first git, so every way out the close reached left the tree, and its branch, to the
/// sweep. The removal is git's few starts, each bounded by its own timeout.</para>
///
/// <para><b>Until the open is asked.</b> Once the record's open is asked the ledger may hold the tree (LEFT2), even where the close cut
/// its answer off, so a close after that leaves it to the sweep, which reads the ledger. A refused open still lets it go itself.</para>
///
/// <para><b>Only the close.</b> A start that failed otherwise fails its look as it always did, its tree the sweep's.</para>
/// </remarks>
/// <param name="remove">The clean removal (<c>SessionTrees.RemoveAsync</c>), on no token.</param>
internal sealed class FreshTree(Func<string, Task> remove)
{
    private string? _path;
    private volatile bool _asked;

    /// <summary>The start opened <paramref name="path"/> for its session.</summary>
    internal void Opened(string path) => Volatile.Write(ref _path, path);

    /// <summary>The record's open is asked: a close from here leaves the tree to the ledger and the sweep.</summary>
    internal void Asked() => _asked = true;

    /// <summary>Let the tree go, once: nothing where the start opened none, or it went already.</summary>
    internal Task LetGoAsync() => Interlocked.Exchange(ref _path, null) is { } path ? remove(path) : Task.CompletedTask;

    /// <summary>
    /// Run <paramref name="start"/> to its record's open or to what it came to. Where the driver's close (<paramref name="ct"/>) ends
    /// it before the open is asked, its tree goes first, and the close goes on as it came.
    /// </summary>
    internal async Task<T> UntilOpenAsync<T>(Func<Task<T>> start, CancellationToken ct)
    {
        try
        {
            return await start().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested && !_asked)
        {
            await LetGoAsync().ConfigureAwait(false);
            throw;
        }
    }
}
