using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The archive is judged and kept per conversation: archiving a part archives its conversation, which is archived or not as a
/// whole, its mark one fact of the desktop store; and archive never hides what needs the person.
/// </summary>
public sealed class ConversationArchiveTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-conversation-archive-" + Guid.NewGuid().ToString("N")[..8]);

    private static readonly DateTimeOffset At = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    public ConversationArchiveTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static SessionRecord Part(string id, string? line = null, string state = "completed") => new(id, "daoris", state) { Line = line };

    private static SessionGrouping In(string session, string group) => new(session, group, "completed");

    [Fact]
    public void Archiving_one_part_archives_its_conversation_and_the_mark_is_the_conversations()
    {
        SessionRecord[] records = [Part("c1"), Part("c2", "c1"), Part("c3", "c1"), Part("other")];
        var archive = new ConversationArchive(_home);

        var answer = archive.Archive(["c2"], [In("c1", SessionGroup.Done), In("c2", SessionGroup.Done), In("c3", SessionGroup.Failed)], records, At);

        Assert.Equal(ArchiveVerdict.Archived, Assert.Single(answer.Outcomes).Verdict);
        Assert.Equal(["c1"], archive.Marks().Keys);
        Assert.Equal(At, archive.Marks()["c1"]);
    }

    [Fact]
    public void A_conversation_with_a_part_live_or_needing_you_is_kept_whole()
    {
        SessionRecord[] records = [Part("a1"), Part("a2", "a1"), Part("b1"), Part("b2", "b1")];
        IWorkplaceSessionRow[] rows =
        [
            In("a1", SessionGroup.Done), In("a2", SessionGroup.Working), In("b1", SessionGroup.Done), In("b2", SessionGroup.NeedsYou),
        ];

        var answer = new ConversationArchive(_home).Archive(["a1", "b1"], rows, records, At);

        Assert.Equal([ArchiveVerdict.Live, ArchiveVerdict.NeedsYou], answer.Outcomes.Select(outcome => outcome.Verdict));
        Assert.Empty(new ConversationArchive(_home).Marks());
    }

    [Fact]
    public void The_parts_to_read_are_every_part_held_of_each_conversation_asked()
    {
        SessionRecord[] records = [Part("c1"), Part("c2", "c1"), Part("other"), Part("laptop/t1")];

        Assert.Equal(["c2", "c1"], ConversationArchive.Parts(["c2"], records));
    }

    [Fact]
    public void Unarchiving_any_part_takes_the_conversations_mark_away_and_says_one_not_archived()
    {
        SessionRecord[] records = [Part("c1"), Part("c2", "c1"), Part("other")];
        var archive = new ConversationArchive(_home);
        archive.Archive(["c1"], [In("c1", SessionGroup.Done), In("c2", SessionGroup.Done)], records, At);

        var back = archive.Unarchive(["c2", "other"], records);

        Assert.Equal(["other"], back.NotArchived);
        Assert.Empty(archive.Marks());
    }

    [Fact]
    public void A_teammates_record_is_its_own_conversation_and_may_be_archived_here()
    {
        SessionRecord[] records = [Part("laptop/t1")];

        new ConversationArchive(_home).Archive(["laptop/t1"], [In("laptop/t1", SessionGroup.Done)], records, At);

        Assert.Equal(["laptop/t1"], new ConversationArchive(_home).Marks().Keys);
    }

    [Fact]
    public void A_part_going_on_shows_the_conversation_again()
    {
        SessionRecord[] records = [Part("c1"), Part("c2", "c1")];
        var archive = new ConversationArchive(_home);
        archive.Archive(["c2"], [In("c1", SessionGroup.Done), In("c2", SessionGroup.Done)], records, At);

        archive.Show(records[1]);

        Assert.Empty(archive.Marks());
    }
}
