using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The archive marks (SESSUX1a, D126 §5.2): this machine's, never the record's, at <c>&lt;home&gt;/sessions/archived.json</c>,
/// written whole; a lost mark costs a row back in the list, never a row gone; and archive never hides what needs the
/// person. Files only, so this is the suite's fast half.
/// </summary>
public sealed class SessionArchiveTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-session-archive-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionArchiveTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private static SessionGrouping In(string session, string group, string shown = "completed") => new(session, group, shown);

    /// <summary>Every session the reader placed, as the routes hand them over: one in each group.</summary>
    private static readonly IReadOnlyList<SessionGrouping> Groups =
    [
        In("waiting", SessionGroup.You, "awaiting-person"),
        In("parked", SessionGroup.You, ShownState.Parked),
        In("review", SessionGroup.Review),
        In("running", SessionGroup.Working, "working"),
        In("asked", SessionGroup.Later, ShownState.AwaitingReply),
        In("done", SessionGroup.Ended),
        In("old", SessionGroup.Ended),
    ];

    private static readonly string[] Known = [.. Groups.Select(group => group.Session)];

    [Fact]
    public void The_marks_are_the_homes_own_file_beside_the_sessions()
    {
        Assert.Equal(Path.Combine(_home, "sessions", "archived.json"), new SessionArchive(_home).FilePath);
    }

    [Fact]
    public void A_missing_file_is_nothing_archived()
    {
        Assert.Empty(new SessionArchive(_home).Marks());
    }

    /// <summary>A mark lost costs a row back in the list, never a row gone: a torn file is nothing archived, and the next write starts it again.</summary>
    [Fact]
    public void A_torn_file_is_nothing_archived_and_the_next_write_starts_it_again()
    {
        var archive = new SessionArchive(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(archive.FilePath)!);
        File.WriteAllText(archive.FilePath, """{ "archived": [ { "session": "done", "at": """);

        Assert.Empty(archive.Marks());

        archive.Archive(["done"], Groups, Known, At);
        Assert.Equal(["done"], archive.Marks().Keys);
    }

    [Fact]
    public void An_ended_session_is_archived_written_whole()
    {
        var archive = new SessionArchive(_home);

        var answer = archive.Archive(["done", "asked"], Groups, Known, At);

        Assert.All(answer.Outcomes, outcome => Assert.Equal(ArchiveVerdict.Archived, outcome.Verdict));
        Assert.Equal(["asked", "done"], answer.Marks.Select(mark => mark.Session).Order(StringComparer.Ordinal));
        Assert.Equal(At, archive.Marks()["done"]);
        // Atomic, BOM-less UTF-8, LF, a final newline, and nothing left beside it.
        var bytes = File.ReadAllBytes(archive.FilePath);
        Assert.NotEqual(0xEF, bytes[0]);
        var text = File.ReadAllText(archive.FilePath);
        Assert.DoesNotContain("\r", text);
        Assert.EndsWith("}\n", text);
        Assert.Equal([archive.FilePath], Directory.GetFiles(Path.GetDirectoryName(archive.FilePath)!));
        using var document = JsonDocument.Parse(text);
        Assert.Equal(2, document.RootElement.GetProperty("archived").GetArrayLength());
    }

    /// <summary>
    /// Archive never hides what needs the person, nor anything still running, nor what no record names; and a refused
    /// session writes nothing.
    /// </summary>
    [Fact]
    public void A_live_session_what_needs_you_and_an_unknown_one_are_refused_and_nothing_is_written()
    {
        var archive = new SessionArchive(_home);

        var answer = archive.Archive(["running", "waiting", "parked", "review", "nobody"], Groups, Known, At);

        var outcomes = answer.Outcomes.ToDictionary(outcome => outcome.Session);
        Assert.Equal(ArchiveVerdict.Live, outcomes["running"].Verdict);
        Assert.Equal(ArchiveVerdict.NeedsYou, outcomes["waiting"].Verdict);
        Assert.Equal(SessionGroup.You, outcomes["waiting"].Group);
        Assert.Equal(SessionGroup.You, outcomes["parked"].Group);
        Assert.Equal(ArchiveVerdict.NeedsYou, outcomes["review"].Verdict);
        Assert.Equal(SessionGroup.Review, outcomes["review"].Group);
        Assert.Equal(ArchiveVerdict.Unknown, outcomes["nobody"].Verdict);
        Assert.Empty(answer.Marks);
        Assert.False(File.Exists(archive.FilePath));
    }

    /// <summary>The second press of *Archive what ended* judges each again: what still may goes, and what changed is kept.</summary>
    [Fact]
    public void Several_at_once_archive_what_may_go_and_keep_what_may_not()
    {
        var archive = new SessionArchive(_home);

        var answer = archive.Archive(["done", "review", "old"], Groups, Known, At);

        Assert.Equal(["done", "old"], answer.Marks.Select(mark => mark.Session).Order(StringComparer.Ordinal));
        Assert.Equal(ArchiveVerdict.NeedsYou, answer.Outcomes.Single(outcome => outcome.Session == "review").Verdict);
    }

    [Fact]
    public void A_session_archived_again_keeps_when_it_was_first_archived()
    {
        var archive = new SessionArchive(_home);
        archive.Archive(["done"], Groups, Known, At);

        var again = archive.Archive(["done"], [In("done", SessionGroup.Archived)], Known, At.AddHours(3));

        Assert.Equal(ArchiveVerdict.Archived, Assert.Single(again.Outcomes).Verdict);
        Assert.Equal(At, archive.Marks()["done"]);
    }

    [Fact]
    public void A_mark_for_a_record_that_is_gone_is_dropped_on_the_next_write()
    {
        var archive = new SessionArchive(_home);
        archive.Archive(["done", "old"], Groups, Known, At);

        archive.Archive(["asked"], Groups, ["done", "asked"], At.AddMinutes(5));

        Assert.Equal(["asked", "done"], archive.Marks().Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>Unarchive brings a session back to its group; one that was not archived is said, as information, never refused (D48 §6).</summary>
    [Fact]
    public void Unarchive_takes_the_mark_away_and_says_which_were_not_archived()
    {
        var archive = new SessionArchive(_home);
        archive.Archive(["done", "old"], Groups, Known, At);

        var answer = archive.Unarchive(["done", "asked"]);

        Assert.Equal(["asked"], answer.NotArchived);
        Assert.Equal(["old"], answer.Marks.Select(mark => mark.Session));
        Assert.Equal(["old"], archive.Marks().Keys);
    }

    [Fact]
    public void Unarchiving_nothing_archived_writes_nothing()
    {
        var archive = new SessionArchive(_home);

        var answer = archive.Unarchive(["done"]);

        Assert.Equal(["done"], answer.NotArchived);
        Assert.False(File.Exists(archive.FilePath));
    }
}
