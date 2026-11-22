namespace Daoris.Driver;

/// <summary>
/// The quest list as the last move left it, so each wake of the quests' family names what it changed (<see cref="QuestMoves.Between"/>):
/// read at the wake, one wake at a time, so the facts are teed in the order the lists were read.
/// </summary>
internal sealed class QuestMoveReader(Func<CancellationToken, Task<IReadOnlyList<QuestView>>> read)
{
    private readonly SemaphoreSlim _one = new(1, 1);
    private IReadOnlyList<QuestView>? _last;

    /// <summary>Tee the quests' move over <paramref name="bus"/> with the names it read; the unnamed fact where the list cannot be read.</summary>
    public async Task TeeAsync(BusLog bus, IReadOnlyList<string> families, CancellationToken ct)
    {
        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            QuestMoveNames? names = null;
            try
            {
                var now = await read(ct).ConfigureAwait(false);
                names = QuestMoves.Between(_last, now);
                _last = now;
            }
            catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
            {
                _last = null;
            }

            // A fact the log did not take loses its names: the next wake then names nothing, so the gate judges every owed opinion once.
            if (!bus.Tee(() => OpinionBusFacts.QuestMoved(families, names))) _last = null;
        }
        finally
        {
            _one.Release();
        }
    }
}
