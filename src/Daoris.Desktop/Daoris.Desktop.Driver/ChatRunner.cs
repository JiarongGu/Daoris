using System.Diagnostics;
using System.Text.Json;

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
///
/// <para><b>An ended chat goes on</b> (MSG1c, D137 §2.2): the person's words kept on its record are taken up here at
/// once, its record reopening and its own conversation resumed with them (<c>ChatRunner.GoOn.cs</c>).</para>
/// </remarks>
public sealed partial class ChatRunner : IDisposable
{
    private readonly ServiceClient _service;

    private readonly string _home;

    private readonly SessionProcesses _processes;

    private readonly SessionOutput? _output;

    private readonly IInAppBrowser? _browser;

    private readonly SessionUsage _usage;

    private readonly PluginLog _plugins;

    // Shared with the driver where a shell has both, so a probe is paid for once; its own where it
    // does not, which is the headless chat door's case.
    private readonly HarnessRoster _harnesses;

    private readonly SessionEvents _events;

    /// <param name="events">
    /// Where a conversation's structure is kept (D76): the shell's shared record, so its page hears each message live; the
    /// home's own where nobody passes one, which is the headless chat door's case.
    /// </param>
    /// <param name="usage">
    /// What each account has carried (TOOL3): the loop's one record where a shell has one, so a chat's end and a driven
    /// session's never write it at once; the home's own for the headless chat door (USAGE1).
    /// </param>
    /// <param name="browser">
    /// Daoris's own browser (D78), asked for by a plugin server that drives it. Null where no shell carries one, which is
    /// the headless chat door's case: such a server is then not handed.
    /// </param>
    /// <param name="plugins">
    /// Where a conversation's plugin servers, handed or withheld, are written without their words (PLUGUI1e, D119 §4.2): the
    /// shell's loop hands its log. Null writes nothing, the headless chat door's case, which writes no session lines either.
    /// </param>
    public ChatRunner(
        ServiceClient service,
        AdapterSet adapters,
        string home,
        SessionProcesses processes,
        SessionOutput? output = null,
        HarnessRoster? harnesses = null,
        SessionEvents? events = null,
        SessionUsage? usage = null,
        IInAppBrowser? browser = null,
        PluginLog? plugins = null)
    {
        _service = service;
        _home = home;
        _processes = processes;
        _output = output;
        _browser = browser;
        _usage = usage ?? new SessionUsage(home);
        _plugins = plugins ?? PluginLog.None;
        _harnesses = harnesses ?? new HarnessRoster(adapters);
        _events = events ?? new SessionEvents(Path.Combine(home, "sessions"));
        _conversations = new HarnessConversations(home);
        _marks = new GoOnMarks(home);

        // The person's words to an ended chat are kept on its record and shown in its conversation by whichever door they
        // were said at (MSG1d); hearing them there is what takes them up at once (MSG1c, D137 §2.2).
        _events.Evented += Heard;
    }

    // Which adapter each live conversation runs on — how a person's message is framed for its harness
    // (CONV3). An entry lives exactly as long as the conversation's process.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ISessionAdapter> _talking =
        new(StringComparer.OrdinalIgnoreCase);

    // Each live conversation's watch, which ends by writing its record — what closing the driver waits
    // on, because the host goes next and a record written after it is never written (2026-09-25).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _watching =
        new(StringComparer.OrdinalIgnoreCase);

    // The conversations whose turns are visible, by session: the protocol door's (CONV3b) and the
    // native door's on a structured wire (CONV4a). Every question a page asks of a turn is the same on
    // both, so one map answers it (REV3 CLEAN1: it was two, and each question asked both in turn).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ITurnedChat> _turned =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Where a conversation's turns stand — whether one is in flight, and what the person sent that is
    /// not at the harness yet — each time that changes (CONV4a). The page shows the waiting messages as
    /// queued, since they are in no record until they are sent, and offers a stop while a turn runs.
    /// </summary>
    public event Action<string, ChatQueue>? QueueChanged;

    /// <summary>
    /// A conversation's model and effort, as its agent offers them, each time they change (AGT6b, D98): as
    /// its session opened, after a change, and when the agent changed them itself. Only the options that
    /// are the person's (<see cref="AcpConfigOption.ThePersons"/>).
    /// </summary>
    public event Action<string, IReadOnlyList<AcpConfigOption>>? OptionsChanged;

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

        var snapshot = await _service.SnapshotAsync(ct).ConfigureAwait(false);
        var known = snapshot.Repositories
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));
        var root = known?.Root;
        if (string.IsNullOrWhiteSpace(root))
        {
            return new(
                null,
                $"`{repository}` has no checkout on this machine — a conversation runs IN a working "
                + "tree. `daoris connect` from inside it, or add it from Repositories.");
        }

        // The harness and the account (D49 §4), asked before the record exists — the same place the
        // `interactive` question is asked, and for the same reason. The circle comes from the registry
        // row, which is the machine's own wiring and the only honest source for it (D48 §2).
        var selection = await _harnesses
            .SelectAsync(resolved.Name, config, known!.Workspace, profile, StartKind.Conversation, ct)
            .ConfigureAwait(false);
        if (!selection.Allowed) return new(null, selection.Refusal!);

        // A tree of the conversation's own, when asked for — per conversation, or as the repository's
        // standing choice (D51). Grown before the record, like every refusal above this line. Its
        // repository's trees are held from here until the record is open, as a driven start holds them
        // (LEFT2), so bringing the repository up to date does not rebase the tree under it.
        var isolated = ownTree || config.OpensOwnTree(repository);
        using var starting = isolated ? TreeLock.TryStarting(_home, known.Workspace, known.Repository) : null;
        if (isolated && starting is null) return new(null, TreeLock.Replaying(known.Repository));

        TreeOpened? opened = null;
        if (isolated)
        {
            try
            {
                opened = await new SessionTrees(_home)
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

        var (sessionId, message) = await _service
            // The tree the conversation runs in (D51) — its own where one was grown, the checkout
            // found above otherwise, stated by the side that is about to spawn into it.
            .OpenChatAsync(
                repository, resolved.Name, selection.Version, selection.Profile, workTree, before, ct)
            .ConfigureAwait(false);
        if (sessionId is null)
        {
            // Fresh and workless, so the clean path removes it; anything already in it is kept.
            if (opened is not null) await new SessionTrees(_home).RemoveAsync(opened.Path, ct: ct).ConfigureAwait(false);
            return new(null, message);
        }

        // The record is open, so the ledger holds the tree and a replay's own look sees it in use (LEFT2).
        starting?.Dispose();

        // A new conversation holds no context yet, so it rotates like a start (TOOL4f, D125 §3.3), and only here, at its
        // opening: its account is its process's home, set at spawn. Its first line says which account and why.
        RotatedOpening.Say(_service, _events, sessionId, resolved.Name, selection, carried: null);

        return await RunAsync(
            sessionId, message, resolved, selection, config,
            RepositoryPlace(config, snapshot.Repositories, known, repository, workTree),
            onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Where a conversation in a repository runs (D49 §3): its tree, what it may do there and reach outside it (D107), as a
    /// driven session in this repository would, the plugins' servers, and its conversation kept for it to go on (MSG1c).
    /// </summary>
    /// <param name="repository">The repository as the conversation names it, which its rules are composed for.</param>
    private ChatPlace RepositoryPlace(
        DriverConfig config, IReadOnlyList<RepoView> registry, RepoView known, string repository, string workTree)
    {
        var across = AcrossRules.Reach(config, registry, known.Repository, known.Workspace);
        return new ChatPlace(
            known.Repository, workTree,
            (file, id) => RulesFor(file, known.Workspace, repository, workTree, ChatFiles.Folder(_home, id), across),
            Plugins: true, ConnectorOnPipe: false, Posture: null, ToolsUpFront: false)
        {
            Also = [.. across.Writes.Select(target => target.Path)],
            Keeps = true,
        };
    }

    /// <summary>
    /// What a conversation's agent in a repository may do (PERM1, D72): the same union a driven session in
    /// this repository is handed, with what it may reach across (D107). What the person attaches is kept
    /// outside the tree, for this conversation alone, and the agent reads it where it lies (CONV4c): a read of
    /// exactly that folder, INT4j's rule — every other read there would be asked, and every ask is refused (D52).
    /// </summary>
    internal static RuleLists RulesFor(
        PermissionFile file, string? workspace, string repository, string tree, string kept, AcrossReach across)
    {
        var composed = PermissionRules.Compose(file, workspace, repository);
        return (composed with { Allow = [.. composed.Allow, PermissionRules.ReadRule(kept)] })
            .Joined(AcrossRules.Rules(across, [tree, kept]));
    }

    /// <summary>
    /// Open Ask Daoris's conversation (HELP1a, D89): a chat about Daoris itself, in the room written from
    /// <paramref name="machine"/> under the home, on the helper's agent. The record is the service's; the
    /// process is this machine's.
    /// </summary>
    /// <remarks>
    /// <para><b>It reads, and it advises</b>: handed the knowledge connector on either door, the room's
    /// allow-list and the person's denies, and none of the plugins' servers — a browser brought up for a
    /// tool it is not allowed would be a window for nothing.</para>
    ///
    /// <para><b>Its tools load with the first request</b> (HELP5), by the adapter's own switch where it names
    /// one, so the first answer is not a search for the tools it was handed.</para>
    ///
    /// <para><b>One at a time</b>: the ledger refuses a second while one runs, naming it. The caller hands
    /// the person the running one rather than asking for another.</para>
    /// </remarks>
    public async Task<ChatStart> StartHelpAsync(
        string adapter, DriverConfig config, HelpMachine machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        var resolved = _harnesses.Adapters.Resolve(adapter);
        if (!resolved.Interactive)
        {
            return new(
                null,
                $"the `{resolved.Name}` adapter does not hold conversations — it spawns an agent that "
                + "takes its target once and runs to completion. Name another under Settings → AI features, "
                + "or `daoris driver helper <agent>`.");
        }

        // The account the default circle names, then the machine's (D89): Ask Daoris belongs to no workspace.
        var selection = await _harnesses.SelectAsync(resolved.Name, config, null, null, StartKind.Conversation, ct).ConfigureAwait(false);
        if (!selection.Allowed) return new(null, selection.Refusal!);

        string room;
        try
        {
            room = HelpRoom.Prepare(_home, machine);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new(null, $"Ask Daoris's room could not be written — {error.Message}");
        }

        var (sessionId, message) = await _service
            .OpenHelpAsync(resolved.Name, room, selection.Version, selection.Profile, ct)
            .ConfigureAwait(false);
        if (sessionId is null) return new(null, message);

        // Ask Daoris's opening rotates like a start, on the machine's order (TOOL4f, D125 §3.2), and says so first.
        RotatedOpening.Say(_service, _events, sessionId, resolved.Name, selection, carried: null);

        return await RunAsync(
            sessionId, message, resolved, selection, config,
            new ChatPlace(
                HelpRoom.Repository, room,
                (file, id) => HelpRoom.Rules(file, ChatFiles.Folder(_home, id), machine.Reads),
                Plugins: false, ConnectorOnPipe: true, Posture: HelpRoom.Posture, ToolsUpFront: true),
            onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>What differs between a conversation in a repository and Ask Daoris's in its room (HELP1a).</summary>
    /// <param name="Name">What the conversation is for: the repository, or <see cref="HelpRoom.Repository"/>.</param>
    /// <param name="Tree">Where its process runs.</param>
    /// <param name="Rules">What its agent may do, from this machine's rules file and its session id.</param>
    /// <param name="Plugins">Whether the plugins' servers are handed — and Daoris's browser brought up for one.</param>
    /// <param name="ConnectorOnPipe">
    /// Whether the knowledge connector is handed on the pipe door too. The protocol door always carries
    /// it; a repository's own `.mcp.json` wires it on the pipe, and a room has none.
    /// </param>
    /// <param name="Posture">The protocol door's mode for this conversation, or null for the adapter's own (D81).</param>
    /// <param name="ToolsUpFront">
    /// Whether its tools load with the first request, by the adapter's own switch (HELP5): true for the room,
    /// whose every answer reads the family, and false for a repository, which keeps the harness's default.
    /// </param>
    private sealed record ChatPlace(
        string Name, string Tree, Func<PermissionFile, string, RuleLists> Rules, bool Plugins, bool ConnectorOnPipe,
        string? Posture, bool ToolsUpFront)
    {
        /// <summary>The checkouts it may also write into (D107), which its tree guard lets a write reach.</summary>
        public IReadOnlyList<string> Also { get; init; } = [];

        /// <summary>
        /// Whether its harness conversation's id is kept for it (MSG1c, D137 §4.2), so the conversation goes on when the
        /// person writes to it after it ended: a repository's conversation. Ask Daoris's never goes on (§2.2), and keeps none.
        /// </summary>
        public bool Keeps { get; init; }
    }

    /// <summary>
    /// The plugins' servers a conversation is handed, with Daoris's own browser brought up for one that drives it, or
    /// that one withheld and the sentence why (D78); and a <c>plugin.served</c> line for each, handed or withheld
    /// (PLUGUI1e, D119 §4.2), read from the catalogue the servers came from, as a driven session's are.
    /// </summary>
    internal static async Task<(IReadOnlyList<AcpMcpServer> Handed, string? Notice, bool Drives)> HandServersAsync(
        string home, IEnumerable<string> reserved, IInAppBrowser? browser, PluginLog plugins, string sessionId, CancellationToken ct)
    {
        var catalog = PluginCatalog.Load(home, reserved);
        var handed = await InAppBrowserServers.HandAsync(catalog.Servers, browser, ct).ConfigureAwait(false);
        plugins.Served(catalog, sessionId, handed.Handed);
        return handed;
    }

    /// <summary>
    /// A conversation whose record is open: its process spawned with its rules and servers, its door's
    /// turns held, and its watch started — or, when the spawn fails, its record concluded.
    /// </summary>
    /// <param name="goOn">
    /// An ended chat going on (MSG1c, D137 §2.2): its record already moved to working by the ledger's one move out of an
    /// ended state, its own conversation resumed rather than opened, and the person's words waiting its first turn. Null
    /// for a new conversation.
    /// </param>
    private async Task<ChatStart> RunAsync(
        string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
        ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn = null)
    {
        var workTree = place.Tree;
        var transcript = Path.Combine(_home, "sessions", $"{sessionId}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

        Process process;
        string? rules = null;
        string? servers = null;
        object? meta = null;
        IReadOnlyList<AcpMcpServer> pluginServers = [];
        string? browserNotice = null;
        var drivesBrowser = false;
        try
        {
            // A conversation going on is working already: the ledger moved it there before anything was spawned.
            if (goOn is null) await _service.AdvanceAsync(sessionId, "starting", ct: ct).ConfigureAwait(false);

            var info = resolved.PrepareChat(
                // The native door resumes its own conversation by the id kept for it; the protocol door on its wire.
                new ChatTarget(place.Name, workTree, _service.BaseUrl)
                {
                    Resume = resolved.Wire == SessionWire.Pipe ? goOn?.Ask.Conversation : null,
                },
                config.Commands.GetValueOrDefault(resolved.Name));
            if (resolved.Toolchain is { } toolchain)
            {
                HarnessProbe.Apply(
                    info, toolchain, selection.ProfileHome, selection.Binary, selection.ClaudeExecutable,
                    selection.Environment);
            }

            // HELP5: the helper paid a model round trip to find its own knowledge tools. Which variable
            // stops that is a fact about the harness, so the adapter names it, and nothing here does.
            if (place.ToolsUpFront)
            {
                foreach (var (name, value) in resolved.ToolsUpFront) info.Environment[name] = value;
            }

            // What the conversation's agent may do (PERM1, D72), handed by each door's own way: a flag on
            // the pipe, `session/new`'s `_meta` on the protocol door, now that a conversation there opens a
            // session (CONV3b).
            if (resolved.TakesSettings)
            {
                var file = PermissionRules.Load(_home);
                rules = SpawnSettings.Write(
                    _home, sessionId, place.Rules(file, sessionId), PermissionRules.HardDeny(file),
                    PermissionRules.GuardsTree(file) ? TreeGuard.For(_home, workTree, place.Also) : null);
                if (rules is not null && resolved.Wire == SessionWire.Pipe) resolved.HandSettings(info, rules);
                else if (rules is not null) meta = resolved.AcpSessionMeta(rules);
            }

            // The plugins' servers, resolved once for both doors — with Daoris's own browser brought up
            // for one that drives it (D78), or that one left out and the conversation told why.
            if (place.Plugins)
            {
                (pluginServers, browserNotice, drivesBrowser) = await HandServersAsync(
                    _home, _harnesses.Adapters.Names, _browser, _plugins, sessionId, ct).ConfigureAwait(false);
                if (browserNotice is not null) Record(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = browserNotice });
            }

            // 🔴 The servers the plugins hand every session (D64, D65 §1f), on this door too (REV3). The
            // protocol door carries them on `session/new` (`Servers`); a driven or intake session on the
            // pipe is handed a file — and a conversation on the pipe was handed nothing at all. A room
            // has no `.mcp.json` of its own, so its connector goes in the same file (HELP1a).
            if (resolved.Wire == SessionWire.Pipe)
            {
                servers = SpawnServers.Hand(
                    resolved, info, _home, sessionId, place.ConnectorOnPipe ? Servers(sessionId, pluginServers) : pluginServers);
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
        // A conversation going on hands its words to the native door only once its process is tracked, so a word the person
        // says meanwhile goes after them; the protocol door waits for its session to open anyway (MSG1c).
        var tracked = goOn is null ? null : new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (resolved.Wire == SessionWire.Acp)
        {
            chat = new ProtocolChat(
                place.Posture ?? resolved.AcpPosture, meta, workTree, Servers(sessionId, pluginServers), Changed,
                stopped: () => _processes.WasStopRequested(sessionId),
                limited: failure => Limited(sessionId, resolved, selection.Profile, failure, config.CoolOff),
                goOn: goOn?.Ask,
                // On this door the words went once their prompt is on the wire.
                wentOn: goOn is null ? null : () =>
                {
                    if (goOn.Sent(named: false)) _ = WentOnAsync(sessionId, goOn, resolved.Name);
                });
            _turned[sessionId] = chat;
        }
        else if (mapper is not null)
        {
            // 🔴 Turns are only visible where the wire says where one ends (CONV4a). A text-only pipe
            // takes each line at once, as it always did.
            native = new NativeChat(
                sessionId, resolved, _processes, e => Record(sessionId, e), Changed, tracked?.Task,
                // On this door the words went once their line is on stdin and the harness has opened the conversation.
                wentOn: goOn is null ? null : () =>
                {
                    if (goOn.Sent(named: true)) _ = WentOnAsync(sessionId, goOn, resolved.Name);
                });
            _turned[sessionId] = native;
        }

        // The person's words a conversation goes on with are its first turn (MSG1c, D137 §2.2), queued before any word said
        // to it once it runs.
        var first = goOn is null ? null : new ChatMessage(goOn.Ask.Prompt, []) { GoesOn = goOn.Ask };
        if (first is not null) (chat?.Turns ?? native?.Turns)?.Say(first);

        // What the conversation consumed counts toward the account it ran as (USAGE1), as a driven
        // session's does: at its end, at its high-water mark, where the door reported one.
        void Measured(AcpUsage used) => _usage.Record(new UsageEntry(
            sessionId, place.Name, resolved.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));

        // What its door says about its account's windows is kept for the next start's walk (TOOL6c), as a driven session's is.
        void Said(JsonElement info)
        {
            try
            {
                _harnesses.Said(resolved.Name, selection.Profile, info, sessionId);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Nothing kept is nothing said: the account reads as unknown, as it did before the door spoke.
            }
        }

        var watch = WatchAsync(
            sessionId, process, transcript, onEnded, Measured, rules, mapper, chat, native, servers, drivesBrowser, Said,
            new Watched(resolved.Name, place.Keeps, goOn));
        _watching[sessionId] = watch;
        _ = watch.ContinueWith(
            _ => _watching.TryRemove(new KeyValuePair<string, Task>(sessionId, watch)), TaskScheduler.Default);
        // Its process is tracked now (the watch's first statements): the words it goes on with may reach it.
        tracked?.TrySetResult(true);

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
        // No conversation goes on from here (MSG1c), and one already on its way is let finish its start first, so its
        // process is among those stopped below rather than spawned after them.
        _closing = true;
        _events.Evented -= Heard;
        try
        {
            Task.WaitAll([.. _goingOn.Values], bound);
        }
        catch (AggregateException)
        {
            // A start that failed has said so where it could; either way it has ended.
        }

        var watches = _watching.ToArray();
        foreach (var (id, _) in watches) _processes.Stop(id, ClosedNote);

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
        // On the native door too, the turns already asked for run first: finishing is not withdrawing (CONV4a).
        if (_turned.TryGetValue(sessionId, out var chat))
        {
            _ = chat.FinishAsync(() => _processes.CloseInput(sessionId));
            return true;
        }

        return _processes.CloseInput(sessionId);
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
    /// <param name="preface">Where the person is (HELP1b), handed to the agent ahead of the words, or null.</param>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    public bool Say(string sessionId, string message, IReadOnlyList<ChatUpload>? files = null, string? preface = null) =>
        Say(sessionId, message, out _, files, preface);

    /// <summary>Send a person's message to a live chat, saying when it reaches it. False when there is nothing listening.</summary>
    /// <param name="reaches">
    /// When the words reach it, in the record's spelling (MSG1c, D137 §2.1): <c>next-step</c> where its door took them into
    /// the turn on the wire, <c>turn-end</c> where they wait for the running turn, and null where they are its turn now or
    /// its door carries only text.
    /// </param>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    public bool Say(
        string sessionId, string message, out string? reaches, IReadOnlyList<ChatUpload>? files = null, string? preface = null)
    {
        reaches = null;
        var held = _turned.ContainsKey(sessionId) || _talking.ContainsKey(sessionId);
        var kept = held && files is { Count: > 0 } ? ChatFiles.Keep(_home, sessionId, files) : [];
        var said = new ChatMessage(message, kept) { Preface = ChatMessage.Bound(preface) };

        if (_turned.TryGetValue(sessionId, out var chat))
        {
            var reach = chat.Turns.Take(said);
            reaches = Spelled(reach);
            return reach is not null;
        }

        // A text-only door keeps the person's words where it keeps the agent's — the console. In the
        // record they would be half a conversation: questions with no answers beside them. A file it is
        // handed is named by its path, which any agent can read.
        var text = said.Prompt + ChatFiles.PathLines(kept);
        var framed = _talking.TryGetValue(sessionId, out var adapter) ? adapter.FrameMessage(text) : text;
        return _processes.Send(sessionId, framed);
    }

    /// <summary>Whether a conversation's turn is on its way to the harness or running there — false on a text-only door.</summary>
    public bool Taking(string sessionId) => _turned.TryGetValue(sessionId, out var chat) && chat.Turns.Running;

    /// <summary>
    /// What a word said now to a live conversation would do (MSG1c, D137 §2.1), in the record's spelling: <c>next-step</c>
    /// while a turn runs on a door whose agent takes words then, <c>turn-end</c> while one runs anywhere else, and null
    /// where it would be the next turn at once, or the door carries only text.
    /// </summary>
    public string? Reach(string sessionId) =>
        _turned.TryGetValue(sessionId, out var chat) && chat.Turns.Running
            ? chat is ProtocolChat { Session.NextStep: true } && !chat.Turns.Stopped ? "next-step" : "turn-end"
            : null;

    /// <summary>A word's reach in the spelling the record and the page use (D136 §3).</summary>
    private static string? Spelled(TurnReach? reach) => reach switch
    {
        TurnReach.NextStep => "next-step",
        TurnReach.TurnEnd => "turn-end",
        _ => null,
    };

    /// <summary>Where a conversation's turns stand; <see cref="ChatQueue.Idle"/> for one nothing here holds.</summary>
    public ChatQueue Queue(string sessionId) =>
        _turned.TryGetValue(sessionId, out var chat) ? chat.Turns.State : ChatQueue.Idle;

    /// <summary>
    /// Stop the turn a conversation is taking and keep the conversation (CONV4a): what was waiting is
    /// withdrawn and handed back, and the turn in flight ends on the harness's own word.
    /// </summary>
    /// <returns><see cref="TurnStop.Nothing"/> when nothing here holds the conversation, or nothing ran.</returns>
    /// <exception cref="DriverException">The conversation's door carries only text, so it has no turn to stop.</exception>
    public Task<TurnStop> CancelTurnAsync(string sessionId)
    {
        if (_turned.TryGetValue(sessionId, out var chat)) return chat.Turns.StopAsync();
        if (_talking.TryGetValue(sessionId, out var adapter))
        {
            throw new DriverException(
                $"the `{adapter.Name}` agent carries only text, so the driver cannot tell where one turn ends "
                + "and the next begins — there is no turn to stop. Finish the conversation, or stop it.");
        }

        return Task.FromResult(TurnStop.Nothing);
    }

    /// <summary>
    /// A conversation's model and effort, as its agent offered them (AGT6b, D98) — empty for one on a door
    /// that carries none, one whose agent offered none, one still opening, and one nothing here holds.
    /// </summary>
    public IReadOnlyList<AcpConfigOption> Options(string sessionId) =>
        _turned.TryGetValue(sessionId, out var turned) && turned is ProtocolChat { Session: { } session }
            ? ThePersons(session.ConfigOptions)
            : [];

    /// <summary>
    /// Change a conversation's model or effort (AGT6b, D98): <c>session/set_config_option</c> on its
    /// session, as the tool's own console's <c>/model</c> would — and the record says the person did.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Only the options that are the person's</b>: the model and the thought level. The mode is the
    /// posture Daoris runs a session under (D37, D81), and a menu that widened it is the approval surface
    /// D52 refuses, so it is refused here before anything reaches the agent — the page never offers it,
    /// and this holds whatever a page sends. A value is the agent's to judge: it refuses one it does not
    /// take, in its own words.
    /// </remarks>
    /// <returns>The conversation's options after the change, the person's only.</returns>
    /// <exception cref="DriverException">Nothing here holds it, its door carries no options, or the option is not the person's.</exception>
    public async Task<IReadOnlyList<AcpConfigOption>> SetOptionAsync(
        string sessionId, string configId, string value, CancellationToken ct)
    {
        if (!_turned.TryGetValue(sessionId, out var turned) || turned is not ProtocolChat chat)
        {
            throw new DriverException(turned is not null || _talking.ContainsKey(sessionId)
                ? $"conversation `{sessionId}` runs on a door that carries no options to change — only the protocol "
                  + "door carries the model and effort an agent offers."
                : $"nothing on this machine holds conversation `{sessionId}` — it has ended, or runs elsewhere.");
        }

        var session = chat.Session
            ?? throw new DriverException($"conversation `{sessionId}` is still opening — its options are not known yet.");
        var option = session.ConfigOptions.FirstOrDefault(offered => offered.Id == configId)
            ?? throw new DriverException($"the agent offered no option `{configId}` for conversation `{sessionId}`.");
        if (!option.ThePersons)
        {
            throw new DriverException(
                $"the conversation's `{configId}` is the posture Daoris runs it under (D81), not the person's to change "
                + "here — its model and its effort are.");
        }

        var after = await session.SetConfigOptionAsync(configId, value, ct).ConfigureAwait(false);
        // Said as the agent names them, so a reader later knows what ran the turns after this one.
        var chosen = option.Choices.FirstOrDefault(choice => choice.Value == value)?.Name ?? value;
        Record(sessionId, new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = $"the person set {option.Name} to {chosen} for this conversation.",
        });
        return ThePersons(after);
    }

    /// <summary>The options a person may set (<see cref="AcpConfigOption.ThePersons"/>), in the agent's order.</summary>
    private static IReadOnlyList<AcpConfigOption> ThePersons(IReadOnlyList<AcpConfigOption> options) =>
        [.. options.Where(option => option.ThePersons)];

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

    /// <summary>
    /// What the agent was told beside the person's words (HELP1b), as the record keeps it: a note, never
    /// the person's, so a reader later sees what the agent knew and who said what.
    /// </summary>
    private static SessionEvent? Told(ChatMessage message) => message.Preface is { Length: > 0 } preface
        ? new SessionEvent { Kind = SessionEventKind.Note, Text = $"told where the person is: {preface}" }
        : null;

    /// <summary>
    /// A conversation's turn the door refused, read for an account's limit (TOOL4d, D125 §2.3): where the adapter's table
    /// recognises it, the account the conversation runs as cools, the log says so, and the sentence for its record comes
    /// back. Null is a refusal as today. The conversation goes on: the process is still there, and so is the person.
    /// </summary>
    /// <param name="coolOff">The machine's <c>cooloff</c> (TOOL4e), for a limit that names no time; the default when null.</param>
    internal string? Limited(string sessionId, ISessionAdapter adapter, string? profile, string failure, TimeSpan? coolOff = null)
    {
        (CoolingEntry Entry, LimitSeen Seen)? limited;
        try
        {
            limited = _harnesses.Limited(adapter.Name, profile, failure, sessionId, coolOff);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (limited is not { } read) return null;

        _service.AccountSaid(AccountLine.Limited(
            sessionId, adapter.Name, profile, read.Seen, Driver.TurnsEnded(_events, sessionId) + 1, used: null));
        return CoolingWords.Conversation(read.Entry, _harnesses.Zone);
    }

    /// <summary>One event into a conversation's record. Sent is what the person asked for; the record's failure is its own.</summary>
    private void Record(string sessionId, SessionEvent e) => _events.Keep(sessionId, e, say: null);

    /// <summary>What a conversation's watch knows of it beyond its process (MSG1c).</summary>
    /// <param name="Adapter">The adapter it runs on, whose conversation id is kept for it.</param>
    /// <param name="Keeps">Whether its conversation's id is kept, so it can go on once it ended (<see cref="ChatPlace.Keeps"/>).</param>
    /// <param name="GoOn">The ended chat this run goes on with, or null for a new conversation.</param>
    private sealed record Watched(string Adapter, bool Keeps, GoingOn? GoOn);

    /// <summary>
    /// What a conversation's ending note adds where Daoris kept no id for its harness conversation (MSG1c, D137 §4.2): words
    /// written to it once it ended cannot go on in it, which the record says before anyone writes.
    /// </summary>
    public const string NotKept = "Daoris kept no id for its conversation, so words written to it cannot go on in it.";

    /// <param name="measured">Told the conversation's high-water context at its end, where its door reported one (USAGE1).</param>
    /// <param name="rules">The conversation's rules file (PERM1), which goes when the conversation does.</param>
    /// <param name="mapper">The harness's structured-output reader (CONV3), or null where its door is text.</param>
    /// <param name="chat">The conversation's session on the protocol door (CONV3b), or null on the pipe.</param>
    /// <param name="native">The conversation's turns on the native door's structured wire (CONV4a), or null.</param>
    /// <param name="servers">The plugins' servers file handed on the pipe door, which goes when the conversation does.</param>
    /// <param name="drivesBrowser">Whether it was handed a server that drives Daoris's browser (BRW8), kept beside its process.</param>
    /// <param name="said">Told what its door says about its account's windows (TOOL6c), on either structured door.</param>
    /// <param name="watched">Its adapter, whether its conversation is kept, and the ended chat it goes on with (MSG1c).</param>
    private async Task WatchAsync(
        string sessionId, Process process, string transcript, Func<string, string, Task>? onEnded,
        Action<AcpUsage>? measured, string? rules, IStreamMapper? mapper, ProtocolChat? chat, NativeChat? native,
        string? servers, bool drivesBrowser, Action<JsonElement>? said, Watched watched)
    {
        var goOn = watched.GoOn;
        // Its harness conversation's id, kept for its record the moment its door names it (MSG1c, D137 §4.2).
        var keep = watched.Keeps ? _conversations.Keeping(sessionId, watched.Adapter) : null;

        // Declared first, so it is disposed LAST — after the process is untracked and every map that
        // talks to it has let go (REV3: nothing disposed a conversation's process or its pipes).
        using var owned = process;
        using var tracked = _processes.Track(sessionId, process, drivesBrowser: drivesBrowser);
        using var talking = new Disposer(() =>
        {
            _talking.TryRemove(sessionId, out _);
            _turned.TryRemove(sessionId, out _);
            // Nothing waiting will be sent now, and the page is told its queue emptied.
            chat?.Turns.Gone();
            native?.Gone();
        });
        try
        {
            // The conversation's messages are recorded as the person sends them (`Say`), so the
            // capture opens with no composed prompt of its own.
            var protocol = chat is not null ? CaptureProtocolAsync(sessionId, process, transcript, chat, said, keep) : null;
            Task capture = protocol
                ?? (mapper is not null
                    ? Driver.CaptureStructuredAsync(
                        process.StandardOutput, process.StandardError, transcript, sessionId, _output, _events, mapper,
                        prompt: null, CancellationToken.None,
                        // The turn's end, once the record holds it, lets the next message go (CONV4a).
                        observed: e =>
                        {
                            if (e.Kind == SessionEventKind.Turn) native?.TurnEnded();
                        },
                        // A conversation going on appends to the transcript of the run before it (MSG1c).
                        append: goOn is not null,
                        said: said,
                        named: conversation =>
                        {
                            keep?.Invoke(conversation);
                            // 🔴 Its harness opened the conversation its words went to, so they went (MSG1c): off the record by their ids.
                            if (goOn is not null && goOn.Named()) _ = WentOnAsync(sessionId, goOn, watched.Adapter);
                        })
                    : Driver.CaptureAsync(process, transcript, sessionId, _output, CancellationToken.None));

            // A conversation going on is working already: the ledger moved it out of its ended state before the spawn.
            if (goOn is null) await _service.AdvanceAsync(sessionId, "working", transcript: transcript).ConfigureAwait(false);

            await process.WaitForExitAsync().ConfigureAwait(false);
            await capture.ConfigureAwait(false);

            // 🔴 The native door's harness ended on a failed exit before naming its conversation: it never opened it, which
            // is how one it no longer has ends there (MSG1b's rule). Read from the structure, never from its words.
            if (goOn is { Ask.Refused: null } && mapper is not null && chat is null && !goOn.Ask.Named
                && process.ExitCode != 0 && !_processes.WasStopRequested(sessionId))
            {
                goOn.Ask.Refused = ContinueWhy.Of(ContinueWhy.Gone);
            }

            if (goOn?.Ask.Refused is { } refused)
            {
                await WentBackAsync(sessionId, goOn, refused, watched.Adapter, onEnded).ConfigureAwait(false);
                return;
            }

            // What it consumed, where its door reported it — measured before the record moves, so a page
            // told of the ending finds the account's usage already counting it.
            if ((protocol is not null ? await protocol.ConfigureAwait(false) : mapper?.Usage) is { } used)
            {
                measured?.Invoke(used);
            }

            // The person's stop outranks the observation, exactly as for a driven session: a killed
            // process leaves the same signals as one that ended on its own, and only this flag knows
            // whose decision it was. Otherwise a conversation simply ended — `completed`, with no
            // evidence claim beyond whatever its commits already say.
            var stopped = _processes.WasStopRequested(sessionId);
            // The driver's own reason when it was the driver — closing, say — and the person's otherwise. Said before anyone
            // writes to it (MSG1c): a conversation whose harness named no id cannot go on in it. Never on the driver's own
            // reason, which a reader matches whole (ClosedNote's twin in the deployment rehearsal).
            var note = _processes.StopReason(sessionId)
                ?? ((stopped ? "the person ended the conversation." : "the conversation ended; its commits are its record.")
                    + (watched.Keeps && _conversations.Read(sessionId) is null ? $" {NotKept}" : ""));
            await Conclude(sessionId, stopped ? "stopped" : "completed", note, onEnded).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // 🔴 The harness first, while it is still tracked (REV3): a record that could not move to
            // `working` — a stop pressed during `starting` ends it so — used to conclude here and leave
            // the agent running, untracked and unmarked, until the application exited.
            SessionProcesses.EndIfRunning(process);

            // An agent that would not resume the conversation (MSG1c): nothing went, so the record goes back to how it ended.
            if (goOn?.Ask.Refused is { } refused)
            {
                await WentBackAsync(sessionId, goOn, refused, watched.Adapter, onEnded).ConfigureAwait(false);
                return;
            }

            await Conclude(sessionId, "failed", error.Message, onEnded).ConfigureAwait(false);
        }
        finally
        {
            _output?.Close(sessionId);
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
    private IReadOnlyList<AcpMcpServer> Servers(string sessionId, IReadOnlyList<AcpMcpServer> plugins)
    {
        var offered = new List<AcpMcpServer>();
        if (KnowledgeConnector.Offer(
                Environment.GetEnvironmentVariable(KnowledgeConnector.PathVariable), DaorisHome.Resolve(),
                AppContext.BaseDirectory, scope: Driver.ConnectorScope(_home, sessionId, null)) is { } connector)
        {
            offered.Add(connector);
        }

        offered.AddRange(plugins);
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
    /// <param name="keep">Told the id <c>session/new</c>'s answer names, kept for the record (MSG1c); null keeps none.</param>
    /// <returns>The session's high-water context, or null when it reported none.</returns>
    private async Task<AcpUsage?> CaptureProtocolAsync(
        string sessionId, Process process, string transcript, ProtocolChat chat, Action<JsonElement>? said,
        Action<string>? keep)
    {
        // A conversation going on appends to the transcript of the run before it (MSG1c).
        await using var file = new StreamWriter(transcript, append: chat.GoOn is not null);
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

            _output?.Append(sessionId, text);
        }

        void Record(SessionEvent e) => _events.Keep(sessionId, e, Line);

        var errors = Driver.PumpAsync(process.StandardError, file, sessionId, _output, CancellationToken.None);
        var session = new AcpSession(
            process.StandardOutput, process.StandardInput, Line, closeTimeout: null, chat.Posture, chat.Meta, Record,
            streams: _output is null ? null : new SessionStreams(_output, sessionId),
            // Its model and effort, told each time they change, so the page offers what the agent does (AGT6b).
            onOptions: options => OptionsChanged?.Invoke(sessionId, ThePersons(options)),
            onConversation: keep);
        // What it says about its account's windows, kept as it says it (TOOL6c).
        if (said is not null) session.LimitsSaid += said;
        // Its background work stoppable from its tab for as long as the conversation lasts (CONSOLE3a).
        using var stops = _output is null ? null : _processes.OpenTaskStops(sessionId, session.StopTaskAsync);
        // The person's words said during a turn, as the record keeps them where the session took them (MSG1c, D136 §3).
        session.OnTaken(words =>
        {
            Line($"— the conversation took what the person added: {words.Text}");
            Record(Driver.Words(words));
        });

        Exception? failed = null;
        try
        {
            // 🔴 An ended chat goes on in its own conversation (MSG1c, D137 §2.2): resumed by the id kept for it, never a
            // new session, which would be a new conversation under the same record.
            if (chat.GoOn is { } goOn) await session.ResumeAsync(chat.Cwd, goOn.Conversation, CancellationToken.None, chat.Servers).ConfigureAwait(false);
            else await session.OpenAsync(chat.Cwd, CancellationToken.None, chat.Servers).ConfigureAwait(false);
            chat.Opened(session, Line, Record);
        }
        catch (AcpResumeRefused refused) when (chat.GoOn is { } goOn)
        {
            // Not a turn that failed: nothing was prompted. The agent's own words stay in this machine's record of the
            // conversation; the reason goes back to the record, which goes back to how it ended (MSG1b's rule).
            failed = refused;
            goOn.Refused = refused.Why;
            chat.Opened(null, Line, Record);
            Line($"— {refused.Message}.");
            Record(new SessionEvent { Kind = SessionEventKind.Note, Text = $"— {refused.Message}." });
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }
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
        return session.Usage;
    }

    /// <summary>A conversation whose turns are visible — what every door's question about a turn asks.</summary>
    private interface ITurnedChat
    {
        ChatTurns Turns { get; }

        /// <summary>Let the turns already asked for run, then end the input.</summary>
        Task FinishAsync(Action endInput);
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
    ///
    /// <para><b>On the next-step door</b> (MSG1c, D137 §2.1), where the agent said it takes a prompt during a turn, a word
    /// said while a turn runs goes at once, shown waiting with its reach and taken where the agent hands the turn off to it
    /// (D136's door, a chat's now). A stop with such a word on its way says the agent may have read it.</para>
    /// </remarks>
    private sealed class ProtocolChat : ITurnedChat
    {
        private readonly TaskCompletionSource<AcpSession?> _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Func<bool> _stopped;
        private readonly Func<string, string?>? _limited;
        private readonly Action? _wentOn;
        private Action<string> _line = _ => { };
        private Action<SessionEvent> _record = _ => { };

        // The words handed to the running turn and not yet answered (MSG1c), and those a stop was asked while on their way.
        private readonly object _gate = new();
        private readonly HashSet<string> _onTheirWay = new(StringComparer.Ordinal);
        private readonly HashSet<string> _stoppedUnder = new(StringComparer.Ordinal);

        /// <param name="limited">
        /// A refused turn's failure, read for an account's limit (TOOL4d): the sentence its record takes, or null.
        /// </param>
        /// <param name="goOn">The ended chat's conversation this one goes on in, resumed rather than opened (MSG1c); null for a new one.</param>
        /// <param name="wentOn">Told once the words it goes on with are on the wire (MSG1c).</param>
        public ProtocolChat(
            string? posture, object? meta, string cwd, IReadOnlyList<AcpMcpServer> servers,
            Action<ChatQueue> changed, Func<bool> stopped, Func<string, string?>? limited = null,
            ResumeAsk? goOn = null, Action? wentOn = null)
        {
            _stopped = stopped;
            _limited = limited;
            _wentOn = wentOn;
            Posture = posture;
            Meta = meta;
            Cwd = cwd;
            Servers = servers;
            GoOn = goOn;
            Turns = new ChatTurns(
                ready: async () => await _open.Task.ConfigureAwait(false) is not null,
                take: TurnAsync,
                interrupt: CancelAsync,
                changed,
                steer: Steer);
        }

        public string? Posture { get; }

        public object? Meta { get; }

        public string Cwd { get; }

        public IReadOnlyList<AcpMcpServer> Servers { get; }

        public ChatTurns Turns { get; }

        /// <summary>The ended chat's conversation this one goes on in (MSG1c), or null for a new conversation.</summary>
        public ResumeAsk? GoOn { get; }

        /// <summary>The session, once it opened; null while it opens, or when it could not.</summary>
        public AcpSession? Session => _open.Task.IsCompletedSuccessfully ? _open.Task.Result : null;

        /// <summary>The session is open — or could not be, and then every turn asked for is dropped.</summary>
        public void Opened(AcpSession? session, Action<string> line, Action<SessionEvent> record)
        {
            _line = line;
            _record = record;
            _open.TrySetResult(session);
        }

        /// <summary>
        /// A word said while a turn is on the wire, handed to it at once where the agent said it takes words then (MSG1c,
        /// D137 §2.1): shown waiting with its reach under a fresh id, then sent as a prompt of the same session. What
        /// completes when the agent has answered it; null where the door takes no word during a turn.
        /// </summary>
        /// <remarks>Called under the turns' lock: nothing here calls back into them.</remarks>
        private Task? Steer(ChatMessage message)
        {
            if (Session is not { NextStep: true } session) return null;

            // Its own id, so the words shown while they wait pair with the same words where the session took them (D136 §3).
            var words = message with { Id = $"said-{Guid.NewGuid():N}"[..13] };
            if (Told(words) is { } told) _record(told);
            _line($"— the person added, which reaches the conversation at its next step: {words.Text}");
            _record(Driver.Words(words) with { Reaches = "next-step" });
            lock (_gate) _onTheirWay.Add(words.Id!);
            return AnsweredAsync(words, session.SayAsync(words, CancellationToken.None));
        }

        /// <summary>
        /// A word handed to the running turn, answered: said where the agent refused it, where it never reached the agent, or
        /// where the person's stop was asked while it was on its way and the agent answered it cancelled, since it may have
        /// read it and its answer was not kept (STEER1 §3).
        /// </summary>
        private async Task AnsweredAsync(ChatMessage words, Task<string> answered)
        {
            string? why = null;
            try
            {
                var reason = await answered.ConfigureAwait(false);
                bool stoppedUnder;
                lock (_gate) stoppedUnder = _stoppedUnder.Contains(words.Id!);
                if (stoppedUnder && reason == "cancelled") why = "— it may have read what you added; its answer was not kept.";
            }
            catch (AcpRefusal refused)
            {
                // A word the agent would not take costs that word, in its own words, never the conversation.
                why = $"— the conversation would not take what the person added: {refused.Words}";
            }
            catch (Exception error) when (error is DriverException or IOException or ObjectDisposedException or InvalidOperationException)
            {
                why = $"— what the person added never reached it: {error.Message}";
            }
            finally
            {
                lock (_gate)
                {
                    _onTheirWay.Remove(words.Id!);
                    _stoppedUnder.Remove(words.Id!);
                }
            }

            if (why is null) return;
            _line(why);
            // The words' id beside the line, so the page settles any it still shows waiting (MSG1f).
            _record(new SessionEvent { Kind = SessionEventKind.Note, Text = why, Words = [words.Id!] });
        }

        private async Task TurnAsync(ChatMessage message, Action sent)
        {
            if (await _open.Task.ConfigureAwait(false) is not { } session) return;

            try
            {
                if (message.GoesOn is { } goOn)
                {
                    // 🔴 The words an ended chat goes on with (MSG1c, D137 §2.2, §3.1): the driver's first line, then each word
                    // under the id its record gave it, then one prompt of one block each; off the record once on the wire.
                    foreach (var opening in goOn.Opening())
                    {
                        if (opening.Kind == SessionEventKind.Note && opening.Text is { } note) _line(note);
                        _record(opening);
                    }

                    await session.PromptAsync(goOn.Blocks, CancellationToken.None, () =>
                    {
                        sent();
                        _wentOn?.Invoke();
                    }).ConfigureAwait(false);
                    return;
                }

                if (Told(message) is { } told) _record(told);
                _record(Asked(message));
                // Each attached file a `resource_link` to where it is kept (CONV4c).
                await session.PromptAsync(message.Prompt, CancellationToken.None, sent, message.Files).ConfigureAwait(false);
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

                // A refusal the door carried, read for an account's limit (TOOL4d) before the turn's end is recorded, so
                // the refused turn is counted after the turns that ended. Never the transcript's words (D125 §1.4).
                if (error is DriverException && _limited?.Invoke(error.Message) is { } cooling)
                {
                    _line($"— {cooling}");
                    _record(new SessionEvent { Kind = SessionEventKind.Note, Text = cooling });
                }

                // 🔴 And the turn ENDS (REV3). Only a turn event closes a turn on the page, so a refused
                // one drew *working…* under this very note until the next message — while the composer,
                // told by the queue that nothing was taking, offered to send.
                _record(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
            }
        }

        private async Task CancelAsync()
        {
            if (!_open.Task.IsCompletedSuccessfully || _open.Task.Result is not { } session) return;

            // 🔴 A person's stop is never refused, even with a word on its way (D137 §2.1): the agent may still act on that
            // word with its answer dropped (STEER1 §3), and its record says so once the word is answered.
            lock (_gate) _stoppedUnder.UnionWith(_onTheirWay);
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
    private sealed class NativeChat : ITurnedChat
    {
        private readonly object _gate = new();
        private TaskCompletionSource? _turn;

        /// <param name="ready">
        /// What a conversation going on waits for before its words go (MSG1c): its process tracked, so a send reaches it.
        /// Null for a new conversation, whose first message comes from the person once it is running.
        /// </param>
        /// <param name="wentOn">Told once the words it goes on with are on stdin (MSG1c).</param>
        public NativeChat(
            string sessionId, ISessionAdapter adapter, SessionProcesses processes, Action<SessionEvent> record,
            Action<ChatQueue> changed, Task<bool>? ready = null, Action? wentOn = null)
        {
            Turns = new ChatTurns(
                ready: () => ready ?? Task.FromResult(true),
                take: (message, sent) =>
                {
                    var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    lock (_gate) _turn = ended;
                    string line;
                    if (message.GoesOn is { } goOn)
                    {
                        // The words an ended chat goes on with (MSG1c, D137 §2.2): the driver's first line, then each word under
                        // the id its record gave it, then all of them as one message, joined by a blank line.
                        foreach (var opening in goOn.Opening()) record(opening);
                        line = adapter.FrameMessage(goOn.Prompt);
                    }
                    else
                    {
                        if (Told(message) is { } told) record(told);
                        record(Asked(message));
                        // Each attached file named by its path, which the agent reads with its own tool
                        // (CONV4c, measured); the record keeps the person's words, not these lines.
                        line = adapter.FrameMessage(message.Prompt + ChatFiles.PathLines(message.Files));
                    }

                    if (!processes.Send(sessionId, line))
                    {
                        // Nothing is listening: the process is going, and its watch concludes the record.
                        record(new SessionEvent { Kind = SessionEventKind.Note, Text = "the message could not be sent: the agent had gone." });
                        Gone();
                        return Task.CompletedTask;
                    }

                    sent();
                    if (message.GoesOn is not null) wentOn?.Invoke();
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
            await _service.AdvanceAsync(sessionId, state, note: note).ConfigureAwait(false);
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
