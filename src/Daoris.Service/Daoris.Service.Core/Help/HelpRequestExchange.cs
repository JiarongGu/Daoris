using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The exchange each of Ask Daoris's granted tools makes with the driver (HELPMODE1c2a, HELPMODE1c3; D183's notes): a keyed request
/// written under the home's session requests (<see cref="HomeEntry.Requests"/>) for the conversation's own session, the take waited
/// for, a request nobody took withdrawn, then the answer beside it waited for and read once. <see cref="HousekeepingBox"/> and
/// <see cref="SettingsBox"/> each write their own request and say their own words around it.
/// </summary>
/// <remarks>
/// <b>A twin, the file the contract</b> (<c>.claude/knowledge/twins.md</c>): the driver's <c>SessionRequests</c> takes the request by
/// renaming it and writes the answer beside it; the withdrawal of one nobody took is the driver's REQTAKE2, renamed aside, then
/// removed only once nothing holds it, so of a withdrawal and a take only one wins.
/// </remarks>
internal static class HelpRequestExchange
{
    private const string AnswerSuffix = ".answer.json";

    /// <summary>How an exchange ended: nobody took it (withdrawn), taken and not answered in time, or answered.</summary>
    internal enum Ending
    {
        Untaken,
        Unanswered,
        Answered,
    }

    /// <summary>
    /// Write the request <paramref name="request"/> composes for its key, wait for a loop to take it and for its answer, and say how
    /// it ended, with the answer's text where one came.
    /// </summary>
    internal static async Task<(Ending Ending, string? Text)> ExchangeAsync(
        string home, string session, Func<string, string> request, TimeSpan take, TimeSpan answer, TimeSpan poll, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N")[..12];
        var asked = DaorisHome.Entry(home, HomeEntry.Requests, $"{session}.{key}.json");
        var answered = DaorisHome.Entry(home, HomeEntry.Requests, $"{session}.{key}{AnswerSuffix}");
        Directory.CreateDirectory(Path.GetDirectoryName(asked)!);
        AtomicFile.WriteText(asked, request(key));

        try
        {
            if (!await TakenAsync(asked, take, poll, ct).ConfigureAwait(false) && Withdraw(asked)) return (Ending.Untaken, null);
        }
        catch (OperationCanceledException)
        {
            Withdraw(asked);
            throw;
        }

        return await AnswerAsync(answered, answer, poll, ct).ConfigureAwait(false) is { } text ? (Ending.Answered, text) : (Ending.Unanswered, null);
    }

    /// <summary>The <c>said</c> of an answer, or null where it does not read as one.</summary>
    internal static string? Said(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("said", out var said) && said.ValueKind == JsonValueKind.String)
            {
                return said.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    /// <summary>A request in the twin's order: the fields every request carries, then the tool's own.</summary>
    internal static string Request(string session, string move, string by, string key, DateTimeOffset at, Action<Utf8JsonWriter> own)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("session", session);
            writer.WriteString("move", move);
            writer.WriteString("at", Moments.Write(at));
            writer.WriteString("by", by);
            writer.WriteString("key", key);
            own(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    /// <summary>Whether a loop took the request (its name is gone) before <paramref name="within"/>.</summary>
    private static async Task<bool> TakenAsync(string request, TimeSpan within, TimeSpan poll, CancellationToken ct)
    {
        var until = DateTimeOffset.UtcNow + within;
        while (File.Exists(request))
        {
            if (DateTimeOffset.UtcNow >= until) return false;
            await Task.Delay(poll, ct).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>The loop's answer, read once and removed; null where none came within <paramref name="within"/>.</summary>
    private static async Task<string?> AnswerAsync(string path, TimeSpan within, TimeSpan poll, CancellationToken ct)
    {
        var until = DateTimeOffset.UtcNow + within;
        while (true)
        {
            if (Read(path) is { } text)
            {
                for (var attempt = 0; attempt < 10 && !Remove(path); attempt++) await Task.Delay(10, ct).ConfigureAwait(false);
                return text;
            }

            if (DateTimeOffset.UtcNow >= until) return null;
            await Task.Delay(poll, ct).ConfigureAwait(false);
        }
    }

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Held a moment by its writer's rename: read at the next look.
            return null;
        }
    }

    private static bool Remove(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Withdraw a request nobody took (the driver's REQTAKE2, this side of the twin): renamed aside as a take renames it, then
    /// opened alone and removed as it closes, which waits out a take's rename begun a moment before. False where a take won, when
    /// the loop has it and the answer is waited for.
    /// </summary>
    private static bool Withdraw(string path)
    {
        var withdrawing = $"{path}.{Guid.NewGuid().ToString("N")[..8]}.withdrawing";
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(path, withdrawing);
                break;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception error) when (attempt < 20 && error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using (new FileStream(withdrawing, FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                    return true;
                }
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception error) when (attempt < 100 && error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held past every try: put back for a loop to take, and so not withdrawn.
                try
                {
                    File.Move(withdrawing, path);
                }
                catch (Exception again) when (again is IOException or UnauthorizedAccessException)
                {
                }

                return false;
            }
        }
    }
}
