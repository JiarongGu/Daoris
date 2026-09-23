using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The transport half of the write-through relay (D47 §5/§9): how a verb reaches the remote's own
/// judgement. Deliberately dumb — it moves bytes and reports what came back; every decision about what
/// an answer MEANS stays in <see cref="QuestExchange"/>, behind the same seam both hosts share.
/// </summary>
public interface IRemoteQuestClient
{
    /// <summary>
    /// Publish at the quest's home. What it carries crosses as LINKS and NAMES — an attachment's bytes
    /// stay on the machine that has them (D65 §2), and this signature has nowhere to put them.
    /// </summary>
    Task<RemoteQuestAnswer> PublishAsync(
        string from, string to, string title, string body,
        IReadOnlyList<string> links, IReadOnlyList<QuestAttachment> attachments,
        IReadOnlyList<QuestStep> then,
        CancellationToken ct = default);

    Task<RemoteQuestAnswer> RespondAsync(
        string id, string action, string? reason, CancellationToken ct = default);
}

/// <param name="Status">The HTTP status the remote answered — or 0 when it could not be reached at all,
/// which the exchange turns into the changed-nothing refusal (an eventually-consistent lock is not a
/// lock, so an unreachable one refuses rather than queueing).</param>
/// <param name="Message">The remote judgement's own message, passed on verbatim so the two deployments
/// cannot drift on what an agent is told.</param>
/// <param name="Quest">The quest as the remote now holds it, on success — what the local mirror takes.</param>
public sealed record RemoteQuestAnswer(int Status, string Message, Quest? Quest);

/// <summary>
/// Where a workspace's remote is and how this machine speaks to it — machine-local configuration,
/// never per-repository (D47 §9, D48 §5): one file under the user profile, with the environment
/// overriding.
/// </summary>
/// <remarks>
/// <para>The file is the home's `remotes.json` (D63) — a MAP, `{ "aurora": { "url": "...", "key": "dk_..." } }` —
/// because one shared deployment serves one workspace (D48 §5), and a machine may hold repositories
/// from several circles. The workspace NAME keys the map; whether a given repository may feed at all
/// stays its own manifest's `remote` declaration: the manifest says MAY, the machine says WHERE.</para>
///
/// <para>It is not tracked by any repository, which is what `sensitive-info` requires of a credential;
/// the OS secret store is held as D47's open question 3. `daoris remote list|add|remove` is the
/// surface over it (D50) — and hand-editing keeps working, because the file is the truth.</para>
/// </remarks>
public sealed record RemoteConfig(string Url, string Key)
{
    public const string UrlVariable = "DAORIS_REMOTE_URL";
    public const string KeyVariable = "DAORIS_REMOTE_KEY";

    /// <summary>Which workspace the environment pair serves. Absent is <see cref="Workspaces.Default"/>.</summary>
    public const string WorkspaceVariable = "DAORIS_REMOTE_WORKSPACE";

    public const string PathVariable = "DAORIS_REMOTE_CONFIG";

    /// <summary>
    /// The map's conventional home — under the Daoris home (D63), and null where there is none: a
    /// machine with no home has no remotes, which is the documented default anyway.
    /// </summary>
    public static string? DefaultPath => DaorisHome.File("remotes.json");

    /// <summary>This machine's remotes, by workspace. Absence is the default and it is silent (D21).</summary>
    public static IReadOnlyDictionary<string, RemoteConfig> Load() => Load(
        Environment.GetEnvironmentVariable,
        Environment.GetEnvironmentVariable(PathVariable) ?? DefaultPath);

    /// <summary>
    /// The testable shape: the same judgement over injected surroundings.
    /// </summary>
    /// <remarks>
    /// Either environment variable present means the environment IS the answer — for the WHOLE
    /// MACHINE, not one entry of it, and a half-set pair is no remote at all. Never a mix of an env
    /// URL with the file's key, which would quietly aim one machine's key at another's host; and never
    /// a merge, which would let a real map leak into a process that thought it had named its only
    /// remote. <see cref="WorkspaceVariable"/> names which circle the pair serves.
    /// </remarks>
    public static IReadOnlyDictionary<string, RemoteConfig> Load(Func<string, string?> environment, string? path)
    {
        var map = new Dictionary<string, RemoteConfig>(StringComparer.OrdinalIgnoreCase);
        var url = environment(UrlVariable);
        var key = environment(KeyVariable);

        if (!string.IsNullOrWhiteSpace(url) || !string.IsNullOrWhiteSpace(key))
        {
            if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(key))
            {
                map[Workspaces.Normalize(environment(WorkspaceVariable))] = new(url.TrimEnd('/'), key);
            }

            return map;
        }

        if (!File.Exists(path)) return map;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return map;

            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object) continue;
                var entryUrl = Text(entry.Value, "url");
                var entryKey = Text(entry.Value, "key");
                // An entry missing half its pair is one workspace with no remote, never a machine with
                // none: a typo in one circle must not silently unwire the others.
                if (string.IsNullOrWhiteSpace(entryUrl) || string.IsNullOrWhiteSpace(entryKey)) continue;

                map[Workspaces.Normalize(entry.Name)] = new(entryUrl.TrimEnd('/'), entryKey);
            }
        }
        catch (JsonException)
        {
            // A file that will not parse is a file that names no remote. The sync loop, not this
            // reader, is where "you configured a remote and it does not work" gets said out loud.
            return new Dictionary<string, RemoteConfig>(StringComparer.OrdinalIgnoreCase);
        }

        return map;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>
/// Which remote a verb on a quest reaches: the quest's own workspace decides (D48 §5).
/// </summary>
/// <remarks>
/// One shared deployment serves one workspace, so "the machine's remote" stopped being a single thing
/// the moment a machine could hold two circles. A circle with no entry syncs nowhere, silently — the
/// local deployment working alone is binding (design §2a), and that is the shape it takes here.
/// </remarks>
public interface IRemoteQuestRoutes
{
    /// <summary>The remote serving a workspace, or null when that circle has none.</summary>
    IRemoteQuestClient? For(string? workspace);

    /// <summary>The circles this machine has a remote for — what a refusal names when it must say which.</summary>
    IReadOnlyCollection<string> Workspaces { get; }
}

/// <summary>The map, as the doors hold it: one HTTP client per workspace, built once at composition.</summary>
public sealed class RemoteQuestRoutes(IReadOnlyDictionary<string, IRemoteQuestClient> byWorkspace) : IRemoteQuestRoutes
{
    /// <summary>Null when the machine has no remote at all — the absence every host already handles.</summary>
    public static IRemoteQuestRoutes? From(IReadOnlyDictionary<string, RemoteConfig> remotes) =>
        remotes.Count == 0
            ? null
            : new RemoteQuestRoutes(remotes.ToDictionary(
                entry => Knowledge.Workspaces.Normalize(entry.Key),
                entry => (IRemoteQuestClient)new HttpRemoteQuests(entry.Value),
                StringComparer.OrdinalIgnoreCase));

    public IRemoteQuestClient? For(string? workspace) =>
        byWorkspace.TryGetValue(Knowledge.Workspaces.Normalize(workspace), out var client) ? client : null;

    public IReadOnlyCollection<string> Workspaces => byWorkspace.Keys.ToList();
}

/// <summary>
/// The one HTTP implementation of the relay, used by both local doors — written once in Core for the
/// same reason <see cref="QuestExchange"/> lives here: two hosts each rolling their own client is two
/// clients that drift. It speaks to the remote's own quest endpoints, so the remote needs no special
/// relay surface: a written-through verb and a directly-published one are indistinguishable there.
/// </summary>
public sealed class HttpRemoteQuests(RemoteConfig config) : IRemoteQuestClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public Task<RemoteQuestAnswer> PublishAsync(
        string from, string to, string title, string body,
        IReadOnlyList<string> links, IReadOnlyList<QuestAttachment> attachments,
        IReadOnlyList<QuestStep> then,
        CancellationToken ct = default) =>
        SendAsync("/api/quests", writer =>
        {
            // The chain crosses whole (D65 §4): the remote is its home, and its close publishes each step.
            writer.WriteStartArray("then");
            foreach (var step in then)
            {
                writer.WriteStartObject();
                writer.WriteString("to", step.To);
                writer.WriteString("title", step.Title);
                writer.WriteString("body", step.Body);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteString("from", from);
            writer.WriteString("to", to);
            writer.WriteString("title", title);
            writer.WriteString("body", body);
            writer.WriteStartArray("links");
            foreach (var link in links) writer.WriteStringValue(link);
            writer.WriteEndArray();
            // By name: the shared door refuses content outright, and there is none to send.
            writer.WriteStartArray("attachments");
            foreach (var attachment in attachments)
            {
                writer.WriteStartObject();
                writer.WriteString("name", attachment.Name);
                writer.WriteString("sha256", attachment.Sha256);
                writer.WriteNumber("bytes", attachment.Bytes);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }, ct);

    public Task<RemoteQuestAnswer> RespondAsync(
        string id, string action, string? reason, CancellationToken ct = default) =>
        SendAsync($"/api/quests/{Uri.EscapeDataString(id)}/respond", writer =>
        {
            writer.WriteString("action", action);
            if (reason is not null) writer.WriteString("reason", reason);
        }, ct);

    private async Task<RemoteQuestAnswer> SendAsync(
        string path, Action<Utf8JsonWriter> writeBody, CancellationToken ct)
    {
        try
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writeBody(writer);
                writer.WriteEndObject();
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, config.Url + path);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + config.Key);
            request.Content = new ByteArrayContent(stream.ToArray());
            request.Content.Headers.ContentType = new("application/json");

            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Parse((int)response.StatusCode, payload);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // Cancellation belongs to the caller, never swallowed into "unreachable".
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return new(0, error.Message, null);
        }
    }

    // Hand-rolled for the same reason the registration store's JSON is: nothing here may quietly stop
    // working under AOT, and the shapes are three fields deep. Internal so the tests can drive it
    // without a live remote — it is the only judgement in this otherwise deliberately dumb transport.
    internal static RemoteQuestAnswer Parse(int status, string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                return new(status, error.GetString() ?? "", null);
            }

            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() ?? ""
                : "";
            Quest? quest = null;
            if (root.TryGetProperty("quest", out var q) && q.ValueKind == JsonValueKind.Object)
            {
                quest = new Quest(
                    q.GetProperty("id").GetString()!,
                    q.GetProperty("from").GetString()!,
                    q.GetProperty("to").GetString()!,
                    q.GetProperty("title").GetString()!,
                    q.GetProperty("body").GetString()!,
                    Enum.Parse<QuestStatus>(q.GetProperty("status").GetString()!, ignoreCase: true),
                    q.TryGetProperty("note", out var note) && note.ValueKind == JsonValueKind.String ? note.GetString() : null,
                    q.GetProperty("filed").GetDateTimeOffset(),
                    q.GetProperty("updated").GetDateTimeOffset())
                {
                    // Absent is nothing carried — a remote from before D65 answers without either.
                    Links = q.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array
                        ? links.EnumerateArray().Select(l => l.GetString()).OfType<string>().ToList()
                        : [],
                    Attachments = q.TryGetProperty("attachments", out var attachments)
                        && attachments.ValueKind == JsonValueKind.Array
                        ? attachments.EnumerateArray().Select(a => new QuestAttachment(
                            a.GetProperty("name").GetString()!,
                            a.GetProperty("sha256").GetString()!,
                            a.GetProperty("bytes").GetInt64())).ToList()
                        : [],
                    Then = q.TryGetProperty("then", out var then) && then.ValueKind == JsonValueKind.Array
                        ? then.EnumerateArray().Select(s => new QuestStep(
                            s.GetProperty("to").GetString()!,
                            s.GetProperty("title").GetString()!,
                            s.GetProperty("body").GetString()!)).ToList()
                        : [],
                    Parent = q.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.String
                        ? parent.GetString()
                        : null,
                };
            }

            return new(status, message, quest);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return new(status, $"the remote answered something that is not a quest response: {Truncate(payload)}", null);
        }
    }

    private static string Truncate(string payload) =>
        payload.Length <= 200 ? payload : payload[..200] + "…";
}
