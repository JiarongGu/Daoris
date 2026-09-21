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
        return new Snapshot(quests, repositories, active);
    }

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
                Text(quest, "status") ?? ""));
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

    private static IReadOnlyList<SessionView> ReadSessions(string json)
    {
        using var document = JsonDocument.Parse(json);
        var sessions = new List<SessionView>();
        foreach (var session in document.RootElement.EnumerateArray())
        {
            sessions.Add(new SessionView(
                Text(session, "id") ?? "",
                Text(session, "repository") ?? "",
                // Read since SURF5b: a park is a state change nothing here performs, so the only way
                // to see one is to look. Defaulted rather than required, because a service older
                // than this field answers without it and the planner never needed it either way.
                Text(session, "state") ?? "",
                Text(session, "note")));
        }

        return sessions;
    }

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
