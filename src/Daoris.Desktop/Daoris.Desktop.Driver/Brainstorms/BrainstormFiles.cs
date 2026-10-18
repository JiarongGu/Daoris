using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>A brainstorm's request as its verb wrote it (BRAINSTORM1a, D176 point 1): written once, never changed.</summary>
/// <param name="Agents">The adapters the request names, in order; empty for the machine's own agent.</param>
public sealed record BrainstormRequest(string Id, string Question, string? Repository, IReadOnlyList<string> Agents, DateTimeOffset At);

/// <summary>Where a brainstorm stands, as the driver alone writes it (the helper's file shape, D163).</summary>
/// <param name="Packet">The digest of the packet frozen for it; null before the look froze one.</param>
/// <param name="Code">Why it ended as it did, where it did not end with a document's ranking: a fixed code.</param>
public sealed record BrainstormState(string Id, string State, string? Packet, string? Code, DateTimeOffset At);

/// <summary>The states a brainstorm moves through: frozen and shown, then sent by the person, running, and ended.</summary>
public static class BrainstormStates
{
    public const string Frozen = "frozen";
    public const string Running = "running";
    public const string Ended = "ended";

    /// <summary>The code of a brainstorm no agent could answer (D176 point 10): the packet is kept.</summary>
    public const string NoAgent = "no-agent";

    /// <summary>The code of a brainstorm whose synthesis did not finish: the document is unranked.</summary>
    public const string Unranked = "unranked";
}

/// <summary>
/// A brainstorm's records under <c>&lt;home&gt;/brainstorms/&lt;id&gt;/</c> (D176 point 8, D172's <c>data/brainstorms</c>), each file written
/// atomically by one writer: <c>request.json</c> by the verb, <c>sent.json</c> by the person's send, <c>state.json</c>,
/// <c>packet.json</c>, <c>passes/</c> and the document by the driver, and <c>answers/&lt;turn&gt;.json</c> by the pass's result tool.
/// The rooms are under <c>brainstorm-rooms/&lt;id&gt;/</c> (D172's <c>work/</c>), and go when the brainstorm ends.
/// </summary>
public sealed class BrainstormFiles(string home)
{
    public string Folder(string id) => DaorisHome.Entry(home, HomeEntry.Brainstorms, id);

    public string PathOf(string id, params string[] parts) => Path.Combine([Folder(id), .. parts]);

    /// <summary>The rooms of one brainstorm, one folder per turn.</summary>
    public string Rooms(string id) => DaorisHome.Entry(home, HomeEntry.BrainstormRooms, id);

    /// <summary>The file a turn's result tool writes its answer to: <c>brainstorm_give</c>'s or <c>brainstorm_synthesize</c>'s.</summary>
    public string AnswerPath(string id, string turn) => PathOf(id, "answers", $"{turn}.json");

    /// <summary>The request a person's terminal writes, atomically: its id, which names the brainstorm everywhere.</summary>
    public string WriteRequest(string question, string? repository, IReadOnlyList<string> agents, DateTimeOffset at)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        Write(PathOf(id, "request.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("brainstorm", BrainstormAnswers.Schema);
            writer.WriteString("id", id);
            writer.WriteString("question", question);
            if (repository is null) writer.WriteNull("repository");
            else writer.WriteString("repository", repository);
            writer.WriteStartArray("agents");
            foreach (var agent in agents) writer.WriteStringValue(agent);
            writer.WriteEndArray();
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        });
        return id;
    }

    /// <summary>Every request on file, oldest first; one unreadable is not a request here.</summary>
    public IReadOnlyList<BrainstormRequest> Requests()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Brainstorms);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.EnumerateDirectories(folder).Select(Path.GetFileName).OfType<string>().Select(Request).OfType<BrainstormRequest>()
            .OrderBy(request => request.At).ThenBy(request => request.Id, StringComparer.Ordinal)];
    }

    public BrainstormRequest? Request(string id)
    {
        if (Parse(PathOf(id, "request.json")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "question") is not { Length: > 0 } question) return null;
            var agents = element.TryGetProperty("agents", out var named) && named.ValueKind == JsonValueKind.Array
                ? named.EnumerateArray().Where(agent => agent.ValueKind == JsonValueKind.String).Select(agent => agent.GetString()!).ToList()
                : [];
            return new BrainstormRequest(id, question, Text(element, "repository"), agents, Moments.Read(element, "at") ?? DateTimeOffset.UnixEpoch);
        }
    }

    public BrainstormState? ReadState(string id)
    {
        if (Parse(PathOf(id, "state.json")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            return Text(element, "state") is { } state
                ? new BrainstormState(id, state, Text(element, "packet"), Text(element, "code"), Moments.Read(element, "at") ?? DateTimeOffset.UnixEpoch)
                : null;
        }
    }

    /// <summary>Keep one brainstorm's state whole, atomically: the driver is its only writer.</summary>
    public void WriteState(BrainstormState state) =>
        Write(PathOf(state.Id, "state.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", state.Id);
            writer.WriteString("state", state.State);
            if (state.Packet is null) writer.WriteNull("packet");
            else writer.WriteString("packet", state.Packet);
            if (state.Code is null) writer.WriteNull("code");
            else writer.WriteString("code", state.Code);
            writer.WriteString("at", Moments.Write(state.At));
            writer.WriteEndObject();
        });

    /// <summary>The packet, frozen: written once, and never changed after (a new packet is a new brainstorm).</summary>
    public void WritePacket(string id, byte[] bytes) => Bytes(PathOf(id, BrainstormInstruction.PacketFile), bytes);

    public byte[]? PacketBytes(string id) => ReadBytes(PathOf(id, BrainstormInstruction.PacketFile));

    /// <summary>The person's send (D176 point 2): the digest of the packet they saw, which is the one that goes.</summary>
    public void WriteSent(string id, string digest, DateTimeOffset at) =>
        Write(PathOf(id, "sent.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("packet", digest);
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        });

    /// <summary>The digest the person sent, or null where they have not.</summary>
    public string? Sent(string id)
    {
        if (Parse(PathOf(id, "sent.json")) is not { } root) return null;
        using (root) return Text(root.RootElement, "packet");
    }

    /// <summary>A turn's answer as its tool wrote it; null where it wrote none.</summary>
    public string? ReadAnswer(string id, string turn) => ReadText(AnswerPath(id, turn));

    /// <summary>One pass's record, kept with the brainstorm.</summary>
    public void WritePass(string id, BrainstormPassRecord pass) =>
        Write(PathOf(id, "passes", $"{pass.By.Pass}.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("pass", pass.By.Pass);
            writer.WriteString("role", pass.Role);
            writer.WriteString("maker", pass.By.Maker);
            writer.WriteString("adapter", pass.By.Adapter);
            if (pass.By.Account is null) writer.WriteNull("account");
            else writer.WriteString("account", pass.By.Account);
            writer.WriteNull("model");
            writer.WriteString("state", pass.State);
            writer.WriteNumber("turns", pass.Turns);
            writer.WriteStartArray("codes");
            foreach (var code in pass.Codes) writer.WriteStringValue(code);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    /// <summary>The document, as JSON and as its Markdown rendering.</summary>
    public void WriteDocument(string id, BrainstormDocument document)
    {
        Bytes(PathOf(id, "document.json"), BrainstormDocuments.Json(document));
        Bytes(PathOf(id, "document.md"), System.Text.Encoding.UTF8.GetBytes(BrainstormDocuments.Markdown(document)));
    }

    public string? DocumentText(string id) => ReadText(PathOf(id, "document.md"));

    /// <summary>
    /// A turn's room: the packet, byte for byte the frozen file, its instruction, and for a synthesis the anonymous proposals; nothing
    /// else, no checkout and no repository path.
    /// </summary>
    public string OpenRoom(string id, string turn, byte[] packet, string instruction, byte[]? proposals = null)
    {
        var room = Path.Combine(Rooms(id), turn);
        if (Directory.Exists(room)) Directory.Delete(room, recursive: true);
        Directory.CreateDirectory(room);
        File.WriteAllBytes(Path.Combine(room, BrainstormInstruction.PacketFile), packet);
        File.WriteAllText(Path.Combine(room, BrainstormInstruction.InstructionFile), instruction);
        if (proposals is not null) File.WriteAllBytes(Path.Combine(room, BrainstormInstruction.ProposalsFile), proposals);
        return room;
    }

    /// <summary>The brainstorm's rooms removed; what could not be, said, never thrown.</summary>
    public string? RemoveRooms(string id) => Remove(Rooms(id));

    /// <summary>The brainstorm cleared (D176 point 8): its records, its packet and its rooms. What could not be removed is said.</summary>
    public string? Clear(string id) => RemoveRooms(id) ?? Remove(Folder(id));

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

    private static void Write(string path, Action<Utf8JsonWriter> write)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        JsonFile.Write(path, write);
    }

    private static void Bytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteBytes(path, bytes);
    }

    private static byte[]? ReadBytes(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadText(string path) => ReadBytes(path) is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes) : null;

    private static JsonDocument? Parse(string path)
    {
        try
        {
            return File.Exists(path) ? JsonDocument.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
