using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// The driver's watch loop, in the shell's process — the same library the headless host runs, one
/// driver with two doors (D46 §7). In-process because the shell is where the tick reports become
/// something a person sees, and where session process control lives. The loop mechanics are
/// <see cref="DriverWatch"/>'s; this class owns only what the shell adds — bringing the host up, and
/// turning reports into event-bus notifications.
/// </summary>
public sealed class DriverLoop(IEventBus eventBus, HostSupervisor supervisor, string serviceUrl) : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource<bool> _hostReady = new();
    private DriverWatch? _watch;
    private Task? _loop;

    /// <summary>Completes when the HTTP host answers (or provably will not) — what navigation waits on.</summary>
    public Task<bool> HostReady => _hostReady.Task;

    /// <summary>The live processes, shared across ticks — how "stop that session" reaches its target.</summary>
    public SessionProcesses Processes { get; } = new();

    /// <summary>
    /// What sessions are saying, as they say it (D49 §2) — shared across ticks for the same reason the
    /// processes are, and readable only through this shell: output is transcript-class material and
    /// never leaves the machine (D47 §4).
    /// </summary>
    public SessionOutput Output { get; } = new();

    /// <summary>Where this loop reads the person's choices — what the control surface edits.</summary>
    public string ConfigPath { get; } = DriverConfig.ResolvePath();

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown.</summary>
    public void Nudge() => _watch?.Nudge();

    public void Start()
    {
        _loop = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        var ct = _stopping.Token;

        // Whatever happens in here, HostReady must complete — the splash awaits it, and a supervisor
        // fault that skipped the TrySetResult would leave a permanently dark window, the exact failure
        // the form's own fallback was written to prevent.
        var up = false;
        string? trouble = null;
        try
        {
            up = await supervisor.EnsureAsync(ct).ConfigureAwait(false);
            trouble = supervisor.Trouble;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            trouble = error.Message;
        }
        finally
        {
            _hostReady.TrySetResult(up);
        }

        if (!up)
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = trouble })
                .ConfigureAwait(false);
            return;
        }

        var home = Path.GetDirectoryName(Path.GetFullPath(ConfigPath))!;
        var key = Environment.GetEnvironmentVariable(ServiceClient.KeyVariable);
        using var service = new ServiceClient(serviceUrl, key);

        // The machine's remotes — one per workspace that has one (D47 §9, D48 §5). The syncs ride the
        // tick, in the shell exactly as in the headless host. Absence is silent and local.
        using var sync = RemoteSyncSet.FromEnvironment(service.BaseUrl, key);

        // Live console lines become IPC events, BATCHED by the library's relay (D49 §2). The shell's
        // half is only what a batch becomes: the page asks for the backlog once over `TAIL_SESSION`
        // and takes everything after it from here.
        using var console = new ConsoleRelay(Output, (session, lines) =>
            eventBus.EmitAsync("DAORIS", "SESSION_OUTPUT", new
            {
                Session = session,
                Lines = lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
            }));

        _watch = new DriverWatch(service, ConfigPath, home, Processes, sync, Output);
        await _watch.RunAsync(
            async (report, _) =>
            {
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
            },
            // An unattended loop outlives its service's restarts — say so, wait, look again.
            onError: error => eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { error.Message }),
            ct).ConfigureAwait(false);
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
