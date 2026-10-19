using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Knowledge;

/// <summary>One brainstorm as its records stand (BRAINSTORM1a3): its request, where it stands, its packet's digest and the person's send.</summary>
/// <param name="State"><c>asked</c> before the driver's look froze its packet, then the driver's own: <c>frozen</c>, <c>running</c>, <c>ended</c>.</param>
/// <param name="Packet">The frozen packet's digest; null before the look froze one.</param>
/// <param name="Sent">The digest the person sent; null where they have not.</param>
public sealed record BrainstormStanding(
    string Id, string Question, string? Repository, IReadOnlyList<string> Agents, DateTimeOffset At, string State, string? Code,
    string? Packet, string? Sent);

/// <summary>A brainstorm shown: where it stands, the packet as frozen (null before the look froze one) and the document's Markdown.</summary>
public sealed record BrainstormShown(BrainstormStanding Brainstorm, JsonElement? Frozen, string? Document);

/// <summary>Why a door to a brainstorm did not do what it was asked; <see cref="None"/> where it did.</summary>
public enum BrainstormRefusal
{
    None,

    /// <summary>No brainstorm by that id on this machine, or an id that is not a plain name.</summary>
    NotFound,

    /// <summary>A question or its agents out of their bounds.</summary>
    BadShape,

    /// <summary>A state the brainstorm is in: no packet yet to send, or running and not to be cleared.</summary>
    State,

    /// <summary>A clear the disk would not finish.</summary>
    NotWhole,

    /// <summary>No Daoris home to keep it in.</summary>
    NoHome,
}

/// <summary>What a door did, the desk's sentence, and the brainstorm as it now stands where there is one.</summary>
public sealed record BrainstormOutcome(BrainstormRefusal Refusal, string Message, BrainstormStanding? Brainstorm = null);

/// <summary>
/// The local host's brainstorm desk (BRAINSTORM1a3; D176 points 1, 2 and 8): the door a terminal's <c>daoris brainstorm</c> reaches.
/// It writes the request the driver's look takes, the person's send, and the clear; it reads where each brainstorm stands. It runs
/// no pass and spawns nothing: the passes are the driver's, in whichever process holds the loop.
/// </summary>
/// <remarks>
/// <para><b>A twin of the driver's <c>BrainstormFiles</c></b>, sharing no code: the files under the home's <c>brainstorms</c> folder
/// are the contract, one writer each. <c>request.json</c> is the verb's or this door's, written once; <c>state.json</c>,
/// <c>packet.json</c> and the document are the driver's alone; <c>sent.json</c> is the person's press, by either door. Both readers
/// are held, row for row, to one table, the driver suite's <c>fixtures/brainstorm-files.json</c>; a change here is a change there.</para>
///
/// <para><b>This machine's alone</b> (D176 point 8): the host maps the door in local mode only and answers it to a caller on this
/// machine only; a shared host keeps no brainstorm, and nothing here is a quest operation or synced.</para>
/// </remarks>
/// <param name="home">The home the records are under, or null when there is none: every door then says so.</param>
public sealed class BrainstormDesk(string? home)
{
    /// <summary>A question's most characters: the driver's <c>BrainstormBounds.Question</c>, duplicated deliberately (twins.md).</summary>
    public const int MostQuestion = 4_000;

    /// <summary>A request's most agents: the driver's <c>DriverBrainstormLimits.MostAgents</c>, duplicated deliberately.</summary>
    public const int MostAgents = 4;

    /// <summary>The driver's states (<c>BrainstormStates</c>), and this desk's own word for a request no look has frozen yet.</summary>
    public const string Asked = "asked";

    public const string Frozen = "frozen";

    public const string Running = "running";

    public const string Ended = "ended";

    private static readonly Regex PlainName = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);

    public string? Home { get; } = home;

    /// <summary>The home this host was started on (D63), or none.</summary>
    public static BrainstormDesk FromEnvironment() => new(DaorisHome.Resolve());

    /// <summary>
    /// The person's question, written as a request the driver's look freezes into a packet: the words trimmed, a blank repository
    /// none, the agents named in order without blanks. Refused, with nothing written, out of the driver verb's bounds.
    /// </summary>
    public BrainstormOutcome Ask(string? question, string? repository, IReadOnlyList<string?>? agents, DateTimeOffset at)
    {
        var words = question?.Trim() ?? "";
        if (words.Length == 0)
        {
            return new(BrainstormRefusal.BadShape, "A brainstorm needs its question: what you want proposals for. Nothing was asked.");
        }

        if (words.Length > MostQuestion)
        {
            return new(BrainstormRefusal.BadShape,
                $"A brainstorm's question is 1 to {MostQuestion} characters; this one is {words.Length}. Nothing was asked.");
        }

        var named = (agents ?? []).Select(agent => agent?.Trim() ?? "").Where(agent => agent.Length > 0).ToList();
        if (named.Count > MostAgents)
        {
            return new(BrainstormRefusal.BadShape, $"A brainstorm runs at most {MostAgents} agents; {named.Count} were named. Nothing was asked.");
        }

        if (named.FirstOrDefault(agent => agent.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) is { } spaced)
        {
            return new(BrainstormRefusal.BadShape,
                $"An agent is named as one word, as it is listed in Settings, and `{spaced}` is not. Nothing was asked.");
        }

        if (Home is null) return new(BrainstormRefusal.NoHome, $"{DaorisHome.Sentence} Nothing was asked.");

        var of = string.IsNullOrWhiteSpace(repository) ? null : repository.Trim();
        var id = NewId();
        Write(PathOf(id, "request.json"), writer =>
        {
            writer.WriteNumber("brainstorm", 1);
            writer.WriteString("id", id);
            writer.WriteString("question", words);
            if (of is null) writer.WriteNull("repository");
            else writer.WriteString("repository", of);
            writer.WriteStartArray("agents");
            foreach (var agent in named) writer.WriteStringValue(agent);
            writer.WriteEndArray();
            writer.WriteString("at", Moments.Write(at));
        });

        return new(BrainstormRefusal.None,
            $"Brainstorm `{id}` asked. Daoris freezes its packet at its next look, from "
            + (of is null ? "the question alone" : $"its knowledge of `{of}`")
            + "; with no Daoris running, it waits. Read the packet before you send it: nothing goes to any agent until you do.",
            Find(id));
    }

    /// <summary>Every brainstorm on this machine, oldest first, as the driver lists them; a request that does not read is none.</summary>
    public IReadOnlyList<BrainstormStanding> List()
    {
        if (Home is null) return [];
        var folder = DaorisHome.Entry(Home, HomeEntry.Brainstorms);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.EnumerateDirectories(folder).Select(Path.GetFileName).OfType<string>().Select(Find).OfType<BrainstormStanding>()
            .OrderBy(each => each.At).ThenBy(each => each.Id, StringComparer.Ordinal)];
    }

    /// <summary>One brainstorm as its records stand, or null where there is no readable request by that id.</summary>
    public BrainstormStanding? Find(string id)
    {
        if (Home is null || !PlainName.IsMatch(id) || Reserved(id) || Parse(PathOf(id, "request.json")) is not { } request) return null;
        if (Text(request, "question") is not { Length: > 0 } question) return null;

        var agents = request.TryGetProperty("agents", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? listed.EnumerateArray().Where(agent => agent.ValueKind == JsonValueKind.String).Select(agent => agent.GetString()!).ToList()
            : [];
        var state = Parse(PathOf(id, "state.json")) is { } stood && Text(stood, "state") is { } named ? (named, stood) : (Asked, default(JsonElement?));
        var sent = Parse(PathOf(id, "sent.json")) is { } send ? Text(send, "packet") : null;
        return new BrainstormStanding(
            id, question, Text(request, "repository"), agents, Moments.Read(request, "at") ?? DateTimeOffset.UnixEpoch, state.Item1,
            state.Item2 is { } s ? Text(s, "code") : null, state.Item2 is { } p ? Text(p, "packet") : null, sent);
    }

    /// <summary>One brainstorm, with the packet as frozen once the look froze one, and the document's Markdown once it was kept.</summary>
    public BrainstormShown? Show(string id)
    {
        if (Find(id) is not { } brainstorm) return null;
        var frozen = brainstorm.State == Asked ? null : Parse(PathOf(id, "packet.json"));
        return new BrainstormShown(brainstorm, frozen, Read(PathOf(id, "document.md")));
    }

    /// <summary>
    /// The person's send (D176 point 2): the frozen packet's digest written as the one that goes, as the driver's verb writes it.
    /// Nothing to send before the look froze a packet; sent again, it is the same brainstorm, never a second spend.
    /// </summary>
    public BrainstormOutcome Send(string id, DateTimeOffset at)
    {
        if (Find(id) is not { } brainstorm) return NotFound(id);
        if (brainstorm.State == Asked)
        {
            return new(BrainstormRefusal.State,
                $"Brainstorm `{id}` has no packet yet: Daoris freezes it at its next look, and you read it before it goes.", brainstorm);
        }

        if (brainstorm is not { State: Frozen, Packet: { } digest })
        {
            return new(BrainstormRefusal.None, $"Brainstorm `{id}` was already sent ({brainstorm.State}).", brainstorm);
        }

        if (brainstorm.Sent == digest)
        {
            return new(BrainstormRefusal.None, $"Brainstorm `{id}` is already sent: the same brainstorm, never a second spend.", brainstorm);
        }

        Write(PathOf(id, "sent.json"), writer =>
        {
            writer.WriteString("packet", digest);
            writer.WriteString("at", Moments.Write(at));
        });
        return new(BrainstormRefusal.None,
            $"Brainstorm `{id}` sent, packet `{digest}`: its passes start at Daoris's next look.", Find(id));
    }

    /// <summary>The brainstorm cleared (D176 point 8): its records, its packet and its rooms; never while it runs.</summary>
    public BrainstormOutcome Clear(string id)
    {
        if (Home is null || !PlainName.IsMatch(id) || Reserved(id)) return NotFound(id);
        var folder = DaorisHome.Entry(Home, HomeEntry.Brainstorms, id);
        var brainstorm = Find(id);
        if (brainstorm is null && !Directory.Exists(folder)) return NotFound(id);
        if (brainstorm is { State: Running })
        {
            return new(BrainstormRefusal.State, $"Brainstorm `{id}` is running; it is cleared once it ends.", brainstorm);
        }

        if ((Remove(DaorisHome.Entry(Home, HomeEntry.BrainstormRooms, id)) ?? Remove(folder)) is { } left)
        {
            return new(BrainstormRefusal.NotWhole, $"Brainstorm `{id}` could not be cleared whole: {left}", brainstorm);
        }

        return new(BrainstormRefusal.None, $"Brainstorm `{id}` cleared: its records and its packet are gone from this machine.", brainstorm);
    }

    /// <summary>
    /// The folder that holds every meeting is no brainstorm's id (AGENTROOM1a; BRAINSTORM-RESERVED1): the driver's
    /// <c>BrainstormFiles.Reserved</c>, duplicated deliberately (twins.md). Every door checks it before the disk, so a clear can
    /// never take the meetings' records.
    /// </summary>
    public static bool Reserved(string id) => string.Equals(id.Trim(), "meetings", StringComparison.OrdinalIgnoreCase);

    /// <summary>The sentence for an id no brainstorm answers to, the reserved one with its own reason.</summary>
    public static string NotFoundSentence(string id) => Reserved(id)
        ? "`meetings` holds the meetings' records, not a brainstorm."
        : $"There is no brainstorm `{id.Trim()}` on this machine.";

    private static BrainstormOutcome NotFound(string id) => new(BrainstormRefusal.NotFound, NotFoundSentence(id));

    private string PathOf(string id, string file) => DaorisHome.Entry(Home!, HomeEntry.Brainstorms, id, file);

    /// <summary>A new id, as the driver's verb makes one: eight hex digits, and never one a brainstorm here already has.</summary>
    private string NewId()
    {
        while (true)
        {
            var id = Guid.NewGuid().ToString("N")[..8];
            if (!Directory.Exists(DaorisHome.Entry(Home!, HomeEntry.Brainstorms, id))) return id;
        }
    }

    private static string? Remove(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(folder, recursive: true);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    /// <summary>A file written whole and atomically, indented, LF, as the driver writes its own.</summary>
    private static void Write(string path, Action<Utf8JsonWriter> body)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        AtomicFile.WriteText(path, Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n"));
    }

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A file's root when it is a JSON object; null where it is missing, does not read, or is not one.</summary>
    private static JsonElement? Parse(string path) => Read(path) is { } text ? JsonFields.ParseObject(text) : null;

    private static string? Text(JsonElement element, string name) => JsonFields.Text(element, name);
}
