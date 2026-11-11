using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each reader of a record AtomicFile moves into place reads it while the move still holds it (<see cref="MoveHold"/>).
/// </summary>
public sealed class ReadSharePlatformTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private const string Address = "git@ssh.dev.azure.com:v3/org/project/engine";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-readshare-platform-" + Guid.NewGuid().ToString("N")[..8]);

    public ReadSharePlatformTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A file's place made on the way, and the path back.</summary>
    private static string Folded(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private static void Refused(string path) => Assert.Throws<IOException>(() => File.ReadAllText(path));

    /// <summary>
    /// A writer that reads <paramref name="path"/> again before writing over it, run while the move still holds it: its read is made under
    /// the hold, and the hold is let go once its write beside appears, since Windows refuses the rename over a held file for as long as it
    /// is held (the move's own hold lasts a moment, which <c>AtomicFile</c>'s rename waits out).
    /// </summary>
    private static async Task<T> WrittenOverHold<T>(string path, Func<T> write)
    {
        var hold = MoveHold.Take(path);
        try
        {
            Refused(path);
            var writing = Task.Run(write);
            var folder = Path.GetDirectoryName(path)!;
            var beside = Path.GetFileName(path) + ".*.writing";
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!writing.IsCompleted && DateTime.UtcNow < until && !Directory.EnumerateFiles(folder, beside).Any()) await Task.Delay(5);
            hold.Dispose();
            return await writing;
        }
        finally
        {
            hold.Dispose();
        }
    }

    /// <summary><c>RecordList.Read</c>, reached through <see cref="AbandonRecord"/>, reads a record its move still holds, and an edit keeps it.</summary>
    [Fact]
    public async Task A_record_list_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var record = new AbandonRecord(_home);
        record.Write(new AbandonEntry("quest", "q1", Now, "screen", "not needed"));

        using (MoveHold.Take(record.FilePath))
        {
            Refused(record.FilePath);
            Assert.Equal("not needed", Assert.Single(record.Entries()).Reason);
        }

        // An edit's read under the hold keeps what is there: a read refused would be no entries, and the edit would drop q1.
        await WrittenOverHold(record.FilePath, () =>
        {
            record.Write(new AbandonEntry("quest", "q2", Now, "screen", "kept"));
            return true;
        });
        Assert.Equal(["q1", "q2"], record.Entries().Select(entry => entry.Id));
    }

    /// <summary><c>JsonKeeping.SaveHolding</c>'s re-read, reached through <see cref="JsonKeeping.Save"/>, saves over a file its move still holds.</summary>
    [Fact]
    public async Task A_kept_edit_is_saved_while_the_move_into_place_still_holds_its_file()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Path.Combine(_home, "settings.json");
        AtomicFile.WriteText(path, "{\n  \"a\": 1,\n  \"b\": 2\n}\n");

        await WrittenOverHold(path, () =>
        {
            JsonKeeping.Save(path, found: true, "{\"a\":1}", "{\"a\":3}");
            return true;
        });

        var saved = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal((3, 2), (saved["a"]!.GetValue<int>(), saved["b"]!.GetValue<int>()));
    }

    /// <summary><see cref="KeepingRun.Holder"/> reads the holder's file its move still holds.</summary>
    [Fact]
    public void A_keeping_run_s_holder_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var run = KeepingRun.TryTake(_home, KeepingRun.Press);
        Assert.NotNull(run);
        var path = KeepingRun.HolderPathOf(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            var holder = KeepingRun.Holder(_home, _ => true);
            Assert.Equal((Environment.ProcessId, KeepingRun.Press, false), (holder?.Pid, holder?.Door, holder?.Gone));
        }
    }

    /// <summary><see cref="KeepingRun.Dispose"/> reads the holder's file its move still holds, and removes it as its own.</summary>
    [Fact]
    public void A_keeping_run_removes_its_holder_while_the_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var run = KeepingRun.TryTake(_home, KeepingRun.Press);
        Assert.NotNull(run);
        var path = KeepingRun.HolderPathOf(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            run.Dispose();
        }

        Assert.False(File.Exists(path));
    }

    /// <summary><see cref="KeepingRecord.Read"/> reads <c>keeping.json</c> while its move still holds it.</summary>
    [Fact]
    public void The_keeping_record_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Null(KeepingRecord.WriteLast(_home, KeepingRecord.Read(_home), "trees", new KeepingLast(Now, "press", "run-1", 2, 1024, new Dictionary<string, int>())));
        var path = KeepingRecord.PathOf(_home);

        KeepingFile read;
        using (MoveHold.Take(path))
        {
            Refused(path);
            read = KeepingRecord.Read(_home);
        }

        Assert.Null(read.Problem);
        Assert.Equal(("run-1", 2, 1024L), (read.Last["trees"].Run, read.Last["trees"].Removed, read.Last["trees"].Bytes));
    }

    /// <summary><see cref="Notices.Read"/> reads <c>notices.json</c> while its move still holds it.</summary>
    [Fact]
    public void The_notices_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Null(Notices.Add(_home, new Notice("ended-s1", Notices.Ended, Now.AddMinutes(-5), Session: "s1", State: "failed"), Now));
        var path = Notices.PathOf(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.Equal("ended-s1", Assert.Single(Notices.Read(_home, Now)!).Id);
        }
    }

    /// <summary><c>Notices.Edit</c>'s read, reached through <see cref="Notices.Add"/>, keeps what <c>notices.json</c> holds while its move still holds it.</summary>
    [Fact]
    public async Task A_notice_is_added_while_the_move_into_place_still_holds_the_record()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.Null(Notices.Add(_home, new Notice("ended-s1", Notices.Ended, Now.AddMinutes(-5), Session: "s1", State: "failed"), Now));
        var path = Notices.PathOf(_home);

        Assert.Null(await WrittenOverHold(path, () =>
            Notices.Add(_home, new Notice("ended-s2", Notices.Ended, Now.AddMinutes(-1), Session: "s2", State: "done"), Now)));

        Assert.Equal(["ended-s2", "ended-s1"], Notices.Read(_home, Now)!.Select(notice => notice.Id));
    }

    /// <summary><see cref="RunningAddress.Read"/> reads the address file its move still holds.</summary>
    [Fact]
    public void The_running_address_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var written = RunningAddress.Write(_home, "http://localhost:5177/");
        Assert.NotNull(written);
        var path = RunningAddress.PathIn(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            var read = RunningAddress.Read(_home);
            Assert.Equal(("http://localhost:5177", Environment.ProcessId), (read?.Url, read?.Pid));
        }
    }

    /// <summary><see cref="RunningAddress.Dispose"/> reads the address file its move still holds, and removes it as its own.</summary>
    [Fact]
    public void The_running_address_is_removed_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var written = RunningAddress.Write(_home, "http://localhost:5177/");
        Assert.NotNull(written);
        var path = RunningAddress.PathIn(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            written.Dispose();
        }

        Assert.False(File.Exists(path));
    }

    /// <summary><see cref="SentPresses.Find"/> reads a kept press its move still holds.</summary>
    [Fact]
    public void A_sent_press_is_found_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString();
        var presses = new SentPresses(_home);
        presses.Keep(new SentPress(id, "key-1", "SESSION_INPUT", now));
        var path = Path.Combine(DaorisHome.Entry(_home, HomeEntry.PipeSent), id + ".json");

        using (MoveHold.Take(path))
        {
            Refused(path);
            var found = presses.Find(id, now);
            Assert.Equal(("key-1", "SESSION_INPUT"), (found?.Key, found?.Route));
        }
    }

    /// <summary><see cref="DriverConfig.Load"/> reads <c>driver.json</c> while its move still holds it.</summary>
    [Fact]
    public void The_driver_config_is_loaded_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Folded(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        AtomicFile.WriteText(path, "{\n  \"pollSeconds\": 45\n}\n");

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.Equal(45, DriverConfig.Load(path).PollSeconds);
        }
    }

    /// <summary><c>GitHolds.Load</c>, reached through <see cref="GitHolds.Read"/>, reads <c>held.json</c> while its move still holds it.</summary>
    [Fact]
    public void The_git_holds_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(_home, "engine");
        Assert.NotNull(GitHolds.Hold(_home, root, "engine", "work", "origin", Address, "git-a", GitFailureClass.Refused,
            "git@ssh.dev.azure.com: Permission denied (password,publickey).", Now));
        var path = DaorisHome.Entry(_home, HomeEntry.GitHeld);

        using (MoveHold.Take(path))
        {
            Refused(path);
            var hold = Assert.Single(GitHolds.Read(_home));
            Assert.Equal(("origin", "all"), (hold.Remote, hold.Holds));
        }
    }

    /// <summary>
    /// <c>DaorisHome.Holds</c>, reached through <see cref="DaorisHome.Entry"/>, opens a new place its move still holds, so a
    /// torn old place never shadows it.
    /// </summary>
    [Fact]
    public void A_new_place_holds_its_entry_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var places = DaorisHome.Places(_home, HomeEntry.DriverConfig);
        File.WriteAllText(places[1], "torn");
        var next = Folded(places[0]);
        AtomicFile.WriteText(next, "{\n  \"pollSeconds\": 45\n}\n");

        using (MoveHold.Take(next))
        {
            Refused(next);
            Assert.Equal(next, DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        }
    }

    private WiringWorld World() => new()
    {
        Home = _home,
        BaseDirectory = Path.Combine(_home, "app"),
        Process = _ => null,
        Account = _ => null,
        Machine = _ => null,
        Lives = (_, _) => false,
        AskStatus = _ => null,
        Sizes = new WiringSizes(),
    };

    /// <summary><c>SystemWiring.LoadRemotes</c>, reached through <see cref="SystemWiring.Read"/>, reads <c>remotes.json</c> while its move still holds it.</summary>
    [Fact]
    public void The_remotes_are_read_while_their_move_into_place_still_holds_them()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Folded(DaorisHome.Entry(_home, HomeEntry.Remotes));
        AtomicFile.WriteText(path, """{ "team": { "url": "https://team.example", "key": "not-a-minted-key" } }""");

        JsonObject wiring;
        using (MoveHold.Take(path))
        {
            Refused(path);
            wiring = SystemWiring.Read(World());
        }

        var row = Assert.Single(wiring["remotes"]!.AsArray())!;
        Assert.Equal(("team", "file", true), ((string?)row["workspace"], (string?)row["from"], (bool)row["keySet"]!));
        Assert.Null(wiring["remotesWhy"]);
    }

    /// <summary><c>SystemWiring.Parses</c>, reached through <see cref="SystemWiring.Read"/>, opens a settings file its move still holds.</summary>
    [Fact]
    public void A_settings_file_reads_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = Folded(DaorisHome.Entry(_home, HomeEntry.Tools));
        AtomicFile.WriteText(path, "{}\n");

        JsonObject wiring;
        using (MoveHold.Take(path))
        {
            Refused(path);
            wiring = SystemWiring.Read(World());
        }

        var row = wiring["files"]!.AsArray().Select(each => each!.AsObject()).Single(each => (string?)each["id"] == "tools");
        Assert.Equal((true, null), ((bool?)row["reads"], (string?)row["readsWhy"]));
    }

    /// <summary><c>HomeLease.Read</c>, reached through <see cref="HomeLease.HeldBy"/>, reads the lease its move still holds.</summary>
    [Fact]
    public void The_home_lease_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        Assert.NotNull(lease);
        var path = HomeLease.PathIn(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            var holder = HomeLease.HeldBy(_home);
            Assert.Equal((Environment.ProcessId, LeaseKind.Headless), (holder?.Pid, holder?.Kind));
        }
    }

    /// <summary><see cref="HomeIdentity.Read(string, out string?)"/> reads the home's id its move still holds.</summary>
    [Fact]
    public void The_home_s_id_is_read_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var id = HomeIdentity.Ensure(_home);
        Assert.True(HomeIdentity.IsId(id), id);
        var path = HomeIdentity.PathIn(_home);

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.Equal(id, HomeIdentity.Read(_home, out var unread));
            Assert.Null(unread);
        }
    }

    /// <summary><see cref="GitGrants.Take"/> reads a grant its move still holds, and takes it once.</summary>
    [Fact]
    public void A_git_grant_is_taken_while_its_move_into_place_still_holds_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string Remote = "git@github.com:org/engine.git";
        var id = GitGrants.Write(_home, Remote, [new GitGrantRef("refs/heads/main", new string('a', 40))], Now);
        Assert.NotNull(id);
        var path = Path.Combine(DaorisHome.Entry(_home, HomeEntry.GitGrants), id + ".json");

        using (MoveHold.Take(path))
        {
            Refused(path);
            Assert.True(GitGrants.Take(_home, id, GitAddressKeys.Of(Remote), Now));
        }

        Assert.False(GitGrants.Take(_home, id, GitAddressKeys.Of(Remote), Now));
    }
}
