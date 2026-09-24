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
        var strikes = ReadStrikes(await GetAsync("/api/sessions?includeClosed=true", ct).ConfigureAwait(false));
        return new Snapshot(quests, repositories, active, strikes);
    }

    /// <summary>This machine's active sessions — the one read an orphan sweep needs, without a whole snapshot.</summary>
    public async Task<IReadOnlyList<SessionView>> ActiveSessionsAsync(CancellationToken ct = default) =>
        ReadSessions(await GetAsync("/api/sessions", ct).ConfigureAwait(false));

    /// <summary>One quest's current status, closed ones included — how a session's end is observed.</summary>
    public async Task<string?> QuestStatusAsync(string id, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(
            await GetAsync("/api/quests?includeClosed=true", ct).ConfigureAwait(false));
        foreach (var quest in document.RootElement.EnumerateArray())
        {
            if (string.Equals(Text(quest, "id"), id, StringComparison.OrdinalIgnoreCase))
            {
                return Text(quest, "status");
            }
        }

        return null;
    }

    /// <summary>
    /// Where this machine's claim on a quest stands (D68 §4): none, held, unconfirmed or lost — how the
    /// driver learns that a session it is running took a quest another machine took first.
    /// </summary>
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

    public async Task<string> ClaimAsync(string id, CancellationToken ct = default) =>
        RemoteSyncPayloads.Claim(await GetAsync($"/api/quests/{Uri.EscapeDataString(id)}/claim", ct).ConfigureAwait(false));

    /// <summary>Ask the ledger to queue a session. A refusal is an answer, not an exception.</summary>
    public async Task<(string? SessionId, string Message)> OpenSessionAsync(
        string questId, string adapter, string? harnessVersion = null, string? profile = null,
        string? tree = null, string? baseCommit = null, CancellationToken ct = default)
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

        using var response = await _http.PostAsync(
            $"{_base}/api/sessions", new StringContent(body, Encoding.UTF8, "application/json"), ct)
            .ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);

        if (!response.IsSuccessStatusCode)
        {
            return (null, Text(document.RootElement, "error") ?? payload);
        }

        var session = document.RootElement.GetProperty("session");
        return (Text(session, "id"), Text(document.RootElement, "message") ?? "");
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

        using var response = await _http.PostAsync(
            $"{_base}/api/sessions/chat", new StringContent(body, Encoding.UTF8, "application/json"), ct)
            .ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);

        if (!response.IsSuccessStatusCode)
        {
            return (null, Text(document.RootElement, "error") ?? payload);
        }

        var session = document.RootElement.GetProperty("session");
        return (Text(session, "id"), Text(document.RootElement, "message") ?? "");
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

        using var response = await _http.PostAsync(
            $"{_base}/api/sessions/intake", new StringContent(body, Encoding.UTF8, "application/json"), ct)
            .ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return (null, $"the service at {_base} has no intake door ({(int)response.StatusCode}) — is it older than this driver?");
        }

        using (document)
        {
            if (!response.IsSuccessStatusCode) return (null, Text(document.RootElement, "error") ?? payload);
            return (Text(document.RootElement.GetProperty("session"), "id"), Text(document.RootElement, "message") ?? "");
        }
    }

    /// <summary>This machine's asks that are not closed, newest first — what the loop finds intakes in.</summary>
    public async Task<IReadOnlyList<AskView>> AsksAsync(CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(await GetAsync("/api/asks", ct).ConfigureAwait(false));
        return [.. document.RootElement.EnumerateArray().Select(ReadAsk)];
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
                Text(repo, "root"))),
        ];
    }

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
        };

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

    private async Task<AskAnswer> PostAskAsync(string path, Action<Utf8JsonWriter> write, CancellationToken ct)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        });

        using var response = await _http.PostAsync(
            $"{_base}{path}", new StringContent(body, Encoding.UTF8, "application/json"), ct)
            .ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            // A host older than asks answers a bare 404 — said plainly rather than parsed as nothing.
            return new AskAnswer(false, $"the service at {_base} has no ask door ({(int)response.StatusCode}) — is it older than this driver?", null, null);
        }

        using (document)
        {
            var root = document.RootElement;
            if (!response.IsSuccessStatusCode) return new AskAnswer(false, Text(root, "error") ?? payload, null, null);

            return new AskAnswer(
                true,
                Text(root, "message") ?? "",
                root.TryGetProperty("ask", out var ask) ? Text(ask, "id") : null,
                root.TryGetProperty("quest", out var quest) && quest.ValueKind == JsonValueKind.Object ? Text(quest, "id") : null);
        }
    }

    /// <summary>Move a session's record. The ledger judges; the driver reports what it observed.</summary>
    public async Task<string> AdvanceAsync(
        string id, string state, string? note = null, string? evidence = null, string? transcript = null,
        CancellationToken ct = default)
    {
        var body = WriteJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("state", state);
            if (note is not null) writer.WriteString("note", note);
            if (evidence is not null) writer.WriteString("evidence", evidence);
            if (transcript is not null) writer.WriteString("transcript", transcript);
            writer.WriteEndObject();
        });

        using var response = await _http.PostAsync(
            $"{_base}/api/sessions/{Uri.EscapeDataString(id)}/state",
            new StringContent(body, Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var document = JsonDocument.Parse(payload);

        if (!response.IsSuccessStatusCode)
        {
            // The refusal sentence is the contract; losing it would make the driver's log say less
            // than the service said.
            throw new DriverException(
                $"the service refused moving session `{id}` to {state}: "
                + (Text(document.RootElement, "error") ?? payload));
        }

        return Text(document.RootElement, "message") ?? "";
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
    /// Both come back only over loopback, which is the point: the tree is a filesystem path and the
    /// base is useless without the checkout it names. A caller on another machine reads nulls and has
    /// nothing to diff, which is the honest answer rather than a refusal.
    /// </remarks>
    public async Task<(string? Tree, string? BaseCommit)> SessionGroundAsync(
        string id, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(
            await GetAsync("/api/sessions", ct).ConfigureAwait(false));

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
            });
        }

        return sessions;
    }

    /// <summary>
    /// How often each quest has been failed, from the records themselves (DRV6).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Only <c>failed</c> counts.</b> A <c>stood-down</c> session means somebody else took the
    /// quest first — the race resolving as designed, not a failure. A <c>declined</c> one is a real
    /// answer and closes the quest anyway. A <c>stopped</c> one was the person. Counting any of those
    /// would park quests for succeeding.
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
            if (!string.Equals(Text(session, "state"), "failed", StringComparison.OrdinalIgnoreCase)) continue;
            if (Text(session, "quest") is not { Length: > 0 } quest) continue;

            strikes[quest] = strikes.TryGetValue(quest, out var seen) ? seen + 1 : 1;
        }

        return strikes;
    }

    /// <summary>A record that came down from the team — keyed `origin/id`, the id this machine's own never has.</summary>
    internal static bool IsTeams(JsonElement session) => Text(session, "id")?.Contains('/') == true;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
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

/// <summary>What the service said to an ask, a publish or a close (D65 §1a).</summary>
/// <param name="Ok">Whether it did what was asked — false is a refusal, said in <paramref name="Message"/>.</param>
/// <param name="Message">The service's whole sentence, verbatim.</param>
/// <param name="AskId">The ask it answered about, when it answered with one.</param>
/// <param name="QuestId">The quest this call published, when it published one.</param>
public sealed record AskAnswer(bool Ok, string Message, string? AskId, string? QuestId);
