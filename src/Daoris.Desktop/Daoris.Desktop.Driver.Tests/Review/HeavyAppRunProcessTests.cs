using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris gate run</c> as built, against a stand-in of the running Daoris that runs a terminal's heavy run through the
/// application's own half: the gate is started by the stand-in's process, never the verb's, in the verb's folder and environment,
/// writing to the verb's own output; the verb exits with the run's code; a verb killed while its run runs ends the run; and with no
/// Daoris to ask the verb says so and starts the gate itself.
/// </summary>
/// <remarks>
/// The gate is a node script that writes its parent's id, which is the shell <c>cmd.exe</c> the run starts it through, and the
/// variable handed to the verb alone, then waits for its go; the test reads which process started that shell from the system.
/// The home is a scratch folder in the repository's gitignored <c>_fixtures/</c>, and every <c>DAORIS_</c> variable of the machine
/// is dropped from the verb.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HeavyAppRunProcessTests : IAsyncDisposable
{
    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-app-run", Guid.NewGuid().ToString("N")[..8]);
    private readonly List<Process> _verbs = [];

    public HeavyAppRunProcessTests()
    {
        Directory.CreateDirectory(_scratch);
        File.WriteAllText(Path.Combine(_scratch, "probe.mjs"), """
            import { existsSync, renameSync, writeFileSync } from 'node:fs';
            import { join } from 'node:path';
            const folder = process.cwd();
            writeFileSync(join(folder, 'shell.next'), `${process.ppid} ${process.env.PROCTREE_PROBE ?? ''}`);
            renameSync(join(folder, 'shell.next'), join(folder, 'shell.txt'));
            const wait = setInterval(() => {
              if (!existsSync(join(folder, 'go'))) return;
              clearInterval(wait);
              console.log(`probe saw ${process.env.PROCTREE_PROBE ?? 'nothing'}`);
              process.exit(3);
            }, 20);
            setTimeout(() => process.exit(9), 120000);
            """);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var verb in _verbs)
        {
            try
            {
                if (!verb.HasExited) verb.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Ended already.
            }

            verb.Dispose();
        }

        await Task.CompletedTask;
        GitSeed.Discard(_scratch);
    }

    [Fact]
    public async Task A_gate_run_asked_of_the_running_app_is_started_by_the_app_and_the_verb_exits_with_its_code_and_output()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (home, id) = Home();
        await using var daoris = new InstanceStandIn(PipeName.For(id, Random.Shared.Next(100_000, 999_999)), id) { HeavyHome = home };

        var (verb, said) = Verb(home, daoris.Name);
        var (shell, seen) = await ShellAsync(verb, said);

        Assert.Equal(Environment.ProcessId, ParentOf(shell));
        Assert.NotEqual(verb.Id, ParentOf(shell));
        Assert.Equal("from-the-verb", seen);

        File.WriteAllText(Path.Combine(_scratch, "go"), "");
        await verb.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(90)).Token);
        var text = said.ToString();
        Assert.Equal(1, verb.ExitCode);
        Assert.Contains("probe saw from-the-verb", text);
        Assert.Contains("run-heavy: `command` failed (exit 3).", text);
        Assert.DoesNotContain("This terminal runs it itself", text);
    }

    [Fact]
    public async Task A_verb_killed_while_the_app_runs_its_gate_ends_the_gate()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (home, id) = Home();
        await using var daoris = new InstanceStandIn(PipeName.For(id, Random.Shared.Next(100_000, 999_999)), id) { HeavyHome = home };

        var (verb, said) = Verb(home, daoris.Name);
        var (shell, _) = await ShellAsync(verb, said);

        verb.Kill();
        Assert.True(await GoneAsync(shell), $"the gate's shell ({shell}) outlived the verb that asked for it:\n{said}");
    }

    [Fact]
    public async Task With_no_app_to_ask_the_verb_says_so_and_starts_the_gate_itself()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (home, _) = Home();

        var (verb, said) = Verb(home, instance: null);
        var (shell, seen) = await ShellAsync(verb, said);

        Assert.Equal(verb.Id, ParentOf(shell));
        Assert.Equal("from-the-verb", seen);
        File.WriteAllText(Path.Combine(_scratch, "go"), "");
        await verb.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(90)).Token);
        var text = said.ToString();
        Assert.Equal(1, verb.ExitCode);
        Assert.Contains("run-heavy: a workspace build reads no address file. This terminal runs it itself, and what it starts is its own.", text);
        Assert.Contains("probe saw from-the-verb", text);
    }

    private (string Home, string Id) Home()
    {
        var home = Path.Combine(_scratch, "home");
        Directory.CreateDirectory(home);
        return (home, HomeIdentity.Ensure(home)!);
    }

    /// <summary><c>daoris gate run -- node probe.mjs</c> as built, in the scratch folder, on the home alone, its output read as it comes.</summary>
    private (Process Verb, StringBuilder Said) Verb(string home, string? instance)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _scratch,
        };
        foreach (var word in new[] { DriverHostTests.HostDll(), "gate", "run", "--", "node", "probe.mjs" }) info.ArgumentList.Add(word);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = home;
        info.Environment["PROCTREE_PROBE"] = "from-the-verb";
        if (instance is not null) info.Environment[InstanceDoor.Variable] = instance;

        var said = new StringBuilder();
        var verb = new Process { StartInfo = info };
        verb.OutputDataReceived += (_, line) => { if (line.Data is { } data) lock (said) said.AppendLine(data); };
        verb.ErrorDataReceived += (_, line) => { if (line.Data is { } data) lock (said) said.AppendLine(data); };
        verb.Start();
        verb.BeginOutputReadLine();
        verb.BeginErrorReadLine();
        _verbs.Add(verb);
        return (verb, said);
    }

    /// <summary>The gate's shell, once the probe under it has said its parent, and the variable the probe saw.</summary>
    private async Task<(int Shell, string Seen)> ShellAsync(Process verb, StringBuilder said)
    {
        var file = Path.Combine(_scratch, "shell.txt");
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < until && !verb.HasExited)
        {
            if (File.Exists(file) && File.ReadAllText(file).Split(' ', 2) is [var pid, var seen] && int.TryParse(pid, out var shell))
            {
                return (shell, seen);
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"the gate never started:\n{said}");
    }

    private static async Task<bool> GoneAsync(int pid)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < until)
        {
            if (ParentOf(pid) is null) return true;
            await Task.Delay(50);
        }

        return false;
    }

    /// <summary>The process that started <paramref name="pid"/>, as the system's snapshot says; null where it no longer runs.</summary>
    private static int? ParentOf(int pid)
    {
        var snapshot = CreateToolhelp32Snapshot(0x00000002, 0);
        if (snapshot == new IntPtr(-1)) return null;
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            for (var more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
            {
                if (entry.ProcessId == pid) return (int)entry.ParentProcessId;
            }

            return null;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
