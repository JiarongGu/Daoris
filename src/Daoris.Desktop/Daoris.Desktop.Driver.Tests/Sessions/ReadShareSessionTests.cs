using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>READSHARE1: each reader of a record AtomicFile moves into place reads it while the move still holds it (MoveHold).</summary>
public sealed class ReadShareSessionTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-readshare-sessions-" + Guid.NewGuid().ToString("N")[..8]);

    public ReadShareSessionTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Markers => DaorisHome.Entry(_home, HomeEntry.Sessions);

    /// <summary>A marker naming this test's own process with a start time an hour off: a process that is not the one it marked.</summary>
    private string MarkerOfAnother(string session, string? word = null)
    {
        using var self = Process.GetCurrentProcess();
        var started = self.StartTime.ToUniversalTime().Ticks - TimeSpan.TicksPerHour;
        var path = Path.Combine(Markers, session + ".pid");
        Directory.CreateDirectory(Markers);
        AtomicFile.WriteText(path, word is null ? $"{self.Id} {started}" : $"{self.Id} {started} {word}");
        return path;
    }

    /// <summary>READSHARE1: <see cref="ChildLineage.Install"/> keeps a held script that is as shipped, reading it beside its move.</summary>
    [Fact]
    public void A_held_lineage_script_as_shipped_is_kept_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = ChildLineage.Install(_home);
        var written = File.GetLastWriteTimeUtc(path);

        string again;
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            again = ChildLineage.Install(_home);
        }

        Assert.Equal(path, again);
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        Assert.Equal(ChildLineage.Source, File.ReadAllText(path));
    }

    /// <summary>READSHARE1: <see cref="SessionProcesses.AnyAlive"/> reads a held marker, so one naming a process gone is none alive.</summary>
    [Fact]
    public void A_marker_is_read_as_no_process_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = MarkerOfAnother("a1b2c3d4");

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            // A marker that could not be read counts as alive: only the one read says its process is gone.
            Assert.False(SessionProcesses.AnyAlive(Markers));
            Assert.False(new SessionProcesses(Markers).AliveOnThisMachine("a1b2c3d4"));
        }
    }

    /// <summary>READSHARE1: <see cref="SessionProcesses.OpenerWent"/> reads a held opener's mark, so an opener gone is told gone.</summary>
    [Fact]
    public void An_openers_mark_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = MarkerOfAnother("a1b2c3d4", "opening");

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            // A mark that could not be read is no opener gone: only the one read says it went.
            Assert.True(new SessionProcesses(Markers).OpenerWent("a1b2c3d4"));
        }
    }

    /// <summary>READSHARE1: <see cref="HeldWordsFile.Forget"/> reads the held words beside the move that put them in place.</summary>
    [Fact]
    public void Held_words_are_forgotten_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = HeldWordsFile.PathOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteText(path, """{"held":[{"session":"s1","text":"also the changelog; 中文 too."}]}""" + "\n");

        IReadOnlyCollection<string> dropped;
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            dropped = HeldWordsFile.Forget(_home, ["s1"]);
        }

        Assert.Equal(["s1"], dropped);
        Assert.False(File.Exists(path));
    }

    /// <summary>READSHARE1: <see cref="SessionArchive.Marks"/> reads the archive marks while their move into place still holds them.</summary>
    [Fact]
    public void Archive_marks_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var at = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        var archive = new SessionArchive(_home);
        archive.Archive(["done"], [new SessionGrouping("done", SessionGroup.Ended, "completed")], ["done"], at);

        IReadOnlyDictionary<string, DateTimeOffset> marks;
        using (MoveHold.Take(archive.FilePath))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(archive.FilePath));
            marks = archive.Marks();
        }

        Assert.Equal(at, Assert.Single(marks, mark => mark.Key == "done").Value);
    }

    /// <summary>READSHARE1: <see cref="SessionRequests.Pending"/> reads a request while the move that put it in place still holds it.</summary>
    [Fact]
    public void A_pending_request_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var requests = new SessionRequests(_home);
        var now = DateTimeOffset.UtcNow;
        requests.Write(new SessionRequest("s1", SessionMove.Decline, now) { Note = "Not this way; 中文 too." });
        var path = requests.PathOf("s1");

        IReadOnlyList<SessionRequest> pending;
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            pending = requests.Pending(now.AddSeconds(5));
        }

        var read = Assert.Single(pending);
        Assert.Equal(("s1", SessionMove.Decline, "Not this way; 中文 too."), (read.Session, read.Move, read.Note));
    }

    /// <summary>READSHARE1: <see cref="SessionRequests.Take(string)"/> reads what it moved aside while the move into place still holds it.</summary>
    [Fact]
    public void A_request_is_taken_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var requests = new SessionRequests(_home);
        var now = DateTimeOffset.UtcNow;
        requests.Write(new SessionRequest("s1", SessionMove.Finish, now) { Parked = true });
        var path = requests.PathOf("s1");

        SessionRequest? taken;
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            // The take's rename carries the hold with the file, so what it moved aside is still held as it reads it.
            taken = requests.Take("s1");
        }

        Assert.NotNull(taken);
        Assert.Equal(("s1", SessionMove.Finish, true), (taken.Session, taken.Move, taken.Parked));
        Assert.False(requests.IsPending("s1"));
    }

    /// <summary>READSHARE1: <see cref="SessionRequests.AnswerOf"/> reads a say's answer while the move that put it in place still holds it.</summary>
    [Fact]
    public void A_says_answer_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var requests = new SessionRequests(_home);
        var say = new SessionRequest("s1", SessionMove.Say, DateTimeOffset.UtcNow) { Text = "also the changelog", Key = SessionRequests.NewKey() };
        requests.Answer(say, new WordsHeld(true, "the session", null));
        var path = Path.Combine(requests.Folder, $"s1.{say.Key}{SessionRequests.Answered}");
        Assert.True(File.Exists(path));

        WordsHeld? answer;
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            answer = requests.AnswerOf(say);
        }

        Assert.NotNull(answer);
        Assert.Equal((true, "the session"), (answer.Sent, answer.Reaches));
    }

    /// <summary>
    /// READSHARE1: <see cref="PressesTold.Drop"/> reads a told press's moment while its move into place still holds it, so one nobody
    /// took in time is removed; unread, it would be judged by its file's own fresh time and kept.
    /// </summary>
    [Fact]
    public void A_told_press_nobody_took_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var presses = new PressesTold(_home);
        var now = DateTimeOffset.UtcNow;
        Assert.True(presses.Tell(new PressTold(PressesTold.Sweep, null, now - PressesTold.Lifetime - TimeSpan.FromMinutes(1))));
        var path = Assert.Single(Directory.EnumerateFiles(presses.Folder, "*" + PressesTold.Suffix));

        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            presses.Drop(now);
        }

        Assert.False(File.Exists(path));
    }

    /// <summary>READSHARE1: <see cref="SessionChecks.Held"/> and <see cref="SessionChecks.HeldMinutes"/> read the kept checks beside their move.</summary>
    [Fact]
    public void A_sessions_kept_checks_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = SessionChecks.Hold(_home, "s1", SessionChecks.HeavyAtEnd, callMinutes: 45);
        Assert.NotNull(path);

        string? held;
        int? minutes;
        using (MoveHold.Take(path))
        {
            Assert.Throws<IOException>(() => File.ReadAllText(path));
            held = SessionChecks.Held(_home, "s1");
            minutes = SessionChecks.HeldMinutes(_home, "s1");
        }

        Assert.Equal((SessionChecks.HeavyAtEnd, (int?)45), (held, minutes));
    }
}
