using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A brainstorm's packet as its pass reads it, laid out as files: an index naming each source's file with where it came from,
/// its size, its relevance and why it is in, and one Markdown file per source. A source the search matched by words alone that holds few of
/// the question's terms is listed apart; nothing is one large JSON.
/// </summary>
public sealed class BrainstormRoomPacketTests
{
    private const string Question = "How should the daily report stop dropping its last window day?";

    private static BrainstormPacket Packet() =>
        BrainstormPackets.Freeze(Question, "engine", BrainstormPackets.Meaning, "abc1234",
        [
            new BrainstormFound("decision", "docs/decisions/D12.md", 3, 40,
                "The daily report reads an exclusive window, so its last day is dropped.", "daily report window", BrainstormPackets.Meaning,
                "a decision that matched the question's terms"),
            new BrainstormFound("knowledge", "docs/areas/report.md", null, null,
                "The report's window and its last day: the daily run stops at midnight.", "report window", BrainstormPackets.Words),
            new BrainstormFound("fix", "docs/FIX-LOG.md", 100, 120,
                "The palette's contrast was computed twice for every theme.", "should", BrainstormPackets.Words),
            new BrainstormFound("knowledge", "C:/machine/path.md", null, null, "never in a packet", "report", BrainstormPackets.Words),
        ], BrainstormBounds.First);

    [Fact]
    public void A_packet_is_laid_out_as_an_index_and_one_markdown_file_per_source_and_no_packet_json()
    {
        var packet = Packet();

        var laid = BrainstormRoomPacket.Lay(packet);

        Assert.Equal(
            [BrainstormRoomPacket.IndexFile, "sources/S1.md", "sources/S2.md", "sources/S3.md"],
            laid.Select(file => file.Path));
        Assert.DoesNotContain(laid, file => file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        // Each source's file holds where it came from, why it is in, and its text as kept.
        var first = laid.Single(file => file.Path == "sources/S1.md").Text;
        Assert.Contains("docs/decisions/D12.md", first, StringComparison.Ordinal);
        Assert.Contains("lines 3–40", first, StringComparison.Ordinal);
        Assert.Contains("a decision that matched the question's terms", first, StringComparison.Ordinal);
        Assert.Contains("The daily report reads an exclusive window, so its last day is dropped.", first, StringComparison.Ordinal);
    }

    [Fact]
    public void The_index_names_each_source_with_its_file_size_relevance_and_why_and_lists_one_off_the_question_apart()
    {
        var packet = Packet();

        var index = BrainstormRoomPacket.Lay(packet).Single(file => file.Path == BrainstormRoomPacket.IndexFile).Text;

        Assert.Contains(packet.Digest, index, StringComparison.Ordinal);
        Assert.Contains(Question, index, StringComparison.Ordinal);
        // Each source by its file, its size in characters kept of what it had, and why it is in.
        Assert.Contains("`sources/S1.md`", index, StringComparison.Ordinal);
        Assert.Contains($"{packet.Sources[0].Kept:N0} of {packet.Sources[0].Of:N0} characters", index, StringComparison.Ordinal);
        Assert.Contains("a decision that matched the question's terms", index, StringComparison.Ordinal);
        // Its relevance said: how many of the question's terms its text holds.
        var terms = BrainstormWordings.Terms(Question);
        Assert.Contains($"{BrainstormRoomPacket.Matched(terms, packet.Sources[0])} of {terms.Count} terms", index, StringComparison.Ordinal);

        // The palette's fix, matched by a word alone and holding none of the question's other terms, is listed apart after the others.
        Assert.True(BrainstormRoomPacket.OffQuestion(terms, packet.Sources[2]));
        Assert.False(BrainstormRoomPacket.OffQuestion(terms, packet.Sources[1]));
        var apart = index.IndexOf(BrainstormRoomPacket.ApartHeading, StringComparison.Ordinal);
        Assert.True(apart > 0);
        Assert.True(index.IndexOf("`sources/S3.md`", StringComparison.Ordinal) > apart);
        Assert.True(index.IndexOf("`sources/S2.md`", StringComparison.Ordinal) < apart);

        // A source found and left out is said too, never its machine path.
        Assert.Contains(BrainstormPackets.LeftForMachinePath, index, StringComparison.Ordinal);
        Assert.DoesNotContain("C:/machine", index, StringComparison.Ordinal);
    }

    [Fact]
    public void A_question_alone_is_an_index_with_no_source()
    {
        var packet = BrainstormPackets.Freeze("What should the product be for?", null, null, null, [], BrainstormBounds.First);

        var laid = BrainstormRoomPacket.Lay(packet);

        var index = Assert.Single(laid);
        Assert.Equal(BrainstormRoomPacket.IndexFile, index.Path);
        Assert.Contains("What should the product be for?", index.Text, StringComparison.Ordinal);
    }
}
