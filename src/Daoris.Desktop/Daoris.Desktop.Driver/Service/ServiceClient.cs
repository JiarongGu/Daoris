using System.Globalization;
using System.Text;
using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.History;
using Daoris.Contracts.Knowledge;
using Daoris.Contracts.Sessions;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The driver's half of the conversation with the service — a client through the same doors as every
/// other component (D46 §7). Deliberately NOT a reference to the service's assemblies: the doors are
/// the contract, declared in Daoris.Contracts, and a driver that could reach the store directly would
/// drift from every client that cannot.
/// </summary>
/// <remarks>
/// It writes each request and reads each answer, and the HTTP is behind
/// <see cref="IServiceDoors"/>, whose real one, <see cref="ServiceHttp"/>, the constructors that take a URL compose. Every
/// module names this client; none names the HTTP. A family of doors on a resource of its own that another module asks is a
/// class over the client, reached through it (<c>Opinions</c>, <c>Helpers</c> and <c>Partners</c>, each its owner's member of the client; PART8): it reaches the HTTP, the
/// JSON reading and <see cref="Opened"/> through the internal surface below (D166's PART8 note).
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
    }

    /// <summary>A client over HTTP to the host at <paramref name="baseUrl"/>, with its key; a test may hand its own HTTP client.</summary>
    public ServiceClient(string baseUrl, string? key, HttpClient? http = null)
        : this(new ServiceHttp(baseUrl, key, http))
    {
    }

    /// <summary>The local host's confirmation doors, read for the windows (NOTIFY2j), over this client.</summary>
    public ConfirmationDoors Confirmations { get; }

    public SessionOpenDoors Opens { get; }

    /// <summary>A door class's request whose every answer comes back, a refusal's included (<see cref="IServiceDoors.SendAsync"/>).</summary>
    internal async Task<ServiceReply> SendAsync(ServiceVerb verb, string path, string? json, CancellationToken ct)
    {
        var answer = await _doors.SendAsync(verb, path, json, ct).ConfigureAwait(false);
        Opens.Observe(answer);
        return answer;
    }

    // A route the contracts do not declare yet has a method of its own below.
    /// <summary>A route the contracts declare, sent as its record and read as its answer; a refusal comes back as one.</summary>
    public async Task<Sent<TAnswer>> SendAsync<TAnswer>(IRoute<TAnswer> request, CancellationToken ct = default) where TAnswer : class
    {
        var (verb, path, json) = RouteRequest.Of(request);
        return Sent<TAnswer>.Read($"{_base}{path}", await SendAsync(verb, path, json, ct).ConfigureAwait(false));
    }

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
    /// A repository's first registration from its working tree at <paramref name="root"/> (ONEWAY1u, D124 §3.3), at this client's
    /// service, which may be any address: what <see cref="RegistryConnect.Plan"/> composes, taken or refused in the service's own
    /// words. The workspace the service says it took is read back, since silence preserves whatever the row held.
    /// </summary>
    /// <exception cref="RegistryRefusal">The plan's refusal, or the service's; nothing was registered.</exception>
    public async Task<ConnectDone> ConnectAsync(string root, string? workspace, CancellationToken ct = default)
    {
        var plan = RegistryConnect.Plan(root, workspace, _base);
        var (ok, status, _, answer) = await PostJsonAsync("/api/registry", plan.Body.ToJsonString(), ct).ConfigureAwait(false);
        if (!ok) throw Refused($"the service refused the registration: {ServiceSaid(status, answer)}", 2);
        return new ConnectDone(plan, answer is { } landed ? Text(landed, "workspace") : null);
    }

    /// <summary>
    /// Take a repository off the registry (ONEWAY1u): the service's own sentence, which says what retiring does not do. One that was
    /// never registered is an answer, not a failure.
    /// </summary>
    /// <exception cref="RegistryRefusal">The service refused; nothing changed.</exception>
    public async Task<string> RetireAsync(string repository, CancellationToken ct = default)
    {
        var answer = await SendAsync(ServiceVerb.Delete, $"/api/registry/{Uri.EscapeDataString(repository)}", null, ct).ConfigureAwait(false);
        var root = AnswerOf(answer.Payload);
        if (!answer.Ok) throw Refused(ServiceSaid(answer.Status, root), 1);
        return (root is { } said ? Text(said, "message") : null) ?? $"{repository} is no longer registered.";
    }

    /// <summary>
    /// Register everything a folder's subdirectories propose (ONEWAY1u, D48 §3). A folder is a machine path and goes only to a service on
    /// this machine; none lets the service use its own knowledge root.
    /// </summary>
    /// <param name="folder">An absolute folder, or null.</param>
    /// <exception cref="RegistryRefusal">A folder for a service elsewhere, or the service refused; nothing was sent or changed.</exception>
    public async Task<string> ImportAsync(string? folder, string? workspace, CancellationToken ct = default)
    {
        if (folder is not null && !RegistryConnect.IsLocal(_base))
        {
            throw new RegistryRefusal(
                RegistryRefusal.FolderNotLocal,
                $"{_base} is not on this machine, and a folder here means nothing there — import a "
                + "folder into this machine's own service; a deployment is fed by the machines that join it.",
                2);
        }

        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            if (!string.IsNullOrEmpty(folder)) writer.WriteString("folder", folder);
            if (!string.IsNullOrEmpty(workspace)) writer.WriteString("workspace", workspace);
            writer.WriteEndObject();
        });
        var (ok, status, _, answer) = await PostJsonAsync("/api/registry/import", body, ct).ConfigureAwait(false);
        if (!ok) throw Refused(ServiceSaid(status, answer), 1);
        return (answer is { } said ? Text(said, "message") : null) ?? "imported.";
    }

    private static RegistryRefusal Refused(string message, int exit) => new(RegistryRefusal.ServiceRefused, message, exit);

    /// <summary>The service's own sentence, or a bare status when it did not compose one.</summary>
    private static string ServiceSaid(int status, JsonElement? answer) =>
        (answer is { } said ? Text(said, "error") : null) ?? $"the service answered {status}";

    private static JsonElement? AnswerOf(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
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
    /// The session list as the service wrote it, the active records or with <paramref name="includeClosed"/> every one: the text the
    /// readers below and the snapshot read from. A refused read throws with the service's own sentence.
    /// </summary>
    /// <exception cref="DriverException">A refused read.</exception>
    public async Task<string> SessionsJsonAsync(bool includeClosed, CancellationToken ct = default)
    {
        var sent = await SendAsync(new ListSessions(IncludeClosed: includeClosed ? true : null), ct).ConfigureAwait(false);
        sent.Answered();
        return sent.Reply.Payload;
    }

    /// <summary>
    /// How many sessions failed on each quest (DRV6), as <c>SnapshotAsync</c> (the work orders' read of the client) derives them: what a workspace plan reads a
    /// parked set-up by (WSSETUP6), without the rest of a snapshot.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> StrikesAsync(CancellationToken ct = default) =>
        ReadStrikes(await SessionsJsonAsync(true, ct).ConfigureAwait(false));

    /// <summary>
    /// Every session record this machine keeps, closed ones included, with the repository it ran in: what a workspace plan
    /// counts the sessions run in each repository by, and whose conversation records it reads (WSSETUP6, D124 §4.2). A
    /// teammate's record says nothing of this machine's work, and is left out as the strikes leave it.
    /// </summary>
    public async Task<IReadOnlyList<SessionRun>> SessionRunsAsync(CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await SessionsJsonAsync(true, ct).ConfigureAwait(false));
        return
        [
            .. document.RootElement.EnumerateArray()
                .Where(session => !IsTeams(session))
                .Select(session => new SessionRun(Text(session, "id") ?? "", Text(session, "repository") ?? ""))
                .Where(run => run.Session.Length > 0 && run.Repository.Length > 0),
        ];
    }

    /// <summary>
    /// One session record as it stands, an ended one included (FOUNDATION1 m-ii: what a piece of news says of its session, which the journal's
    /// row never carries); null where the service has none.
    /// </summary>
    public async Task<SessionView?> FindSessionAsync(string id, CancellationToken ct = default) =>
        ReadSessions(await SessionsJsonAsync(true, ct).ConfigureAwait(false))
            .FirstOrDefault(session => string.Equals(session.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The asking session and every other session of its repository with a tree and a base here, chats included (LINESTOGETHER1c); an
    /// active session is taken as running. Null where the service has no such session or it holds no tree.
    /// </summary>
    public async Task<(RelatedCandidate Mine, IReadOnlyList<RelatedCandidate> Others)?> RelatedCandidatesAsync(string id, CancellationToken ct = default)
    {
        var all = ReadSessions(await SessionsJsonAsync(true, ct).ConfigureAwait(false));
        var running = ReadSessions(await SessionsJsonAsync(false, ct).ConfigureAwait(false)).Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var quests = (await this.EveryQuestAsync(ct).ConfigureAwait(false))
            .ToDictionary(quest => quest.Id, quest => quest.Paths, StringComparer.OrdinalIgnoreCase);
        return RelatedCandidates.Pick(all, running, quest => quests.TryGetValue(quest, out var paths) ? paths : [], id, Directory.Exists);
    }

    /// <summary>This machine's active sessions — the one read an orphan sweep needs, without a whole snapshot.</summary>
    public async Task<IReadOnlyList<SessionView>> ActiveSessionsAsync(CancellationToken ct = default) =>
        ReadSessions(await SessionsJsonAsync(false, ct).ConfigureAwait(false));

    /// <summary>Where a circle stands on this machine (SYNC6a) — read from its host, without reaching the remote.</summary>
    public async Task<SyncStanding> SyncStandingAsync(string workspace, CancellationToken ct = default) =>
        RemoteSyncPayloads.Standing(await GetAsync($"/api/sync?workspace={Uri.EscapeDataString(workspace)}", ct).ConfigureAwait(false));

    /// <summary>
    /// Whether this session took its own quest through its own connector (STANDDOWN2) — read at its end,
    /// so a session holding the quest it took is told from one that found it taken.
    /// </summary>
    public async Task<bool> TookAsync(string id, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await SessionsJsonAsync(true, ct).ConfigureAwait(false));
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
        using var document = JsonDocument.Parse(await SessionsJsonAsync(true, ct).ConfigureAwait(false));
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (Text(session, "id") == id) return Text(session, "tookBind") is { Length: > 0 } why ? why : null;
        }

        return null;
    }

    /// <summary>
    /// Tell the service what the person added to a running session (DRIFT1a2, D133 §1), which keeps it on the ask that
    /// session's work is for: the words verbatim, by the session's own id. Whether it was kept is an answer, never an
    /// exception: <c>kept: false</c> is a session on no ask, with the service's sentence, and a refusal or a host without
    /// the door (one older than DRIFT1a, or a shared one) is false with a sentence saying which.
    /// </summary>
    public async Task<(bool Kept, string Message)> AddedToSessionAsync(string id, string text, CancellationToken ct = default)
    {
        var (ok, status, payload, root) = await SendJsonAsync(new AddToSession(id, text), ct).ConfigureAwait(false);
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
    /// <param name="ending">
    /// The shell keeps the words on a record that winds up or moves (FOUNDATION1o, the foundation design §3.4 rule 4), for its next part to go on
    /// with; a host before the row ignores it and refuses them as <c>running</c>, which the shell then holds as before.
    /// </param>
    /// <param name="question">The question id a person's press answers (COMMS1e), kept on the word.</param>
    public async Task<SayAnswer> SayAsync(
        string id, string text, IReadOnlyList<string>? files = null, CancellationToken ct = default, string? channel = null,
        string? from = null, string? key = null, string? level = null, bool running = false,
        (string Decision, string Question, string Choice)? supervised = null, bool ending = false, string? question = null)
    {
        // COMMS1e: the person's press names its question, which the host's say door checks.
        var presses = question is { Length: > 0 } && supervised is null;
        var request = new SaySession(
            id, supervised is null ? text : null,
            files is { Count: > 0 } ? files : null,
            // The channel message these words are, which the ask holds already; an older service ignores it.
            channel is { Length: > 0 } ? channel : null,
            from is { Length: > 0 } ? from : null,
            key is { Length: > 0 } ? key : null,
            level is { Length: > 0 } ? level : null,
            running ? true : null,
            supervised?.Decision,
            supervised is { } release ? release.Question : presses ? question : null,
            supervised?.Choice,
            // Every keyed say reads a settled answer, the supervisor's release excepted, whose door reads no settlement. A host before
            // the row ignores the field and answers as it did.
            key is { Length: > 0 } && supervised is null ? true : null,
            ending ? true : null);
        var (ok, status, payload, root) = await SendJsonAsync(request, ct).ConfigureAwait(false);
        if (root is not { } answered)
        {
            return new SayAnswer(false, $"the service at {_base} has no door for words to a session that is not running ({status}) — is it older than this driver?");
        }

        if (!ok)
        {
            // FOUNDATION1d-ii (the design §3.4 rule 2): `continued` names the line's head beside its word, at the status of every other refusal.
            var refusal = Text(answered, "refusal");
            return new SayAnswer(false, Text(answered, "error") ?? payload)
            {
                Refusal = refusal,
                Head = refusal == SayAnswer.Continued && answered.ValueKind == JsonValueKind.Object && Text(answered, "head") is { Length: > 0 } head ? head : null,
            };
        }

        // Settled before a word (row p): a success naming the record the key's word left carries no `said`, so it is read first and no
        // reader below it finds a word to act on.
        if (answered.ValueKind == JsonValueKind.Object && Text(answered, "settled") is { Length: > 0 } settled)
        {
            return new SayAnswer(true, Text(answered, "message") ?? "") { Settled = settled };
        }

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
        var (ok, status, payload, root) = await SendJsonAsync(new TakeBackSaid(id, word, text), ct).ConfigureAwait(false);
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
    /// Move one of the person's words held on a part's record to the head of the line <paramref name="to"/> is on (LINESTOGETHER1b's move
    /// door): taken off and kept there in one step, a retry answered moved again. Whether it moved is an answer, never an exception, as the
    /// take-back's is; a host without the door says so with no word. A request whose answer never came throws, for the caller to say so.
    /// </summary>
    public async Task<MoveAnswer> MoveSaidAsync(string id, string word, string? to = null, CancellationToken ct = default)
    {
        var (ok, status, payload, root) = await SendJsonAsync(new MoveSaid(id, word, to is { Length: > 0 } ? to : null), ct).ConfigureAwait(false);
        if (root is not { ValueKind: JsonValueKind.Object } answered)
        {
            return new MoveAnswer(false, $"the service at {_base} has no door for moving words to a session's head ({status}) — is it older than this driver?");
        }

        if (!ok) return new MoveAnswer(false, Text(answered, "error") ?? payload, Text(answered, "refusal")) { To = Text(answered, "head") };
        return new MoveAnswer(Flag(answered, "moved"), Text(answered, "message") ?? "", Text(answered, "why"))
        {
            To = Text(answered, "to"),
            Again = Flag(answered, "again"),
            Word = answered.TryGetProperty("said", out var said) ? ReadWord(said) : null,
        };
    }

    /// <summary>
    /// Mark these waiting words attempted on this machine's record, before the wire (COWORK1c's attempt door, the message-urgency
    /// design §6): the first moment kept, so a shell that comes up again never sends one twice. Whether they were marked is an answer,
    /// never an exception: a host without the door (one older than COWORK1c) is false with a sentence saying so.
    /// </summary>
    /// <remarks>
    /// FOUNDATION1 row p (the design §3.4 rule 5): the answer names each word it marked. The service answers the words that no longer wait as
    /// <c>unmarked</c>, a list this client once ignored, answering success; <see cref="AttemptAnswer.Marked"/> is the words asked less those, and
    /// is empty where the mark was not acknowledged at all (a refusal, a host with no door, an answer that did not come or does not read).
    /// </remarks>
    public async Task<AttemptAnswer> AttemptAsync(string id, IReadOnlyList<string> said, CancellationToken ct = default)
    {
        var (ok, status, payload, root) = await SendJsonAsync(new AttemptSaid(id, [.. said]), ct).ConfigureAwait(false);
        if (root is not { } answered)
        {
            return new AttemptAnswer(false, $"the service at {_base} has no door for a word's attempt ({status}) — is it older than this driver?", [], said);
        }

        if (!ok) return new AttemptAnswer(false, Text(answered, "error") ?? payload, [], said);
        // The words the answer names as no longer waiting; every other word asked was marked.
        var unmarked = answered.ValueKind == JsonValueKind.Object && answered.TryGetProperty("unmarked", out var named) && named.ValueKind == JsonValueKind.Array
            ? named.EnumerateArray().Where(word => word.ValueKind == JsonValueKind.String).Select(word => word.GetString()!).ToList()
            : [];
        return new AttemptAnswer(true, Text(answered, "message") ?? "", [.. said.Except(unmarked, StringComparer.Ordinal)], unmarked);
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
        var (ok, status, payload, root) = await SendJsonAsync(new TakeSaid(id, [.. said], by), ct).ConfigureAwait(false);
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
        ContinuedFrom? from = null, IReadOnlyDictionary<string, string>? access = null) =>
        await OpenRecordAsync(
                DrivenOpen(questId, adapter, harnessVersion, profile, tree, baseCommit, from, access), opened => (opened.Session, opened.Message),
                SessionOpened.Driven, adapter, repository: null, ct, setup)
            .ConfigureAwait(false);

    /// <summary>
    /// <see cref="OpenSessionAsync"/> through the keyed door (FOUNDATION1d-ii part 5, <see cref="SessionOpenDoors.OpenAsync"/>), asked to move
    /// <paramref name="moves"/>' words onto the driven record it opens, in the open's own step: sent only to a host whose <c>opens</c> mark is 2,
    /// under a key of its own, so a host below it opens as before and moves nothing. What opened is told to <see cref="Opened"/>'s watchers
    /// as the record names it, as <see cref="OpenSessionAsync"/>'s is. A lost answer is held, naming the records that continue the one left,
    /// and opens nothing here.
    /// </summary>
    /// <param name="moves">
    /// The words to move; null for an open that moves none, which still goes under its key (FOUNDATION1f-iv part a: a carry-on that hands no
    /// word keeps a move record, whose <c>opens</c> is that key) and then names the record it continues by <paramref name="from"/>.
    /// </param>
    /// <param name="opens">The key the open is sent under: the move record's, written before the open; null draws one of the open's own.</param>
    public async Task<DestinationOpen> OpenSessionMovingAsync(
        string questId, string adapter, string? harnessVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from,
        MoveWords? moves, CancellationToken ct = default, bool setup = false, IReadOnlyDictionary<string, string>? access = null,
        string? opens = null)
    {
        var left = moves?.From ?? from?.Session ?? throw new ArgumentException("A keyed open names the record it continues.", nameof(from));
        var opened = await Opens
            .OpenAsync(
                DrivenOpen(questId, adapter, harnessVersion, profile, tree, baseCommit, from, access),
                opens ?? moves?.Opens ?? MoveWords.DrivenKey(), left, ct: ct, moves: moves)
            .ConfigureAwait(false);
        if (opened.SessionId is { } id)
        {
            Raise(Opened, new SessionOpened(id, SessionOpened.Driven, opened.Recorded.Adapter ?? adapter, opened.Recorded.Repository)
            {
                Workspace = opened.Recorded.Workspace,
                Setup = setup,
            });
        }

        return opened;
    }

    /// <summary>
    /// A driven record's open, as both of its doors send it. What the harness is, the account, the working tree and where it stood
    /// are observed on this side before the spawn: the service has no binaries or checkout to look at. The setup it starts under keeps
    /// the pauses' order, resolved here, which holds the driver's file; <paramref name="from"/> is the session it continues, why and who
    /// asked, known on this side alone.
    /// </summary>
    private static OpenSession DrivenOpen(
        string questId, string adapter, string? harnessVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from,
        IReadOnlyDictionary<string, string>? access) =>
        new(questId, adapter, harnessVersion, profile, tree, baseCommit, from?.Session, from?.Why, from?.By,
            access is { Count: > 0 } ? new Dictionary<string, string>(access) : null, "build");

    /// <summary>
    /// Ask the ledger to open a CHAT in a repository (D49 §3) — a person's session, serving no quest
    /// yet. A refusal is an answer, not an exception: the usual one is that the repository is busy,
    /// and the sentence names what holds it.
    /// </summary>
    /// <param name="from">The conversation this one continues, handed over (MIXAGENT1a-iii), for its record; null for none.</param>
    public async Task<(string? SessionId, string Message)> OpenChatAsync(
        string repository, string adapter, string? harnessVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default, ContinuedFrom? from = null) =>
        await OpenRecordAsync(
                ChatOpen(repository, adapter, harnessVersion, profile, tree, baseCommit, from), opened => (opened.Session, opened.Message),
                SessionOpened.Chat, adapter, repository, ct)
            .ConfigureAwait(false);

    /// <summary>
    /// <see cref="OpenChatAsync"/> through the keyed door (FOUNDATION1d-ii part 4, <see cref="SessionOpenDoors.OpenAsync"/>), asked to move
    /// <paramref name="moves"/>' words onto the record it opens in the open's own step: sent only to a host whose <c>opens</c> mark is 2, under
    /// a key of its own, so a host below it opens as before and moves nothing. What opened is told to <see cref="Opened"/>'s watchers as
    /// <see cref="OpenChatAsync"/>'s is. A lost answer is held, naming the records that continue the one left, and opens nothing here.
    /// </summary>
    /// <param name="workspace">The circle the repository is wired into on this machine, for the watchers: the keyed answer names no record.</param>
    public async Task<DestinationOpen> OpenChatMovingAsync(
        string repository, string adapter, string? harnessVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from,
        MoveWords moves, string? workspace, CancellationToken ct = default)
    {
        var opened = await Opens
            .OpenAsync(
                ChatOpen(repository, adapter, harnessVersion, profile, tree, baseCommit, from),
                moves.Opens ?? MoveWords.ChatKey(), moves.From, ct: ct, moves: moves)
            .ConfigureAwait(false);
        if (opened.SessionId is { } id) Raise(Opened, new SessionOpened(id, SessionOpened.Chat, adapter, repository) { Workspace = workspace });
        return opened;
    }

    /// <summary>A chat's open, as both of its doors send it.</summary>
    private static OpenChat ChatOpen(
        string repository, string adapter, string? harnessVersion, string? profile, string? tree, string? baseCommit, ContinuedFrom? from) =>
        new(repository, adapter, harnessVersion, profile, tree, baseCommit, from?.Session, from?.Why, from?.By, "chat");

    /// <summary>
    /// Ask the ledger to open an INTAKE for an ask (D65 §1b), in the room this side is about to run it
    /// in. A refusal is an answer, not an exception — usually that the ask is already answered.
    /// </summary>
    public async Task<(string? SessionId, string Message)> OpenIntakeAsync(
        string ask, string adapter, string room, string? harnessVersion = null, string? profile = null,
        CancellationToken ct = default)
    {
        return await OpenRecordAsync(
                new OpenIntake(ask, adapter, room, harnessVersion, profile, "intake"), opened => (opened.Session, opened.Message),
                SessionOpened.Intake, adapter, repository: null, ct)
            .ConfigureAwait(false);
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
        return await OpenRecordAsync(
                new OpenHelp(adapter, room, harnessVersion, profile, workspace, mode, "ask-daoris"), opened => (opened.Session, opened.Message),
                SessionOpened.Help, adapter, repository: null, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Record the access mode an Ask Daoris conversation going on runs under (HELPMODE1a2, D183 point 3), where the person's mode
    /// changed since it last ran. A refusal is the ledger's sentence (<see cref="DriverException"/>).
    /// </summary>
    public async Task KeepHelpModeAsync(string sessionId, string mode, CancellationToken ct = default)
    {
        var (ok, _, payload, root) = await SendJsonAsync(new KeepHelpMode(sessionId, mode), ct).ConfigureAwait(false);
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
        var (ok, _, payload, root) = await SendJsonAsync(new KeepAccess(sessionId, access), ct).ConfigureAwait(false);
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
        var (ok, _, payload, root) = await SendJsonAsync(new KeepResolved(sessionId, [.. versions]), ct).ConfigureAwait(false);
        if (!ok)
        {
            throw new DriverException(
                $"the service refused recording the versions session `{sessionId}` was handed: "
                + ((root is { } refused ? Text(refused, "error") : null) ?? payload));
        }
    }

    /// <summary>
    /// History's age applied to the finished trees' records no session names (KEEPSIZE1b, <c>POST /api/trees/record/age</c>): each kept at
    /// or before <paramref name="before"/> goes; how many. A service with no such door answers none.
    /// </summary>
    internal async Task<int> AgeWorkRecordsAsync(DateTimeOffset before, CancellationToken ct = default)
    {
        var (ok, status, payload, root) = await SendJsonAsync(new AgeWorkRecords(before.UtcDateTime), ct).ConfigureAwait(false);
        if (ok) return root is { } answered && answered.TryGetProperty("cleared", out var cleared) && cleared.TryGetInt32(out var count) ? count : 0;
        if (status == 404) return 0;
        throw new DriverException($"the service refused ageing the finished trees' records: {(root is { } refused ? Text(refused, "error") : null) ?? payload}");
    }

    /// <summary>
    /// The record of the finished tree a session ran in (<c>GET /api/sessions/{id}/record</c>), as the service answers it; null where none
    /// is kept or the service has no such door.
    /// </summary>
    public async Task<string?> SessionRecordAsync(string session, CancellationToken ct = default)
    {
        var answer = (await SendAsync(new ReadSessionRecord(session), ct).ConfigureAwait(false)).Reply;
        if (answer.Ok) return answer.Payload;
        if (answer.Status == 404) return null;
        throw new DriverException($"the service refused reading session `{session}`'s record: {answer.Payload}");
    }

    /// <summary>
    /// What the line ending at <paramref name="session"/> read of memory, each entry in one line as the service rechecked it now
    /// (<c>POST /api/sessions/{id}/memory/read</c>, KNOWCENTRAL2f); none where the service has no such door or answers none.
    /// </summary>
    public async Task<IReadOnlyList<string>> LineMemoryAsync(string session, CancellationToken ct = default)
    {
        var answer = (await SendAsync(new ReadLineMemory(session), ct).ConfigureAwait(false)).Reply;
        if (answer.Status == 404) return [];
        if (!answer.Ok) throw new DriverException($"the service refused reading the memory session `{session}`'s line read: {answer.Payload}");
        using var document = JsonDocument.Parse(answer.Payload);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? [.. Items(document.RootElement, "shown").Select(item => Text(item, "line")).OfType<string>().Where(line => line.Trim().Length > 0)]
            : [];
    }

    /// <summary>
    /// A session's memory rows as the service answers them for its page (KNOWCENTRAL2e; the memory coordinator design §1, §4): its writes,
    /// ends and proposals, an end's reason without the row's token. Null from a host with no door.
    /// </summary>
    public async Task<string?> SessionMemoryAsync(string session, CancellationToken ct = default)
    {
        var answer = (await SendAsync(new ReadSessionMemory(session), ct).ConfigureAwait(false)).Reply;
        if (answer.Ok) return answer.Payload;
        if (answer.Status == 404) return null;
        throw new DriverException($"the service refused reading session `{session}`'s memory: {answer.Payload}");
    }

    /// <summary>
    /// Every session with a memory change waiting on the person (KNOWCENTRAL2e), as the service reads them: ends and replacements awaiting,
    /// a stopped session's staged rows. None from a host with no door.
    /// </summary>
    public async Task<IReadOnlyList<JsonElement>> MemoryWaitsAsync(CancellationToken ct = default)
    {
        var answer = (await SendAsync(new ListMemoryWaits(), ct).ConfigureAwait(false)).Reply;
        if (answer.Status == 404) return [];
        if (!answer.Ok) throw new DriverException($"the service refused reading the memory waits: {answer.Payload}");
        using var document = JsonDocument.Parse(answer.Payload);
        return document.RootElement.TryGetProperty("waits", out var waits) && waits.ValueKind == JsonValueKind.Array
            ? [.. waits.EnumerateArray().Select(each => each.Clone())]
            : [];
    }

    /// <summary>
    /// The person's decision on a session's memory (KNOWCENTRAL2e): an awaiting end approved or declined by its row where
    /// <paramref name="op"/> names one, else the ended session's staged changes kept or dropped. A refusal is an answer, in the service's words.
    /// </summary>
    public async Task<(bool Ok, string Message)> DecideMemoryAsync(string session, string? op, bool yes, CancellationToken ct = default)
    {
        var (ok, _, payload, root) = op is { Length: > 0 }
            ? await SendJsonAsync(new DecideMemory(op, yes), ct).ConfigureAwait(false)
            : await SendJsonAsync(new KeepSessionMemory(session, yes), ct).ConfigureAwait(false);
        var said = root is { ValueKind: JsonValueKind.Object } answered ? Text(answered, ok ? "message" : "error") : null;
        return (ok, said ?? payload);
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

    /// <summary>An ISO 8601 moment the service wrote, or null where it wrote none that reads.</summary>
    private static DateTimeOffset? Moment(JsonElement element, string name) => Moments.Read(element, name);

    // Empty strings are kept here, as the copy this replaced kept them.
    private static IReadOnlyList<string> Strings(JsonElement element, string name) => Json.Strings(element, name, keepEmpty: true);

    /// <summary>
    /// Whether this client's host knows a closure graph's operations and doors (FABRIC-CLOSE1b's handshake): <c>/api/status</c>'s
    /// <c>closure</c> and (CLOSECAP1) <c>closeOutcome</c>, the outcome door's recovery: both must be answered. A host from before
    /// answers none, or <c>closure</c> alone, and is not asked to keep what it cannot recover.
    /// </summary>
    public async Task<bool> KnowsClosureAsync(CancellationToken ct = default) =>
        (await SendAsync(new ReadStatus(), ct).ConfigureAwait(false)).Answered() is { Closure: true, CloseOutcome: true };

    /// <summary>
    /// Whether this client's host judges a bind in a workspace synced through a remote by the remote's handshake (FABRIC-CLOSE1i):
    /// <c>/api/status</c>'s <c>closeRemote</c>, beside the two <see cref="KnowsClosureAsync"/> reads. A host before it answers none, and is
    /// asked no bind there.
    /// </summary>
    public async Task<bool> KnowsRemoteClosureAsync(CancellationToken ct = default) =>
        (await SendAsync(new ReadStatus(), ct).ConfigureAwait(false)).Answered() is { Closure: true, CloseOutcome: true, CloseRemote: true };

    // The ports' halves the quest doors answer, sent through QuestViews.
    Task<RequestPublished> IRequestPort.PublishAsync(RequestAsk ask, CancellationToken ct) => this.PublishAsync(ask, ct);

    Task<(bool Ok, string Message)> IClosureBindPort.BindQuestAsync(string id, string graph, string session, CancellationToken ct) =>
        this.BindQuestAsync(id, graph, session, ct);

    Task<QuestCloseAnswer> IQuestClosePort.CloseQuestsAsync(
        string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct) =>
        QuestPersonViews.CloseQuestsAsync(this, quest, key, token, quests, ct);

    Task<QuestCloseAnswer> IQuestClosePort.CloseOutcomeAsync(
        string quest, string key, string token, IReadOnlyList<QuestCloseItem> quests, CancellationToken ct) =>
        QuestPersonViews.CloseOutcomeAsync(this, quest, key, token, quests, ct);

    /// <summary>
    /// Every session record, closed ones included, as the service answers them, with this client's key: what the session
    /// list's reader and a session delete read (SESSUX1a, SESSUX1f).
    /// </summary>
    public async Task<IReadOnlyList<SessionRecord>> SessionRecordsAsync(CancellationToken ct = default) =>
        SessionRecords.Parse(await SessionRecordsJsonAsync(ct).ConfigureAwait(false));

    /// <summary>The same records as the service wrote them, for a reader that reads the strikes and the last runs from them too.</summary>
    public Task<string> SessionRecordsJsonAsync(CancellationToken ct = default) => SessionsJsonAsync(true, ct);

    /// <summary>
    /// One quest's handback as the service wrote it (HANDBACK1, <c>GET /api/handbacks/{id}</c>), by the quest's id or the id of the session
    /// that served it: what <c>sessions handback</c> prints. Null from a host from before the door, which answers the route not found; the
    /// door answers every id, one naming nothing as not found in its own answer, so its own answer is never a 404.
    /// </summary>
    /// <exception cref="DriverException">The host answered the door with a failure.</exception>
    public async Task<string?> HandbackJsonAsync(string id, CancellationToken ct = default)
    {
        var answer = await _doors.SendAsync(
            ServiceVerb.Get, $"/api/handbacks/{Uri.EscapeDataString(id.TrimStart('#'))}", null, ct).ConfigureAwait(false);
        if (answer.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!answer.Ok) throw new DriverException($"the service would not read `{id}`'s handback: {answer.Payload}");
        return answer.Payload;
    }

    /// <summary>
    /// The ledger's judgement of deleting a session's record (SESSUX1f, D126 §5.4), deleting nothing: whether it would, and
    /// if not, its sentence, its word and the facts the word names.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, which answers no judgement.</exception>
    public Task<SessionDeleteAnswer> JudgeSessionDeleteAsync(string id, CancellationToken ct = default) =>
        SessionDeleteAsync(new JudgeSessionDelete(id), ct);

    /// <summary>
    /// Delete a session's record (SESSUX1f): the ledger judges it again as it deletes, so a record that moved since is
    /// refused as it now stands. A refusal is an answer, not an exception.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, which answers no judgement.</exception>
    public Task<SessionDeleteAnswer> DeleteSessionAsync(string id, CancellationToken ct = default) =>
        SessionDeleteAsync(new DeleteSession(id), ct);

    private async Task<SessionDeleteAnswer> SessionDeleteAsync<TAnswer>(IRoute<TAnswer> route, CancellationToken ct) where TAnswer : class
    {
        var (verb, path, _) = RouteRequest.Of(route);
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
        var turns = Ending.Contains(state) ? await TurnLinesForAsync(id, ct).ConfigureAwait(false) : null;
        var request = new AdvanceSession(
            id, state, note, evidence, transcript, interrupted ? true : null, limit ? true : null,
            note is not null && parts is { Count: > 0 } ? PartsElement(parts) : null,
            expected, unbindAsk ? true : null, expect is null ? null : [.. expect],
            turns?.ToDictionary(
                pair => pair.Key,
                pair => new MemoryTurnsRequest(
                    [.. pair.Value.Select(line => new MemoryTurnLine(line.Seq, line.Started, line.Ended, line.Model))], []),
                StringComparer.Ordinal));

        var (ok, _, payload, root) = await SendJsonAsync(request, ct).ConfigureAwait(false);

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
            await SessionEnds.Ended(Prior(record)).ConfigureAwait(false);
        Raise(Moved, new SessionMoved(id, (root is { } moved ? StateOf(moved) : null) ?? state));
        return root is { } answer ? Text(answer, "message") ?? "" : "";
    }

    /// <summary>
    /// The usage history's turn lines, by session, for the sessions named (KNOWCENTRAL2d): set where this client's home is known, so a
    /// session's end hands the service the lines its memory rows' models are read from. Null hands none, and the rows name <c>unknown</c>.
    /// </summary>
    public Func<IReadOnlyCollection<string>, IReadOnlyDictionary<string, IReadOnlyList<TurnLine>>>? TurnLines { get; init; }

    /// <summary>One turn line as the service is handed it: the fields its memory rows' models are read from.</summary>
    public sealed record TurnLine(long Seq, DateTimeOffset? Started, DateTimeOffset Ended, string Model);

    /// <summary>Maps the usage history's kept turns onto the lines this client hands the service.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<TurnLine>> LinesOf<T>(
        IReadOnlyDictionary<string, IReadOnlyList<T>> kept, Func<T, TurnLine> line) =>
        kept.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<TurnLine>)[.. pair.Value.Select(line)], StringComparer.Ordinal);

    /// <summary>The states a session's record ends in: where its staged memory's models are named (the memory coordinator design §1).</summary>
    private static readonly string[] Ending = ["completed", "stopped", "failed", "declined", "stood-down"];

    /// <summary>
    /// The turn lines of <paramref name="id"/> and of each session it continues, as its record names them; null where none is known or the
    /// records did not read, which costs the rows their models and never the move.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<TurnLine>>?> TurnLinesForAsync(string id, CancellationToken ct)
    {
        if (TurnLines is null) return null;
        var chain = new List<string> { id };
        try
        {
            var records = ReadRecords(await SessionsJsonAsync(true, ct).ConfigureAwait(false));
            // A chain longer than any hand-over makes is cut: the lines are evidence, never a reason to fail.
            while (chain.Count < 16 && records.GetValueOrDefault(chain[^1])?.From is { Length: > 0 } from && !chain.Contains(from, StringComparer.OrdinalIgnoreCase))
            {
                chain.Add(from);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The session's own lines still go.
        }

        var lines = TurnLines(chain);
        return lines.Count == 0 ? null : lines;
    }

    /// <summary>The note's parts as the record's JSON spells them, for a route that carries them beside the note.</summary>
    private static JsonElement PartsElement(IReadOnlyList<NotePart> parts)
    {
        using var document = JsonDocument.Parse(WriteJson(writer => NotePart.Write(writer, parts)));
        return document.RootElement.Clone();
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
        var (ok, _, payload, root) = await SendJsonAsync(
            new RestoreSessionPark(
                id, [.. takeBack], noted?.Note, noted is { Parts: { Count: > 0 } parts } ? PartsElement(parts) : null),
            ct).ConfigureAwait(false);
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
    internal Task<string> GetAsync(string path, CancellationToken ct) => _doors.GetAsync(path, ct);

    internal static IReadOnlyList<RepoView> ReadRegistry(string json)
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
                RemoteTarget.Workspace(Text(repo, "workspace")))
            {
                // The lanes it declares, by id (D115 §2.2), from the answer DeclarationsAsync reads their words from; absent is none.
                Lanes = repo.TryGetProperty("lanes", out var lanes) && lanes.ValueKind == JsonValueKind.Array
                    ? [.. lanes.EnumerateArray().Where(lane => lane.ValueKind == JsonValueKind.Object).Select(lane => Text(lane, "id")).OfType<string>().Where(id => id.Length > 0)]
                    : [],
            });
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
        using var document = JsonDocument.Parse(await SessionsJsonAsync(true, ct).ConfigureAwait(false));

        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (Text(session, "id") == id) return Text(session, "quest") is { Length: > 0 } quest ? quest : null;
        }

        return null;
    }

    public async Task<(string? Tree, string? BaseCommit)> SessionGroundAsync(
        string id, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await SessionsJsonAsync(true, ct).ConfigureAwait(false));

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
        var sessions = new List<SessionView>();
        var listed = JsonSerializer.Deserialize(json, ContractJson.Options.GetTypeInfo(typeof(SessionResponse[]))) as SessionResponse[];
        foreach (var session in listed ?? [])
        {
            if (session.Id?.Contains('/') == true) continue;
            sessions.Add(new SessionView(session.Id ?? "", session.Repository ?? "", session.State ?? "", session.Note)
            {
                Ask = session.Ask,
                Tree = session.Tree,
                Quest = session.Quest,
                // Where its tree stood when it opened: what the sweep reads a lost done's evidence as changed from.
                BaseCommit = session.BaseCommit is { Length: > 0 } baseCommit ? baseCommit : null,
                // Its note's lines by code, handed on wherever the note is; null for a record from before parts.
                NoteParts = session.NoteParts is { } parts ? NotePart.ReadParts(parts) : null,
                // Its words, read as SessionRecords reads them: the person's make an answered park.
                Said = session.Said is { } said ? [.. said.Select(ReadWord).OfType<SaidWordView>()] : null,
                Workspace = RemoteTarget.Workspace(session.Workspace),
                // What the cap counts by: its kind, and the mark of each conversation the driver opens for itself, an intake's ask
                // above, a helper's serves and a reviewer's opinion. A conversation the person holds has none of them.
                Kind = session.Kind ?? SessionKinds.Driven,
                Opinion = session.Opinion is { Length: > 0 } opinion ? opinion : null,
                Serves = session.Serves is { Length: > 0 } serves ? serves : null,
                Piece = session.Piece is { Length: > 0 } piece ? piece : null,
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
        ReadRunning(await SessionsJsonAsync(false, ct).ConfigureAwait(false));

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
    /// its provider refused its credential (AGT3b), which its note says by an account's line (<see cref="RecordNotes.AccountsOwn"/>).
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
            if (NotePart.Read(session)?.Any(part => part.Code == RecordNotes.UpdateClosed) == true) continue;
            // AGENTERRORS1a (D210 §5.1): an end past a transient bound is its provider's outage, never the quest's: no strike, its
            // quest held for the person instead.
            if (NotePart.Read(session)?.Any(part => part.Code == RecordNotes.EndedTransient) == true) continue;
            // D205 §3.5(c): a choice its agent refused at the open, before any prompt, is the account's, never the quest's.
            if (NotOffered(session)) continue;
            if (!cutOff) continue;
            if (Text(session, "quest") is not { Length: > 0 } quest) continue;

            strikes[quest] = strikes.TryGetValue(quest, out var seen) ? seen + 1 : 1;
        }

        return strikes;
    }

    /// <summary>
    /// How many strikes in a row each quest's last choice has made (STAFFPOLICY1e2): the trailing failed records on the same agent and asked
    /// model, in the order they were made. A limit, a refused account, a transient end and a choice not offered are no strike and break
    /// nothing; any other end that is not a failure, or a failure on another choice, starts the count again. The policy's <c>failed</c>
    /// move with <c>after</c> weighs its tries against this.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> ReadFailedRuns(string json)
    {
        using var document = JsonDocument.Parse(json);
        var runs = new List<(string Quest, DateTimeOffset At, string State, string Choice)>();
        foreach (var session in document.RootElement.EnumerateArray())
        {
            if (IsTeams(session) || Text(session, "quest") is not { Length: > 0 } quest) continue;
            var state = Text(session, "state") ?? "";
            if (state is "stood-down" or "") continue;
            var parts = NotePart.Read(session);
            if (Limit(session) || AccountRefused(session) || NotOffered(session) || parts?.Any(part => part.Code is RecordNotes.EndedTransient or RecordNotes.UpdateClosed) == true) continue;
            var asked = ActorRead<ActorAsked>(session, "asked");
            runs.Add((quest, Moments.Read(Text(session, "created")) ?? DateTimeOffset.MinValue, state, $"{Text(session, "adapter")}|{asked?.NamedModel}|{asked?.Effort.Value}"));
        }

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var choices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs.OrderBy(each => each.At))
        {
            if (!string.Equals(run.State, "failed", StringComparison.OrdinalIgnoreCase))
            {
                counts[run.Quest] = 0;
                continue;
            }

            counts[run.Quest] = choices.TryGetValue(run.Quest, out var was) && was == run.Choice && counts.GetValueOrDefault(run.Quest) > 0 ? counts[run.Quest] + 1 : 1;
            choices[run.Quest] = run.Choice;
        }

        return counts.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
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
        object? changed = null, CancellationToken ct = default, ActorChosen? chosen = null)
    {
        JsonElement? Wired(object? value) => value is null ? null : JsonSerializer.SerializeToElement(value, ActorWire.Json);
        var (ok, status, payload, _) = await SendJsonAsync(
            new KeepActor(id, choice, Wired(asked), Wired(changed), Wired(ran), Wired(chosen)), ct).ConfigureAwait(false);
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
        object? changed = null, CancellationToken ct = default, ActorChosen? chosen = null)
    {
        try
        {
            await KeepActorAsync(id, choice, asked, changed: changed, ct: ct, chosen: chosen).ConfigureAwait(false);
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
        ReadRecord(await SessionsJsonAsync(true, ct).ConfigureAwait(false), id);

    /// <summary>
    /// The trees of this machine's records the person's words wait on (AUTOTIDY2d, MSG1b, D137 §2.2): a record that ended or
    /// parked with words a later run goes on with in its own tree, which the look's own tidy keeps as it keeps a session's in use.
    /// </summary>
    public async Task<IReadOnlyList<string>> WaitingTreesAsync(CancellationToken ct = default) =>
        WaitingTrees(await SessionsJsonAsync(true, ct).ConfigureAwait(false));

    /// <summary>
    /// This machine's records, open and closed, by id (<see cref="ReadRecords"/>): what the person's <i>Send to its session</i> finds a
    /// line of work's newest part here among (COWORK1d). A teammate's is none here.
    /// </summary>
    public async Task<IReadOnlyCollection<PriorSession>> RecordsAsync(CancellationToken ct = default) =>
        [.. ReadRecords(await SessionsJsonAsync(true, ct).ConfigureAwait(false)).Values];

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
            Text(session, "repository"), Interrupted(session))
        {
            // Which account it ran on and whether a limit cut it off (TOOL4f): what a carry-on is compared with and told.
            Profile = Text(session, "profile"),
            Actor = Text(session, "actor"),
            Choice = Text(session, "choice"),
            Asked = ActorRead<ActorAsked>(session, "asked"),
            Override = ActorRead<JsonElement?>(session, "override"),
            Ran = ActorRead<ActorRan>(session, "ran"),
            Chosen = ActorRead<ActorChosen>(session, "chosen"),
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
            TookQuest = Text(session, "tookQuest") is { Length: > 0 } took ? took : null,
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
    internal static IReadOnlyList<SaidWordView>? ReadSaid(JsonElement session)
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
            ? ReadWord(new SaidWordResponse(
                id, words, at, Strings(word, "files"), Flag(word, "reopens"), Text(word, "by"), Text(word, "helper"), Text(word, "from"),
                Text(word, "key"), Text(word, "level"), Moment(word, "attempt"), Text(word, "decision")))
            : null;

    /// <summary>
    /// A word as the typed answer carries it, with the same reading: the marks that say whose it is are absent when blank, a helper's
    /// answer is its own mark (never <c>by</c>), and a host from before a mark says none, so its word is the person's.
    /// </summary>
    internal static SaidWordView? ReadWord(SaidWordResponse word) =>
        word.Id is { Length: > 0 } id && word.Text is { } words && word.At != default
            ? new SaidWordView(id, words, word.At, word.Files ?? [], word.Reopens)
            {
                By = word.By is { Length: > 0 } by ? by : null,
                Helper = word.Helper is { Length: > 0 } helper ? helper : null,
                From = word.From is { Length: > 0 } from ? from : null,
                Key = word.Key is { Length: > 0 } key ? key : null,
                Level = MessageLevels.Read(word.Level),
                Attempt = word.Attempt,
                Decision = word.Decision is { Length: > 0 } decision ? decision : null,
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
            .. QuestViews.Read(json).Where(quest =>
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
        && parts.Any(part => part.Code is { } code && RecordNotes.AccountsOwn.Contains(code));

    /// <summary>
    /// Whether a record ended <c>choice-not-offered</c> (D205 §3.5(c), MODELROLE1c2), read by its end code: its agent refused the choice at
    /// the open, before any prompt, so it is no strike, no last run and no start the walk counts.
    /// </summary>
    internal static bool NotOffered(JsonElement session) =>
        NotePart.Read(session)?.Any(part => part.Code == RecordNotes.EndedChoiceNotOffered) == true;

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
        return Parsed(await SendAsync(ServiceVerb.Post, path, body, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// A declared route sent as its record, its answer read as <see cref="PostJsonAsync"/> reads one: for the doors whose answers
    /// are read by their fields, a refusal's included.
    /// </summary>
    internal async Task<(bool Ok, int Status, string Payload, JsonElement? Root)> SendJsonAsync<TAnswer>(
        IRoute<TAnswer> route, CancellationToken ct) where TAnswer : class
    {
        var (verb, path, json) = RouteRequest.Of(route);
        return Parsed(await SendAsync(verb, path, json, ct).ConfigureAwait(false));
    }

    private static (bool Ok, int Status, string Payload, JsonElement? Root) Parsed(ServiceReply answer)
    {
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
    /// <param name="route">The door's request.</param>
    /// <param name="read">The record and the sentence its answer carries.</param>
    /// <param name="kind">Which door this is, for the watchers (<see cref="SessionOpened"/>).</param>
    /// <param name="adapter">The adapter asked for — the record's own, when the answer carries one, wins.</param>
    /// <param name="repository">The repository asked for, where the door names one — the record's own wins.</param>
    /// <param name="setup">That a driven session's quest is a set-up, for the watchers (WSSETUP11).</param>
    private async Task<(string? SessionId, string Message)> OpenRecordAsync<TAnswer>(
        IRoute<TAnswer> route, Func<TAnswer, (SessionResponse? Session, string? Message)> read, string kind, string adapter,
        string? repository, CancellationToken ct, bool setup = false) where TAnswer : class
    {
        var sent = await SendAsync(route, ct).ConfigureAwait(false);
        if (sent.Answer is not { } answer)
        {
            return (null, QuestViews.Error(sent) ?? $"the service at {_base} has no `{RouteDeclaration.Of(route.GetType()).Path}` door ({sent.Reply.Status}) — is it older than this driver?");
        }

        var (session, message) = read(answer);
        if (session?.Id is { } id)
        {
            // What the ledger recorded rather than what was asked: a driven session names its quest, and
            // the record answers with the quest's receiver; a blank adapter takes the ledger's default. The
            // workspace is the record's too, the circle its repository is wired into on this machine (D48).
            Raise(Opened, new SessionOpened(id, kind, session.Adapter ?? adapter, session.Repository ?? repository)
            {
                Workspace = session.Workspace,
                Setup = setup,
            });
        }

        return (session?.Id, message ?? "");
    }

    internal static string WriteJson(Action<Utf8JsonWriter> write)
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

    /// <summary>The units a scope holds, each judged by the service, deleting nothing: <paramref name="plan"/> names the one scope.</summary>
    /// <exception cref="DriverException">A host older than the door, or one that refused the scope, in its words.</exception>
    public async Task<IReadOnlyList<HistoryUnitView>> HistoryAsync(PlanHistory plan, CancellationToken ct = default)
    {
        var (ok, status, payload, _) = await SendJsonAsync(plan, ct).ConfigureAwait(false);
        var root = Answered(ok, status, payload);
        return [.. Items(root, "units").Select(ReadHistoryUnit)];
    }

    /// <summary>
    /// The second press: exactly <paramref name="units"/>, each judged again by the service and cleared in one transaction of
    /// its own, or kept with its word.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, or one that refused the press, in its words.</exception>
    public async Task<IReadOnlyList<HistoryClearedView>> ClearHistoryAsync(IReadOnlyList<HistoryUnitName> units, CancellationToken ct = default)
    {
        var (ok, status, payload, _) = await SendJsonAsync(
            new ClearHistory([.. units.Select(unit => new HistoryUnitRequest(unit.Kind, unit.Id))]), ct).ConfigureAwait(false);
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

    // What the review's showing reads of a session's record (REVIEWENV1d): the tree its set-up was built in.

    /// <summary>
    /// The tree a session of this machine's held (D51), closed records included, as the records answer it on loopback; null where
    /// the service holds no such record or says no tree. What <i>Show it again</i> serves a set-up's folder from.
    /// </summary>
    public async Task<string?> SessionTreeAsync(string session, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await SessionsJsonAsync(true, ct).ConfigureAwait(false));
        if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
        return document.RootElement.EnumerateArray()
            .Where(record => record.ValueKind == JsonValueKind.Object
                             && string.Equals(Text(record, "id"), session, StringComparison.OrdinalIgnoreCase))
            .Select(record => Text(record, "tree"))
            .FirstOrDefault(tree => tree is { Length: > 0 });
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
