using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A press this terminal sent to the running Daoris: its id and the key that binds it, the route and when it was sent.</summary>
public sealed record SentPress(string Id, string Key, string Route, DateTimeOffset At);

/// <summary>
/// The presses a terminal sent to the running Daoris, kept under the home for a day (RPC1j, the terminal RPC design §6.1) so
/// <c>daoris-driver result &lt;id&gt;</c> can ask <c>RESULT</c> for one whose answer did not reach the verb. Beside the instance's own
/// journal in the home's <c>pipe</c> folder, in a folder of its own, one file per press.
/// </summary>
/// <remarks>
/// <para>The key is a secret only this terminal's user holds, kept where only this user reads the home, as the pipe's own journal
/// keeps its fingerprints: it proves nothing a session of the same account could not read, and the pipe confers no person's
/// authority either way (design §4.2). A file past its day is deleted when the next press is kept or looked for.</para>
/// </remarks>
public sealed class SentPresses(string home)
{
    /// <summary>How long a press is kept to be asked again: the instance's journal keeps its lines as long (§6.1).</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromHours(24);

    private string Folder => Path.Combine(DaorisHome.Entry(home, HomeEntry.Pipe), "sent");

    /// <summary>Keeps a press before it is sent, so an answer lost on the way can still be asked for. Never a failure of the press.</summary>
    public void Keep(SentPress press)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            Prune(press.At);
            AtomicFile.WriteText(PathOf(press.Id), JsonSerializer.Serialize(new { id = press.Id, key = press.Key, route = press.Route, at = press.At }) + "\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A press whose record cannot be written is still sent: the verb then says its id and cannot be asked again.
        }
    }

    /// <summary>The press kept under <paramref name="id"/>, or null where none is, it is older than a day, or the id is no id this keeps.</summary>
    public SentPress? Find(string id, DateTimeOffset now)
    {
        if (!IsId(id)) return null;
        try
        {
            Prune(now);
            var path = PathOf(id);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (Text(root, "id") != id || Text(root, "key") is not { Length: > 0 } key || Text(root, "route") is not { } route) return null;
            return root.TryGetProperty("at", out var at) && at.TryGetDateTimeOffset(out var sent) && now - sent <= Kept
                ? new SentPress(id, key, route, sent)
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>An id as a terminal mints one (a GUID's text): never a path, so a name cannot leave the folder.</summary>
    private static bool IsId(string id) => id.Length is > 0 and <= 128 && id.All(each => char.IsAsciiLetterOrDigit(each) || each == '-');

    private string PathOf(string id) => Path.Combine(Folder, id + ".json");

    private void Prune(DateTimeOffset now)
    {
        if (!Directory.Exists(Folder)) return;
        foreach (var path in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                if (now - File.GetLastWriteTimeUtc(path) > Kept) File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held by another verb: left for the next look.
            }
        }
    }

    private static string? Text(JsonElement element, string field) =>
        element.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
