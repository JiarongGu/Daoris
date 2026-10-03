using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The moves a request may carry (D126 §7.1): the person's three on a live or parked session, and their words to any session
/// of this machine's (MSG1e).
/// </summary>
public static class SessionMove
{
    /// <summary>End it now, as the person's stop: its quest is then held here until *Try again* (SESSUX1b).</summary>
    public const string Stop = "stop";

    /// <summary>Finish one that waits on the person, with their note.</summary>
    public const string Finish = "finish";

    /// <summary>Decline one that waits on the person, with their reason.</summary>
    public const string Decline = "decline";

    /// <summary>
    /// Words said to a session from a terminal (MSG1e, D137 §5.2): held at its door where it runs, or kept on its record for it
    /// to go on with. Answered beside the request (<see cref="SessionRequests.Answer"/>), since no state move says where they went.
    /// </summary>
    public const string Say = "say";

    public static bool Known(string move) => move is Stop or Finish or Decline or Say;
}

/// <summary>Who asked, as the machine log's <c>door</c> names a door: a word, never a sentence.</summary>
public static class RequestDoor
{
    /// <summary><c>daoris-driver sessions</c>.</summary>
    public const string Terminal = "terminal";

    /// <summary>A pause stopping what of its work another process runs (PAUSE1b, D132 §2.1): <see cref="WorkPausing"/>.</summary>
    public const string Pause = "pause";

    /// <summary>An abandon stopping or ending what of its work another process runs (PAUSE1d, D132 §3.4): <see cref="WorkAbandoning"/>.</summary>
    public const string Abandon = "abandon";
}

/// <summary>One move asked of one session, for whichever loop on the home runs it.</summary>
/// <param name="Move">One of <see cref="SessionMove"/>.</param>
/// <param name="At">When it was asked: a request nobody took for <see cref="SessionRequests.Lifetime"/> is dropped.</param>
public sealed record SessionRequest(string Session, string Move, DateTimeOffset At)
{
    /// <summary>
    /// A finish's note or a decline's reason, the person's words; for a stop, none, or a pause's words for the record
    /// (PAUSE1b, design §4.1).
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// That the record waited on the person when it was asked: its stop is then its card's, the record moved by the ledger
    /// once the process is let go, as <c>RESOLVE_SESSION</c>'s <c>stopped</c> moves it (D126 §3.3).
    /// </summary>
    public bool Parked { get; init; }

    /// <summary>Who asked, one of <see cref="RequestDoor"/>.</summary>
    public string By { get; init; } = RequestDoor.Terminal;

    /// <summary>A say's words, as the person said them (MSG1e); null for a move.</summary>
    public string? Text { get; init; }

    /// <summary>A say's files, by the names they are kept under for the session (<see cref="ChatFiles"/>), never a path.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>
    /// A say's own name (MSG1e): <c>&lt;id&gt;.&lt;key&gt;.json</c>, so two said at once both wait and a stop of the same session is
    /// not replaced by them, and its answer is found beside it. Null for a move, which is one per session.
    /// </summary>
    public string? Key { get; init; }
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
///
/// <para><b>And for a terminal's words</b> (MSG1e, D137 §5.2): a say is named by its own key beside the session's id, and the
/// loop that takes it writes where the words stand beside it (<see cref="Answer"/>), since a say moves no record the asker could
/// watch. An answer nobody read is dropped with the requests nobody took.</para>
/// </remarks>
public sealed partial class SessionRequests(string home)
{
    /// <summary>How long a request nobody took stands: the asking door waits ten seconds (§7.1), and a minute is ample.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private const string Taking = ".taking";

    private const string Answered = ".answer.json";

    public string Folder => Path.Combine(home, "sessions", "requests");

    /// <summary>A fresh key for a say (MSG1e): enough of a random id that two said at once never share one.</summary>
    public static string NewKey() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>Where a request is kept: a move under its session's id, a say under that and its own key.</summary>
    public string PathOf(SessionRequest request) => request.Key is null ? PathOf(request.Session) : Beside(request, ".json");

    /// <summary>Where a say's answer is kept, beside it, for the door that asked.</summary>
    private string AnswerPath(SessionRequest request) => Beside(request, Answered);

    private string Beside(SessionRequest request, string suffix)
    {
        PathOf(request.Session);
        if (request.Key is not { } key || !KeyShape().IsMatch(key))
        {
            throw new DriverException($"`{request.Key}` is not a request's key, so no request can be named by it.");
        }

        return Path.Combine(Folder, $"{request.Session}.{key}{suffix}");
    }

    [GeneratedRegex("^[0-9a-f]{6,32}$")]
    private static partial Regex KeyShape();

    /// <summary>Where a session's request is kept; refused for anything that is not a session id of this machine's.</summary>
    public string PathOf(string session)
    {
        if (!SessionEvents.IsId(session) || session.Contains(':'))
        {
            throw new DriverException($"`{session}` is not a session id of this machine's, so no request can name it.");
        }

        return Path.Combine(Folder, $"{session}.json");
    }

    /// <summary>
    /// Ask: the request written beside, then renamed, replacing one for the same session that nobody took yet; a say,
    /// named by its own key, replaces nothing.
    /// </summary>
    public void Write(SessionRequest request)
    {
        var path = PathOf(request);
        if (!SessionMove.Known(request.Move)) throw new DriverException($"`{request.Move}` is not a move a request carries.");
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(path, ToJson(request));
    }

    /// <summary>
    /// The requests waiting, oldest first. One older than <see cref="Lifetime"/>, or one that does not read and is as old,
    /// is dropped as this looks; so is a say's answer nobody read, which is never opened here, since its asker reads it.
    /// </summary>
    public IReadOnlyList<SessionRequest> Pending(DateTimeOffset now)
    {
        if (!Directory.Exists(Folder)) return [];
        var pending = new List<SessionRequest>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*.json"))
        {
            // 🔴 A look every second that opened the answer its asker was reading made that read fail, or its removal (MSG1e).
            if (path.EndsWith(Answered, StringComparison.Ordinal))
            {
                if (File.Exists(path) && now - new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) > Lifetime) Remove(path);
                continue;
            }

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
    public SessionRequest? Take(string session) => TakeAt(PathOf(session));

    /// <summary>Take this request, once, by its own name: a say by its key. Null when another loop took it first or its asker withdrew it.</summary>
    public SessionRequest? Take(SessionRequest request) => TakeAt(PathOf(request));

    private static SessionRequest? TakeAt(string path)
    {
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
    public bool Withdraw(string session) => WithdrawAt(PathOf(session));

    /// <summary>Withdraw this request by its own name while nobody has taken it: a say by its key.</summary>
    public bool Withdraw(SessionRequest request) => WithdrawAt(PathOf(request));

    /// <summary>
    /// Withdrawn by renaming it aside, as a loop takes it, so of a withdrawal and a take only one wins; a look reading it that
    /// moment holds it for milliseconds, and is waited out rather than read as a take (MSG1e).
    /// </summary>
    private static bool WithdrawAt(string path)
    {
        var withdrawing = path + "." + Guid.NewGuid().ToString("N")[..8] + ".withdrawing";
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

        Remove(withdrawing);
        return true;
    }

    /// <summary>
    /// Where a say's words stand, written beside it by the loop that took it (MSG1e), for the door that asked: atomically, so
    /// the asker reads the whole of it or nothing.
    /// </summary>
    public void Answer(SessionRequest request, WordsHeld held)
    {
        var path = AnswerPath(request);
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(path, ToJson(held));
    }

    /// <summary>
    /// A say's answer, read once and removed; null while none is written. One held open that moment is read at the next ask,
    /// never dropped; one that does not read as an answer is removed.
    /// </summary>
    public WordsHeld? AnswerOf(SessionRequest request)
    {
        var path = AnswerPath(request);
        string text;
        try
        {
            if (!File.Exists(path)) return null;
            text = File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        for (var attempt = 0; attempt < 10 && !Remove(path); attempt++) Thread.Sleep(10);
        return ReadAnswer(text);
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
                Text = Text("text"),
                Files = root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array
                    ? [.. files.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!)]
                    : [],
                Key = Text("key"),
            };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>One answer, or null where the text does not read as one.</summary>
    private static WordsHeld? ReadAnswer(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("sent", out var sent)) return null;
            string? Text(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            return new WordsHeld(sent.ValueKind == JsonValueKind.True, Text("reaches"), Text("why"))
            {
                Word = Text("word"),
                Message = Text("message"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(SessionRequest request) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("session", request.Session);
        writer.WriteString("move", request.Move);
        if (request.Note is not null) writer.WriteString("note", request.Note);
        writer.WriteBoolean("parked", request.Parked);
        writer.WriteString("by", request.By);
        writer.WriteString("at", request.At.ToString("O", CultureInfo.InvariantCulture));
        if (request.Text is not null) writer.WriteString("text", request.Text);
        if (request.Files.Count > 0)
        {
            writer.WriteStartArray("files");
            foreach (var name in request.Files) writer.WriteStringValue(name);
            writer.WriteEndArray();
        }

        if (request.Key is not null) writer.WriteString("key", request.Key);
        writer.WriteEndObject();
    });

    private static string ToJson(WordsHeld held) => Written(writer =>
    {
        writer.WriteStartObject();
        writer.WriteBoolean("sent", held.Sent);
        if (held.Reaches is not null) writer.WriteString("reaches", held.Reaches);
        if (held.Why is not null) writer.WriteString("why", held.Why);
        if (held.Word is not null) writer.WriteString("word", held.Word);
        if (held.Message is not null) writer.WriteString("message", held.Message);
        writer.WriteEndObject();
    });

    private static string Written(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            // The person's words stay readable in the file, 中文 included.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            write(writer);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }
}
