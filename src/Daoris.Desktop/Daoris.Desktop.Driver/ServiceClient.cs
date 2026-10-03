using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The driver's half of the conversation with the service — a client through the same doors as every
/// other component (D46 §7). Deliberately NOT a reference to the service's assemblies: the doors are
/// the contract, and a driver that could reach the store directly would drift from every client that
/// cannot.
/// </summary>
public sealed class ServiceClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _base;

    public ServiceClient(string baseUrl, string? key, HttpClient? http = null)
    {
        _base = baseUrl.TrimEnd('/');
        _http = http ?? DriverHttp.Client(key);
        if (http is not null && !string.IsNullOrWhiteSpace(key))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
    }

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

    /// <summary>Where the service is — handed to sessions so they can claim their own quests there.</summary>
    public string BaseUrl => _base;

    /// <summary>URL and key from the environment — the same two names `connect` reads (design §5b).</summary>
    public static ServiceClient FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new DriverException(
                $"no {UrlVariable} — the driver watches a service, and needs to know where one is. "
                + "A local host is usually http://localhost:5177.");
        }

        return new ServiceClient(url, Environment.GetEnvironmentVariable(KeyVariable));
    }

    public void Dispose() => _http.Dispose();

    /// <summary>Everything one tick decides from, fetched together so the plan is coherent.</summary>
    public async Task<Snapshot> SnapshotAsync(CancellationToken ct = default)
    {
        var quests = ReadQuests(await GetAsync("/api/quests", ct).ConfigureAwait(false));
        var repositories = ReadRegistry(await GetAsync("/api/registry", ct).ConfigureAwait(false));
        var active = ReadSessions(await GetAsync("/api/sessions", ct).ConfigureAwait(false));
        // A second read rather than deriving both from one (DRV6): `/api/sessions` means ACTIVE, the
        // planner's "is this repository busy" rests on that, and re-deriving active-ness here would
        // put a second opinion about it on this side of the wire.
        var records = await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false);
        var lastRun = ReadLastRun(records);
        // The closed quests a session written to served (MSG1b, D137 §2.2), read only where one waits: no open list holds them.
        var open = quests.Select(quest => quest.Id).ToList();
        var closed = lastRun.Any(run => run.Value.WordsWaiting && !open.Contains(run.Key, StringComparer.OrdinalIgnoreCase))
            ? ReadClosed(await GetAsync("/api/quests?includeClosed=true", ct).ConfigureAwait(false), lastRun, open)
            : [];
        return new Snapshot(quests, repositories, active, ReadStrikes(records))
        {
            LastRun = lastRun,
            Started = ReadStarted(records, active),
            Closed = closed,
        };
    }

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
        using var answer = JsonDocument.Parse(await DriverHttp.PostAsync(
            _http, $"{_base}/api/quests/{Uri.EscapeDataString(quest.TrimStart('#'))}/conflicts/dismiss", "{}", ct)
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
    public async Task<SayAnswer> SayAsync(string id, string text, IReadOnlyList<string>? files = null, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("text", text);
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

    /// <summary>Ask the ledger to queue a session. A refusal is an answer, not an exception.</summary>
    /// <param name="setup">
    /// That the quest is a set-up (<see cref="SetupQuests"/>), for the watchers alone (WSSETUP11): the
    /// ledger is not told, and the machine log marks the session's start.
    /// </param>
    public async Task<(string? SessionId, string Message)> OpenSessionAsync(
        string questId, string adapter, string? harnessVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default, bool setup = false)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("quest", questId);
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
            writer.WriteEndObject();
        });

        return await OpenRecordAsync("/api/sessions", body, SessionOpened.Driven, adapter, repository: null, ct, setup).ConfigureAwait(false);
    }

    /// <summary>
    /// Ask the ledger to open a CHAT in a repository (D49 §3) — a person's session, serving no quest
    /// yet. A refusal is an answer, not an exception: the usual one is that the repository is busy,
    /// and the sentence names what holds it.
    /// </summary>
    public async Task<(string? SessionId, string Message)> OpenChatAsync(
        string repository, string adapter, string? harnessVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("repository", repository);
            writer.WriteString("adapter", adapter);
            if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
            if (profile is not null) writer.WriteString("profile", profile);
            if (tree is not null) writer.WriteString("tree", tree);
            if (baseCommit is not null) writer.WriteString("baseCommit", baseCommit);
            writer.WriteEndObject();
        });

        return await OpenRecordAsync("/api/sessions/chat", body, SessionOpened.Chat, adapter, repository, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Make an ask at a workspace (D65 §1a) — the sentence, its links, and files read from this
    /// machine. The service answers with what became of it; a refusal is an answer, not an exception.
    /// </summary>
    public Task<AskAnswer> AskAsync(
        string workspace, string sentence, IReadOnlyList<string> links,
        IReadOnlyList<(string Name, byte[] Content)> files, string? to, CancellationToken ct = default) =>
        PostAskAsync("/api/asks", writer =>
        {
            writer.WriteString("workspace", workspace);
            writer.WriteString("sentence", sentence);
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
    /// run it in. A refusal is an answer, not an exception — usually that one is already running.
    /// </summary>
    public async Task<(string? SessionId, string Message)> OpenHelpAsync(
        string adapter, string room, string? harnessVersion = null, string? profile = null,
        CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("adapter", adapter);
            writer.WriteString("room", room);
            if (harnessVersion is not null) writer.WriteString("harnessVersion", harnessVersion);
            if (profile is not null) writer.WriteString("profile", profile);
            writer.WriteEndObject();
        });

        return await OpenRecordAsync("/api/sessions/help", body, SessionOpened.Help, adapter, repository: null, ct).ConfigureAwait(false);
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
        using var response = await _http.GetAsync(
            $"{_base}/api/asks/{Uri.EscapeDataString(id.TrimStart('#'))}", ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new DriverException($"the service would not say what became of ask `#{id}`: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        return ReadAsk(document.RootElement);
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
    private static DateTimeOffset? Moment(JsonElement element, string name) =>
        DateTimeOffset.TryParse(Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;

    private static AskView ReadAsk(JsonElement ask) =>
        new(
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
            Proposed = ask.TryGetProperty("proposal", out var proposal) && proposal.ValueKind == JsonValueKind.Array
                ? [.. proposal.EnumerateArray().Select(match => Text(match, "repository")).OfType<string>()]
                : [],
            Deletable = Flag(ask, "deletable"),
            // The person's words (DRIFT1a), which every session on the ask is handed (DRIFT1b).
            Words = ReadWords(ask),
            WordsKeptFrom = Moment(ask, "wordsKeptFrom"),
            // The go-aheads its sessions asked (KNOWUSE1a), which every session on the ask is handed beside the words.
            GoAheads = ReadGoAheads(ask),
        };

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
    public async Task<(bool Ok, string Message)> AnswerGoAheadAsync(
        string ask, int number, bool approved, string? words, CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("answer", approved ? "approved" : "refused");
            if (!string.IsNullOrWhiteSpace(words)) writer.WriteString("words", words);
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

    /// <summary>A field that is true, or false when it is anything else or absent.</summary>
    private static bool Flag(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : [];

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
    /// Every session record, closed ones included, as the service answers them, with this client's key: what the session
    /// list's reader and a session delete read (SESSUX1a, SESSUX1f).
    /// </summary>
    public async Task<IReadOnlyList<SessionRecord>> SessionRecordsAsync(CancellationToken ct = default) =>
        SessionRecords.Parse(await SessionRecordsJsonAsync(ct).ConfigureAwait(false));

    /// <summary>The same records as the service wrote them, for a reader that reads the strikes and the last runs from them too.</summary>
    public Task<string> SessionRecordsJsonAsync(CancellationToken ct = default) => GetAsync(SessionRecords.Door, ct);

    /// <summary>
    /// The ledger's judgement of deleting a session's record (SESSUX1f, D126 §5.4), deleting nothing: whether it would, and
    /// if not, its sentence, its word and the facts the word names.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, which answers no judgement.</exception>
    public Task<SessionDeleteAnswer> JudgeSessionDeleteAsync(string id, CancellationToken ct = default) =>
        SessionDeleteAsync(HttpMethod.Get, $"/api/sessions/{Uri.EscapeDataString(id)}/deletable", ct);

    /// <summary>
    /// Delete a session's record (SESSUX1f): the ledger judges it again as it deletes, so a record that moved since is
    /// refused as it now stands. A refusal is an answer, not an exception.
    /// </summary>
    /// <exception cref="DriverException">A host older than the door, which answers no judgement.</exception>
    public Task<SessionDeleteAnswer> DeleteSessionAsync(string id, CancellationToken ct = default) =>
        SessionDeleteAsync(HttpMethod.Delete, $"/api/sessions/{Uri.EscapeDataString(id)}", ct);

    private async Task<SessionDeleteAnswer> SessionDeleteAsync(HttpMethod method, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{_base}{path}");
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
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
        var taken = response.IsSuccessStatusCode && root.ValueKind == JsonValueKind.Object && refusal is null;
        if (!taken && refusal is null)
        {
            throw new DriverException(
                $"the service at {_base} has no session delete door ({(int)response.StatusCode}) — is it older than this driver?");
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
        using var response = await _http.DeleteAsync($"{_base}{path}", ct).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            return response.IsSuccessStatusCode
                ? (true, Text(root, "message") ?? "")
                : (false, Text(root, "error") ?? payload);
        }
        catch (JsonException)
        {
            // A host older than the delete door answers a bare 404 or 405 — said plainly, not parsed as nothing.
            return (false, $"the service at {_base} has no delete door ({(int)response.StatusCode}) — is it older than this driver?");
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
    public async Task<string> AdvanceAsync(
        string id, string state, string? note = null, string? evidence = null, string? transcript = null,
        CancellationToken ct = default, bool interrupted = false, bool limit = false)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("state", state);
            if (note is not null) writer.WriteString("note", note);
            if (evidence is not null) writer.WriteString("evidence", evidence);
            if (transcript is not null) writer.WriteString("transcript", transcript);
            if (interrupted) writer.WriteBoolean("interrupted", true);
            if (limit) writer.WriteBoolean("limit", true);
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

        Raise(Moved, new SessionMoved(id, (root is { } moved ? StateOf(moved) : null) ?? state));
        return root is { } answer ? Text(answer, "message") ?? "" : "";
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

    // Through DriverHttp, so a refused read carries the service's own sentence — a bare
    // EnsureSuccessStatusCode threw it away, and the driver's log said less than the service said.
    private Task<string> GetAsync(string path, CancellationToken ct) =>
        DriverHttp.GetAsync(_http, $"{_base}{path}", ct);

    private static IReadOnlyList<QuestView> ReadQuests(string json)
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
                        Text(step, "to") ?? "", Text(step, "title") ?? "", Text(step, "body") ?? "")).ToList()
                    : [],
                Parent = Text(quest, "parent"),
                // Absent is not waiting: a host from before D79 answers without it.
                Awaits = Text(quest, "awaits"),
                // Absent is a person's publish or a chain's step: a host from before SESS1 answers without it.
                PublishedBy = Text(quest, "publishedBy") is { Length: > 0 } by ? by : null,
                Note = Text(quest, "note"),
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
                        .Select(r => new QuestRequirementView(Text(r, "quote") ?? "", Text(r, "check") ?? "")).ToList()
                    : [],
                // Whether a departure holds it for the person's yes (DRIFT1d). Absent is false: a host from before answers.
                Held = Flag(quest, "held"),
            });
        }

        return quests;
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
    private static IReadOnlyList<SessionView> ReadSessions(string json)
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
            });
        }

        return sessions;
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
            var cutOff = (string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase) && !Limit(session))
                         || (string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase) && Interrupted(session));
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

            var at = DateTimeOffset.TryParse(Text(session, "created"), out var created) ? created : DateTimeOffset.MinValue;
            if (last.TryGetValue(quest, out var seen) && seen.At > at) continue;
            last[quest] = (Prior(session), at);
        }

        return last.ToDictionary(pair => pair.Key, pair => pair.Value.Session, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One record as a run that goes on reads it (MSG1c): its state, its words waiting, the adapter, account, tree and
    /// version it ran on, its note and its flags, and its kind. Null when no record of this machine has that id.
    /// </summary>
    public async Task<PriorSession?> RecordAsync(string id, CancellationToken ct = default) =>
        ReadRecord(await GetAsync(SessionRecords.Door, ct).ConfigureAwait(false), id);

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

    /// <summary>A record as what a later run on it is compared with and told (D79, D80, ANSWER1a, MSG1b).</summary>
    private static PriorSession Prior(JsonElement session) =>
        new(
            Text(session, "id") ?? "", Text(session, "tree"), Text(session, "state") ?? "", Text(session, "note"),
            Text(session, "repository"), Text(session, "answer"), Interrupted(session))
        {
            // Which account it ran on and whether a limit cut it off (TOOL4f): what a carry-on is compared with and told.
            Profile = Text(session, "profile"),
            Limit = Limit(session),
            // What an answered park is compared with, and where its evidence counts from (ANSWER1a, D131).
            Adapter = Text(session, "adapter"),
            HarnessVersion = Text(session, "harnessVersion"),
            BaseCommit = Text(session, "baseCommit"),
            // The person's words waiting on it (MSG1a): what goes on with it, parked or ended (MSG1b, D137 §2.2).
            Said = ReadSaid(session),
            Ask = Text(session, "ask"),
            // A chat goes on through the chat runner, never the planner (MSG1c).
            Kind = Text(session, "kind") ?? "driven",
        };

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
    /// the record ended. Null for one without its id, its words or a moment that reads.
    /// </summary>
    private static SaidWordView? ReadWord(JsonElement word) =>
        word.ValueKind == JsonValueKind.Object
        && Text(word, "id") is { Length: > 0 } id && Text(word, "text") is { } words && Moment(word, "at") is { } at
            ? new SaidWordView(id, words, at, Strings(word, "files"), Flag(word, "reopens"))
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
                DateTimeOffset.TryParse(Text(session, "created"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var created) ? created : null,
                running.Contains(Text(session, "id") ?? "")));
        }

        return started;
    }

    /// <summary>Whether a record says its stop was not the person's (D104) — absent is false, the old reading.</summary>
    private static bool Interrupted(JsonElement session) =>
        session.TryGetProperty("interrupted", out var interrupted) && interrupted.ValueKind == JsonValueKind.True;

    /// <summary>Whether a record says an account's limit made its failure (TOOL4c) — absent is false, the old reading.</summary>
    private static bool Limit(JsonElement session) =>
        session.TryGetProperty("limit", out var limit) && limit.ValueKind == JsonValueKind.True;

    /// <summary>A record that came down from the team — keyed `origin/id`, the id this machine's own never has.</summary>
    internal static bool IsTeams(JsonElement session) => Text(session, "id")?.Contains('/') == true;

    /// <summary>
    /// POST a JSON body and read the answer: whether it succeeded, its status, its text, and its root
    /// when the text is JSON. Every write this client makes goes through here (REV3 CLEAN1: five
    /// callers each wrote it out, and two of them threw on an answer that was not JSON).
    /// </summary>
    private async Task<(bool Ok, int Status, string Payload, JsonElement? Root)> PostJsonAsync(
        string path, string body, CancellationToken ct)
    {
        using var response = await _http.PostAsync(
            $"{_base}{path}", new StringContent(body, Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

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

        return (response.IsSuccessStatusCode, (int)response.StatusCode, payload, root);
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

    // An element that is not an object has no fields — asked as one, TryGetProperty would throw.
    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
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

/// <summary>
/// What the service said of the person's words to a parked or ended session of this machine's (MSG1a's say door, MSG1d):
/// whether it kept them for the session to go on with, and its sentence.
/// </summary>
/// <param name="Kept">The words are on the record, waiting for it to go on with them.</param>
/// <param name="Message">The service's sentence: its yes, or its refusal, verbatim.</param>
public sealed record SayAnswer(bool Kept, string Message)
{
    /// <summary>
    /// The refusal's word, which a reader acts on instead of the sentence: <c>running</c>, <c>not-ours</c>, <c>intake</c>,
    /// <c>stood-down</c>, <c>not-found</c> or <c>no-words</c>. Null when kept, and for a host with no door.
    /// </summary>
    public string? Refusal { get; init; }

    /// <summary>The word as kept: its id, which the record's events say again where the session took it. Null when refused.</summary>
    public SaidWordView? Word { get; init; }
}

/// <summary>
/// What the ledger said of deleting a session's record (SESSUX1f, D126 §5.4): whether it did, or would; and if not its
/// sentence, its word (<c>not-found</c>, <c>not-ours</c>, <c>live</c>, <c>served-quest</c>, <c>named</c>,
/// <c>on-remote</c>), which a reader acts on instead of the sentence, and the facts the word names.
/// </summary>
/// <param name="Taken">It was deleted, or the judgement says it would be.</param>
/// <param name="Message">The ledger's sentence: its yes, or its refusal, verbatim.</param>
public sealed record SessionDeleteAnswer(bool Taken, string Message)
{
    public string? Refusal { get; init; }
    public string? Quest { get; init; }
    public string? Ask { get; init; }
    public string? Origin { get; init; }
    public string? Workspace { get; init; }
}

/// <summary>What the service said to an ask, a publish or a close (D65 §1a).</summary>
/// <param name="Ok">Whether it did what was asked — false is a refusal, said in <paramref name="Message"/>.</param>
/// <param name="Message">The service's whole sentence, verbatim.</param>
/// <param name="AskId">The ask it answered about, when it answered with one.</param>
/// <param name="QuestId">The quest this call published, when it published one.</param>
public sealed record AskAnswer(bool Ok, string Message, string? AskId, string? QuestId);
