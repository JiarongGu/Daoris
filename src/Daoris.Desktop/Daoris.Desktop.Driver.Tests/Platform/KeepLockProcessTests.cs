using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONFIGLOCK1 across the two runtimes (D173's CONFIG1 note): the driver's writer of <c>driver.json</c> and the CLI's take one lock,
/// <c>driver.json.lock</c>, so neither's edit lands inside the other's re-read and rename. The other process is a Node running the CLI's
/// own <c>keeplock.ts</c> and <c>driverconfig.ts</c>: the lock file is the contract, as the lease's is (<see cref="RecordLockProcessTests"/>).
/// The in-process half is <see cref="KeepLockTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class KeepLockProcessTests : IDisposable
{
    private const string Start = "{\n  \"drivable\": [\n    \"engine\"\n  ],\n  \"notify\": true\n}\n";

    private const string Both = "{\n  \"drivable\": [\n    \"engine\",\n    \"orders\"\n  ],\n  \"notify\": false\n}\n";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-keeplock-process-" + Guid.NewGuid().ToString("N")[..8]);

    public KeepLockProcessTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Source(params string[] parts) =>
        new Uri(Path.Combine([WorkspaceRoot.Folder, "src", "Daoris.Cli", "src", .. parts])).AbsoluteUri;

    /// <summary>A Node process running <paramref name="script"/> as a module, <c>KEEP_FILE</c> naming the file.</summary>
    private static Process Node(string script, string file)
    {
        var start = new ProcessStartInfo("node", ["--input-type=module", "-e", script])
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        start.Environment["KEEP_FILE"] = file;
        return Process.Start(start)!;
    }

    /// <summary>
    /// 🔴 The terminal holds the lock across its read and its write, writing what it read with its own change; the driver's edit, made
    /// meanwhile, waits for it, then reads the file again and keeps the terminal's change.
    /// </summary>
    [Fact]
    public async Task The_drivers_edit_waits_for_the_terminal_holding_the_lock_and_neither_is_lost()
    {
        var path = Path.Combine(_folder, "driver.json");
        File.WriteAllText(path, Start);
        var script = $$"""
            import { readFileSync, writeFileSync } from 'node:fs';
            import { takeKeepLock } from '{{Source("platform", "keeplock.ts")}}';
            const held = takeKeepLock(process.env.KEEP_FILE);
            const text = readFileSync(process.env.KEEP_FILE, 'utf8');
            process.stdout.write('held\n');
            Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1500);
            writeFileSync(process.env.KEEP_FILE, text.replace('"notify": true', '"notify": false'));
            held.release();
            """;
        using var other = Node(script, path);
        var errors = other.StandardError.ReadToEndAsync();
        Assert.Equal("held", await other.StandardOutput.ReadLineAsync());

        DriverConfig.Load(path).WithDrivable("orders", true).Save(path);

        await other.WaitForExitAsync();
        Assert.True(other.ExitCode == 0, await errors);
        Assert.Equal(Both, File.ReadAllText(path));
    }

    /// <summary>
    /// 🔴 The driver holds the lock across its read and its write; the terminal's edit, made meanwhile, waits for it, then reads the file
    /// again and keeps the driver's change.
    /// </summary>
    [Fact]
    public async Task The_terminals_edit_waits_for_the_driver_holding_the_lock_and_neither_is_lost()
    {
        var path = Path.Combine(_folder, "driver.json");
        File.WriteAllText(path, Start);
        var script = $$"""
            import { readDriverChoices, writeDriverChoices } from '{{Source("driver", "driverconfig.ts")}}';
            const choices = readDriverChoices(process.env.KEEP_FILE);
            process.stdout.write('read\n');
            writeDriverChoices(process.env.KEEP_FILE, { ...choices, drivable: [...choices.drivable, 'orders'] });
            """;
        Process other;
        Task<string> errors;
        using (KeepLock.Take(path))
        {
            var read = File.ReadAllText(path);
            other = Node(script, path);
            errors = other.StandardError.ReadToEndAsync();
            Assert.Equal("read", await other.StandardOutput.ReadLineAsync());
            // Long enough for the terminal to reach its write, and to have made it, had it not waited.
            await Task.Delay(400);
            File.WriteAllText(path, read.Replace("\"notify\": true", "\"notify\": false"));
        }

        using (other)
        {
            await other.WaitForExitAsync();
            Assert.True(other.ExitCode == 0, await errors);
        }

        Assert.Equal(Both, File.ReadAllText(path));
    }
}
