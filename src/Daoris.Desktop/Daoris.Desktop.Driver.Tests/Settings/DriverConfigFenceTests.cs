using System.Globalization;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1w-s part 2 (D212's note, the design §19.15, C8-1.2): every save of <c>driver.json</c>, any key's, reads its keeping lock again
/// just before its rename and renames only while the lock still says what this writer wrote; a save whose lock another writer took (an aged
/// lock taken as stale) is refused with nothing written. The lock is planted here as that writer leaves it, since the writers of one
/// process queue on a gate and cannot take each other's; <see cref="DriverConfigFenceProcessTests"/> holds it against another process.
/// </summary>
public sealed class DriverConfigFenceTests : IDisposable
{
    private const string Start = "{\n  \"cap\": 3,\n  \"intakeAdapter\": \"codex-acp\"\n}\n";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-fence-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _path;

    public DriverConfigFenceTests()
    {
        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "driver.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Moment(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>The lock as another writer's take of it leaves it: its process, its time now.</summary>
    private string TakenByAnother()
    {
        var text = $"{{\"pid\":{Environment.ProcessId},\"since\":\"{Moment(DateTimeOffset.UtcNow)}\"}}\n";
        File.WriteAllText(KeepLock.PathOf(_path), text);
        return text;
    }

    private string[] Beside() => Directory.GetFiles(_folder, "*.writing");

    [Fact]
    public void A_save_that_holds_its_lock_through_the_look_renames_and_lets_it_go()
    {
        File.WriteAllText(_path, Start);
        var looked = 0;

        DriverConfig.Load(_path).WithCap(5).Save(_path, new KeepSeams { BeforeLook = () => looked++ });

        Assert.Equal(1, looked);
        Assert.Equal(Start.Replace("\"cap\": 3", "\"cap\": 5"), File.ReadAllText(_path));
        Assert.False(File.Exists(KeepLock.PathOf(_path)));
    }

    [Fact]
    public void A_save_whose_lock_another_writer_took_before_its_rename_writes_nothing_and_says_so_leaving_that_writers_lock()
    {
        File.WriteAllText(_path, Start);
        var taken = "";

        var refusal = Assert.Throws<KeepLockTakenException>(
            () => DriverConfig.Load(_path).WithCap(5).Save(_path, new KeepSeams { BeforeLook = () => taken = TakenByAnother() }));

        Assert.Equal(Start, File.ReadAllText(_path));
        Assert.Contains(KeepLock.PathOf(_path), refusal.Message);
        Assert.Contains("not written", refusal.Message);
        Assert.Equal(taken, File.ReadAllText(KeepLock.PathOf(_path)));
        Assert.Empty(Beside());
    }

    [Fact]
    public void A_save_whose_lock_is_gone_at_the_look_is_refused_too_since_another_writer_took_it_and_finished()
    {
        File.WriteAllText(_path, Start);

        Assert.Throws<KeepLockTakenException>(
            () => DriverConfig.Load(_path).WithCap(5).Save(_path, new KeepSeams { BeforeLook = () => File.Delete(KeepLock.PathOf(_path)) }));

        Assert.Equal(Start, File.ReadAllText(_path));
        Assert.Empty(Beside());
    }

    [Fact]
    public void The_refusal_is_the_io_exception_a_lock_held_for_the_whole_wait_raises_so_every_door_says_it_as_it_says_that()
    {
        File.WriteAllText(_path, Start);

        Assert.IsAssignableFrom<IOException>(Assert.Throws<KeepLockTakenException>(
            () => DriverConfig.Load(_path).WithCap(5).Save(_path, new KeepSeams { BeforeLook = () => TakenByAnother() })));
    }

    [Fact]
    public void A_config_built_in_memory_is_fenced_as_well()
    {
        Assert.Throws<KeepLockTakenException>(
            () => DriverConfig.Empty.WithCap(5).Save(_path, new KeepSeams { BeforeLook = () => TakenByAnother() }));

        Assert.False(File.Exists(_path));
    }

    /// <summary>
    /// The second window, named and not closed (§19.15, the bound against an older writer): a writer that took the lock after this save's last
    /// look and finished inside that instant is renamed over.
    /// </summary>
    [Fact]
    public void A_lock_taken_after_the_last_look_and_before_the_rename_is_not_seen_and_the_save_renames_over_that_writer_the_bound_stated()
    {
        File.WriteAllText(_path, Start);

        DriverConfig.Load(_path).WithCap(5).Save(_path, new KeepSeams
        {
            AfterLook = () =>
            {
                TakenByAnother();
                File.WriteAllText(_path, Start.Replace("\"intakeAdapter\": \"codex-acp\"", "\"intakeAdapter\": \"gone\""));
            },
        });

        Assert.Equal(Start.Replace("\"cap\": 3", "\"cap\": 5"), File.ReadAllText(_path));
    }

    /// <summary>
    /// The first window, named and not closed: an older save renames with no second look at its lock. Its lock aged, it is taken as stale by a
    /// newer save that completes, and the older rename lands over it afterwards.
    /// </summary>
    [Fact]
    public void An_older_save_whose_lock_aged_and_was_taken_by_a_newer_save_renames_late_over_it_the_bound_stated()
    {
        File.WriteAllText(_path, Start);
        var older = DriverConfig.Load(_path).WithCap(4).ToJson();
        var aged = $"{{\"pid\":{Environment.ProcessId},\"since\":\"{Moment(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2))}\"}}\n";
        File.WriteAllText(KeepLock.PathOf(_path), aged);

        DriverConfig.Load(_path).WithCap(5).Save(_path);
        Assert.Equal(Start.Replace("\"cap\": 3", "\"cap\": 5"), File.ReadAllText(_path));

        AtomicFile.WriteText(_path, older);

        Assert.Equal(Start.Replace("\"cap\": 3", "\"cap\": 4"), File.ReadAllText(_path));
    }

    [Fact]
    public void A_lock_planted_older_than_a_minute_is_taken_as_stale_and_the_save_that_took_it_renames_as_it_does_today()
    {
        File.WriteAllText(_path, Start);
        File.WriteAllText(KeepLock.PathOf(_path), $"{{\"pid\":{Environment.ProcessId},\"since\":\"{Moment(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2))}\"}}\n");

        DriverConfig.Load(_path).WithCap(5).Save(_path);

        Assert.Equal(Start.Replace("\"cap\": 3", "\"cap\": 5"), File.ReadAllText(_path));
        Assert.False(File.Exists(KeepLock.PathOf(_path)));
    }
}
