using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONSOLE4a (D96): a shell under a Windows pseudo-console, so the person's own terminal behaves as a
/// console window does. Every test here runs a real process, as the rest of this suite does: a pseudo-console
/// is a few Win32 calls whose whole value is what a real console program does under them.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class PseudoConsoleTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "daoris-pty-" + Guid.NewGuid().ToString("N")[..8]);

    public PseudoConsoleTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>What a terminal wrote and how it ended, as the callbacks were told.</summary>
    private sealed class Heard
    {
        private readonly StringBuilder _output = new();
        private readonly List<int> _exits = [];

        /// <summary>What had arrived at the moment the exit was told: the exit must follow the last output.</summary>
        public string? OutputAtExit { get; private set; }

        public string Output
        {
            get { lock (_output) return _output.ToString(); }
        }

        public IReadOnlyList<int> Exits
        {
            get { lock (_exits) return [.. _exits]; }
        }

        public void Write(string data)
        {
            lock (_output) _output.Append(data);
        }

        public void Exit(int code)
        {
            OutputAtExit = Output;
            lock (_exits) _exits.Add(code);
        }
    }

    private static string Cmd => CommandPresence.Resolve("cmd", startable: true)
        ?? throw new InvalidOperationException("cmd.exe is not on PATH");

    private PseudoConsole Start(Heard heard, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment = null) =>
        PseudoConsole.Start(
            new TerminalLaunch(new TerminalShell("cmd", Cmd, arguments), _folder, environment ?? new Dictionary<string, string>()),
            heard.Write, heard.Exit);

    [Fact]
    public async Task What_the_shell_prints_arrives_and_its_exit_follows_the_last_of_it()
    {
        if (!OperatingSystem.IsWindows()) return; // The pseudo-console is Windows'.

        var heard = new Heard();
        using var terminal = Start(heard, ["/c", "echo", "hello-from-the-pty"]);

        await Poll.Until(() => heard.Exits.Count > 0, () => $"no exit; heard: {heard.Output}");
        Assert.Equal(0, Assert.Single(heard.Exits));
        Assert.Contains("hello-from-the-pty", heard.OutputAtExit);
    }

    [Fact]
    public async Task An_exit_code_other_than_zero_is_the_shell_s_own()
    {
        if (!OperatingSystem.IsWindows()) return;

        var heard = new Heard();
        using var terminal = Start(heard, ["/c", "exit", "3"]);

        await Poll.Until(() => heard.Exits.Count > 0, () => $"no exit; heard: {heard.Output}");
        Assert.Equal(3, Assert.Single(heard.Exits));
    }

    [Fact]
    public async Task A_program_that_reads_a_line_gets_what_is_typed()
    {
        if (!OperatingSystem.IsWindows()) return;

        var heard = new Heard();
        using var terminal = Start(heard, ["/d", "/v:on", "/c", "set /p LINE=ask: & echo got:!LINE!"]);

        await Poll.Until(() => heard.Output.Contains("ask:"), () => $"no prompt; heard: {heard.Output}");
        terminal.Write("typed-here\r");

        await Poll.Until(() => heard.Output.Contains("got:typed-here"), () => $"the line never arrived; heard: {heard.Output}");
        await Poll.Until(() => heard.Exits.Count > 0, () => $"no exit; heard: {heard.Output}");
    }

    [Fact]
    public async Task Its_environment_is_this_process_s_own_plus_what_it_is_given()
    {
        if (!OperatingSystem.IsWindows()) return;

        var heard = new Heard();
        using var terminal = Start(
            heard, ["/c", "echo", "home=%DAORIS_HOME%;root=%SystemRoot%"],
            new Dictionary<string, string> { ["DAORIS_HOME"] = "marker-home-folder" });

        await Poll.Until(() => heard.Exits.Count > 0, () => $"no exit; heard: {heard.Output}");
        Assert.Contains("home=marker-home-folder;", heard.Output);
        // The process's own variables come with it: a terminal is the person's environment, not an empty one.
        Assert.Contains($"root={Environment.GetEnvironmentVariable("SystemRoot")}", heard.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task It_starts_in_the_folder_it_is_given()
    {
        if (!OperatingSystem.IsWindows()) return;

        var heard = new Heard();
        using var terminal = Start(heard, ["/c", "cd"]);

        await Poll.Until(() => heard.Exits.Count > 0, () => $"no exit; heard: {heard.Output}");
        // The console may wrap a long path at its width, so the line breaks it gained are taken out.
        Assert.Contains(Path.GetFileName(_folder), heard.Output.Replace("\r", "").Replace("\n", ""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_resize_is_taken_live_and_after_the_close_alike()
    {
        if (!OperatingSystem.IsWindows()) return;

        var heard = new Heard();
        var terminal = Start(heard, ["/d"]);
        await Poll.Until(() => heard.Output.Contains('>'), () => $"no prompt; heard: {heard.Output}");

        terminal.Resize(132, 43);
        terminal.Resize(1, 1);
        terminal.Resize(0, -4); // Nonsense is clamped, never thrown.
        terminal.Dispose();
        terminal.Resize(80, 24);
    }

    [Fact]
    public async Task Closing_it_ends_a_child_the_shell_started_too()
    {
        if (!OperatingSystem.IsWindows()) return;

        var heartbeat = Path.Combine(_folder, "heartbeat.txt");
        var heard = new Heard();
        var terminal = Start(heard, ["/d"]);
        await Poll.Until(() => heard.Output.Contains('>'), () => $"no prompt; heard: {heard.Output}");

        // A child that leaves the console: detached, so only the terminal's job can reach it.
        terminal.Write($"node \"{Parent()}\" \"{Child()}\" \"{heartbeat}\"\r");
        await Poll.Until(() => File.Exists(heartbeat), () => $"the child never started; heard: {heard.Output}");
        Assert.True(await Beating(heartbeat), "the child should run while the terminal is open");

        terminal.Dispose();

        Assert.False(await Beating(heartbeat), "the shell's child outlived its terminal");
        await Poll.Until(() => heard.Exits.Count > 0, () => "a closed terminal never said it ended");
    }

    [Fact]
    public async Task An_end_is_told_once_however_it_came()
    {
        if (!OperatingSystem.IsWindows()) return;

        var heard = new Heard();
        var terminal = Start(heard, ["/c", "echo", "done"]);
        await Poll.Until(() => heard.Exits.Count > 0, () => $"no exit; heard: {heard.Output}");

        terminal.Dispose();
        terminal.Dispose();
        terminal.Write("ignored\r"); // Nothing to write to, and nothing to say about it.
        await Task.Delay(300);

        Assert.Single(heard.Exits);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\cmd.exe", new string[0], @"C:\Windows\System32\cmd.exe")]
    [InlineData(@"C:\Program Files\Git\bin\bash.exe", new[] { "--login", "-i" }, @"""C:\Program Files\Git\bin\bash.exe"" --login -i")]
    [InlineData("cmd.exe", new[] { "/c", "set /p X=a & echo !X!" }, @"cmd.exe /c ""set /p X=a & echo !X!""")]
    [InlineData("x.exe", new[] { "", "say \"hi\"", @"C:\a folder\", @"C:\plain\" }, @"x.exe """" ""say \""hi\"""" ""C:\a folder\\"" C:\plain\")]
    public void A_command_line_quotes_as_Windows_reads_it(string program, string[] arguments, string expected)
    {
        Assert.Equal(expected, PseudoConsole.CommandLine(program, arguments));
    }

    private string Child()
    {
        var child = Path.Combine(_folder, "child.mjs");
        File.WriteAllText(child, """
            import { writeFileSync } from 'node:fs';
            const at = process.argv[2];
            setInterval(() => writeFileSync(at, String(Date.now())), 100);
            setTimeout(() => process.exit(0), 60000);
            """);
        return child;
    }

    private string Parent()
    {
        var parent = Path.Combine(_folder, "parent.mjs");
        File.WriteAllText(parent, """
            import { spawn } from 'node:child_process';
            const [child, at] = process.argv.slice(2);
            spawn(process.execPath, [child, at], { detached: true, stdio: 'ignore' }).unref();
            """);
        return parent;
    }

    /// <summary>Whether the heartbeat moves over most of a second.</summary>
    private static async Task<bool> Beating(string heartbeat)
    {
        var before = await Beat(heartbeat);
        await Task.Delay(800);
        var after = await Beat(heartbeat);
        return before != after;
    }

    /// <summary>
    /// The heartbeat as it stands, read beside its writer: the child rewrites it every 100 ms, and a read that
    /// asked to share only reading met the writer's open handle and threw, under load, twice in a row (TEST4).
    /// </summary>
    private static async Task<string?> Beat(string heartbeat)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (!File.Exists(heartbeat)) return null;
            try
            {
                using var stream = new FileStream(heartbeat, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (IOException) when (attempt < 20)
            {
                await Task.Delay(15);
            }
        }
    }
}
