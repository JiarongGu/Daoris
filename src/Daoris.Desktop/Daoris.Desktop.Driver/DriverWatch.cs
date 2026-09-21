namespace Daoris.Driver;

/// <summary>
/// The watch loop, owned once by the library — the headless host and the desktop shell both ran the
/// same algorithm (re-read the config, tick, wait) as two copies, four six-argument constructions
/// between them. Each host keeps only its reporting half: what a tick report BECOMES (console lines,
/// event-bus notifications) differs by door; when the loop looks does not.
/// </summary>
/// <remarks>
/// The person's standing choices are re-read every tick, so a hold, an opt-in, or a new adapter
/// command takes effect without a restart — the shell's controls are edits to the config file, and a
/// control that needs a bounce is a control nobody trusts.
/// </remarks>
public sealed class DriverWatch(
    ServiceClient service, string configPath, string home, SessionProcesses processes, RemoteSyncSet? sync,
    // The live console, where something is watching (D49 §2). Null in the headless host: a buffer
    // nobody reads is memory spent on an audience that does not exist.
    SessionOutput? output = null,
    // The harness cache (D49 §4), shared across ticks for the same reason the process registry is:
    // a probe spawns a process, and re-detecting every harness every tick would be absurd.
    HarnessRoster? harnesses = null,
    // What sessions consumed (TOOL3). Null in the headless host for the same reason the console
    // buffer is: a record nobody reads is a file written for an audience that does not exist.
    SessionUsage? usage = null)
{
    private CancellationTokenSource _pause = new();

    /// <summary>This machine's harnesses, as the loop sees them — also what a roster surface reads.</summary>
    private readonly HarnessRoster _harnesses = harnesses ?? new HarnessRoster(AdapterSet.Built());

    /// <inheritdoc cref="_harnesses"/>
    public HarnessRoster Harnesses => _harnesses;

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown.</summary>
    public void Nudge()
    {
        // Swap first, atomically, so two racing nudges each cancel a source that is no longer current
        // — the swapped-out source is cancelled, disposed, and never waited on again.
        var paused = Interlocked.Exchange(ref _pause, new CancellationTokenSource());
        paused.Cancel();
        paused.Dispose();
    }

    /// <summary>
    /// Tick until cancelled. <paramref name="onReport"/> receives every tick's report with the config
    /// that produced it; <paramref name="onError"/> decides what a failed tick means — the shell says
    /// so and keeps watching (an unattended loop outlives its service's restarts), while a null lets
    /// the failure propagate, which is the headless host's exit-2 contract.
    /// </summary>
    public async Task RunAsync(
        Func<TickReport, DriverConfig, Task> onReport,
        Func<Exception, Task>? onError,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var config = DriverConfig.Load(configPath);
            try
            {
                var report = await new Driver(
                    service, config, AdapterSet.Built(), home, processes, sync, output, _harnesses, usage)
                    .TickAsync(ct).ConfigureAwait(false);
                await onReport(report, config).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error) when (onError is not null)
            {
                await onError(error).ConfigureAwait(false);
            }

            try
            {
                // Interruptible two ways: cancellation ends the loop; a nudge only ends the wait.
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct, _pause.Token);
                await Task.Delay(TimeSpan.FromSeconds(config.PollSeconds), wait.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested) return;
            }
        }
    }
}
