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
/// worth saying — state moved in, the variable set for the person's account, or the account's home
/// overridden by the install's own (D105). Null otherwise.
/// </param>
public sealed class DriverLoop(
    IEventBus eventBus, HostSupervisor supervisor, string serviceUrl, HomeEstablished? home = null,
    // Daoris's own browser (D78): the shell's window, asked for by a plugin server that drives it.
    IInAppBrowser? browser = null,
    // The shell's machine log (LOG1b, D94): what the person runs and how long it takes. Null writes none.
    MachineLog? log = null,
    // The account's own DAORIS_HOME, which a terminal reads (LEFT2); the user environment's by default, and a
    // test's stand-in so a test never reads the machine's.
    Func<string?>? account = null) : IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource<bool> _hostReady = new();
    private DriverWatch? _watch;
    private Task? _loop;

    /// <summary>Completes when the HTTP host answers (or provably will not) — what navigation waits on.</summary>
    public Task<bool> HostReady => _hostReady.Task;

    /// <summary>
    /// The shell's machine log (D94), or null where none was handed in — for a route that writes what the person
    /// did through it, as the preview's opening is (LEFT2).
    /// </summary>
    public MachineLog? Log => log;

    /// <summary>The live processes, shared across ticks — how "stop that session" reaches its target.</summary>
    /// <remarks>
    /// Marked under the home's <c>sessions/</c>, so a terminal's driver sharing the home can tell this
    /// loop's live sessions from orphans, and this loop can tell its (2026-09-25).
    /// </remarks>
    public SessionProcesses Processes { get; } = new(Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(DriverConfig.ResolvePath()))!, "sessions"));

    /// <summary>
    /// What sessions are saying, as they say it (D49 §2) — shared across ticks for the same reason the
    /// processes are, and readable only through this shell: output is transcript-class material and
    /// never leaves the machine (D47 §4).
    /// </summary>
    public SessionOutput Output { get; } = Announcing(new SessionOutput(), eventBus);

    /// <summary>
    /// A stream opening or ending (CONSOLE2c) becomes the page's <c>SESSION_STREAMS</c> event. It names
    /// the session and nothing else: the page asks for the list, so a missed event costs nothing.
    /// </summary>
    private static SessionOutput Announcing(SessionOutput output, IEventBus bus)
    {
        output.Streamed += session => _ = bus.EmitAsync("DAORIS", "SESSION_STREAMS", new { Session = session });
        return output;
    }

    /// <summary>
    /// What sessions did, as typed events (D76 §2) — the conversation the page renders, kept under the
    /// home beside the transcripts so it outlives the console's window and a restart. Machine-local by
    /// the same rule, and reachable only over this bridge.
    /// </summary>
    public SessionEvents Events { get; } = new(Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(DriverConfig.ResolvePath()))!, "sessions"));

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
    public string Home => DriverConfig.HomeOf(ConfigPath);

    /// <summary>
    /// What establishing the home did on this start, when it is worth a person's attention — state
    /// moved in from a profile directory, the account's environment gaining the variable, or the
    /// account's home set aside for this install's own (D105), which lasts as long as the variable
    /// names the other folder, so it is said on every such start. Carried
    /// in the state as well as raised once, because the page subscribes only after the host answers,
    /// and a one-time sentence raised before that is a sentence nobody read.
    /// </summary>
    public string? HomeNotice => home is { Worth: true } ? home.Notice : null;

    /// <summary>
    /// How <see cref="Home"/> stands to the account's DAORIS_HOME (LEFT2), a <see cref="Desktop.HomeAccount"/> value: the
    /// same folder, overridden by this install's own, or named for this start alone. Settings → Driver's home hint
    /// reads it to say whether a terminal's daoris reads this folder, where it read the notice's English before —
    /// and a start named for itself alone has no notice at all.
    /// </summary>
    public string HomeAccount => InstallHome.AccountOf(Home, home, (account ?? InstallHome.AccountVariable)());

    /// <summary>
    /// The host the shell adopted is serving a page that is not this install's (case study 4d), or
    /// null. Carried in the state for the same reason the home's notice is: it was a toast, and a
    /// toast raised before the page subscribed — which is exactly when adoption is decided — reached
    /// nobody. The one surface item the second deployment left unfiled.
    /// </summary>
    public string? HostNotice => supervisor.Notice;

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

    /// <summary>
    /// The loop's own record of each plugin's health (PLUGUI1d, D119 §2): fed by its hook set and by every landing and
    /// hand-off a route runs in this process, and read for the Plugins view, which a terminal reads from the log instead.
    /// </summary>
    public PluginHealth Health { get; } = new();

    /// <summary>Which plugins have a hook process up right now, by id — what the Plugins card shows as running.</summary>
    public IReadOnlyList<string> RunningPlugins => _hooks?.Running ?? [];

    /// <summary>Stop one plugin's hook process now — before its folder is removed (REV3).</summary>
    public Task<bool> StopPluginAsync(string id) => _hooks?.StopAsync(id) ?? Task.FromResult(false);

    /// <summary>Look now rather than at the next poll — a control that just changed something should
    /// not leave the person watching a countdown.</summary>
    public void Nudge() => _watch?.Nudge();

    /// <summary>
    /// A session's console lines, as the page's one event for them. The shape is the page's contract,
    /// so one writer builds it: the relay's batches and a harness action's lines alike (REV3 CLEAN1).
    /// </summary>
    /// <param name="live">
    /// Whether the console still runs as the batch goes out: a session's last words are written after it
    /// closed, and a batch the page read as live re-marked a finished session so (CONSOLE3c).
    /// </param>
    internal static Task EmitOutput(IEventBus bus, string session, IEnumerable<ConsoleLine> lines, bool live = true) =>
        bus.EmitAsync("DAORIS", "SESSION_OUTPUT", new
        {
            Session = session,
            Lines = lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
            Live = live,
        });

    /// <summary>
    /// What the last tick held for the harness's trust (D73) — the only grants the screen may confirm.
    /// </summary>
    public TrustHolds Trust { get; } = new();

    /// <summary>
    /// What the last tick parked by its strikes (DRV6) — the verdict the quest drawer shows its Retry by, and what
    /// Ask Daoris's <c>retry</c> is judged against and its room lists (HELP10).
    /// </summary>
    public ParkedQuests Parked { get; } = new();

    /// <summary>
    /// Everything the last tick considered (SESSUX1a): the planner's verdicts Sessions' groups read, so a session's
    /// *parked* or *awaiting reply* is what the loop decided. Null before the first tick, when a fresh plan is read.
    /// </summary>
    public LastLook Look { get; } = new();

    /// <summary>The loop's syncs, once it is up — the ones its tick runs, and the ones *Sync now* runs.</summary>
    private RemoteSyncSet? _sync;

    /// <summary>
    /// One circle's pass now (SYNC6b): the tick's own pass through the loop's own sync set, so the
    /// screen's *Sync now* and `daoris-driver sync` run the same thing (D50). Null before the loop is up.
    /// </summary>
    /// <exception cref="DriverException">The circle has no remote on this machine.</exception>
    public Task<SyncReport>? SyncNowAsync(string workspace, CancellationToken ct) =>
        _sync?.RunOnceAsync(workspace, ct);

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

        // The home's own news (D63) on the same channel, for the same reason: a `~/.daoris` that just
        // moved into the install, or a variable just set for the account, is something the person acts
        // on once — a terminal opened before it was set does not see it. And an account's home this
        // install set aside for its own (D105): a terminal still reads the other folder.
        if (home is { Worth: true })
        {
            await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = home.Notice })
                .ConfigureAwait(false);
        }

        var homeDirectory = Home;
        var key = Environment.GetEnvironmentVariable(ServiceClient.KeyVariable);
        using var service = new ServiceClient(serviceUrl, key);

        // Every session this loop and its conversations open, move and hear, timed into the machine log
        // (LOG1b) — declared after the client and before the chat runner, so it hears the runner's
        // closing moves and lets go before the client does.
        using var sessionLog = log is null ? null : new SessionLog(log, service, Events);

        // The machine's remotes — one per workspace that has one (D47 §9, D48 §5). The syncs ride the
        // tick, in the shell exactly as in the headless host. Absence is silent and local.
        using var sync = RemoteSyncSet.FromEnvironment(service.BaseUrl, key);
        _sync = sync;

        // Live console lines become IPC events, BATCHED by the library's relay (D49 §2). The shell's
        // half is only what a batch becomes: the page asks for the backlog once over `TAIL_SESSION`
        // and takes everything after it from here.
        using var console = new ConsoleRelay(Output, (session, lines) => EmitOutput(eventBus, session, lines, Output.IsLive(session)));

        // The conversation's events, the same way (D76, CONV1): the page reads the history once over
        // `SESSION_HISTORY` and takes everything after it from here, asking for a gap it notices.
        using var conversation = new EventRelay(Events, (session, events) =>
            eventBus.EmitAsync("DAORIS", "SESSION_EVENTS", new { Session = session, Events = events.ToArray() }));

        // Conversations share everything the loop has — the same service, the same process registry
        // (so one lock and one "stop" reach both kinds), the same console buffer.
        // …and the same harness roster, so one probe serves both doors and a login the person just
        // did is seen by whichever of them asks next.
        // 🔴 Declared AFTER the client, so it is disposed BEFORE it: disposing the runner ends each
        // conversation and writes its record through this client, and the language's reverse order is
        // what guarantees the client is still there. Stopped after the client, a chat open at close was
        // recorded nowhere and read `working` forever (2026-09-25).
        // …and the same usage record, so a conversation's end and a driven session's are one writer (USAGE1).
        // …and the same log, where each plugin server a conversation is handed or withheld is a line (PLUGUI1e).
        using var chat = new ChatRunner(
            service, Harnesses.Adapters, homeDirectory, Processes, Output, Harnesses, Events, Usage, browser,
            plugins: new PluginLog(log, Health));
        // Where a conversation's turns stand, as it moves (CONV4a): whether one is in flight, and what is
        // waiting, which is in no record until it is sent. Each change is the whole state, so a missed one
        // costs nothing.
        chat.QueueChanged += (session, queue) =>
            _ = eventBus.EmitAsync("DAORIS", "SESSION_QUEUED", new
            {
                Session = session,
                // The words and the names of their files — never where the files are kept.
                Queued = queue.Queued.Select(message => new { message.Text, Files = message.Files.Select(file => file.Name).ToArray() }).ToArray(),
                queue.Taking,
                // Whether what waits, waits for the door to open rather than for a turn (HELP4).
                queue.Opening,
                // When its last turn ended here (RAIL2): the chat's last move, which its record never
                // says. Machine-local, like everything this event carries.
                LastTurn = queue.LastTurnEnded,
            });
        // A conversation's model and effort each time they change (AGT6b, D98), in the shape `SESSION_OPTIONS`
        // answers with, so a page that asked once follows them. Machine-local, like the queue.
        chat.OptionsChanged += (session, options) =>
            _ = eventBus.EmitAsync("DAORIS", "SESSION_OPTIONS_CHANGED", DriverModule.OptionsAnswer(session, options));
        // What a person told a driven session, waiting for its turn to end (SESS3), told the same way —
        // and `Listening` false once it stops taking any, so the page takes its box away.
        Processes.HeldChanged += (session, queue) =>
            _ = eventBus.EmitAsync("DAORIS", "SESSION_QUEUED", new
            {
                Session = session,
                Queued = queue.Queued.Select(message => new { message.Text, Files = Array.Empty<string>() }).ToArray(),
                queue.Taking,
                // An open inbox always has a turn in hand; a closed one tells Idle.
                Listening = queue.Taking,
            });
        Chat = chat;
        await ComeUpAsync(service).ConfigureAwait(false);

        // A plugin's word goes to the console under `plugin:<id>` (D49 §2, D64 §4) — the same buffer
        // a session's lines and a harness action's lines land in, readable only over this bridge.
        // What the set does with each plugin goes to the machine log without its words, and into the loop's
        // record of its health (PLUGUI1d).
        _hooks = new HookSet(homeDirectory, Output, log: log, health: Health);

        // What is worth interrupting the person for (SURF5b). The judgement is the library's, so the
        // headless host reaches the same answer; the shell's half is only what an event BECOMES.
        // One live driver per home (DRV8a, D104): the loop waits for the home's lock while a headless loop
        // holds it, and everything above — the host, the page, the conversations — carries on meanwhile.
        using var held = await HoldHomeAsync(ct).ConfigureAwait(false);
        if (held is null) return;

        var attention = new AttentionWatch();
        string? lastConsidered = null;
        string? lastAsked = null;
        string? lastActive = null;
        string? lastRegistered = null;
        var failures = new TickErrors();

        _watch = new DriverWatch(service, ConfigPath, homeDirectory, Processes, sync, Output, Harnesses, Usage, _hooks, Events, browser);
        await _watch.RunAsync(
            async (report, ticked) =>
            {
                failures.Ran();

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

                // What this tick held for trust, kept for the screen's grant to be checked against
                // (D73) — replaced whole, so a folder the driver stopped holding cannot be granted.
                Trust.Record(report.Untrusted);

                // And what it parked by its strikes (HELP10), replaced whole the same way, so Ask Daoris proposes a
                // retry only of a quest the drawer would offer Retry on.
                Parked.Record(report.Considerations);

                // And all of it, whole, for the session list's groups (SESSUX1a): parked and awaiting reply are its verdicts.
                Look.Record(report.Considerations);

                // 🔴 And the asks (INT4d): the attention band reads them beside the sessions, and an
                // ask made by the other door — a terminal, a teammate's sync — moves nothing above,
                // so a quiet tick never told the page and the band missed it until a reload. Read
                // here because the tick reads asks only when an intake is named. A host that answers
                // no ask door changes nothing.
                var asked = await AskedAsync(service, ct).ConfigureAwait(false);
                if (asked is not null && asked != lastAsked)
                {
                    changed = true;
                    lastAsked = asked;
                }

                // 🔴 And the sessions (UX5 U13), for the same reason: a conversation started or ended
                // from the main window moves nothing this tick reports, so the monitor and a detached
                // window, which hear of sessions only by a tick, never showed it. Seen on the window: a
                // chat opened in `game`, and a minute later the monitor still did not have it.
                var active = await ActiveAsync(service, ct).ConfigureAwait(false);
                if (active is not null && active != lastActive)
                {
                    changed = true;
                    lastActive = active;
                }

                // 🔴 And the registry (FG4): a folder imported from a terminal, a retire, a re-wire —
                // none moves anything above, and the page said *no workspace yet* over 29 repositories
                // just registered, until it was reloaded.
                var registered = await RegisteredAsync(service, ct).ConfigureAwait(false);
                if (registered is not null && registered != lastRegistered)
                {
                    changed = true;
                    lastRegistered = registered;
                }

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
                        // The trust holds as facts (D73): machine-local paths, so over this bridge only.
                        // A hold's `quest` or `ask`, whichever it is not, is left out by the bridge.
                        Untrusted = report.Untrusted.Select(hold => new
                        {
                            hold.Folder,
                            hold.TrustFile,
                            hold.Quest,
                            hold.Ask,
                        }).ToArray(),
                    }).ConfigureAwait(false);
                }
            },
            // An unattended loop outlives its service's restarts — say so, wait, look again. Said once
            // until a tick runs again, and a failure it can name is named (UX5 U30).
            onError: error => failures.Failed(error) is { } said
                ? eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { said.Code, said.Message })
                : Task.CompletedTask,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The loop's service, handed to the routes, and the page told (LOOK2a): until now every route that reads the
    /// service refused *still coming up*, and from now it answers.
    /// </summary>
    /// <remarks>
    /// <para><b>The page asks again on this, not on a tick.</b> A page opened on a screen that reads the service asks at
    /// once, is refused, and until LOOK2a kept that refusal until something else asked again: the first tick, which can
    /// wait on a remote's sync or on another driver's lock (DRV8a). Settings → Workspace said no repository had a line
    /// until *Bring up to date* happened to ask again. <c>DRIVER_READY</c> is said once, the moment the refusals stop.</para>
    ///
    /// <para>The service holds its registry the moment it answers at all (its store is read on each ask), so the
    /// service being handed over is the whole of being ready. Called by the loop once its host answers; public so the
    /// tests hold what the page is told.</para>
    /// </remarks>
    public async Task ComeUpAsync(ServiceClient service)
    {
        Service = service;
        await eventBus.EmitAsync("DAORIS", "DRIVER_READY", new { Ready = true }).ConfigureAwait(false);
    }

    /// <summary>How often a loop waiting on another driver's lock looks again (DRV8a).</summary>
    public TimeSpan HoldRetry { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// This home's driver lock, taken for the desktop's loop (DRV8a, D104). While another live driver
    /// holds it — a terminal's `daoris-driver drive` — the loop waits, and says so once, on the channel a
    /// person acts on; it takes the lock the moment that one lets go. Null when the shell closes first.
    /// </summary>
    /// <remarks>
    /// Waited for rather than refused, as a terminal's loop is: the desktop is where a person drives from,
    /// and nothing else it carries races anyone for a quest.
    /// </remarks>
    public async Task<DriverLock?> HoldHomeAsync(CancellationToken ct)
    {
        var told = false;
        while (true)
        {
            DriverLock? held;
            string? said;
            try
            {
                held = DriverLock.TryAcquire(Home, DriverKind.Desktop, out var holder);
                said = holder is null
                    ? null
                    : $"The driver loop is waiting: this home is already driven by {holder.Named}. It starts "
                      + "here the moment that one stops.";
            }
            catch (DriverException error)
            {
                held = null;
                said = error.Message;
            }

            if (held is not null) return held;
            if (!told && said is not null)
            {
                await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = said }).ConfigureAwait(false);
                told = true;
            }

            try
            {
                await Task.Delay(HoldRetry, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }

    /// <summary>What the asks say this tick, or null when the host could not answer — never a change.</summary>
    private static async Task<string?> AskedAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return Asks.Signature(await service.AsksAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>What the registry holds, or null when the host could not answer — never a change.</summary>
    private static async Task<string?> RegisteredAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return Repositories.Signature(await service.RegistryAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Which sessions are active and how, or null when the host could not answer — never a change.</summary>
    private static async Task<string?> ActiveAsync(ServiceClient service, CancellationToken ct)
    {
        try
        {
            return ActiveSessions.Signature(await service.ActiveSessionsAsync(ct).ConfigureAwait(false));
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Stop, bounded: an in-flight session is ended and recorded `stopped` by the driver itself, and so is
    /// a conversation — the loop's run ends by disposing its chat runner, inside its client's scope.
    /// </summary>
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

/// <param name="Code">A failure the page words from its catalogue, or null when the words are the driver's.</param>
/// <param name="Message">The driver's own sentence, which a page that does not know the code shows.</param>
public sealed record TickError(string? Code, string Message);

/// <summary>
/// What a failed tick tells the page (UX5 U30): said once until a tick runs again, and a failure the
/// driver can name is named.
/// </summary>
/// <remarks>
/// <para>🔴 With the machine's service stopped, the shell toasted the .NET socket's own words on every
/// poll, <i>No connection could be made because the target machine actively refused it.
/// (127.0.0.1:5188)</i>, beside the page's own sentence for the same fact. A refused connection is
/// <c>SERVICE_UNREACHABLE</c>, the code the page's own requests use, so both doors say one sentence
/// in the reader's language. A tick report is a log and a toast is an interruption (D62), so the same
/// failure again is not news.</para>
///
/// <para>Called by the watch loop alone, one tick at a time, so it holds no lock.</para>
/// </remarks>
public sealed class TickErrors
{
    private string? _last;

    /// <summary>The failure to tell the page, or null when it is the one already told.</summary>
    public TickError? Failed(Exception error)
    {
        var said = error is HttpRequestException { StatusCode: null }
            ? new TickError("SERVICE_UNREACHABLE", "the service is not answering — the driver looks again in a few seconds.")
            : new TickError(null, error.Message);
        if (said.Message == _last) return null;
        _last = said.Message;
        return said;
    }

    /// <summary>A tick ran, so the next failure is news again.</summary>
    public void Ran() => _last = null;
}
