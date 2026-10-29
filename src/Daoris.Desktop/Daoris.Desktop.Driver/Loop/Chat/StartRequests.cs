using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

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

    private string PathOf(string id) => DaorisHome.Entry(home, HomeEntry.Starts, id + ".json");

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
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || Moments.Read(root, "at") is not { } at
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
            var folder = DaorisHome.Entry(home, HomeEntry.Starts);
            JsonFile.Write(PathOf(id), writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("sessionId", result.SessionId);
                writer.WriteBoolean("sent", result.Sent);
                if (result.Why is not null) writer.WriteString("why", result.Why);
                writer.WriteString("message", result.Message);
                writer.WriteString("at", Moments.Write(at));
                writer.WriteEndObject();
            });
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
