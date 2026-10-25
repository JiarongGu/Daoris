using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END2's cross-process writer (D173's END2 note): two writers of <c>session-branches.json</c> in two processes are serialized
/// by the record's lock under the home's <c>locks/records/</c>. The other process is a PowerShell that takes the lock file as the driver
/// does, reads the record, waits, and writes it back with its own entry: the lock file is the contract, as the lease's file is
/// (<see cref="HomeLeaseProcessTests"/>). The in-process half is <see cref="HeldFactsTests"/>'.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class RecordLockProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-record-lock-" + Guid.NewGuid().ToString("N")[..8]);

    public RecordLockProcessTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 The other process holds the lock across its read and its write; this one's edit waits for it, then reads what it wrote. Without
    /// the lock this edit would land inside the other's window and the other's write would drop it.
    /// </summary>
    [Fact]
    public async Task Two_writers_in_two_processes_are_serialized_and_neither_write_is_lost()
    {
        if (!OperatingSystem.IsWindows()) return;
        var grown = new SessionBranches(_home);
        grown.Record(new GrownBranch("engine", "default", "daoris/first", "main", "f0", null, DateTimeOffset.UtcNow));
        var lockPath = RecordLock.PathOf(_home, SessionBranches.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

        // Takes the lock, says so, reads the record, waits, and writes it back with its own entry before letting go.
        var script = $$"""
            $lock = [System.IO.File]::Open('{{lockPath}}', 'OpenOrCreate', 'ReadWrite', 'None')
            [Console]::Out.WriteLine('held'); [Console]::Out.Flush()
            $text = [System.IO.File]::ReadAllText('{{grown.FilePath}}')
            Start-Sleep -Milliseconds 1500
            $entry = '{ "repository": "engine", "workspace": "default", "branch": "daoris/other", "from": "f0", "at": "2026-10-17T00:00:00Z" }'
            $text = $text.Replace('"branches": [', '"branches": [' + $entry + ',')
            [System.IO.File]::WriteAllText('{{grown.FilePath}}', $text)
            $lock.Dispose()
            """;
        using var other = Process.Start(new ProcessStartInfo("powershell", ["-NoProfile", "-NonInteractive", "-Command", script])
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        })!;
        var errors = other.StandardError.ReadToEndAsync();
        Assert.Equal("held", await other.StandardOutput.ReadLineAsync());

        grown.Record(new GrownBranch("engine", "default", "daoris/mine", "main", "f0", null, DateTimeOffset.UtcNow));

        await other.WaitForExitAsync();
        Assert.True(other.ExitCode == 0, await errors);
        Assert.Equal(["daoris/first", "daoris/mine", "daoris/other"], grown.All().Select(each => each.Branch).Order(StringComparer.Ordinal));
    }
}
