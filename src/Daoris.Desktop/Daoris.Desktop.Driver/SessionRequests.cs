using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The moves a request may carry (D126 §7.1): the person's three on a live or parked session.</summary>
public static class SessionMove
{
    /// <summary>End it now, as the person's stop: its quest is then held here until *Try again* (SESSUX1b).</summary>
    public const string Stop = "stop";

    /// <summary>Finish one that waits on the person, with their note.</summary>
    public const string Finish = "finish";

    /// <summary>Decline one that waits on the person, with their reason.</summary>
    public const string Decline = "decline";

    public static bool Known(string move) => move is Stop or Finish or Decline;
}

/// <summary>Who asked, as the machine log's <c>door</c> names a door: a word, never a sentence.</summary>
public static class RequestDoor
{
    /// <summary><c>daoris-driver sessions</c>.</summary>
    public const string Terminal = "terminal";

    /// <summary>A pause stopping what of its work another process runs (PAUSE1b, D132 §2.1), when it lands.</summary>
    public const string Pause = "pause";
}

/// <summary>One move asked of one session, for whichever loop on the home runs it.</summary>
/// <param name="Move">One of <see cref="SessionMove"/>.</param>
/// <param name="At">When it was asked: a request nobody took for <see cref="SessionRequests.Lifetime"/> is dropped.</param>
public sealed record SessionRequest(string Session, string Move, DateTimeOffset At)
{
    /// <summary>A finish's note or a decline's reason, the person's words; none for a stop.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// That the record waited on the person when it was asked: its stop is then its card's, the record moved by the ledger
    /// once the process is let go, as <c>RESOLVE_SESSION</c>'s <c>stopped</c> moves it (D126 §3.3).
    /// </summary>
    public bool Parked { get; init; }

    /// <summary>Who asked, one of <see cref="RequestDoor"/>.</summary>
    public string By { get; init; } = RequestDoor.Terminal;
}

/// <summary>
/// The requests every loop on the home watches (SESSUX1g, D126 §7.1): <c>&lt;home&gt;/sessions/requests/&lt;id&gt;.json</c>, a
/// move the person asked of a session another process on this machine runs, written atomically by the door that asked
/// and taken, once, by the loop whose own registry runs it.
/// </summary>
/// <remarks>
/// <para><b>Why a folder.</b> A terminal's verb holds no process; the session's process belongs to whichever loop started
/// it, the desktop's or a headless one, and only that loop can end it as the person's stop or let it go before the ledger
/// moves its record. A file under the home is what every Daoris process on this machine already shares, as the process
/// markers are (<see cref="SessionProcesses"/>), and it needs no port and no service.</para>
///
/// <para><b>Taken once.</b> A loop takes a request by renaming it, which only one rename can do; two loops honouring at
/// once act on it once. The door that asked withdraws it when nothing took it in time, so a late loop never acts on an
/// answer the person was already told did not come. A request nobody took for <see cref="Lifetime"/> is dropped by any
/// loop that looks, so one left by a door that died does not wait for ever.</para>
///
/// <para><b>Built for PAUSE1b too</b> (D132 §2.1, §6.1): a pause stops what of an ask's work another process runs through
/// the same request, a stop with <see cref="RequestDoor.Pause"/>, and records the stop as its own itself.</para>
/// </remarks>
public sealed class SessionRequests(string home)
{
    /// <summary>How long a request nobody took stands: the asking door waits ten seconds (§7.1), and a minute is ample.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private const string Taking = ".taking";

    public string Folder => Path.Combine(home, "sessions", "requests");

    /// <summary>Where a session's request is kept; refused for anything that is not a session id of this machine's.</summary>
    public string PathOf(string session)
    {
        if (!SessionEvents.IsId(session) || session.Contains(':'))
        {
            throw new DriverException($"`{session}` is not a session id of this machine's, so no request can name it.");
        }

        return Path.Combine(Folder, $"{session}.json");
    }

    /// <summary>Ask: the request written beside, then renamed, replacing one for the same session that nobody took yet.</summary>
    public void Write(SessionRequest request)
    {
        var path = PathOf(request.Session);
        if (!SessionMove.Known(request.Move)) throw new DriverException($"`{request.Move}` is not a move a request carries.");
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(path, ToJson(request));
    }

    /// <summary>
    /// The requests waiting, oldest first. One older than <see cref="Lifetime"/>, or one that does not read and is as old,
    /// is dropped as this looks.
    /// </summary>
    public IReadOnlyList<SessionRequest> Pending(DateTimeOffset now)
    {
        if (!Directory.Exists(Folder)) return [];
        var pending = new List<SessionRequest>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*.json"))
        {
            var request = Read(path);
            var written = File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : now;
            if (now - (request?.At ?? written) > Lifetime)
            {
                Remove(path);
                continue;
            }

            if (request is not null) pending.Add(request);
        }

        return [.. pending.OrderBy(request => request.At)];
    }

    /// <summary>Whether a request for this session still waits to be taken.</summary>
    public bool IsPending(string session) => File.Exists(PathOf(session));

    /// <summary>
    /// Take a session's request, once: renamed aside, read and removed. Null when another loop took it first or its asker
    /// withdrew it.
    /// </summary>
    public SessionRequest? Take(string session)
    {
        var path = PathOf(session);
        var taking = path + "." + Guid.NewGuid().ToString("N")[..8] + Taking;
        try
        {
            File.Move(path, taking);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var request = Read(taking);
        Remove(taking);
        return request;
    }

    /// <summary>Withdraw a session's request while nobody has taken it; false when it was taken, or never written.</summary>
    public bool Withdraw(string session)
    {
        var path = PathOf(session);
        if (!File.Exists(path)) return false;
        return Remove(path);
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
            // Held for a moment by whoever read it: the next look removes it.
            return false;
        }
    }

    /// <summary>One request, or null where the file does not read as one.</summary>
    private static SessionRequest? Read(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string? Text(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            if (Text("session") is not { Length: > 0 } session || Text("move") is not { } move || !SessionMove.Known(move)) return null;
            var at = DateTimeOffset.TryParse(Text("at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
                ? when
                : DateTimeOffset.MinValue;
            return new SessionRequest(session, move, at)
            {
                Note = Text("note"),
                Parked = root.TryGetProperty("parked", out var parked) && parked.ValueKind == JsonValueKind.True,
                By = Text("by") ?? RequestDoor.Terminal,
            };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(SessionRequest request)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            // The person's words stay readable in the file, 中文 included.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer.WriteStartObject();
            writer.WriteString("session", request.Session);
            writer.WriteString("move", request.Move);
            if (request.Note is not null) writer.WriteString("note", request.Note);
            writer.WriteBoolean("parked", request.Parked);
            writer.WriteString("by", request.By);
            writer.WriteString("at", request.At.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }
}
