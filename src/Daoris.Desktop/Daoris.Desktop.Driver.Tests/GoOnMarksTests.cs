using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The mark a record's words could not go on with (MSG1b, closing MSG1a's open point): <c>&lt;home&gt;/sessions/&lt;id&gt;.cannot.json</c>,
/// the words judged by their ids and why by a code, so a later look leaves them waiting rather than trying them again. A
/// file that does not read is no mark, which costs one more try and never the words.
/// </summary>
public sealed class GoOnMarksTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-goon-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-03T09:05:00Z");

    private static PriorSession Record(params string[] ids) =>
        new("s1", "D:/trees/s-1", "completed")
        {
            Said = [.. ids.Select(id => new SaidWordView(id, "words " + id, At, [], Reopens: true))],
        };

    [Fact]
    public void A_mark_is_kept_and_read_back_with_its_words_and_why()
    {
        var marks = new GoOnMarks(_home);

        marks.Mark("s1", ["w1", "w2"], ContinueWhy.Of(ContinueWhy.Tree), At);

        Assert.Equal(new GoOnMark(["w1", "w2"], ContinueWhy.Tree, At), marks.Read("s1"), new MarkComparer());
        Assert.True(File.Exists(Path.Combine(_home, "sessions", "s1.cannot.json")));
        Assert.DoesNotContain("\r\n", File.ReadAllText(Path.Combine(_home, "sessions", "s1.cannot.json")));
    }

    /// <summary>Judged: every word waiting is among those marked. A word said since is tried again; no words, nothing to try.</summary>
    [Fact]
    public void A_record_is_judged_only_while_every_word_waiting_was_marked()
    {
        var mark = new GoOnMark(["w1"], ContinueWhy.Tree, At);

        Assert.True(GoOnMarks.Judged(Record("w1"), mark));
        Assert.False(GoOnMarks.Judged(Record("w1", "w2"), mark));
        Assert.False(GoOnMarks.Judged(Record("w1"), mark: null));
        Assert.False(GoOnMarks.Judged(Record(), mark));
    }

    /// <summary>A mark that does not read, and an id that is not a session's, are no mark: never a throw, never a path.</summary>
    [Fact]
    public void A_mark_that_does_not_read_is_none()
    {
        Directory.CreateDirectory(Path.Combine(_home, "sessions"));
        File.WriteAllText(Path.Combine(_home, "sessions", "s1.cannot.json"), "{ not json");
        var marks = new GoOnMarks(_home);

        Assert.Null(marks.Read("s1"));
        Assert.Null(marks.Read("../s1"));
        marks.Mark("../s1", ["w1"], ContinueWhy.Of(ContinueWhy.Tree), At);
        Assert.False(File.Exists(Path.Combine(_home, "s1.cannot.json")));
    }

    /// <summary>Going on clears the mark: the words it judged are taken, and what the page says of it is past.</summary>
    [Fact]
    public void Clearing_a_mark_removes_it()
    {
        var marks = new GoOnMarks(_home);
        marks.Mark("s1", ["w1"], ContinueWhy.Of(ContinueWhy.Tree), At);

        marks.Clear("s1");
        marks.Clear("s2");

        Assert.Null(marks.Read("s1"));
    }

    /// <summary>The marks a look reads: one per record the person's words wait on, and nothing for a record nobody wrote to.</summary>
    [Fact]
    public void A_look_reads_the_marks_of_the_records_with_words_waiting()
    {
        var marks = new GoOnMarks(_home);
        marks.Mark("s1", ["w1"], ContinueWhy.Of(ContinueWhy.Tree), At);
        marks.Mark("s2", ["w9"], ContinueWhy.Of(ContinueWhy.Tree), At);

        var read = marks.For([Record("w1"), new PriorSession("s2", null, "completed") { Said = [] }]);

        Assert.Equal(["s1"], read.Keys);
    }

    private sealed class MarkComparer : IEqualityComparer<GoOnMark?>
    {
        public bool Equals(GoOnMark? x, GoOnMark? y) =>
            x is not null && y is not null && x.Said.SequenceEqual(y.Said) && x.Why == y.Why && x.At == y.At;

        public int GetHashCode(GoOnMark? mark) => 0;
    }
}
