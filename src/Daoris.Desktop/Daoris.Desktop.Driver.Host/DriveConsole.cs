namespace Daoris.Driver.Host;

/// <summary>
/// `daoris drive`: the headless loop, which watches a service, starts sessions where the person opted in, and prints every verdict. The
/// entry point catches what it throws and says it as an exit code.
/// </summary>
internal static class DriveConsole
{
    /// <param name="log">The host's machine log.</param>
    /// <param name="closing">The loop's close, cancelled only by the person's Ctrl+C, which is registered here once a loop is asked for.</param>
    /// <param name="entryPoint">The watch of the entry point, told when the loop's start is over.</param>
    public static async Task<int> RunAsync(string[] args, MachineLog log, CancellationTokenSource closing, EntryPointWatch entryPoint)
    {
        // 🔴 The loop only by its verb: a bare invocation, or a word nobody answers, is the
        // usage and starts nothing.
        if (DriverCommand.Read(args, out var problem) is not { } loop)
        {
            var help = DriverCommand.AskedForHelp(args);
            if (problem is not null) Console.Error.WriteLine($"daoris: {problem}");
            (help ? Console.Out : Console.Error).WriteLine(DriverCommand.Usage);
            return help ? 0 : 2;
        }

        // This process is the headless loop, and its run of the desktop store says so; a move it claims names that run.
        StoreProcess.Kind = StoreProcess.Host;

        var once = loop.Mode == LoopMode.Once;
        var untilIdle = loop.Mode == LoopMode.UntilIdle;

        // 🔴 Ctrl+C ends the loop, not the process: killed outright, this host would leave every session
        // it ran working in its record and its agent running on. Cancelled, each session is ended and says
        // so, as the desktop's close does. Registered here, after the subcommands: `chat` has its own.
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            closing.Cancel();
        };

        var configPath = DriverConfig.ResolvePath();
        var config = DriverConfig.Load(configPath);
        // The home a verb's lease is taken in too (`DriverConfig.FindHome`): the folder this config is in.
        var home = DriverConfig.HomeOf(configPath);

        // One live driver per home, taken before anything reaches the service: a second loop
        // races the first for the same quests, so it is refused naming the first — a refusal, exit 1, not a
        // tool error — unless asked to share. The lock is the home's lease: an act holding it
        // (a terminal's verb, the first-start move, the launcher's swap) is waited for up to thirty seconds, as a verb waits,
        // and only then refused; `--share` runs beside an instance, never beside an act.
        var held = loop.Share
            ? HomeLease.Share(home, LeaseKind.Headless, out var holder)
            : HomeLease.TryAcquire(home, LeaseKind.Headless, out holder);
        if (held is null && holder is { IsInstance: false })
        {
            held = HomeLease.Await(home, LeaseKind.Headless, null, HomeLease.Wait, out holder, besideInstance: loop.Share);
        }

        if (held is null)
        {
            Console.Error.WriteLine($"driver: {(holder!.IsInstance ? HomeLease.Refusal(home, holder) : HomeLease.WaitRefusal(home, holder))}");
            return 1;
        }

        using var leaseHeld = held;

        if (held.Beside is { } beside)
        {
            // Only the lease's holder lands done work automatically, so this loop lands none.
            Console.WriteLine(
                $"driver: running beside {beside.Named}, as --share asked — the two loops race for the same quests, and the take decides. "
                + "Done work lands automatically from that one alone, which holds the home.");
        }

        var started = DateTimeOffset.UtcNow;
        log.Info("app.started", ("mode", once ? "once" : untilIdle ? "until-idle" : "watch"));
        // Marked under the home, so the desktop sharing it can tell this host's sessions from orphans.
        var processes = new SessionProcesses(DaorisHome.Entry(home, HomeEntry.SessionPids));

        using var service = ServiceClient.FromEnvironment();

        // What this host runs and how long each part takes, into its log: the watcher hears the
        // client's opens and moves and the record's events, so the record is handed to every driver below.
        var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
        using var sessionLog = new SessionLog(log, service, events);
        // The service's journal is read from this process's come-up, so `session.ended` is written for every end, whoever wrote it.
        // This host also has its own teller of the driven work's news, from a cursor it keeps, so a park made just before a restart is told once after it.
        var news = new News(home);
        // The plugins that speak: their processes live as long as this host does, and are stopped
        // with it. Their diagnostics have no console buffer here, so they go to stderr under their name.
        // What the set does with each, without their words, goes to this host's machine log.
        await using var hooks = new HookSet(home, say: (id, line) => Console.Error.WriteLine($"plugin:{id}  {line}"), log: log, hand: PlaceHands.For(home));
        var hostNews = new NewsTeller(
            news, "host", row => !row.Marks.Contains("chat", StringComparer.Ordinal), () => DateTimeOffset.UtcNow,
            async pieces =>
            {
                foreach (var piece in pieces)
                {
                    var record = await NewsAttention.ReadAsync(service, piece, closing.Token);
                    // The driven work's parks and ends told at `work/event` as they are claimed.
                    if (WorkEventNews.Of(piece, record) is { } happened) hooks.Tell(happened);
                    var item = NewsAttention.Of(piece, record);
                    // Kept in Notifications whatever the switch says: the desktop's windows read the record again and say what this host wrote.
                    Notices.Keep(home, [item], DateTimeOffset.UtcNow, why => log.Warn("notices.refused", ("why", why)));
                    if (NotifyRule.Tells(item, NotifyRule.SwitchOn(configPath))) Console.WriteLine($"  !  {item.Line}");
                }
            })
        {
            Incomplete = batch => log.Warn("news.incomplete", ("teller", "host"), ("store", batch.Store), ("floor", batch.Floor)),
            Refused = why => log.Warn("news.refused", ("teller", "host"), ("why", why)),
        };
        _ = Task.Run(
            () => JournalFeed.RunAsync(
                service,
                [
                    // The quests' moves first, its sink never waiting.
                    WorkEventNews.Gapped(new WorkEventTeller(hooks.Tell).Teller, hooks.Tell, () => DateTimeOffset.UtcNow),
                    new JournalTeller(sessionLog.TellAsync),
                    WorkEventNews.Gapped(
                        new JournalTeller(hostNews.TellAsync, () => news.Cursor("host"), position => news.Advance("host", position)), hooks.Tell, () => DateTimeOffset.UtcNow),
                ],
                log, closing.Token),
            closing.Token);
        // No session is ended on time; --quiet-end ends one of this run unheard that long, and says so.
        if (loop.QuietEnd is { } quietEnd)
        {
            SessionWatch.Of(events).EndAfter = TimeSpan.FromMinutes(quietEnd);
            Console.WriteLine($"driver: --quiet-end {quietEnd}: a session of this run unheard for {quietEnd} minutes is ended and carried on.");
        }
        else if (once || untilIdle)
        {
            Console.WriteLine("driver: no session is ended on time; a quiet one is said here, and this run waits for it (--quiet-end <minutes> ends one).");
        }

        // The watch, once it runs, so a terminal's words a loop keeps are taken up at once rather than at its next look.
        DriverWatch? watching = null;

        // Every loop on the home watches the requests a terminal's `sessions stop|finish|decline` writes,
        // and acts on one for a session this host runs as the screen's route would. A terminal's `sessions say` is held
        // at the door of a session this host runs, or kept on the record of one nothing here runs, shown in this host's own
        // record of it, and the look nudged.
        await using var requests = new SessionRequestWatch(home, processes, () => service)
        {
            Say = new LoopWords(processes, () => service) { Events = events, Nudge = session => watching?.Said(session), Home = home }.HoldAsync,
            // A terminal's `sessions take-back` settles the word in this host's own record of the conversation.
            TakeBack = new LoopWords(processes, () => service) { Events = events, Nudge = session => watching?.Said(session) }.TakeBackAsync,
            // Each look the folder woke, with its ms and why.
            Log = log,
        };

        // The machine's remotes, one per workspace that has one ($DAORIS_HOME/remotes.json, environment
        // overriding): the syncs ride the tick, so a headless driver on a server machine
        // feeds and mirrors exactly as the desktop does. Absence is silent and local, and the map is
        // re-read every pass, so a remote wired later syncs without a restart.
        using var sync = RemoteSyncSet.FromEnvironment(
            service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable));
        if (sync.Workspaces.Count > 0)
        {
            Console.WriteLine(
                $"driver: syncing each tick with the remotes for {string.Join(", ", sync.Workspaces)}");
        }

        if (config.Drivable.Count == 0)
        {
            Console.WriteLine($"driver: nothing is opted in — name repositories under \"drivable\" in {configPath}");
        }

        // The loop's start is over: its first look begins, and what ends the driver from here is `unhandled`.
        entryPoint.Running();

        if (once)
        {
            // One look, then what it started to its end: a look does not wait for its sessions, and a
            // single run that let go of them would leave them working with nothing watching. Each part is printed
            // as it is said, each pass beside the sessions as it ends, so a stop or an ending already
            // made is never lost with the process.
            await new Driver(service, config, ProviderRegistry.Built(), home, processes, sync, hooks: hooks, events: events) { Log = log }
                .RunOnceAsync(closing.Token, said: report => Print(report));
        }
        else if (untilIdle)
        {
            // Each look is printed as it ends, and what ended before a failure or a close lets go; a look that
            // failed still prints what it had said.
            await new Driver(service, config, ProviderRegistry.Built(), home, processes, sync, hooks: hooks, events: events) { Log = log }
                .RunUntilIdleAsync(closing.Token, said: report => Print(report));
        }
        else
        {
            Console.WriteLine($"driver: watching {service.BaseUrl} every {config.PollSeconds}s — Ctrl+C stops it");

            // What is worth interrupting a person for. A machine with no screen cannot toast
            // and still has to answer the question, so the judgement is the library's and this
            // door's delivery is a line — the same shape as everything else this host prints.
            var attention = new AttentionWatch();
            // A quest's park is said with its last session's facts, read from the records when the parks change.
            var parks = new QuestParkReader();

            // The loop itself — re-read the config, tick, wait — is the library's (DriverWatch); this host
            // keeps only its reporting half. A null onError lets a failed tick propagate to the catch
            // below, which is this door's exit-2 contract; what that look had said before it failed, a
            // stop among it, is printed first.
            // The machine log rides each look, where a run's failure nothing else awaits is written with its place.
            HomeSeed.Once(configPath, home);
            watching = new DriverWatch(service, configPath, home, processes, sync, hooks: hooks, events: events) { Log = log, Share = held.Beside is not null };
            await watching.RunAsync(
                async (report, ticked) =>
                {
                    Print(report, quietWhenIdle: true);
                    // Observed either way, so turning notifications back on does not then announce
                    // everything that happened while they were off — the switch is about being TOLD.
                    var read = await parks.LookAsync(
                        report, ticked.ForgivenAt, token => service.SessionList.Kept.JsonAsync(token), closing.Token);
                    // The look's own news, told once across restarts by the said-set and not by a first look's baseline.
                    var events = AttentionNews.Told(news, attention.Observe(report, read), DateTimeOffset.UtcNow);
                    // A quest's park told at `work/event` as the person is told it.
                    foreach (var parked in events) if (WorkEventNews.Of(parked, DateTimeOffset.UtcNow) is { } happened) hooks.Tell(happened);
                    // Kept in Notifications whatever the switch says: the desktop's windows
                    // read the record again and say what this host wrote; a write refused is logged and dropped.
                    Notices.Keep(home, events, DateTimeOffset.UtcNow, why => log.Warn("notices.refused", ("why", why)));
                    foreach (var item in events.Where(each => NotifyRule.Tells(each, ticked.Notify))) Console.WriteLine($"  !  {item.Line}");
                },
                onError: null,
                closing.Token,
                said: report => Print(report));
            Console.WriteLine("driver: stopped — every session it ran was ended and recorded.");
        }

        log.Info("app.stopped", ("uptimeSeconds", (long)(DateTimeOffset.UtcNow - started).TotalSeconds));
        return 0;
    }

    private static void Print(TickReport report, bool quietWhenIdle = false)
    {
        if (quietWhenIdle && !report.PlannedAnything && report.Events.Count == 0) return;

        foreach (var consideration in report.Considerations)
        {
            var verdict = consideration.Verdict == StartVerdict.Start ? "start" : "sitting";
            Console.WriteLine($"  {verdict,-8} #{consideration.Quest.Id} → {consideration.Quest.Address} — {consideration.Reason}");
        }

        foreach (var line in report.Events)
        {
            Console.WriteLine($"  {line}");
        }
    }
}
