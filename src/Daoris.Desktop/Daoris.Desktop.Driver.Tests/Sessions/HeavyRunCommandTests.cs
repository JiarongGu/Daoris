using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1a (D180's HEAVYRUN1 note): <c>daoris-driver run-heavy [--gate &lt;name&gt;]</c>, run in a checkout, runs the gates its
/// <c>daoris.gates.json</c> marks <c>"heavy": true</c>, in declared order, or the one named. Its words, its choice of gates and its
/// refusals start no process, so they are the fast half's; the runs themselves are <see cref="HeavyRunProcessTests"/>'.
/// </summary>
public sealed class HeavyRunCommandTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("heavy-run-command");

    public void Dispose() => _fixture.Dispose();

    public static TheoryData<string, string?> AskRows() => new()
    {
        { "", null },
        { "--gate web", "web" },
    };

    [Theory]
    [MemberData(nameof(AskRows))]
    public void The_words_name_every_heavy_gate_or_one(string words, string? gate)
    {
        var ask = HeavyRun.Read(Words(words), out var problem);

        Assert.Null(problem);
        Assert.Equal(gate, ask?.Gate);
    }

    [Theory]
    [InlineData("--gate", "`--gate` takes a gate's name.")]
    [InlineData("--gate --gate", "`--gate` takes a gate's name.")]
    [InlineData("web", "`web` is not a word `run-heavy` takes: it takes `--gate <name>`, or nothing for every heavy gate.")]
    [InlineData("--gate web --gate e2e", "`run-heavy` runs one named gate, or every heavy one.")]
    public void Words_it_does_not_take_are_said(string words, string said)
    {
        Assert.Null(HeavyRun.Read(Words(words), out var problem));
        Assert.Equal(said, problem);
    }

    /// <summary>The heavy gates in the order the repository declares them; one named alone; a gate not marked heavy is no heavy gate.</summary>
    [Fact]
    public void The_heavy_gates_are_taken_in_declared_order_and_a_named_one_alone()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("build", "dotnet build", true), ("unit", "npm test", false), ("e2e", "npm run e2e", true));
        var declared = HeavyGates.Read(_fixture.Checkout);

        var every = HeavyGates.Choose(declared, null);
        Assert.Equal((0, (string?)null), (every.Exit, every.Refusal));
        Assert.Equal(["build", "e2e"], every.Gates.Select(gate => gate.Name));
        Assert.Equal("dotnet build", every.Gates[0].Run);

        var one = HeavyGates.Choose(declared, "e2e");
        Assert.Equal(["e2e"], one.Gates.Select(gate => gate.Name));
    }

    /// <summary>A gate named that is not marked heavy is refused, saying so: the slot is for the heavy ones.</summary>
    [Fact]
    public void A_named_gate_not_marked_heavy_is_refused_saying_so()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("build", "dotnet build", true));

        var refused = HeavyGates.Choose(HeavyGates.Read(_fixture.Checkout), "lint");

        Assert.Empty(refused.Gates);
        Assert.Equal(1, refused.Exit);
        Assert.Equal(
            "`lint` is not marked heavy in daoris.gates.json, so `run-heavy` does not run it: run it as you would, or mark it `\"heavy\": true`. Its heavy gates: build.",
            refused.Refusal);
    }

    /// <summary>A gate nobody declared, or a file that declares no heavy gate, is a tool error: there is nothing to run.</summary>
    [Fact]
    public void An_unknown_gate_or_no_heavy_gate_is_a_tool_error()
    {
        _fixture.Gates(_fixture.Checkout, ("lint", "npm run lint", null), ("unit", "npm test", false));
        var declared = HeavyGates.Read(_fixture.Checkout);

        var unknown = HeavyGates.Choose(declared, "nope");
        Assert.Equal(2, unknown.Exit);
        Assert.Equal("daoris.gates.json declares no gate `nope`; its gates: lint, unit.", unknown.Refusal);

        var none = HeavyGates.Choose(declared, null);
        Assert.Equal(2, none.Exit);
        Assert.Equal("daoris.gates.json marks no gate `\"heavy\": true`, so there is nothing for `run-heavy` to run.", none.Refusal);
    }

    /// <summary>The file is the contract, and a reading that cannot tell is a tool error, never a guess.</summary>
    [Fact]
    public void A_missing_file_or_one_that_does_not_read_is_a_tool_error_naming_why()
    {
        Assert.Contains("no daoris.gates.json", Assert.Throws<DriverException>(() => HeavyGates.Read(_fixture.Checkout)).Message);

        File.WriteAllText(Path.Combine(_fixture.Checkout, "daoris.gates.json"), "{ \"gates\": [ ");
        Assert.Contains("daoris.gates.json is not valid JSON", Assert.Throws<DriverException>(() => HeavyGates.Read(_fixture.Checkout)).Message);

        File.WriteAllText(Path.Combine(_fixture.Checkout, "daoris.gates.json"), """{ "gates": [ { "name": "build" } ] }""");
        Assert.Contains("needs both 'name' and 'run'", Assert.Throws<DriverException>(() => HeavyGates.Read(_fixture.Checkout)).Message);

        File.WriteAllText(Path.Combine(_fixture.Checkout, "daoris.gates.json"), """{ "gates": [ { "name": "build", "run": "x", "heavy": "yes" } ] }""");
        Assert.Contains("`build`'s \"heavy\" is true or false", Assert.Throws<DriverException>(() => HeavyGates.Read(_fixture.Checkout)).Message);
    }

    /// <summary>A gate's folder is the repository's, where it names one: its <c>cwd</c>, as the devkit reads it.</summary>
    [Fact]
    public void A_gate_s_folder_is_its_cwd_under_the_checkout()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Checkout, "daoris.gates.json"),
            """{ "gates": [ { "name": "web", "run": "npm test", "cwd": "src/web", "heavy": true }, { "name": "build", "run": "x", "heavy": true } ] }""");

        var gates = HeavyGates.Read(_fixture.Checkout);

        Assert.Equal("src/web", gates[0].WorkingDirectory);
        Assert.Null(gates[1].WorkingDirectory);
    }

    /// <summary>A run that finds nothing to run says why and exits 2, holding nothing.</summary>
    [Fact]
    public async Task A_run_with_nothing_to_run_says_why_and_holds_nothing()
    {
        // A tree reads its own copy of the file, as it reads its own copy of every other.
        _fixture.Gates(_fixture.Tree, ("lint", "npm run lint", null));
        var said = new SaidWriter();

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Tree), said, CancellationToken.None).WaitAsync(Poll.Patience);

        Assert.Equal(2, exit);
        Assert.Contains("run-heavy: daoris.gates.json marks no gate", said.Text);
        using var free = HeavySlot.TryTake(_fixture.Home, HeavySlot.KeyOf(_fixture.Checkout)!);
        Assert.NotNull(free);
    }

    /// <summary>
    /// 🔴 Never waits on itself. A heavy gate whose command runs <c>run-heavy</c> again is inside the run that holds the slot: its
    /// environment says so. Asked again for a gate already running in that run, it would run inside itself without end, so it is
    /// refused, saying so, at once, while the slot stays held by the run it is inside.
    /// </summary>
    [Fact]
    public async Task A_run_inside_a_run_already_running_its_gate_is_refused_at_once_and_never_waits_on_the_slot()
    {
        _fixture.Gates(_fixture.Tree, ("build", "dotnet build", true), ("e2e", "npm run e2e", true));
        var key = HeavySlot.KeyOf(_fixture.Tree)!;
        using var outer = HeavySlot.TryTake(_fixture.Home, key)!;
        var said = new SaidWriter();
        var marker = HeavyRun.Marker(key, ["build"]);

        var exit = await HeavyRun.RunAsync(new HeavyRunAsk(null), World(_fixture.Tree, (HeavyRun.Variable, marker)), said, CancellationToken.None)
            .WaitAsync(Poll.Patience);

        Assert.Equal(1, exit);
        Assert.Equal(
            "run-heavy: `build` is already running in the heavy run this one is inside, so it is not run again inside itself.",
            Assert.Single(said.Lines));
    }

    /// <summary>The marker a run hands its gates names its slot and the gates running, outermost first, and reads back as written.</summary>
    [Fact]
    public void The_marker_names_the_slot_and_the_gates_running_and_reads_back()
    {
        var marker = HeavyRun.ReadMarker(HeavyRun.Marker("0123abcd0123abcd", ["build", "web"]));

        Assert.Equal("0123abcd0123abcd", marker?.Slot);
        Assert.Equal(["build", "web"], marker?.Gates);
        Assert.Null(HeavyRun.ReadMarker(null));
        Assert.Null(HeavyRun.ReadMarker("not a marker"));
    }

    /// <summary>The host answers the verb, its usage names it, and it holds no home lease for the minutes it runs.</summary>
    [Fact]
    public void The_host_routes_the_verb_and_its_usage_names_it()
    {
        Assert.Contains("\n  run-heavy [--gate <name>]\n", DriverCommand.Usage.ReplaceLineEndings("\n"));
        Assert.Equal("usage: daoris-driver run-heavy [--gate <name>]", HeavyRun.Usage);

        var program = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Host", "Program.cs"));
        Assert.Contains("if (args is [\"run-heavy\", .. var heavyArgs])", program);
        Assert.Contains("Daoris.Driver.Host.HeavyConsole.RunAsync(heavyArgs)", program);
        Assert.Contains("//   run-heavy [--gate <name>]", program);
    }

    private HeavyRunWorld World(string directory, params (string Name, string Value)[] variables) =>
        new(_fixture.Home, directory, name => variables.FirstOrDefault(variable => variable.Name == name).Value);

    private static string[] Words(string words) => words.Length == 0 ? [] : words.Split(' ');
}
