using System.Diagnostics;

namespace Daoris.Driver;

/// <param name="SessionId">The record's id, when one was opened — null when nothing started.</param>
/// <param name="Message">What to tell the person: the ledger's sentence, or why nothing began.</param>
public sealed record ChatStart(string? SessionId, string Message);

/// <summary>
/// A conversation with an agent in one repository (D49 §3) — the session D46 built, entered by a
/// person instead of planned from a quest.
/// </summary>
/// <remarks>
/// <para><b>Nothing here is a chat loop.</b> Daoris pipes text and observes; the harness carries the
/// model and the conversation (D24, `model-decoupling`). This class spawns a process, relays the
/// person's lines into it, keeps the transcript and the console exactly as the driven path does, and
/// concludes the record from what it can see. A Daoris-owned loop calling a model API was rejected in
/// the design: it would duplicate what every harness already is, and produce sessions with no
/// doctrine path into them.</para>
///
/// <para><b>A chat may open on a dirty tree</b>, where a driven session may not (D46 §3). The
/// clean-tree rule exists because an unattended agent would entangle itself with somebody's work in
/// flight; in a conversation the person IS present, and it is their work in flight.</para>
///
/// <para><b>The lock is the ledger's</b>, not this class's: one active session per repository,
/// judged in one place for every door. This asks, and reports the refusal verbatim.</para>
/// </remarks>
public sealed class ChatRunner(
    ServiceClient service,
    AdapterSet adapters,
    string home,
    SessionProcesses processes,
    SessionOutput? output = null,
    HarnessRoster? harnesses = null,
    // Where a conversation's structure is kept (D76): the shell's shared record, so its page hears each
    // message live; the home's own where nobody passes one, which is the headless chat door's case.
    SessionEvents? events = null) : IDisposable
{
    // Shared with the driver where a shell has both, so a probe is paid for once; its own where it
    // does not, which is the headless chat door's case.
    private readonly HarnessRoster _harnesses = harnesses ?? new HarnessRoster(adapters);

    private readonly SessionEvents _events = events ?? new SessionEvents(Path.Combine(home, "sessions"));

    // Which adapter each live conversation runs on — how a person's message is framed for its harness
    // (CONV3). An entry lives exactly as long as the conversation's process.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ISessionAdapter> _talking =
        new(StringComparer.OrdinalIgnoreCase);

    // Each live conversation's watch, which ends by writing its record — what closing the driver waits
    // on, because the host goes next and a record written after it is never written (2026-09-25).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _watching =
        new(StringComparer.OrdinalIgnoreCase);

    // The protocol door's conversations (CONV3b), by session: each one's ACP session and its turns.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ProtocolChat> _protocol =
        new(StringComparer.OrdinalIgnoreCase);

    // The native door's conversations on a structured wire (CONV4a), by session: each one's turns.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, NativeChat> _native =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Where a conversation's turns stand — whether one is in flight, and what the person sent that is
    /// not at the harness yet — each time that changes (CONV4a). The page shows the waiting messages as
    /// queued, since they are in no record until they are sent, and offers a stop while a turn runs.
    /// </summary>
    public event Action<string, ChatQueue>? QueueChanged;

    /// <summary>The note a conversation's record takes when the driver holding it closes.</summary>
    public const string ClosedNote =
        "the application closed while this conversation ran; its process was ended with it.";

    /// <summary>
    /// Open a chat and put a harness behind it. The record is the service's; the process is this
    /// machine's, and never leaves it (D46 §7).
    /// </summary>
    /// <param name="onEnded">
    /// Called when the conversation ends, with the state the record took. The caller decides what that
    /// becomes — a console line, an IPC event, nothing at all.
    /// </param>
    /// <param name="profile">
    /// The per-session picker (D49 §4): which credential profile this conversation runs as. Null takes
    /// the workspace's default, then the machine's, then the harness's own configuration home.
    /// </param>
    /// <param name="ownTree">
    /// Open the conversation in a session tree of its own (D51) — the per-conversation choice, beside
    /// the repository's standing opt-in in the config. Either says yes; a chat is exactly the case the
    /// decision was made for, since the person is usually IN the root it would otherwise hold.
    /// </param>
    public async Task<ChatStart> StartAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded = null, string? profile = null, bool ownTree = false,
        CancellationToken ct = default)
    {
        // Through the roster's LIVE set, not the one this runner was built with: the driver's tick
        // hands the roster the build's adapters plus whatever the plugins declare (D64), so a harness
        // declared since the shell started is a harness a conversation can run on now.
        var resolved = _harnesses.Adapters.Resolve(adapter);
        if (!resolved.Interactive)
        {
            // Asked BEFORE the record is opened: a session record for a conversation that could never
            // have happened would hold the repository and explain nothing.
            return new(
                null,
                $"the `{resolved.Name}` adapter does not hold conversations — it spawns an agent that "
                + "takes its target once and runs to completion.");
        }

        var snapshot = await service.SnapshotAsync(ct).ConfigureAwait(false);
        var known = snapshot.Repositories
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));
        var root = known?.Root;
        if (string.IsNullOrWhiteSpace(root))
        {
            return new(
                null,
                $"`{repository}` has no checkout on this machine — a conversation runs IN a working "
                + "tree. `daoris connect` from inside it, or add it from Projects.");
        }

        // The harness and the account (D49 §4), asked before the record exists — the same place the
        // `interactive` question is asked, and for the same reason. The circle comes from the registry
        // row, which is the machine's own wiring and the only honest source for it (D48 §2).
        var selection = await _harnesses
            .SelectAsync(resolved.Name, config, known!.Workspace, profile, ct)
            .ConfigureAwait(false);
        if (!selection.Allowed) return new(null, selection.Refusal!);

        // A tree of the conversation's own, when asked for — per conversation, or as the repository's
        // standing choice (D51). Grown before the record, like every refusal above this line.
        TreeOpened? opened = null;
        if (ownTree || config.OpensOwnTree(repository))
        {
            try
            {
                opened = await new SessionTrees(home)
                    .OpenAsync(root!, known.Repository, known.Workspace, ct).ConfigureAwait(false);
            }
            catch (DriverException error)
            {
                return new(null, error.Message);
            }
        }

        var workTree = opened?.Path ?? root!;

        // Where the tree stands before the conversation begins (SURF6). A chat may open on a DIRTY
        // tree (D49 §3), so this is the commit — not the working state — and the review it feeds is
        // committed work only, for exactly that reason.
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);

        var (sessionId, message) = await service
            // The tree the conversation runs in (D51) — its own where one was grown, the checkout
            // found above otherwise, stated by the side that is about to spawn into it.
            .OpenChatAsync(
                repository, resolved.Name, selection.Version, selection.Profile, workTree, before, ct)
            .ConfigureAwait(false);
        if (sessionId is null)
        {
            // Fresh and workless, so the clean path removes it; anything already in it is kept.
            if (opened is not null) await new SessionTrees(home).RemoveAsync(opened.Path, ct: ct).ConfigureAwait(false);
            return new(null, message);
        }

        var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

        Process process;
        string? rules = null;
        string? servers = null;
        object? meta = null;
        try
        {
            await service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var info = resolved.PrepareChat(
                new ChatTarget(repository, workTree, service.BaseUrl),
                config.Commands.GetValueOrDefault(resolved.Name));
            if (resolved.Toolchain is { } toolchain)
            {
                HarnessProbe.Apply(
                    info, toolchain, selection.ProfileHome, selection.Binary, selection.ClaudeExecutable,
                    selection.Environment);
            }

            // What the conversation's agent may do (PERM1, D72) — the same union a driven session in
            // this repository is handed, by each door's own way: a flag on the pipe, `session/new`'s
            // `_meta` on the protocol door, now that a conversation there opens a session (CONV3b).
            if (resolved.TakesSettings)
            {
                var file = PermissionRules.Load(home);
                var composed = PermissionRules.Compose(file, known?.Workspace, repository);
                // What the person attaches is kept outside the tree, for this conversation alone, and the
                // agent reads it where it lies (CONV4c): a read of exactly that folder, INT4j's rule —
                // every other read there would be asked, and every ask is refused (D52).
                composed = composed with
                {
                    Allow = [.. composed.Allow, PermissionRules.ReadRule(ChatFiles.Folder(home, sessionId))],
                };
                rules = SpawnSettings.Write(
                    home, sessionId, composed,
                    PermissionRules.GuardsTree(file) ? TreeGuard.For(home, workTree) : null);
                if (rules is not null && resolved.Wire == SessionWire.Pipe) resolved.HandSettings(info, rules);
                else if (rules is not null) meta = resolved.AcpSessionMeta(rules);
            }

            // 🔴 The servers the plugins hand every session (D64, D65 §1f), on this door too (REV3). The
            // protocol door carries them on `session/new` (`Servers`); a driven or intake session on the
            // pipe is handed a file — and a conversation on the pipe was handed nothing at all.
            if (resolved.Wire == SessionWire.Pipe)
            {
                servers = SpawnServers.Hand(
                    resolved, info, home, sessionId, PluginCatalog.Load(home, _harnesses.Adapters.Names).Servers);
            }

            process = Process.Start(info)
                ?? throw new DriverException($"the {resolved.Name} adapter's process did not start");
        }
        catch (Exception error)
        {
            // The record exists and must say what happened, or it sits at `queued` or `starting` forever
            // holding the repository — the same rule the driven path follows. EVERY failure: a client
            // timeout is an OperationCanceledException too, and filtering those out left the record
            // stranded where no stop and no sweep reaches it (REV3).
            SpawnSettings.Remove(rules);
            SpawnServers.Remove(servers);
            var cancelled = error is OperationCanceledException && ct.IsCancellationRequested;
            await Conclude(
                sessionId, "failed",
                cancelled ? "the request that started it was cancelled before its process started." : error.Message,
                onEnded).ConfigureAwait(false);
            if (cancelled) throw;
            return new(null, error.Message);
        }

        // The conversation outlives this call: the person types, the harness answers, and the record
        // moves when the process does. Watched on an unbound token deliberately — a chat is not ended
        // by the request that started it.
        _talking[sessionId] = resolved;

        // 🔴 On the protocol door a conversation is ONE session held over the wire (CONV3b): opened
        // once, each message a turn on it. It used to be spawned as a pipe, and a person's words went
        // into the JSON-RPC stream as raw text — the harness declared itself interactive and could not
        // hold a conversation at all.
        ProtocolChat? chat = null;
        NativeChat? native = null;
        var mapper = resolved.StructuredOutput();
        void Changed(ChatQueue queue) => QueueChanged?.Invoke(sessionId, queue);
        if (resolved.Wire == SessionWire.Acp)
        {
            chat = new ProtocolChat(
                resolved.AcpPosture, meta, workTree, Servers(sessionId), Changed,
                stopped: () => processes.WasStopRequested(sessionId));
            _protocol[sessionId] = chat;
        }
        else if (mapper is not null)
        {
            // 🔴 Turns are only visible where the wire says where one ends (CONV4a). A text-only pipe
            // takes each line at once, as it always did.
            native = new NativeChat(sessionId, resolved, processes, e => Record(sessionId, e), Changed);
            _native[sessionId] = native;
        }

        var watch = WatchAsync(sessionId, process, transcript, onEnded, rules, mapper, chat, native, servers);
        _watching[sessionId] = watch;
        _ = watch.ContinueWith(
            _ => _watching.TryRemove(new KeyValuePair<string, Task>(sessionId, watch)), TaskScheduler.Default);

        return new(sessionId, message);
    }

    /// <summary>
    /// End every conversation this runner holds, as the driver's own act, and return once each record
    /// says so — or once <paramref name="bound"/> has passed, since a shutdown must not hang on one.
    /// </summary>
    /// <remarks>
    /// Stopped, not finished: a harness given end-of-input finishes what it was saying first, which
    /// can take minutes, and the person is closing the application now.
    /// </remarks>
    public void StopAll(TimeSpan bound)
    {
        var watches = _watching.ToArray();
        foreach (var (id, _) in watches) processes.Stop(id, ClosedNote);

        try
        {
            Task.WaitAll([.. watches.Select(entry => entry.Value)], bound);
        }
        catch (AggregateException)
        {
            // A watch that failed has written its own record, or could not; either way it has ended.
        }
    }

    /// <summary>
    /// Ends every conversation and waits for each record (<see cref="StopAll"/>). 🔴 Dispose it inside the
    /// scope of the <see cref="ServiceClient"/> it was given: its conclusions go through that client, and
    /// a runner stopped after the client was disposed writes nothing (2026-09-25).
    /// </summary>
    public void Dispose() => StopAll(TimeSpan.FromSeconds(10));

    /// <summary>
    /// End a conversation the way a person finishing does: the harness winds up and exits on its own,
    /// which is a `completed` record. False when nothing here is holding it.
    /// </summary>
    /// <remarks>
    /// On the protocol door that is the turns already asked for, then `session/close`, then the end of
    /// input — never a cut stdin under a turn still running, which the agent would read as the wire
    /// breaking rather than the person finishing.
    /// </remarks>
    public bool Finish(string sessionId)
    {
        if (_protocol.TryGetValue(sessionId, out var chat))
        {
            _ = chat.FinishAsync(() => processes.CloseInput(sessionId));
            return true;
        }

        // The native door's turns already asked for run first too: finishing is not withdrawing (CONV4a).
        if (_native.TryGetValue(sessionId, out var native))
        {
            _ = native.FinishAsync(() => processes.CloseInput(sessionId));
            return true;
        }

        return processes.CloseInput(sessionId);
    }

    /// <summary>Send a person's message to a live chat. False when there is nothing listening.</summary>
    /// <remarks>
    /// Framed for the conversation's harness (CONV3) — a `stream-json` line where it reads one, a turn on
    /// its session on the protocol door (CONV3b) — and, once it is sent, part of the record: the person's
    /// words were never in it before (D76 §4). On either structured door a message sent mid-turn waits
    /// for its own (CONV4a).
    /// </remarks>
    /// <param name="files">
    /// What the person attached (CONV4c): kept for this conversation before the message is queued, so a
    /// refusal — too many, too large — reaches the person while they are still looking, and nothing
    /// half-kept is sent.
    /// </param>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    public bool Say(string sessionId, string message, IReadOnlyList<ChatUpload>? files = null)
    {
        var held = _protocol.ContainsKey(sessionId) || _native.ContainsKey(sessionId) || _talking.ContainsKey(sessionId);
        var kept = held && files is { Count: > 0 } ? ChatFiles.Keep(home, sessionId, files) : [];
        var said = new ChatMessage(message, kept);

        if (_protocol.TryGetValue(sessionId, out var chat)) return chat.Turns.Say(said);
        if (_native.TryGetValue(sessionId, out var native)) return native.Turns.Say(said);

        // A text-only door keeps the person's words where it keeps the agent's — the console. In the
        // record they would be half a conversation: questions with no answers beside them. A file it is
        // handed is named by its path, which any agent can read.
        var text = message + ChatFiles.PathLines(kept);
        var framed = _talking.TryGetValue(sessionId, out var adapter) ? adapter.FrameMessage(text) : text;
        return processes.Send(sessionId, framed);
    }

    /// <summary>Whether a conversation's turn is on its way to the harness or running there — false on a text-only door.</summary>
    public bool Taking(string sessionId) =>
        _protocol.TryGetValue(sessionId, out var chat) ? chat.Turns.Running
        : _native.TryGetValue(sessionId, out var native) && native.Turns.Running;

    /// <summary>Where a conversation's turns stand; <see cref="ChatQueue.Idle"/> for one nothing here holds.</summary>
    public ChatQueue Queue(string sessionId) =>
        _protocol.TryGetValue(sessionId, out var chat) ? chat.Turns.State
        : _native.TryGetValue(sessionId, out var native) ? native.Turns.State
        : ChatQueue.Idle;

    /// <summary>
    /// Stop the turn a conversation is taking and keep the conversation (CONV4a): what was waiting is
    /// withdrawn and handed back, and the turn in flight ends on the harness's own word.
    /// </summary>
    /// <returns><see cref="TurnStop.Nothing"/> when nothing here holds the conversation, or nothing ran.</returns>
    /// <exception cref="DriverException">The conversation's door carries only text, so it has no turn to stop.</exception>
    public Task<TurnStop> CancelTurnAsync(string sessionId)
    {
        if (_protocol.TryGetValue(sessionId, out var chat)) return chat.Turns.StopAsync();
        if (_native.TryGetValue(sessionId, out var native)) return native.Turns.StopAsync();
        if (_talking.TryGetValue(sessionId, out var adapter))
        {
            throw new DriverException(
                $"the `{adapter.Name}` harness carries only text, so the driver cannot tell where one turn ends "
                + "and the next begins — there is no turn to stop. Finish the conversation, or stop it.");
        }

        return Task.FromResult(TurnStop.Nothing);
    }

    /// <summary>
    /// The person's message as the record keeps it (CONV4c): their words and the names of what they
    /// attached — never the kept paths, nor the lines or links a door added to reach them.
    /// </summary>
    private static SessionEvent Asked(ChatMessage message) => new()
    {
        Kind = SessionEventKind.User,
        Origin = "person",
        Text = message.Text,
        Files = message.Files.Count > 0 ? [.. message.Files.Select(file => file.Name)] : null,
    };

    /// <summary>One event into a conversation's record. Sent is what the person asked for; the record's failure is its own.</summary>
    private void Record(string sessionId, SessionEvent e) => _events.Keep(sessionId, e, say: null);

    /// <param name="rules">The conversation's rules file (PERM1), which goes when the conversation does.</param>
    /// <param name="mapper">The harness's structured-output reader (CONV3), or null where its door is text.</param>
    /// <param name="chat">The conversation's session on the protocol door (CONV3b), or null on the pipe.</param>
    /// <param name="native">The conversation's turns on the native door's structured wire (CONV4a), or null.</param>
    /// <param name="servers">The plugins' servers file handed on the pipe door, which goes when the conversation does.</param>
    private async Task WatchAsync(
        string sessionId, Process process, string transcript, Func<string, string, Task>? onEnded,
        string? rules = null, IStreamMapper? mapper = null, ProtocolChat? chat = null, NativeChat? native = null,
        string? servers = null)
    {
        // Declared first, so it is disposed LAST — after the process is untracked and every map that
        // talks to it has let go (REV3: nothing disposed a conversation's process or its pipes).
        using var owned = process;
        using var tracked = processes.Track(sessionId, process);
        using var talking = new Disposer(() =>
        {
            _talking.TryRemove(sessionId, out _);
            _protocol.TryRemove(sessionId, out _);
            _native.TryRemove(sessionId, out _);
            // Nothing waiting will be sent now, and the page is told its queue emptied.
            chat?.Turns.Gone();
            native?.Gone();
        });
        try
        {
            // The conversation's messages are recorded as the person sends them (`Say`), so the
            // capture opens with no composed prompt of its own.
            var capture = chat is not null
                ? CaptureProtocolAsync(sessionId, process, transcript, chat)
                : mapper is not null
                    ? Driver.CaptureStructuredAsync(
                        process.StandardOutput, process.StandardError, transcript, sessionId, output, _events, mapper,
                        prompt: null, CancellationToken.None,
                        // The turn's end, once the record holds it, lets the next message go (CONV4a).
                        observed: e =>
                        {
                            if (e.Kind == SessionEventKind.Turn) native?.TurnEnded();
                        })
                    : Driver.CaptureAsync(process, transcript, sessionId, output, CancellationToken.None);
            await service.AdvanceAsync(sessionId, "working", transcript: transcript).ConfigureAwait(false);

            await process.WaitForExitAsync().ConfigureAwait(false);
            await capture.ConfigureAwait(false);

            // The person's stop outranks the observation, exactly as for a driven session: a killed
            // process leaves the same signals as one that ended on its own, and only this flag knows
            // whose decision it was. Otherwise a conversation simply ended — `completed`, with no
            // evidence claim beyond whatever its commits already say.
            var stopped = processes.WasStopRequested(sessionId);
            await Conclude(
                sessionId,
                stopped ? "stopped" : "completed",
                // The driver's own reason when it was the driver — closing, say — and the person's otherwise.
                processes.StopReason(sessionId) ?? (stopped
                    ? "the person ended the conversation."
                    : "the conversation ended; its commits are its record."),
                onEnded).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // 🔴 The harness first, while it is still tracked (REV3): a record that could not move to
            // `working` — a stop pressed during `starting` ends it so — used to conclude here and leave
            // the agent running, untracked and unmarked, until the application exited.
            SessionProcesses.EndIfRunning(process);
            await Conclude(sessionId, "failed", error.Message, onEnded).ConfigureAwait(false);
        }
        finally
        {
            output?.Close(sessionId);
            SpawnSettings.Remove(rules);
            SpawnServers.Remove(servers);
        }
    }

    /// <summary>
    /// What a conversation on the protocol door is handed on <c>session/new</c> — what a driven session on
    /// that door is handed (ACP4, D65 §1f): the machine's knowledge connector, carrying who the session
    /// is, and every server the plugins declare. Located per conversation, because a machine can gain
    /// either between two.
    /// </summary>
    private IReadOnlyList<AcpMcpServer> Servers(string sessionId)
    {
        var offered = new List<AcpMcpServer>();
        if (KnowledgeConnector.Offer(
                Environment.GetEnvironmentVariable(KnowledgeConnector.PathVariable), DaorisHome.Resolve(),
                AppContext.BaseDirectory, scope: Driver.ConnectorScope(home, sessionId, null)) is { } connector)
        {
            offered.Add(connector);
        }

        offered.AddRange(PluginCatalog.Load(home, _harnesses.Adapters.Names).Servers);
        return offered;
    }

    /// <summary>
    /// A conversation's capture on the protocol door (CONV3b): its session opened on the wire, what each
    /// turn renders reaching the transcript and the console, what it means reaching the record — the
    /// driven path's split (<c>Driver.CaptureAcpAsync</c>), held open for as many turns as the person takes.
    /// </summary>
    /// <remarks>
    /// A session that cannot be opened is one nothing can be said to: its process is ended, and the
    /// failure is what the record concludes with — the driver saw it, and it is not the person's.
    /// </remarks>
    private async Task CaptureProtocolAsync(string sessionId, Process process, string transcript, ProtocolChat chat)
    {
        await using var file = new StreamWriter(transcript, append: false);
        var closed = false;

        void Line(string text)
        {
            lock (file)
            {
                // A late frame after the transcript closed is dropped from the file, never a throw
                // inside the reader.
                if (closed) return;
                file.WriteLine(text);
            }

            output?.Append(sessionId, text);
        }

        void Record(SessionEvent e) => _events.Keep(sessionId, e, Line);

        var errors = Driver.PumpAsync(process.StandardError, file, sessionId, output, CancellationToken.None);
        var session = new AcpSession(
            process.StandardOutput, process.StandardInput, Line, closeTimeout: null, chat.Posture, chat.Meta, Record);

        Exception? failed = null;
        try
        {
            await session.OpenAsync(chat.Cwd, CancellationToken.None, chat.Servers).ConfigureAwait(false);
            chat.Opened(session, Line, Record);
        }
        catch (Exception error)
        {
            // 🔴 EVERY failure ends the open here (REV3): one that escaped — an unreadable answer, a pipe
            // that broke mid-write — left the queue waiting on a session that never came, and skipped
            // the wait below, so the transcript closed under a stderr pump still writing to it.
            failed = error;
            chat.Opened(null, Line, Record);
            Line($"— the ACP session could not open: {error.Message}");
            Record(new SessionEvent { Kind = SessionEventKind.Note, Text = $"the ACP session could not open: {error.Message}" });
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already gone, which is usually why it could not open.
            }
        }

        await Task.WhenAll(errors, session.Ended).ConfigureAwait(false);
        lock (file) closed = true;
        session.Release();

        if (failed is not null) throw new DriverException($"the ACP session could not open: {failed.Message}");
    }

    /// <summary>
    /// One conversation on the protocol door (CONV3b): its session once opened, and its turns one at a
    /// time, in the order the person sent them (<see cref="ChatTurns"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>A message is recorded as it is SENT, not as it is typed</b> — the queue's rule, and the
    /// reason it exists.</para>
    ///
    /// <para><b>A stop is <c>session/cancel</c></b>, which keeps the session: the agent winds its turn up
    /// and answers the prompt <c>cancelled</c>, and that answer is the turn's end in the record.</para>
    ///
    /// <para><b>A turn that could not be sent is said</b>, on the transcript and in the record, and the
    /// conversation goes on: the process is still there, and so is the person.</para>
    /// </remarks>
    private sealed class ProtocolChat
    {
        private readonly TaskCompletionSource<AcpSession?> _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Func<bool> _stopped;
        private Action<string> _line = _ => { };
        private Action<SessionEvent> _record = _ => { };

        public ProtocolChat(
            string? posture, object? meta, string cwd, IReadOnlyList<AcpMcpServer> servers,
            Action<ChatQueue> changed, Func<bool> stopped)
        {
            _stopped = stopped;
            Posture = posture;
            Meta = meta;
            Cwd = cwd;
            Servers = servers;
            Turns = new ChatTurns(
                ready: async () => await _open.Task.ConfigureAwait(false) is not null,
                take: TurnAsync,
                interrupt: CancelAsync,
                changed);
        }

        public string? Posture { get; }

        public object? Meta { get; }

        public string Cwd { get; }

        public IReadOnlyList<AcpMcpServer> Servers { get; }

        public ChatTurns Turns { get; }

        /// <summary>The session is open — or could not be, and then every turn asked for is dropped.</summary>
        public void Opened(AcpSession? session, Action<string> line, Action<SessionEvent> record)
        {
            _line = line;
            _record = record;
            _open.TrySetResult(session);
        }

        private async Task TurnAsync(ChatMessage message, Action sent)
        {
            if (await _open.Task.ConfigureAwait(false) is not { } session) return;

            _record(Asked(message));
            try
            {
                // Each attached file a `resource_link` to where it is kept (CONV4c).
                await session.PromptAsync(message.Text, CancellationToken.None, sent, message.Files).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or IOException or ObjectDisposedException
                                              or InvalidOperationException)
            {
                if (_stopped())
                {
                    // 🔴 The person stopped the session, which ended the agent and this turn's prompt
                    // with it (REV3). That is not the turn failing, and a note saying so would blame
                    // the agent for the person's act. The turn ends as a stopped turn does.
                    _line("— the turn ended: the session was stopped");
                    _record(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled" });
                    return;
                }

                _line($"— the turn could not be taken: {error.Message}");
                _record(new SessionEvent { Kind = SessionEventKind.Note, Text = $"the turn could not be taken: {error.Message}" });
                // 🔴 And the turn ENDS (REV3). Only a turn event closes a turn on the page, so a refused
                // one drew *working…* under this very note until the next message — while the composer,
                // told by the queue that nothing was taking, offered to send.
                _record(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
            }
        }

        private async Task CancelAsync()
        {
            if (!_open.Task.IsCompletedSuccessfully || _open.Task.Result is not { } session) return;
            try
            {
                await session.CancelTurnAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // The agent is gone, and its turn with it.
                _line($"— the stop could not reach the agent: {error.Message}");
            }
        }

        /// <summary>
        /// The turns already asked for, then <c>session/close</c>, then the end of input — after which the
        /// agent exits on its own, and that exit is what the record concludes from.
        /// </summary>
        public async Task FinishAsync(Action endInput)
        {
            await Turns.FinishAsync().ConfigureAwait(false);
            if (await _open.Task.ConfigureAwait(false) is { } session)
            {
                await session.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }

            endInput();
        }
    }

    /// <summary>
    /// One conversation on the native door's structured wire (CONV4a): its turns one at a time, each a
    /// line on the harness's stdin, each ended by the <c>result</c> the capture reads.
    /// </summary>
    /// <remarks>
    /// <para>🔴 <b>A mid-turn message used to be written at once</b>, where Claude Code may fold it into
    /// the turn still running and the record took it in the middle of that turn. It waits now, as the
    /// protocol door's always did.</para>
    ///
    /// <para><b>The person's words are recorded, then sent</b>: the harness can answer before a record
    /// written afterwards lands, and the answer would sit above the question.</para>
    /// </remarks>
    private sealed class NativeChat
    {
        private readonly object _gate = new();
        private TaskCompletionSource? _turn;

        public NativeChat(
            string sessionId, ISessionAdapter adapter, SessionProcesses processes, Action<SessionEvent> record,
            Action<ChatQueue> changed)
        {
            Turns = new ChatTurns(
                ready: () => Task.FromResult(true),
                take: (message, sent) =>
                {
                    var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    lock (_gate) _turn = ended;
                    record(Asked(message));
                    // Each attached file named by its path, which the agent reads with its own tool
                    // (CONV4c, measured); the record keeps the person's words, not these lines.
                    if (!processes.Send(sessionId, adapter.FrameMessage(message.Text + ChatFiles.PathLines(message.Files))))
                    {
                        // Nothing is listening: the process is going, and its watch concludes the record.
                        record(new SessionEvent { Kind = SessionEventKind.Note, Text = "the message could not be sent: the harness had gone." });
                        Gone();
                        return Task.CompletedTask;
                    }

                    sent();
                    return ended.Task;
                },
                interrupt: () =>
                {
                    if (adapter.FrameInterrupt() is { } stop) processes.Send(sessionId, stop);
                    return Task.CompletedTask;
                },
                changed);
        }

        public ChatTurns Turns { get; }

        /// <summary>The turn's end is in the record: the next message may go.</summary>
        public void TurnEnded()
        {
            TaskCompletionSource? turn;
            lock (_gate)
            {
                turn = _turn;
                _turn = null;
            }

            turn?.TrySetResult();
        }

        /// <summary>The harness has gone: nothing waiting is sent, and a turn waiting on its end stops waiting.</summary>
        public void Gone()
        {
            Turns.Gone();
            TurnEnded();
        }

        /// <summary>The turns already asked for, then the end of input: the harness winds up and exits on its own.</summary>
        public async Task FinishAsync(Action endInput)
        {
            await Turns.FinishAsync().ConfigureAwait(false);
            endInput();
        }
    }

    private async Task Conclude(
        string sessionId, string state, string note, Func<string, string, Task>? onEnded)
    {
        try
        {
            await service.AdvanceAsync(sessionId, state, note: note).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort by construction: the host — or the client that reaches it — may already be gone
            // on the same shutdown, and the first failure is the report. EVERY failure, a client timeout
            // included (an OperationCanceledException nobody asked for): one escaping here never reached
            // `onEnded`, and the terminal door waited on it forever (REV3).
        }

        if (onEnded is not null)
        {
            try
            {
                await onEnded(sessionId, state).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A listener's failure is its own; the record is already written.
            }
        }
    }
}
