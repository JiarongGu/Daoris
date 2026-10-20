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

    [Theory]
    [InlineData("workflow list")]
    [InlineData("work-cycle engine quest cycle.json")]
    [InlineData("brainstorms")]
    [InlineData("frobnicate")]
    public async Task A_verb_it_does_not_have_is_its_usage_and_does_nothing(string words)
    {
        var exit = await DriverChoicesCommand.RunAsync(words.Split(' '), World(), CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Contains(DriverChoicesCommand.Usage, _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Config));
    }

    [Theory]
    [InlineData("landing engine merge --cycle cycle.json")]
    [InlineData("landing engine merge --no-cycle")]
    public async Task A_landing_cycle_is_the_CLIs_and_is_refused_here(string words)
    {
        var exit = await DriverChoicesCommand.RunAsync(words.Split(' '), World(), CancellationToken.None);

        Assert.Equal(2, exit);
        Assert.Contains("`daoris driver landing … --cycle", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Config));
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
        var family = program.IndexOf("if (args is [\"driver\", .. var choicesArgs])", StringComparison.Ordinal);
        var handed = program.IndexOf("DriverChoicesConsole.RunAsync(choicesArgs, closing.Token)", StringComparison.Ordinal);
        Assert.True(closing > 0 && family > closing && handed > family, "the family is routed after `closing` is made, and handed its token");
        var between = program[family..handed];
        Assert.Contains("Console.CancelKeyPress += (_, press) =>", between, StringComparison.Ordinal);
        Assert.Contains("press.Cancel = true;", between, StringComparison.Ordinal);
        Assert.Contains("closing.Cancel();", between, StringComparison.Ordinal);
        // Every verb routed above the family keeps the runtime's own Ctrl+C: no handler is registered before it but the loop's, below.
        Assert.DoesNotContain("CancelKeyPress", program[..family], StringComparison.Ordinal);
        Assert.Contains("VerbLeases.HoldsHere(args, Environment.GetEnvironmentVariable)", program, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }
}
