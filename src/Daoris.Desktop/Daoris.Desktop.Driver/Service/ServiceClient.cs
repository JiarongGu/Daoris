using System.Globalization;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The driver's half of the conversation with the service — a client through the same doors as every
/// other component (D46 §7). Deliberately NOT a reference to the service's assemblies: the doors are
/// the contract, and a driver that could reach the store directly would drift from every client that
/// cannot.
/// </summary>
/// <remarks>
/// It writes each request and reads each answer, and the HTTP is behind
/// <see cref="IServiceDoors"/>, whose real one, <see cref="ServiceHttp"/>, the constructors that take a URL compose. Every
/// module names this client; none names the HTTP. A family of doors on a resource of its own that another module asks is a
/// class over the client, reached through it (<c>Opinions</c>, <c>Helpers</c> and <c>Partners</c>, each its owner's member of the client; PART8): it reaches the HTTP, the
/// JSON reading and <see cref="Opened"/> through the internal surface below (D166's PART8 note).
/// </remarks>
public sealed class ServiceClient : IDisposable, IQuestClosePort, IClosureBindPort
{
    private readonly IServiceDoors _doors;
    private readonly string _base;

    /// <summary>A client over the doors it is handed: the HTTP's, or a test's.</summary>
    public ServiceClient(IServiceDoors doors)
    {
        _doors = doors;
        _base = doors.BaseUrl;
        Confirmations = new ConfirmationDoors(this);
    }

    /// <summary>A client over HTTP to the host at <paramref name="baseUrl"/>, with its key; a test may hand its own HTTP client.</summary>
    public ServiceClient(string baseUrl, string? key, HttpClient? http = null)
        : this(new ServiceHttp(baseUrl, key, http))
    {
    }

    /// <summary>The local host's confirmation doors, read for the windows (NOTIFY2j), over this client.</summary>
    public ConfirmationDoors Confirmations { get; }

    /// <summary>A door class's request whose every answer comes back, a refusal's included (<see cref="IServiceDoors.SendAsync"/>).</summary>
    internal Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct) =>
        _doors.SendAsync(verb, path, json, ct);

    /// <summary>A record a door class opened, told to <see cref="Opened"/>'s watchers as this client's own doors tell them (LOG1b).</summary>
    internal void RecordOpened(SessionOpened opened) => Raise(Opened, opened);

    public const string UrlVariable = "DAORIS_SERVICE_URL";
    public const string KeyVariable = "DAORIS_SERVICE_KEY";

    /// <summary>
    /// A session record this client opened, raised once the ledger said yes (LOG1b): every session on
    /// this machine is opened through one of these doors, so a watcher here sees what the person runs
    /// without a second door onto the ledger. A refusal raises nothing, because nothing opened.
    /// </summary>
    public event Action<SessionOpened>? Opened;

    /// <summary>A session record this client moved, raised once the ledger said yes (LOG1b).</summary>
    public event Action<SessionMoved>? Moved;
    /// <summary>D205: local provenance changed without a state move or sync revision.</summary>
    public event Action<string>? ActorKept;

    /// <summary>
    /// A repository's registration followed from its line (WSSETUP5, D124 §3.4), whatever it came to: every follow on this
    /// machine goes through this client's registry door, so a watcher here logs each outcome as it logs the opens and
    /// moves (<c>registry.followed</c>), without the follower knowing the log.
    /// </summary>
    public event Action<RegistrationFollowed>? RegistryFollowed;

    /// <summary>Tell the watchers what following one repository came to (<see cref="RegistryFollowed"/>).</summary>
    public void Followed(RegistrationFollowed what) => Raise(RegistryFollowed, what);

    /// <summary>
    /// A workspace plan's line (WSSETUP6, D124 §4.1): a plan written, a set-up published or skipped, a pause, a resume, a
    /// stop. Every plan on this machine reads its facts through this client, so a watcher here logs each line as it logs the
    /// opens and moves, without the plan knowing the log.
    /// </summary>
    public event Action<SetupLine>? SetupLined;

    /// <summary>Tell the watchers a plan's line (<see cref="SetupLined"/>).</summary>
    public void SetupSaid(SetupLine line) => Raise(SetupLined, line);

    /// <summary>
    /// An account's line (TOOL4d, D125 §5.4): a limit met, or starts waiting on a cooling account. Every driven session,
    /// intake and conversation on this machine is concluded through this client, so a watcher here logs each line as it
    /// logs the opens and moves, without the driver knowing the log.
    /// </summary>
    public event Action<AccountLine>? AccountLined;

    /// <summary>Tell the watchers an account's line (<see cref="AccountLined"/>).</summary>
    public void AccountSaid(AccountLine line) => Raise(AccountLined, line);

    /// <summary>
    /// A landing at done's line (LAND2b, design §8): <c>landing.auto</c>, one per try. The look lands through this client's
    /// reads, so a watcher here logs each try as it logs the opens and moves, without the lander knowing the log.
    /// </summary>
    public event Action<LandingLine>? LandingLined;

    /// <summary>Tell the watchers a landing's line (<see cref="LandingLined"/>).</summary>
    public void LandingSaid(LandingLine line) => Raise(LandingLined, line);

    /// <summary>
    /// A done's evidence read (EVID1b, D144 §5): <c>evidence.checked</c>, one per read, counts and codes. A session's end, the
    /// sweep and the terminal's check each post their verdict through this client, so a watcher here logs each read as it logs
    /// the opens and moves, without the reader knowing the log.
    /// </summary>
    public event Action<EvidenceLine>? EvidenceLined;

    /// <summary>Tell the watchers an evidence read's line (<see cref="EvidenceLined"/>).</summary>
    public void EvidenceSaid(EvidenceLine line) => Raise(EvidenceLined, line);

    /// <summary>Where the service is — handed to sessions so they can claim their own quests there.</summary>
    public string BaseUrl => _base;

    /// <summary>URL and key from the environment — the same two names `connect` reads (design §5b).</summary>
    public static ServiceClient FromEnvironment() => new(ServiceHttp.FromEnvironment());

    public void Dispose() => _doors.Dispose();

    /// <summary>Everything one tick decides from, fetched together so the plan is coherent.</summary>
    public async Task<Snapshot> SnapshotAsync(CancellationToken ct = default)
    {
        // PARALLEL1b (D181 point 3): the four reads asked at once, so one door slow to answer holds its own read, not the reads after it.
        // Each is still the service's answer as it stood when asked; the plan was never one transaction across them.
        var questsRead = GetAsync("/api/quests", ct);
        var registryRead = GetAsync("/api/registry", ct);
        var activeRead = GetAsync("/api/sessions", ct);
        // A second read rather than deriving both from one (DRV6): `/api/sessions` means ACTIVE, the
        // planner's "is this repository busy" rests on that, and re-deriving active-ness here would
        // put a second opinion about it on this side of the wire.
        var recordsRead = GetAsync("/api/sessions?includeClosed=true", ct);
        await Task.WhenAll(questsRead, registryRead, activeRead, recordsRead).ConfigureAwait(false);
        var quests = ReadQuests(await questsRead.ConfigureAwait(false));
        var repositories = ReadRegistry(await registryRead.ConfigureAwait(false));
        var active = ReadSessions(await activeRead.ConfigureAwait(false));
        var records = await recordsRead.ConfigureAwait(false);
        var lastRun = ReadLastRun(records);
        // The stand-downs a bring-forward would carry on (SYNCMERGE1b), which the last run never holds.
        var stoodDown = ReadStoodDown(records);
        // The closed quests a session written to served (MSG1b, D137 §2.2), read only where one waits: no open list holds them.
        var open = quests.Select(quest => quest.Id).ToList();
        var closed = lastRun.Any(run => run.Value.WordsWaiting && !open.Contains(run.Key, StringComparer.OrdinalIgnoreCase))
            ? ReadClosed(await GetAsync("/api/quests?includeClosed=true", ct).ConfigureAwait(false), lastRun, open)
            : [];
        var (claims, whys, forwards, taken) = await CarryOnClaimsAsync(quests, lastRun, stoodDown, ct).ConfigureAwait(false);
        return new Snapshot(quests, repositories, active, ReadStrikes(records))
        {
            LastRun = lastRun,
            StoodDown = stoodDown,
            // The session a taken quest's reopen names, where a later one is its last here (FABRIC-CLOSE1c9): its words go on in it.
            NamedByReopen = ReadNamedByReopen(records, quests, lastRun),
            Started = ReadStarted(records, active),
            Closed = closed,
            // Whose the take is on each taken quest a look would carry on (CARRY2b), the claim asked of the host for those alone.
            Takes = ReadTakes(records, claims, whys, forwards, taken),
            // Each record's entry, which a quest's publisher names its line by (SESSONE1d).
            Entries = ReadEntries(records),
        };
    }

    /// <summary>
    /// This machine's claim on each taken quest a look would carry on (CARRY2b): one that waits on nothing, whose last session
    /// here ended as a carry-on goes on from (<see cref="Planner.CarriesOn"/>), a stop the person still holds included, which
    /// the planner holds by the stop first. A claim the host does not answer is unread, and the carry-on is planned as before. A
    /// step the person's reopen holds is not asked (FABRIC-CLOSE1c2): nothing carries it on until their go-on, which the next look reads.
    /// And one whose newest record here stood down (<paramref name="stoodDown"/>, SYNCMERGE1b): its claim names the person's bring-forward,
    /// where one made the take, which carries that record on.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, string> Claims, IReadOnlyDictionary<string, string?> Whys,
        IReadOnlyDictionary<string, ForwardTake?> Forwards, IReadOnlyDictionary<string, DateTimeOffset?> Taken)> CarryOnClaimsAsync(
        IReadOnlyList<QuestView> quests, IReadOnlyDictionary<string, PriorSession> lastRun, IReadOnlyDictionary<string, PriorSession> stoodDown,
        CancellationToken ct)
    {
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var whys = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var forwards = new Dictionary<string, ForwardTake?>(StringComparer.OrdinalIgnoreCase);
        var taken = new Dictionary<string, DateTimeOffset?>(StringComparer.OrdinalIgnoreCase);
        var asked = quests
            .Where(quest => quest is { Status: "Taken", Awaits: null or "", HeldByReopen: false }
                            && ((lastRun.TryGetValue(quest.Id, out var last) && Planner.CarriesOn(last)) || (stoodDown.ContainsKey(quest.Id) && HeldHere(quest))))
            .ToArray();

        // PARALLEL1b (D181 point 3): asked at once, at most ClaimsAtOnce together, so a host slow to answer for one quest holds that
        // question alone, not the snapshot's every other.
        await Parallel.ForEachAsync(
            asked, new ParallelOptions { MaxDegreeOfParallelism = ClaimsAtOnce, CancellationToken = ct },
            async (quest, token) =>
            {
                try
                {
                    var (claim, why, forward, at) = await ClaimTakeAsync(quest.Id, token).ConfigureAwait(false);
                    lock (claims)
                    {
                        claims[quest.Id] = claim;
                        whys[quest.Id] = why;
                        forwards[quest.Id] = forward;
                        taken[quest.Id] = at;
                    }
                }
                catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
                                                  // The client's own timeout (DEV3a): a host that took the question and stalled.
                                                  || (error is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    // Unread: the ledger judges the carry-on, as it did before the planner asked. A stand-down unread carries nothing.
                }
            }).ConfigureAwait(false);

        return (claims, whys, forwards, taken);
    }

    /// <summary>
    /// Whether the quest list says this machine holds the quest (SYNCMERGE1b1): its holder is this machine's id. A stood-down quest held
    /// elsewhere, or by no press, has no bring-forward of this machine's to read, so its claim is not asked.
    /// </summary>
    private static bool HeldHere(QuestView quest) =>
        quest.Holder is { } holder && string.Equals(holder, quest.Here, StringComparison.Ordinal);

    /// <summary>How many claim questions a snapshot asks the host together (PARALLEL1b): local, and each one small.</summary>
    private const int ClaimsAtOnce = 4;

    /// <summary>Every repository this host holds, in every circle — what the page's scope is read from (FG4).</summary>
    public async Task<IReadOnlyList<RepoView>> RegistryAsync(CancellationToken ct = default) =>
        ReadRegistry(await GetAsync("/api/registry", ct).ConfigureAwait(false));

    /// <summary>
    /// Every repository this host holds, with the declaration each row holds (WSSETUP5): what following a line compares
    /// against, so a registration is sent only where the row holds something else.
    /// </summary>
    public async Task<IReadOnlyList<RegistrationRow>> RegistrationsAsync(CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/registry", ct).ConfigureAwait(false));
        return
        [
            .. document.RootElement.EnumerateArray().Select(repo => new RegistrationRow(
                Text(repo, "repository") ?? "",
                RemoteTarget.Workspace(Text(repo, "workspace")),
                Text(repo, "root"),
                Flag(repo, "adopted"),
                Text(repo, "summary"),
                Strings(repo, "owns"),
                Strings(repo, "accepts"),
                Strings(repo, "uses"),
                Strings(repo, "packs"),
                Flag(repo, "joined"),
                Flag(repo, "sharesKnowledge"),
                // Absent is none: a host from before lanes answers without them.
                repo.TryGetProperty("lanes", out var lanes) && lanes.ValueKind == JsonValueKind.Array
                    ? [.. lanes.EnumerateArray()
                        .Where(lane => lane.ValueKind == JsonValueKind.Object && Text(lane, "id") is { Length: > 0 })
                        .Select(lane => new LaneView(Text(lane, "id")!, Text(lane, "title") ?? "", Text(lane, "summary") ?? "", Flag(lane, "steward")))]
                    : [])
            {
                // The service's own word (WSSETUP6): a workspace plan's *set up*, never recomputed on this side.
                Registered = Flag(repo, "registered"),
            }),
        ];
    }

    /// <summary>
    /// Register a repository through the registry door, with the body <c>connect</c> would send (WSSETUP5, D124 §3.3): taken,
    /// or refused in the service's own words.
    /// </summary>
    public async Task<(bool Ok, string Message)> RegisterAsync(System.Text.Json.Nodes.JsonObject body, CancellationToken ct = default)
    {
        var (ok, status, payload, root) = await PostJsonAsync("/api/registry", body.ToJsonString(), ct).ConfigureAwait(false);
        if (ok) return (true, "");
        return (false, (root is { } refused ? Text(refused, "error") : null) ?? $"the registry door answered {status}: {payload}");
    }

    /// <summary>
    /// Move a registered repository to a workspace on this machine (ENTRY1d1, D48 §7): the local host's
    /// <c>POST /api/registry/{repository}/workspace</c>, the request the Manage drawer's *Move to workspace* sends, which edits
    /// one field of the row. The workspace the host took is said as the drawer says it; a refusal comes back in the service's
    /// words (a name it does not hold, a shared host's boundary), and a host older than the door is said to be.
    /// </summary>
    public async Task<(bool Ok, string Message)> WireRepositoryAsync(string repository, string workspace, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("workspace", workspace);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/registry/{Uri.EscapeDataString(repository)}/workspace", body, ct).ConfigureAwait(false);
        if (root is not { } answer)
        {
            return (false, $"the service at {_base} has no workspace door for a repository ({status}) — is it older than this driver?");
        }

        return ok
            ? (true, $"`{Text(answer, "repository") ?? repository}` is now in workspace `{Text(answer, "workspace") ?? workspace}`.")
            : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// Ask the host to read the index again (the refresh every door has): registering does not re-index (D124 §3.3). Null
    /// when it did; else why not, in the service's words.
    /// </summary>
    public async Task<string?> RefreshAsync(CancellationToken ct = default)
    {
        var (ok, status, payload, root) = await PostJsonAsync("/api/refresh", "{}", ct).ConfigureAwait(false);
        return ok ? null : (root is { } refused ? Text(refused, "error") : null) ?? $"the refresh door answered {status}: {payload}";
    }

    /// <summary>
    /// How many sessions failed on each quest (DRV6), as <see cref="SnapshotAsync"/> derives them: what a workspace plan reads a
    /// parked set-up by (WSSETUP6), without the rest of a snapshot.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> StrikesAsync(CancellationToken ct = default) =>
        ReadStrikes(await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));

    /// <summary>
    /// Every session record this machine keeps, closed ones included, with the repository it ran in: what a workspace plan
    /// counts the sessions run in each repository by, and whose conversation records it reads (WSSETUP6, D124 §4.2). A
    /// teammate's record says nothing of this machine's work, and is left out as the strikes leave it.
    /// </summary>
    public async Task<IReadOnlyList<SessionRun>> SessionRunsAsync(CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));
        return
        [
            .. document.RootElement.EnumerateArray()
                .Where(session => !IsTeams(session))
                .Select(session => new SessionRun(Text(session, "id") ?? "", Text(session, "repository") ?? ""))
                .Where(run => run.Session.Length > 0 && run.Repository.Length > 0),
        ];
    }

    /// <summary>This machine's active sessions — the one read an orphan sweep needs, without a whole snapshot.</summary>
    public async Task<IReadOnlyList<SessionView>> ActiveSessionsAsync(CancellationToken ct = default) =>
        ReadSessions(await GetAsync("/api/sessions", ct).ConfigureAwait(false));

    /// <summary>
    /// One quest as it stands, closed ones included — how a session's end is observed, and what a
    /// resumed session is told its question came back with (D79). Null when the service has none.
    /// </summary>
    public async Task<QuestView?> FindQuestAsync(string id, CancellationToken ct = default) =>
        ReadQuests(await GetAsync("/api/quests?includeClosed=true", ct).ConfigureAwait(false))
            .FirstOrDefault(quest => string.Equals(quest.Id, id.TrimStart('#'), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Dismiss every conflict a quest carries (SYNC6c) — the terminal's form of the drawer's button. The
    /// service's sentence comes back verbatim, including "nothing to dismiss".
    /// </summary>
    /// <exception cref="DriverException">The service refused, in its own words — no such quest.</exception>
    public async Task<string> DismissConflictsAsync(string quest, CancellationToken ct = default)
    {
        using var answer = JsonDocument.Parse(await _doors.PostAsync(
            $"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/conflicts/dismiss", "{}", ct)
            .ConfigureAwait(false));
        return Text(answer.RootElement, "message") ?? "";
    }

    /// <summary>Where a circle stands on this machine (SYNC6a) — read from its host, without reaching the remote.</summary>
    public async Task<SyncStanding> SyncStandingAsync(string workspace, CancellationToken ct = default) =>
        RemoteSyncPayloads.Standing(await GetAsync($"/api/sync?workspace={Uri.EscapeDataString(workspace)}", ct).ConfigureAwait(false));

    /// <summary>
    /// Whether this session took its own quest through its own connector (STANDDOWN2) — read at its end,
    /// so a session holding the quest it took is told from one that found it taken.
    /// </summary>
    public async Task<bool> TookAsync(string id, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));
        return document.RootElement.EnumerateArray().Any(session =>
            Text(session, "id") == id && session.TryGetProperty("took", out var took) && took.ValueKind == JsonValueKind.True);
    }

    /// <summary>
    /// Why the closure graph named for this session's take did not bind it (CLOSETAKE1, FABRIC-CLOSE1o-ii): <c>expired</c>,
    /// <c>replaced</c> or <c>remote</c>, read off its record at its end; null for a bound take, a take no graph was named for, and a
    /// host from before the field.
    /// </summary>
    public async Task<string?> TookBindAsync(string id, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (Text(session, "id") == id) return Text(session, "tookBind") is { Length: > 0 } why ? why : null;
        }

        return null;
    }

    /// <summary>
    /// Answer a driven session that parked to ask the person (STANDDOWN2) — the terminal's form of the
    /// page's box. The service's sentence comes back verbatim, and a refusal is an answer too.
    /// </summary>
    public async Task<(bool Ok, string Message)> AnswerSessionAsync(string id, string? answer, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            if (answer is not null) writer.WriteString("answer", answer);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(id)}/answer", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answered) return (false, $"the service at {_base} has no answer door ({status}) — is it older than this driver?");
        if (!ok) return (false, Text(answered, "error") ?? payload);

        // An answer ends the parked record `completed` (STANDDOWN2), which is a move like any other. Once it keeps the
        // record parked for the driver's next look (ANSWER1b, D131 §5) nothing moved, and a watcher is told nothing.
        var state = StateOf(answered) ?? "completed";
        if (!SessionStates.IsParked(state)) Raise(Moved, new SessionMoved(id, state));
        return (true, Text(answered, "message") ?? "");
    }

    /// <summary>
    /// Tell the service what the person added to a running session (DRIFT1a2, D133 §1), which keeps it on the ask that
    /// session's work is for: the words verbatim, by the session's own id. Whether it was kept is an answer, never an
    /// exception: <c>kept: false</c> is a session on no ask, with the service's sentence, and a refusal or a host without
    /// the door (one older than DRIFT1a, or a shared one) is false with a sentence saying which.
    /// </summary>
    public async Task<(bool Kept, string Message)> AddedToSessionAsync(string id, string text, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("text", text);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(id)}/added", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answered)
        {
            return (false, $"the service at {_base} has no door for what the person adds ({status}) — is it older than this driver?");
        }

        if (!ok) return (false, Text(answered, "error") ?? payload);
        return (Flag(answered, "kept"), Text(answered, "message") ?? "");
    }

    /// <summary>
    /// Keep the person's words on a parked or ended session of this machine's for it to go on with (MSG1a's say door, MSG1d,
    /// D137 §5.3): the words as said, and the names of what they gave with them, never where a file is kept. The kept word
    /// comes back with the id the record's events say again where the session took it. Whether they were kept is an answer,
    /// never an exception: a refusal carries the service's word beside its sentence, and a host without the door (one older
    /// than MSG1a, or a shared one) says so.
    /// </summary>
    /// <param name="from">
    /// Their author where that is not the person (COWORK1b, <see cref="DaorisWords.From"/>): kept as Daoris's, never read as the
    /// person's. A service from before the field ignores it and keeps the person's words, which is how such a word reads back.
    /// </param>
    /// <param name="key">The producer's key (COWORK1b): a say under a key the record holds is answered with the word kept.</param>
    /// <param name="level">When the sender asked the words to reach a working session (COWORK1c), kept beside them; null keeps none.</param>
    /// <param name="running">
    /// This driver runs the session and keeps the words for its run (COWORK1c, the message-urgency design §6): a running record keeps
    /// them only so, and a service from before refuses them as <c>running</c>, as it always did.
    /// </param>
    /// <param name="supervised">
    /// The supervisor's release (SUPERVISOR1a-i, D208; the supervisor design §3.4): its decision, question and choice, sent with no text,
    /// since the service composes the word from the choice. Null for every other say.
    /// </param>
    public async Task<SayAnswer> SayAsync(
        string id, string text, IReadOnlyList<string>? files = null, CancellationToken ct = default, string? channel = null,
        string? from = null, string? key = null, string? level = null, bool running = false,
        (string Decision, string Question, string Choice)? supervised = null)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            if (supervised is { } release)
            {
                writer.WriteString("decision", release.Decision);
                writer.WriteString("question", release.Question);
                writer.WriteString("choice", release.Choice);
            }
            else
            {
                writer.WriteString("text", text);
            }

            // SESSONE2e: the channel message these words are, which the ask holds already; an older service ignores it.
            if (channel is { Length: > 0 }) writer.WriteString("channel", channel);
            if (from is { Length: > 0 }) writer.WriteString("from", from);
            if (key is { Length: > 0 }) writer.WriteString("key", key);
            if (level is { Length: > 0 }) writer.WriteString("level", level);
            if (running) writer.WriteBoolean("running", true);
            if (files is { Count: > 0 })
            {
                writer.WriteStartArray("files");
                foreach (var name in files) writer.WriteStringValue(name);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(id)}/say", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answered)
        {
            return new SayAnswer(false, $"the service at {_base} has no door for words to a session that is not running ({status}) — is it older than this driver?");
        }

        if (!ok) return new SayAnswer(false, Text(answered, "error") ?? payload) { Refusal = Text(answered, "refusal") };
        return new SayAnswer(true, Text(answered, "message") ?? "")
        {
            Word = answered.ValueKind == JsonValueKind.Object && answered.TryGetProperty("said", out var said) ? ReadWord(said) : null,
        };
    }

    /// <summary>
    /// Take back one of the person's words waiting on a record before a session took it, or with <paramref name="text"/> reword
    /// it (SAIDBACK1a's take-back door, D137 §2.4): the old word leaves, and a reword's new words wait last under a new id, which
    /// comes back. Whether it was done is an answer, never an exception: a word no longer waiting is <c>taken</c>, a refusal
    /// carries the service's word beside its sentence, and a host without the door (one older than SAIDBACK1a, or a shared
    /// one) says so with no word.
    /// </summary>
    public async Task<TakeBackAnswer> TakeBackAsync(string id, string word, string? text = null, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("word", word);
            if (text is not null) writer.WriteString("text", text);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(id)}/take-back", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answered)
        {
            return new TakeBackAnswer(false, $"the service at {_base} has no door for taking back words a session has not taken ({status}) — is it older than this driver?");
        }

        if (!ok) return new TakeBackAnswer(false, Text(answered, "error") ?? payload, Text(answered, "refusal"));
        return new TakeBackAnswer(
            Flag(answered, "done"), Text(answered, "message") ?? "", Text(answered, "why"),
            answered.ValueKind == JsonValueKind.Object && answered.TryGetProperty("said", out var said) ? ReadWord(said) : null);
    }

    /// <summary>
    /// Mark these waiting words attempted on this machine's record, before the wire (COWORK1c's attempt door, the message-urgency
    /// design §6): the first moment kept, so a shell that comes up again never sends one twice. Whether they were marked is an answer,
    /// never an exception: a host without the door (one older than COWORK1c) is false with a sentence saying so.
    /// </summary>
    public async Task<(bool Ok, string Message)> AttemptAsync(string id, IReadOnlyList<string> said, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("said");
            foreach (var word in said) writer.WriteStringValue(word);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(id)}/attempt", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answered)
        {
            return (false, $"the service at {_base} has no door for a word's attempt ({status}) — is it older than this driver?");
        }

        return ok ? (true, Text(answered, "message") ?? "") : (false, Text(answered, "error") ?? payload);
    }

    /// <summary>
    /// Take the person's words off a record once a session took them (MSG1a's door, MSG1b): by their ids, so a word said
    /// after the driver read the record stays for the next run; <paramref name="by"/> names the new session a fallback
    /// handed them to. The service keeps each word said after the record ended on its ask as <c>reopened</c>. Whether they
    /// were taken is an answer, never an exception: a refusal, or a host without the door (one older than MSG1a, or a shared
    /// one), is false with a sentence saying which.
    /// </summary>
    public async Task<(bool Ok, string Message)> TakenAsync(
        string id, IReadOnlyList<string> said, string? by = null, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("said");
            foreach (var word in said) writer.WriteStringValue(word);
            writer.WriteEndArray();
            if (by is not null) writer.WriteString("by", by);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(id)}/taken", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answered)
        {
            return (false, $"the service at {_base} has no door for the words a session took ({status}) — is it older than this driver?");
        }

        return ok ? (true, Text(answered, "message") ?? "") : (false, Text(answered, "error") ?? payload);
    }

    /// <summary>
    /// Where this machine's claim on a quest stands (D68 §4): none, held, unconfirmed or lost — how the
    /// driver learns that a session it is running took a quest another machine took first.
    /// </summary>
    public async Task<string> ClaimAsync(string id, CancellationToken ct = default) =>
        RemoteSyncPayloads.Claim(await GetAsync($"/api/quests/{Uri.EscapeDataString(id)}/claim", ct).ConfigureAwait(false));

    /// <summary>
    /// <see cref="ClaimAsync"/>, with why a lost claim lost where the host says (FABRIC-CLOSE1c18): <c>source-reopened</c> where the take
    /// lost to the person's reopen of the work its step grew from; null otherwise, and from a host before it.
    /// </summary>
    public async Task<(string Claim, string? Why)> ClaimStandingAsync(string id, CancellationToken ct = default)
    {
        var (claim, why, _, _) = await ClaimTakeAsync(id, ct).ConfigureAwait(false);
        return (claim, why);
    }

    /// <summary>
    /// <see cref="ClaimStandingAsync"/>, with the person's bring-forward the take was made by where the host names one (SYNCMERGE1b0 point
    /// 3): what a look carries a stood-down session on by (SYNCMERGE1b). Null from a host before it, and for any other take. And when the
    /// take was made (CARRY3), null from a host before it.
    /// </summary>
    public async Task<(string Claim, string? Why, ForwardTake? Forward, DateTimeOffset? Taken)> ClaimTakeAsync(string id, CancellationToken ct = default)
    {
        var json = await GetAsync($"/api/quests/{Uri.EscapeDataString(id)}/claim", ct).ConfigureAwait(false);
        return (RemoteSyncPayloads.Claim(json), RemoteSyncPayloads.ClaimWhy(json), RemoteSyncPayloads.Forward(json), RemoteSyncPayloads.ClaimTaken(json));
    }

    /// <summary>Ask the ledger to queue a session. A refusal is an answer, not an exception.</summary>
    /// <param name="setup">
    /// That the quest is a set-up (<see cref="SetupQuests"/>), for the watchers alone (WSSETUP11): the
    /// ledger is not told, and the machine log marks the session's start.
    /// </param>
    /// <param name="from">The session this one carries on, why and who asked (MIXAGENT1a-iii), for its record; null for none.</param>
    /// <param name="access">
    /// The access setup this start resolved (AUTONOMY1a, D184 point 1), each pause with its value, for its record, which keeps it on this
    /// machine alone; null for none.
    /// </param>
    public async Task<(string? SessionId, string Message)> OpenSessionAsync(
        string questId, string adapter, string? harnessVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default, bool setup = false,
        ContinuedFrom? from = null, IReadOnlyDictionary<string, string>? access = null)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("quest", questId);
            writer.WriteString("actor", "build");
            writer.WriteString("adapter", adapter);
            // Observed by this side before the spawn (D49 §4) — the service has no binaries to look
            // at. Omitted when unknown rather than sent blank, as every optional field here is.
            if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
            if (profile is not null) writer.WriteString("profile", profile);
            // Which working tree this spawn will hold (D51). The same split: the service has no
            // checkout to look at, and this side is about to run a process in one.
            if (tree is not null) writer.WriteString("tree", tree);
            // Where that tree stood before the process ran (SURF6) — the range the review is measured
            // from. Observed here for the same reason as the two above: only this side has a checkout.
            if (baseCommit is not null) writer.WriteString("baseCommit", baseCommit);
            WriteFrom(writer, from);
            // The setup it starts under (AUTONOMY1a), in the pauses' order: resolved on this side, which holds the driver's file.
            if (access is { Count: > 0 })
            {
                writer.WriteStartObject("access");
                foreach (var (pause, value) in access) writer.WriteString(pause, value);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });

        return await OpenRecordAsync("/api/sessions", body, SessionOpened.Driven, adapter, repository: null, ct, setup).ConfigureAwait(false);
    }

    /// <summary>
    /// The session a new record continues, why and who asked (MIXAGENT1a-iii, D163 point 4): known on this side alone, which ran
    /// the hand-over or the carry-on. Omitted for none, as every optional field of an open is.
    /// </summary>
    private static void WriteFrom(Utf8JsonWriter writer, ContinuedFrom? from)
    {
        if (from is null) return;
        writer.WriteString("from", from.Session);
        writer.WriteString("fromWhy", from.Why);
        writer.WriteString("fromBy", from.By);
    }

    /// <summary>
    /// Ask the ledger to open a CHAT in a repository (D49 §3) — a person's session, serving no quest
    /// yet. A refusal is an answer, not an exception: the usual one is that the repository is busy,
    /// and the sentence names what holds it.
    /// </summary>
    /// <param name="from">The conversation this one continues, handed over (MIXAGENT1a-iii), for its record; null for none.</param>
    public async Task<(string? SessionId, string Message)> OpenChatAsync(
        string repository, string adapter, string? harnessVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default, ContinuedFrom? from = null)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("repository", repository);
            writer.WriteString("actor", "chat");
            writer.WriteString("adapter", adapter);
            if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
            if (profile is not null) writer.WriteString("profile", profile);
            if (tree is not null) writer.WriteString("tree", tree);
            if (baseCommit is not null) writer.WriteString("baseCommit", baseCommit);
            WriteFrom(writer, from);
            writer.WriteEndObject();
        });

        return await OpenRecordAsync("/api/sessions/chat", body, SessionOpened.Chat, adapter, repository, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Make an ask at a workspace (D65 §1a) — the sentence, its links, and files read from this
    /// machine. The service answers with what became of it; a refusal is an answer, not an exception.
    /// </summary>
    /// <param name="review">
    /// The person's review choice for its work (REVIEWENV1j; the review environment design §1.5), as the composer sends it: <c>off</c>,
    /// <c>on</c> or an environment's name; null sends none, and each repository's rule decides.
    /// </param>
    /// <param name="reviewWords">Their words with the choice, sent only with one.</param>
    /// <param name="workflow">
    /// The person's kind and workflow for its work (WORKFLOW1e; the workflow design §4.3), as the composer sends them; null sends
    /// none, and each repository's choice decides.
    /// </param>
    /// <param name="shortTitle">The ask's short title (HEAD2e), at most 40 characters; null sends none.</param>
    public Task<AskAnswer> AskAsync(
        string workspace, string sentence, IReadOnlyList<string> links,
        IReadOnlyList<(string Name, byte[] Content)> files, string? to, CancellationToken ct = default,
        string? review = null, string? reviewWords = null, AskWorkflowComposed? workflow = null, string? shortTitle = null) =>
        PostAskAsync("/api/asks", writer =>
        {
            writer.WriteString("workspace", workspace);
            writer.WriteString("sentence", sentence);
            // HEAD2e: written only where given, so an ask that gives none reads as it always did.
            if (!string.IsNullOrWhiteSpace(shortTitle)) writer.WriteString("short", shortTitle.Trim());
            writer.WriteStartArray("links");
            foreach (var link in links) writer.WriteStringValue(link);
            writer.WriteEndArray();
            writer.WriteStartArray("attachments");
            foreach (var (name, content) in files)
            {
                writer.WriteStartObject();
                writer.WriteString("name", name);
                writer.WriteBase64String("content", content);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (to is not null) writer.WriteString("to", to);
            if (review is not null)
            {
                writer.WriteString("review", review);
                if (!string.IsNullOrWhiteSpace(reviewWords)) writer.WriteString("reviewWords", reviewWords.Trim());
            }

            // Written only when chosen (WORKFLOW1e), so an ask that chooses none reads as it always did.
            if (workflow is { } chosen && (chosen.Kind ?? chosen.Workflow) is not null)
            {
                if (chosen.Kind is { } kind) writer.WriteString("kind", kind);
                if (chosen.Workflow is { } named) writer.WriteString("workflow", named);
                if (!string.IsNullOrWhiteSpace(chosen.Words)) writer.WriteString("workflowWords", chosen.Words.Trim());
            }
        }, ct);

    /// <summary>
    /// Ask the ledger to open an INTAKE for an ask (D65 §1b), in the room this side is about to run it
    /// in. A refusal is an answer, not an exception — usually that the ask is already answered.
    /// </summary>
    public async Task<(string? SessionId, string Message)> OpenIntakeAsync(
        string ask, string adapter, string room, string? harnessVersion = null, string? profile = null,
        CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("ask", ask);
            writer.WriteString("actor", "intake");
            writer.WriteString("adapter", adapter);
            writer.WriteString("room", room);
            if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
            if (profile is not null) writer.WriteString("profile", profile);
            writer.WriteEndObject();
        });

        return await OpenRecordAsync("/api/sessions/intake", body, SessionOpened.Intake, adapter, repository: null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Ask the ledger to open Ask Daoris's conversation (HELP1a, D89), in the room this side is about to
    /// run it in. A refusal is an answer, not an exception — usually that one is already running in its workspace.
    /// </summary>
    /// <param name="workspace">
    /// The workspace the person's window was on at the press, which the record keeps (ASKSCOPE1a); null sends none, which the
    /// ledger records as <c>default</c>, as it did before.
    /// </param>
    /// <param name="mode">
    /// The access mode its start gets (HELPMODE1a2, D183 point 3), the effective one, which the record keeps; null sends none, which
    /// the ledger records as restricted.
    /// </param>
    public async Task<(string? SessionId, string Message)> OpenHelpAsync(
        string adapter, string room, string? harnessVersion = null, string? profile = null, string? workspace = null,
        string? mode = null, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("adapter", adapter);
            writer.WriteString("actor", "ask-daoris");
            writer.WriteString("room", room);
            if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
            if (profile is not null) writer.WriteString("profile", profile);
            if (workspace is not null) writer.WriteString("workspace", workspace);
            if (mode is not null) writer.WriteString("mode", mode);
            writer.WriteEndObject();
        });

        return await OpenRecordAsync("/api/sessions/help", body, SessionOpened.Help, adapter, repository: null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Record the access mode an Ask Daoris conversation going on runs under (HELPMODE1a2, D183 point 3), where the person's mode
    /// changed since it last ran. A refusal is the ledger's sentence (<see cref="DriverException"/>).
    /// </summary>
    public async Task KeepHelpModeAsync(string sessionId, string mode, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("mode", mode);
            writer.WriteEndObject();
        });

        var (ok, _, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(sessionId)}/help-mode", body, ct)
            .ConfigureAwait(false);
        if (!ok)
        {
            throw new DriverException(
                $"the service refused recording that Ask Daoris `{sessionId}` goes on {mode}: "
                + ((root is { } refused ? Text(refused, "error") : null) ?? payload));
        }
    }

    /// <summary>
    /// Record the access setup a resumed driven session now runs under (AUTONOMY1b2, D184 point 1), where the person's setup changed
    /// since it started. A refusal is the ledger's sentence (<see cref="DriverException"/>).
    /// </summary>
    public async Task KeepAccessAsync(string sessionId, IReadOnlyDictionary<string, string> access, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartObject("access");
            foreach (var (pause, value) in access) writer.WriteString(pause, value);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });

        var (ok, _, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(sessionId)}/access", body, ct)
            .ConfigureAwait(false);
        if (!ok)
        {
            throw new DriverException(
                $"the service refused recording the setup session `{sessionId}` goes on under: "
                + ((root is { } refused ? Text(refused, "error") : null) ?? payload));
        }
    }

    /// <summary>
    /// Record the downloaded versions a start of this session was handed (KEEP1b, D195 point 6; the cleanup design §3.5, Fable F2.5),
    /// <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c> (<see cref="DownloadClearing.Handed"/>), which the record adds to those of its
    /// earlier starts: the downloads' rule keeps each while the session runs. A refusal is the ledger's sentence (<see cref="DriverException"/>).
    /// </summary>
    public async Task KeepResolvedAsync(string sessionId, IReadOnlyList<string> versions, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("resolved");
            foreach (var version in versions) writer.WriteStringValue(version);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        var (ok, _, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(sessionId)}/resolved", body, ct)
            .ConfigureAwait(false);
        if (!ok)
        {
            throw new DriverException(
                $"the service refused recording the versions session `{sessionId}` was handed: "
                + ((root is { } refused ? Text(refused, "error") : null) ?? payload));
        }
    }

    /// <summary>
    /// Post a finished tree's record (KEEPSIZE1b, the keep-size design §2.2) to <c>POST /api/trees/record</c>: the manifest's facts and the
    /// commits read from the tip. Null where the service keeps it, a second post of a kept identity included; else why it does not, in its
    /// words, a store a newer build stamped among them; a 404, a service with no door for it, says so (<see cref="RecordRefusal.NoDoor"/>).
    /// </summary>
    internal async Task<RecordRefusal?> PostWorkRecordAsync(FinishedPost record, CancellationToken ct = default)
    {
        var tree = record.Tree;
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("workspace", record.Workspace);
            writer.WriteString("repository", record.Repository);
            writer.WriteString("folder", record.Folder);
            writer.WriteString("tree", tree.Tree);
            writer.WriteString("moved", record.Moved.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            Optional("unit", tree.Unit);
            writer.WriteString("branch", record.Manifest.Branch);
            Optional("line", tree.Line);
            writer.WriteStartObject("hold");
            writer.WriteString("kind", tree.Hold.Kind);
            Optional("where", tree.Hold.Where);
            Optional("form", tree.Hold.Form);
            writer.WriteEndObject();
            writer.WriteString("tip", tree.Tip);
            Optional("grewFrom", tree.GrewFrom);
            Optional("ref", tree.Ref);
            if (record.Commits is { } commits)
            {
                writer.WriteStartArray("commits");
                foreach (var commit in commits)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", commit.Id);
                    writer.WriteString("subject", commit.Subject);
                    writer.WriteStartArray("files");
                    foreach (var (path, added, removed) in commit.Files)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("path", path);
                        writer.WriteNumber("added", added);
                        writer.WriteNumber("removed", removed);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull("commits");
            }

            Optional("commitsUnread", record.Unread);
            writer.WriteStartObject("left");
            writer.WriteStartArray("names");
            foreach (var name in tree.Left.Names) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteNumber("count", tree.Left.Count);
            writer.WriteBoolean("cut", tree.Left.Cut);
            writer.WriteEndObject();
            writer.WriteBoolean("whole", tree.Whole);
            // Codex's read of the design, finding 6: the landing captured before the move, or why none is named.
            if (tree.Landing is { } landing)
            {
                writer.WriteStartObject("landing");
                writer.WriteString("branch", landing.Branch);
                Optional("line", landing.Line);
                writer.WriteString("at", landing.At.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                Optional("pullRequest", landing.PullRequest);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull("landing");
            }

            Optional("landingWhy", tree.LandingWhy);
            writer.WriteEndObject();

            void Optional(string name, string? text)
            {
                if (text is null) writer.WriteNull(name);
                else writer.WriteString(name, text);
            }
        });

        var (ok, status, payload, root) = await PostJsonAsync("/api/trees/record", body, ct).ConfigureAwait(false);
        if (ok) return null;
        if (status == 404) return new("the service has no door for a finished tree's record, so its folder keeps its days", NoDoor: true);
        return new((root is { } refused ? Text(refused, "error") : null) ?? payload);
    }

    /// <summary>
    /// History's age applied to the finished trees' records no session names (KEEPSIZE1b, <c>POST /api/trees/record/age</c>): each kept at
    /// or before <paramref name="before"/> goes; how many. A service with no such door answers none.
    /// </summary>
    internal async Task<int> AgeWorkRecordsAsync(DateTimeOffset before, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("before", before.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync("/api/trees/record/age", body, ct).ConfigureAwait(false);
        if (ok) return root is { } answered && answered.TryGetProperty("cleared", out var cleared) && cleared.TryGetInt32(out var count) ? count : 0;
        if (status == 404) return 0;
        throw new DriverException($"the service refused ageing the finished trees' records: {(root is { } refused ? Text(refused, "error") : null) ?? payload}");
    }

    /// <summary>This machine's asks that are not closed, newest first — what the loop finds intakes in.</summary>
    public async Task<IReadOnlyList<AskView>> AsksAsync(CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/asks", ct).ConfigureAwait(false));
        return [.. document.RootElement.EnumerateArray().Select(ReadAsk)];
    }

    /// <summary>Every ask this machine holds, closed ones included — what a delete Ask Daoris proposes is judged against (HELP6).</summary>
    public async Task<IReadOnlyList<AskView>> EveryAskAsync(CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/asks?includeClosed=true", ct).ConfigureAwait(false));
        return [.. document.RootElement.EnumerateArray().Select(ReadAsk)];
    }

    /// <summary>Every quest, closed ones included, each with the service's reading of whether it may be deleted (HELP6).</summary>
    public async Task<IReadOnlyList<QuestView>> EveryQuestAsync(CancellationToken ct = default) =>
        ReadQuests(await GetAsync("/api/quests?includeClosed=true", ct).ConfigureAwait(false));

    /// <summary>
    /// The path of each entry the index holds as one repository's own (D47 §4's door), once per entry — what a set-up
    /// quest says the workspace already knows of it (LAYOUT7, D124 §2.2). Canonical doctrine is not its own.
    /// </summary>
    public async Task<IReadOnlyList<string>> EntryPathsAsync(string repository, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(
            await GetAsync($"/api/entries?repository={Uri.EscapeDataString(repository)}", ct).ConfigureAwait(false));
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? [.. document.RootElement.EnumerateArray().Select(entry => Text(entry, "path")).OfType<string>()]
            : [];
    }

    /// <summary>One ask as it stands — how an intake's end is observed. Null when the service has none.</summary>
    public async Task<AskView?> FindAskAsync(string id, CancellationToken ct = default)
    {
        var answer = await _doors.SendAsync(
            ServiceVerb.Get, $"/api/asks/{Uri.EscapeDataString(id.TrimStart('#'))}", null, ct).ConfigureAwait(false);
        if (answer.Status == (int)System.Net.HttpStatusCode.NotFound) return null;

        if (!answer.Ok)
        {
            throw new DriverException($"the service would not say what became of ask `#{id}`: {answer.Payload}");
        }

        using var document = JsonDocument.Parse(answer.Payload);
        return ReadAsk(document.RootElement);
    }

    /// <summary>
    /// An ask's channel as one entry reads it (SESSONE2a's <c>GET /api/asks/{id}/channel?entry=</c>), which moves no mark: what the
    /// look hands a running part, and a starting part's instruction carries (SESSONE2b). Null where the host holds no such ask, or
    /// is one from before the channel, which answers the route not found as well.
    /// </summary>
    /// <exception cref="DriverException">The host answered something other than the channel.</exception>
    public async Task<ChannelView?> ChannelAsync(string ask, string? entry, CancellationToken ct = default, DateTimeOffset? since = null)
    {
        // SESSONE2c: no entry is the person's own (the service's default), and `since` the page's tail of a long channel.
        var query = string.Join('&', new[]
        {
            entry is null ? null : $"entry={Uri.EscapeDataString(entry)}",
            since is { } after ? $"since={Uri.EscapeDataString(Moments.Write(after))}" : null,
        }.OfType<string>());
        var answer = await _doors.SendAsync(
            ServiceVerb.Get, $"/api/asks/{Uri.EscapeDataString(ask.TrimStart('#'))}/channel{(query.Length == 0 ? "" : "?" + query)}", null, ct)
            .ConfigureAwait(false);
        if (answer.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!answer.Ok) throw new DriverException($"the service would not read ask `#{ask}`'s channel: {answer.Payload}");

        return ChannelView.Read(answer.Payload)
               ?? throw new DriverException($"the service answered something other than ask `#{ask}`'s channel: {answer.Payload}");
    }

    /// <summary>
    /// The asks the mirrored channel messages name that this machine does not hold (SESSONE2h's <c>GET /api/channel/elsewhere</c>),
    /// newest message first. Empty where the host is one from before the door, which answers the route not found.
    /// </summary>
    /// <exception cref="DriverException">The host answered something other than the list.</exception>
    public async Task<IReadOnlyList<ElsewhereAskAnswer>> ElsewhereAsksAsync(CancellationToken ct = default)
    {
        var answer = await _doors.SendAsync(ServiceVerb.Get, "/api/channel/elsewhere", null, ct).ConfigureAwait(false);
        if (answer.Status == (int)System.Net.HttpStatusCode.NotFound) return [];
        if (!answer.Ok) throw new DriverException($"the service would not list the asks held elsewhere: {answer.Payload}");

        using var document = JsonDocument.Parse(answer.Payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("asks", out var listed) || listed.ValueKind != JsonValueKind.Array)
        {
            throw new DriverException($"the service answered something other than the asks held elsewhere: {answer.Payload}");
        }

        return [.. listed.EnumerateArray().Select(row => (row, id: Json.Text(row, "id"), latest: Moments.Read(row, "latest")))
            .Where(read => read.id is { Length: > 0 } && read.latest is not null)
            .Select(read => new ElsewhereAskAnswer(
                read.id!, Json.Text(read.row, "workspace") ?? "",
                read.row.TryGetProperty("origins", out var origins) && origins.ValueKind == JsonValueKind.Array
                    ? [.. origins.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.String).Select(o => o.GetString()!)]
                    : [],
                read.latest!.Value, Count(read.row, "messages"), Count(read.row, "unread")))];

        static int Count(JsonElement row, string name) => row.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : 0;
    }

    /// <summary>
    /// An entry's acknowledgment of messages on an ask's channel by their ids (COWORK1d's <c>POST …/channel/ack</c>; the message-urgency
    /// design §6): a part's claim (<c>taking</c>), one part's only, its letting go (<c>released</c>), a start's packet (<c>taken</c>,
    /// <c>partly</c>), the write (<c>sent</c>) and the door's word (<c>taken</c>). Answers the ids it changed: for a claim, those this
    /// part won. A host without the door, or without the ask, changes none, so nothing is claimed and the messages wait on the channel
    /// for the agent's own read.
    /// </summary>
    /// <exception cref="DriverException">The host refused the acknowledgment.</exception>
    public async Task<IReadOnlyList<string>> ChannelAckAsync(
        string ask, string entry, string session, string state, IReadOnlyList<string> messages, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("entry", entry);
            writer.WriteString("session", session);
            writer.WriteString("state", state);
            writer.WriteStartArray("messages");
            foreach (var message in messages) writer.WriteStringValue(message);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/asks/{Uri.EscapeDataString(ask.TrimStart('#'))}/channel/ack", body, ct).ConfigureAwait(false);
        if (status == (int)System.Net.HttpStatusCode.NotFound) return [];
        if (!ok) throw new DriverException($"the service would not keep `{entry}`'s {state} of ask `#{ask}`'s channel: {payload}");

        return root is { ValueKind: JsonValueKind.Object } answered
               && answered.TryGetProperty("acknowledged", out var changed) && changed.ValueKind == JsonValueKind.Array
            ? [.. changed.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!)]
            : [];
    }

    /// <summary>
    /// Move the person's own read mark on an ask's channel (SESSONE2c, <c>POST …/channel/read</c> with no entry), through
    /// <paramref name="through"/> or the newest item, and answer the channel as it then reads. Null where the host holds no such ask,
    /// or is one from before the channel.
    /// </summary>
    /// <exception cref="DriverException">The host refused the move.</exception>
    public async Task<ChannelView?> ChannelMarkAsync(string ask, DateTimeOffset? through = null, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            if (through is { } moment) writer.WriteString("through", Moments.Write(moment));
            writer.WriteEndObject();
        });
        var (ok, status, payload, _) = await PostJsonAsync(
            $"/api/asks/{Uri.EscapeDataString(ask.TrimStart('#'))}/channel/read", body, ct).ConfigureAwait(false);
        if (status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!ok) throw new DriverException($"the service would not move the person's mark on ask `#{ask}`'s channel: {payload}");

        return ChannelView.Read(payload);
    }

    /// <summary>
    /// The person's post on an ask's channel (SESSONE2c, <c>POST /api/asks/{id}/channel</c>): their words, to everyone or one of the
    /// ask's repositories, answering a message or not. A refusal is an answer carrying the service's code (<c>empty</c>,
    /// <c>too-long</c>, <c>bad-to</c>, <c>bad-re</c>, <c>not-found</c>), which each door words its own way.
    /// </summary>
    /// <param name="urgent">
    /// Their level (COWORK1e, D188 point 2): false posts it ordinary, true urgent; null sends none, and the service keeps the
    /// person's default, urgent.
    /// </param>
    /// <exception cref="DriverException">The host answered neither the post nor a code.</exception>
    public async Task<ChannelPosted> ChannelPostAsync(
        string ask, string text, string? to, string? re, CancellationToken ct = default, bool? urgent = null)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("text", text);
            if (to is not null) writer.WriteString("to", to);
            if (re is not null) writer.WriteString("re", re);
            if (urgent is { } level) writer.WriteBoolean("urgent", level);
            writer.WriteEndObject();
        });
        var (ok, _, payload, root) = await PostJsonAsync($"/api/asks/{Uri.EscapeDataString(ask.TrimStart('#'))}/channel", body, ct)
            .ConfigureAwait(false);
        if (ok)
        {
            return root is { ValueKind: JsonValueKind.Object } posted && posted.TryGetProperty("message", out var message)
                   && ChannelView.ReadItem(message) is { } item
                ? new ChannelPosted(true, item, null)
                : throw new DriverException($"the service answered something other than a posted message on ask `#{ask}`'s channel: {payload}");
        }

        return root is { ValueKind: JsonValueKind.Object } refused && Text(refused, "code") is { Length: > 0 } code
            ? new ChannelPosted(false, null, code)
            : throw new DriverException($"the service would not take the post on ask `#{ask}`'s channel: {payload}");
    }

    /// <summary>
    /// A circle's declarations — what each repository owns and accepts, and where it is (D34). The
    /// intake's room is written from this, so it decides from the registry as it stands now.
    /// </summary>
    public async Task<IReadOnlyList<DeclarationView>> DeclarationsAsync(string workspace, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(
            await GetAsync($"/api/registry?workspace={Uri.EscapeDataString(workspace)}", ct).ConfigureAwait(false));
        return
        [
            .. document.RootElement.EnumerateArray().Select(repo => new DeclarationView(
                Text(repo, "repository") ?? "",
                repo.TryGetProperty("adopted", out var adopted) && adopted.ValueKind == JsonValueKind.True,
                repo.TryGetProperty("registered", out var registered) && registered.ValueKind == JsonValueKind.True,
                Text(repo, "summary"),
                Strings(repo, "owns"),
                Strings(repo, "accepts"),
                Text(repo, "root"))
            {
                // Its lanes' words (D115 §2.2). Absent is none: a host from before lanes answers without them.
                Lanes = repo.TryGetProperty("lanes", out var lanes) && lanes.ValueKind == JsonValueKind.Array
                    ? lanes.EnumerateArray()
                        .Where(lane => lane.ValueKind == JsonValueKind.Object && Text(lane, "id") is { Length: > 0 })
                        .Select(lane => new LaneView(
                            Text(lane, "id")!, Text(lane, "title") ?? "", Text(lane, "summary") ?? "", Flag(lane, "steward")))
                        .ToList()
                    : [],
            }),
        ];
    }

    /// <summary>One ask as the service writes it — the reader the routes use, for the tests that hold its fields.</summary>
    internal static AskView ReadAskJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ReadAsk(document.RootElement);
    }

    /// <summary>
    /// The person's words on an ask (DRIFT1a), oldest first, or null where the host answered none — a host from before
    /// them, which is never read as the person having said nothing. A word without its kind, its words or a moment that
    /// reads is passed over, as the service passes over a kind it does not know: never a failed read of the ask.
    /// </summary>
    private static IReadOnlyList<AskWordView>? ReadWords(JsonElement ask)
    {
        if (!ask.TryGetProperty("words", out var words) || words.ValueKind != JsonValueKind.Array) return null;
        return
        [
            .. words.EnumerateArray()
                .Select(word => (Kind: Text(word, "kind"), Said: Text(word, "text"), At: Moment(word, "at"), Word: word))
                .Where(word => word.Kind is { Length: > 0 } && word.Said is not null && word.At is not null)
                .Select(word => new AskWordView(word.Kind!, word.Said!, word.At!.Value, Text(word.Word, "session"), Text(word.Word, "quest"))),
        ];
    }

    /// <summary>An ISO 8601 moment the service wrote, or null where it wrote none that reads.</summary>
    private static DateTimeOffset? Moment(JsonElement element, string name) => Moments.Read(element, name);

    private static AskView ReadAsk(JsonElement ask)
    {
        var proposal = ReadProposal(ask);
        return new(
            Text(ask, "id") ?? "", Text(ask, "workspace") ?? "", Text(ask, "sentence") ?? "",
            Text(ask, "state") ?? "", Text(ask, "tier") ?? "")
        {
            Asker = Text(ask, "asker"),
            Note = Text(ask, "note"),
            // Absent is none: a host from before the intake answers without it.
            Intake = Text(ask, "intake"),
            Links = Strings(ask, "links"),
            Attachments = ask.TryGetProperty("attachments", out var files) && files.ValueKind == JsonValueKind.Array
                ? [.. files.EnumerateArray().Select(file => new QuestFileView(
                    Text(file, "name") ?? "", Text(file, "sha256") ?? "",
                    file.TryGetProperty("bytes", out var bytes) && bytes.ValueKind == JsonValueKind.Number ? bytes.GetInt64() : 0,
                    Text(file, "path")))]
                : [],
            Quests = Strings(ask, "quests"),
            Proposed = [.. proposal.Select(match => match.Repository)],
            // ASKNAME1b: a proposal whose evidence is its own name is one the sentence names, and the intake is told so.
            Named = [.. proposal.Where(match => Asks.IsNamed(match.Repository, match.Matched)).Select(match => match.Repository)],
            Deletable = Flag(ask, "deletable"),
            // The person's words (DRIFT1a), which every session on the ask is handed (DRIFT1b).
            Words = ReadWords(ask),
            WordsKeptFrom = Moment(ask, "wordsKeptFrom"),
            // The go-aheads its sessions asked (KNOWUSE1a), which every session on the ask is handed beside the words.
            GoAheads = ReadGoAheads(ask),
            // The person's review choices and its intake's proposals (REVIEWENV1b). Absent is none, and a host before them.
            ReviewChoices = ReadReviewChoices(ask),
            ReviewProposals = ReadReviewProposals(ask),
            // The person's kind and workflow for its work (WORKFLOW1e). Absent is none, and a host before them.
            WorkflowChoices = ReadWorkflowChoices(ask),
            // Its intake's kind proposals (WORKFLOW1i): read, never applied here (WORKFLOW1i2).
            WorkflowProposals = ReadWorkflowProposals(ask),
        };
    }

    /// <summary>
    /// What the declarations tier proposed for an ask, best first: each repository with the words it matched, or with its
    /// name alone where the sentence names it (ASKNAME1). One without a repository is passed over, as before.
    /// </summary>
    private static List<(string Repository, IReadOnlyList<string> Matched)> ReadProposal(JsonElement ask) =>
        ask.TryGetProperty("proposal", out var proposal) && proposal.ValueKind == JsonValueKind.Array
            ? [.. proposal.EnumerateArray()
                .Where(match => Text(match, "repository") is not null)
                .Select(match => (Text(match, "repository")!, Strings(match, "matched")))]
            : [];

    /// <summary>
    /// The go-aheads an ask holds (KNOWUSE1a), oldest first; null where the host answered none, a host from before them. One
    /// this build cannot read is passed over, as the service passes over a kind it does not know: never a failed read.
    /// </summary>
    private static IReadOnlyList<GoAheadView>? ReadGoAheads(JsonElement ask)
    {
        if (!ask.TryGetProperty("goAheads", out var held) || held.ValueKind != JsonValueKind.Array) return null;
        var goAheads = new List<GoAheadView>();
        foreach (var goAhead in held.EnumerateArray())
        {
            if (goAhead.ValueKind != JsonValueKind.Object
                || !goAhead.TryGetProperty("number", out var number) || number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var n)
                || Text(goAhead, "kind") is not { Length: > 0 } kind || Text(goAhead, "on") is not { Length: > 0 } on
                || Text(goAhead, "act") is not { Length: > 0 } act)
            {
                continue;
            }

            var asked = goAhead.TryGetProperty("asked", out var requests) && requests.ValueKind == JsonValueKind.Array
                ? requests.EnumerateArray()
                    .Where(request => request.ValueKind == JsonValueKind.Object && Text(request, "session") is { Length: > 0 } && Moment(request, "at") is not null)
                    .Select(request => new GoAheadRequestView(Text(request, "session")!, Text(request, "quest"), Moment(request, "at")!.Value, Text(request, "why") ?? ""))
                    .ToList()
                : [];
            GoAheadAnswerView? answer = null;
            if (goAhead.TryGetProperty("answer", out var answered) && answered.ValueKind == JsonValueKind.Object
                && answered.TryGetProperty("approved", out var approved) && approved.ValueKind is JsonValueKind.True or JsonValueKind.False
                && Moment(answered, "at") is { } at)
            {
                answer = new GoAheadAnswerView(approved.GetBoolean(), Text(answered, "words"), at);
            }

            goAheads.Add(new GoAheadView(n, kind, on, act, asked)
            {
                Answer = answer,
                Near = goAhead.TryGetProperty("near", out var near) && near.ValueKind == JsonValueKind.Number && near.TryGetInt32(out var m) ? m : null,
            });
        }

        return goAheads;
    }

    /// <summary>
    /// The person's yes or no to a go-ahead a session asked on their ask (KNOWUSE1a, D135 §2), with their words where they
    /// give any: the terminal's door. The service's sentence comes back verbatim, a refusal (no such go-ahead) included.
    /// </summary>
    /// <param name="goesOn">
    /// Whether the answer that leaves none of a parked session's go-aheads waiting is that park's answer, so the session goes
    /// on with it (GOAHEAD2), as the ask's page has it: the terminal's door and the session page's (GOAHEAD2b) both say so.
    /// False by default, for a caller that answers the park itself, which leaves the park to it.
    /// </param>
    public async Task<(bool Ok, string Message)> AnswerGoAheadAsync(
        string ask, int number, bool approved, string? words, CancellationToken ct = default, bool goesOn = false)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("answer", approved ? "approved" : "refused");
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words);
            // Always said: a host reads it absent as true, the ask page's way (GOAHEAD2).
            writer.WriteBoolean("goesOn", goesOn);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/asks/{Uri.EscapeDataString(ask.Trim().TrimStart('#'))}/go-aheads/{number.ToString(CultureInfo.InvariantCulture)}", body, ct)
            .ConfigureAwait(false);
        // A host older than the go-ahead door answers a bare 404 or 405 — said plainly, not parsed as nothing.
        if (root is not { } answer)
        {
            return (false, $"the service at {_base} has no go-ahead door ({status}) — is it older than this driver?");
        }

        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    // Empty strings are kept here, as the copy this replaced kept them.
    private static IReadOnlyList<string> Strings(JsonElement element, string name) => Json.Strings(element, name, keepEmpty: true);

    /// <summary>A person turns an ask into a quest for <paramref name="to"/>.</summary>
    public Task<AskAnswer> PublishAskAsync(string id, string to, CancellationToken ct = default) =>
        PostAskAsync($"/api/asks/{Uri.EscapeDataString(id.TrimStart('#'))}/publish", w => w.WriteString("to", to), ct);

    /// <summary>A person closes their own ask, with the reason.</summary>
    public Task<AskAnswer> CloseAskAsync(string id, string reason, CancellationToken ct = default) =>
        PostAskAsync($"/api/asks/{Uri.EscapeDataString(id.TrimStart('#'))}/close", w => w.WriteString("reason", reason), ct);

    /// <summary>
    /// Decline a quest with the person's reason, verbatim (PAUSE1d, D132 §4.1): an abandon's decline. With
    /// <paramref name="whileOpen"/> it applies only while the quest is open (PAUSE1c, D132 point 10), so a take that reached
    /// a remote first stands and the decline is kept on the quest as a conflict. The service's sentence comes back
    /// verbatim, a refusal included: a decline refused because the quest was taken meanwhile is an answer, not an exception.
    /// </summary>
    public async Task<(bool Ok, string Message)> DeclineQuestAsync(string id, string reason, bool whileOpen, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("action", "decline");
            writer.WriteString("reason", reason);
            // Written only when set, so a plain decline is the request it always was.
            if (whileOpen) writer.WriteBoolean("whileOpen", true);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(id.TrimStart('#'))}/respond", body, ct).ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no respond door ({status}) — is it older than this driver?");
        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// A person deletes an ask made by mistake, with every quest asked by it (D95). The service's
    /// sentence comes back verbatim, and a refusal is an answer too.
    /// </summary>
    public Task<(bool Ok, string Message)> DeleteAskAsync(string id, CancellationToken ct = default) =>
        DeleteRecordAsync($"/api/asks/{Uri.EscapeDataString(id.TrimStart('#'))}", ct);

    /// <summary>A person deletes a quest made by mistake (D95) — refused, in the service's words, for one anything stands on.</summary>
    public Task<(bool Ok, string Message)> DeleteQuestAsync(string id, CancellationToken ct = default) =>
        DeleteRecordAsync($"/api/quests/{Uri.EscapeDataString(id.TrimStart('#'))}", ct);

    /// <summary>
    /// The person's yes to a done's departure from what they required (DRIFT1d, D133 §4): what it held goes on. The
    /// service's sentence comes back verbatim, a refusal (nothing waits for a yes) included.
    /// </summary>
    public async Task<(bool Ok, string Message)> AcceptDepartureAsync(string id, CancellationToken ct = default)
    {
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(id.TrimStart('#'))}/accept", "{}", ct).ConfigureAwait(false);
        // A host older than the accept door answers a bare 404 or 405 — said plainly, not parsed as nothing.
        if (root is not { } answer)
        {
            return (false, $"the service at {_base} has no accept door ({status}) — is it older than this driver?");
        }

        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// The person marks a quest done (QUESTCLOSE1, D126's note): their words, verbatim, to the service's own door for it, which
    /// writes the sentence saying the done was theirs before them and answers none of their requirements. Never respond's
    /// door, which is an agent's done. The service's sentence comes back verbatim, a refusal (a closed quest, no such quest)
    /// included, and a host older than the door is said to be.
    /// </summary>
    public async Task<(bool Ok, string Message)> PersonDoneAsync(string id, string? note, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            // Written only when given: no words is the service's sentence alone.
            if (!string.IsNullOrWhiteSpace(note)) writer.WriteString("note", note.Trim());
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(id.TrimStart('#'))}/done", body, ct).ConfigureAwait(false);
        if (root is not { } answer)
        {
            return (false, $"the service at {_base} has no done door for a person ({status}) — is it older than this driver?");
        }

        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// The person's reopen of a sealed submission (FABRIC-CLOSE1c3, §E5; D156): <c>POST /api/quests/{id}/reopen</c>, naming the
    /// submission by its machine and sequence, with their words where they wrote any. The service's sentence comes back verbatim, a
    /// refusal (another submission current, nothing published from it, a step closed on it) included.
    /// </summary>
    public Task<(bool Ok, string Message)> ReopenQuestAsync(
        string id, QuestSubmissionRef submission, string? note, CancellationToken ct = default) =>
        PersonMoveAsync(id, "reopen", WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartObject("submission");
            writer.WriteString("machine", submission.Machine);
            writer.WriteNumber("sequence", submission.Sequence);
            writer.WriteEndObject();
            // Written only when given: no words is the reopen alone.
            if (!string.IsNullOrWhiteSpace(note)) writer.WriteString("note", note.Trim());
            writer.WriteEndObject();
        }), ct);

    /// <summary>
    /// The person's word to go on with a step its source's reopen held (FABRIC-CLOSE1c3): <c>POST /api/quests/{id}/continue</c>. A
    /// refusal (its source not ready again, nothing holding it) is the service's sentence.
    /// </summary>
    public Task<(bool Ok, string Message)> ContinueQuestAsync(string id, CancellationToken ct = default) =>
        PersonMoveAsync(id, "continue", "{}", ct);

    #region Bring-forward (SYNCMERGE1c3)

    /// <summary>
    /// The person's bring-forward of one side of two machines' work on a quest (SYNCMERGE1c3; D178): <c>POST
    /// /api/quests/{id}/conflicts/forward</c> with the side's machine and sequence. A refusal or a loss again (409) and a quest not
    /// found (404) come back as the service's sentence, not as a tool error.
    /// </summary>
    public Task<(bool Ok, string Message)> ForwardConflictAsync(
        string id, string machine, long sequence, CancellationToken ct = default) =>
        PersonMoveAsync(id, "conflicts/forward", WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("machine", machine);
            writer.WriteNumber("sequence", sequence);
            writer.WriteEndObject();
        }), ct);

    #endregion

    /// <summary>A person's door on one quest (FABRIC-CLOSE1c3): its sentence, a refusal's included, and a host older than it said to be.</summary>
    private async Task<(bool Ok, string Message)> PersonMoveAsync(string id, string door, string body, CancellationToken ct)
    {
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(id.TrimStart('#'))}/{door}", body, ct).ConfigureAwait(false);
        if (root is not { } answer)
        {
            return (false, $"the service at {_base} has no {door} door ({status}) — is it older than this driver?");
        }

        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// Whether this client's host knows a closure graph's operations and doors (FABRIC-CLOSE1b's handshake): <c>/api/status</c>'s
    /// <c>closure</c> and (CLOSECAP1) <c>closeOutcome</c>, the outcome door's recovery: both must be answered. A host from before
    /// answers none, or <c>closure</c> alone, and is not asked to keep what it cannot recover.
    /// </summary>
    public async Task<bool> KnowsClosureAsync(CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/status", ct).ConfigureAwait(false));
        return Flag(document.RootElement, "closure") && Flag(document.RootElement, "closeOutcome");
    }

    /// <summary>
    /// Whether this client's host judges a bind in a workspace synced through a remote by the remote's handshake (FABRIC-CLOSE1i):
    /// <c>/api/status</c>'s <c>closeRemote</c>, beside the two <see cref="KnowsClosureAsync"/> reads. A host before it answers none, and is
    /// asked no bind there.
    /// </summary>
    public async Task<bool> KnowsRemoteClosureAsync(CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/status", ct).ConfigureAwait(false));
        return Flag(document.RootElement, "closure") && Flag(document.RootElement, "closeOutcome") && Flag(document.RootElement, "closeRemote");
    }

    /// <summary>
    /// The driver binds a quest it took to the graph that closes it (FABRIC-CLOSE1a's <c>POST /api/quests/{id}/bind</c>), at its take. The
    /// service's sentence comes back verbatim, a refusal included, and a host older than the door is said to be.
    /// </summary>
    public async Task<(bool Ok, string Message)> BindQuestAsync(string id, string graph, string session, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("graph", graph);
            // FABRIC-CLOSE1l (CLOSE1f): named for the take as the first session opens, never bound after it; a host before it refuses an
            // open quest's bind as not taken, and the quest closes by its done.
            writer.WriteBoolean("atTake", true);
            // FABRIC-CLOSE1o: the session opening, whose take alone binds it. A host before it reads it as absent, and binds as CLOSE1l did.
            writer.WriteString("session", session);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(id.TrimStart('#'))}/bind", body, ct).ConfigureAwait(false);
        if (root is not { } answer)
        {
            return (false, $"the service at {_base} has no bind door ({status}) — is it older than this driver?");
        }

        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// The graph's close (FABRIC-CLOSE1a's <c>POST /api/quests/{id}/close</c>, §E5 step 2): every quest named, with the submission it judged,
    /// under its key and token. A refusal is an answer with its check's code (<c>key-reused</c> among them), the same close again answers its
    /// first outcome, and a host older than the door is said to be.
    /// </summary>
    /// <exception cref="HttpRequestException">The host did not answer: the caller keeps the close unknown, and asks what became of it (FABRIC-CLOSE1u).</exception>
    public Task<QuestCloseAnswer> CloseQuestsAsync(
        string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct = default) =>
        CloseDoorAsync("close", quest, key, token, quests, ct);

    /// <summary>
    /// What became of a close sent with <paramref name="key"/> (FABRIC-CLOSE1u, <c>POST /api/quests/{id}/close/outcome</c>): the close as it was
    /// sent, answered from the door's own record and read as the close is: its first outcome, or refused <c>withdrawn</c> where it never took
    /// the key. A host before the question has no such door, which proves nothing, so the close stays unknown.
    /// </summary>
    /// <exception cref="HttpRequestException">The host did not answer, or answered as no door of its own: the close stays unknown.</exception>
    public Task<QuestCloseAnswer> CloseOutcomeAsync(
        string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct = default) =>
        CloseDoorAsync("close/outcome", quest, key, token, quests, ct);

    /// <summary>One call at the close door's <paramref name="door"/>: the close as it is named, and the door's own answer, else a throw.</summary>
    private async Task<QuestCloseAnswer> CloseDoorAsync(
        string door, string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("key", key);
            writer.WriteString("token", token);
            writer.WriteStartArray("quests");
            foreach (var each in quests)
            {
                writer.WriteStartObject();
                writer.WriteString("id", each.Id);
                writer.WriteString("machine", each.Machine);
                writer.WriteNumber("sequence", each.Sequence);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/{door}", body, ct).ConfigureAwait(false);

        // FABRIC-CLOSE1l, the review's finding 7: only the door's own answer settles a close, its close or its coded refusal. Anything
        // else (a proxy's page, the host's 500, a bare 404, a refusal naming no check of the door's) proves nothing about whether its
        // transaction committed, so the close is unknown and the same close is sent again, whose key answers its first outcome.
        if (root is { ValueKind: JsonValueKind.Object } answer)
        {
            if (ok && answer.TryGetProperty("quests", out var named) && named.ValueKind == JsonValueKind.Array)
            {
                return new(QuestCloseOutcomes.Closed, null, Text(answer, "message") ?? "", ReadQuests(named.GetRawText()));
            }

            if (status is 400 or 404 or 409 && Text(answer, "code") is { } code && QuestCloseOutcomes.Codes.Contains(code))
            {
                return new(QuestCloseOutcomes.Refused, code, Text(answer, "error") ?? payload, []);
            }
        }

        throw new HttpRequestException($"the close door at {_base} answered {status}, which is not its own answer, so whether it closed is unknown: "
                                       + TextBounds.Clip(payload, 200));
    }

    /// <summary>
    /// What Daoris read of a done's evidence, posted to the one door that takes it (EVID1a, EVID1b; D144 §3): the local host's
    /// <c>POST /api/quests/{id}/evidence</c>, with this client's key, as records are written. A refusal is an answer, in the
    /// service's words: the done no longer waits on evidence (409), the verdict does not read what it waits on (400), or no
    /// such quest (404); and a host older than the door is said to be.
    /// </summary>
    /// <exception cref="HttpRequestException">The host did not answer: the caller says so, and posts again at a later read.</exception>
    public async Task<EvidencePosted> EvidenceAsync(string id, EvidenceVerdict verdict, CancellationToken ct = default)
    {
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(id.TrimStart('#'))}/evidence", verdict.Json(), ct).ConfigureAwait(false);
        if (root is not { } answer)
        {
            return new(EvidencePosted.NoDoor, $"the service at {_base} has no evidence door ({status}) — is it older than this driver?");
        }

        return ok ? new(EvidencePosted.Kept, Text(answer, "message") ?? "")
            : status == (int)System.Net.HttpStatusCode.Conflict ? new(EvidencePosted.NotWaiting, Text(answer, "error") ?? payload)
            : new(EvidencePosted.Refused, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// Every session record, closed ones included, as the service answers them, with this client's key: what the session
    /// list's reader and a session delete read (SESSUX1a, SESSUX1f).
    /// </summary>
    public async Task<IReadOnlyList<SessionRecord>> SessionRecordsAsync(CancellationToken ct = default) =>
        SessionRecords.Parse(await SessionRecordsJsonAsync(ct).ConfigureAwait(false));

    /// <summary>The same records as the service wrote them, for a reader that reads the strikes and the last runs from them too.</summary>
    public Task<string> SessionRecordsJsonAsync(CancellationToken ct = default) => GetAsync(SessionRecords.Door, ct);

    /// <summary>
    /// Every quest, closed ones included, as the service wrote them: what a trace reads a quest's answers, its yes and its
    /// moments from (TRACE1), which <see cref="QuestView"/> leaves out because no planner needs them.
    /// </summary>
    public Task<string> QuestsJsonAsync(CancellationToken ct = default) => GetAsync("/api/quests?includeClosed=true", ct);

    /// <summary>
    /// One quest's moves as the service wrote them (FLOWCHART1a2, <c>GET /api/quests/{id}/moves</c>), in the order its history
    /// replays: what a trace's quest link carries. Null from a host from before the door, which answers the route not found: its
    /// path is not kept there, which is no failure (the flowchart design §7, trap 18). The door answers every id, one with no history
    /// with no moves, so its own answer is never a 404.
    /// </summary>
    /// <exception cref="DriverException">The host answered the door with a failure.</exception>
    public async Task<string?> QuestMovesJsonAsync(string id, CancellationToken ct = default)
    {
        var answer = await _doors.SendAsync(
            ServiceVerb.Get, $"/api/quests/{Uri.EscapeDataString(id.TrimStart('#'))}/moves", null, ct).ConfigureAwait(false);
        if (answer.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!answer.Ok) throw new DriverException($"the service would not read quest `#{id}`'s moves: {answer.Payload}");
        return answer.Payload;
    }

    /// <summary>
    /// Every ask, closed and done ones included, each as it stands, as the service wrote them: what the asks' pushed slice reads
    /// (PUSH1d3), which <see cref="AskView"/> leaves out because no planner needs it.
    /// </summary>
    public Task<string> AsksJsonAsync(CancellationToken ct = default) => GetAsync("/api/asks?includeClosed=true", ct);

    /// <summary>
    /// The ledger's judgement of deleting a session's record (SESSUX1f, D126 §5.4), deleting nothing: whether it would, and
    /// if not, its sentence, its word and the facts the word names.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, which answers no judgement.</exception>
    public Task<SessionDeleteAnswer> JudgeSessionDeleteAsync(string id, CancellationToken ct = default) =>
        SessionDeleteAsync(ServiceVerb.Get, $"/api/sessions/{Uri.EscapeDataString(id)}/deletable", ct);

    /// <summary>
    /// Delete a session's record (SESSUX1f): the ledger judges it again as it deletes, so a record that moved since is
    /// refused as it now stands. A refusal is an answer, not an exception.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, which answers no judgement.</exception>
    public Task<SessionDeleteAnswer> DeleteSessionAsync(string id, CancellationToken ct = default) =>
        SessionDeleteAsync(ServiceVerb.Delete, $"/api/sessions/{Uri.EscapeDataString(id)}", ct);

    private async Task<SessionDeleteAnswer> SessionDeleteAsync(ServiceVerb verb, string path, CancellationToken ct)
    {
        var answer = await _doors.SendAsync(verb, path, null, ct).ConfigureAwait(false);
        var payload = answer.Payload;
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            root = default;
        }

        // A refusal carries its word; a yes is a success that carries none. Anything else is a host older than the door.
        var refusal = root.ValueKind == JsonValueKind.Object ? Text(root, "refusal") : null;
        var taken = answer.Ok && root.ValueKind == JsonValueKind.Object && refusal is null;
        if (!taken && refusal is null)
        {
            throw new DriverException(
                $"the service at {_base} has no session delete door ({answer.Status}) — is it older than this driver?");
        }

        return new SessionDeleteAnswer(taken, Text(root, taken ? "message" : "error") ?? "")
        {
            Refusal = refusal,
            Quest = Text(root, "quest"),
            Ask = Text(root, "ask"),
            Origin = Text(root, "origin"),
            Workspace = Text(root, "workspace"),
        };
    }

    private async Task<(bool Ok, string Message)> DeleteRecordAsync(string path, CancellationToken ct)
    {
        var answer = await _doors.SendAsync(ServiceVerb.Delete, path, null, ct).ConfigureAwait(false);
        var payload = answer.Payload;
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            return answer.Ok
                ? (true, Text(root, "message") ?? "")
                : (false, Text(root, "error") ?? payload);
        }
        catch (JsonException)
        {
            // A host older than the delete door answers a bare 404 or 405 — said plainly, not parsed as nothing.
            return (false, $"the service at {_base} has no delete door ({answer.Status}) — is it older than this driver?");
        }
    }

    private async Task<AskAnswer> PostAskAsync(string path, Action<Utf8JsonWriter> write, CancellationToken ct)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        });

        var (ok, status, payload, answer) = await PostJsonAsync(path, body, ct).ConfigureAwait(false);
        // A host older than asks answers a bare 404 — said plainly rather than parsed as nothing.
        if (answer is not { } root)
        {
            return new AskAnswer(false, $"the service at {_base} has no ask door ({status}) — is it older than this driver?", null, null);
        }

        if (!ok) return new AskAnswer(false, Text(root, "error") ?? payload, null, null);

        return new AskAnswer(
            true,
            Text(root, "message") ?? "",
            root.TryGetProperty("ask", out var ask) ? Text(ask, "id") : null,
            root.TryGetProperty("quest", out var quest) && quest.ValueKind == JsonValueKind.Object ? Text(quest, "id") : null);
    }

    /// <summary>Move a session's record. The ledger judges; the driver reports what it observed.</summary>
    /// <param name="interrupted">
    /// That a stop was not the person's (D104): the orphan sweep's, or this driver's shutdown — so a take it
    /// ended is carried on. A host older than the field ignores it, and the stop reads as the person's.
    /// </param>
    /// <param name="limit">
    /// That an account's limit refused the turn of a session moved to <c>failed</c> (TOOL4d, D125 §5.2) — which is never
    /// a strike. Sent only where true, so a host older than the field reads it as any failure; the ledger refuses it on
    /// a move to any other state (TOOL4c). It names no account.
    /// </param>
    /// <param name="parts">
    /// The note's parts beside its English (LANG1a, D142 point 2), sent only with a note: a note sent without them clears the
    /// record's parts, so an older driver's note never sits beside stale ones. A host older than the field ignores it.
    /// </param>
    /// <param name="expected">
    /// The state the record must be in, which the ledger compares in its step (TERMTRUST1a, D201 point 2): a finish under the standing
    /// confirmation sends <c>awaiting-person</c>. A host older than the field ignores it, which the verb's own read guards first.
    /// </param>
    /// <param name="unbindAsk">
    /// That an intake's end lets its ask go for the next intake (LIMITRESUME1h, D206 design §3.8), in the same step, only where the ask
    /// still names it; a move to <c>failed</c> alone. Sent only where true; a host older than the field ignores it, and the ask stays
    /// served as before.
    /// </param>
    /// <param name="expect">
    /// The exact ids of the supervisor's words a go-on's move to working admits (SUPERVISOR1a-i, D208; the supervisor design §6.3): the
    /// ledger refuses the move (<c>words-changed</c>) where the record's waiting supervisor's words are not exactly these, and an absent
    /// one reads as empty. Sent on every go-on's move; null sends none.
    /// </param>
    public async Task<string> AdvanceAsync(
        string id, string state, string? note = null, string? evidence = null, string? transcript = null,
        CancellationToken ct = default, bool interrupted = false, bool limit = false, IReadOnlyList<NotePart>? parts = null,
        string? expected = null, bool unbindAsk = false, IReadOnlyList<string>? expect = null)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("state", state);
            if (note is not null) writer.WriteString("note", note);
            if (note is not null && parts is { Count: > 0 })
            {
                writer.WritePropertyName("noteParts");
                NotePart.Write(writer, parts);
            }

            if (evidence is not null) writer.WriteString("evidence", evidence);
            if (transcript is not null) writer.WriteString("transcript", transcript);
            if (interrupted) writer.WriteBoolean("interrupted", true);
            if (limit) writer.WriteBoolean("limit", true);
            if (expected is not null) writer.WriteString("expected", expected);
            if (unbindAsk) writer.WriteBoolean("unbindAsk", true);
            if (expect is not null)
            {
                writer.WriteStartArray("expect");
                foreach (var word in expect) writer.WriteStringValue(word);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        });

        var (ok, _, payload, root) = await PostJsonAsync(
            $"/api/sessions/{Uri.EscapeDataString(id)}/state", body, ct).ConfigureAwait(false);

        if (!ok)
        {
            // The refusal sentence is the contract; losing it would make the driver's log say less
            // than the service said.
            throw new DriverException(
                $"the service refused moving session `{id}` to {state}: "
                + ((root is { } refused ? Text(refused, "error") : null) ?? payload));
        }

        // TREELOCK2: the successful move's local record owns the lock, including a person's end of a park after its run returned.
        // Use the returned state, never the requested one: a refused move releases nothing, and a teammate supplies no machine path.
        if (root is { ValueKind: JsonValueKind.Object } changed && changed.TryGetProperty("session", out var record) && record.ValueKind == JsonValueKind.Object
            && !IsTeams(record) && Text(record, "id") == id)
            await SessionLocks.LetGoEndedAsync(Prior(record)).ConfigureAwait(false);
        Raise(Moved, new SessionMoved(id, (root is { } moved ? StateOf(moved) : null) ?? state));
        return root is { } answer ? Text(answer, "message") ?? "" : "";
    }

    /// <summary>Move a session's record with a composed note: its English and its parts together (LANG1a).</summary>
    public Task<string> AdvanceAsync(
        string id, string state, Noted noted, string? evidence = null, string? transcript = null,
        CancellationToken ct = default, bool interrupted = false, bool limit = false, string? expected = null,
        IReadOnlyList<string>? expect = null) =>
        AdvanceAsync(id, state, noted.Note, evidence, transcript, ct, interrupted, limit, noted.Parts, expected, expect: expect);

    /// <summary>
    /// Put a park the driver took up and could not hand back to waiting on the owner (SUPERVISOR1a-i, D208; the supervisor design §6.3,
    /// <c>RestoreParkAsync</c>), keeping every waiting word but <paramref name="takeBack"/>, the supervisor's word it admitted, with
    /// the note the look read. Refused where the record is not the working one a go-on took up; a refusal throws as a move's does.
    /// </summary>
    public async Task RestoreParkAsync(string id, IReadOnlyList<string> takeBack, Noted? noted, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("takeBack");
            foreach (var word in takeBack) writer.WriteStringValue(word);
            writer.WriteEndArray();
            if (noted is { } note)
            {
                writer.WriteString("note", note.Note);
                if (note.Parts is { Count: > 0 })
                {
                    writer.WritePropertyName("noteParts");
                    NotePart.Write(writer, note.Parts);
                }
            }

            writer.WriteEndObject();
        });
        var (ok, _, payload, root) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(id)}/restore", body, ct).ConfigureAwait(false);
        if (!ok)
        {
            throw new DriverException(
                $"the service refused restoring session `{id}`'s park: " + ((root is { } refused ? Text(refused, "error") : null) ?? payload));
        }

        Raise(Moved, new SessionMoved(id, "awaiting-person"));
    }

    /// <summary>The state an answer's session record is in now, or null when it carries none.</summary>
    private static string? StateOf(JsonElement answer) =>
        answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var session) ? Text(session, "state") : null;

    /// <summary>
    /// Tell the watchers. A watcher that fails costs its own view, never the call: the ledger has already
    /// moved, and the caller is owed its answer (LOG1b).
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

    // A refused read throws with the service's own sentence (IServiceDoors.GetAsync): a bare
    // EnsureSuccessStatusCode threw it away, and the driver's log said less than the service said.
    private Task<string> GetAsync(string path, CancellationToken ct) => _doors.GetAsync(path, ct);

    internal static IReadOnlyList<QuestView> ReadQuests(string json)
    {
        using var document = JsonDocument.Parse(json);
        var quests = new List<QuestView>();
        foreach (var quest in document.RootElement.EnumerateArray())
        {
            quests.Add(new QuestView(
                Text(quest, "id") ?? "",
                Text(quest, "from") ?? "",
                Text(quest, "to") ?? "",
                Text(quest, "title") ?? "",
                Text(quest, "body") ?? "",
                Text(quest, "status") ?? "")
            {
                // Absent is nothing carried: a service from before D65 answers without either.
                Links = quest.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array
                    ? links.EnumerateArray().Select(l => l.GetString()).OfType<string>().ToList()
                    : [],
                Attachments = quest.TryGetProperty("attachments", out var files) && files.ValueKind == JsonValueKind.Array
                    ? files.EnumerateArray().Select(file => new QuestFileView(
                        Text(file, "name") ?? "",
                        Text(file, "sha256") ?? "",
                        file.TryGetProperty("bytes", out var bytes) && bytes.ValueKind == JsonValueKind.Number ? bytes.GetInt64() : 0,
                        // The service answers a path only to this machine, and only when the bytes are here.
                        Text(file, "path"))).ToList()
                    : [],
                Then = quest.TryGetProperty("then", out var then) && then.ValueKind == JsonValueKind.Array
                    ? then.EnumerateArray().Select(step => new QuestStepView(
                        Text(step, "to") ?? "", Text(step, "title") ?? "", Text(step, "body") ?? "")
                        {
                            // A set-up step's environment (REVIEWENV1b). Absent on every other step, and from a host before it.
                            SetUpIn = Text(step, "setUpIn"),
                        }).ToList()
                    : [],
                Parent = Text(quest, "parent"),
                // Who holds the quest and this machine's id where it is the page's (SYNCMERGE1b1). Absent is none: a quest no press settled.
                Holder = Text(quest, "holder") is { Length: > 0 } held ? held : null,
                Here = Text(quest, "here") is { Length: > 0 } here ? here : null,
                // What a list calls it (SESSUX1j). Absent is a host from before the field: the title names it.
                Short = Text(quest, "short") is { Length: > 0 } named ? named : null,
                // Absent is not waiting: a host from before D79 answers without it.
                Awaits = Text(quest, "awaits"),
                // Absent is a person's publish or a chain's step: a host from before SESS1 answers without it.
                PublishedBy = Text(quest, "publishedBy") is { Length: > 0 } by ? by : null,
                Note = Text(quest, "note"),
                NoteParts = NotePart.Read(quest),
                Workspace = RemoteTarget.Workspace(Text(quest, "workspace")),
                Deletable = Flag(quest, "deletable"),
                // The lanes of `to` it addresses (D115 §2.2). Absent is none: the whole repository.
                Lanes = quest.TryGetProperty("lanes", out var lanes) && lanes.ValueKind == JsonValueKind.Array
                    ? lanes.EnumerateArray().Select(l => l.ValueKind == JsonValueKind.String ? l.GetString() : null).OfType<string>().ToList()
                    : [],
                // What the person requires (DRIFT1c), in the service's order, which a done answers by number (DRIFT1d), so
                // each keeps its place. Absent is none: a host from before requirements.
                Requirements = quest.TryGetProperty("requirements", out var required) && required.ValueKind == JsonValueKind.Array
                    ? required.EnumerateArray()
                        .Select(r => new QuestRequirementView(Text(r, "quote") ?? "", Text(r, "check") ?? "") { Evidence = EvidenceOf(r) })
                        .ToList()
                    : [],
                // Whether a done is held for the person's yes (DRIFT1d) or its evidence (EVID1a). Absent is false: a host from before.
                Held = Flag(quest, "held"),
                // Why, and whether its done waits on evidence, and what was last read of it (EVID1a, D144 §3, §6). Absent is
                // none: a host from before evidence, whose one hold was a departure.
                Hold = Text(quest, "hold") is { Length: > 0 } hold ? hold : null,
                AwaitsEvidence = Flag(quest, "awaitsEvidence"),
                Evidence = EvidenceVerdict.Read(quest),
                // How its done answered each requirement (DRIFT1d), which an accept shows before the yes (DRIFT1d2). Absent
                // is none: a quest no done answered, or a host from before answers. A number left out reads as 0, no requirement's.
                Answers = quest.TryGetProperty("answers", out var answered) && answered.ValueKind == JsonValueKind.Array
                    ? answered.EnumerateArray()
                        .Select(a => new QuestAnswerView(
                            a.TryGetProperty("requirement", out var number) && number.ValueKind == JsonValueKind.Number ? number.GetInt32() : 0,
                            Text(a, "met"), Text(a, "departed"), Text(a, "quote")))
                        .ToList()
                    : [],
                // When its status last moved: a taken quest's take, compared with its last session's end (CARRY2b).
                Updated = Moment(quest, "updated"),
                // The review on the record (REVIEWENV1b): each absent where there is none, and from a host before them.
                Review = ReadReviewChoice(quest),
                SetUpIn = Text(quest, "setUpIn"),
                SetUps = ReadSetUps(quest),
                Verdicts = ReadVerdicts(quest),
                // FABRIC-CLOSE1a: the graph that closes it and its taker's current submission. Absent for a quest no closure graph holds,
                // and from a host before them; a submission named only in half is none.
                Closure = Text(quest, "closure") is { Length: > 0 } closure ? closure : null,
                Submission = ReadSubmission(quest),
                // FABRIC-CLOSE1c, read for FABRIC-WORKFLOW2d's seal: the submission a step grew from and the reopen standing on it. Absent
                // from a step a done published and from a host before them; one named in half is none.
                GrewFrom = ReadSubmissionRef(quest, "grewFrom"),
                SourceReopened = ReadSubmissionRef(quest, "sourceReopened"),
                // FABRIC-CLOSE1c4, read for FABRIC-CLOSE1c8: the source's own reopen no later submission answered. Absent once one did,
                // and from a host before it; one named in half is none.
                Reopened = ReadReopen(quest),
                // WORKFLOW1e2: the person's kind and workflow on a quest no ask published, in an ask's shape. Absent is none.
                WorkflowChoices = ReadWorkflowChoices(quest),
            });
        }

        return quests;
    }

    /// <summary>
    /// A requirement's evidence (EVID1a), each item exactly one of a path or a gate; an item naming both or neither is passed
    /// over, as half of a fact is none. Empty, never a list of its own, where it names none.
    /// </summary>
    internal static IReadOnlyList<QuestEvidenceItem> EvidenceOf(JsonElement requirement)
    {
        if (requirement.ValueKind != JsonValueKind.Object || !requirement.TryGetProperty("evidence", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var read = items.EnumerateArray()
            .Select(item => new QuestEvidenceItem(Text(item, "path"), Text(item, "gate")))
            .Where(item => (item.Path is null) != (item.Gate is null))
            .ToList();
        return read.Count == 0 ? [] : read;
    }

    private static IReadOnlyList<RepoView> ReadRegistry(string json)
    {
        using var document = JsonDocument.Parse(json);
        var repositories = new List<RepoView>();
        foreach (var repo in document.RootElement.EnumerateArray())
        {
            repositories.Add(new RepoView(
                Text(repo, "repository") ?? "",
                repo.TryGetProperty("adopted", out var adopted) && adopted.ValueKind == JsonValueKind.True,
                Text(repo, "root"),
                // The machine's own wiring (D48 §2). An older host that does not answer it leaves the
                // default, which is what an unwired machine has always meant.
                RemoteTarget.Workspace(Text(repo, "workspace"))));
        }

        return repositories;
    }

    /// <summary>
    /// Where one session ran and what it started from — the two machine-local facts a review needs
    /// (SURF6).
    /// </summary>
    /// <remarks>
    /// <para>Both come back only over loopback, which is the point: the tree is a filesystem path and the
    /// base is useless without the checkout it names. A caller on another machine reads nulls and has
    /// nothing to diff, which is the honest answer rather than a refusal.</para>
    ///
    /// <para>🔴 <b>Closed records too</b> (GROUND1): <c>/api/sessions</c> alone means ACTIVE, and a session
    /// is reviewed, merged or discarded once it has ended — which read here as a session with no tree.</para>
    /// </remarks>
    /// <summary>The quest a session serves, or null for a conversation's or an intake's (WSR1: what a landing names).</summary>
    public async Task<string?> SessionQuestAsync(string id, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(
            await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));

        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (Text(session, "id") == id) return Text(session, "quest") is { Length: > 0 } quest ? quest : null;
        }

        return null;
    }

    public async Task<(string? Tree, string? BaseCommit)> SessionGroundAsync(
        string id, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(
            await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));

        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (Text(session, "id") == id)
            {
                return (Text(session, "tree"), Text(session, "baseCommit"));
            }
        }

        return (null, null);
    }

    /// <remarks>
    /// THIS machine's sessions only. A teammate's record came down with the sync keyed `origin/id` — the
    /// same rule the platform reads it by — and it holds nothing here (D47 §6, SYNC4): counted, it would
    /// spend this machine's cap on work another machine is doing, block a repository whose tree that
    /// machine holds and this one does not, and park-notify for a session nobody here can reach.
    /// </remarks>
    internal static IReadOnlyList<SessionView> ReadSessions(string json)
    {
        using var document = JsonDocument.Parse(json);
        var sessions = new List<SessionView>();
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (IsTeams(session)) continue;
            sessions.Add(new SessionView(
                Text(session, "id") ?? "",
                Text(session, "repository") ?? "",
                // Read since SURF5b: a park is a state change nothing here performs, so the only way
                // to see one is to look. Defaulted rather than required, because a service older
                // than this field answers without it and the planner never needed it either way.
                Text(session, "state") ?? "",
                Text(session, "note"))
            {
                // An intake's ask (D65 §1b) — how a parked one is ended when the person answers it.
                Ask = Text(session, "ask"),
                // The tree it holds — the lock where a repository opens a tree per session (PAR1).
                Tree = Text(session, "tree"),
                // The quest it serves, which it holds while it works (DEV3).
                Quest = Text(session, "quest"),
                // Where its tree stood when it opened (SURF6): what the sweep reads a lost done's evidence as changed from (EVID1b).
                BaseCommit = Text(session, "baseCommit") is { Length: > 0 } baseCommit ? baseCommit : null,
                // Its note's lines by code (LANG1a), handed on wherever the note is; null for a record from before parts.
                NoteParts = NotePart.Read(session),
                // The person's answer to its park (ENTRY1f2), read as SessionRecords reads it: a blank one is none.
                Answer = Text(session, "answer") is { Length: > 0 } answer ? answer : null,
                // Its workspace (ASKSCOPE1a), as SessionRecords reads it: none from a host from before is `default`.
                Workspace = RemoteTarget.Workspace(Text(session, "workspace")),
                // What the cap counts by (CAPCOUNT1): its kind, and the mark of each conversation the driver opens for itself, an
                // intake's ask above, a helper's serves and a reviewer's opinion. A conversation the person holds has none of them.
                Kind = Text(session, "kind") ?? ActiveSessions.DrivenKind,
                Opinion = Text(session, "opinion") is { Length: > 0 } opinion ? opinion : null,
                Serves = Text(session, "serves") is { Length: > 0 } serves ? serves : null,
                // A partner's piece (COAGENT1b): its reservation is counted once its record opens.
                Piece = Text(session, "piece") is { Length: > 0 } piece ? piece : null,
            });
        }

        return sessions;
    }

    /// <summary>
    /// This machine's active sessions as the downloads' rule reads them (KEEP1b, D195 point 6; the cleanup design §3.5): each id, when its
    /// record was opened, and the downloaded versions its starts were handed (<c>resolved</c>, answered on loopback alone), null for a record
    /// from before that field, which keeps the start-time rule. A teammate's record runs nothing here.
    /// </summary>
    public async Task<IReadOnlyList<LiveSession>> LiveSessionsAsync(CancellationToken ct = default) =>
        ReadRunning(await GetAsync("/api/sessions", ct).ConfigureAwait(false));

    /// <summary><see cref="LiveSessionsAsync"/>'s read of an answer.</summary>
    internal static IReadOnlyList<LiveSession> ReadRunning(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray()
            .Where(session => !IsTeams(session))
            .Select(session => new LiveSession(
                Text(session, "id") ?? "", Moment(session, "created"),
                session.TryGetProperty("resolved", out var resolved) && resolved.ValueKind == JsonValueKind.Array
                    ? [.. resolved.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
                    : null))];
    }

    /// <summary>
    /// How often each quest has been failed, from the records themselves (DRV6).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Only <c>failed</c> counts — and a stop that was not the person's</b> (D104), which is a
    /// cut-off by the sweep or a shutdown. A <c>stood-down</c> session means somebody else took the
    /// quest first — the race resolving as designed, not a failure. A <c>declined</c> one is a real
    /// answer and closes the quest anyway. A <c>stopped</c> one the person stopped was theirs. Counting
    /// any of those would park quests for succeeding.
    /// <para>🔴 <b>A failure that says <c>limit</c> is not a strike either</b> (TOOL4d, D125 §5.2, amending D58). A strike
    /// says trying again spends an account without progress; a limit is the account's state, with its own reset, and
    /// the cool-off bounds it. Counted, three refusals by one spent account parked a healthy quest in seconds on
    /// 1 October.</para>
    /// <para>🔴 <b>Nor is a failure whose account was refused</b> (ROSTER1b): its agent refused the start for its sign-in, or
    /// its provider refused its credential (AGT3b), which its note says by an account's line (<see cref="NoteCodes.AccountsOwn"/>).
    /// The account reads signed out and the next start walks past it; counted, four starts on one empty account parked a
    /// healthy quest on the install in two minutes (2026-10-04).</para>
    /// </remarks>
    internal static IReadOnlyDictionary<string, int> ReadStrikes(string json)
    {
        using var document = JsonDocument.Parse(json);
        var strikes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in document.RootElement.EnumerateArray())
        {
            // A strike is this driver's own judgement of its own attempts: a teammate's failure there
            // says nothing about whether THIS machine's next try would fail.
            if (IsTeams(session)) continue;
            var state = Text(session, "state");
            var cutOff = (string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase) && !Limit(session) && !AccountRefused(session))
                         || (string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase) && Interrupted(session));
            if (NotePart.Read(session)?.Any(part => part.Code == NoteCodes.UpdateClosed.Code) == true) continue;
            // AGENTERRORS1a (D210 §5.1): an end past a transient bound is its provider's outage, never the quest's: no strike, its
            // quest held for the person instead.
            if (NotePart.Read(session)?.Any(part => part.Code == NoteCodes.EndedTransient.Code) == true) continue;
            // D205 §3.5(c): a choice its agent refused at the open, before any prompt, is the account's, never the quest's.
            if (NotOffered(session)) continue;
            if (!cutOff) continue;
            if (Text(session, "quest") is not { Length: > 0 } quest) continue;

            strikes[quest] = strikes.TryGetValue(quest, out var seen) ? seen + 1 : 1;
        }

        return strikes;
    }

    /// <summary>
    /// The session this machine last ran on each quest, and where (D79) — from the records, as the
    /// strikes are.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>A stand-down is not a run.</b> It means somebody else had the quest, so a later wait on it is
    /// theirs — resuming it here from a stand-down would double another taker's work.
    /// </remarks>
    internal static IReadOnlyDictionary<string, PriorSession> ReadLastRun(string json)
    {
        using var document = JsonDocument.Parse(json);
        var last = new Dictionary<string, (PriorSession Session, DateTimeOffset At)>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (IsTeams(session)) continue;
            if (Text(session, "quest") is not { Length: > 0 } quest) continue;
            if (string.Equals(Text(session, "state"), "stood-down", StringComparison.OrdinalIgnoreCase)) continue;
            // D205 §3.5(c): a record its agent's refusal of the choice ended before any prompt ran nothing, so a carry-on carries on from
            // the record before it, never from the empty one.
            if (NotOffered(session)) continue;

            var at = Moments.Read(Text(session, "created")) ?? DateTimeOffset.MinValue;
            if (last.TryGetValue(quest, out var seen) && seen.At > at) continue;
            last[quest] = (Prior(session), at);
        }

        return last.ToDictionary(pair => pair.Key, pair => pair.Value.Session, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The newest record of this machine's on each quest where it stood down (SYNCMERGE1b; <see cref="Snapshot.StoodDown"/>): newer, by
    /// that alone, than the quest's <see cref="ReadLastRun"/>, which skips it. The newest by when it was made, a later one in the answer
    /// on a tie, as the ledger orders them (its last own record, which it carries a bring-forward on from).
    /// </summary>
    /// <remarks>
    /// Read only so a look asks the claim of those quests: a stand-down carries nothing on unless the person's bring-forward made the take
    /// the quest stands by (<see cref="Planner.BroughtForward"/>), which the remark on <see cref="ReadLastRun"/> still holds for every other.
    /// </remarks>
    internal static IReadOnlyDictionary<string, PriorSession> ReadStoodDown(string json)
    {
        using var document = JsonDocument.Parse(json);
        var newest = new Dictionary<string, (JsonElement Session, DateTimeOffset At)>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || IsTeams(session)) continue;
            if (Text(session, "quest") is not { Length: > 0 } quest) continue;

            var at = Moments.Read(Text(session, "created")) ?? DateTimeOffset.MinValue;
            if (newest.TryGetValue(quest, out var seen) && seen.At > at) continue;
            newest[quest] = (session, at);
        }

        return newest
            .Where(pair => string.Equals(Text(pair.Value.Session, "state"), "stood-down", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => Prior(pair.Value.Session), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The record each taken quest's standing reopen names (FABRIC-CLOSE1c9; <see cref="Snapshot.NamedByReopen"/>), where it is
    /// this machine's and not the quest's last record here (<paramref name="lastRun"/>), from the same records.
    /// </summary>
    /// <remarks>Nothing is parsed where no taken quest's reopen names such a session, which is every look of an unbound install.</remarks>
    internal static IReadOnlyDictionary<string, PriorSession> ReadNamedByReopen(
        string json, IReadOnlyList<QuestView> quests, IReadOnlyDictionary<string, PriorSession> lastRun)
    {
        var named = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase);
        var wanted = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var quest in quests)
        {
            // A teammate's session (origin/id) is never this machine's to go on with; the last one here is the planner's already.
            if (quest is not { Status: "Taken", Reopened.Session: { Length: > 0 } session } || session.Contains('/')
                || string.Equals(lastRun.GetValueOrDefault(quest.Id)?.Session, session, StringComparison.Ordinal))
            {
                continue;
            }

            if (!wanted.TryGetValue(session, out var naming)) wanted[session] = naming = [];
            naming.Add(quest.Id);
        }

        if (wanted.Count == 0) return named;

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return named;
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || IsTeams(session)) continue;
            if (Text(session, "id") is not { } id || !wanted.TryGetValue(id, out var naming)) continue;

            var record = Prior(session);
            foreach (var quest in naming) named[quest] = record;
        }

        return named;
    }

    /// <summary>
    /// Whose each take is (CARRY2b), for the quests whose claim was read, from the records as the ledger reads them (CARRY2):
    /// whether a record of this machine's, in any state, marks that it took the quest, and the newest teammate's record on it
    /// that did not stand down, which names another machine's take. A teammate's mark is never this machine's.
    /// </summary>
    /// <param name="claims">This machine's claim on each quest read, by quest id (<see cref="ClaimAsync"/>).</param>
    /// <param name="whys">Why a lost claim lost, by quest id (<see cref="ClaimStandingAsync"/>), where the host said.</param>
    /// <param name="forwards">The bring-forward each take was made by, by quest id (<see cref="ClaimTakeAsync"/>), where the host named one.</param>
    /// <param name="taken">When each take was made, by quest id (<see cref="ClaimTakeAsync"/>, CARRY3), where the host said.</param>
    internal static IReadOnlyDictionary<string, QuestTake> ReadTakes(
        string json, IReadOnlyDictionary<string, string> claims, IReadOnlyDictionary<string, string?>? whys = null,
        IReadOnlyDictionary<string, ForwardTake?>? forwards = null, IReadOnlyDictionary<string, DateTimeOffset?>? taken = null)
    {
        var took = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var teammates = new Dictionary<string, (string Id, DateTimeOffset At)>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || Text(session, "quest") is not { Length: > 0 } quest) continue;
            if (!IsTeams(session))
            {
                if (Flag(session, "took")) took.Add(quest);
                continue;
            }

            if (string.Equals(Text(session, "state"), "stood-down", StringComparison.OrdinalIgnoreCase)) continue;
            // The newest by when it was made, a later one in the answer on a tie, as the ledger orders them.
            var at = Moment(session, "created") ?? DateTimeOffset.MinValue;
            if (teammates.TryGetValue(quest, out var seen) && seen.At > at) continue;
            teammates[quest] = (Text(session, "id")!, at);
        }

        return claims.ToDictionary(
            pair => pair.Key,
            pair => new QuestTake(pair.Value, took.Contains(pair.Key), teammates.TryGetValue(pair.Key, out var teammate) ? teammate.Id : null,
                whys is not null && whys.TryGetValue(pair.Key, out var why) ? why : null,
                forwards?.GetValueOrDefault(pair.Key), taken?.GetValueOrDefault(pair.Key)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// D205 §9.3: a record's provenance after its open, through the loopback-only later door, with no revision. A record this
    /// machine no longer has is no failure; any other refusal throws.
    /// </summary>
    public async Task KeepActorAsync(string id, string? choice = null, ActorAsked? asked = null, ActorRan? ran = null,
        object? changed = null, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            if (choice is not null) writer.WriteString("choice", choice);
            if (asked is not null) { writer.WritePropertyName("asked"); JsonSerializer.Serialize(writer, asked, ActorWire.Json); }
            if (ran is not null) { writer.WritePropertyName("ran"); JsonSerializer.Serialize(writer, ran, ActorWire.Json); }
            if (changed is not null) { writer.WritePropertyName("override"); JsonSerializer.Serialize(writer, changed, ActorWire.Json); }
            writer.WriteEndObject();
        });
        var (ok, status, payload, _) = await PostJsonAsync($"/api/sessions/{Uri.EscapeDataString(id)}/actor", body, ct).ConfigureAwait(false);
        if (ok) Raise(ActorKept, id);
        else if (status != 404) throw new DriverException($"the service refused actor provenance: {payload}");
    }

    /// <summary>
    /// An intake refused its choice at the open (D205 §3.5(f), MODELROLE1d): one write ends its record <paramref name="conclusion"/>
    /// (<c>failed</c>, <c>ended.choice-not-offered</c>) and lets its ask go, only where the ask's binding still names
    /// <paramref name="session"/>; the record stays, naming its ask, so the ask's history keeps its line.
    /// </summary>
    public Task EndRefusedIntakeAsync(string session, SessionConclusion conclusion, CancellationToken ct = default) =>
        AdvanceAsync(session, conclusion.State, note: conclusion.Note, ct: ct, parts: conclusion.Parts, unbindAsk: true);

    /// <summary>
    /// <see cref="KeepActorAsync"/> where a start or a composer change keeps it: reporting never changes what runs (D205 row b),
    /// so a refusal or an unreachable service is logged and the run goes on. A cancellation still cancels.
    /// </summary>
    public async Task<bool> TryKeepActorAsync(string id, MachineLog? log, string? choice = null, ActorAsked? asked = null,
        object? changed = null, CancellationToken ct = default)
    {
        try
        {
            await KeepActorAsync(id, choice, asked, changed: changed, ct: ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log?.Write("warn", "actor.keep-failed", [("session", id)]);
            return false;
        }
    }

    /// <summary>
    /// One record as a run that goes on reads it (MSG1c): its state, its words waiting, the adapter, account, tree and
    /// version it ran on, its note and its flags, and its kind. Null when no record of this machine has that id.
    /// </summary>
    public async Task<PriorSession?> RecordAsync(string id, CancellationToken ct = default) =>
        ReadRecord(await GetAsync(SessionRecords.Door, ct).ConfigureAwait(false), id);

    /// <summary>
    /// The trees of this machine's records the person's words wait on (AUTOTIDY2d, MSG1b, D137 §2.2): a record that ended or
    /// parked with words a later run goes on with in its own tree, which the look's own tidy keeps as it keeps a session's in use.
    /// </summary>
    public async Task<IReadOnlyList<string>> WaitingTreesAsync(CancellationToken ct = default) =>
        WaitingTrees(await GetAsync(SessionRecords.Door, ct).ConfigureAwait(false));

    /// <summary>
    /// This machine's records, open and closed, by id (<see cref="ReadRecords"/>): what the person's <i>Send to its session</i> finds a
    /// line of work's newest part here among (COWORK1d). A teammate's is none here.
    /// </summary>
    public async Task<IReadOnlyCollection<PriorSession>> RecordsAsync(CancellationToken ct = default) =>
        [.. ReadRecords(await GetAsync(SessionRecords.Door, ct).ConfigureAwait(false)).Values];

    /// <summary><see cref="WaitingTreesAsync"/>'s trees among the answer's records; a teammate's is none here.</summary>
    internal static IReadOnlyList<string> WaitingTrees(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return [];
        var trees = new List<string>();
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || IsTeams(session)) continue;
            if (Prior(session) is { WordsWaiting: true, Tree: { Length: > 0 } tree }) trees.Add(tree);
        }

        return trees;
    }

    /// <summary>The record with this id among the answer's, read as <see cref="RecordAsync"/> reads it; a teammate's is none here.</summary>
    internal static PriorSession? ReadRecord(string json, string id)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || IsTeams(session)) continue;
            if (string.Equals(Text(session, "id"), id, StringComparison.Ordinal)) return Prior(session);
        }

        return null;
    }

    /// <summary>
    /// This machine's records among the answer's, by id, read as <see cref="ReadRecord"/> reads one: what a brief follows a line's
    /// <c>from</c> links through (SESSCTX1b). A teammate's is none here.
    /// </summary>
    internal static IReadOnlyDictionary<string, PriorSession> ReadRecords(string json)
    {
        using var document = JsonDocument.Parse(json);
        var records = new Dictionary<string, PriorSession>(StringComparer.Ordinal);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return records;
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind != JsonValueKind.Object || IsTeams(session) || Text(session, "id") is not { Length: > 0 } id) continue;
            records.TryAdd(id, Prior(session));
        }

        return records;
    }

    /// <summary>A record as what a later run on it is compared with and told (D79, D80, ANSWER1a, MSG1b).</summary>
    private static PriorSession Prior(JsonElement session) =>
        new(
            Text(session, "id") ?? "", Text(session, "tree"), Text(session, "state") ?? "", Text(session, "note"),
            Text(session, "repository"), Text(session, "answer"), Interrupted(session))
        {
            // Which account it ran on and whether a limit cut it off (TOOL4f): what a carry-on is compared with and told.
            Profile = Text(session, "profile"),
            Actor = Text(session, "actor"),
            Choice = Text(session, "choice"),
            Asked = ActorRead<ActorAsked>(session, "asked"),
            Override = ActorRead<JsonElement?>(session, "override"),
            Ran = ActorRead<ActorRan>(session, "ran"),
            Limit = Limit(session),
            NotOffered = NotOffered(session),
            // What an answered park is compared with, and where its evidence counts from (ANSWER1a, D131).
            Adapter = Text(session, "adapter"),
            HarnessVersion = Text(session, "harnessVersion"),
            BaseCommit = Text(session, "baseCommit"),
            // The person's words waiting on it (MSG1a): what goes on with it, parked or ended (MSG1b, D137 §2.2).
            Said = ReadSaid(session),
            Ask = Text(session, "ask"),
            // The work its marks read as (FOUNDATION1c, D212): served on loopback, none for a mirror and from a host before the field.
            Work = Text(session, "work") is { Length: > 0 } work ? work : null,
            // What a helper may be asked of, and where its rule resolves (MIXAGENT2a-ii): served on loopback with the record.
            Serves = Text(session, "serves"),
            // A partner's piece (COAGENT1b, the co-agent design §7.3): served on loopback beside its serves.
            Piece = Text(session, "piece") is { Length: > 0 } piece ? piece : null,
            Opinion = Text(session, "opinion"),
            // The session it continues, why and who asked (MIXAGENT1a-iii): served on loopback with the record, for the head to say.
            From = Text(session, "from"),
            FromWhy = Text(session, "fromWhy"),
            FromBy = Text(session, "fromBy"),
            Workspace = Text(session, "workspace"),
            // A chat goes on through the chat runner, never the planner (MSG1c).
            Kind = Text(session, "kind") ?? "driven",
            // Its note's lines by code (LANG1a), which a note added to it carries on; null for a record from before parts.
            NoteParts = NotePart.Read(session),
            // When it last moved, which a take here is compared with (CARRY2b).
            Updated = Moment(session, "updated"),
            // When it was made, which a curve move's hold is counted from (SWITCH1c-ii).
            Created = Moment(session, "created"),
            // Its session entry and its quest (SESSONE1a): what a later quest of the same line goes into its tree by (SESSONE1d).
            Entry = Text(session, "entry") is { Length: > 0 } entry ? entry : null,
            Quest = Text(session, "quest") is { Length: > 0 } quest ? quest : null,
            // The mode an Ask Daoris record last ran under (HELPMODE1a2), which a conversation going on is compared with.
            HelpMode = Text(session, "helpMode") is { Length: > 0 } mode ? mode : null,
            // The setup a driven record started under (AUTONOMY1a), which a run going on from it compares its own with: its permission
            // prompts (AUTONOMY1b) and its go-aheads (AUTONOMY1c).
            Access = AccessOf(session),
        };

    internal static T? ActorRead<T>(JsonElement session, string name)
    {
        if (!session.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object) return default;
        try { return JsonSerializer.Deserialize<T>(value.GetRawText(), ActorWire.Json); }
        catch (JsonException) { return default; }
    }

    /// <summary>
    /// A record's access setup as the service answers it on loopback (AUTONOMY1a): each pause that is a word, read as a stored shape;
    /// null where the record answers none, a record from before the setting.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? AccessOf(JsonElement session)
    {
        if (!session.TryGetProperty("access", out var access) || access.ValueKind != JsonValueKind.Object) return null;
        var pauses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pause in access.EnumerateObject())
        {
            if (pause.Value.ValueKind == JsonValueKind.String && pause.Value.GetString() is { Length: > 0 } value) pauses[pause.Name] = value;
        }

        return pauses;
    }

    /// <summary>
    /// Every record's entry key by its id (SESSONE1d), as the service answered it: this machine's and a teammate's mirror, keyed
    /// <c>origin/id</c>, since a quest's publisher may be either. A record from a host before the key answers none, and is absent.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ReadEntries(string json)
    {
        using var document = JsonDocument.Parse(json);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return entries;
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (session.ValueKind == JsonValueKind.Object && Text(session, "id") is { Length: > 0 } id && Text(session, "entry") is { Length: > 0 } entry)
            {
                entries[id] = entry;
            }
        }

        return entries;
    }

    /// <summary>
    /// The words waiting on a record (MSG1a, D137 §2.4), one by one, read without trusting the shape: a word without its id,
    /// its words or a moment is skipped, a file's name that is not text is none. Null where the record answers no list, a
    /// host from before MSG1a, which is not the same as nothing waiting.
    /// </summary>
    private static IReadOnlyList<SaidWordView>? ReadSaid(JsonElement session)
    {
        if (!session.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array) return null;
        return [.. said.EnumerateArray().Select(ReadWord).OfType<SaidWordView>()];
    }

    /// <summary>
    /// One word as the service answers it (MSG1a): its id, its words, when, its files' names and whether it was said after
    /// the record ended; and the second opinion whose findings it carries (XAGENT1c's <c>by</c>), absent for the person's.
    /// Null for one without its id, its words or a moment that reads.
    /// </summary>
    internal static SaidWordView? ReadWord(JsonElement word) =>
        word.ValueKind == JsonValueKind.Object
        && Text(word, "id") is { Length: > 0 } id && Text(word, "text") is { } words && Moment(word, "at") is { } at
            ? new SaidWordView(id, words, at, Strings(word, "files"), Flag(word, "reopens"))
            {
                By = Text(word, "by") is { Length: > 0 } by ? by : null,
                // A helper's answer is its own mark, never `by` (MIXAGENT2a-i): the person's words are neither.
                Helper = Text(word, "helper") is { Length: > 0 } helper ? helper : null,
                // COWORK1b: Daoris's own words, and the producer's key; a host from before says neither, and its word is the person's.
                From = Text(word, "from") is { Length: > 0 } from ? from : null,
                Key = Text(word, "key") is { Length: > 0 } key ? key : null,
                // COWORK1c: its level, and when a dispatcher first wrote it to a harness; a host from before says neither.
                Level = MessageLevels.Read(Text(word, "level")),
                Attempt = Moment(word, "attempt"),
                // SUPERVISOR1a-i: the decision a supervisor's word claims; a host from before says none.
                Decision = Text(word, "decision") is { Length: > 0 } decision ? decision : null,
            }
            : null;

    /// <summary>
    /// The closed quests whose last session here has the person's words waiting (MSG1b, D137 §2.2), read from every quest
    /// the service holds: an open one is planned from the open list, and one with nothing waiting is not looked at.
    /// </summary>
    /// <param name="open">The ids of the quests the open list holds.</param>
    internal static IReadOnlyList<QuestView> ReadClosed(
        string json, IReadOnlyDictionary<string, PriorSession> lastRun, IReadOnlyCollection<string> open)
    {
        var listed = new HashSet<string>(open, StringComparer.OrdinalIgnoreCase);
        return
        [
            .. ReadQuests(json).Where(quest =>
                !listed.Contains(quest.Id)
                && lastRun.TryGetValue(quest.Id, out var last)
                && last.WordsWaiting),
        ];
    }

    /// <summary>
    /// This machine's records as the goal's walk counts them (TOOL6b, D130 §4.2): each one's adapter, account, when it was
    /// opened, and whether it runs now, which is the active list's word, never re-derived here (as the strikes keep it).
    /// </summary>
    /// <remarks>A teammate's record holds nothing here (SYNC4), and a record naming no adapter names no owner to count it for.</remarks>
    internal static IReadOnlyList<SessionStarted> ReadStarted(string json, IReadOnlyList<SessionView> active)
    {
        var running = active.Select(session => session.Id).ToHashSet(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(json);
        var started = new List<SessionStarted>();
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (IsTeams(session) || Text(session, "adapter") is not { Length: > 0 } adapter) continue;
            started.Add(new SessionStarted(
                adapter,
                Text(session, "profile"),
                Moments.Read(Text(session, "created")),
                running.Contains(Text(session, "id") ?? ""))
            {
                Id = Text(session, "id"),
                // D205 §3.5(c): a record its agent's refusal of the choice ended at the open, read by its end code.
                NotOffered = NotOffered(session),
            });
        }

        return started;
    }

    /// <summary>Whether a record says its stop was not the person's (D104) — absent is false, the old reading.</summary>
    private static bool Interrupted(JsonElement session) =>
        session.TryGetProperty("interrupted", out var interrupted) && interrupted.ValueKind == JsonValueKind.True;

    /// <summary>Whether a record says an account's limit made its failure (TOOL4c) — absent is false, the old reading.</summary>
    private static bool Limit(JsonElement session) =>
        session.TryGetProperty("limit", out var limit) && limit.ValueKind == JsonValueKind.True;

    /// <summary>
    /// Whether a record's note says its account was refused, its sign-in or its credential (ROSTER1b): by an account's line's
    /// code, never by its words; a record from before parts is false, the old reading.
    /// </summary>
    private static bool AccountRefused(JsonElement session) =>
        NotePart.Read(session) is { } parts
        && parts.Any(part => part.Code is { } code && NoteCodes.AccountsOwn.Any(own => own.Code == code));

    /// <summary>
    /// Whether a record ended <c>choice-not-offered</c> (D205 §3.5(c), MODELROLE1c2), read by its end code: its agent refused the choice at
    /// the open, before any prompt, so it is no strike, no last run and no start the walk counts.
    /// </summary>
    internal static bool NotOffered(JsonElement session) =>
        NotePart.Read(session)?.Any(part => part.Code == NoteCodes.EndedChoiceNotOffered.Code) == true;

    /// <summary>A record that came down from the team — keyed `origin/id`, the id this machine's own never has.</summary>
    internal static bool IsTeams(JsonElement session) => Text(session, "id")?.Contains('/') == true;

    /// <summary>
    /// POST a JSON body and read the answer: whether it succeeded, its status, its text, and its root
    /// when the text is JSON. Every write this client makes goes through here (REV3 CLEAN1: five
    /// callers each wrote it out, and two of them threw on an answer that was not JSON).
    /// </summary>
    internal async Task<(bool Ok, int Status, string Payload, JsonElement? Root)> PostJsonAsync(
        string path, string body, CancellationToken ct)
    {
        var answer = await _doors.SendAsync(ServiceVerb.Post, path, body, ct).ConfigureAwait(false);
        var payload = answer.Payload;

        JsonElement? root = null;
        try
        {
            using var document = JsonDocument.Parse(payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON: a host older than the door answers a bare 404. Each caller says so its way.
        }

        return (answer.Ok, answer.Status, payload, root);
    }

    /// <summary>
    /// Open a session record — a quest's, a chat's or an intake's, which the ledger answers alike. A
    /// refusal is an answer, not an exception, and so is a door that did not answer in JSON.
    /// </summary>
    /// <param name="kind">Which door this is, for the watchers (<see cref="SessionOpened"/>).</param>
    /// <param name="adapter">The adapter asked for — the record's own, when the answer carries one, wins.</param>
    /// <param name="repository">The repository asked for, where the door names one — the record's own wins.</param>
    /// <param name="setup">That a driven session's quest is a set-up, for the watchers (WSSETUP11).</param>
    private async Task<(string? SessionId, string Message)> OpenRecordAsync(
        string path, string body, string kind, string adapter, string? repository, CancellationToken ct, bool setup = false)
    {
        var (ok, status, payload, root) = await PostJsonAsync(path, body, ct).ConfigureAwait(false);
        if (root is not { } answer)
        {
            return (null, $"the service at {_base} has no `{path}` door ({status}) — is it older than this driver?");
        }

        if (!ok) return (null, Text(answer, "error") ?? payload);
        var session = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var record) ? record : default;
        var id = Text(session, "id");
        if (id is not null)
        {
            // What the ledger recorded rather than what was asked: a driven session names its quest, and
            // the record answers with the quest's receiver; a blank adapter takes the ledger's default. The
            // workspace is the record's too, the circle its repository is wired into on this machine (D48).
            Raise(Opened, new SessionOpened(id, kind, Text(session, "adapter") ?? adapter, Text(session, "repository") ?? repository)
            {
                Workspace = Text(session, "workspace"),
                Setup = setup,
            });
        }

        return (id, Text(answer, "message") ?? "");
    }

    private static string WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // The service's history doors, a local host's only (HIST1b, D153; the history-clearing design §6.3): what a clear of finished
    // history would take, and the clear itself. The driver's HistoryClearing calls them, judging this machine's half before and
    // removing the home's files after.

    /// <summary>
    /// The units a scope holds, each judged by the service, deleting nothing: <paramref name="query"/> is the door's one scope,
    /// <c>workspace=</c>, <c>quest=</c>, <c>ask=</c> or <c>quest=…&amp;failed=true</c>, escaped.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, or one that refused the scope, in its words.</exception>
    public async Task<IReadOnlyList<HistoryUnitView>> HistoryAsync(string query, CancellationToken ct = default)
    {
        var answer = await _doors.SendAsync(ServiceVerb.Get, $"/api/history?{query}", null, ct).ConfigureAwait(false);
        var root = Answered(answer.Ok, answer.Status, answer.Payload);
        return [.. Items(root, "units").Select(ReadHistoryUnit)];
    }

    /// <summary>
    /// One wording's search of one repository's records for a brainstorm's packet (BRAINSTORM1a, D176 point 2; D151 §6): the
    /// knowledge service's <c>/api/search</c>, local records only, each hit's entry read whole for its text, and the tier its header
    /// names, <c>meaning</c> where the semantic tier answered, else <c>words</c>. Only reads: a search moves nothing.
    /// </summary>
    /// <exception cref="DriverException">A host that refused the search, or one older than the door, in its words.</exception>
    public async Task<BrainstormSearch> BrainstormSearchAsync(string words, string repository, CancellationToken ct = default)
    {
        var path = $"/api/search?q={Uri.EscapeDataString(words)}&repositories={Uri.EscapeDataString(repository)}&limit=12";
        var answer = await _doors.SendAsync(ServiceVerb.Get, path, null, ct).ConfigureAwait(false);
        if (!answer.Ok) throw new DriverException($"the service would not search `{repository}`: {answer.Payload}");

        var found = new List<BrainstormFound>();
        using (var document = JsonDocument.Parse(answer.Payload))
        {
            foreach (var hit in document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray() : default)
            {
                if (Text(hit, "id") is not { } id || Text(hit, "path") is not { Length: > 0 } relative) continue;
                var entry = await _doors.SendAsync(ServiceVerb.Get, $"/api/entry?id={Uri.EscapeDataString(id)}", null, ct).ConfigureAwait(false);
                if (!entry.Ok) continue;
                using var read = JsonDocument.Parse(entry.Payload);
                found.Add(new BrainstormFound(
                    Text(hit, "kind")?.ToLowerInvariant() ?? "entry", relative,
                    hit.TryGetProperty("firstLine", out var first) && first.ValueKind == JsonValueKind.Number ? first.GetInt32() : null,
                    hit.TryGetProperty("lastLine", out var last) && last.ValueKind == JsonValueKind.Number ? last.GetInt32() : null,
                    Text(read.RootElement, "body") ?? "", words));
            }
        }

        // The words tier always answers a search (BM25, D151 §6); the header says where meaning answered beside it.
        return new BrainstormSearch(found, answer.Tier?.Contains("semantic", StringComparison.Ordinal) == true ? BrainstormPackets.Meaning : BrainstormPackets.Words);
    }

    /// <summary>
    /// The second press: exactly <paramref name="units"/>, each judged again by the service and cleared in one transaction of
    /// its own, or kept with its word.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, or one that refused the press, in its words.</exception>
    public async Task<IReadOnlyList<HistoryClearedView>> ClearHistoryAsync(IReadOnlyList<HistoryUnitName> units, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("units");
            foreach (var unit in units)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", unit.Kind);
                writer.WriteString("id", unit.Id);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
        var (ok, status, payload, _) = await PostJsonAsync("/api/history/clear", body, ct).ConfigureAwait(false);
        var root = Answered(ok, status, payload);
        return
        [
            .. Items(root, "units").Select(each =>
            {
                // Additive (HIST1j): absent where every removal was made, and from a host before it.
                var failed = each.TryGetProperty("failed", out var named) && named.ValueKind == JsonValueKind.Object;
                return new HistoryClearedView(
                    each.TryGetProperty("unit", out var unit) ? ReadHistoryUnit(unit) : new HistoryUnitView("", "", null),
                    Flag(each, "cleared"),
                    Text(each, "message") ?? "")
                {
                    FailedQuests = failed ? Strings(named, "quests") : [],
                    FailedAsks = failed ? Strings(named, "asks") : [],
                };
            }),
        ];
    }

    /// <summary>The door's answer read, or its refusal said: in its words where it gave some, else as a host older than the door.</summary>
    private JsonElement Answered(bool ok, int status, string payload)
    {
        JsonElement root = default;
        try
        {
            using var document = JsonDocument.Parse(payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON: a host older than the door answers a bare 404 or 405.
        }

        if (ok && root.ValueKind == JsonValueKind.Object) return root;
        if (Text(root, "error") is { Length: > 0 } error) throw new DriverException($"the service refused the history door ({status}): {error}");
        throw new DriverException($"the service at {_base} has no history door ({status}) — is it older than this driver?");
    }

    private static HistoryUnitView ReadHistoryUnit(JsonElement unit) => new(Text(unit, "kind") ?? "", Text(unit, "id") ?? "", Text(unit, "workspace"))
    {
        Clearable = Flag(unit, "clearable"),
        Quests = Strings(unit, "quests"),
        Forgotten = Strings(unit, "forgotten"),
        Asks = Strings(unit, "asks"),
        Sessions = Strings(unit, "sessions"),
        Teammates = Strings(unit, "teammates"),
        Refusal = unit.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.Object ? ReadHistoryRefusal(refusal) : null,
        Kept = [.. Items(unit, "kept").Select(ReadHistoryRefusal)],
    };

    private static HistoryRefusalView ReadHistoryRefusal(JsonElement refusal) => new(Text(refusal, "refusal") ?? "", Text(refusal, "error") ?? "")
    {
        Quest = Text(refusal, "quest"),
        Ask = Text(refusal, "ask"),
        Session = Text(refusal, "session"),
        Origin = Text(refusal, "origin"),
        Workspace = Text(refusal, "workspace"),
        // Additive (HIST1l, HIST1m): each absent beside every other word, and from a host before it.
        Waits = Text(refusal, "waits"),
        Stands = Text(refusal, "stands"),
        By = Text(refusal, "by"),
    };

    // The review on the record, read (REVIEWENV1b2): each field optional, so a quest or an ask no review touches, and one from a
    // host before them, reads as it did. A set-up, a verdict, a choice or a proposal that is not whole is passed over, as the
    // service passes over what it cannot read: never a failed read of the quest or the ask. Then the review's quest and ask
    // doors.

    /// <summary>A quest's chain choice (<c>review</c>), or null where it answers none, or one that names no choice.</summary>
    private static QuestReviewChoiceView? ReadReviewChoice(JsonElement quest) =>
        quest.TryGetProperty("review", out var review) && Text(review, "choice") is { Length: > 0 } choice
            ? new QuestReviewChoiceView(choice, Text(review, "words"))
            : null;

    /// <summary>
    /// A taker's current submission (FABRIC-CLOSE1a), or null: none, from a host before it, or named only in half. Its commit
    /// (FABRIC-CLOSE1c5) only where it is a commit's full id, lowered as a verdict's is; absent from a host before it. The session
    /// that said its ready (FABRIC-CLOSE1c4), absent where none did and from a host before it.
    /// </summary>
    private static QuestSubmissionView? ReadSubmission(JsonElement quest) =>
        quest.TryGetProperty("submission", out var submission) && submission.ValueKind == JsonValueKind.Object
        && Text(submission, "machine") is { Length: > 0 } machine
        && submission.TryGetProperty("sequence", out var sequence) && sequence.ValueKind == JsonValueKind.Number
        && sequence.TryGetInt64(out var number) && number > 0
            ? new QuestSubmissionView(
                machine, number, Moment(submission, "at"), Text(submission, "note"),
                Text(submission, "commit") is { } commit && EvidenceCodes.IsObjectId(commit) ? commit.ToLowerInvariant() : null,
                Text(submission, "session") is { Length: > 0 } session ? session : null)
            : null;

    /// <summary>
    /// A source's own reopen no later submission answered (FABRIC-CLOSE1c4), or null: none, from a host before it, or named only in half,
    /// the reopen's or the reopened submission's machine and sequence. The session that said the reopened ready (FABRIC-CLOSE1c8), absent
    /// where none did and from a host before it.
    /// </summary>
    private static QuestReopenView? ReadReopen(JsonElement quest) =>
        quest.TryGetProperty("reopened", out var reopen) && reopen.ValueKind == JsonValueKind.Object
        && ReadSubmissionRef(quest, "reopened") is { } named && ReadSubmissionRef(reopen, "of") is { } of
            ? new QuestReopenView(
                named.Machine, named.Sequence, of, Text(reopen, "note"), Moment(reopen, "at"),
                Text(reopen, "session") is { Length: > 0 } session ? session : null)
            : null;

    /// <summary>A submission a quest names by its machine and sequence (FABRIC-CLOSE1c), or null: none, from a host before it, or named only in half.</summary>
    private static QuestSubmissionRef? ReadSubmissionRef(JsonElement quest, string name) =>
        quest.TryGetProperty(name, out var named) && named.ValueKind == JsonValueKind.Object
        && Text(named, "machine") is { Length: > 0 } machine
        && named.TryGetProperty("sequence", out var sequence) && sequence.ValueKind == JsonValueKind.Number
        && sequence.TryGetInt64(out var number) && number > 0
            ? new QuestSubmissionRef(machine, number)
            : null;

    /// <summary>A set-up step's set-ups (<c>setUps</c>), each with a full commit: one without is half of one.</summary>
    private static IReadOnlyList<QuestSetUpView> ReadSetUps(JsonElement quest) =>
    [
        .. Items(quest, "setUps")
            .Where(setUp => EvidenceCodes.IsObjectId(Text(setUp, "commit")))
            .Select(setUp => new QuestSetUpView(Text(setUp, "commit")!)
            {
                Look = Text(setUp, "look"),
                Shows = Text(setUp, "shows"),
                Again = Text(setUp, "again"),
                Served = Text(setUp, "served"),
                Run = Text(setUp, "run"),
                Session = Text(setUp, "session"),
                Local = Flag(setUp, "local"),
                At = Moment(setUp, "at"),
                Machine = Text(setUp, "machine"),
                Sequence = Long(setUp, "sequence"),
            }),
    ];

    /// <summary>The person's verdicts (<c>verdicts</c>), each saying one: a verdict that says nothing is none.</summary>
    private static IReadOnlyList<QuestReviewVerdictView> ReadVerdicts(JsonElement quest) =>
    [
        .. Items(quest, "verdicts")
            .Where(verdict => Text(verdict, "said") is { Length: > 0 })
            .Select(verdict =>
            {
                var setUp = verdict.TryGetProperty("setUp", out var named) && named.ValueKind == JsonValueKind.Object ? named : default;
                return new QuestReviewVerdictView(Text(verdict, "said")!)
                {
                    SetUpMachine = Text(setUp, "machine"),
                    SetUpSequence = Long(setUp, "sequence"),
                    Commit = Text(verdict, "commit"),
                    Words = Text(verdict, "words"),
                    At = Moment(verdict, "at"),
                    Machine = Text(verdict, "machine"),
                };
            }),
    ];

    /// <summary>An ask's review choices (<c>reviewChoices</c>), each a choice with when it was made.</summary>
    private static IReadOnlyList<AskReviewChoiceView> ReadReviewChoices(JsonElement ask) =>
    [
        .. Items(ask, "reviewChoices")
            .Where(chosen => Text(chosen, "choice") is { Length: > 0 } && Moment(chosen, "at") is not null)
            .Select(chosen => new AskReviewChoiceView(Text(chosen, "choice")!, Moment(chosen, "at")!.Value, Text(chosen, "words"))),
    ];

    /// <summary>An ask's intake's review proposals (<c>reviewProposals</c>), each a choice with its reason and when.</summary>
    private static IReadOnlyList<AskReviewProposalView> ReadReviewProposals(JsonElement ask) =>
    [
        .. Items(ask, "reviewProposals")
            .Where(proposed => Text(proposed, "choice") is { Length: > 0 } && Text(proposed, "reason") is { Length: > 0 }
                               && Moment(proposed, "at") is not null)
            .Select(proposed => new AskReviewProposalView(
                Text(proposed, "choice")!, Text(proposed, "reason")!, Moment(proposed, "at")!.Value,
                Text(proposed, "session"), Text(proposed, "quest"))),
    ];

    /// <summary>
    /// Post a set-up step's session's said set-ups with the commit Daoris read from its tree (REVIEWENV1c, design §2.6): the local
    /// host's <c>POST /api/quests/{id}/set-up</c>, <c>{ session, commit, kind }</c>, which keeps each said set-up once. The commit
    /// is read, never the session's word. A refusal is an answer, in the service's words, and a host older than the door says so.
    /// </summary>
    /// <param name="kind">The environment's kind, as the rule declares it: <c>local</c> or <c>deployed</c>.</param>
    public async Task<(bool Ok, string Message)> PostSetUpAsync(
        string quest, string session, string commit, string kind, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("session", session);
            writer.WriteString("commit", commit);
            writer.WriteString("kind", kind);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/set-up", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no set-up door ({status}) — is it older than this driver?");
        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// The person's verdict on a review (REVIEWENV1c, design §3.3): the local host's <c>POST /api/quests/{id}/review</c>, with the
    /// set-up they were shown, named by its <c>machine</c> and <c>sequence</c> whole (REVIEWENV1b3); a skip names none. The
    /// service's sentence comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    /// <param name="verdict"><c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>.</param>
    public async Task<(bool Ok, string Message)> ReviewAsync(
        string quest, string verdict, string? words, string? machine, long? sequence, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("verdict", verdict);
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words.Trim());
            if (machine is not null && sequence is { } at)
            {
                writer.WriteStartObject("setUp");
                writer.WriteString("machine", machine);
                writer.WriteNumber("sequence", at);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/review", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no review door ({status}) — is it older than this driver?");
        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }

    /// <summary>
    /// The person's <i>Set it up in <c>environment</c></i> (REVIEWENV1j, design §3.6): the local host's
    /// <c>POST /api/quests/{id}/set-up-step</c>, <c>{ environment }</c>, the door the gate's press calls, which publishes a set-up step
    /// following a done quest. The service's sentence comes back verbatim, a refusal included, with the step it published; a host
    /// older than the door says so.
    /// </summary>
    public async Task<(bool Ok, string Message, string? Step)> PublishSetUpStepAsync(string quest, string environment, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("environment", environment);
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync(
            $"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/set-up-step", body, ct).ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no set-up step door ({status}) — is it older than this driver?", null);
        if (!ok) return (false, Text(answer, "error") ?? payload, null);
        var step = answer.TryGetProperty("quest", out var published) && published.ValueKind == JsonValueKind.Object ? Text(published, "id") : null;
        return (true, Text(answer, "message") ?? "", step);
    }

    /// <summary>
    /// The person's review choice on an ask, or its intake's proposal applied (REVIEWENV1j; design §1.5): the local host's
    /// <c>POST /api/asks/{id}/review</c>, <c>{ choice, words? }</c>, the door the ask's page presses. The latest stands. The service's
    /// sentence comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    public Task<AskAnswer> ChooseAskReviewAsync(string ask, string choice, string? words, CancellationToken ct = default) =>
        PostAskAsync($"/api/asks/{Uri.EscapeDataString(ask.TrimStart('#'))}/review", writer =>
        {
            writer.WriteString("choice", choice);
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words.Trim());
        }, ct);

    // What the review's showing reads of a session's record (REVIEWENV1d): the tree its set-up was built in.

    /// <summary>
    /// The tree a session of this machine's held (D51), closed records included, as the records answer it on loopback; null where
    /// the service holds no such record or says no tree. What <i>Show it again</i> serves a set-up's folder from.
    /// </summary>
    public async Task<string?> SessionTreeAsync(string session, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));
        if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
        return document.RootElement.EnumerateArray()
            .Where(record => record.ValueKind == JsonValueKind.Object
                             && string.Equals(Text(record, "id"), session, StringComparison.OrdinalIgnoreCase))
            .Select(record => Text(record, "tree"))
            .FirstOrDefault(tree => tree is { Length: > 0 });
    }

    // An ask's kind and workflow (WORKFLOW1e, WORKFLOW1i): the person's choices and its intake's proposals read, and the
    // person's choice posted.

    /// <summary>An ask's kind and workflow choices (<c>workflowChoices</c>), each with when it was made; one without a moment is half of one.</summary>
    private static IReadOnlyList<AskWorkflowChoiceView> ReadWorkflowChoices(JsonElement ask) =>
    [
        .. Items(ask, "workflowChoices")
            .Where(chosen => Moment(chosen, "at") is not null)
            .Select(chosen => new AskWorkflowChoiceView(Text(chosen, "kind"), Text(chosen, "workflow"), Moment(chosen, "at")!.Value, Text(chosen, "words"))),
    ];

    /// <summary>An ask's intake's kind proposals (<c>workflowProposals</c>), each a kind with its reason and when; one without is half of one.</summary>
    private static IReadOnlyList<AskWorkflowProposalView> ReadWorkflowProposals(JsonElement ask) =>
    [
        .. Items(ask, "workflowProposals")
            .Where(proposed => Text(proposed, "kind") is { Length: > 0 } && Text(proposed, "reason") is { Length: > 0 }
                               && Moment(proposed, "at") is not null)
            .Select(proposed => new AskWorkflowProposalView(
                Text(proposed, "kind")!, Text(proposed, "reason")!, Moment(proposed, "at")!.Value,
                Text(proposed, "session"), Text(proposed, "quest"))),
    ];

    /// <summary>
    /// The person's kind and workflow on an ask (WORKFLOW1e; the workflow design §4.3): the local host's
    /// <c>POST /api/asks/{id}/workflow</c>, <c>{ kind?, workflow?, words? }</c>, a person's door (D156); neither clears the choice.
    /// The latest stands. The service's sentence comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    public Task<AskAnswer> ChooseAskWorkflowAsync(string ask, string? kind, string? workflow, string? words, CancellationToken ct = default) =>
        PostAskAsync($"/api/asks/{Uri.EscapeDataString(ask.TrimStart('#'))}/workflow", writer =>
        {
            if (kind is not null) writer.WriteString("kind", kind);
            if (workflow is not null) writer.WriteString("workflow", workflow);
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words.Trim());
        }, ct);

    /// <summary>
    /// The person's kind and workflow on a quest no ask published, before it starts (WORKFLOW1e2; the workflow design §4.1 row 1): the
    /// local host's <c>POST /api/quests/{id}/workflow</c>, <c>{ kind?, workflow?, words? }</c>, a person's door (D156); neither clears
    /// the choice. The service's sentence comes back verbatim, a refusal included, and a host older than the door says so.
    /// </summary>
    public async Task<(bool Ok, string Message)> ChooseQuestWorkflowAsync(
        string quest, string? kind, string? workflow, string? words, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            if (kind is not null) writer.WriteString("kind", kind);
            if (workflow is not null) writer.WriteString("workflow", workflow);
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words.Trim());
            writer.WriteEndObject();
        });
        var (ok, status, payload, root) = await PostJsonAsync($"/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/workflow", body, ct)
            .ConfigureAwait(false);
        if (root is not { } answer) return (false, $"the service at {_base} has no quest workflow door ({status}) — is it older than this driver?");
        return ok ? (true, Text(answer, "message") ?? "") : (false, Text(answer, "error") ?? payload);
    }
}

/// <summary>A session record the ledger opened for this client (LOG1b).</summary>
/// <param name="Kind">
/// Which door opened it — <see cref="Driven"/>, <see cref="Chat"/>, <see cref="Intake"/> or
/// <see cref="Help"/>. The door's, not the record's: the record calls an intake and Ask Daoris chats.
/// </param>
/// <param name="Repository">The repository the record runs in, as the ledger recorded it; a name, never a path.</param>
public sealed record SessionOpened(string Session, string Kind, string Adapter, string? Repository)
{
    /// <summary>The workspace the record runs in, as the ledger recorded it (WSSETUP11); null where it said none.</summary>
    public string? Workspace { get; init; }

    /// <summary>That a driven session serves a set-up quest (<see cref="SetupQuests"/>, WSSETUP11).</summary>
    public bool Setup { get; init; }

    public const string Driven = "driven";
    public const string Chat = "chat";
    public const string Intake = "intake";
    public const string Help = "help";
}

/// <summary>A session record the ledger moved for this client (LOG1b), in the state's public spelling.</summary>
public sealed record SessionMoved(string Session, string State);
