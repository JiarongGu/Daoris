using Daoris.Driver;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The agent family through the CLI and the host together (PUSH1f2e, the alone-path design §5's Process proofs): <c>daoris agent …</c>
/// typed at a terminal, run by Node through the CLI's one spawn site, reaching the driver's own program, which runs the agent's sign-in
/// at that terminal. The agent is a stub, never a real one (LIVELOGIN1): Claude Code pinned to a version whose managed binary is a
/// script under the scratch home, so the binary a start runs is that script and nothing on <c>PATH</c>; every other agent the build
/// declares is pinned to a version with nothing installed, so it is asked nothing. The stub's sign-in asks for a code on its standard
/// input, which is the terminal's, and keeps it in the account's folder; its status question answers by that file.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class AgentSignInProcessTests : IDisposable
{
    private const string Agent = "claude-code";

    private const string Stubbed = "9.9.9-stub";

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "agent-sign-in", Guid.NewGuid().ToString("N")[..8]);

    public AgentSignInProcessTests()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Said);
        Stub();
    }

    public void Dispose() => GitSeed.Discard(_root);

    private string Home => Path.Combine(_root, "home");

    /// <summary>Where the stub says what it was asked: its pid while its sign-in waits for a code, and each status question.</summary>
    private string Said => Path.Combine(_root, "said");

    private string Pid => Path.Combine(Said, "login.pid");

    private string Asked => Path.Combine(Said, "status.log");

    private const string Script =
        """
        import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
        import { join } from 'node:path';
        import { createInterface } from 'node:readline';
        const said = process.env.STUB_SAID;
        const home = process.env.CLAUDE_CONFIG_DIR ?? '';
        const words = process.argv.slice(2).join(' ');
        const credentials = join(home, 'credentials.json');
        if (words === '--version') {
          console.log('9.9.9-stub (Claude Code)');
        } else if (words === 'auth status') {
          appendFileSync(join(said, 'status.log'), home + '\n');
          console.log(existsSync(credentials)
            ? JSON.stringify({ loggedIn: true, email: 'seat@work.example' })
            : JSON.stringify({ loggedIn: false }));
        } else if (words === 'auth login') {
          process.stdout.write('Paste code here if prompted > ');
          writeFileSync(join(said, 'login.pid'), String(process.pid));
          const lines = createInterface({ input: process.stdin });
          let code = null;
          for await (const line of lines) { code = line.trim(); break; }
          lines.close();
          if (!code) { console.log('stub: no code was typed'); process.exit(1); }
          writeFileSync(credentials, JSON.stringify({ code }));
          console.log('stub: signed in');
          process.exit(0);
        } else {
          console.log('stub: ' + words + ' is not asked of me');
          process.exit(3);
        }
        """;

    /// <summary>
    /// The stub as Claude Code's managed binary at <see cref="Stubbed"/>, and every agent of the build pinned: Claude Code to the stub,
    /// each other one to a version with nothing installed, so no binary on this machine's <c>PATH</c> is started.
    /// </summary>
    private void Stub()
    {
        var bin = Path.Combine(HarnessSettings.ManagedHome(Home, Agent, Stubbed), "node_modules", ".bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "stub.mjs"), Script);
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(Path.Combine(bin, "claude.cmd"), "@node \"%~dp0stub.mjs\" %*\r\n");
        }
        else
        {
            var shim = Path.Combine(bin, "claude");
            File.WriteAllText(shim, "#!/bin/sh\nexec node \"$(dirname \"$0\")/stub.mjs\" \"$@\"\n");
            File.SetUnixFileMode(shim, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var set = AdapterSet.Built();
        var settings = new HarnessSettings().WithVersion(Agent, Stubbed);
        foreach (var name in set.Names.Where(name => name != Agent && set.Resolve(name).Toolchain is { Binary.Count: > 0 }))
        {
            settings = settings.WithVersion(name, "0.0.0-none");
        }

        settings.Save(DaorisHome.Entry(Home, HomeEntry.Harnesses));

        // 🔴 LIVELOGIN1: the binary a sign-in runs is the stub, by its full path, before anything starts.
        var binary = HarnessProbe.CommandOf(Agent, set.Resolve(Agent).Toolchain!, command: null, settings, Home);
        Assert.False(binary.Missing);
        Assert.StartsWith(bin, binary.Run[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The driver's own program, as the install lays it: the host's apphost beside the build these tests run.</summary>
    private static string Program()
    {
        var dll = DriverHostTests.HostDll();
        var program = OperatingSystem.IsWindows() ? Path.ChangeExtension(dll, ".exe") : Path.ChangeExtension(dll, null);
        return File.Exists(program) ? program : throw new FileNotFoundException("the host's apphost is built before these tests", program);
    }

    /// <summary>
    /// `daoris` as its entry runs it, from the CLI's sources (as <c>DriverProgramWordsProcessTests</c> runs it), at a terminal of this
    /// machine's variables with nothing of a Daoris in them but the scratch home and the program.
    /// </summary>
    private Process Typed(params string[] words)
    {
        var dispatcher =
            $"const {{ runCli }} = await import({JsonSerializer.Serialize(new Uri(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Cli", "src", "cli.ts")).AbsoluteUri)});"
            + " process.exitCode = await runCli(process.argv.slice(1), process.cwd());";
        var info = new ProcessStartInfo("node")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _root,
        };
        foreach (var word in (string[])["--input-type=module", "-e", dispatcher, "--", .. words]) info.ArgumentList.Add(word);
        foreach (var name in info.Environment.Keys.Where(key => key.StartsWith("DAORIS_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(name);
        }

        info.Environment["DAORIS_HOME"] = Home;
        info.Environment["DAORIS_DRIVER_PROGRAM"] = Program();
        info.Environment["STUB_SAID"] = Said;
        return Process.Start(info)!;
    }

    /// <summary>A run's end: its exit, and what it said on each stream.</summary>
    private static async Task<(int Code, string Out, string Error)> Ended(Process process, Task<string> output, Task<string> error)
    {
        using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"the run did not end: {await output}{await error}");
        }

        return (process.ExitCode, (await output).Replace("\r\n", "\n", StringComparison.Ordinal), await error);
    }

    private static async Task Until(Func<bool> holds, string what)
    {
        using var bound = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        while (!holds())
        {
            await Task.Delay(100, bound.Token).ConfigureAwait(false);
        }

        Assert.True(holds(), what);
    }

    private string[] StatusAsked() => File.Exists(Asked) ? File.ReadAllLines(Asked) : [];

    [Fact]
    public async Task A_code_typed_at_the_terminal_reaches_the_agent_s_sign_in_and_the_account_is_kept_and_named()
    {
        using var run = Typed("agent", "login", Agent, "--new", "--name", "Seat");
        var output = run.StandardOutput.ReadToEndAsync();
        var error = run.StandardError.ReadToEndAsync();

        await Until(() => File.Exists(Pid), "the stub's sign-in asked for its code");
        // Made before the flow, and nothing published while the person types.
        var account = Assert.Single(HarnessSettings.Profiles(Home, Agent));
        Assert.Null(AccountNames.NameOf(Home, Agent, account));
        await run.StandardInput.WriteAsync("code-1234\n");
        await run.StandardInput.FlushAsync();

        var (code, said, told) = await Ended(run, output, error);

        Assert.True(code == 0, $"{said}\n{told}");
        Assert.Equal(account, Assert.Single(HarnessSettings.Profiles(Home, Agent)));
        var where = HarnessSettings.ProfileHome(Home, Agent, account);
        Assert.Equal("code-1234", JsonDocument.Parse(File.ReadAllText(Path.Combine(where, "credentials.json"))).RootElement.GetProperty("code").GetString());
        Assert.Equal("Seat", AccountNames.NameOf(Home, Agent, account));
        Assert.NotNull(ProbeLock.SignedIn(Home, Agent, account));
        Assert.Contains($"signing in to a new `{Agent}` account, with its own login flow.\n", said, StringComparison.Ordinal);
        Assert.Contains("Paste code here if prompted > ", said, StringComparison.Ordinal);
        Assert.Contains("signed in as seat@work.example", said, StringComparison.Ordinal);
        Assert.False(File.Exists(HomeLease.PathIn(Home)), "the publication let the home go");
    }

    [Fact]
    public async Task A_sign_in_stopped_midway_leaves_its_folder_gone()
    {
        using var run = Typed("agent", "login", Agent, "--new");
        var output = run.StandardOutput.ReadToEndAsync();
        var error = run.StandardError.ReadToEndAsync();

        await Until(() => File.Exists(Pid) && new FileInfo(Pid).Length > 0, "the stub's sign-in asked for its code");
        var account = Assert.Single(HarnessSettings.Profiles(Home, Agent));
        // The agent's flow ends midway, as a sign-in its person walked away from does: the host's `finally` removes the folder.
        using (var stub = Process.GetProcessById(int.Parse(File.ReadAllText(Pid), System.Globalization.CultureInfo.InvariantCulture)))
        {
            stub.Kill();
        }

        var (code, said, told) = await Ended(run, output, error);

        Assert.True(code == 2, $"{said}\n{told}");
        Assert.Empty(HarnessSettings.Profiles(Home, Agent));
        Assert.False(Directory.Exists(HarnessSettings.ProfileHome(Home, Agent, account)), "the account's folder is gone");
        Assert.Contains("nothing was signed in, so nothing was kept — the account opened for it is gone again.", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_finished_sign_in_s_end_waits_for_another_process_s_hold_on_the_home_and_is_published_when_it_lets_go()
    {
        var held = HomeLease.TryAcquire(Home, LeaseKind.Verb, out _, "daoris-driver trees land");
        Assert.NotNull(held);
        using var run = Typed("agent", "login", Agent, "--new", "--name", "Seat");
        var output = run.StandardOutput.ReadToEndAsync();
        var error = run.StandardError.ReadToEndAsync();
        string account;
        try
        {
            await Until(() => File.Exists(Pid), "the stub's sign-in asked for its code");
            account = Assert.Single(HarnessSettings.Profiles(Home, Agent));
            await run.StandardInput.WriteAsync("code-5678\n");
            await run.StandardInput.FlushAsync();
            // Who signed in is asked before the end is published; then the end waits on this hold.
            var where = HarnessSettings.ProfileHome(Home, Agent, account);
            await Until(() => StatusAsked().Contains(where), "who signed in was asked");
            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.False(run.HasExited, "the end waits while another process holds the home");
            Assert.Null(AccountNames.NameOf(Home, Agent, account));
        }
        finally
        {
            held!.Dispose();
        }

        var (code, said, told) = await Ended(run, output, error);

        Assert.True(code == 0, $"{said}\n{told}");
        Assert.Equal("Seat", AccountNames.NameOf(Home, Agent, account));
    }

    [Fact]
    public async Task A_key_typed_at_the_terminal_is_read_through_the_CLI_and_the_host_and_published()
    {
        using var run = Typed("agent", "key", Agent);
        var output = run.StandardOutput.ReadToEndAsync();
        var error = run.StandardError.ReadToEndAsync();
        await run.StandardInput.WriteAsync("sk-ant-api03-typed-wxyz\n");
        run.StandardInput.Close();

        var (code, said, told) = await Ended(run, output, error);

        Assert.True(code == 0, $"{said}\n{told}");
        var account = Assert.Single(HarnessSettings.Profiles(Home, Agent));
        Assert.Equal("sk-ant-api03-typed-wxyz", HarnessKeys.Of(Home, Agent, account));
        Assert.Contains($"`{Agent}` account `{account}` is the API key …wxyz.", said, StringComparison.Ordinal);
        Assert.DoesNotContain("typed", said, StringComparison.Ordinal);
        Assert.False(File.Exists(HomeLease.PathIn(Home)), "the publication let the home go");
    }

    [Fact]
    public async Task The_host_s_list_asks_no_account_another_process_holds_the_probe_lock_of_until_it_lets_go()
    {
        var account = "account-1";
        var where = HarnessSettings.ProfileHome(Home, Agent, account);
        Directory.CreateDirectory(where);
        File.WriteAllText(Path.Combine(where, "credentials.json"), "{}");
        var held = await ProbeLock.TakeAsync(ProbeLock.PathOf(Home, Agent, account));
        Assert.NotNull(held);
        using var run = Typed("agent", "list");
        var output = run.StandardOutput.ReadToEndAsync();
        var error = run.StandardError.ReadToEndAsync();
        try
        {
            // Long enough for the list to have reached the account: its version was asked of the stub first.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.DoesNotContain(where, StatusAsked());
        }
        finally
        {
            await held!.DisposeAsync();
        }

        var (code, said, told) = await Ended(run, output, error);

        Assert.True(code == 0, $"{said}\n{told}");
        Assert.Equal([where], StatusAsked().Where(line => line.Length > 0));
        Assert.Contains("seat@work.example", said, StringComparison.Ordinal);
    }
}
