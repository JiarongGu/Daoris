using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1w-s part 2 (D212's note, the design §19.15, C8-1.2 and "its bound against an older writer, both orders") across two
/// processes: the other writer is a Node running the CLI's own keeping write (<c>keeplock.ts</c>, <c>jsonkeep.ts</c>), an older writer that
/// takes a lock a minute old as stale and renames with no second look. The in-process half is <see cref="DriverConfigFenceTests"/>.
/// </summary>
/// <remarks>
/// A lock the newer save holds is aged by handing its take a clock five minutes back (<see cref="KeepSeams.Clock"/>), so the lock says what
/// a lock a minute old says and a Node takes it as stale at once, with no wait of a minute.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DriverConfigFenceProcessTests : IDisposable
{
    private const string Start = "{\n  \"cap\": 3,\n  \"intakeAdapter\": \"codex-acp\"\n}\n";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-fence-process-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _path;

    public DriverConfigFenceProcessTests()
    {
        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "driver.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly string KeepLockSource =
        new Uri(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "src", "platform", "keeplock.ts")).AbsoluteUri;

    private static readonly string KeepingSource =
        new Uri(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "src", "platform", "jsonkeep.ts")).AbsoluteUri;

    /// <summary>The older writer's save of Intake turned off, as the CLI's keeping write makes it: the key taken out of the file.</summary>
    private static readonly string IntakeOff = $$"""
        import { writeKeeping } from '{{KeepingSource}}';
        writeKeeping(process.env.KEEP_FILE, true, { cap: 3, intakeAdapter: 'codex-acp' }, { cap: 3 });
        """;

    private static Process Node(string script, string file, bool input = false)
    {
        var start = new ProcessStartInfo("node", ["--input-type=module", "-e", script])
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = input, StandardOutputEncoding = Encoding.UTF8,
        };
        start.Environment["KEEP_FILE"] = file;
        return Process.Start(start)!;
    }

    /// <summary>Run <see cref="IntakeOff"/> to its end, as the older writer that takes the aged lock; fails the test if it did not finish.</summary>
    private void OlderWriterTurnsIntakeOff()
    {
        using var other = Node(IntakeOff, _path);
        var errors = other.StandardError.ReadToEndAsync();
        Assert.True(other.WaitForExit(Poll.Patience), "the older writer finished");
        Assert.True(other.ExitCode == 0, errors.Result);
    }

    private static KeepSeams Aged(Action? before = null, Action? after = null) =>
        new() { Clock = new Aged5Minutes(), BeforeLook = before, AfterLook = after };

    private sealed class Aged5Minutes : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// A newer save of an unrelated setting (a cap), paused with its lock aged while an older save turns Intake off, before its last look:
    /// refused with nothing written, the turn-off kept in the file (the next check applies it).
    /// </summary>
    [Fact]
    public void A_newer_save_whose_lock_aged_and_was_taken_by_an_older_save_that_completed_is_refused_and_the_older_turn_off_stands()
    {
        File.WriteAllText(_path, Start);

        var refusal = Assert.Throws<KeepLockTakenException>(
            () => DriverConfig.Load(_path).WithCap(5).Save(_path, Aged(before: OlderWriterTurnsIntakeOff)));

        Assert.Contains("not written", refusal.Message);
        Assert.Equal("{\n  \"cap\": 3\n}\n", File.ReadAllText(_path));
    }

    /// <summary>
    /// The second window, named and not closed: the same older save completing after the newer save's last look and before its rename is
    /// renamed over and lost; were it a turn-off, the work stays on in both builds until the person turns it off again.
    /// </summary>
    [Fact]
    public void An_older_save_that_completed_after_the_newer_saves_last_look_is_renamed_over_and_lost_the_bound_stated()
    {
        File.WriteAllText(_path, Start);

        DriverConfig.Load(_path).WithCap(5).Save(_path, Aged(after: OlderWriterTurnsIntakeOff));

        Assert.Equal(Start.Replace("\"cap\": 3", "\"cap\": 5"), File.ReadAllText(_path));
    }

    /// <summary>
    /// The first window, named and not closed: an older save whose lock aged, taken as stale by a newer save that completed, renames
    /// late over it, the older Intake showing in the file again until the next check.
    /// </summary>
    [Fact]
    public async Task An_older_save_whose_lock_aged_and_was_taken_by_a_newer_save_renames_late_over_it_the_bound_stated()
    {
        File.WriteAllText(_path, Start);
        var script = $$"""
            import { readFileSync, writeFileSync } from 'node:fs';
            import { takeKeepLock } from '{{KeepLockSource}}';
            const held = takeKeepLock(process.env.KEEP_FILE);
            const text = readFileSync(process.env.KEEP_FILE, 'utf8');
            process.stdout.write('held\n');
            readFileSync(0);
            writeFileSync(process.env.KEEP_FILE, text.replace('"cap": 3', '"cap": 4'));
            held.release();
            """;
        using var older = Node(script, _path, input: true);
        var errors = older.StandardError.ReadToEndAsync();
        Assert.Equal("held", await older.StandardOutput.ReadLineAsync());

        // A minute has passed: the older writer's lock says so, its process still alive.
        var aged = $"{{\"pid\":{older.Id},\"since\":\"{(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(2)).UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}\"}}\n";
        File.WriteAllText(KeepLock.PathOf(_path), aged);
        (DriverConfig.Load(_path) with { IntakeAdapter = null }).WithCap(5).Save(_path);
        Assert.Equal("{\n  \"cap\": 5\n}\n", File.ReadAllText(_path));

        older.StandardInput.Close();
        await older.WaitForExitAsync();
        Assert.True(older.ExitCode == 0, await errors);

        Assert.Equal(Start.Replace("\"cap\": 3", "\"cap\": 4"), File.ReadAllText(_path));
    }
}
