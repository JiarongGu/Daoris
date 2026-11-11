using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.History;
using Daoris.Contracts.Quests;
using Daoris.Contracts.Sessions;
using Daoris.Contracts.Sync;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The driver's half of the conversation with the service — a client through the same doors as every
/// other component. Deliberately NOT a reference to the service's assemblies: the doors are
/// the contract, declared in Daoris.Contracts, and a driver that could reach the store directly would
/// drift from every client that cannot.
/// </summary>
/// <remarks>
/// It wires, sends a declared route and tells its watchers; the HTTP is behind <see cref="IServiceDoors"/>, whose real one,
/// <see cref="ServiceHttp"/>, the constructors that take a URL compose. Every module names this client; none names the HTTP. Each family of
/// doors is a class of its own over the client, grouped as the service's route modules are and reached through a property
/// (<see cref="Registry"/>, <see cref="SessionList"/>, <see cref="Words"/>, <see cref="Moves"/>, <see cref="Keeps"/>, <see cref="Store"/>,
/// <see cref="Standing"/>, <see cref="Openings"/>, <see cref="History"/>, <see cref="Confirmations"/>, and the owners' <c>Opinions</c>,
/// <c>Helpers</c> and <c>Partners</c>); the methods below are the one-line doors callers name. A class reaches the HTTP,
/// the JSON reading and the watchers through the internal surface here.
/// </remarks>
public sealed class ServiceClient : IDisposable, IQuestClosePort, IClosureBindPort, IRequestPort
{
    private readonly IServiceDoors _doors;
    private readonly string _base;

    /// <summary>A client over the doors it is handed: the HTTP's, or a test's.</summary>
    public ServiceClient(IServiceDoors doors)
    {
        _doors = doors;
        _base = doors.BaseUrl;
        Confirmations = new ConfirmationDoors(this);
        Opens = new SessionOpenDoors(this);
        Registry = new RegistryDoors(this);
        SessionList = new SessionListDoors(this);
        Words = new SessionWordDoors(this);
        Moves = new SessionMoveDoors(this);
        Keeps = new SessionKeepDoors(this);
        Store = new SessionStoreDoors(this);
        Standing = new QuestStandingDoors(this);
        Openings = new SessionOpenings(this);
        History = new HistoryDoors(this);
    }

    /// <summary>A client over HTTP to the host at <paramref name="baseUrl"/>, with its key; a test may hand its own HTTP client.</summary>
    public ServiceClient(string baseUrl, string? key, HttpClient? http = null, string? personKey = null)
        : this(new ServiceHttp(baseUrl, key, http, personKey))
    {
    }

    /// <summary>The local host's confirmation doors, read for the windows, over this client.</summary>
    public ConfirmationDoors Confirmations { get; }

    public SessionOpenDoors Opens { get; }

    public RegistryDoors Registry { get; }

    public SessionListDoors SessionList { get; }

    public SessionWordDoors Words { get; }

    public SessionMoveDoors Moves { get; }

    public SessionKeepDoors Keeps { get; }

    public SessionStoreDoors Store { get; }

    public QuestStandingDoors Standing { get; }

    public SessionOpenings Openings { get; }

    public HistoryDoors History { get; }

    /// <summary>A door class's request whose every answer comes back, a refusal's included (<see cref="IServiceDoors.SendAsync"/>).</summary>
    internal async Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
    {
        var answer = await _doors.SendAsync(verb, path, json, ct).ConfigureAwait(false);
        Opens.Observe(answer);
        return answer;
    }

    /// <summary>A route the contracts declare, sent as its record and read as its answer; a refusal comes back as one.</summary>
    public async Task<Sent<TAnswer>> SendAsync<TAnswer>(IRoute<TAnswer> request, CancellationToken ct = default) where TAnswer : class
    {
        var (verb, path, json) = RouteRequest.Of(request);
        return Sent<TAnswer>.Read($"{_base}{path}", await SendAsync(verb, path, json, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// A declared route sent as its record, its answer read by its fields as a JSON root, a refusal's included: for the doors of an owner that
    /// reads its answers so (<c>Opinions</c>, <c>Helpers</c> and <c>Partners</c>).
    /// </summary>
    internal async Task<(bool Ok, int Status, string Payload, JsonElement? Root)> SendJsonAsync<TAnswer>(
        IRoute<TAnswer> route, CancellationToken ct) where TAnswer : class
    {
        var (verb, path, json) = RouteRequest.Of(route);
        var answer = await SendAsync(verb, path, json, ct).ConfigureAwait(false);
        JsonElement? root = null;
        try
        {
            using var document = JsonDocument.Parse(answer.Payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON: a host older than the door answers a bare 404. Each caller says so its way.
        }

        return (answer.Ok, answer.Status, answer.Payload, root);
    }

    /// <summary>A record a door class opened, told to <see cref="Opened"/>'s watchers as this client's own doors tell them.</summary>
    internal void RecordOpened(SessionOpened opened) => Raise(Opened, opened);

    /// <summary>A record a door class moved, told to <see cref="Moved"/>'s watchers.</summary>
    internal void RecordMoved(SessionMoved moved) => Raise(Moved, moved);

    /// <summary>A record whose provenance a door class kept, told to <see cref="ActorKept"/>'s watchers.</summary>
    internal void RecordActorKept(string id) => Raise(ActorKept, id);

    public const string UrlVariable = "DAORIS_SERVICE_URL";
    public const string KeyVariable = "DAORIS_SERVICE_KEY";

    /// <summary>
    /// A session record this client opened, raised once the ledger says yes: every session on this machine is opened through one of
    /// these doors, so a watcher here sees what the person runs without a second door onto the ledger. A refusal raises nothing.
    /// </summary>
    public event Action<SessionOpened>? Opened;

    /// <summary>A session record this client moved, raised once the ledger says yes.</summary>
    public event Action<SessionMoved>? Moved;

    /// <summary>Local provenance changed without a state move or sync revision.</summary>
    public event Action<string>? ActorKept;

    /// <summary>
    /// A repository's registration followed from its line, whatever it came to: every follow on this machine goes
    /// through this client's registry door, so a watcher here logs each outcome (<c>registry.followed</c>) without the follower knowing the log.
    /// </summary>
    public event Action<RegistrationFollowed>? RegistryFollowed;

    /// <summary>Tell the watchers what following one repository came to (<see cref="RegistryFollowed"/>).</summary>
    public void Followed(RegistrationFollowed what) => Raise(RegistryFollowed, what);

    /// <summary>A workspace plan's line: a plan written, a set-up published or skipped, a pause, a resume, a stop.</summary>
    public event Action<SetupLine>? SetupLined;

    /// <summary>Tell the watchers a plan's line (<see cref="SetupLined"/>).</summary>
    public void SetupSaid(SetupLine line) => Raise(SetupLined, line);

    /// <summary>An account's line: a limit met, or starts waiting on a cooling account.</summary>
    public event Action<AccountLine>? AccountLined;

    /// <summary>Tell the watchers an account's line (<see cref="AccountLined"/>).</summary>
    public void AccountSaid(AccountLine line) => Raise(AccountLined, line);

    /// <summary>A landing at done's line: <c>landing.auto</c>, one per try.</summary>
    public event Action<LandingLine>? LandingLined;

    /// <summary>Tell the watchers a landing's line (<see cref="LandingLined"/>).</summary>
    public void LandingSaid(LandingLine line) => Raise(LandingLined, line);

    /// <summary>A done's evidence read: <c>evidence.checked</c>, one per read, counts and codes.</summary>
    public event Action<EvidenceLine>? EvidenceLined;

    /// <summary>Tell the watchers an evidence read's line (<see cref="EvidenceLined"/>).</summary>
    public void EvidenceSaid(EvidenceLine line) => Raise(EvidenceLined, line);

    /// <summary>Where the service is — handed to sessions so they can claim their own quests there.</summary>
    public string BaseUrl => _base;

    /// <summary>URL and key from the environment — the same two names `connect` reads.</summary>
    public static ServiceClient FromEnvironment() => new(ServiceHttp.FromEnvironment());

    public void Dispose() => _doors.Dispose();

    /// <summary>
    /// Tell the watchers. A watcher that fails costs its own view, never the call: the ledger has already moved, and the caller is owed its
    /// answer.
    /// </summary>
    private static void Raise<T>(Action<T>? watchers, T what)
    {
        try
        {
            watchers?.Invoke(what);
        }
        catch (Exception)
        {
            // The watcher's failure is its own; the record already says what happened.
        }
    }

    // The ports' halves the quest doors answer, sent through QuestViews.
    Task<RequestPublished> IRequestPort.PublishAsync(RequestAsk ask, CancellationToken ct) => this.PublishAsync(ask, ct);

    Task<QuestCloseAnswer> IQuestClosePort.CloseQuestsAsync(
        string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct) =>
        QuestPersonViews.CloseQuestsAsync(this, quest, key, token, quests, ct);

    Task<QuestCloseAnswer> IQuestClosePort.CloseOutcomeAsync(
        string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct) =>
        QuestPersonViews.CloseOutcomeAsync(this, quest, key, token, quests, ct);

    // ---- The one-line doors, each over the class that owns it. ----

    public Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct = default) => Registry.ListAsync(ct);

    public Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct = default) => Registry.RegistrationsAsync(ct);

    public Task<IReadOnlyList<DeclarationView>> DeclarationsAsync(string workspace, CancellationToken ct = default) => Registry.DeclarationsAsync(workspace, ct);

    public Task<(bool Ok, string Message)> RegisterAsync(System.Text.Json.Nodes.JsonObject body, CancellationToken ct = default) => Registry.RegisterAsync(body, ct);

    public Task<(bool Ok, string Message)> WireRepositoryAsync(string repository, string workspace, CancellationToken ct = default) =>
        Registry.WireRepositoryAsync(repository, workspace, ct);

    public Task<(bool Ok, string Message)> SetFolderExcludesAsync(string repository, IReadOnlyList<string> excludes, CancellationToken ct = default) =>
        Registry.SetFolderExcludesAsync(repository, excludes, ct);

    public Task<ConnectDone> ConnectAsync(string root, string? workspace, CancellationToken ct = default) => Registry.ConnectAsync(root, workspace, ct);

    public Task<ConnectDone> AddFolderAsync(string root, string? workspace, IReadOnlyList<string> excludes, CancellationToken ct = default) =>
        Registry.AddFolderAsync(root, workspace, excludes, ct);

    public Task<ConnectDone> AddSystemAsync(ConnectPlan plan, CancellationToken ct = default) =>
        Registry.AddSystemAsync(plan, ct);

    public Task<string> RetireAsync(string repository, CancellationToken ct = default) => Registry.RetireAsync(repository, ct);

    public Task<string> ImportAsync(string? folder, string? workspace, CancellationToken ct = default) => Registry.ImportAsync(folder, workspace, ct);

    public Task<string?> RefreshAsync(CancellationToken ct = default) => Registry.RefreshAsync(ct);

    public Task<SyncStanding> SyncStandingAsync(string workspace, CancellationToken ct = default) => Standing.SyncAsync(workspace, ct);

    public Task<IReadOnlyDictionary<string, int>> StrikesAsync(CancellationToken ct = default) => SessionList.StrikesAsync(ct);

    public Task<IReadOnlyList<SessionRun>> SessionRunsAsync(CancellationToken ct = default) => SessionList.RunsAsync(ct);

    public Task<SessionView?> FindSessionAsync(string id, CancellationToken ct = default) => SessionList.FindAsync(id, ct);

    public Task<(RelatedCandidate Mine, IReadOnlyList<RelatedCandidate> Others)?> RelatedCandidatesAsync(string id, CancellationToken ct = default) =>
        SessionList.RelatedCandidatesAsync(id, ct);

    public Task<IReadOnlyList<SessionView>> ActiveSessionsAsync(CancellationToken ct = default) => SessionList.ActiveAsync(ct);

    public Task<bool> TookAsync(string id, CancellationToken ct = default) => SessionList.TookAsync(id, ct);

    public Task<string?> SessionQuestAsync(string id, CancellationToken ct = default) => SessionList.QuestAsync(id, ct);

    public Task<(string? Tree, string? BaseCommit)> SessionGroundAsync(string id, CancellationToken ct = default) => SessionList.GroundAsync(id, ct);

    public Task<string?> SessionTreeAsync(string session, CancellationToken ct = default) => SessionList.TreeAsync(session, ct);

    public Task<IReadOnlyList<LiveSession>> LiveSessionsAsync(CancellationToken ct = default) => SessionList.LiveAsync(ct);

    public Task<PriorSession?> RecordAsync(string id, CancellationToken ct = default) => SessionList.RecordAsync(id, ct);

    public Task<IReadOnlyList<string>> WaitingTreesAsync(CancellationToken ct = default) => SessionList.WaitingTreesAsync(ct);

    public Task<IReadOnlyCollection<PriorSession>> RecordsAsync(CancellationToken ct = default) => SessionList.RecordsAsync(ct);

    public Task<IReadOnlyList<SessionRecord>> SessionRecordsAsync(CancellationToken ct = default) => SessionList.AllAsync(ct);

    /// <summary>Every record as the kept list's text, for a reader that needs the records beside the one it names (its line, its quest's earlier parts).</summary>
    public Task<string> SessionRecordsJsonAsync(CancellationToken ct = default) => SessionList.JsonAsync(true, ct);

    public Task<(bool Kept, string Message)> AddedToSessionAsync(string id, string text, CancellationToken ct = default) => Words.AddedAsync(id, text, ct);

    public Task<SayAnswer> SayAsync(
        string id, string text, IReadOnlyList<string>? files = null, CancellationToken ct = default, string? channel = null,
        string? from = null, string? key = null, string? level = null, bool running = false,
        (string Decision, string Question, string Choice)? supervised = null, bool ending = false, string? question = null) =>
        Words.SayAsync(id, text, files, ct, channel, from, key, level, running, supervised, ending, question);

    public Task<TakeBackAnswer> TakeBackAsync(string id, string word, string? text = null, CancellationToken ct = default) => Words.TakeBackAsync(id, word, text, ct);

    public Task<MoveAnswer> MoveSaidAsync(string id, string word, string? to = null, CancellationToken ct = default) => Words.MoveSaidAsync(id, word, to, ct);

    public Task<AttemptAnswer> AttemptAsync(string id, IReadOnlyList<string> said, CancellationToken ct = default) => Words.AttemptAsync(id, said, ct);

    public Task<(bool Ok, string Message)> TakenAsync(string id, IReadOnlyList<string> said, string? by = null, CancellationToken ct = default) =>
        Words.TakenAsync(id, said, by, ct);

    public Task<string> ClaimAsync(string id, CancellationToken ct = default) => Standing.ClaimAsync(id, ct);

    public Task<(string Claim, string? Why)> ClaimStandingAsync(string id, CancellationToken ct = default) => Standing.ClaimStandingAsync(id, ct);

    public Task<(string Claim, string? Why, ForwardTake? Forward, DateTimeOffset? Taken)> ClaimTakeAsync(string id, CancellationToken ct = default) =>
        Standing.ClaimTakeAsync(id, ct);

    public Task<HandbackResponse?> HandbackAsync(string id, CancellationToken ct = default) => Standing.HandbackAsync(id, ct);

    public Task<bool> KnowsClosureAsync(CancellationToken ct = default) => Standing.KnowsClosureAsync(ct);

    public Task<bool> KnowsRemoteClosureAsync(CancellationToken ct = default) => Standing.KnowsRemoteClosureAsync(ct);

    public Task<(string? SessionId, string Message)> OpenSessionAsync(
        string questId, string adapter, string? doorVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default, bool setup = false,
        ContinuedFrom? from = null, IReadOnlyDictionary<string, string>? access = null, IStartHold? starting = null)
    {
        Governed(tree, starting);
        return Openings.SessionAsync(questId, adapter, doorVersion, profile, tree, baseCommit, ct, setup, from, access);
    }

    public Task<DestinationOpen> OpenSessionMovingAsync(
        string questId, string adapter, string? doorVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from,
        MoveWords? moves, CancellationToken ct = default, bool setup = false, IReadOnlyDictionary<string, string>? access = null,
        string? opens = null, IStartHold? starting = null)
    {
        Governed(tree, starting);
        return Openings.SessionMovingAsync(questId, adapter, doorVersion, profile, tree, baseCommit, from, moves, ct, setup, access, opens);
    }

    /// <summary>An open in a tree is a start's, asked inside its hold; one with no tree starts in nothing a hold covers.</summary>
    private static void Governed(string? tree, IStartHold? starting)
    {
        if (tree is null) return;
        (starting ?? throw new InvalidOperationException($"a session's open in `{tree}` was asked with no start's hold.")).Assert("a session's open", tree);
    }

    public Task<(string? SessionId, string Message)> OpenChatAsync(
        string repository, string adapter, string? doorVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default, ContinuedFrom? from = null) =>
        Openings.ChatAsync(repository, adapter, doorVersion, profile, tree, baseCommit, ct, from);

    public Task<DestinationOpen> OpenChatMovingAsync(
        string repository, string adapter, string? doorVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from,
        MoveWords moves, string? workspace, CancellationToken ct = default) =>
        Openings.ChatMovingAsync(repository, adapter, doorVersion, profile, tree, baseCommit, from, moves, workspace, ct);

    public Task<(string? SessionId, string Message)> OpenIntakeAsync(
        string ask, string adapter, string room, string? doorVersion = null, string? profile = null, CancellationToken ct = default) =>
        Openings.IntakeAsync(ask, adapter, room, doorVersion, profile, ct);

    public Task<(string? SessionId, string Message)> OpenHelpAsync(
        string adapter, string room, string? doorVersion = null, string? profile = null, string? workspace = null,
        string? mode = null, CancellationToken ct = default) =>
        Openings.HelpAsync(adapter, room, doorVersion, profile, workspace, mode, ct);

    public Task KeepHelpModeAsync(string sessionId, string mode, CancellationToken ct = default) => Keeps.HelpModeAsync(sessionId, mode, ct);

    public Task KeepAccessAsync(string sessionId, IReadOnlyDictionary<string, string> access, CancellationToken ct = default) => Keeps.AccessAsync(sessionId, access, ct);

    public Task KeepResolvedAsync(string sessionId, IReadOnlyList<string> versions, CancellationToken ct = default) => Keeps.ResolvedAsync(sessionId, versions, ct);

    public Task KeepActorAsync(string id, string? choice = null, ActorAsked? asked = null, ActorRan? ran = null,
        object? changed = null, CancellationToken ct = default, ActorChosen? chosen = null) =>
        Keeps.ActorAsync(id, choice, asked, ran, changed, ct, chosen);

    public Task<bool> TryKeepActorAsync(string id, MachineLog? log, string? choice = null, ActorAsked? asked = null,
        object? changed = null, CancellationToken ct = default, ActorChosen? chosen = null) =>
        Keeps.TryActorAsync(id, log, choice, asked, changed, ct, chosen);

    internal Task<int> AgeWorkRecordsAsync(DateTimeOffset before, CancellationToken ct = default) => Store.AgeWorkRecordsAsync(before, ct);

    public Task<string?> SessionRecordAsync(string session, CancellationToken ct = default) => Store.RecordAsync(session, ct);

    public Task<IReadOnlyList<string>> LineMemoryAsync(string session, CancellationToken ct = default) => Store.LineMemoryAsync(session, ct);

    public Task<string?> SessionMemoryAsync(string session, CancellationToken ct = default) => Store.MemoryAsync(session, ct);

    public Task<IReadOnlyList<JsonElement>> MemoryWaitsAsync(CancellationToken ct = default) => Store.MemoryWaitsAsync(ct);

    public Task<(bool Ok, string Message)> DecideMemoryAsync(string session, string? op, bool yes, CancellationToken ct = default) =>
        Store.DecideMemoryAsync(session, op, yes, ct);

    public Task<SessionDeleteAnswer> JudgeSessionDeleteAsync(string id, CancellationToken ct = default) => Store.JudgeDeleteAsync(id, ct);

    public Task<SessionDeleteAnswer> DeleteSessionAsync(string id, CancellationToken ct = default) => Store.DeleteAsync(id, ct);

    public Task<string> AdvanceAsync(
        string id, string state, string? note = null, string? evidence = null, string? transcript = null,
        CancellationToken ct = default, bool interrupted = false, bool limit = false, IReadOnlyList<NotePart>? parts = null,
        string? expected = null, bool unbindAsk = false, IReadOnlyList<string>? expect = null) =>
        Moves.AdvanceAsync(id, state, note, evidence, transcript, ct, interrupted, limit, parts, expected, unbindAsk, expect);

    public Task<string> AdvanceAsync(
        string id, string state, Noted noted, string? evidence = null, string? transcript = null,
        CancellationToken ct = default, bool interrupted = false, bool limit = false, string? expected = null,
        IReadOnlyList<string>? expect = null) =>
        Moves.AdvanceAsync(id, state, noted, evidence, transcript, ct, interrupted, limit, expected, expect);

    public Task RestoreParkAsync(string id, IReadOnlyList<string> takeBack, Noted? noted, CancellationToken ct = default) => Moves.RestoreParkAsync(id, takeBack, noted, ct);

    public Task EndRefusedIntakeAsync(string session, SessionConclusion conclusion, CancellationToken ct = default) => Moves.EndRefusedIntakeAsync(session, conclusion, ct);

    public Task<IReadOnlyList<HistoryUnitView>> HistoryAsync(PlanHistory plan, CancellationToken ct = default) => History.PlanAsync(plan, ct);

    public Task<IReadOnlyList<HistoryClearedView>> ClearHistoryAsync(IReadOnlyList<HistoryUnitName> units, CancellationToken ct = default) => History.ClearAsync(units, ct);

    // ---- The turn lines a session's end hands the service. ----

    /// <summary>
    /// The usage history's turn lines, by session, for the sessions named: set where this client's home is known, so a session's
    /// end hands the service the lines its memory rows' models are read from. Null hands none, and the rows name <c>unknown</c>.
    /// </summary>
    public Func<IReadOnlyCollection<string>, IReadOnlyDictionary<string, IReadOnlyList<TurnLine>>>? TurnLines { get; init; }

    public Func<string, CancellationToken, Task<IReadOnlyList<string>?>>? PlaceEnds { get; init; }

    /// <summary>One turn line as the service is handed it: the fields its memory rows' models are read from.</summary>
    public sealed record TurnLine(long Seq, DateTimeOffset? Started, DateTimeOffset Ended, string Model);

    /// <summary>Maps the usage history's kept turns onto the lines this client hands the service.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<TurnLine>> LinesOf<T>(
        IReadOnlyDictionary<string, IReadOnlyList<T>> kept, Func<T, TurnLine> line) =>
        kept.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<TurnLine>)[.. pair.Value.Select(line)], StringComparer.Ordinal);

    // ---- The readers of the session list's text, over SessionReads. ----

    internal static IReadOnlyList<SessionView> ReadSessions(string json) => SessionReads.Views(SessionReads.Parse(json));

    internal static IReadOnlyList<LiveSession> ReadRunning(string json) => SessionReads.Running(SessionReads.Parse(json));

    internal static IReadOnlyDictionary<string, int> ReadStrikes(string json) => SessionReads.Strikes(SessionReads.Parse(json));

    internal static IReadOnlyDictionary<string, int> ReadFailedRuns(string json) => SessionReads.FailedRuns(SessionReads.Parse(json));

    internal static IReadOnlyDictionary<string, PriorSession> ReadLastRun(string json) => SessionReads.LastRun(SessionReads.Parse(json));

    internal static IReadOnlyDictionary<string, PriorSession> ReadStoodDown(string json) => SessionReads.StoodDown(SessionReads.Parse(json));

    internal static IReadOnlyDictionary<string, PriorSession> ReadNamedByReopen(
        string json, IReadOnlyList<QuestView> quests, IReadOnlyDictionary<string, PriorSession> lastRun) =>
        SessionReads.NamedByReopen(SessionReads.Listed(json), quests, lastRun);

    internal static IReadOnlyDictionary<string, QuestTake> ReadTakes(
        string json, IReadOnlyDictionary<string, string> claims, IReadOnlyDictionary<string, string?>? whys = null,
        IReadOnlyDictionary<string, ForwardTake?>? forwards = null, IReadOnlyDictionary<string, DateTimeOffset?>? taken = null) =>
        SessionReads.Takes(SessionReads.Parse(json), claims, whys, forwards, taken);

    internal static PriorSession? ReadRecord(string json, string id) => ListedPriors.Of(json).GetValueOrDefault(id);

    internal static IReadOnlyDictionary<string, PriorSession> ReadRecords(string json) => ListedPriors.Of(json);

    internal static IReadOnlyDictionary<string, string> ReadEntries(string json) => SessionReads.Entries(SessionReads.Listed(json));

    internal static IReadOnlyList<SessionStarted> ReadStarted(string json, IReadOnlyList<SessionView> active) => SessionReads.Started(SessionReads.Parse(json), active);

    internal static IReadOnlyList<QuestView> ReadClosed(string json, IReadOnlyDictionary<string, PriorSession> lastRun, IReadOnlyCollection<string> open) =>
        SessionReads.Closed(QuestViews.Read(json), lastRun, open);
}

/// <summary>A session record the ledger opened for this client.</summary>
/// <param name="Kind">
/// Which door opened it — <see cref="Driven"/>, <see cref="Chat"/>, <see cref="Intake"/> or
/// <see cref="Help"/>. The door's, not the record's: the record calls an intake and Ask Daoris chats.
/// </param>
/// <param name="Repository">The repository the record runs in, as the ledger recorded it; a name, never a path.</param>
public sealed record SessionOpened(string Session, string Kind, string Adapter, string? Repository)
{
    /// <summary>The workspace the record runs in, as the ledger recorded it; null where it says none.</summary>
    public string? Workspace { get; init; }

    /// <summary>That a driven session serves a set-up quest (<see cref="SetupQuests"/>).</summary>
    public bool Setup { get; init; }

    public const string Driven = "driven";
    public const string Chat = "chat";
    public const string Intake = "intake";
    public const string Help = "help";
}

/// <summary>A session record the ledger moved for this client, in the state's public spelling.</summary>
public sealed record SessionMoved(string Session, string State);
