using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>daoris-driver driver</c>, the standing choices <c>daoris driver …</c> hands on (PUSH1f2a, D194 points 1, 2 and 5; the alone-path
/// design §1.2 and §2): a change goes to the running Daoris's route where one answers, confirmed in its window whatever the
/// terminal's environment says; with none it is the verb's own act, on the terminal's word, refused from a session's terminal; a file
/// <c>DAORIS_DRIVER_CONFIG</c> names is written alone and never sent; a read is never refused; and a verb it does not have is its usage.
/// </summary>
public sealed class DriverChoicesCommandTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driver-choices-command", Guid.NewGuid().ToString("N")[..8]);
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public DriverChoicesCommandTests() => Directory.CreateDirectory(_home);

    private string Config => DaorisHome.Entry(_home, HomeEntry.DriverConfig);

    /// <summary>A terminal on this test's home with the variables named, where no Daoris runs (a workspace build reads no address file).</summary>
    private DriverChoicesWorld World(params (string Name, string Value)[] variables) => new()
    {
        Instance = new InstanceWorld
        {
            Home = _home,
            Environment = name => variables.FirstOrDefault(each => each.Name == name).Value,
            Out = _out,
            Error = _error,
            Verb = "daoris",
        },
        ConfigPath = Config,
    };

    private static readonly string Refused =
        "daoris: this change is the person's, and this terminal is a session's (`DAORIS_SESSION_ID`): make it in Daoris's window, through "
        + "Ask Daoris's proposal and its Apply, or from your own terminal; nothing was done.";

    [Theory]
    [InlineData("drive engine")]
    [InlineData("cap 3")]
    [InlineData("timeout 90")]
    [InlineData("retry q7 --at 2")]
    public async Task A_persons_press_acting_here_from_a_sessions_terminal_is_refused_and_nothing_is_written(string words)
    {
        var exit = await DriverChoicesCommand.RunAsync(words.Split(' '), World(("DAORIS_SESSION_ID", "s1a2b3c4")), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal(Refused, _error.ToString().TrimEnd());
        Assert.Equal("", _out.ToString());
        Assert.False(File.Exists(Config));
    }

    [Fact]
    public async Task A_named_file_from_a_sessions_terminal_is_refused_as_well()
    {
        var exit = await DriverChoicesCommand.RunAsync(["hold", "engine"],
            World(("DAORIS_SESSION_ID", "s1a2b3c4"), (DriverConfig.PathVariable, Config)), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal(Refused, _error.ToString().TrimEnd());
        Assert.False(File.Exists(Config));
    }

    [Fact]
    public async Task A_read_from_a_sessions_terminal_is_said()
    {
        var exit = await DriverChoicesCommand.RunAsync(["list"], World(("DAORIS_SESSION_ID", "s1a2b3c4")), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.StartsWith($"daoris: {Config}", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public async Task With_no_marker_a_change_here_is_made_on_the_terminals_word()
    {
        var exit = await DriverChoicesCommand.RunAsync(["drive", "engine"], World(), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(["engine"], DriverConfig.Load(Config).Drivable);
    }

    /// <summary>
    /// *Try again* alone judges from the planner's verdict over the service's snapshot (D194's PUSH1f2a note): where the service cannot
    /// be read, nothing is guessed and nothing is written, and the words say how to give the count, as the CLI's did.
    /// </summary>
    [Fact]
    public async Task A_retry_that_cannot_read_the_verdicts_writes_nothing_and_says_how_to_give_the_count()
    {
        var world = World() with { Verdicts = (_, _) => throw new HttpRequestException("No connection could be made") };

        var exit = await DriverChoicesCommand.RunAsync(["retry", "q7"], world, CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.StartsWith("daoris: cannot count the failures of `#q7`: this machine's session records could not be read — No connection could be made.",
            _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("`daoris driver retry q7 --at <n>`", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Config));
    }

    /// <summary>A quest the verdicts neither parked nor held is refused alone as the window refuses it, in the catalogue's code.</summary>
    [Fact]
    public async Task A_retry_of_a_quest_neither_parked_nor_held_is_refused_alone_in_its_code()
    {
        var quest = new QuestView("q7", "game", "engine", "A title", "A body.", "Open");
        var world = World() with { Verdicts = (_, _) => Task.FromResult<IReadOnlyList<Consideration>>([new Consideration(quest, StartVerdict.Start, "it may start")]) };

        var exit = await DriverChoicesCommand.RunAsync(["retry", "q7"], world, CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Equal("daoris: #q7 is neither parked nor held by your stop on this machine, so there is nothing to try again. (QUEST_NOT_HELD)",
            _error.ToString().TrimEnd());
        Assert.False(File.Exists(Config));
    }

    [Theory]
    [InlineData("frobnicate")]
    [InlineData("workflows list")]
    public async Task A_verb_it_does_not_have_is_its_usage_and_does_nothing(string words)
    {
        var exit = await DriverChoicesCommand.RunAsync(words.Split(' '), World(), CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Contains(DriverChoicesCommand.Usage, _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Config));
    }

    /// <summary>
    /// PUSH1f2b: the family's last verbs are this program's, and the CLI hands it every word. Each the CLI said once is a verb here,
    /// and its usage names each of them.
    /// </summary>
    [Fact]
    public void The_family_is_this_programs_whole_and_its_usage_names_each_verb()
    {
        Assert.Equal(
            ["list", "drive", "undrive", "hold", "resume", "trees", "notify", "cap", "strikes", "retry", "timeout", "cooloff", "adapter", "helper",
                "intake", "line", "landing", "across", "standing", "language", "review", "opinion", "helpers", "handover", "commits", "work-cycle",
                "brainstorms", "workflow", "keep"],
            DriverChoicesCommand.Verbs);
        Assert.All(DriverChoicesCommand.Verbs, verb =>
            Assert.Matches($"(?<![\\w-]){System.Text.RegularExpressions.Regex.Escape(verb)}(?![\\w-])", DriverChoicesCommand.Usage));
        Assert.Contains("--cycle <file>|--no-cycle", DriverChoicesCommand.Usage, StringComparison.Ordinal);
        Assert.All(DriverWorkflowsCommand.Verbs, verb => Assert.Contains(verb, DriverChoicesCommand.Usage, StringComparison.Ordinal));
        Assert.DoesNotContain("still", DriverChoicesCommand.Usage, StringComparison.Ordinal);
    }

    /// <summary>
    /// A change no route takes acts here on the terminal's word (D171's PUSH1f note, kept by D194 point 5), refused from a session's
    /// terminal before anything is read or written: a landing's cycle, a work cycle, the brainstorm policy and a workflow's write.
    /// </summary>
    [Theory]
    [InlineData("landing engine merge --no-cycle")]
    [InlineData("work-cycle engine quest --clear")]
    [InlineData("brainstorms --mode off")]
    [InlineData("workflow new docs --from merge-after-accept")]
    [InlineData("workflow kind work docs --label Documentation")]
    public async Task A_change_no_route_takes_from_a_sessions_terminal_is_refused_and_nothing_is_written(string words)
    {
        var exit = await DriverChoicesCommand.RunAsync(words.Split(' '), World(("DAORIS_SESSION_ID", "s1a2b3c4")), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Equal(Refused, _error.ToString().TrimEnd());
        Assert.Equal("", _out.ToString());
        Assert.False(File.Exists(Config));
        Assert.False(Directory.Exists(WorkflowStore.FolderOf(_home)));
    }

    /// <summary>A read, and a workflow's write with <c>--plan</c>, are said from a session's terminal: they change nothing.</summary>
    [Theory]
    [InlineData("brainstorms")]
    [InlineData("workflow list")]
    [InlineData("workflow new docs --from merge-after-accept --plan")]
    public async Task A_read_and_a_plan_from_a_sessions_terminal_are_said(string words)
    {
        var exit = await DriverChoicesCommand.RunAsync(words.Split(' '), World(("DAORIS_SESSION_ID", "s1a2b3c4")), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.StartsWith("daoris: ", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal("", _error.ToString());
        Assert.False(File.Exists(Config));
        Assert.False(Directory.Exists(WorkflowStore.FolderOf(_home)));
    }

    /// <summary>
    /// Beside the running Daoris a change no route takes is written here, with nothing sent (PUSH1f2b, D194 point 5): a landing's cycle,
    /// whose rule SET_LANDING would take, sends nothing, since the route has no field for the cycle.
    /// </summary>
    [Theory]
    [InlineData("landing engine merge --no-cycle")]
    [InlineData("brainstorms --mode off")]
    [InlineData("workflow kind work docs --label Documentation")]
    public async Task Beside_the_running_Daoris_a_change_no_route_takes_is_written_here_and_nothing_is_sent(string words)
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        var instance = running.World("daoris");
        var config = DaorisHome.Entry(running.Home, HomeEntry.DriverConfig);
        var world = new DriverChoicesWorld
        {
            Instance = new InstanceWorld
            {
                Home = running.Home,
                Environment = _ => null,
                BaseDirectory = instance.BaseDirectory,
                Status = instance.Status,
                Out = running.Out,
                Error = running.Error,
                Verb = "daoris",
                Answer = instance.Answer,
                Wait = instance.Wait,
            },
            ConfigPath = config,
        };

        var exit = await DriverChoicesCommand.RunAsync(words.Split(' '), world, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(config), running.Error.ToString());
        Assert.Empty(standIn.Presses);
        Assert.Equal(0, standIn.Connections);
    }

    [Fact]
    public async Task Asked_for_its_usage_it_says_it_and_exits_0()
    {
        var exit = await DriverChoicesCommand.RunAsync(["--help"], World(), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains(DriverChoicesCommand.Usage, _out.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A file `DAORIS_DRIVER_CONFIG` names is no instance's (D194 point 2): beside a running Daoris on this home, its change is written
    /// to that file alone, and nothing is sent.
    /// </summary>
    [Fact]
    public async Task A_named_file_is_written_alone_and_nothing_is_sent()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        var instance = running.World("daoris");
        var world = new DriverChoicesWorld
        {
            Instance = new InstanceWorld
            {
                Home = running.Home,
                Environment = name => name == DriverConfig.PathVariable ? Config : null,
                BaseDirectory = instance.BaseDirectory,
                Status = instance.Status,
                Out = _out,
                Error = _error,
                Verb = "daoris",
            },
            ConfigPath = Config,
        };

        var exit = await DriverChoicesCommand.RunAsync(["hold", "engine"], world, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(["engine"], DriverConfig.Load(Config).Holds);
        Assert.Empty(standIn.Presses);
        Assert.Equal(0, standIn.Connections);
    }

    /// <summary>
    /// Beside the running Daoris a person's press is its route, confirmed in its window (RPC1c), whatever the terminal's environment
    /// says (D194 point 5: the marker refuses, its absence grants nothing, and the window's Confirm is the person's): sent with its
    /// payload, said in the verb's words from the answer, and this process writes nothing.
    /// </summary>
    [Fact]
    public async Task Beside_the_running_Daoris_a_press_is_its_route_whatever_the_marker_says()
    {
        if (!OperatingSystem.IsWindows()) return;
        await using var running = new InstanceTestHome();
        var standIn = running.Serve();
        standIn.Confirming = true;
        standIn.Data = new { drivable = new[] { "engine" } };
        var instance = running.World("daoris");
        var config = DaorisHome.Entry(running.Home, HomeEntry.DriverConfig);
        var world = new DriverChoicesWorld
        {
            Instance = new InstanceWorld
            {
                Home = running.Home,
                Environment = name => name == "DAORIS_SESSION_ID" ? "s1a2b3c4" : null,
                BaseDirectory = instance.BaseDirectory,
                Status = instance.Status,
                Out = running.Out,
                Error = running.Error,
                Verb = "daoris",
                Answer = instance.Answer,
                Wait = instance.Wait,
            },
            ConfigPath = config,
        };

        var exit = await DriverChoicesCommand.RunAsync(["drive", "engine"], world, CancellationToken.None);

        Assert.Equal(0, exit);
        var press = Assert.Single(standIn.Presses);
        Assert.Equal("SET_DRIVABLE", press.GetProperty("type").GetString());
        Assert.Equal("engine", press.GetProperty("payload").GetProperty("repository").GetString());
        Assert.True(press.GetProperty("payload").GetProperty("drivable").GetBoolean());
        Assert.StartsWith("daoris: this machine may drive `engine`.", running.Out.ToString(), StringComparison.Ordinal);
        Assert.Contains("daoris: waiting for you to confirm this in Daoris's window…", running.Error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(config));
    }

    /// <summary>
    /// The host routes the family and takes Ctrl+C in one handler for it (D194 point 1, the alone-path design §1.1): registered where the
    /// family is routed, after `closing` is made and after every verb routed above it, keeping the process and cancelling `closing`, whose
    /// token the family's command is handed; and its lease is the named file's to forgo (<see cref="VerbLeases.HoldsHere"/>).
    /// </summary>
    [Fact]
    public void The_host_routes_the_family_with_its_one_Ctrl_C_handler_and_the_named_files_lease()
    {
        var program = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"))
            .ReplaceLineEndings("\n");
        var closing = program.IndexOf("using var closing = new CancellationTokenSource();", StringComparison.Ordinal);
        // PUSH1f2c: `tool` is routed beside `driver`, under the same one handler; PUSH1f2d: and `agent`.
        var family = program.IndexOf("if (args is [\"driver\" or \"tool\" or \"agent\", ..])", StringComparison.Ordinal);
        var tools = program.IndexOf("ToolsConsole.RunAsync(args[1..], log, closing.Token)", StringComparison.Ordinal);
        var agents = program.IndexOf("AgentsConsole.RunAsync(args[1..], closing.Token)", StringComparison.Ordinal);
        var handed = program.IndexOf("DriverChoicesConsole.RunAsync(args[1..], closing.Token)", StringComparison.Ordinal);
        Assert.True(closing > 0 && family > closing && tools > family && agents > family && handed > family,
            "the families are routed after `closing` is made, and handed its token");
        var between = program[family..new[] { tools, agents, handed }.Min()];
        Assert.Contains("Console.CancelKeyPress += (_, press) =>", between, StringComparison.Ordinal);
        Assert.Contains("press.Cancel = true;", between, StringComparison.Ordinal);
        Assert.Contains("closing.Cancel();", between, StringComparison.Ordinal);
        // Every verb routed above the family keeps the runtime's own Ctrl+C: no handler is registered before it but the loop's, below.
        Assert.DoesNotContain("CancelKeyPress", program[..family], StringComparison.Ordinal);
        Assert.Contains("VerbLeases.HoldsHere(args, Environment.GetEnvironmentVariable)", program, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }
}
