namespace Daoris.Knowledge;

/// <summary>
/// The service's memory work, run from its start (KNOWCENTRAL2d; the memory coordinator design §3): the applier's recovery and every
/// <c>ready</c> row at the start, then whatever a commit point releases, read at <see cref="Pace"/> since any process may record one.
/// A failed pass is told to <paramref name="failed"/> and the next pass tries again.
/// </summary>
public sealed class MemoryCoordinator(
    MemoryOpStore ops, IMemoryEngines engines, KnowledgeService? knowledge, TimeProvider? time = null, Action<Exception>? failed = null)
{
    /// <summary>How often the log is read for rows released since.</summary>
    public TimeSpan Pace { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Runs until <paramref name="ct"/> stops it.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var applier = new MemoryApplier(ops, engines, time);
        var started = false;
        try
        {
            using var timer = new PeriodicTimer(Pace, time ?? TimeProvider.System);
            do
            {
                try
                {
                    if (!started) await applier.StartAsync(ct).ConfigureAwait(false);
                    else await applier.ApplyReadyAsync(ct).ConfigureAwait(false);
                    started = true;
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    failed?.Invoke(error);
                }
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The service stops.
        }
    }
}
