using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// Where this machine's remote is — machine-local configuration, never per-repository (D47 §9):
/// `~/.daoris/remote.json` holding `{ "url": "...", "key": "dk_..." }`, with the environment
/// overriding. The FILE is the contract this component shares with the service's hosts; the driver
/// deliberately reads it with its own code, because it links against no service assembly.
/// </summary>
public sealed record RemoteTarget(string Url, string Key)
{
    public const string UrlVariable = "DAORIS_REMOTE_URL";
    public const string KeyVariable = "DAORIS_REMOTE_KEY";
    public const string PathVariable = "DAORIS_REMOTE_CONFIG";

    /// <summary>The machine's remote, if it has one. Absence is the default and it is silent (D21).</summary>
    public static RemoteTarget? Load() => Load(
        Environment.GetEnvironmentVariable,
        Environment.GetEnvironmentVariable(PathVariable)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "remote.json"));

    /// <summary>
    /// The testable shape: the same judgement over injected surroundings. Either environment variable
    /// present means the environment IS the answer, whole — a half-set pair is no remote, never a mix
    /// of an env URL with the file's key, which would quietly aim one machine's key at another's host.
    /// </summary>
    public static RemoteTarget? Load(Func<string, string?> environment, string path)
    {
        var url = environment(UrlVariable);
        var key = environment(KeyVariable);

        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(key) && File.Exists(path))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                url = Text(root, "url");
                key = Text(root, "key");
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)
            ? null
            : new RemoteTarget(url.TrimEnd('/'), key);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>What one sync pass moved — and, when it hit a wall, what the wall said.</summary>
/// <param name="Family">Foreign registrations mirrored down: the teammates' repositories this machine
/// can now address, without ever seeing their disks.</param>
/// <param name="Problem">
/// Null on a clean pass. Records sync eventually (D47 §2), so a problem here is reported and retried
/// next tick rather than failing the tick — but it is REPORTED, because a feed dying quietly (an
/// expired key, an unjoined repository) looks exactly like a family with nothing to say.
/// </param>
public sealed record SyncReport(
    int Registrations, int Sessions, int Entries, int Quests, int Family, string? Problem)
{
    public static readonly SyncReport Nothing = new(0, 0, 0, 0, 0, null);
}

/// <summary>
/// The pure half of the sync: what leaves this machine and what comes back, built from the doors' own
/// JSON. Pure so the boundary is testable where it matters most — the payloads these functions build
/// are the disclosure boundary in practice, and none of them has a field for a machine path. A root or
/// a transcript in the input is dropped at PARSE time; there is no branch that could forward one.
/// </summary>
public static class RemoteSyncPayloads
{
    /// <param name="SharesKnowledge">Whether its knowledge content feeds too — its manifest's second
    /// declaration (D47 §4). The registration itself travels for every joined repository.</param>
    public sealed record JoinedRepository(
        string Repository, string? Summary, IReadOnlyList<string> Owns, IReadOnlyList<string> Accepts,
        IReadOnlyList<string> Packs, bool SharesKnowledge);

    /// <summary>Every repository a registry answer names — joined or not, adopted or not.</summary>
    public static IReadOnlySet<string> Names(string registryJson)
    {
        using var document = JsonDocument.Parse(registryJson);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in document.RootElement.EnumerateArray())
        {
            if (Text(repo, "repository") is { Length: > 0 } name) names.Add(name);
        }

        return names;
    }

    /// <summary>
    /// The joined repositories in a local registry answer — the only ones a remote may hear of FROM
    /// here. Joined alone is not enough: mirrored-down teammate rows carry the flag too, and feeding
    /// one back up would speak for a repository this machine cannot see — worse, its empty entries
    /// feed is a replacement, wiping the teammate's shared knowledge from a machine that never had it.
    /// The root is the checkout and the checkout is the authority (D47 §5), so a root is required —
    /// and the local host answers roots to this loopback caller, so a rootless row IS a foreign one.
    /// </summary>
    public static IReadOnlyList<JoinedRepository> Joined(string registryJson)
    {
        using var document = JsonDocument.Parse(registryJson);
        var joined = new List<JoinedRepository>();
        foreach (var repo in document.RootElement.EnumerateArray())
        {
            if (!(repo.TryGetProperty("joined", out var j) && j.ValueKind == JsonValueKind.True)) continue;
            if (Text(repo, "root") is not { Length: > 0 }) continue;

            joined.Add(new JoinedRepository(
                Text(repo, "repository") ?? "",
                Text(repo, "summary"),
                Strings(repo, "owns"),
                Strings(repo, "accepts"),
                Strings(repo, "packs"),
                repo.TryGetProperty("sharesKnowledge", out var s) && s.ValueKind == JsonValueKind.True));
        }

        return joined;
    }

    /// <summary>One joined repository's registration, as the remote hears it: the declaration, no root.</summary>
    public static string Registration(JoinedRepository repo) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("repository", repo.Repository);
        writer.WriteStartArray("packs");
        foreach (var pack in repo.Packs) writer.WriteStringValue(pack);
        writer.WriteEndArray();
        writer.WriteStartObject("domain");
        if (repo.Summary is not null) writer.WriteString("summary", repo.Summary);
        writer.WriteStartArray("owns");
        foreach (var owns in repo.Owns) writer.WriteStringValue(owns);
        writer.WriteEndArray();
        writer.WriteStartArray("accepts");
        foreach (var accepts in repo.Accepts) writer.WriteStringValue(accepts);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteBoolean("join", true);
        writer.WriteBoolean("shareKnowledge", repo.SharesKnowledge);
        writer.WriteEndObject();
    });

    /// <summary>
    /// The session records worth feeding: joined repositories only, and never the transcript — the
    /// field is dropped here, at parse, so no later step could forward it. Null when nothing qualifies.
    /// </summary>
    public static (string Json, int Count)? Sessions(string sessionsJson, IReadOnlySet<string> joined)
    {
        using var document = JsonDocument.Parse(sessionsJson);
        var count = 0;
        var json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("records");
            foreach (var session in document.RootElement.EnumerateArray())
            {
                var repository = Text(session, "repository") ?? "";
                var id = Text(session, "id") ?? "";
                // A record already carrying an origin is somebody else's, mirrored here — feeding it
                // back would launder another machine's record through this machine's identity.
                if (!joined.Contains(repository) || id.Contains('/')) continue;

                count++;
                writer.WriteStartObject();
                writer.WriteString("id", id);
                writer.WriteString("quest", Text(session, "quest"));
                writer.WriteString("repository", repository);
                writer.WriteString("adapter", Text(session, "adapter"));
                writer.WriteString("state", Text(session, "state"));
                Copy(writer, session, "note");
                Copy(writer, session, "evidence");
                writer.WriteString("created", Text(session, "created"));
                writer.WriteString("updated", Text(session, "updated"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        return count == 0 ? null : (json, count);
    }

    /// <summary>
    /// One sharing repository's content for the remote's ingest. An empty list still feeds: the
    /// remote's copy is a replacement, and a repository that deleted its knowledge means the deletion.
    /// </summary>
    public static (string Json, int Count) Entries(string repository, string entriesJson)
    {
        using var document = JsonDocument.Parse(entriesJson);
        var count = 0;
        var json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("repository", repository);
            writer.WriteStartArray("entries");
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                count++;
                writer.WriteStartObject();
                writer.WriteString("kind", Text(entry, "kind"));
                writer.WriteString("title", Text(entry, "title"));
                writer.WriteString("body", Text(entry, "body"));
                writer.WriteString("relativePath", Text(entry, "path"));
                Copy(writer, entry, "anchor");
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        return (json, count);
    }

    /// <summary>
    /// The remote quests this machine mirrors: those touching its joined repositories, as sender or
    /// receiver. Everything else on the remote is other people's business. Null when nothing qualifies.
    /// </summary>
    public static (string Json, int Count)? Quests(string remoteQuestsJson, IReadOnlySet<string> joined)
    {
        using var document = JsonDocument.Parse(remoteQuestsJson);
        var count = 0;
        var json = Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("quests");
            foreach (var quest in document.RootElement.EnumerateArray())
            {
                var from = Text(quest, "from") ?? "";
                var to = Text(quest, "to") ?? "";
                if (!joined.Contains(from) && !joined.Contains(to)) continue;

                count++;
                writer.WriteStartObject();
                writer.WriteString("id", Text(quest, "id"));
                writer.WriteString("from", from);
                writer.WriteString("to", to);
                writer.WriteString("title", Text(quest, "title"));
                writer.WriteString("body", Text(quest, "body"));
                writer.WriteString("status", Text(quest, "status"));
                Copy(writer, quest, "note");
                writer.WriteString("filed", Text(quest, "filed"));
                writer.WriteString("updated", Text(quest, "updated"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        return count == 0 ? null : (json, count);
    }

    /// <summary>
    /// The remote's registry as this machine should hear of it: FOREIGN rows only. A repository this
    /// machine already has keeps its own registration — and its root — because the machine that holds
    /// the checkout is the authority on it; re-posting the remote's stripped copy would overwrite the
    /// one field spawning needs. What arrives makes teammates' repositories addressable here (D47 §5):
    /// their quests home at the remote, and the relay carries the verbs.
    /// </summary>
    public static IReadOnlyList<(string Repository, string Json)> ForeignRegistrations(
        string remoteRegistryJson, IReadOnlySet<string> localNames)
    {
        using var document = JsonDocument.Parse(remoteRegistryJson);
        var foreign = new List<(string, string)>();
        foreach (var repo in document.RootElement.EnumerateArray())
        {
            var name = Text(repo, "repository") ?? "";
            if (name.Length == 0 || localNames.Contains(name)) continue;

            var payload = Write(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("repository", name);
                writer.WriteStartArray("packs");
                foreach (var pack in Strings(repo, "packs")) writer.WriteStringValue(pack);
                writer.WriteEndArray();
                writer.WriteStartObject("domain");
                if (Text(repo, "summary") is { } summary) writer.WriteString("summary", summary);
                writer.WriteStartArray("owns");
                foreach (var owns in Strings(repo, "owns")) writer.WriteStringValue(owns);
                writer.WriteEndArray();
                writer.WriteStartArray("accepts");
                foreach (var accepts in Strings(repo, "accepts")) writer.WriteStringValue(accepts);
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteBoolean("join", repo.TryGetProperty("joined", out var j) && j.ValueKind == JsonValueKind.True);
                writer.WriteBoolean("shareKnowledge",
                    repo.TryGetProperty("sharesKnowledge", out var s) && s.ValueKind == JsonValueKind.True);
                writer.WriteEndObject();
            });
            foreign.Add((name, payload));
        }

        return foreign;
    }

    private static void Copy(Utf8JsonWriter writer, JsonElement element, string name)
    {
        if (Text(element, name) is { } value) writer.WriteString(name, value);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return [];

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }

    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

/// <summary>
/// One pass of the local↔remote sync (D47 §9): feed the joined registrations, this machine's session
/// records, and each sharing repository's content UP; pull the quests touching this family DOWN into
/// the local mirror. Registrations go first, so the remote knows who is joined before their records
/// arrive. Runs on the driver's own tick — a server machine running `daoris-driver` with a key is just
/// another machine, not a special deployment.
/// </summary>
public sealed class RemoteSync : IDisposable
{
    private readonly HttpClient _local;
    private readonly HttpClient _remote;
    private readonly string _localBase;
    private readonly string _remoteBase;

    public RemoteSync(string localUrl, string? localKey, RemoteTarget target)
    {
        _localBase = localUrl.TrimEnd('/');
        _remoteBase = target.Url;
        _local = Client(localKey);
        _remote = Client(target.Key);

        static HttpClient Client(string? key)
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            if (!string.IsNullOrWhiteSpace(key))
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            }

            return http;
        }
    }

    /// <summary>The machine's sync, when the machine has a remote — null otherwise, silently (D21).</summary>
    public static RemoteSync? FromEnvironment(string localUrl) =>
        RemoteTarget.Load() is { } target
            ? new RemoteSync(localUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable), target)
            : null;

    public void Dispose()
    {
        _local.Dispose();
        _remote.Dispose();
    }

    public async Task<SyncReport> RunOnceAsync(CancellationToken ct = default)
    {
        int registrations = 0, sessions = 0, entries = 0, quests = 0, family = 0;
        try
        {
            var registryJson = await GetAsync(_local, $"{_localBase}/api/registry", ct).ConfigureAwait(false);
            var joined = RemoteSyncPayloads.Joined(registryJson);
            if (joined.Count == 0) return SyncReport.Nothing;

            var names = joined.Select(r => r.Repository).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var repo in joined)
            {
                await PostAsync(_remote, $"{_remoteBase}/api/registry", RemoteSyncPayloads.Registration(repo), ct)
                    .ConfigureAwait(false);
                registrations++;
            }

            var records = RemoteSyncPayloads.Sessions(
                await GetAsync(_local, $"{_localBase}/api/sessions?includeClosed=true", ct).ConfigureAwait(false),
                names);
            if (records is { } feed)
            {
                await PostAsync(_remote, $"{_remoteBase}/api/feed/sessions", feed.Json, ct).ConfigureAwait(false);
                sessions = feed.Count;
            }

            foreach (var repo in joined.Where(r => r.SharesKnowledge))
            {
                var content = RemoteSyncPayloads.Entries(repo.Repository, await GetAsync(
                    _local, $"{_localBase}/api/entries?repository={Uri.EscapeDataString(repo.Repository)}", ct)
                    .ConfigureAwait(false));
                await PostAsync(_remote, $"{_remoteBase}/api/feed/entries", content.Json, ct).ConfigureAwait(false);
                entries += content.Count;
            }

            // The remote's registry comes down as foreign rows only — teammates' repositories become
            // addressable here, while everything this machine holds keeps its own registration.
            foreach (var (_, payload) in RemoteSyncPayloads.ForeignRegistrations(
                await GetAsync(_remote, $"{_remoteBase}/api/registry", ct).ConfigureAwait(false),
                RemoteSyncPayloads.Names(registryJson)))
            {
                await PostAsync(_local, $"{_localBase}/api/registry", payload, ct).ConfigureAwait(false);
                family++;
            }

            var mirror = RemoteSyncPayloads.Quests(
                await GetAsync(_remote, $"{_remoteBase}/api/quests?includeClosed=true", ct).ConfigureAwait(false),
                names);
            if (mirror is { } pull)
            {
                await PostAsync(_local, $"{_localBase}/api/feed/quests", pull.Json, ct).ConfigureAwait(false);
                quests = pull.Count;
            }

            return new(registrations, sessions, entries, quests, family, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Cancellation belongs to the caller, never converted into a sync problem.
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or DriverException or JsonException)
        {
            // Report and carry on: records sync eventually and the next tick retries — but a feed
            // dying quietly looks exactly like a family with nothing to say, so the wall is named.
            return new(registrations, sessions, entries, quests, family, error.Message);
        }
    }

    private static async Task<string> GetAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new DriverException($"{url} answered {(int)response.StatusCode}: {ErrorOf(payload)}");
        }

        return payload;
    }

    private static async Task PostAsync(HttpClient http, string url, string json, CancellationToken ct)
    {
        using var response = await http.PostAsync(
            url, new StringContent(json, Encoding.UTF8, "application/json"), ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new DriverException($"{url} answered {(int)response.StatusCode}: {ErrorOf(payload)}");
        }
    }

    private static string ErrorOf(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                ? error.GetString() ?? payload
                : payload;
        }
        catch (JsonException)
        {
            return payload.Length <= 200 ? payload : payload[..200] + "…";
        }
    }
}
