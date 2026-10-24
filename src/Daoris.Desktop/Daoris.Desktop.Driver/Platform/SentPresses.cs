using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A press this terminal sent to the running Daoris: its id and the key that binds it, the route and when it was sent.</summary>
public sealed record SentPress(string Id, string Key, string Route, DateTimeOffset At)
{
    /// <summary>When a read last found it waiting for the person's Confirm (CONFIRMWAIT1b), or null where none did since it ended.</summary>
    public DateTimeOffset? WaitingAt { get; init; }

    /// <summary>When a read found it ended after it had waited, from which its day runs (CONFIRMWAIT1b), or null.</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>
    /// Whether it is still kept at <paramref name="now"/>, the one rule the find and the prune read (the confirm-wait design §4.2, §15.2
    /// item 6): a day from when its answer or a read found it ended; until then at most thirty days from when it was sent, waited on or
    /// not, since a verb killed before it read the press waiting may leave a card whose only key this is.
    /// </summary>
    public bool KeptAt(DateTimeOffset now) =>
        EndedAt is { } ended ? now - ended <= SentPresses.Kept : now - At <= SentPresses.KeptWaiting;
}

/// <summary>
/// The presses a terminal sent to the running Daoris, each with the key that reads it (RPC1j, the terminal RPC design §6.1), so
/// <c>daoris-driver result &lt;id&gt;</c> can ask <c>RESULT</c> for one whose answer did not reach the verb. One file per press in the
/// home's <c>profiles/pipe-sent</c> (<see cref="HomeEntry.PipeSent"/>; CONFIRMWAIT1, the confirm-wait design §15.2 item 6): a file
/// that holds a key is a sign-in (D172 point 3), and the card it reads may live for weeks in <c>data/</c>, so it is never under
/// <c>run/</c>, which anyone may clear while Daoris is closed; one a build before kept under <c>run/pipe/sent</c> is still read there.
/// A press is kept until its answer or a read finds it ended, then a day; thirty days from when it was sent at most.
/// </summary>
/// <remarks>
/// <para>The key is a secret only this terminal's user holds, kept where only this user reads the home, as the pipe's own journal
/// keeps its fingerprints: it proves nothing a session of the same account could not read, and the pipe confers no person's
/// authority either way (design §4.2). A file past its keep is deleted when the next press is kept or looked for.</para>
/// </remarks>
public sealed class SentPresses(string home)
{
    /// <summary>How long a press is kept to be asked again: the instance's journal keeps its lines as long (§6.1).</summary>
    public static readonly TimeSpan Kept = TimeSpan.FromHours(24);

    /// <summary>How long a press last read waiting is kept at most, from when it was sent (the confirm-wait design §4.2).</summary>
    public static readonly TimeSpan KeptWaiting = TimeSpan.FromDays(30);

    private string Folder => DaorisHome.Entry(home, HomeEntry.PipeSent);

    /// <summary>Where a build before CONFIRMWAIT1's amendment kept them: read where the kept place has none, never written.</summary>
    private string Before => Path.Combine(DaorisHome.Entry(home, HomeEntry.Pipe), "sent");

    /// <summary>Keeps a press before it is sent, so an answer lost on the way can still be asked for. Never a failure of the press.</summary>
    public void Keep(SentPress press)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            Prune(press.At);
            Write(press);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A press whose record cannot be written is still sent: the verb then says its id and cannot be asked again.
        }
    }

    /// <summary>The press kept under <paramref name="id"/>, or null where none is, it is past its keep, or the id is no id this keeps.</summary>
    public SentPress? Find(string id, DateTimeOffset now)
    {
        if (!IsId(id)) return null;
        try
        {
            Prune(now);
            return (Read(PathOf(id)) ?? Read(Path.Combine(Before, id + ".json"))) is { } press && press.Id == id && press.KeptAt(now) ? press : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A read found the press waiting at <paramref name="now"/>: kept past its day, thirty days at most from when it was sent.</summary>
    public void Waiting(string id, DateTimeOffset now) => Rewrite(id, press => press with { WaitingAt = now, EndedAt = null });

    /// <summary>Its answer or a read found the press ended, or unknown, at <paramref name="now"/>: it keeps a day from then.</summary>
    public void Ended(string id, DateTimeOffset now) => Rewrite(id, press => press with { WaitingAt = null, EndedAt = now });

    private void Rewrite(string id, Func<SentPress, SentPress?> change)
    {
        if (!IsId(id)) return;
        try
        {
            // One a build before kept is written to the kept place from here on.
            if ((Read(PathOf(id)) ?? Read(Path.Combine(Before, id + ".json"))) is { } press && press.Id == id && change(press) is { } changed)
            {
                Directory.CreateDirectory(Folder);
                Write(changed);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Never a failure of the verb: the press is kept as it was, and its day may end sooner than it waits.
        }
    }

    /// <summary>An id as a terminal mints one (a GUID's text): never a path, so a name cannot leave the folder.</summary>
    private static bool IsId(string id) => id.Length is > 0 and <= 128 && id.All(each => char.IsAsciiLetterOrDigit(each) || each == '-');

    private string PathOf(string id) => Path.Combine(Folder, id + ".json");

    private void Write(SentPress press) =>
        AtomicFile.WriteText(PathOf(press.Id), JsonSerializer.Serialize(new
        {
            id = press.Id, key = press.Key, route = press.Route, at = press.At, waitingAt = press.WaitingAt, endedAt = press.EndedAt,
        }) + "\n");

    /// <summary>The record at <paramref name="path"/>, or null where there is none or it does not read as one.</summary>
    private static SentPress? Read(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (Text(root, "id") is not { } id || Text(root, "key") is not { Length: > 0 } key || Text(root, "route") is not { } route
                || Time(root, "at") is not { } at)
            {
                return null;
            }

            return new SentPress(id, key, route, at) { WaitingAt = Time(root, "waitingAt"), EndedAt = Time(root, "endedAt") };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Deletes every record past its keep at <paramref name="now"/>, by the rule <see cref="Find"/> reads; one that does not read, a day after its file was written.</summary>
    private void Prune(DateTimeOffset now)
    {
        foreach (var path in new[] { Folder, Before }.Where(Directory.Exists).SelectMany(folder => Directory.EnumerateFiles(folder, "*.json")))
        {
            try
            {
                var kept = Read(path) is { } press ? press.KeptAt(now) : now - File.GetLastWriteTimeUtc(path) <= Kept;
                if (!kept) File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held by another verb: left for the next look.
            }
        }
    }

    private static string? Text(JsonElement element, string field) =>
        element.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Time(JsonElement element, string field) =>
        element.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var at)
            ? at
            : null;
}
