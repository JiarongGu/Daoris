using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation another Daoris process runs tells its turns in the request folder (CLITURN1): a terminal's chat owns its runner,
/// so the window's reader needs the owner's word, and a mark is believed only while its owner lives.
/// </summary>
public sealed class TurnMarksTests : IDisposable
{
    private const string Session = "c1a2b3d4";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-turn-" + Guid.NewGuid().ToString("N")[..8]);

    public TurnMarksTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string MarkPath => Path.Combine(DaorisHome.Entry(_home, HomeEntry.Requests), Session + TurnMarks.Suffix);

    private static ChatQueue Running(int waiting = 0) =>
        new(Taking: true, [.. Enumerable.Range(0, waiting).Select(each => new ChatMessage($"word {each}", []))]);

    [Fact]
    public void What_the_owner_told_is_read_back_whole()
    {
        var ended = new DateTimeOffset(2026, 10, 30, 23, 24, 2, TimeSpan.Zero);
        var marks = new TurnMarks(_home);

        Assert.True(marks.Write(Session, Running(waiting: 2) with { LastTurnEnded = ended, Opening = true }));

        Assert.Equal(new TurnMark(Taking: true, Opening: true, Queued: 2, ended), marks.Read(Session));
    }

    [Fact]
    public void A_conversation_that_told_nothing_reads_as_none_not_as_idle()
    {
        Assert.Null(new TurnMarks(_home).Read(Session));
    }

    [Fact]
    public void A_mark_whose_owner_is_gone_reads_as_none_and_is_removed_by_the_next_look()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkPath)!);
        // A process id no machine hands out, and a start time none could have.
        File.WriteAllText(MarkPath, """{"pid":2147483000,"started":1,"taking":true}""");
        var marks = new TurnMarks(_home);

        Assert.Null(marks.Read(Session));
        Assert.Empty(marks.Changed(new Dictionary<string, long>()));
        Assert.False(File.Exists(MarkPath));
    }

    [Fact]
    public void A_mark_names_its_owner_by_the_process_that_wrote_it()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkPath)!);
        using var self = Process.GetCurrentProcess();
        // The right process id with another start time is a recycled id, not the owner.
        File.WriteAllText(MarkPath, $$"""{"pid":{{self.Id}},"started":{{self.StartTime.ToUniversalTime().Ticks + TimeSpan.TicksPerHour}},"taking":true}""");

        Assert.Null(new TurnMarks(_home).Read(Session));
    }

    [Fact]
    public void A_look_names_each_conversation_whose_mark_arrived_changed_or_went()
    {
        var marks = new TurnMarks(_home);
        var seen = new Dictionary<string, long>();

        marks.Write(Session, Running());
        Assert.Equal([Session], marks.Changed(seen));
        Assert.Empty(marks.Changed(seen));

        // Rewritten with a later stamp, as a turn's end rewrites it.
        File.SetLastWriteTimeUtc(MarkPath, File.GetLastWriteTimeUtc(MarkPath).AddSeconds(5));
        Assert.Equal([Session], marks.Changed(seen));

        marks.Remove(Session);
        Assert.Equal([Session], marks.Changed(seen));
        Assert.Empty(marks.Changed(seen));
    }

    [Fact]
    public async Task The_loop_that_watches_the_folder_hears_a_mark_and_the_name_of_one_is_heard_by_the_watcher()
    {
        var heard = new List<string>();
        await using var watch = new SessionRequestWatch(_home, runsHere: _ => false, stop: _ => true, service: () => null) { Turned = heard.Add };
        new TurnMarks(_home).Write(Session, Running());

        Assert.Equal(1, watch.HearTurns());

        Assert.Equal([Session], heard);
        Assert.True(SessionRequestWatch.Hears(Session + TurnMarks.Suffix, ChangeKind.Changed));
        Assert.False(SessionRequestWatch.Hears(Session + TurnMarks.Suffix, ChangeKind.Deleted));
    }

    [Fact]
    public async Task A_stop_nothing_takes_is_withdrawn_so_the_person_is_told_it_did_not_stop_and_none_acts_on_it_later()
    {
        var stopped = await SessionMoves.StopThroughOwnerAsync(
            _home, Session, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(20), () => DateTimeOffset.UtcNow);

        Assert.False(stopped);
        Assert.False(new SessionRequests(_home).IsPending(Session));
    }

    [Fact]
    public void A_mark_never_names_a_path()
    {
        Assert.Throws<DriverException>(() => new TurnMarks(_home).Write(@"..\escape", Running()));
    }
}
