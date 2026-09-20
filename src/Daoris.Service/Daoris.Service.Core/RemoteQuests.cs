using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The transport half of the write-through relay (D47 §5/§9): how a verb reaches the remote's own
/// judgement. Deliberately dumb — it moves bytes and reports what came back; every decision about what
/// an answer MEANS stays in <see cref="QuestExchange"/>, behind the same seam both hosts share.
/// </summary>
public interface IRemoteQuestClient
{
    Task<RemoteQuestAnswer> PublishAsync(
        string from, string to, string title, string body, CancellationToken ct = default);

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
/// Where the remote is and how this machine speaks to it — machine-local configuration, never
/// per-repository (D47 §9): one file under the user profile, with the environment overriding.
/// </summary>
/// <remarks>
/// The file is `~/.daoris/remote.json` — `{ "url": "...", "key": "dk_..." }` — written by the person
/// when they join a machine to a remote. It is not tracked by any repository, which is what
/// `sensitive-info` requires of a credential; the OS secret store is held as D47's open question 3.
/// </remarks>
public sealed record RemoteConfig(string Url, string Key)
{
    public const string UrlVariable = "DAORIS_REMOTE_URL";
    public const string KeyVariable = "DAORIS_REMOTE_KEY";
    public const string PathVariable = "DAORIS_REMOTE_CONFIG";

    /// <summary>Read the machine's remote, if it has one. Absence is the default and it is silent (D21).</summary>
    public static RemoteConfig? Load() => Load(
        Environment.GetEnvironmentVariable,
        Environment.GetEnvironmentVariable(PathVariable)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".daoris", "remote.json"));

    /// <summary>
    /// The testable shape: the same judgement over injected surroundings. Either environment variable
    /// present means the environment IS the answer, whole — a half-set pair is no remote, never a mix
    /// of an env URL with the file's key, which would quietly aim one machine's key at another's host.
    /// </summary>
    public static RemoteConfig? Load(Func<string, string?> environment, string path)
    {
        var url = environment(UrlVariable);
        var key = environment(KeyVariable);

        if (string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(key) && File.Exists(path))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                url = root.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                key = root.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
            }
            catch (JsonException)
            {
                // A file that will not parse is a file that names no remote. The sync loop, not this
                // reader, is where "you configured a remote and it does not work" gets said out loud.
                return null;
            }
        }

        return string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key)
            ? null
            : new RemoteConfig(url.TrimEnd('/'), key);
    }
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
        string from, string to, string title, string body, CancellationToken ct = default) =>
        SendAsync("/api/quests", writer =>
        {
            writer.WriteString("from", from);
            writer.WriteString("to", to);
            writer.WriteString("title", title);
            writer.WriteString("body", body);
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
                    q.GetProperty("updated").GetDateTimeOffset());
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
