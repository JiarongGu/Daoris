using System.Security.Cryptography;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A participant of a meeting as its packet names it (AGENTROOM1a, D177 point 1): Daoris's id for it, <c>a1</c> and on in the
/// person's order, and the maker and adapter its turns run on. Never an account: a packet names none.
/// </summary>
public sealed record MeetingParticipant(string Id, string Maker, string Adapter);

/// <summary>
/// One message as Daoris relays it (D177 point 1): the round, the participant whose turn carried it, that participant's maker and
/// adapter from Daoris's own records (never from what the message says), its text, and the digest of the packet it answered.
/// </summary>
public sealed record MeetingMessage(int Round, string Participant, string Maker, string Adapter, string Text, string Packet);

/// <summary>
/// A meeting's packet (AGENTROOM1a, D177 point 2): the brainstorm's assembly of the question and its sources (D176 point 2,
/// unchanged), the participants in the person's order, the round it is for, and the transcript so far. Frozen once per round as
/// <c>meeting: 1</c>, named by the digest of its content, and handed byte for byte to every turn of its round.
/// </summary>
public sealed record MeetingPacket(
    string Question, string? Repository, string? Tier, IReadOnlyList<BrainstormSource> Sources, IReadOnlyList<BrainstormLeftOut> LeftOut,
    IReadOnlyList<MeetingParticipant> Participants, int Round, IReadOnlyList<MeetingMessage> Transcript)
{
    /// <summary>The digest of its content, <c>sha256:</c> and the hex, which names it everywhere.</summary>
    public string Digest => MeetingPackets.DigestOf(this);
}

/// <summary>A meeting packet's assembly, its refresh, its bytes and its digest, with no model and no I/O.</summary>
public static class MeetingPackets
{
    /// <summary>The schema's version, written as <c>meeting</c>: a brainstorm's packet is <c>packet</c>, and neither reads as the other.</summary>
    public const int Version = 1;

    /// <summary>The most characters one message keeps (a first value, read again on the first real run, as the packet's bounds are).</summary>
    public const int MessageChars = 4_000;

    /// <summary>The rounds a meeting holds in this slice (D177 point 7): one, and a second only by the person's press.</summary>
    public const int MostRounds = 2;

    /// <summary>A meeting turn's role in its pass record, beside a brainstorm's <c>generate</c> and <c>synthesize</c>.</summary>
    public const string Say = "say";

    /// <summary>The first round's packet: the brainstorm's assembly, the participants in order, and no transcript yet.</summary>
    public static MeetingPacket Freeze(BrainstormPacket basis, IReadOnlyList<MeetingParticipant> participants) =>
        new(basis.Question, basis.Repository, basis.Tier, basis.Sources, basis.LeftOut, [.. participants], 1, []);

    /// <summary>
    /// The next round's packet (D177 point 2): the same question, sources and participants, and the transcript so far, under a new
    /// digest. Nothing else changes, so what a refresh adds is the messages alone.
    /// </summary>
    public static MeetingPacket Refresh(MeetingPacket previous, IReadOnlyList<MeetingMessage> transcript) =>
        previous with { Round = previous.Round + 1, Transcript = [.. transcript] };

    /// <summary>What a packet adds to the one before it, which the person already sent: the messages it holds that one did not.</summary>
    public static IReadOnlyList<MeetingMessage> Added(MeetingPacket previous, MeetingPacket packet) =>
        [.. packet.Transcript.Where(message => !previous.Transcript.Contains(message))];

    /// <summary>The packet's file: its schema version, its digest, then its content, as the digest was taken of it.</summary>
    public static byte[] Bytes(MeetingPacket packet) => Write(packet, withDigest: true);

    /// <summary><c>sha256:</c> and the hex of the content's bytes, everything the file holds but its version and its digest.</summary>
    public static string DigestOf(MeetingPacket packet) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Write(packet, withDigest: false)));

    /// <summary>A packet read back from its file; null where it is not a meeting's, or its digest is not its content's.</summary>
    public static MeetingPacket? Read(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("meeting", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != Version
                || !root.TryGetProperty("round", out var round) || round.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var (sources, left) = BrainstormPackets.ReadSources(root);
            var participants = BrainstormPackets.Items(root, "participants")
                .Select(each => new MeetingParticipant(Json.Text(each, "id") ?? "", Json.Text(each, "maker") ?? "", Json.Text(each, "adapter") ?? ""))
                .ToList();
            var transcript = BrainstormPackets.Items(root, "transcript").Select(Message).ToList();
            var packet = new MeetingPacket(
                Json.Text(root, "question") ?? "", Json.Text(root, "repository"), Json.Text(root, "tier"), sources, left, participants,
                round.GetInt32(), transcript);
            return Json.Text(root, "digest") == packet.Digest ? packet : null;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException
                                          or FormatException)
        {
            return null;
        }
    }

    /// <summary>One message read back, as the packet and the meeting's transcript each hold it.</summary>
    internal static MeetingMessage Message(JsonElement each) =>
        new(each.GetProperty("round").GetInt32(), Json.Text(each, "participant") ?? "", Json.Text(each, "maker") ?? "",
            Json.Text(each, "adapter") ?? "", Json.Text(each, "text") ?? "", Json.Text(each, "packet") ?? "");

    /// <summary>One message written, as the packet and the meeting's transcript each hold it.</summary>
    internal static void Write(Utf8JsonWriter writer, MeetingMessage message)
    {
        writer.WriteStartObject();
        writer.WriteNumber("round", message.Round);
        writer.WriteString("participant", message.Participant);
        writer.WriteString("maker", message.Maker);
        writer.WriteString("adapter", message.Adapter);
        writer.WriteString("text", message.Text);
        writer.WriteString("packet", message.Packet);
        writer.WriteEndObject();
    }

    private static byte[] Write(MeetingPacket packet, bool withDigest)
    {
        using var stream = new MemoryStream();
        using (var writer = BrainstormPackets.Writer(stream))
        {
            writer.WriteStartObject();
            if (withDigest)
            {
                writer.WriteNumber("meeting", Version);
                writer.WriteString("digest", packet.Digest);
            }

            writer.WriteString("question", packet.Question);
            BrainstormPackets.Nullable(writer, "repository", packet.Repository);
            BrainstormPackets.Nullable(writer, "tier", packet.Tier);
            writer.WriteNumber("round", packet.Round);
            writer.WriteStartArray("participants");
            foreach (var participant in packet.Participants)
            {
                writer.WriteStartObject();
                writer.WriteString("id", participant.Id);
                writer.WriteString("maker", participant.Maker);
                writer.WriteString("adapter", participant.Adapter);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            BrainstormPackets.WriteSources(writer, packet.Sources, packet.LeftOut);
            writer.WriteStartArray("transcript");
            foreach (var message in packet.Transcript) Write(writer, message);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return BrainstormPackets.Lf(stream);
    }
}

/// <summary>A message read from a turn's answer: its text where it holds one, else the codes that refused it.</summary>
public sealed record MeetingChecked(string? Text, IReadOnlyList<string> Codes);

/// <summary>
/// A turn's answer read as a message (AGENTROOM1a, D177 point 1): <c>{ meeting: 1, text }</c>, by its shape alone and with no model.
/// Nothing else in it is read: who said it is Daoris's binding of the turn, so a field or a sentence naming another participant
/// is the sender's words, kept as theirs.
/// </summary>
public static class MeetingMessages
{
    public static MeetingChecked Check(string? answer)
    {
        if (answer is null) return new(null, [BrainstormAnswers.Missing]);
        if (answer.Length > BrainstormAnswers.AnswerChars) return new(null, [BrainstormAnswers.TooLong]);
        try
        {
            using var document = JsonDocument.Parse(answer);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("meeting", out var schema)
                || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != MeetingPackets.Version
                || !root.TryGetProperty("text", out var said) || said.ValueKind != JsonValueKind.String)
            {
                return new(null, [BrainstormAnswers.Shape]);
            }

            var text = said.GetString()!.Trim();
            return text.Length == 0 ? new(null, [BrainstormAnswers.Empty])
                : text.Length > MeetingPackets.MessageChars ? new(null, [BrainstormAnswers.TooLong])
                : new(text, []);
        }
        catch (JsonException)
        {
            return new(null, [BrainstormAnswers.Shape]);
        }
    }
}

/// <summary>
/// A meeting turn's fixed instruction (AGENTROOM1a, D177 points 1 and 2): the packet named first by its digest, so the turn reads
/// what the person sent; then who it is in Daoris's records, its one job and its one result tool. Never another participant's
/// process, the person's conversation, or anything outside its room.
/// </summary>
public static class MeetingInstruction
{
    public static string Say(string digest, MeetingParticipant you, int round) =>
        $"Daoris holds a meeting, and its context packet `{BrainstormInstruction.PacketFile}` in this folder is packet `{digest}`, for "
        + $"round {round}. Read the packet first: its question, its sources, its participants in their order, and its transcript, each "
        + "message with who said it as Daoris relayed it.\n\n"
        + $"You are participant `{you.Id}`. Your job: say your one message of this round to the person and the other participants, "
        + "about the packet's question. Daoris relays it with your name from its own records: nothing you write names you, and a "
        + "message quoting another participant is your claim.\n\n"
        + $"Answer once, through your tool `meeting_say`, with `{{ meeting: 1, text }}`, the text at most {MeetingPackets.MessageChars} "
        + "characters.\n\n"
        + "You change nothing: your message is a claim the person and the others read, and nothing outside this folder is yours to read.\n";
}
