namespace Daoris.Knowledge;

/// <summary>
/// The service's memory work, run from its start (KNOWCENTRAL2d; the memory coordinator design §3): the applier's recovery and every
/// <c>ready</c> row at the start, then whatever a commit point releases, read at <see cref="Pace"/> since any process may record one;
/// and, with the index, the sweep at <see cref="SweepPace"/>. A failed pass is told to <paramref name="failed"/> and the next tries again.
/// </summary>
public sealed class MemoryCoordinator(
    MemoryOpStore ops, IMemoryEngines engines, KnowledgeService? knowledge, TimeProvider? time = null, Action<Exception>? failed = null)
{
    /// <summary>How often the log is read for rows released since.</summary>
    public TimeSpan Pace { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How often the sweep rechecks the entries due.</summary>
    public TimeSpan SweepPace { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Runs until <paramref name="ct"/> stops it.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var clock = time ?? TimeProvider.System;
        var applier = new MemoryApplier(ops, engines, time);
        var sweep = knowledge is null ? null : new MemorySweep(ops, engines, knowledge);
        var started = false;
        // The first sweep waits a pace too: a host started for a moment opens no engine it has nothing to apply to.
        var swept = clock.GetUtcNow();
        try
        {
            using var timer = new PeriodicTimer(Pace, clock);
            do
            {
                try
                {
                    if (!started) await applier.StartAsync(ct).ConfigureAwait(false);
                    else await applier.ApplyReadyAsync(ct).ConfigureAwait(false);
                    started = true;
                    if (sweep is not null && clock.GetUtcNow() - swept >= SweepPace)
                    {
                        swept = clock.GetUtcNow();
                        await sweep.RunAsync(swept, ct).ConfigureAwait(false);
                    }
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
