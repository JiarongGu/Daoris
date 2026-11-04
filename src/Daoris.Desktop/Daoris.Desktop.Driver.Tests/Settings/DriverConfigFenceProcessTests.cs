using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1w-s part 2 (D212's note, the design §19.15, C8-1.2 and "its bound against an older writer") across two processes: the other
/// writer is a Node running the CLI's keeping lock (<c>keeplock.ts</c>), an older writer that takes a lock a minute old as stale. The
/// in-process half is <see cref="DriverConfigFenceTests"/>.
/// </summary>
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
