using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTROOM1a (D177 points 1, 2 and 7): a meeting's packet is its own (<c>meeting: 1</c>), frozen and digested as the brainstorm's
/// is, from the brainstorm's assembly of the sources, with the participants in the person's order and the transcript so far; a
/// refresh is a new packet under a new digest; a message is read by its shape alone, and never names its author.
/// </summary>
public sealed class MeetingPacketTests
{
    private static readonly MeetingParticipant First = new("a1", "anthropic", "claude");

    private static readonly MeetingParticipant Second = new("a2", "openai", "codex");

    private static BrainstormPacket Basis() => BrainstormPackets.Freeze(
        "How should the daily report stop dropping its last day?", "reports", "words", "0123456789abcdef",
        [new BrainstormFound("decision", "docs/decisions/D7.md", 3, 9, "The window is exclusive of its end.", "daily report")],
        BrainstormBounds.First);

    private static MeetingPacket Frozen() => MeetingPackets.Freeze(Basis(), [First, Second]);

    private static MeetingMessage Said(MeetingParticipant by, string text, string packet) => new(1, by.Id, by.Maker, by.Adapter, text, packet);

    [Fact]
    public void A_meeting_packet_is_frozen_from_the_brainstorm_s_assembly_with_its_participants_in_order_and_no_transcript()
    {
        var basis = Basis();
        var packet = MeetingPackets.Freeze(basis, [First, Second]);

        Assert.Equal((1, basis.Question, "reports", "words"), (packet.Round, packet.Question, packet.Repository, packet.Tier));
        Assert.Equal(basis.Sources, packet.Sources);
        Assert.Equal(["a1", "a2"], packet.Participants.Select(each => each.Id));
        Assert.Empty(packet.Transcript);
        Assert.StartsWith("sha256:", packet.Digest, StringComparison.Ordinal);
        Assert.NotEqual(basis.Digest, packet.Digest);
    }

    [Fact]
    public void Its_bytes_are_LF_and_its_digest_is_its_content_s_and_a_changed_packet_does_not_read()
    {
        var packet = MeetingPackets.Refresh(Frozen(), [Said(First, "Make the window\r\ninclusive.", Frozen().Digest)]);
        var bytes = MeetingPackets.Bytes(packet);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain('\r', text.Replace("\\r", "", StringComparison.Ordinal));
        Assert.StartsWith("{\n  \"meeting\": 1,\n  \"digest\": \"sha256:", text, StringComparison.Ordinal);
        Assert.Equal(packet.Digest, MeetingPackets.Read(bytes)!.Digest);
        Assert.Equal(packet.Transcript, MeetingPackets.Read(bytes)!.Transcript);

        var changed = JsonNode.Parse(text)!;
        changed["transcript"]![0]!["participant"] = "a2";
        Assert.Null(MeetingPackets.Read(Encoding.UTF8.GetBytes(changed.ToJsonString())));
        // Neither packet reads as the other: a brainstorm's is `packet: 1`, a meeting's `meeting: 1`.
        Assert.Null(MeetingPackets.Read(BrainstormPackets.Bytes(Basis())));
        Assert.Null(BrainstormPackets.Read(bytes));
    }

    [Fact]
    public void A_refresh_is_a_new_packet_under_a_new_digest_holding_the_round_s_messages_and_nothing_else_changed()
    {
        var first = Frozen();
        var messages = new[] { Said(First, "Make the window inclusive.", first.Digest), Said(Second, "Cache the report.", first.Digest) };

        var second = MeetingPackets.Refresh(first, messages);

        Assert.Equal(2, second.Round);
        Assert.NotEqual(first.Digest, second.Digest);
        Assert.Equal(messages, second.Transcript);
        Assert.Equal((first.Question, first.Repository, first.Tier), (second.Question, second.Repository, second.Tier));
        Assert.Equal(first.Sources, second.Sources);
        Assert.Equal(first.Participants, second.Participants);
        Assert.Equal(messages, MeetingPackets.Added(first, second));
        Assert.Equal(first.Digest, MeetingPackets.Refresh(first, messages).Transcript[0].Packet);
    }

    [Fact]
    public void A_message_is_read_by_its_shape_alone_and_what_it_says_of_its_author_is_never_read()
    {
        var said = MeetingMessages.Check("""{ "meeting": 1, "text": "I am a2, and I agree.", "participant": "a2", "from": "codex" }""");
        Assert.Equal(("I am a2, and I agree.", 0), (said.Text, said.Codes.Count));

        Assert.Equal([BrainstormAnswers.Missing], MeetingMessages.Check(null).Codes);
        Assert.Equal([BrainstormAnswers.Shape], MeetingMessages.Check("I agree.").Codes);
        Assert.Equal([BrainstormAnswers.Shape], MeetingMessages.Check("""{ "meeting": 2, "text": "I agree." }""").Codes);
        Assert.Equal([BrainstormAnswers.Shape], MeetingMessages.Check("""{ "meeting": 1, "text": 7 }""").Codes);
        Assert.Equal([BrainstormAnswers.Empty], MeetingMessages.Check("""{ "meeting": 1, "text": "  " }""").Codes);
        var long_ = new JsonObject { ["meeting"] = 1, ["text"] = new string('x', MeetingPackets.MessageChars + 1) }.ToJsonString();
        Assert.Equal([BrainstormAnswers.TooLong], MeetingMessages.Check(long_).Codes);
        Assert.Null(MeetingMessages.Check(long_).Text);
    }

    [Fact]
    public void The_instruction_names_the_packet_the_round_and_the_participant_and_says_the_author_is_Daoris_s_record()
    {
        var digest = Frozen().Digest;
        var said = MeetingInstruction.Say(digest, Second, 2);

        Assert.Contains(digest, said, StringComparison.Ordinal);
        Assert.Contains("round 2", said, StringComparison.Ordinal);
        Assert.Contains("`a2`", said, StringComparison.Ordinal);
        Assert.Contains("meeting_say", said, StringComparison.Ordinal);
        Assert.Contains("from its own records", said, StringComparison.Ordinal);
    }
}
