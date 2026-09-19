using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The driver's watch loop, in the shell's process — the same library the headless host runs, one
/// driver with two doors (D46 §7). In-process because the shell is where the tick reports become
/// something a person sees, and where session process control will live.
/// </summary>
/// <remarks>
/// The person's standing choices are re-read every tick, so an edit to `driver.json` — by hand today,
/// by the platform's controls next — takes effect without a restart. Tick reports go out on the app's
/// event bus as `DAORIS` / `DRIVER_TICK` notifications; a page that never subscribes costs nothing,
/// which is what keeps the browser and the shell one bundle.
/// </remarks>
public sealed class DriverLoop(IEventBus eventBus, HostSupervisor supervisor, string serviceUrl) : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource<bool> _hostReady = new();
    private CancellationTokenSource _pause = new();
    private Task? _loop;

    /// <summary>Completes when the HTTP host answers (or provably will not) — what navigation waits on.</summary>
    public Task<bool> HostReady => _hostReady.Task;

    /// <summary>The live processes, shared across ticks — how "stop that session" reaches its target.</summary>
    public SessionProcesses Processes { get; } = new();

    /// <summary>Where this loop reads the person's choices — what the control surface edits.</summary>
    public string ConfigPath { get; } = DriverConfig.ResolvePath();

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown.</summary>
    public void Nudge()
    {
        var paused = _pause;
        _pause = new CancellationTokenSource();
        paused.Cancel();
    }

    public void Start()
    {
        _loop = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        var ct = _stopping.Token;

        var up = await supervisor.EnsureAsync(ct).ConfigureAwait(false);
        _hostReady.TrySetResult(up);
        if (!up)
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = supervisor.Trouble })
                .ConfigureAwait(false);
            return;
        }

        var home = Path.GetDirectoryName(Path.GetFullPath(ConfigPath))!;
        using var service = new ServiceClient(serviceUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable));

        while (!ct.IsCancellationRequested)
        {
            var config = DriverConfig.Load(ConfigPath);
            try
            {
                var report = await new Daoris.Driver.Driver(service, config, AdapterSet.Built(), home, Processes)
                    .TickAsync(ct).ConfigureAwait(false);

                if (report.PlannedAnything || report.Events.Count > 0)
                {
                    await eventBus.EmitAsync("DAORIS", "DRIVER_TICK", new
                    {
                        Events = report.Events,
                        Considered = report.Considerations.Select(c => new
                        {
                            Quest = c.Quest.Id,
                            Repository = c.Quest.To,
                            Verdict = c.Verdict.ToString(),
                            c.Reason,
                        }).ToArray(),
                    }).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                // An unattended loop outlives its service's restarts — say so, wait, look again.
                await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { error.Message }).ConfigureAwait(false);
            }

            try
            {
                // Interruptible two ways: stopping ends the loop; a nudge only ends the wait.
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct, _pause.Token);
                await Task.Delay(TimeSpan.FromSeconds(config.PollSeconds), wait.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested) return;
            }
        }
    }

    /// <summary>Stop, bounded: an in-flight session is ended and recorded `stopped` by the driver itself.</summary>
    public void Stop()
    {
        _stopping.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(15));
        }
        catch (AggregateException)
        {
            // Cancellation surfacing as it should; the record writes ride an unbound token inside.
        }
    }

    public void Dispose()
    {
        Stop();
        _stopping.Dispose();
    }
}
