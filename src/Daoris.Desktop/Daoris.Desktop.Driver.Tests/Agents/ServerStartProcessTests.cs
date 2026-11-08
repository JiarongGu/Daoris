using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through real processes: a server a session is handed starts as one node and nothing before it, the harness's start
/// of it read as its job counts it; and a package no version of which is installed is installed once, by npm (a stand-in of node's
/// here, never the network), with the log saying so, and every later start takes it from the toolchain.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ServerStartProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "server start", Guid.NewGuid().ToString("N")[..8]);
    private readonly string _home;

    public ServerStartProcessTests()
    {
        _home = Path.Combine(_root, "a home");
        Directory.CreateDirectory(_home);
    }

    public void Dispose() => GitSeed.Discard(_root);

    private static string Node => PackageStarts.Node(null, null) ?? throw new InvalidOperationException("node is not on PATH");

    private static ServerEntry Npx(string name, params string[] arguments) => new(name, "npx", arguments, new Dictionary<string, string>());

    /// <summary>
    /// npm's stand-in: <c>install --prefix &lt;folder&gt; … &lt;package&gt;[@version]</c> lays the package there, at its exact version or at
    /// 2.0.0 for a tag, its one program <c>cli.js</c>, and counts its run in <c>runs.txt</c> beside it.
    /// </summary>
    private (IReadOnlyList<string> Npm, string Runs) StandInNpm()
    {
        var script = Path.Combine(_root, "npm-stand-in.mjs");
        File.WriteAllText(script, """
            import { mkdirSync, writeFileSync, appendFileSync } from 'node:fs';
            import { join } from 'node:path';
            const argv = process.argv.slice(2);
            const prefix = argv[argv.indexOf('--prefix') + 1];
            const spec = argv[argv.length - 1];
            const at = spec.lastIndexOf('@');
            const [name, written] = at > 0 ? [spec.slice(0, at), spec.slice(at + 1)] : [spec, 'latest'];
            const version = /^\d+\.\d+\.\d+$/.test(written) ? written : '2.0.0';
            const folder = join(prefix, 'node_modules', ...name.split('/'));
            mkdirSync(folder, { recursive: true });
            writeFileSync(join(folder, 'package.json'), JSON.stringify({ name, version, bin: { tickets: 'cli.js' } }));
            writeFileSync(join(folder, 'cli.js'), '');
            appendFileSync(join(import.meta.dirname, 'runs.txt'), spec + '\n');
            """);
        return ([Node, script], Path.Combine(_root, "runs.txt"));
    }

    private string Logged()
    {
        var logs = DaorisHome.Entry(_home, HomeEntry.Logs);
        return Directory.Exists(logs) ? string.Concat(Directory.EnumerateFiles(logs, "*", SearchOption.AllDirectories).Select(File.ReadAllText)) : "";
    }

    [Fact]
    public async Task A_cold_cache_installs_a_servers_package_once_and_says_so()
    {
        var (npm, runs) = StandInNpm();
        var server = Npx("tickets", "-y", "@acme/tickets@1.2.3", "--port", "0");
        var script = Path.Combine(ServerPackages.Folder(_home, "@acme/tickets", "1.2.3"), "node_modules", "@acme", "tickets", "cli.js");

        using (var log = new MachineLog(_home, "driver-test"))
        {
            foreach (var _ in new[] { 1, 2 })
            {
                var handed = Assert.Single(await SessionServers.DirectAsync(_home, [server], log, CancellationToken.None, npm));
                Assert.Equal(Node, handed.Command);
                Assert.Equal([script, "--port", "0"], handed.Arguments);
            }
        }

        Assert.Equal(["@acme/tickets@1.2.3"], File.ReadAllLines(runs));
        var logged = Logged();
        Assert.Contains($"\"{ServerPackages.Installed}\"", logged);
        Assert.Contains("@acme/tickets", logged);
        Assert.Single(Directory.GetDirectories(Path.GetDirectoryName(ServerPackages.Folder(_home, "@acme/tickets", "1.2.3"))!));
    }

    [Fact]
    public async Task A_tag_on_a_cold_cache_is_installed_once_under_the_version_npm_came_to()
    {
        var (npm, runs) = StandInNpm();
        var server = Npx("tickets", "-y", "@acme/tickets@latest");

        foreach (var _ in new[] { 1, 2 })
        {
            var handed = Assert.Single(await SessionServers.DirectAsync(_home, [server], null, CancellationToken.None, npm));
            Assert.Equal(
                [Path.Combine(ServerPackages.Folder(_home, "@acme/tickets", "2.0.0"), "node_modules", "@acme", "tickets", "cli.js")],
                handed.Arguments);
        }

        Assert.Equal(["@acme/tickets@latest"], File.ReadAllLines(runs));
    }

    [Fact]
    public async Task A_package_npm_cannot_install_withholds_its_server_and_the_log_says_why()
    {
        var npm = new[] { Node, "-e", "process.exit(3)" };

        using (var log = new MachineLog(_home, "driver-test"))
        {
            Assert.Empty(await SessionServers.DirectAsync(_home, [Npx("tickets", "-y", "@acme/tickets@1.2.3")], log, CancellationToken.None, npm));
        }

        var logged = Logged();
        Assert.Contains($"\"{ServerPackages.Refused}\"", logged);
        Assert.Contains("exited 3", logged);
        Assert.False(Directory.Exists(ServerPackages.Folder(_home, "@acme/tickets", "1.2.3")));
    }

    [Fact]
    public async Task An_install_the_move_loses_to_another_process_keeps_theirs_and_one_it_cannot_make_withholds_only_its_server()
    {
        var (npm, _) = StandInNpm();
        var npx = NpxCommand.Read("npx", ["-y", "@acme/tickets@1.2.3"]).Command!;

        var theirs = SessionServersTests.Lay(_home, "@acme/tickets", "1.2.3");
        Directory.Delete(ServerPackages.Folder(_home, "@acme/tickets", "1.2.3"), recursive: true);
        var kept = await ServerPackages.ScriptAsync(
            _home, npx, null, CancellationToken.None, npm,
            publish: (_, _) =>
            {
                SessionServersTests.Lay(_home, "@acme/tickets", "1.2.3");
                throw new IOException("the folder was made meanwhile");
            });
        Assert.Equal(theirs, kept);

        Directory.Delete(ServerPackages.Folder(_home, "@acme/tickets", "1.2.3"), recursive: true);
        var refused = await Assert.ThrowsAsync<DriverException>(() => ServerPackages.ScriptAsync(
            _home, npx, null, CancellationToken.None, npm, publish: (_, _) => throw new IOException("a file in it is held")));
        Assert.Contains("held", refused.Message);
    }

    /// <summary>
    /// The server a session is handed, started as its harness starts it, from a home whose path holds a space: the process is node,
    /// and the job it is held in counts one process, so no <c>cmd</c>, <c>npx</c> or npm's node stands before it.
    /// </summary>
    [Fact]
    public async Task A_sessions_server_starts_as_one_node_with_nothing_before_it()
    {
        SessionServersTests.Lay(_home, "@acme/server", "1.0.0", body: """
            process.stdout.write('ready ' + process.argv.slice(2).join(' ') + '\n');
            process.stdin.resume();
            process.stdin.on('end', () => process.exit(0));
            """);
        var (handed, _, _, _) = await SessionServers.HandNotedAsync(
            _home, [Npx("server", "-y", "@acme/server@1.0.0", "--flag", "a word")], browser: null, log: null, CancellationToken.None);
        var server = Assert.Single(handed);

        var info = new ProcessStartInfo(server.Command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in server.Arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var job = OperatingSystem.IsWindows() ? CreateJobObject(IntPtr.Zero, null) : IntPtr.Zero;
        try
        {
            if (OperatingSystem.IsWindows()) Assert.True(AssignProcessToJobObject(job, process.Handle));
            Assert.Equal("ready --flag a word", await process.StandardOutput.ReadLineAsync().WaitAsync(Poll.Patience));
            Assert.Equal("node", process.ProcessName, ignoreCase: true);
            // Its tree: the node, and only the console Windows hosts every console process in.
            if (OperatingSystem.IsWindows()) Assert.Equal(["node"], Tree(job).Where(name => name != "conhost"));
        }
        finally
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(Poll.Patience);
            if (job != IntPtr.Zero) CloseHandle(job);
        }
    }

    /// <summary>The names of the processes the job holds, by its own list of them.</summary>
    private static IReadOnlyList<string> Tree(IntPtr job)
    {
        const int ProcessIdList = 3;
        var size = 8 + (64 * IntPtr.Size);
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try
        {
            Assert.True(QueryInformationJobObject(job, ProcessIdList, buffer, (uint)size, IntPtr.Zero));
            var listed = System.Runtime.InteropServices.Marshal.ReadInt32(buffer, 4);
            var names = new List<string>();
            for (var at = 0; at < listed; at++)
            {
                var id = (int)System.Runtime.InteropServices.Marshal.ReadIntPtr(buffer, 8 + (at * IntPtr.Size));
                try
                {
                    using var held = Process.GetProcessById(id);
                    names.Add(held.ProcessName.ToLowerInvariant());
                }
                catch (ArgumentException)
                {
                    // Exited between the list and the look: not in the tree.
                }
            }

            return names;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryInformationJobObject(IntPtr job, int kind, IntPtr information, uint length, IntPtr returned);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
