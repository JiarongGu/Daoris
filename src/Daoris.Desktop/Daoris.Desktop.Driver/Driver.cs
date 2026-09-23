using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A session this driver brought to an end, and whose decision that was (SURF5b).
/// </summary>
/// <param name="ByPerson">
/// 🔴 True when the person asked for this ending — they pressed stop, or they closed the driver.
/// The one fact that separates an interruption worth making from a toast telling somebody what they
/// just pressed (design §4), and the driver is the only place that knows it.
/// </param>
/// <param name="Quest">The quest it served, for a listener that keys on the ask rather than the session (D64).</param>
/// <param name="Adapter">The harness it ran on, as the record names it.</param>
/// <param name="Account">The credential profile it ran as, or null for the harness's own home.</param>
public sealed record SessionEnded(
    string Session, string Repository, string State, bool ByPerson, string? Note = null,
    string? Quest = null, string? Adapter = null, string? Account = null);

/// <param name="Considerations">Every open quest, with its verdict and reason — the plan, printable.</param>
/// <param name="Events">What actually happened this tick: sessions concluded, held, or refused.</param>
/// <param name="Progressed">
/// Whether any session was actually OPENED this tick. A planned start that held (dirty tree) or was
/// refused (raced) is not progress — and treating it as progress is an infinite loop: the same quest
/// would plan, hold, and plan again forever.
/// </param>
/// <param name="Active">
/// The sessions the snapshot found running. Carried so a watcher can see a state change the driver
/// did not cause (SURF5b) — a park is the session asking, through its own connector, and this tick's
/// view is the only place it shows up.
/// </param>
/// <param name="Concluded">
/// What this tick ended, structurally. <paramref name="Events"/> already says so in English and
/// nothing can parse that — deliberately, since those sentences are written for a person (D24).
/// </param>
public sealed record TickReport(
    IReadOnlyList<Consideration> Considerations,
    IReadOnlyList<string> Events,
    bool Progressed,
    IReadOnlyList<SessionView>? Active = null,
    IReadOnlyList<SessionEnded>? Concluded = null)
{
    public bool PlannedAnything => Considerations.Any(c => c.Verdict == StartVerdict.Start);

    /// <inheritdoc cref="Active"/>
    public IReadOnlyList<SessionView> Active { get; init; } = Active ?? [];

    /// <inheritdoc cref="Concluded"/>
    public IReadOnlyList<SessionEnded> Concluded { get; init; } = Concluded ?? [];
}

/// <summary>
/// The loop D45 exists for: watch → plan → spawn → observe. One tick fetches a snapshot, plans it
/// (pure), and runs every planned start to its conclusion — sessions in different repositories run
/// together, which is the point of the whole direction.
/// </summary>
/// <remarks>
/// <para><b>The driver never writes quest state.</b> It opens and advances SESSION records through the
/// service's ledger; the spawned session claims and closes its own quest through its own connector
/// (D46 §3). A failure here therefore leaves the quest exactly where it was.</para>
///
/// <para><b>Everything here is observation.</b> The session's end is concluded from the exit code and
/// the quest's state (<see cref="Observation"/>); the evidence is what git says landed. Nothing is
/// taken from the session's word, because outside sessions have no word to give.</para>
/// </remarks>
public sealed class Driver(
    ServiceClient service, DriverConfig config, AdapterSet adapters, string home,
    SessionProcesses? processes = null, RemoteSyncSet? sync = null, SessionOutput? output = null,
    HarnessRoster? harnesses = null,
    // What sessions consumed (TOOL3). Null where nobody is keeping the record — the family
    // rehearsal's headless driver, and every test that does not care.
    SessionUsage? usage = null,
    // The plugins that speak (D64), shared across ticks like the process registry: their processes
    // outlive a tick, and the tick asks them at its points. Null where no plugin is read at all,
    // which is every test that does not care and nothing that runs on a machine.
    HookSet? hooks = null)
{
    // Shared across the per-tick instances a watch loop constructs, so a control surface can reach
    // what is actually running; per-instance when nobody passes one, which no test has to care about.
    private readonly SessionProcesses _processes = processes ?? new SessionProcesses();

    // The same sharing, for the same reason: a probe spawns a process, so the cache has to outlive
    // one tick or every quest would re-detect every harness. Per-instance when nobody passes one.
    private readonly HarnessRoster _harnesses = harnesses ?? new HarnessRoster(adapters);

    // The adapters this tick spawns with: the build's, plus whatever the plugins read this tick
    // declare (D64 §3). Set once per tick, before any start is run.
    private AdapterSet _adapters = adapters;

    // The servers this tick hands every session (D65 §1f): what the contributing plugins declare,
    // beside the knowledge host. Read with the catalogue, once per tick.
    private IReadOnlyList<AcpMcpServer> _servers = [];

    // The worktree half of D51, beside the transcripts under the same home.
    private readonly SessionTrees _trees = new(home);

    /// <summary>One decision-and-execution round. Returns what happened, for whoever is watching.</summary>
    public async Task<TickReport> TickAsync(CancellationToken ct = default)
    {
        var events = new List<string>();

        // The sync runs before the snapshot, so this tick plans over what the team has done (D68). Its
        // failure is an event, never a dead tick: the next tick retries, and every verb has already
        // committed here — but a sync dying quietly looks exactly like a family with nothing to say,
        // so the wall is named.
        async Task SyncAsync()
        {
            if (sync is null) return;
            var synced = await sync.RunOnceAsync(ct).ConfigureAwait(false);
            if (synced.Problem is not null)
            {
                events.Add($"sync  {synced.Problem}");
            }

            // What the remote understood and deliberately did not take (D48 §6) — a stale or branch
            // feed, a quest it would not keep, a move that lost to another machine's. Reported as its
            // own kind of line, because each is news about the family, not a fault in this machine.
            foreach (var note in synced.Notes) events.Add($"held  {note}");
        }

        await SyncAsync().ConfigureAwait(false);

        // The plugins, read fresh each tick like the config (D64): a harness declared since the
        // last look is spawnable now, a server declared since is handed now, a hook process
        // disabled since is stopped now. The catalogue refuses a name this build carries before
        // anything of that plugin is taken.
        var catalog = PluginCatalog.Load(home, adapters.Names);
        _adapters = adapters.WithPlugins(catalog);
        _servers = catalog.Servers;
        _harnesses.Use(_adapters);
        if (hooks is not null)
        {
            events.AddRange(await hooks.ReconcileAsync(catalog, ct).ConfigureAwait(false));
        }

        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var plan = Planner.Plan(snapshot, config);
        var progressed = false;

        // What this tick ended, structurally — the half of the report a watcher can act on (SURF5b).
        var concluded = new List<SessionEnded>();

        // What held at spawn, by quest — the plan said Start and the spawn said no. Folded back into
        // the report's considerations, so "why is this sitting" is answered for the holds a real
        // machine actually hits, not only the ones the planner can see.
        var heldAt = new Dictionary<string, string>(StringComparer.Ordinal);

        var starts = plan.Where(c => c.Verdict == StartVerdict.Start).ToList();

        // The plugins' say, BEFORE a start costs anything (D64 §4): the first hold in catalogue order
        // is the quest's reason for sitting, exactly as a dirty tree or a missing binary would be —
        // and a plugin that could not decide holds too, naming itself. Asked in plan order, one at a
        // time, so two plugins' answers arrive in an order a person can predict.
        if (hooks is not null && starts.Count > 0)
        {
            var allowed = new List<Consideration>();
            foreach (var start in starts)
            {
                var decision = await hooks.ConsiderAsync(start, ct).ConfigureAwait(false);
                if (decision.Allowed)
                {
                    allowed.Add(start);
                    continue;
                }

                heldAt[start.Quest.Id] = decision.Reason!;
                events.Add($"held  #{start.Quest.Id} → {start.Quest.To}: {decision.Reason}");
            }

            starts = allowed;
        }

        var runs = starts.Select(async start =>
        {
            var (line, opened, ended, held) = await RunAsync(start, ct).ConfigureAwait(false);
            lock (events)
            {
                events.Add(line);
                progressed |= opened;
                if (ended is not null) concluded.Add(ended);
                if (held is not null) heldAt[start.Quest.Id] = held;
            }
        });
        await Task.WhenAll(runs).ConfigureAwait(false);

        // What ended, told to whoever listens — contained: nothing a plugin says here changes the
        // record, which moved on the exit code and the quest before this line ran (D46 §4).
        if (hooks is not null)
        {
            foreach (var ended in concluded)
            {
                events.AddRange(await hooks.EndedAsync(ended, ct).ConfigureAwait(false));
            }
        }

        // What a session did — its take, its close, the next step of a chain — committed here as it
        // happened; syncing again now carries it within the tick that made it, rather than a tick later
        // (sync design §8). Nothing concluded, nothing new to carry.
        if (concluded.Count > 0) await SyncAsync().ConfigureAwait(false);

        return new TickReport(Considerations.Blocked(plan, heldAt), events, progressed, snapshot.Active, concluded);
    }

    /// <summary>
    /// Tick until nothing progresses — the deterministic mode a gate drives. Sessions run inside
    /// their tick, so "no progress" means "nothing left that this driver may begin": a queue that is
    /// empty, held, or waiting on the person.
    /// </summary>
    public async Task<IReadOnlyList<TickReport>> RunUntilIdleAsync(CancellationToken ct = default)
    {
        var reports = new List<TickReport>();
        while (true)
        {
            var report = await TickAsync(ct).ConfigureAwait(false);
            reports.Add(report);
            if (!report.Progressed) return reports;
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false);
        }
    }

    /// <returns>
    /// The console line, whether a session actually opened, — when one ended here — what it ended
    /// as and whose decision that was (SURF5b), and — when the start was HELD at spawn — the hold's
    /// own sentence, which the report carries as the quest's verdict. A hold or a refusal ends
    /// nothing, so the third value is null: there is no session to have ended. A refusal is not a
    /// hold either: somebody else got there first, and the quest is theirs rather than sitting.
    /// </returns>
    private async Task<(string Line, bool Opened, SessionEnded? Ended, string? Held)> RunAsync(
        Consideration start, CancellationToken ct)
    {
        var quest = start.Quest;
        var root = start.Root!;
        var isolated = config.OpensOwnTree(quest.To);
        (string, bool, SessionEnded?, string?) Hold(string why) => ($"held  #{quest.Id} → {quest.To}: {why}", false, null, why);

        // Clean tree, or nothing: uncommitted changes are somebody's work in flight (D46 §3). No
        // session record exists yet, so a hold here costs nothing and destroys nothing. VACUOUS for a
        // repository opted into session trees (D51 rule 5) — a fresh tree is clean by construction,
        // and the person's work in flight in the root no longer holding the driver is the point of
        // the whole decision.
        if (!isolated)
        {
            var (clean, detail) = await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false);
            if (!clean)
            {
                return Hold(detail);
            }
        }

        // The harness, and which account it runs as (D49 §4) — asked BEFORE the record is opened, in
        // the same place and for the same reason as the clean-tree rule above: a session record for a
        // spawn that could never have happened would hold the repository and explain nothing. A
        // missing binary and a logged-out profile are the same shape of answer, and each names the
        // action that fixes it.
        var selection = await _harnesses
            .SelectAsync(config.Adapter, config, start.Workspace, chosen: null, ct)
            .ConfigureAwait(false);
        if (!selection.Allowed)
        {
            return Hold(selection.Refusal!);
        }

        // The session's own tree, where the repository opted in (D51) — grown BEFORE the record for
        // the same reason every refusal above is asked before it: a failure here holds the quest
        // open and records nothing. The branch grows from the canonical line as WSP4 resolves it.
        TreeOpened? opened = null;
        if (isolated)
        {
            try
            {
                opened = await _trees.OpenAsync(root, quest.To, start.Workspace ?? "default", ct)
                    .ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return Hold(error.Message);
            }
        }

        var workTree = opened?.Path ?? root;

        // 🔴 Can this harness use what the repository allows it? (DEPLOY1.) Claude Code ignores a
        // repository's `permissions.allow` until a person has accepted that path, so a session in an
        // untrusted tree does the work and then cannot take or close the quest it exists to serve —
        // measured three times, nine minutes and a real login each. Asked HERE, with the other
        // pre-spawn holds, because the whole value is not spending that.
        //
        // Held rather than failed: nothing is wrong with the quest, and one command fixes it.
        // Resolved defensively: an unknown adapter name is its own error with its own sentence,
        // reported where it already was, so this check simply does not run for one.
        HarnessToolchain? preflight = null;
        try { preflight = _adapters.Resolve(config.Adapter).Toolchain; } catch (DriverException) { }

        if (preflight is { TrustFile: { Length: > 0 } trustFile })
        {
            var configHome = selection.ProfileHome
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (ClaudeTrust.Accepted(Path.Combine(configHome, trustFile), workTree) == false)
            {
                return Hold(ClaudeTrust.Refusal(workTree));
            }
        }

        // Where the tree stands BEFORE anything runs in it (SURF6). Read here rather than after the
        // spawn so it is genuinely the base: between this line and the process starting, the only
        // thing that touches the tree is the process. It goes onto the record because the review
        // reads it back on a later launch, long after this variable is gone — which is what makes
        // the range a fact rather than something reconstructed from the evidence string.
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);

        // The ledger judges the open — the same door any other client would use. A refusal here is
        // an answer (someone else got there first), not an error.
        var (sessionId, message) = await service
            // The tree this spawn will hold (D51) — its own where the repository opted in, the
            // registered root otherwise. Stated rather than left to the service to infer, because
            // this side is the one that knows where it is about to run a process.
            .OpenSessionAsync(
                quest.Id, config.Adapter, selection.Version, selection.Profile, workTree, before, ct)
            .ConfigureAwait(false);
        if (sessionId is null)
        {
            // A fresh tree for a session that never opened has no work in it, so the clean removal
            // path takes it — and if anything landed there in the meantime, the refusal keeps it.
            if (opened is not null)
            {
                await _trees.RemoveAsync(opened.Path, ct: ct).ConfigureAwait(false);
            }

            return ($"refused  #{quest.Id} → {quest.To}: {message}", false, null, null);
        }

        try
        {
            var adapter = _adapters.Resolve(config.Adapter);
            var target = new SessionTarget(
                quest.Id, quest.Title, quest.Body, quest.From, quest.To, workTree, service.BaseUrl)
            {
                Links = quest.Links,
                Attachments = quest.Attachments,
                Then = quest.Then,
                Parent = quest.Parent,
            };
            var info = adapter.Prepare(target, config.Commands.GetValueOrDefault(adapter.Name));

            // The environment seam every harness already carries for exactly this (D49 §4). Applied
            // by the driver rather than inside the adapter's Prepare, so one line governs both doors
            // and no adapter can forget it.
            if (adapter.Toolchain is { } toolchain)
            {
                HarnessProbe.Apply(
                    info, toolchain, selection.ProfileHome, selection.Binary, selection.ClaudeExecutable,
                    selection.Environment);
            }

            // What daoris writes into a dsh home it owns (ACP3): the two rows that send session
            // material off this machine, off — and its own skills reachable. Only where daoris made
            // the directory; the notice is what happens everywhere else, and a home holding somebody
            // else's patch layer is reported rather than overwritten.
            var harnessNotice = DshProfile.NoticeFor(adapter.Name, selection.ProfileHome);
            if (harnessNotice is null && adapter is DshAdapter && selection.ProfileHome is { Length: > 0 } dshHome)
            {
                try
                {
                    DshProfile.Write(dshHome);
                }
                catch (DriverException refused)
                {
                    harnessNotice = $"— {refused.Message}";
                }
            }

            await service.AdvanceAsync(
                sessionId, "starting",
                // The creating sentence, on the record while the session runs (D51 rule 4): where it
                // grew from and what a fresh tree does not hold. The conclusion's note replaces it —
                // by then the record's Tree field and the evidence say the rest.
                note: opened?.Sentence, ct: ct).ConfigureAwait(false);

            var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

            // The servers the plugins hand this session (D65 §1f). The protocol door carries them on
            // the wire below; a pipe-door harness that takes a file at spawn is handed one under
            // Daoris's home, and the file goes when the session does.
            string? handed = null;
            if (adapter.Wire == SessionWire.Pipe && _servers.Count > 0)
            {
                handed = SpawnServers.Write(home, sessionId, _servers);
                if (handed is not null) adapter.HandServers(info, handed);
            }

            using var process = Process.Start(info)
                ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
            using var tracked = _processes.Track(sessionId, process);
            using var _ = new Disposer(() => SpawnServers.Remove(handed));

            // Which door this harness is held over (D53). The protocol door drives an ACP session on
            // the same process; the pipe door reads its text. Both end the same way — the record is
            // concluded below from the exit code and the quest's state, never from what the session
            // said about itself (D46 §4).
            // 🔴 The ACP task is held AS ITS OWN TYPE. Assigning it to a bare `Task` compiles and
            // silently discards the outcome — which is where the usage measurement lives (TOOL3).
            var acp = adapter.Wire == SessionWire.Acp
                // The posture rides with it, because it is the ADAPTER's (ACP3): three harnesses
                // name the same D37 boundary three different ways, and one of them does not name it
                // on the wire at all.
                ? CaptureAcpAsync(
                    process, transcript, sessionId, workTree, TargetPrompt.Compose(target),
                    adapter.AcpPosture, harnessNotice, ct)
                : null;
            Task capture = acp ?? CaptureAsync(process, transcript, sessionId, ct);

            await service.AdvanceAsync(sessionId, "working", transcript: transcript, ct: ct).ConfigureAwait(false);

            var exitCode = await WaitAsync(process, ct).ConfigureAwait(false);
            await capture.ConfigureAwait(false);

            // What it consumed, where the door reported it (TOOL3/D57 §4). The pipe door reports
            // nothing and records nothing — a surface then says "not measured" rather than zero.
            if (acp is not null && (await acp.ConfigureAwait(false))?.Usage is { } used)
            {
                usage?.Record(new UsageEntry(
                    sessionId, quest.To, adapter.Name, selection.Profile,
                    used.Used, used.Size, DateTimeOffset.UtcNow));
            }

            // The person's stop outranks the observation: a killed session leaves the same signals as
            // a crashed one, and only this flag knows whose decision the end was.
            var status = await service.QuestStatusAsync(quest.Id, ct).ConfigureAwait(false) ?? "Open";
            var conclusion = _processes.WasStopRequested(sessionId)
                ? new SessionConclusion("stopped", "the person stopped it.")
                : exitCode is int code
                    ? Observation.Conclude(code, status)
                    : new SessionConclusion("failed", $"timed out after {config.TimeoutMinutes} minutes and was killed.");

            // 🔴 A credential its provider refused is read from the tool's own last words (AGT3b), and
            // the account is held so no further session sits through the same minutes of retries.
            if (conclusion.State == "failed" && adapter.Toolchain is { Refused: { Length: > 0 } refusedWords } refusing
                && Observation.Refused(LastLines(transcript), refusedWords))
            {
                var owner = refusing.Owner(adapter.Name);
                var account = selection.Profile is { } named ? $"the `{owner}` account `{named}`" : $"`{owner}`'s own sign-in";
                var reason = $"its provider refused {account} (401). Replace the key or sign in again — "
                    + $"on Settings, or `daoris agent` — and Daoris will start sessions on it again.";
                conclusion = conclusion with { Note = $"{conclusion.Note} {char.ToUpperInvariant(reason[0])}{reason[1..]}" };
                _harnesses.Refuse(adapter.Name, selection.Profile, $"an earlier session found that {reason}");
            }

            var evidence = await WorkingTree.CommitsSinceAsync(workTree, before, ct).ConfigureAwait(false);
            await service.AdvanceAsync(
                sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct).ConfigureAwait(false);

            // The tree stays, whole — nothing merges itself and nothing deletes itself (D51 rules
            // 6–7): the person merges from the root and discards from a surface that refuses to
            // destroy work. The line names it so a terminal watcher knows where the work is sitting.
            var where = opened is null ? "" : $" [own tree: {opened.Path}]";
            return (
                $"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}){where}: {conclusion.Note}",
                true,
                // 🔴 The stop flag IS the "whose decision was this" answer (SURF5b) — the same one
                // that outranks the observation two lines above. Read once, used for both.
                new SessionEnded(
                    sessionId, quest.To, conclusion.State,
                    ByPerson: _processes.WasStopRequested(sessionId), conclusion.Note,
                    Quest: quest.Id, Adapter: adapter.Name, Account: selection.Profile),
                null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The person is closing the driver. WaitAsync already ended the process tree, so nothing
            // is orphaned — and the record must say so rather than sit at "working" forever. The write
            // rides an unbound token: the cancelled one would refuse the very report it caused.
            try
            {
                await service.AdvanceAsync(
                    sessionId, "stopped",
                    note: "the driver was stopped while this ran; the session's process was ended with it.",
                    ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            // The person is closing the driver, so this ending is theirs — no interruption is owed
            // for something they are in the middle of doing (design §4).
            return (
                $"stopped  session {sessionId} (#{quest.Id} → {quest.To}): the driver was stopped.",
                true,
                new SessionEnded(sessionId, quest.To, "stopped", ByPerson: true, Quest: quest.Id, Adapter: config.Adapter),
                null);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The record must say what the driver saw, even when what it saw was its own failure —
            // an abandoned "starting" row reads as a session that never ends.
            try
            {
                await service.AdvanceAsync(sessionId, "failed", note: error.Message, ct: ct).ConfigureAwait(false);
            }
            catch (DriverException)
            {
                // The terminal write is best-effort by construction: the first failure is the report.
            }

            return (
                $"failed  session {sessionId} (#{quest.Id} → {quest.To}): {error.Message}",
                true,
                new SessionEnded(sessionId, quest.To, "failed", ByPerson: false, error.Message, Quest: quest.Id, Adapter: config.Adapter),
                null);
        }
        finally
        {
            // However this ended, the stream is over. The buffer stays readable so whoever was
            // watching can see how it finished; the transcript remains the durable copy either way.
            // In the finally rather than the happy path, because a session that FAILED is the one
            // whose last lines someone most wants to read.
            output?.Close(sessionId);
        }
    }

    /// <summary>
    /// Exit code, or null when the timeout killed it. Either way the tree dies with it — a timeout
    /// AND a driver shutdown both end the process, because an orphaned agent session working a quest
    /// nobody is observing is the one thing worse than a failed one.
    /// </summary>
    private async Task<int?> WaitAsync(Process process, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(config.TimeoutMinutes));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (ct.IsCancellationRequested) throw; // shutdown: the caller records "stopped"
            return null; // timeout: the caller records "failed"
        }
    }

    /// <summary>
    /// Both streams into one transcript file — diagnostic, never the record (D46 §4) — and, since
    /// D49 §2, teed into the live console as they pass.
    /// </summary>
    /// <remarks>
    /// One pump, two destinations, and the file is written FIRST: the durable copy must never be the
    /// thing that loses a line to an in-memory reader's problem. The console is optional because the
    /// headless driver has nobody to show it to — the buffer exists only where something reads it.
    /// </remarks>
    private Task CaptureAsync(Process process, string transcript, string sessionId, CancellationToken ct) =>
        CaptureAsync(process, transcript, sessionId, output, ct);

    /// <summary>
    /// The protocol door's capture (D53): an ACP session held over this process's stdio, with the
    /// RENDERED updates reaching the transcript and the console rather than the wire itself.
    /// </summary>
    /// <remarks>
    /// <para><b>stdout belongs to the protocol; stderr is still text a person wants.</b> Agents log
    /// there, and its lines go to the same two destinations through the same tee, so a transcript
    /// stays one readable stream.</para>
    ///
    /// <para><b>The ending is end-of-input, not a kill.</b> When the turn is over the driver closes
    /// stdin and the agent winds up and exits on its own — the same ending the chat door records as
    /// `completed` (D49/SES2), and the exit the conclusion below is actually drawn from.</para>
    ///
    /// <para><b>The wire's own ending is written down and nothing more.</b> It is a self-report, and
    /// the protocol flattens an aborted, blocked or errored turn into `end_turn`, so a record moved
    /// by it would be a record that cannot tell a refusal from a success.</para>
    /// </remarks>
    private async Task<AcpOutcome?> CaptureAcpAsync(
        Process process, string transcript, string sessionId, string cwd, string prompt,
        string? posture, string? harnessNotice, CancellationToken ct)
    {
        await using var file = new StreamWriter(transcript, append: false);

        void Line(string text)
        {
            lock (file) file.WriteLine(text);
            output?.Append(sessionId, text);
        }

        var errors = PumpAsync(process.StandardError, file, sessionId, output, ct);

        try
        {
            // What this harness is doing that daoris has not been able to govern (ACP3). On the
            // transcript rather than swallowed: it is a fact about how this session ran, and the
            // person can act on it in one command.
            if (harnessNotice is { Length: > 0 }) Line(harnessNotice);

            // The session's voice (ACP4). Located per run rather than once, because a machine can
            // gain the host between ticks — and a machine that has none still drives, without a
            // connector, exactly as it did before.
            var connector = KnowledgeConnector.Offer(
                Environment.GetEnvironmentVariable(KnowledgeConnector.PathVariable),
                DaorisHome.Resolve(),
                AppContext.BaseDirectory);
            if (connector is null)
            {
                Line($"— no {KnowledgeConnector.ExecutableName} on this machine, so this session has no "
                     + "connector: it can do the work but cannot take or close its own quest. "
                     + "`npm run publish:service -- --install` lands one.");
            }

            // The knowledge host first, then whatever the plugins hand every session (D65 §1f): a
            // browser, a ticket system — tools the session can reach, under the repository's own
            // posture like every other tool it has.
            var offered = new List<AcpMcpServer>();
            if (connector is not null) offered.Add(connector);
            offered.AddRange(_servers);

            var outcome = await new AcpSession(
                    process.StandardOutput, process.StandardInput, Line, closeTimeout: null, posture)
                .RunAsync(cwd, prompt, ct, offered).ConfigureAwait(false);

            Line($"— the turn ended: {outcome.StopReason}, after {outcome.Updates} update(s). The "
                 + "session record is concluded from the exit code and the quest's own state, not "
                 + "from this line (D46 §4).");
            return outcome;
        }
        catch (DriverException error)
        {
            // A protocol failure is a fact about this run and belongs in its transcript. It does not
            // conclude the record either: the process still has an exit code, and the quest still
            // has a state, and those two are what the conclusion is made of.
            Line($"— the ACP session failed: {error.Message}");
            return null;
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (Exception error) when (error is InvalidOperationException or IOException or ObjectDisposedException)
            {
                // Already gone: the agent exited first, which is the ending this was asking for.
            }

            await errors.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The same capture for a session this class did not spawn — a chat (D49 §3), whose process
    /// belongs to <see cref="ChatRunner"/>. Shared rather than copied: one pump, one tee, one set of
    /// rules about which destination is the durable one.
    /// </summary>
    /// <summary>A finished transcript's last lines — where a tool says why it gave up. Unreadable is none.</summary>
    private static IReadOnlyList<string> LastLines(string transcript)
    {
        try
        {
            return [.. File.ReadLines(transcript).TakeLast(40)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal static async Task CaptureAsync(
        Process process, string transcript, string sessionId, SessionOutput? output, CancellationToken ct)
    {
        await using var file = new StreamWriter(transcript, append: false);
        var stdout = PumpAsync(process.StandardOutput, file, sessionId, output, ct);
        var stderr = PumpAsync(process.StandardError, file, sessionId, output, ct);
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
    }

    /// <summary>
    /// One stream into the transcript and the console. Internal rather than local so the tee itself is
    /// testable without a process: the property worth holding is that a line reaches BOTH.
    /// </summary>
    internal static async Task PumpAsync(
        TextReader reader, TextWriter file, string sessionId, SessionOutput? output, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            lock (file) file.WriteLine(line);
            output?.Append(sessionId, line);
        }
    }
}
