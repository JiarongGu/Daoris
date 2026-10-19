using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Driver;

/// <param name="SessionId">The record's id, when one was opened — null when nothing started.</param>
/// <param name="Message">What to tell the person: the ledger's sentence, or why nothing began.</param>
public sealed record ChatStart(string? SessionId, string Message);

/// <summary>
/// Ask Daoris's conversation placed (HELPSTART1, <see cref="ChatRunner.PlaceHelpAsync"/>): its record open in the room's folder,
/// its agent and account chosen, nothing spawned yet.
/// </summary>
/// <param name="SessionId">The record's id — null when nothing was placed, and <paramref name="Message"/> says why.</param>
/// <param name="Message">The ledger's sentence, or why nothing was placed.</param>
public sealed record HelpPlaced(string? SessionId, string Message)
{
    internal ISessionAdapter? Adapter { get; init; }

    internal HarnessSelection? Selection { get; init; }
}

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
/// once, its record reopening and its own conversation resumed with them (<see cref="ChatGoOn"/>).</para>
/// </remarks>
public sealed class ChatRunner : IDisposable
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

    // What the runner's classes share (PART10g): the fields above that more than one reads, the stores and the live maps. The
    // fields stay here too, the same objects, for the front itself (PART10h left them).
    private readonly ChatContext _context;

    // A conversation handed over, and one moved by its account's usage curve: classes over the context, the front keeping its
    // users' calls (PART10g).
    private readonly ChatHandOver _handOver;

    private readonly CurveMove _curve;

    // What the conversations' turns are judged by (NOTIFY1a): the person's words, their stops and each turn's end.
    private readonly AttentionWatch _answers = new();

    // Ask Daoris's conversations, the helpers' words and an ended chat's going-on: classes over the context too (PART10h). The
    // go-on holds the runner and reads the other two.
    private readonly ChatHelp _help;

    private readonly ChatHelperWords _words;

    private readonly ChatGoOn _goOn;

    // The requests *Start a conversation on its first words* has answered (NEW1a), made when the first carries an id.
    private StartRequests? _starts;

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
        // Its turns kept in the usage history (USAGECAP1a2), watched once however many runners share the record.
        _events = UsageTurns.Watched(home, events ?? new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions)));
        _context = new ChatContext(service, home, processes, output, _harnesses, _events);
        _handOver = new ChatHandOver(_context, this);
        _curve = new CurveMove(_context, _handOver);
        _help = new ChatHelp(_context, this);
        _words = new ChatHelperWords(_context);

        // The go-on hears the record's events from its construction: the person's words to an ended chat (MSG1d, MSG1c).
        _goOn = new ChatGoOn(_context, this, _handOver, _help, _words);
    }

    /// <summary>
    /// The ended chats whose words wait from while no runner heard them are taken up, as the shell comes up (MSG1c2,
    /// <see cref="ChatGoOn.TakeUpAsync"/>).
    /// </summary>
    public Task<IReadOnlyList<ChatStart>> TakeUpAsync(CancellationToken ct = default) => _goOn.TakeUpAsync(ct);

    /// <summary>Go on with an ended chat the person wrote to (MSG1c, <see cref="ChatGoOn.GoOnAsync"/>).</summary>
    public Task<ChatStart> GoOnAsync(
        string sessionId, DriverConfig? config = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        _goOn.GoOnAsync(sessionId, config, onEnded, ct);

    /// <summary>
    /// A conversation this runner took up by itself ended (MSG1c2): the session and the state its record took, as a start's
    /// <c>onEnded</c> is told. No route started it, so the shell announces it as the page's <c>SESSION_ENDED</c>.
    /// </summary>
    public event Action<string, string>? TakenUpEnded
    {
        add => _goOn.TakenUpEnded += value;
        remove => _goOn.TakenUpEnded -= value;
    }

    /// <summary>
    /// A hand-over's or a curve move's take is over, its box gone (HANDWORDS1, <see cref="ChatHandOver.TakeEnded"/>): the shell's
    /// judge hands the words it held meanwhile to the conversation that continues it.
    /// </summary>
    public event Action<string>? TakeEnded
    {
        add => _handOver.TakeEnded += value;
        remove => _handOver.TakeEnded -= value;
    }

    /// <summary>
    /// The machine as Ask Daoris's room says it (ASKHIST1, <see cref="ChatHelp.DescribeHelp"/>): the shell's reading, which a
    /// conversation going on finds the room written from. Null where nothing reads it.
    /// </summary>
    public Func<CancellationToken, Task<HelpMachine?>>? DescribeHelp
    {
        get => _help.DescribeHelp;
        set => _help.DescribeHelp = value;
    }

    /// <summary>
    /// How long an Ask Daoris start waits for the machine's reading before the room as last written serves (HELPSTUCK1,
    /// <see cref="ChatHelp.ReadBound"/>).
    /// </summary>
    internal TimeSpan HelpReadBound
    {
        get => _help.ReadBound;
        set => _help.ReadBound = value;
    }

    /// <summary>Place a new Ask Daoris conversation from an earlier one's words, its room read and agent spawned after (HELPSTART1b, <see cref="ChatHelp.PlaceHelpFromAsync"/>).</summary>
    public Task<HelpPlaced> PlaceHelpFromAsync(string earlier, string adapter, DriverConfig config, CancellationToken ct = default) =>
        _help.PlaceHelpFromAsync(earlier, adapter, config, ct);

    /// <summary>Start a new Ask Daoris conversation from an earlier one's words (ASKHIST1, <see cref="ChatHelp.StartHelpFromAsync"/>).</summary>
    public Task<ChatStart> StartHelpFromAsync(
        string earlier, string adapter, DriverConfig config, HelpMachine machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        _help.StartHelpFromAsync(earlier, adapter, config, machine, onEnded, ct);

    /// <summary>
    /// *Start a conversation with these words* (MSG1f2): <see cref="StartFrom.RunAsync"/> through this runner's own start, on
    /// the adapter and account a new conversation takes, and its own first message. The record they were written to goes on
    /// nowhere meanwhile: a word said to it while this runs waits rather than reopening it under the words handed on.
    /// </summary>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    public async Task<StartedFrom> StartFromAsync(
        string sessionId, string adapter, DriverConfig config, Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (_context.Closing) return new StartedFrom(null, false, null, "the application is closing, so no conversation starts now.");

        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_context.GoingOn.TryAdd(sessionId, going.Task))
        {
            return new StartedFrom(null, false, StartFrom.Running, $"conversation `{sessionId}` is going on with its words already.");
        }

        try
        {
            return await StartFrom.RunAsync(
                _service, _home, _events, _output, sessionId,
                (repository, token) => StartAsync(repository, adapter, config, onEnded, ct: token),
                (id, text, files, preface) => Say(id, text, files, preface),
                ct).ConfigureAwait(false);
        }
        finally
        {
            _context.GoingOn.TryRemove(sessionId, out _);
            going.TrySetResult();
        }
    }

    /// <summary>
    /// Open a conversation on its first words (NEW1a): <see cref="StartAsync"/>, then <see cref="Say"/> with the words and
    /// files. A request id makes a retry answer the first start (<see cref="StartRequests"/>).
    /// </summary>
    /// <param name="ownTree">Absent is the driver's choice; true a tree of its own; false the checkout.</param>
    /// <exception cref="DriverException">The request id is malformed.</exception>
    public Task<StartedFrom> StartWithAsync(
        string repository, string adapter, DriverConfig config, string text, IReadOnlyList<ChatUpload> files, string? requestId,
        bool? ownTree = null, string? profile = null, Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        Task<StartedFrom> Run() => _context.Closing
            ? Task.FromResult(new StartedFrom(null, false, null, "the application is closing, so no conversation starts now."))
            : StartWith.RunAsync(
                token => StartAsync(repository, adapter, config, onEnded, profile, ownTree, token),
                (id, words, attached, preface) => Say(id, words, attached, preface),
                text, files, ct);

        if (requestId is null) return Run();
        _starts ??= new StartRequests(_home);
        return _starts.RunAsync(requestId, Run);
    }

    /// <summary>Whether a conversation opens in a tree of its own: said, or else the repository's standing choice (D51).</summary>
    public static bool Isolates(bool? ownTree, DriverConfig config, string repository) =>
        ownTree ?? config.OpensOwnTree(repository);

    // The process a new conversation in a tree of its own is told with its first message (PROCESS1d-ii), held until then. A map of
    // its own, never the browser's, which a hand-over's briefing appends: a conversation handed over is told Daoris's record instead.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ProcessBriefing> _toldProcess =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// *Hand a conversation over* (MIXAGENT1a-i, D163): judged and kept in its box, then taken at once, or as its turn ends where
    /// it is talking (<see cref="ChatHandOver.HandOverAsync"/>).
    /// </summary>
    /// <param name="why">One of <see cref="HandOverWhy"/>.</param>
    /// <param name="askedBy">One of <see cref="HandOverBy"/>.</param>
    /// <param name="onEnded">Told when the new conversation ends, as a start's is.</param>
    public Task<HandedOver> HandOverAsync(
        string sessionId, string agent, string? account, string why, string askedBy, DriverConfig config,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default) =>
        _handOver.HandOverAsync(sessionId, agent, account, why, askedBy, config, onEnded, ct);

    /// <summary>
    /// Judge a conversation by its account's usage curve and, where the decision says so, move it (SWITCH1c-ii,
    /// <see cref="CurveMove.MoveAsync"/>): null where nothing was judged.
    /// </summary>
    internal Task<SwitchChoice?> MoveByCurveAsync(
        string sessionId, DriverConfig config, Func<string, string, Task>? onEnded, CancellationToken ct = default) =>
        _curve.MoveAsync(sessionId, config, onEnded, ct);

    /// <summary>What each judged turn's end came to, by session: a test hears it; no person reads it.</summary>
    internal event Action<string, SwitchChoice>? CurveJudged
    {
        add => _curve.Judged += value;
        remove => _curve.Judged -= value;
    }

    /// <summary>
    /// Where a conversation's turns stand — whether one is in flight, and what the person sent that is
    /// not at the harness yet — each time that changes (CONV4a). The page shows the waiting messages as
    /// queued, since they are in no record until they are sent, and offers a stop while a turn runs.
    /// </summary>
    public event Action<string, ChatQueue>? QueueChanged;

    /// <summary>
    /// A conversation answered the person (NOTIFY1a): the turn their words started ended, judged by <see cref="AttentionWatch"/>
    /// as its turns move. Told from the conversation's thread; the shell says it as it says the tick's attention.
    /// </summary>
    public event Action<AttentionEvent>? Answered;

    /// <summary>
    /// A conversation's model and effort, as its agent offers them, each time they change (AGT6b, D98): as
    /// its session opened, after a change, and when the agent changed them itself. Only the options that
    /// are the person's (<see cref="AcpConfigOption.ThePersons"/>).
    /// </summary>
    public event Action<string, IReadOnlyList<AcpConfigOption>>? OptionsChanged;

    /// <summary>The earlier conversation's words, kept as this conversation's file, and the line its agent is told beside them.</summary>
    internal sealed record Handing(IReadOnlyList<KeptFile> Files, string Preface);

    /// <summary>The note a conversation's record takes when the driver holding it closes.</summary>
    public const string ClosedNote =
        "the application closed while this conversation ran; its process was ended with it.";

    /// <summary><see cref="ClosedNote"/> with its code (LANG1a).</summary>
    public static Noted ClosedNoted => Noted.Of(NoteCodes.ChatClosed, ClosedNote);

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
    /// decision was made for, since the person is usually IN the root it would otherwise hold. Null leaves it to those two;
    /// false (NEW1a) is the checkout, whatever the repository's standing choice.
    /// </param>
    /// <param name="tree">
    /// A conversation handed over from another agent runs in the tree the old one ran in (MIXAGENT1a-i, D163 §3): its own folder,
    /// neither grown nor removed here, and refused where it no longer exists. Null opens as before.
    /// </param>
    /// <param name="from">
    /// The conversation this one continues, handed over, with why and who asked (MIXAGENT1a-iii): named on the new record as it
    /// opens. Null for every other conversation.
    /// </param>
    public async Task<ChatStart> StartAsync(
        string repository, string adapter, DriverConfig config,
        Func<string, string, Task>? onEnded = null, string? profile = null, bool? ownTree = null,
        CancellationToken ct = default, string? tree = null, ContinuedFrom? from = null)
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
        if (tree is not null && !Directory.Exists(tree))
        {
            return new(null, "the tree the conversation ran in is gone, so there is nothing for a new one to go on in.");
        }

        var isolated = tree is null && Isolates(ownTree, config, repository);
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

        var workTree = tree ?? opened?.Path ?? root!;

        // Where the tree stands before the conversation begins (SURF6). A chat may open on a DIRTY
        // tree (D49 §3), so this is the commit — not the working state — and the review it feeds is
        // committed work only, for exactly that reason.
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);

        var (sessionId, message) = await _service
            // The tree the conversation runs in (D51) — its own where one was grown, the checkout
            // found above otherwise, stated by the side that is about to spawn into it.
            .OpenChatAsync(
                repository, resolved.Name, selection.Version, selection.Profile, workTree, before, ct, from)
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

        // A conversation in a tree it grew is told its repository's process with its first message (PROCESS1d-ii, D164's amendment
        // point 4), as a driven session is in its instruction. Never the person's checkout, nor a tree handed over, whose new
        // conversation is briefed with Daoris's record.
        if (opened is not null) _toldProcess[sessionId] = await ProcessToldAsync(opened.Path, sessionId, config, known, ct).ConfigureAwait(false);

        ChatStart? started = null;
        try
        {
            return started = await RunAsync(
                sessionId, message, resolved, selection, config,
                RepositoryPlace(config, snapshot.Repositories, known, repository, workTree),
                onEnded, ct).ConfigureAwait(false);
        }
        finally
        {
            // A start that failed, or was cancelled, has no first message to tell it with.
            if (started?.SessionId is null) _toldProcess.TryRemove(sessionId, out _);
        }
    }

    /// <summary>
    /// Where a conversation in a repository runs (D49 §3): its tree, what it may do there and reach outside it (D107), as a
    /// driven session in this repository would, the plugins' servers, and its conversation kept for it to go on (MSG1c).
    /// </summary>
    /// <param name="repository">The repository as the conversation names it, which its rules are composed for.</param>
    internal ChatPlace RepositoryPlace(
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
        var placed = await PlaceHelpAsync(adapter, config, ct).ConfigureAwait(false);
        return placed.SessionId is null
            ? new(null, placed.Message)
            : await RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(machine), onEnded, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Place Ask Daoris's conversation (HELPSTART1, D89's note): its agent resolved, its account chosen and its record opened in
    /// the room's folder, which is the same folder for every conversation, so nothing is read or written yet. What the person's
    /// press waits for; <see cref="RunHelpAsync"/> reads the machine, writes the room and spawns the agent after.
    /// </summary>
    /// <exception cref="DriverException">The agent names no harness this machine knows.</exception>
    public async Task<HelpPlaced> PlaceHelpAsync(string adapter, DriverConfig config, CancellationToken ct = default)
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

        var (sessionId, message) = await _service
            .OpenHelpAsync(resolved.Name, HelpRoom.PathOf(_home), selection.Version, selection.Profile, ct)
            .ConfigureAwait(false);
        if (sessionId is null) return new(null, message);

        // Ask Daoris's opening rotates like a start, on the machine's order (TOOL4f, D125 §3.2), and says so first.
        RotatedOpening.Say(_service, _events, sessionId, resolved.Name, selection, carried: null);
        return new(sessionId, message) { Adapter = resolved, Selection = selection };
    }

    /// <summary>
    /// Run Ask Daoris's conversation placed by <see cref="PlaceHelpAsync"/> (HELPSTART1): the machine read, the room written from
    /// it, then the agent spawned as any conversation's. A reading or a room that fails ends the record <c>failed</c> saying why,
    /// so a conversation the person was handed says what became of it rather than waiting at <c>queued</c>. A reading that does
    /// not answer within <see cref="HelpReadBound"/> (HELPSTUCK1) leaves it to the room as last written, said first in its
    /// conversation, or with none written before ends it <c>failed</c> saying so (<see cref="ChatHelp.ReadAsync"/>).
    /// </summary>
    /// <param name="machine">The machine as the room says it; null where it could not be read.</param>
    public async Task<ChatStart> RunHelpAsync(
        HelpPlaced placed, DriverConfig config, Func<CancellationToken, Task<HelpMachine?>> machine,
        Func<string, string, Task>? onEnded = null, CancellationToken ct = default)
    {
        if (placed is not { SessionId: { } sessionId, Adapter: { } resolved, Selection: { } selection }) return new(null, placed.Message);

        // A start being made, as a start from a conversation's words is: no go-on takes it up meanwhile, and a closing waits for it.
        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _context.GoingOn.TryAdd(sessionId, going.Task);
        try
        {
            string room;
            IReadOnlyList<HelpRead> reads;
            try
            {
                // 🔴 HELPSTUCK1: the reading is waited for within its bound. It walks git across every tree and repository, and on
                // the owner's loaded machine it did not answer for minutes while this conversation sat at `queued`: nothing said, no
                // agent started, the person's words held for it. Past the bound, the room as last written serves, said first in
                // its conversation; with none written before, it ends below saying why.
                var (read, late) = await _help.ReadAsync(machine, ct).ConfigureAwait(false);
                if (late && !HelpRoom.Written(_home))
                {
                    var unread = ChatHelp.NotRead(_help.ReadBound);
                    await Conclude(sessionId, "failed", unread, onEnded).ConfigureAwait(false);
                    return new(null, unread.Note);
                }

                if (late)
                {
                    (room, reads) = (HelpRoom.PathOf(_home), []);
                    _context.Note(sessionId, ChatHelp.AsWritten(_help.ReadBound));
                }
                else
                {
                    var machineRead = read ?? throw new DriverException("this machine could not be read for it: the service is not answering yet");
                    (room, reads) = (HelpRoom.Prepare(_home, machineRead), machineRead.Reads);
                }
            }
            catch (Exception error)
            {
                var cancelled = error is OperationCanceledException && ct.IsCancellationRequested;
                var why = $"Ask Daoris's room could not be written — {error.Message}";
                await Conclude(
                    sessionId, "failed",
                    cancelled ? Noted.Of(NoteCodes.ChatCancelled, "the request that started it was cancelled before its process started.") : Observation.Failure(why),
                    onEnded).ConfigureAwait(false);
                if (cancelled) throw;
                return new(null, why);
            }

            if (_context.Closing)
            {
                await Conclude(sessionId, "failed", Observation.Failure("the application closed before its agent started."), onEnded).ConfigureAwait(false);
                return new(null, "the application is closing, so no conversation starts now.");
            }

            return await RunAsync(sessionId, placed.Message, resolved, selection, config, HelpPlace(room, reads), onEnded, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _context.GoingOn.TryRemove(new KeyValuePair<string, Task>(sessionId, going.Task));
            going.TrySetResult();
        }
    }

    /// <summary>
    /// Where Ask Daoris's conversation runs (HELP1a): its room, the room's allows and a read of its own files folder, and the
    /// checkouts reading across lets it read (D107); the agent's own asking mode; its tools up front.
    /// </summary>
    /// <remarks>
    /// <para><b>Its conversation is kept</b> (ASKHIST1): the person goes back to an earlier one from its history, and words written to
    /// one that ended go on in its own harness conversation by the id kept for it (D137 §2.2), as a repository's chat does.</para>
    ///
    /// <para><b>A plugin's server reaches it only where the plugin declares it for Ask Daoris</b> (HELPPLUG1a, D170 point 3), and
    /// only once the person approved its exact tools (<see cref="HelpGrants"/>, D170's note): its reads and the writes they
    /// accepted are allowed, any other write asked, which its mode refuses. HELP1a handed it none, since a browser brought up for
    /// a tool it may not use is a window for nothing; a server approved for it names the tools it may use.</para>
    /// </remarks>
    internal ChatPlace HelpPlace(string room, IReadOnlyList<HelpRead> reads) => HelpPlaceIn(_home, room, reads);

    /// <inheritdoc cref="HelpPlace"/>
    internal static ChatPlace HelpPlaceIn(string home, string room, IReadOnlyList<HelpRead> reads) =>
        new(
            HelpRoom.Repository, room,
            (file, id) =>
            {
                var grants = HelpGrants.Read(home, PluginCatalog.Load(home, AdapterSet.Built().Names));
                return HelpRoom.Rules(file, ChatFiles.Folder(home, id), reads, grants.Allow, grants.Ask);
            },
            Plugins: true, ConnectorOnPipe: true, Posture: HelpRoom.Posture, ToolsUpFront: true)
        {
            Keeps = true,
            ServersTo = PluginServerTo.Help,
        };

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
    internal sealed record ChatPlace(
        string Name, string Tree, Func<PermissionFile, string, RuleLists> Rules, bool Plugins, bool ConnectorOnPipe,
        string? Posture, bool ToolsUpFront)
    {
        /// <summary>The checkouts it may also write into (D107), which its tree guard lets a write reach.</summary>
        public IReadOnlyList<string> Also { get; init; } = [];

        /// <summary>
        /// Whose plugins' servers it is handed, where <see cref="Plugins"/> hands any (HELPPLUG1a): a repository's conversation
        /// those declared for sessions, Ask Daoris's those declared for it.
        /// </summary>
        public string ServersTo { get; init; } = PluginServerTo.Sessions;

        /// <summary>
        /// Whether its harness conversation's id is kept for it (MSG1c, D137 §4.2), so the conversation goes on when the
        /// person writes to it after it ended: a repository's conversation, and since ASKHIST1 Ask Daoris's too.
        /// </summary>
        public bool Keeps { get; init; }
    }

    /// <summary>
    /// The plugins' servers a conversation is handed, with Daoris's own browser brought up for one that drives it, or
    /// that one withheld and the sentence why (D78); and a <c>plugin.served</c> line for each, handed or withheld
    /// (PLUGUI1e, D119 §4.2), read from the catalogue the servers came from, as a driven session's are.
    /// </summary>
    /// <remarks>
    /// The catalogue is read as the driver loop reads it, reserving the build's names (CHATSERVERS1). Never the roster's live
    /// set: that holds the plugins' own agents, so a plugin declaring an agent and a server was refused as one this build
    /// carries, and its servers were withheld from every conversation.
    /// </remarks>
    /// <param name="to">Whose servers: those declared for sessions, or for Ask Daoris (HELPPLUG1a).</param>
    internal static async Task<(IReadOnlyList<AcpMcpServer> Handed, Noted? Notice, bool Drives, IReadOnlyList<string> Drivers)> HandServersAsync(
        string home, IInAppBrowser? browser, PluginLog plugins, string sessionId, CancellationToken ct, string to = PluginServerTo.Sessions)
    {
        var catalog = PluginCatalog.Load(home, AdapterSet.Built().Names);
        // Ask Daoris takes only the servers the person approved for it (HelpGrants, D170's note); a session, its own.
        var servers = to == PluginServerTo.Help ? HelpGrants.Read(home, catalog).Servers : catalog.ServersTo(to);
        var handed = await InAppBrowserServers.HandNotedAsync(servers, browser, ct).ConfigureAwait(false);
        plugins.Served(catalog, sessionId, handed.Handed, to);
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
    internal async Task<ChatStart> RunAsync(
        string sessionId, string message, ISessionAdapter resolved, HarnessSelection selection, DriverConfig config,
        ChatPlace place, Func<string, string, Task>? onEnded, CancellationToken ct, GoingOn? goOn = null)
    {
        var workTree = place.Tree;
        var transcript = DaorisHome.Entry(_home, HomeEntry.Sessions, $"{sessionId}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(transcript)!);

        Process process;
        string? rules = null;
        string? servers = null;
        string? skills = null;
        object? meta = null;
        IReadOnlyList<AcpMcpServer> pluginServers = [];
        Noted? browserNotice = null;
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
                    // Its own record, so a take through the connector its harness starts marks it (CHATTAKE1b).
                    Session = sessionId,
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

            // HELPPLUG1b (D170 point 4): the skills plugins hand this conversation's destination, composed under the home and
            // handed by the harness's own session-only door; the protocol door carries them beside its rules below.
            IReadOnlyList<string> skillPlugins = [];
            if (place.Plugins)
            {
                (skills, skillPlugins) = SpawnSkills.Hand(
                    resolved, info, _home, sessionId, PluginCatalog.Load(_home, AdapterSet.Built().Names), place.ServersTo);
            }

            // What the conversation's agent may do (PERM1, D72), handed by each door's own way: a flag on
            // the pipe, `session/new`'s `_meta` on the protocol door, now that a conversation there opens a
            // session (CONV3b).
            if (resolved.TakesSettings)
            {
                var file = PermissionRules.Load(_home);
                rules = SpawnSettings.Write(
                    _home, sessionId, place.Rules(file, sessionId), PermissionRules.HardDeny(file),
                    PermissionRules.GuardsTree(file) ? TreeGuard.For(_home, workTree, place.Also) : null,
                    // Which agent made each call, and each subagent's start and stop (FABRIC-C1a).
                    ChildLineage.HookFor(_home, sessionId));
                if (rules is not null && resolved.Wire == SessionWire.Pipe) resolved.HandSettings(info, rules);
                else if (rules is not null) meta = resolved.AcpSessionMeta(rules, skillPlugins);
            }

            // The plugins' servers, resolved once for both doors — with Daoris's own browser brought up
            // for one that drives it (D78), or that one left out and the conversation told why.
            if (place.Plugins)
            {
                IReadOnlyList<string> drivers;
                (pluginServers, browserNotice, drivesBrowser, drivers) = await HandServersAsync(
                    _home, _browser, _plugins, sessionId, ct, place.ServersTo).ConfigureAwait(false);
                // A new conversation's agent is told of the browser with its first message (BRWHINT1a); one going on resumes a
                // conversation already told when it opened, so it is told nothing again.
                if (goOn is null && InAppBrowserServers.Told(drivers) is { } line) _context.Browsers[sessionId] = line;
                if (browserNotice is not null)
                {
                    _context.Record(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = browserNotice.Note, Parts = browserNotice.Parts });
                }
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

            // Through the start gate (PROCSTART2): a conversation's press may reach here before its first wait.
            process = await ProcessStarts.Shared.StartAsync(info, CancellationToken.None).ConfigureAwait(false)
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
            SpawnSkills.Remove(skills);
            var cancelled = error is OperationCanceledException && ct.IsCancellationRequested;
            await Conclude(
                sessionId, "failed",
                cancelled
                    ? Noted.Of(NoteCodes.ChatCancelled, "the request that started it was cancelled before its process started.")
                    : Observation.Failure(error.Message),
                onEnded).ConfigureAwait(false);
            if (cancelled) throw;
            return new(null, error.Message);
        }

        // Its turns are kept in the usage history under the account it runs as (USAGECAP1a2), as a driven session's are: bound
        // before any word is queued to it.
        UsageTurns.Bound(_home, _events, sessionId, TurnBinding.Of(resolved, selection.Profile));

        // The conversation outlives this call: the person types, the harness answers, and the record
        // moves when the process does. Watched on an unbound token deliberately — a chat is not ended
        // by the request that started it.
        _context.Talking[sessionId] = resolved;
        // The room holds one at a time (ASKHIST1): going back to an earlier conversation sets this one aside first.
        if (string.Equals(place.Name, HelpRoom.Repository, StringComparison.Ordinal)) _context.Rooms[sessionId] = 0;

        // 🔴 On the protocol door a conversation is ONE session held over the wire (CONV3b): opened
        // once, each message a turn on it. It used to be spawned as a pipe, and a person's words went
        // into the JSON-RPC stream as raw text — the harness declared itself interactive and could not
        // hold a conversation at all.
        ProtocolChat? chat = null;
        NativeChat? native = null;
        var mapper = resolved.StructuredOutput();
        // The turn's end it was last judged at by its account's usage curve (SWITCH1c-ii): told under its turns' own lock, in order.
        DateTimeOffset? judged = null;
        void Changed(ChatQueue queue)
        {
            QueueChanged?.Invoke(sessionId, queue);
            // A conversation asked to go on with another agent while it talked is handed over as its turn ends (MIXAGENT1a-i).
            _handOver.Due(sessionId, queue, config, onEnded);
            // One over its account's usage curve goes on with another account of its agent as its turn ends (SWITCH1c-ii), judged once.
            if (CurveMove.TurnJustEnded(queue, judged))
            {
                judged = queue.LastTurnEnded;
                _curve.Due(sessionId, config, onEnded);
            }

            // A turn the person's words started has ended (NOTIFY1a): judged last, so a listener's failure moves nothing above.
            if (_answers.Turned(sessionId, place.Name, queue) is { } answered) Answered?.Invoke(answered);
        }
        // A conversation going on hands its words to the native door only once its process is tracked, so a word the person
        // says meanwhile goes after them; the protocol door waits for its session to open anyway (MSG1c).
        var tracked = goOn is null ? null : new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (resolved.Wire == SessionWire.Acp)
        {
            chat = new ProtocolChat(
                place.Posture ?? resolved.AcpPosture, meta, workTree, Servers(sessionId, pluginServers), Changed,
                stopped: () => _processes.WasStopRequested(sessionId),
                limited: failure => Limited(sessionId, resolved, selection.Profile, failure, config.CoolOff),
                signedOut: failure => SignedOut(resolved, selection.Profile, failure),
                goOn: goOn?.Ask,
                // On this door the words went once their prompt is on the wire.
                wentOn: goOn is null ? null : () =>
                {
                    if (goOn.Sent(named: false)) _ = _goOn.WentOnAsync(sessionId, goOn, resolved.Name);
                });
            _context.Turned[sessionId] = chat;
        }
        else if (mapper is not null)
        {
            // 🔴 Turns are only visible where the wire says where one ends (CONV4a). A text-only pipe
            // takes each line at once, as it always did.
            native = new NativeChat(
                sessionId, resolved, _processes, e => _context.Record(sessionId, e), Changed, tracked?.Task,
                // On this door the words went once their line is on stdin and the harness has opened the conversation.
                wentOn: goOn is null ? null : () =>
                {
                    if (goOn.Sent(named: true)) _ = _goOn.WentOnAsync(sessionId, goOn, resolved.Name);
                });
            _context.Turned[sessionId] = native;
        }

        // The person's words a conversation goes on with are its first turn (MSG1c, D137 §2.2), queued before any word said
        // to it once it runs.
        var first = goOn is null ? null : new ChatMessage(goOn.Ask.Prompt, []) { GoesOn = goOn.Ask };
        if (first is not null)
        {
            // Their words, so the turn's end they start is an answer they wait on (NOTIFY1a).
            _answers.Asked(sessionId);
            (chat?.Turns ?? native?.Turns)?.Say(first);
        }

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
            new Watched(resolved.Name, place.Keeps, goOn) { Skills = skills });
        _context.Watching[sessionId] = watch;
        _ = watch.ContinueWith(
            _ => _context.Watching.TryRemove(new KeyValuePair<string, Task>(sessionId, watch)), TaskScheduler.Default);
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
        _context.Close();
        _goOn.Stop();
        try
        {
            Task.WaitAll([.. _context.GoingOn.Values], bound);
        }
        catch (AggregateException)
        {
            // A start that failed has said so where it could; either way it has ended.
        }

        var watches = _context.Watching.ToArray();
        foreach (var (id, _) in watches) _processes.Stop(id, ClosedNoted);

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
        if (_context.Turned.TryGetValue(sessionId, out var chat))
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
        var said = Compose(sessionId, message, files, preface);

        if (_context.Turned.TryGetValue(sessionId, out var chat))
        {
            // Before it is taken, so even a turn that ends at once is the answer the person waits on (NOTIFY1a).
            _answers.Asked(sessionId);
            var reach = chat.Turns.Take(said);
            reaches = Spelled(reach);
            return reach is not null;
        }

        return SendText(sessionId, said);
    }

    /// <summary>
    /// The person's *Send now* to a live chat (STEER2a, D136's 2026-10-16 note), carried out by its door: steered into the running
    /// turn where its agent steers, else queued with the turn stopped keeping them so they go next (D90), or the next turn at once
    /// where none runs (<see cref="ChatTurns.TakeNowAsync"/>). A text-only door has no turn to steer or stop: its words go as they
    /// always did, said so. Null when there is nothing listening.
    /// </summary>
    /// <exception cref="DriverException">The files are more than a message carries.</exception>
    public async Task<SentNow?> SayNowAsync(
        string sessionId, string message, IReadOnlyList<ChatUpload>? files = null, string? preface = null)
    {
        var said = Compose(sessionId, message, files, preface);
        if (_context.Turned.TryGetValue(sessionId, out var chat))
        {
            // Before it is taken, as a word said is (NOTIFY1a): the stopped turn's end is no answer while these words wait.
            _answers.Asked(sessionId);
            return await chat.Turns.TakeNowAsync(said).ConfigureAwait(false);
        }

        return SendText(sessionId, said) ? new SentNow(null, Now: false, SendNowWhy.NoInterrupt) : null;
    }

    /// <summary>
    /// The person's message as a live chat's door takes it: the files kept for this conversation (CONV4c), and the preface, the
    /// briefing and the process it is handed once, ahead of its first words. Nothing is kept for a chat nothing here holds.
    /// </summary>
    private ChatMessage Compose(string sessionId, string message, IReadOnlyList<ChatUpload>? files, string? preface)
    {
        var held = _context.Turned.ContainsKey(sessionId) || _context.Talking.ContainsKey(sessionId);
        IReadOnlyList<KeptFile> kept = held && files is { Count: > 0 } ? ChatFiles.Keep(_home, sessionId, files) : [];
        // A conversation started from an earlier one is handed that one's words with the person's first message (ASKHIST1), its
        // line first, so where the person is never crowds it out of the preface's bound.
        if (held && _context.Handing.TryRemove(sessionId, out var handed))
        {
            kept = [.. handed.Files, .. kept];
            preface = preface?.Trim() is { Length: > 0 } where ? $"{handed.Preface}\n\n{where}" : handed.Preface;
        }

        // The browser it was handed is told once, ahead of the first message's words (BRWHINT1a).
        var briefing = held && _context.Browsers.TryRemove(sessionId, out var browserLine) ? browserLine : null;
        // So is the repository's process, for a conversation in a tree it grew (PROCESS1d-ii).
        var process = held && _toldProcess.TryRemove(sessionId, out var told) ? told : null;
        return new ChatMessage(message, kept) { Preface = ChatMessage.Bound(preface), Briefing = briefing, Process = process };
    }

    /// <summary>
    /// A text-only door keeps the person's words where it keeps the agent's — the console. In the record they would be half a
    /// conversation: questions with no answers beside them. A file it is handed is named by its path, which any agent can read.
    /// </summary>
    private bool SendText(string sessionId, ChatMessage said)
    {
        var text = said.Prompt + ChatFiles.PathLines(said.Files);
        var framed = _context.Talking.TryGetValue(sessionId, out var adapter) ? adapter.FrameMessage(text) : text;
        return _processes.Send(sessionId, framed);
    }

    /// <summary>Whether a conversation's turn is on its way to the harness or running there — false on a text-only door.</summary>
    public bool Taking(string sessionId) => _context.Turned.TryGetValue(sessionId, out var chat) && chat.Turns.Running;

    /// <summary>
    /// What a word said now to a live conversation would do (MSG1c, D137 §2.1), in the record's spelling: <c>next-step</c>
    /// while a turn runs on a door whose agent takes words then, <c>turn-end</c> while one runs anywhere else, and null
    /// where it would be the next turn at once, or the door carries only text.
    /// </summary>
    public string? Reach(string sessionId) =>
        _context.Turned.TryGetValue(sessionId, out var chat) && chat.Turns.Running
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
        _context.Turned.TryGetValue(sessionId, out var chat) ? chat.Turns.State : ChatQueue.Idle;

    /// <summary>
    /// Take back or reword one word a conversation holds for its turn (QUEUEEDIT1a); <see cref="QueueEdit.Taken"/> where a turn
    /// took it already, or nothing here holds the conversation.
    /// </summary>
    public QueueEdit EditQueued(string sessionId, string id, string? text) =>
        _context.Turned.TryGetValue(sessionId, out var chat) ? chat.Turns.EditQueued(id, text) : QueueEdit.Taken;

    /// <summary>
    /// Stop the turn a conversation is taking and keep the conversation (CONV4a): what was waiting is
    /// withdrawn and handed back, and the turn in flight ends on the harness's own word.
    /// </summary>
    /// <param name="keep">
    /// Keep what was waiting for the next turn rather than withdraw it (STEER2a, <c>CANCEL_TURN</c> with <c>keep</c>): the turn-end
    /// door's *Send now* (D90), nothing handed back.
    /// </param>
    /// <returns><see cref="TurnStop.Nothing"/> when nothing here holds the conversation, or nothing ran.</returns>
    /// <exception cref="DriverException">The conversation's door carries only text, so it has no turn to stop.</exception>
    public Task<TurnStop> CancelTurnAsync(string sessionId, bool keep = false)
    {
        if (_context.Turned.TryGetValue(sessionId, out var chat))
        {
            // What it kept is the next turn, whose end the person waits on (NOTIFY1a).
            if (keep) return chat.Turns.StopKeepingAsync();

            // The turn ends on the person's own press: no answer to tell them of (NOTIFY1a).
            _answers.Stopped(sessionId);
            return chat.Turns.StopAsync();
        }

        if (_context.Talking.TryGetValue(sessionId, out var adapter))
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
        _context.Turned.TryGetValue(sessionId, out var turned) && turned is ProtocolChat { Session: { } session }
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
        if (!_context.Turned.TryGetValue(sessionId, out var turned) || turned is not ProtocolChat chat)
        {
            throw new DriverException(turned is not null || _context.Talking.ContainsKey(sessionId)
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
        // CONVNOTE2c: the option and its value are the agent's own names, facts the page words the sentence around.
        var set = Noted.Of(
            NoteCodes.ChatOptionSet, $"the person set {option.Name} to {chosen} for this conversation.",
            ("option", option.Name), ("chosen", chosen));
        _context.Record(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = set.Note, Parts = set.Parts });
        return ThePersons(after);
    }

    /// <summary>The options a person may set (<see cref="AcpConfigOption.ThePersons"/>), in the agent's order.</summary>
    private static IReadOnlyList<AcpConfigOption> ThePersons(IReadOnlyList<AcpConfigOption> options) =>
        [.. options.Where(option => option.ThePersons)];

    /// <summary>
    /// The person's message as the record keeps it (CONV4c): their words and the names of what they
    /// attached — never the kept paths, nor the lines or links a door added to reach them.
    /// </summary>
    /// <summary>
    /// What a turn's message puts in the record as it is sent, in order: the browser, the process (PROCESS1d-ii) and where the
    /// person is as their notes, then the person's message. A conversation handed over from another agent opens with Daoris's
    /// briefing instead (MIXAGENT1a-i): its one note says where it came from, since no person spoke.
    /// </summary>
    internal static IEnumerable<SessionEvent> Recorded(ChatMessage message)
    {
        if (message.TakenOver is { } taken)
        {
            yield return new SessionEvent { Kind = SessionEventKind.Note, Text = taken.Note, Parts = taken.Parts };
            yield break;
        }

        if (message.Helped is { } helped)
        {
            // Daoris's words around another agent's claim (MIXAGENT2a-iii), under the id the record gave them so they pair with the
            // event that showed them waiting; never the person's, so no preface and no browser line ride beside them.
            yield return new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Id = helped.Id, Text = message.Text };
            yield break;
        }

        if (message.Said)
        {
            // Handed back after a steer found no turn running (STEER2d): said and told already, taken here.
            yield return ChatMessage.Words(message);
            yield break;
        }

        if (ToldBrowser(message) is { } toldBrowser) yield return toldBrowser;
        if (ToldProcess(message) is { } toldProcess) yield return toldProcess;
        if (Told(message) is { } told) yield return told;
        yield return Asked(message);
    }

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
    internal static SessionEvent? Told(ChatMessage message)
    {
        if (message.Preface is not { Length: > 0 } preface) return null;
        // CONVNOTE2c: the lead by its code; the preface is Daoris's one-copy English to the agent (D142 point 6), shown as written.
        var told = Noted.Of(NoteCodes.ChatToldWhere, "told where the person is:").Then(" ", Noted.Said(preface, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>
    /// The browser the agent was handed and told of beside the person's first words (BRWHINT1a), as the record keeps it: a note of
    /// its own, so the person sees what Daoris told the agent. The line is Daoris's one-copy English (D142 point 6), shown as written.
    /// </summary>
    internal static SessionEvent? ToldBrowser(ChatMessage message)
    {
        if (message.Briefing is not { Length: > 0 } line) return null;
        var told = Noted.Of(NoteCodes.ChatToldBrowser, "told the agent:").Then(" ", Noted.Said(line, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>
    /// The repository's process the agent was told with the person's first words (PROCESS1d-ii), as the record keeps it: a note of
    /// its own under its own lead, the commit rule's mode and where it was set as facts, the words Daoris's one-copy English.
    /// </summary>
    internal static SessionEvent? ToldProcess(ChatMessage message)
    {
        if (message.Process is not { Words.Length: > 0 } process) return null;
        var set = process.Source == CommitRules.DefaultSource ? "by default" : $"set for its {process.Source}";
        var told = Noted.Of(
                NoteCodes.ChatToldProcess, $"told the agent this repository's process, its commit rule `{process.Mode}` {set}:",
                ("mode", process.Mode), ("source", process.Source))
            .Then(" ", Noted.Said(process.Words, NoteBy.Program));
        return new SessionEvent { Kind = SessionEventKind.Note, Text = told.Note, Parts = told.Parts };
    }

    /// <summary>
    /// What a conversation in a tree of its own is told of its process (PROCESS1d-ii, D164's amendment point 4): the commit rule
    /// standing for its repository and its landing, in <see cref="ProcessWords.Conversation"/>'s words with the rule's facts.
    /// </summary>
    internal static ProcessBriefing ProcessTold(ResolvedCommits commits, LandingPlan? plan) =>
        new(ProcessWords.Conversation(commits, plan), commits.Rule.Mode, commits.Source);

    /// <summary>
    /// <see cref="ProcessTold"/> for the tree a new conversation just grew: its landing read as Review's press would read it. A
    /// landing that cannot be read leaves its lead unsaid, never the start: the record is open, so the run that follows concludes
    /// it, cancelled or not, and the commit rule is said either way.
    /// </summary>
    private async Task<ProcessBriefing> ProcessToldAsync(string tree, string sessionId, DriverConfig config, RepoView known, CancellationToken ct)
    {
        LandingPlan? plan;
        try
        {
            plan = await new SessionTrees(_home).PlanAsync(tree, new LandingSubject(sessionId, null, null), ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            plan = null;
        }

        return ProcessTold(CommitRules.Resolve(config, known.Repository, known.Workspace), plan);
    }

    /// <summary>
    /// A conversation's turn the door refused, read for an account's limit (TOOL4d, D125 §2.3): where the adapter's table
    /// recognises it, the account the conversation runs as cools, the log says so, and the line for its record comes back
    /// with its code (CONVNOTE1b), as a session record's cooling line carries it. Null is a refusal as today. The
    /// conversation goes on: the process is still there, and so is the person.
    /// </summary>
    /// <param name="coolOff">The machine's <c>cooloff</c> (TOOL4e), for a limit that names no time; the default when null.</param>
    internal Noted? Limited(string sessionId, ISessionAdapter adapter, string? profile, string failure, TimeSpan? coolOff = null)
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
            sessionId, adapter.Name, profile, read.Seen, _events.TurnsEnded(sessionId) + 1, used: null));
        return CoolingWords.ConversationOf(read.Entry, _harnesses.Zone);
    }

    /// <summary>
    /// A conversation's call the door refused, read for the account's sign-in (SIGNIN1b, D125 ROSTER1b) by the reader a driven
    /// start's conclusion uses (<see cref="RefusedAccounts.SignInRefused"/>): where the adapter's table recognises it, the account the
    /// conversation runs as reads signed out, kept as a reading is, so the next start walks past it, and the line for its record
    /// comes back. Null is a refusal as today. Read from the door's failure, never from the transcript (AGT3c).
    /// </summary>
    internal Noted? SignedOut(ISessionAdapter adapter, string? profile, string failure)
    {
        try
        {
            return RefusedAccounts.SignInRefused(_harnesses, adapter, profile, failure);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A reading not written, or an adapter a plugin no longer declares, costs the next start one more refusal, never
            // this conversation: this runs inside the refused turn's own handling.
            return null;
        }
    }

    /// <summary>What a conversation's watch knows of it beyond its process (MSG1c).</summary>
    /// <param name="Adapter">The adapter it runs on, whose conversation id is kept for it.</param>
    /// <param name="Keeps">Whether its conversation's id is kept, so it can go on once it ended (<see cref="ChatPlace.Keeps"/>).</param>
    /// <param name="GoOn">The ended chat this run goes on with, or null for a new conversation.</param>
    private sealed record Watched(string Adapter, bool Keeps, GoingOn? GoOn)
    {
        /// <summary>The plugins' skills composed for it (HELPPLUG1b, <see cref="SpawnSkills"/>), which go when the conversation does.</summary>
        public string? Skills { get; init; }
    }

    /// <summary>
    /// What a conversation's ending note adds where Daoris kept no id for its harness conversation (MSG1c, D137 §4.2): words
    /// written to it once it ended cannot go on in it, which the record says before anyone writes.
    /// </summary>
    public const string NotKept = "Daoris kept no id for its conversation, so words written to it cannot go on in it.";

    /// <summary>
    /// How a conversation that simply ended says so (the language design §4 rows 61–62), and <see cref="NotKept"/> after it
    /// where no id was kept (MSG1c; the code LANG1a added, its note under D142), each line with its code.
    /// </summary>
    /// <param name="stopped">The person ended it.</param>
    internal static Noted EndedNote(bool stopped, bool notKept)
    {
        var ended = stopped
            ? Noted.Of(NoteCodes.ChatEndedByPerson, "the person ended the conversation.")
            : Noted.Of(NoteCodes.ChatEnded, "the conversation ended; its commits are its record.");
        return notKept ? ended.Then(" ", Noted.Of(NoteCodes.ChatNotKept, NotKept)) : ended;
    }

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
        var keep = watched.Keeps ? _context.Conversations.Keeping(sessionId, watched.Adapter) : null;

        // Declared first, so it is disposed LAST — after the process is untracked and every map that
        // talks to it has let go (REV3: nothing disposed a conversation's process or its pipes).
        using var owned = process;
        using var tracked = _processes.Track(sessionId, process, drivesBrowser: drivesBrowser);
        using var talking = new Disposer(() =>
        {
            _context.Talking.TryRemove(sessionId, out _);
            _context.Turned.TryRemove(sessionId, out _);
            _context.Rooms.TryRemove(sessionId, out _);
            _context.Handing.TryRemove(sessionId, out _);
            _context.Browsers.TryRemove(sessionId, out _);
            _toldProcess.TryRemove(sessionId, out _);
            // Nothing waiting will be sent now, and the page is told its queue emptied.
            chat?.Turns.Gone();
            native?.Gone();
            // Last, so the queue told empty above judges nothing it still waited on (NOTIFY1a).
            _answers.Forget(sessionId);
        });
        try
        {
            // The conversation's messages are recorded as the person sends them (`Say`), so the
            // capture opens with no composed prompt of its own.
            var protocol = chat is not null ? CaptureProtocolAsync(sessionId, process, transcript, chat, said, keep, watched.Adapter) : null;
            Task capture = protocol
                ?? (mapper is not null
                    ? HarnessCapture.CaptureStructuredAsync(
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
                            if (goOn is not null && goOn.Named()) _ = _goOn.WentOnAsync(sessionId, goOn, watched.Adapter);
                        },
                        lineageHome: _home)
                    : HarnessCapture.CaptureAsync(process, transcript, sessionId, _output, CancellationToken.None));

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
                await _goOn.WentBackAsync(sessionId, goOn, refused, watched.Adapter, onEnded).ConfigureAwait(false);
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
                ?? EndedNote(stopped, notKept: watched.Keeps && _context.Conversations.Read(sessionId) is null);
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
                await _goOn.WentBackAsync(sessionId, goOn, refused, watched.Adapter, onEnded).ConfigureAwait(false);
                return;
            }

            await Conclude(sessionId, "failed", Observation.Failure(error.Message), onEnded).ConfigureAwait(false);
        }
        finally
        {
            _output?.Close(sessionId);
            SpawnSettings.Remove(rules);
            SpawnServers.Remove(servers);
            SpawnSkills.Remove(watched.Skills);
            // Its native children end with this run (FABRIC-C1a): lost where it was stopped or failed, ended where it ended clean.
            ChildLineage.End(_home, sessionId, clean: EndedClean(process) && !_processes.WasStopRequested(sessionId), DateTimeOffset.UtcNow);
        }
    }

    /// <summary>Whether a run's process ended on its own with exit 0 (FABRIC-C1a); one still running, or that cannot say, did not.</summary>
    private static bool EndedClean(Process process)
    {
        try
        {
            return process.HasExited && process.ExitCode == 0;
        }
        catch (InvalidOperationException)
        {
            return false;
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
                AppContext.BaseDirectory, scope: KnowledgeConnector.SessionScope(_home, sessionId, null)) is { } connector)
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
    /// <param name="ranOn">The adapter it runs on, which a refused resume's <c>elsewhere</c> names (CONVNOTE2e).</param>
    /// <returns>The session's high-water context, or null when it reported none.</returns>
    private async Task<AcpUsage?> CaptureProtocolAsync(
        string sessionId, Process process, string transcript, ProtocolChat chat, Action<JsonElement>? said,
        Action<string>? keep, string ranOn)
    {
        // A conversation going on appends to the transcript of the run before it (MSG1c).
        await using var file = TranscriptFile.Open(transcript, append: chat.GoOn is not null);
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

        var errors = HarnessCapture.PumpAsync(process.StandardError, file, sessionId, _output, CancellationToken.None);
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
            Record(ChatMessage.Words(words));
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
            // Its reason by code, the agent's words its own (CONVNOTE2e); the English as it always was.
            var refusal = DoorNotes.ResumeRefused(refused, ranOn);
            Line(refusal.Note);
            Record(new SessionEvent { Kind = SessionEventKind.Note, Text = refusal.Note, Parts = refusal.Parts });
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
            var notOpen = DoorNotes.NotOpen(error.Message);
            Line($"— {notOpen.Note}");
            Record(new SessionEvent { Kind = SessionEventKind.Note, Text = notOpen.Note, Parts = notOpen.Parts });
            // A refused sign-in may come at `session/new` as well as at a prompt: which one Claude Code's adapter answers with
            // was not seen (D125 ROSTER1b), so both are read (SIGNIN1b).
            chat.ReadSignIn(error);
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
    internal interface ITurnedChat
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
        private readonly Func<string, Noted?>? _limited;
        private readonly Func<string, Noted?>? _signedOut;
        private readonly Action? _wentOn;
        private Action<string> _line = _ => { };
        private Action<SessionEvent> _record = _ => { };

        // The words handed to the running turn and not yet answered (MSG1c), and those a stop was asked while on their way.
        private readonly object _gate = new();
        private readonly HashSet<string> _onTheirWay = new(StringComparer.Ordinal);
        private readonly HashSet<string> _stoppedUnder = new(StringComparer.Ordinal);

        /// <param name="limited">
        /// A refused turn's failure, read for an account's limit (TOOL4d): the line its record takes, with its code
        /// (CONVNOTE1b), or null.
        /// </param>
        /// <param name="signedOut">
        /// A refused call's failure, read for the account's sign-in (SIGNIN1b): the line its record takes, or null.
        /// </param>
        /// <param name="goOn">The ended chat's conversation this one goes on in, resumed rather than opened (MSG1c); null for a new one.</param>
        /// <param name="wentOn">Told once the words it goes on with are on the wire (MSG1c).</param>
        public ProtocolChat(
            string? posture, object? meta, string cwd, IReadOnlyList<AcpMcpServer> servers,
            Action<ChatQueue> changed, Func<bool> stopped, Func<string, Noted?>? limited = null,
            Func<string, Noted?>? signedOut = null, ResumeAsk? goOn = null, Action? wentOn = null)
        {
            _stopped = stopped;
            _limited = limited;
            _signedOut = signedOut;
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
                steer: Steer,
                steerNow: SteerNow,
                nextStep: () => Session is { NextStep: true });
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
        /// A call the door refused, opening the session or taking a turn, read for the account's sign-in (SIGNIN1b, D125
        /// ROSTER1b): where it was, the account reads signed out and the record says so, its English beside the line's parts as
        /// a session record's note carries them. Only the door's failure is read (AGT3c), as a driven start's conclusion reads it.
        /// </summary>
        public void ReadSignIn(Exception error)
        {
            if (error is not DriverException || _signedOut?.Invoke(error.Message) is not { } signedOut) return;
            _line($"— {signedOut.Note}");
            _record(new SessionEvent { Kind = SessionEventKind.Note, Text = signedOut.Note, Parts = signedOut.Parts });
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
            _record(ChatMessage.Words(words) with { Reaches = "next-step" });
            lock (_gate) _onTheirWay.Add(words.Id!);
            return AnsweredAsync(words, session.SayAsync(words, CancellationToken.None));
        }

        /// <summary>
        /// The person's *Send now* into the turn on the wire (STEER2a, D136's 2026-10-16 note), where the agent steers: shown with
        /// the reach <c>now</c> under a fresh id, then steered into the running turn, cutting the step in flight. The session
        /// takes them where the agent answers <c>injected</c>, through its taken listener; any other answer costs only them, said
        /// by the session in the record. What completes when the agent has answered; null where it never said it steers.
        /// </summary>
        /// <remarks>Called under the turns' lock: nothing here calls back into them.</remarks>
        private Task? SteerNow(ChatMessage message)
        {
            if (Session is not { Steers: true } session) return null;

            var words = message with { Id = $"said-{Guid.NewGuid():N}"[..13] };
            if (Told(words) is { } told) _record(told);
            _line($"— the person sent now, which the conversation takes at once: {words.Text}");
            _record(ChatMessage.Words(words) with { Reaches = SendNowWhy.Spelled(TurnReach.Steered) });
            return session.SteeredAsync(words, CancellationToken.None, "conversation", handBack: back => Turns.HandBack(back with { Said = true }));
        }

        /// <summary>
        /// A word handed to the running turn, answered: said where the agent refused it, where it never reached the agent, or
        /// where the person's stop was asked while it was on its way and the agent answered it cancelled, since it may have
        /// read it and its answer was not kept (STEER1 §3).
        /// </summary>
        private async Task AnsweredAsync(ChatMessage words, Task<string> answered)
        {
            // The words' id beside each line, so the page settles any it still shows waiting (MSG1f).
            SessionEvent Said(Noted why) => new()
            {
                Kind = SessionEventKind.Note, Text = why.Note, Parts = why.Parts.Count > 0 ? why.Parts : null, Words = [words.Id!],
            };

            SessionEvent? said = null;
            try
            {
                var reason = await answered.ConfigureAwait(false);
                bool stoppedUnder;
                lock (_gate) stoppedUnder = _stoppedUnder.Contains(words.Id!);
                // With its code, which the page words in the reader's language (MSG1c3).
                said = ChatTurns.Lost(words.Id!, stoppedUnder, reason);
            }
            catch (AcpRefusal refused)
            {
                // A word the agent would not take costs that word, in its own words, never the conversation.
                said = Said(WordsNotes.Refused(refused.Words, "conversation"));
            }
            catch (Exception error) when (error is DriverException or IOException or ObjectDisposedException or InvalidOperationException)
            {
                said = Said(WordsNotes.NeverReached(error.Message));
            }
            finally
            {
                lock (_gate)
                {
                    _onTheirWay.Remove(words.Id!);
                    _stoppedUnder.Remove(words.Id!);
                }
            }

            if (said is null) return;
            _line(said.Text!);
            _record(said);
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

                    // The files said with the words, a link each after their blocks (MSG1d3).
                    await session.PromptAsync(goOn.Blocks, CancellationToken.None, () =>
                    {
                        sent();
                        _wentOn?.Invoke();
                    }, goOn.Files).ConfigureAwait(false);
                    return;
                }

                foreach (var recorded in Recorded(message)) _record(recorded);
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

                var notTaken = DoorNotes.TurnNotTaken(error.Message);
                _line($"— {notTaken.Note}");
                _record(new SessionEvent { Kind = SessionEventKind.Note, Text = notTaken.Note, Parts = notTaken.Parts });

                // A refusal the door carried, read for an account's limit (TOOL4d) before the turn's end is recorded, so
                // the refused turn is counted after the turns that ended. Never the transcript's words (D125 §1.4). Its English
                // beside the line's parts (CONVNOTE1b), as a refused sign-in's is, so the page words it in either language.
                if (error is DriverException && _limited?.Invoke(error.Message) is { } cooling)
                {
                    _line($"— {cooling.Note}");
                    _record(new SessionEvent { Kind = SessionEventKind.Note, Text = cooling.Note, Parts = cooling.Parts });
                }

                // And for the account's sign-in (SIGNIN1b), so the next start walks past an account its agent refused here.
                ReadSignIn(error);

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
                        // the id its record gave it, then all of them as one message, joined by a blank line, each with where
                        // its files are kept (MSG1d3).
                        foreach (var opening in goOn.Opening()) record(opening);
                        line = adapter.FrameMessage(goOn.Prompt);
                    }
                    else
                    {
                        foreach (var recorded in Recorded(message)) record(recorded);
                        // Each attached file named by its path, which the agent reads with its own tool
                        // (CONV4c, measured); the record keeps the person's words, not these lines.
                        line = adapter.FrameMessage(message.Prompt + ChatFiles.PathLines(message.Files));
                    }

                    if (!processes.Send(sessionId, line))
                    {
                        // Nothing is listening: the process is going, and its watch concludes the record.
                        var gone = WordsNotes.NotSent();
                        record(new SessionEvent { Kind = SessionEventKind.Note, Text = gone.Note, Parts = gone.Parts });
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
        string sessionId, string state, Noted note, Func<string, string, Task>? onEnded)
    {
        try
        {
            await _service.AdvanceAsync(sessionId, state, note).ConfigureAwait(false);
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
