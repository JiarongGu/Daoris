using System.Globalization;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>A meeting's request as its verb wrote it (AGENTROOM1a, D177 point 7): written once, never changed.</summary>
/// <param name="Agents">The adapters the person named, in the order their turns go.</param>
public sealed record MeetingRequest(string Id, string Question, string? Repository, IReadOnlyList<string> Agents, DateTimeOffset At);

/// <summary>
/// Where a meeting stands, as the driver alone writes it: <c>frozen</c> with the round whose packet waits for the person's send,
/// <c>running</c> that round, or <c>ended</c> with its code (<see cref="BrainstormStates"/>' words, which a meeting shares).
/// </summary>
/// <param name="Round">The round the state is of: the one frozen, running, or the last that ran.</param>
/// <param name="Packet">The digest of that round's packet; null before the look froze one.</param>
public sealed record MeetingState(string Id, string State, int Round, string? Packet, string? Code, DateTimeOffset At);

/// <summary>An answer Daoris refused (D177's trap, an agent answering for another): the turn it was written for, and why.</summary>
public sealed record MeetingRefused(string Turn, string Why);

/// <summary>What Daoris relayed, in order, and what it refused: the meeting's own record of the conversation, kept by the driver alone.</summary>
public sealed record MeetingTranscript(IReadOnlyList<MeetingMessage> Messages, IReadOnlyList<MeetingRefused> Refused)
{
    public static MeetingTranscript Empty { get; } = new([], []);
}

/// <summary>
/// A meeting's records (AGENTROOM1a, D177 points 1 and 7), under the brainstorms' entry and apart from them:
/// <c>&lt;home&gt;/brainstorms/meetings/&lt;id&gt;/</c> holds <c>request.json</c> (the verb's), <c>sent/&lt;round&gt;.json</c> (the
/// person's press for each round), <c>stop.json</c> (theirs), and <c>state.json</c>, <c>packets/&lt;round&gt;.json</c>,
/// <c>transcript.json</c>, <c>passes/</c> and <c>read/</c> (the driver's); <c>answers/&lt;turn&gt;.json</c> is what a turn wrote. The
/// rooms are under <c>brainstorm-rooms/meetings/&lt;id&gt;/</c>, and go when a round ends. Kept on this machine: nothing travels.
/// </summary>
public sealed class MeetingFiles(string home)
{
    /// <summary>The folder under the brainstorms' entries that holds every meeting, which no brainstorm's id may name.</summary>
    public const string Under = "meetings";

    /// <summary>A meeting's turns as a brainstorm's pass keeps its own: the pass records, the answers, the rooms and the stop.</summary>
    public BrainstormFiles Turns { get; } = new(home, Under);

    public string Folder(string id) => Turns.Folder(id);

    public string PathOf(string id, params string[] parts) => Turns.PathOf(id, parts);

    /// <summary>A turn's id, which names its pass record, its answer and its room: <c>r1-a1</c> for round 1's first participant.</summary>
    public static string Turn(int round, string participant) => string.Create(CultureInfo.InvariantCulture, $"r{round}-{participant}");

    /// <summary>
    /// The request the person's terminal writes, atomically. An id the caller names makes it idempotent: one already on file under
    /// it is kept as it is, never written again.
    /// </summary>
    public string WriteRequest(string question, string? repository, IReadOnlyList<string> agents, DateTimeOffset at, string? named = null)
    {
        var id = named ?? Guid.NewGuid().ToString("N")[..8];
        if (named is not null && File.Exists(PathOf(id, "request.json"))) return id;
        BrainstormFiles.Write(PathOf(id, "request.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("meeting", MeetingPackets.Version);
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

    /// <summary>Every meeting on file, oldest first; one whose request does not read is none.</summary>
    public IReadOnlyList<MeetingRequest> Requests()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Brainstorms, Under);
        if (!Directory.Exists(folder)) return [];
        return [.. Directory.EnumerateDirectories(folder).Select(Path.GetFileName).OfType<string>().Select(Request).OfType<MeetingRequest>()
            .OrderBy(request => request.At).ThenBy(request => request.Id, StringComparer.Ordinal)];
    }

    public MeetingRequest? Request(string id)
    {
        if (BrainstormFiles.Parse(PathOf(id, "request.json")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "question") is not { Length: > 0 } question) return null;
            var agents = element.TryGetProperty("agents", out var named) && named.ValueKind == JsonValueKind.Array
                ? named.EnumerateArray().Where(agent => agent.ValueKind == JsonValueKind.String).Select(agent => agent.GetString()!).ToList()
                : [];
            return new MeetingRequest(id, question, Text(element, "repository"), agents, Moments.Read(element, "at") ?? DateTimeOffset.UnixEpoch);
        }
    }

    public MeetingState? ReadState(string id)
    {
        if (BrainstormFiles.Parse(PathOf(id, "state.json")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            return Text(element, "state") is { } state
                ? new MeetingState(
                    id, state,
                    element.TryGetProperty("round", out var round) && round.ValueKind == JsonValueKind.Number && round.TryGetInt32(out var number) ? number : 1,
                    Text(element, "packet"), Text(element, "code"), Moments.Read(element, "at") ?? DateTimeOffset.UnixEpoch)
                : null;
        }
    }

    /// <summary>Keep one meeting's state whole, atomically: the driver is its only writer.</summary>
    public void WriteState(MeetingState state) =>
        BrainstormFiles.Write(PathOf(state.Id, "state.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", state.Id);
            writer.WriteString("state", state.State);
            writer.WriteNumber("round", state.Round);
            if (state.Packet is null) writer.WriteNull("packet");
            else writer.WriteString("packet", state.Packet);
            if (state.Code is null) writer.WriteNull("code");
            else writer.WriteString("code", state.Code);
            writer.WriteString("at", Moments.Write(state.At));
            writer.WriteEndObject();
        });

    /// <summary>A round's packet, frozen: written once, and never changed after (a refresh is the next round's packet).</summary>
    public void WritePacket(string id, int round, byte[] bytes) => BrainstormFiles.Bytes(PacketPath(id, round), bytes);

    public byte[]? PacketBytes(string id, int round) => BrainstormFiles.ReadBytes(PacketPath(id, round));

    /// <summary>A round's packet read back; null where it is not on file, or does not read as a meeting's.</summary>
    public MeetingPacket? Packet(string id, int round) => PacketBytes(id, round) is { } bytes ? MeetingPackets.Read(bytes) : null;

    private string PacketPath(string id, int round) => PathOf(id, "packets", string.Create(CultureInfo.InvariantCulture, $"{round}.json"));

    /// <summary>The person's press for one round (D177 point 4): the digest of the packet they saw, which is the one that goes.</summary>
    public void WriteSent(string id, int round, string digest, DateTimeOffset at) =>
        BrainstormFiles.Write(SentPath(id, round), writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("round", round);
            writer.WriteString("packet", digest);
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        });

    /// <summary>The digest the person sent for a round, or null where they have not.</summary>
    public string? Sent(string id, int round)
    {
        if (BrainstormFiles.Parse(SentPath(id, round)) is not { } root) return null;
        using (root) return Text(root.RootElement, "packet");
    }

    private string SentPath(string id, int round) => PathOf(id, "sent", string.Create(CultureInfo.InvariantCulture, $"{round}.json"));

    /// <summary>The person's stop (D177's trap, the person leaving): the meeting ends at once, and nothing more goes.</summary>
    public void WriteStop(string id, DateTimeOffset at) => Turns.WriteStop(id, at);

    public bool Stopped(string id) => Turns.Stopped(id);

    public MeetingTranscript ReadTranscript(string id)
    {
        if (BrainstormFiles.Parse(PathOf(id, "transcript.json")) is not { } root) return MeetingTranscript.Empty;
        using (root)
        {
            try
            {
                var element = root.RootElement;
                var messages = BrainstormPackets.Items(element, "messages").Select(MeetingPackets.Message).ToList();
                var refused = BrainstormPackets.Items(element, "refused").Select(each => new MeetingRefused(Text(each, "turn") ?? "", Text(each, "why") ?? "")).ToList();
                return new MeetingTranscript(messages, refused);
            }
            catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or FormatException)
            {
                return MeetingTranscript.Empty;
            }
        }
    }

    /// <summary>Keep the transcript whole, atomically, as each message is relayed: the driver is its only writer.</summary>
    public void WriteTranscript(string id, MeetingTranscript transcript) =>
        BrainstormFiles.Write(PathOf(id, "transcript.json"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("meeting", MeetingPackets.Version);
            writer.WriteStartArray("messages");
            foreach (var message in transcript.Messages) MeetingPackets.Write(writer, message);
            writer.WriteEndArray();
            writer.WriteStartArray("refused");
            foreach (var refused in transcript.Refused)
            {
                writer.WriteStartObject();
                writer.WriteString("turn", refused.Turn);
                writer.WriteString("why", refused.Why);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

    /// <summary>The turns with an answer waiting in the meeting's answers, which Daoris has not read: each by its turn's id.</summary>
    public IReadOnlyList<string> Waiting(string id)
    {
        var folder = PathOf(id, "answers");
        return Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>
    /// A turn's answer, once its turn ended and Daoris read it, moved out of the answers to <c>read/</c>, kept as what the turn wrote:
    /// so an answer written for it after is a second answer, and refused (<see cref="Refuse"/>).
    /// </summary>
    public void Read(string id, string turn)
    {
        var answer = Turns.AnswerPath(id, turn);
        if (!File.Exists(answer)) return;
        var kept = PathOf(id, "read", $"{turn}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(kept)!);
        File.Move(answer, kept, overwrite: true);
    }

    /// <summary>An answer written outside its turn: removed unread, so it reaches no one.</summary>
    public void Refuse(string id, string turn)
    {
        var answer = Turns.AnswerPath(id, turn);
        if (File.Exists(answer)) File.Delete(answer);
    }

    /// <summary>The meeting's rooms removed; what could not be, said, never thrown.</summary>
    public string? RemoveRooms(string id) => Turns.RemoveRooms(id);

    /// <summary>The meeting cleared: its records, its packets and its rooms. What could not be removed is said.</summary>
    public string? Clear(string id) => Turns.Clear(id);
}
