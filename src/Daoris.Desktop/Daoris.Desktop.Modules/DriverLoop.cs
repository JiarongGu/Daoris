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
/// <param name="home">
/// What establishing the install's home did (D63), when this shell is an install and something is
/// worth saying — state moved in, or the variable set for the person's account. Null otherwise.
/// </param>
public sealed class DriverLoop(
    IEventBus eventBus, HostSupervisor supervisor, string serviceUrl, HomeEstablished? home = null) : IDisposable
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

    /// <summary>
    /// What sessions consumed (TOOL3/D57 §4) — machine-local by the same rule as the console and the
    /// profile name it records, and reachable only over this bridge. Its home is the Daoris home,
    /// which is the directory `driver.json` sits in.
    /// </summary>
    public SessionUsage Usage { get; } = new(
        Path.GetDirectoryName(Path.GetFullPath(DriverConfig.ResolvePath()))!);

    /// <summary>
    /// Conversations, once the loop is up (D49 §3) — null before the host answers, because a chat
    /// needs the service that holds its record. The control surface says so rather than failing
    /// obscurely: "not yet" is a state a person can wait out.
    /// </summary>
    public ChatRunner? Chat { get; private set; }

    /// <summary>
    /// The service this loop drives, once the host answers — null before, like <see cref="Chat"/>
    /// and for the same reason.
    /// </summary>
    /// <remarks>
    /// <b>Exposed so the person's own moves go through the same door the loop's do</b> (D52 §4). A
    /// parked session is cleared by a judgement rather than by an observation, and the record and
    /// the process must move together: this machine lets the process go, then advances the record.
    /// A page that advanced the record by itself would leave a `completed` record beside a process
    /// still running here, which is the one lie the observed lifecycle exists to prevent.
    /// </remarks>
    public ServiceClient? Service { get; private set; }

    /// <summary>Where this loop reads the person's choices — what the control surface edits.</summary>
    public string ConfigPath { get; } = DriverConfig.ResolvePath();

    /// <summary>The Daoris home (D63): the directory every machine-local file lives in.</summary>
    public string Home => Path.GetDirectoryName(Path.GetFullPath(ConfigPath))!;

    /// <summary>
    /// What establishing the home did on this start, when it is worth a person's attention — state
    /// moved in from a profile directory, or the account's environment gaining the variable. Carried
    /// in the state as well as raised once, because the page subscribes only after the host answers,
    /// and a one-time sentence raised before that is a sentence nobody read.
    /// </summary>
    public string? HomeNotice => home is { Worth: true } ? home.Notice : null;

    /// <summary>
    /// This machine's harnesses and the accounts they run as (D49 §4) — the roster surface reads it,
    /// and both spawn doors ask it the same question before starting anything.
    /// </summary>
    /// <remarks>
    /// Constructed eagerly, unlike <see cref="Chat"/>: detection needs no service, so a person can see
    /// what is installed and log a profile in while the host is still coming up — which is exactly the
    /// moment a machine being set up has the question.
    /// </remarks>
    public HarnessRoster Harnesses { get; } = new(WithPlugins(AdapterSet.Built()));

    /// <summary>
    /// The build's adapters plus whatever the home's plugins declare (D64) — read at construction so
    /// the roster a person opens before the loop's first tick already knows a declared harness; every
    /// tick re-reads the catalogue and hands the roster the live set.
    /// </summary>
    private static AdapterSet WithPlugins(AdapterSet built) =>
        built.WithPlugins(PluginCatalog.Load(
            Path.GetDirectoryName(Path.GetFullPath(DriverConfig.ResolvePath()))!, built.Names));

    /// <summary>The plugins that speak (D64): their processes live with this loop and stop with it.</summary>
    private HookSet? _hooks;

    /// <summary>Which plugins have a hook process up right now, by id — what the Plugins card shows as running.</summary>
    public IReadOnlyList<string> RunningPlugins => _hooks?.Running ?? [];

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

        // The host is up and it is somebody else's, serving a page that is not this install's (case
        // study 4d). Said once, on the channel a person has to act on — like a parked session, it is
        // waiting on them — and the loop goes on, because the host does work.
        if (supervisor.Notice is { } notice)
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = notice })
                .ConfigureAwait(false);
        }

        // The home's own one-time news (D63) on the same channel, for the same reason: a `~/.daoris`
        // that just moved into the install, or a variable just set for the account, is something the
        // person acts on once — a terminal opened before it was set does not see it.
        if (home is { Worth: true })
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = home.Notice })
                .ConfigureAwait(false);
        }

        var homeDirectory = Path.GetDirectoryName(Path.GetFullPath(ConfigPath))!;
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

        // Conversations share everything the loop has — the same service, the same process registry
        // (so one lock and one "stop" reach both kinds), the same console buffer.
        // …and the same harness roster, so one probe serves both doors and a login the person just
        // did is seen by whichever of them asks next.
        Chat = new ChatRunner(service, Harnesses.Adapters, homeDirectory, Processes, Output, Harnesses);
        Service = service;

        // A plugin's word goes to the console under `plugin:<id>` (D49 §2, D64 §4) — the same buffer
        // a session's lines and a harness action's lines land in, readable only over this bridge.
        _hooks = new HookSet(homeDirectory, Output);

        // What is worth interrupting the person for (SURF5b). The judgement is the library's, so the
        // headless host reaches the same answer; the shell's half is only what an event BECOMES.
        var attention = new AttentionWatch();
        string? lastConsidered = null;

        _watch = new DriverWatch(service, ConfigPath, homeDirectory, Processes, sync, Output, Harnesses, Usage, _hooks);
        await _watch.RunAsync(
            async (report, ticked) =>
            {
                // Observed either way, so turning notifications back on does not then announce
                // everything that happened while they were off — the switch is about being TOLD.
                var attend = attention.Observe(report);
                if (ticked.Notify)
                {
                    foreach (var item in attend)
                    {
                        await eventBus.EmitAsync("DAORIS", "SESSION_ATTENTION", new
                        {
                            Kind = item.Kind.ToString(),
                            item.Session,
                            item.Repository,
                            item.State,
                            item.Headline,
                            item.Detail,
                        }).ConfigureAwait(false);
                    }
                }

                // Forwarded when the tick has something to say: it did something, or what it says
                // about the sitting quests CHANGED. The considerations are a standing answer the
                // Overview renders — "why is this sitting" (D46 §3) — so a change has to reach the
                // page, and a stable set must not: every tick the page receives refetches four
                // queries, and a machine with one held quest would otherwise send one every poll.
                var considered = Considerations.Signature(report.Considerations);
                var changed = considered != lastConsidered;
                lastConsidered = considered;
                if (report.PlannedAnything || report.Events.Count > 0 || changed)
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

        // The plugins' processes go with the loop (D64 §4, rule 3): registrations are effects, and
        // the effect ends here. Bounded per plugin by the set itself; a plugin that will not leave is
        // ended rather than waited for.
        var hooks = Interlocked.Exchange(ref _hooks, null);
        if (hooks is not null)
        {
            try
            {
                hooks.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(10));
            }
            catch (AggregateException)
            {
                // A plugin that failed while leaving is a plugin that has left.
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _stopping.Dispose();
    }
}
