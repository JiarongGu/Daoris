using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// *Start a conversation on its first words* (NEW1a, D162 point 2): the conversation opens as <c>START_CHAT</c> always opened
/// one, and is handed the person's words as its first message. Files over a message's limit go as the words alone, and there
/// is no preface: nothing was said anywhere before. "Sent" means the door took the words (queued or taken), as
/// <see cref="StartFrom"/>'s does; a conversation that opened without taking them is answered as such and the driver keeps
/// nothing, since the page keeps the person's draft.
/// </summary>
public static class StartWith
{
    /// <summary>Open the conversation, then hand it the words.</summary>
    /// <param name="start">Opens a conversation: the chat runner's own start.</param>
    /// <param name="say">Hands a conversation its first message: false where nothing took it; throws <see cref="DriverException"/> where the files are too many.</param>
    public static async Task<StartedFrom> RunAsync(
        Func<CancellationToken, Task<ChatStart>> start, Func<string, string, IReadOnlyList<ChatUpload>, string?, bool> say,
        string text, IReadOnlyList<ChatUpload> files, CancellationToken ct = default)
    {
        var started = await start(ct).ConfigureAwait(false);
        if (started.SessionId is not { } to) return new StartedFrom(null, false, null, started.Message);

        bool took;
        try
        {
            took = say(to, text, files, null);
        }
        catch (DriverException) when (files.Count > 0)
        {
            // More files than one message carries: the words go alone rather than not at all.
            took = say(to, text, [], null);
        }

        return took
            ? new StartedFrom(to, true, null, $"conversation `{to}` starts with your words.")
            : new StartedFrom(to, false, null, $"conversation `{to}` opened, but your words did not reach it.");
    }
}

/// <summary>
/// The starts a request id has answered (NEW1a): a retry while the first start runs shares its result; a finished start that
/// opened a conversation is kept a day under <c>&lt;home&gt;/starts/&lt;id&gt;.json</c>, so the same id answers the original
/// whatever its words or repository. A start that opened nothing is not kept: a retry tries again.
/// </summary>
public sealed class StartRequests(string home)
{
    /// <summary>How long a finished start answers its id.</summary>
    public static readonly TimeSpan Keeps = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, Task<StartedFrom>> _running = new(StringComparer.Ordinal);

    /// <summary>An id is one word of letters, digits and dashes, at most 64.</summary>
    public static bool IsId(string? id) =>
        id is { Length: > 0 and <= 64 } && id.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-');

    private string PathOf(string id) => Path.Combine(home, "starts", id + ".json");

    /// <summary>Answer the id from what ran or was kept, or run the start and keep what it opened.</summary>
    /// <param name="at">When a finished start counts as having happened; tests set it, the clock otherwise.</param>
    /// <exception cref="DriverException">The id is not one.</exception>
    public async Task<StartedFrom> RunAsync(string id, Func<Task<StartedFrom>> run, DateTimeOffset? at = null)
    {
        if (!IsId(id))
        {
            throw new DriverException("the request id is one word of letters, digits and dashes, at most 64 characters.");
        }

        if (Read(id) is { } kept) return kept;

        var mine = new TaskCompletionSource<StartedFrom>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_running.TryAdd(id, mine.Task)) return await _running[id].ConfigureAwait(false);

        try
        {
            // Read again once this one is the runner: a start that finished between the two reads is answered, never repeated.
            var result = Read(id) ?? await run().ConfigureAwait(false);
            if (result.SessionId is not null) Keep(id, result, at ?? DateTimeOffset.UtcNow);
            mine.SetResult(result);
            return result;
        }
        catch (Exception error)
        {
            mine.SetException(error);
            throw;
        }
        finally
        {
            _running.TryRemove(id, out _);
        }
    }

    private StartedFrom? Read(string id)
    {
        try
        {
            var path = PathOf(id);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("at", out var when) || when.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(when.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                || DateTimeOffset.UtcNow - at > Keeps
                || !root.TryGetProperty("sessionId", out var session) || session.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("sent", out var sent) || sent.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var why = root.TryGetProperty("why", out var w) && w.ValueKind == JsonValueKind.String ? w.GetString() : null;
            return new StartedFrom(session.GetString(), sent.GetBoolean(), why, message.GetString()!);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // A file that does not read is no memory: the start runs once more.
            return null;
        }
    }

    private void Keep(string id, StartedFrom result, DateTimeOffset at)
    {
        try
        {
            var folder = Path.Combine(home, "starts");
            Directory.CreateDirectory(folder);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("sessionId", result.SessionId);
                writer.WriteBoolean("sent", result.Sent);
                if (result.Why is not null) writer.WriteString("why", result.Why);
                writer.WriteString("message", result.Message);
                writer.WriteString("at", at.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }

            AtomicFile.WriteText(PathOf(id), Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n");
            Prune(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Lost: a retry after this one finishes would open a second conversation, which a person can close.
        }
    }

    /// <summary>The files a day old go; each file's age is its own written time, so nothing is parsed.</summary>
    private static void Prune(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > Keeps) File.Delete(file);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Pruned at the next start.
            }
        }
    }
}

public sealed partial class ChatRunner
{
    private StartRequests? _starts;

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
        Task<StartedFrom> Run() => _closing
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
}
