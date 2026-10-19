using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1b2a (D180's HEAVYRUN1b2a note): <c>daoris-driver heavy</c>, the terminal's door to this machine's heavy gates for a
/// repository whose own <c>daoris.gates.json</c> marks none. It reads and writes the driver's file alone, through
/// <see cref="HeavyMapping.Apply"/>, so it refuses what the page's door will, in the same words, with nothing written; and it never
/// touches the repository.
/// </summary>
public sealed class HeavyMappingCommandTests : IDisposable
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "heavy-mapping-command", Guid.NewGuid().ToString("N")[..8]);

    public HeavyMappingCommandTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string ConfigPath => Path.Combine(_folder, "driver.json");

    public static TheoryData<string[], string> AskRows() => new()
    {
        { [], "every say" },
        { ["engine"], "engine say" },
        { [" engine ", "--clear"], "engine clear" },
        { ["engine", "build=dotnet build"], "engine set build=dotnet build" },
        { ["engine", "build=dotnet build", "web=npm test -- --maxWorkers=2", "--cwd", "src/web"], "engine set build=dotnet build | web=npm test -- --maxWorkers=2 @src/web" },
        { ["engine", "e2e=npm run e2e -- --env=ci"], "engine set e2e=npm run e2e -- --env=ci" },
    };

    [Theory]
    [MemberData(nameof(AskRows))]
    public void The_words_name_a_repository_and_its_gates_in_order_or_a_clear_or_none(string[] words, string expected)
    {
        var ask = HeavyMappingCommand.Read(words, out var problem);

        Assert.Null(problem);
        Assert.Equal(expected, Spelled(ask!));
    }

    public static TheoryData<string[], string> ProblemRows() => new()
    {
        { ["--json"], "`--json` is not a repository's name: `heavy` takes a repository's name, then its gates or `--clear`." },
        { ["engine", "--cwd", "web"], "`--cwd` names the folder of the gate before it." },
        { ["engine", "web=npm test", "--cwd"], "`--cwd` takes a folder under the repository's root." },
        { ["engine", "web=npm test", "--cwd", " "], "`--cwd` takes a folder under the repository's root." },
        { ["engine", "web=npm test", "--cwd", "web", "--cwd", "src"], "`web` runs in one folder." },
        { ["engine", "build"], "`build` is not a gate: each is `<name>=<command>`, in the order they run, or `--clear` alone." },
        { ["engine", "--gate=build"], "`--gate=build` is not a gate: each is `<name>=<command>`, in the order they run, or `--clear` alone." },
        { ["engine", "build=x", "--clear"], "one change at a time: the gates, in the order they run, or `--clear` alone." },
    };

    [Theory]
    [MemberData(nameof(ProblemRows))]
    public void Words_it_cannot_read_are_its_usage(string[] words, string problem)
    {
        Assert.Null(HeavyMappingCommand.Read(words, out var said));
        Assert.Equal(problem, said);
    }

    /// <summary>A set writes the driver's file, never the repository, and a say lists the gates in the order they run.</summary>
    [Fact]
    public void A_set_writes_the_driver_s_file_and_a_say_lists_the_gates_in_order()
    {
        Assert.Equal(0, Run(["engine", "build=dotnet build", "web=npm test -- --maxWorkers=2", "--cwd", "src/web"], out var set));
        Assert.Contains("heavy: `engine`'s heavy gates on this machine are now these, run in this order where its own daoris.gates.json marks none:", set);
        Assert.Equal(["build", "web"], HeavyMapping.For(DriverConfig.Load(ConfigPath), "engine")!.Select(gate => gate.Name));
        Assert.Equal("src/web", HeavyMapping.For(DriverConfig.Load(ConfigPath), "engine")![1].WorkingDirectory);

        Assert.Equal(0, Run(["ENGINE"], out var said));
        Assert.Equal(
            """
            heavy: `ENGINE`'s heavy gates on this machine, run in this order where its own daoris.gates.json marks none:
              build  dotnet build
              web    npm test -- --maxWorkers=2  (in src/web)

            """.ReplaceLineEndings("\n"),
            said);

        Assert.Equal(0, Run([], out var every));
        Assert.Equal(
            """
            heavy: the heavy gates set on this machine, each used where its repository's own daoris.gates.json marks none:
              `engine`: build, web

            """.ReplaceLineEndings("\n"),
            every);
    }

    [Fact]
    public void A_clear_and_a_repository_with_none_say_so()
    {
        Assert.Equal(0, Run([], out var nothing));
        Assert.Equal("heavy: no repository's heavy gates are set on this machine; `run-heavy` runs those a repository's own daoris.gates.json marks.\n", nothing);

        Assert.Equal(0, Run(["engine", "build=dotnet build"], out _));
        Assert.Equal(0, Run(["engine", "--clear"], out var cleared));
        Assert.Equal("heavy: `engine` has no heavy gates on this machine now: `run-heavy` runs those its own daoris.gates.json marks.\n", cleared);
        Assert.Empty(DriverConfig.Load(ConfigPath).HeavyGates);

        Assert.Equal(0, Run(["shop"], out var none));
        Assert.Equal("heavy: `shop` has no heavy gates on this machine: `run-heavy` runs those its own daoris.gates.json marks.\n", none);
    }

    /// <summary>A gate the door refuses is said in its words, exit 1, with nothing written.</summary>
    [Fact]
    public void A_gate_the_door_refuses_is_said_in_its_words_and_nothing_is_written()
    {
        Assert.Equal(0, Run(["engine", "build=dotnet build"], out _));
        var before = File.ReadAllText(ConfigPath);

        Assert.Equal(1, Run(["engine", "web=npm test", "--cwd", "../web"], out var said));

        Assert.Equal("heavy: `web`'s folder, `../web`, is not under the repository's root: name it from there, with no `..`.", said.Trim());
        Assert.Equal(before, File.ReadAllText(ConfigPath));
    }

    /// <summary>The host answers the verb, its usage names it, and only a change holds the home's lease.</summary>
    [Fact]
    public void The_host_routes_the_verb_and_its_usage_names_it()
    {
        Assert.Contains("\n  heavy [<repository> [<name>=<command> [--cwd <folder>]… | --clear]]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.Equal("usage: daoris-driver heavy [<repository> [<name>=<command> [--cwd <folder>]… | --clear]]", HeavyMappingCommand.Usage);

        var program = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"heavy\", .. var mappingArgs])", program);
        Assert.Contains("Daoris.Driver.Host.HeavyMappingConsole.Run(mappingArgs)", program);
        Assert.Contains("//   heavy [<repository> [<name>=<command> [--cwd <folder>]… | --clear]]", program);
    }

    private int Run(string[] words, out string said)
    {
        using var output = new StringWriter();
        var exit = HeavyMappingCommand.Run(HeavyMappingCommand.Read(words, out _)!, ConfigPath, output);
        said = output.ToString().ReplaceLineEndings("\n");
        return exit;
    }

    private static string Spelled(HeavyMappingAsk ask)
    {
        var who = ask.Repository ?? "every";
        if (ask.Clear) return $"{who} clear";
        if (ask.Gates is not { } gates) return $"{who} say";
        return $"{who} set " + string.Join(" | ", gates.Select(gate => $"{gate.Name}={gate.Run}{(gate.WorkingDirectory is { } cwd ? $" @{cwd}" : "")}"));
    }
}
