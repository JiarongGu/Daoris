using System.ComponentModel;
using System.Text.Json;
using Daoris.Driver;
using Microsoft.Extensions.DependencyInjection;
using Shenora;
using Shenora.Core.Events;
using Shenora.Core.Ipc;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// CONSOLE4a (D96): <c>DAORIS.TERMINAL</c>, the page's door onto the person's own shells. The module's logic
/// is held over a fake factory, so every case is reachable without a shell; one round trip runs a real one.
/// </summary>
public sealed class TerminalModuleTests : Bridge
{
    private static readonly TerminalShell Pwsh = new(TerminalShells.Pwsh, @"C:\seven\pwsh.exe", ["-NoLogo"]);
    private static readonly TerminalShell PowerShell = new(TerminalShells.WindowsPowerShell, @"C:\system\powershell.exe", ["-NoLogo"]);
    private static readonly TerminalShell Cmd = new(TerminalShells.Cmd, @"C:\system\cmd.exe", []);

    private sealed class FakeTerminal(TerminalLaunch launch, Action<string> output, Action<int> exited) : ITerminal
    {
        public TerminalLaunch Launch { get; } = launch;
        public List<string> Written { get; } = [];
        public List<(int Columns, int Rows)> Sizes { get; } = [];
        public bool Disposed { get; private set; }

        public void Say(string data) => output(data);

        public void End(int code) => exited(code);

        public void Write(string data) => Written.Add(data);

        public void Resize(int columns, int rows) => Sizes.Add((columns, rows));

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeTerminals : ITerminalFactory
    {
        public List<TerminalShell> Available { get; } = [Pwsh, PowerShell, Cmd];
        public List<FakeTerminal> Started { get; } = [];
        public Exception? Refuse { get; set; }

        public IReadOnlyList<TerminalShell> Shells() => Available;

        public ITerminal Start(TerminalLaunch launch, Action<string> output, Action<int> exited)
        {
            if (Refuse is not null) throw Refuse;
            var terminal = new FakeTerminal(launch, output, exited);
            Started.Add(terminal);
            return terminal;
        }
    }

    private readonly FakeTerminals _terminals = new();

    private TerminalModule Module() => new(Bus, _terminals);

    private static JsonElement Payload(EventMessage message) =>
        JsonSerializer.SerializeToElement(message.Payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private IReadOnlyList<JsonElement> RaisedOf(string type) =>
        Raised.Where(message => message.Type == type).Select(Payload).ToList();

    private static async Task UntilAsync(Func<bool> condition, string waiting)
    {
        for (var waited = 0; !condition(); waited += 20)
        {
            if (waited > 10_000) throw new TimeoutException($"never: {waiting}");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task It_lists_the_machine_s_shells_with_the_default_named()
    {
        using var module = Module();

        var answer = await AnswerAsync(module, "SHELLS");

        Assert.Equal(["pwsh", "powershell", "cmd"], answer.GetProperty("shells").EnumerateArray().Select(row => row.GetProperty("shell").GetString()));
        Assert.Equal("pwsh", answer.GetProperty("default").GetString());
    }

    [Fact]
    public async Task Opened_with_nothing_named_it_is_the_default_shell_in_the_home_with_the_home_in_its_environment()
    {
        using var module = Module();

        var answer = await AnswerAsync(module, "OPEN");

        var started = Assert.Single(_terminals.Started);
        Assert.Equal(Pwsh, started.Launch.Shell);
        Assert.Equal(Home, started.Launch.Directory);
        Assert.Equal(Home, started.Launch.Environment["DAORIS_HOME"]);
        Assert.Equal("pwsh", answer.GetProperty("shell").GetString());
        Assert.Equal(Home, answer.GetProperty("cwd").GetString());
        Assert.False(string.IsNullOrEmpty(answer.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task Opened_with_a_shell_a_folder_and_a_size_it_starts_as_asked()
    {
        using var module = Module();
        var folder = Directory.CreateDirectory(Path.Combine(Home, "repository")).FullName;

        var answer = await AnswerAsync(module, "OPEN", new { shell = "cmd", cwd = folder, cols = 120, rows = 30 });

        var started = Assert.Single(_terminals.Started);
        Assert.Equal(Cmd, started.Launch.Shell);
        Assert.Equal(folder, started.Launch.Directory);
        Assert.Equal((120, 30), (started.Launch.Columns, started.Launch.Rows));
        Assert.Equal(folder, answer.GetProperty("cwd").GetString());
    }

    /// <summary>A tree removed since the page last looked: the terminal still opens, in the home, and says where.</summary>
    [Fact]
    public async Task A_folder_that_is_not_there_opens_it_in_the_home_and_the_answer_says_so()
    {
        using var module = Module();

        var answer = await AnswerAsync(module, "OPEN", new { cwd = Path.Combine(Home, "gone") });

        Assert.Equal(Home, Assert.Single(_terminals.Started).Launch.Directory);
        Assert.Equal(Home, answer.GetProperty("cwd").GetString());
    }

    [Fact]
    public async Task A_shell_nobody_knows_and_one_this_machine_lacks_are_refused_by_name()
    {
        using var module = Module();

        Assert.StartsWith(Refusals.TerminalShellUnknown, await RefusalAsync(module, "OPEN", new { shell = "fish" }));
        Assert.StartsWith(Refusals.TerminalShellMissing, await RefusalAsync(module, "OPEN", new { shell = "bash" }));
        Assert.Contains("shell=bash", await RefusalAsync(module, "OPEN", new { shell = "bash" }));
        Assert.Empty(_terminals.Started);
    }

    [Fact]
    public async Task A_machine_with_no_shell_says_so_rather_than_opening_nothing()
    {
        _terminals.Available.Clear();
        using var module = Module();

        Assert.StartsWith(Refusals.TerminalNoShell, await RefusalAsync(module, "OPEN"));
        Assert.Equal(JsonValueKind.Null, (await AnswerAsync(module, "SHELLS")).GetProperty("default").ValueKind);
    }

    [Fact]
    public async Task A_shell_the_system_would_not_start_is_refused_with_the_system_s_reason()
    {
        _terminals.Refuse = new Win32Exception(5);
        using var module = Module();

        var refusal = await RefusalAsync(module, "OPEN", new { shell = "cmd" });

        Assert.StartsWith(Refusals.TerminalNotStarted, refusal);
        Assert.Contains("shell=cmd", refusal);
        Assert.Contains($"problem={new Win32Exception(5).Message}", refusal);
    }

    [Fact]
    public async Task What_the_person_types_and_the_view_s_size_reach_the_shell()
    {
        using var module = Module();
        var id = (await AnswerAsync(module, "OPEN")).GetProperty("id").GetString();

        Assert.True((await AnswerAsync(module, "INPUT", new { id, data = "dir\r" })).GetProperty("written").GetBoolean());
        Assert.True((await AnswerAsync(module, "RESIZE", new { id, cols = 100, rows = 40 })).GetProperty("resized").GetBoolean());

        var terminal = Assert.Single(_terminals.Started);
        Assert.Equal(["dir\r"], terminal.Written);
        Assert.Equal([(100, 40)], terminal.Sizes);
    }

    /// <summary>
    /// A keystroke for a terminal that has gone is not a refusal: a toast per key would be the page's noise,
    /// and the page already knows it ended.
    /// </summary>
    [Fact]
    public async Task Input_for_a_terminal_that_is_not_open_is_answered_quietly()
    {
        using var module = Module();

        Assert.False((await AnswerAsync(module, "INPUT", new { id = "t-nobody", data = "x" })).GetProperty("written").GetBoolean());
        Assert.False((await AnswerAsync(module, "RESIZE", new { id = "t-nobody", cols = 1, rows = 1 })).GetProperty("resized").GetBoolean());
        Assert.False((await AnswerAsync(module, "CLOSE", new { id = "t-nobody" })).GetProperty("closed").GetBoolean());
    }

    [Fact]
    public async Task What_the_shell_writes_is_batched_into_one_event_per_window()
    {
        using var module = Module();
        var id = (await AnswerAsync(module, "OPEN")).GetProperty("id").GetString();
        var terminal = Assert.Single(_terminals.Started);

        terminal.Say("PS C:\\> ");
        terminal.Say("d");
        terminal.Say("ir");

        await UntilAsync(() => RaisedOf("TERMINAL_OUTPUT").Count > 0, "an output event");
        var output = Assert.Single(RaisedOf("TERMINAL_OUTPUT"));
        Assert.Equal(id, output.GetProperty("id").GetString());
        Assert.Equal("PS C:\\> dir", output.GetProperty("data").GetString());
    }

    [Fact]
    public async Task A_shell_that_ends_on_its_own_says_its_last_words_then_its_code()
    {
        using var module = Module();
        var id = (await AnswerAsync(module, "OPEN")).GetProperty("id").GetString();
        var terminal = Assert.Single(_terminals.Started);

        terminal.Say("bye");
        terminal.End(3);

        await UntilAsync(() => RaisedOf("TERMINAL_EXITED").Count > 0, "an exit event");
        var types = Raised.Select(message => message.Type).Where(type => type.StartsWith("TERMINAL_", StringComparison.Ordinal)).ToList();
        Assert.Equal(["TERMINAL_OUTPUT", "TERMINAL_EXITED"], types);
        var exited = Assert.Single(RaisedOf("TERMINAL_EXITED"));
        Assert.Equal(id, exited.GetProperty("id").GetString());
        Assert.Equal(3, exited.GetProperty("code").GetInt32());
        // Gone once it said so.
        Assert.False((await AnswerAsync(module, "INPUT", new { id, data = "x" })).GetProperty("written").GetBoolean());
    }

    [Fact]
    public async Task Closing_a_tab_ends_its_terminal_and_tells_no_exit_the_page_already_knows()
    {
        using var module = Module();
        var id = (await AnswerAsync(module, "OPEN")).GetProperty("id").GetString();
        var terminal = Assert.Single(_terminals.Started);

        Assert.True((await AnswerAsync(module, "CLOSE", new { id })).GetProperty("closed").GetBoolean());
        terminal.End(1); // The closed shell's own end, told afterwards.
        await Task.Delay(200);

        Assert.True(terminal.Disposed);
        Assert.Empty(RaisedOf("TERMINAL_EXITED"));
        Assert.False((await AnswerAsync(module, "CLOSE", new { id })).GetProperty("closed").GetBoolean());
    }

    [Fact]
    public async Task Each_terminal_is_its_own_shell_and_its_own_process()
    {
        using var module = Module();

        var first = (await AnswerAsync(module, "OPEN", new { shell = "pwsh" })).GetProperty("id").GetString();
        var second = (await AnswerAsync(module, "OPEN", new { shell = "cmd" })).GetProperty("id").GetString();
        await AnswerAsync(module, "INPUT", new { id = second, data = "ver\r" });
        await AnswerAsync(module, "CLOSE", new { id = first });

        Assert.NotEqual(first, second);
        Assert.Equal(2, _terminals.Started.Count);
        Assert.True(_terminals.Started[0].Disposed);
        Assert.False(_terminals.Started[1].Disposed);
        Assert.Empty(_terminals.Started[0].Written);
        Assert.Equal(["ver\r"], _terminals.Started[1].Written);
    }

    /// <summary>Every terminal ends when the application does: the container that made the module disposes it.</summary>
    [Fact]
    public async Task Every_terminal_ends_when_the_application_does()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<ITerminalFactory>(_terminals);
        services.AddIpcModule<TerminalModule>();
        var provider = services.BuildServiceProvider();
        var module = provider.GetServices<IIpcModule>().Single(candidate => candidate.ModuleName == "DAORIS.TERMINAL");

        await AnswerAsync(module, "OPEN");
        await AnswerAsync(module, "OPEN", new { shell = "cmd" });
        await provider.DisposeAsync();

        Assert.Equal(2, _terminals.Started.Count);
        Assert.All(_terminals.Started, terminal => Assert.True(terminal.Disposed));
    }

    /// <summary>
    /// 🔴 D94: a terminal's words are the person's own. Through the real dispatcher with the machine log's
    /// middleware, what is typed, what the shell writes and what a refusal names never reach the log; a
    /// refusal is logged by its code, as every module's is.
    /// </summary>
    [Fact]
    public async Task Nothing_typed_written_or_named_in_a_terminal_reaches_the_machine_log()
    {
        using var log = new MachineLog(Home, "desktop");
        var services = new ServiceCollection();
        services.AddSingleton<IEventBus>(Bus);
        services.AddSingleton<ITerminalFactory>(_terminals);
        services.AddIpcModule<TerminalModule>();
        services.UseMessageDispatcher((_, dispatcher) => dispatcher.Use(RefusalLog.Middleware(log)));
        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IMessageDispatcher>();

        var opened = await Dispatch(dispatcher, "OPEN", new { cwd = Path.Combine(Home, "secret-folder") });
        var id = JsonSerializer.SerializeToElement(opened.Data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            .GetProperty("id").GetString();
        await Dispatch(dispatcher, "INPUT", new { id, data = "echo secret-typed\r" });
        _terminals.Started[0].Say("secret-written");
        await Dispatch(dispatcher, "OPEN", new { shell = "secret-shell" });
        await UntilAsync(() => RaisedOf("TERMINAL_OUTPUT").Count > 0, "the output event");

        var folder = Path.Combine(Home, MachineLog.Folder);
        var written = Directory.Exists(folder)
            ? string.Join('\n', Directory.GetFiles(folder).Select(path =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }))
            : "";
        Assert.DoesNotContain("secret", written);
        Assert.Contains(Refusals.TerminalShellUnknown, written);
    }

    private static Task<IpcResponse> Dispatch(IMessageDispatcher dispatcher, string type, object? payload = null) =>
        dispatcher.DispatchAsync(
            new IpcRequest
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Module = "DAORIS.TERMINAL",
                Type = type,
                Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload),
            },
            CancellationToken.None);

    /// <summary>One real round trip: Command Prompt under the pseudo-console, typed at and read back through the module.</summary>
    [Fact]
    public async Task A_real_shell_round_trips_through_the_module()
    {
        if (!OperatingSystem.IsWindows()) return; // The pseudo-console is Windows'.

        using var module = new TerminalModule(Bus, new PseudoConsoleTerminals());
        // Wide, so the home's path is never wrapped across the console's lines.
        var id = (await AnswerAsync(module, "OPEN", new { shell = "cmd", cols = 400, rows = 30 })).GetProperty("id").GetString();
        string Heard() => string.Concat(RaisedOf("TERMINAL_OUTPUT")
            .Where(output => output.GetProperty("id").GetString() == id)
            .Select(output => output.GetProperty("data").GetString()));

        await UntilAsync(() => Heard().Contains('>'), "the prompt");
        // What the shell makes of the words, not the words as typed: the echo of the typing holds `%OS%`.
        await AnswerAsync(module, "INPUT", new { id, data = "echo round-%OS%-trip home=%DAORIS_HOME%\r" });
        await UntilAsync(() => Heard().Contains("round-Windows_NT-trip"), $"the answer; heard: {Heard()}");
        Assert.Contains($"home={Home}", Heard());

        await AnswerAsync(module, "INPUT", new { id, data = "exit 4\r" });
        await UntilAsync(() => RaisedOf("TERMINAL_EXITED").Count > 0, "the exit");
        Assert.Equal(4, Assert.Single(RaisedOf("TERMINAL_EXITED")).GetProperty("code").GetInt32());
    }
}
