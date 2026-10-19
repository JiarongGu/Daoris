using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// This machine's heavy gates for a repository over the bridge (HEAVYRUN1b2b, D180's HEAVYRUN1b2a note): <c>SET_HEAVY_GATES</c>, the
/// setup page's half of <c>daoris-driver heavy</c>, over the same key in the same file (D50), a repository's whole list at once; the
/// state answering each repository's list as rows, in its order, so no key policy on the bridge respells a repository's name; a list
/// the mapping does not read refused in <see cref="HeavyMapping"/>'s words with nothing written. Edits to <c>driver.json</c> only.
/// </summary>
public sealed class DriverModuleHeavyGatesTests : DriverModuleBridge
{
    private static object Gate(string name, string run, string? cwd = null) => new { name, run, cwd };

    /// <summary>The list is kept whole in the order given, each gate trimmed, and the state answers it as a row.</summary>
    [Fact]
    public async Task A_list_is_kept_whole_in_its_order_and_the_state_answers_it_as_rows()
    {
        var module = Module();
        var empty = await AnswerAsync(module, "STATE");
        Assert.Empty(empty.GetProperty("heavyGates").EnumerateArray());

        var state = await AnswerAsync(module, "SET_HEAVY_GATES", new
        {
            repository = " Work-App ",
            gates = new[] { Gate("web", "npm test -- --maxWorkers=2", "src/web"), Gate(" build ", " dotnet build ", " "), Gate("e2e", "npm run e2e") },
        });

        var row = Assert.Single(state.GetProperty("heavyGates").EnumerateArray());
        Assert.Equal("Work-App", row.GetProperty("repository").GetString());
        var gates = row.GetProperty("gates").EnumerateArray().ToList();
        Assert.Equal(["web", "build", "e2e"], gates.Select(gate => gate.GetProperty("name").GetString()));
        Assert.Equal(["npm test -- --maxWorkers=2", "dotnet build", "npm run e2e"], gates.Select(gate => gate.GetProperty("run").GetString()));
        Assert.Equal("src/web", gates[0].GetProperty("cwd").GetString());
        // A blank folder is none, and none is the checkout itself: left out, or null, never an empty folder.
        Assert.All(gates.Skip(1), gate => Assert.True(!gate.TryGetProperty("cwd", out var cwd) || cwd.ValueKind == JsonValueKind.Null));

        var kept = HeavyMapping.For(DriverConfig.Load(DriverConfigPath), "work-app");
        Assert.Equal(
            [new HeavyGate("web", "npm test -- --maxWorkers=2", "src/web", true), new HeavyGate("build", "dotnet build", null, true), new HeavyGate("e2e", "npm run e2e", null, true)],
            kept);
    }

    /// <summary>A second list replaces the first whole under the spelling first written; none, an empty list or null clears it.</summary>
    [Fact]
    public async Task A_list_replaces_the_one_before_under_its_first_spelling_and_none_clears_it()
    {
        var module = Module();
        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "Engine", gates = new[] { Gate("build", "dotnet build"), Gate("unit", "dotnet test") } });
        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "shop", gates = new[] { Gate("web", "npm test") } });

        var replaced = await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "ENGINE", gates = new[] { Gate("unit", "dotnet test"), Gate("build", "dotnet build") } });

        var rows = replaced.GetProperty("heavyGates").EnumerateArray().ToList();
        Assert.Equal(["Engine", "shop"], rows.Select(row => row.GetProperty("repository").GetString()));
        Assert.Equal(["unit", "build"], rows[0].GetProperty("gates").EnumerateArray().Select(gate => gate.GetProperty("name").GetString()));

        var cleared = await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = (object?)null });
        Assert.Equal(["shop"], cleared.GetProperty("heavyGates").EnumerateArray().Select(row => row.GetProperty("repository").GetString()));
        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "shop", gates = Array.Empty<object>() });
        var none = await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "Engine" });
        Assert.Empty(none.GetProperty("heavyGates").EnumerateArray());
        Assert.Empty(DriverConfig.Load(DriverConfigPath).HeavyGates);
    }

    /// <summary>
    /// A list the mapping would not read is refused whole, in the words every door says (<see cref="HeavyMapping.Problem"/>), and the
    /// file is as it was: no name, no command, a name twice in any case, a folder not under the repository's root, no repository.
    /// </summary>
    [Fact]
    public async Task A_list_it_would_not_read_is_refused_in_the_mappings_words_and_nothing_is_written()
    {
        var module = Module();
        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("build", "dotnet build") } });
        var before = File.ReadAllText(DriverConfigPath);

        var unnamed = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("build", "dotnet build"), Gate(" ", "npm test") } });
        var noCommand = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("web", " ") } });
        var twice = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("web", "npm test"), Gate("WEB", "npm run e2e") } });
        var outside = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("web", "npm test", "src/../..") } });
        var noRepository = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = " ", gates = new[] { Gate("web", "npm test") } });
        var notAList = await RefusalAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = "npm test" });

        Assert.Contains(HeavyMapping.Problem([new HeavyGate("", "npm test", null, true)])!, unnamed);
        Assert.Contains(HeavyMapping.Problem([new HeavyGate("web", "", null, true)])!, noCommand);
        Assert.Contains("`WEB` is named twice", twice);
        Assert.Contains("is not under the repository's root", outside);
        Assert.Contains("name one", noRepository);
        Assert.Contains("a list of gates", notAList);
        Assert.Equal(before, File.ReadAllText(DriverConfigPath));
    }

    /// <summary>🔴 The route and the terminal agree (D50): what one sets the other reads, through the same key.</summary>
    [Fact]
    public async Task The_route_and_the_terminal_set_the_same_key()
    {
        var module = Module();
        string Terminal(params string[] args)
        {
            var output = new StringWriter();
            Assert.Equal(0, HeavyMappingCommand.Run(HeavyMappingCommand.Read(args, out _)!, DriverConfigPath, output));
            return output.ToString();
        }

        Terminal("shop", "web=npm test -- --maxWorkers=2", "--cwd", "web");
        var state = await AnswerAsync(module, "STATE");
        var row = Assert.Single(state.GetProperty("heavyGates").EnumerateArray());
        Assert.Equal("shop", row.GetProperty("repository").GetString());
        Assert.Equal("web", row.GetProperty("gates")[0].GetProperty("cwd").GetString());

        await AnswerAsync(module, "SET_HEAVY_GATES", new { repository = "engine", gates = new[] { Gate("build", "dotnet build -nodeReuse:false") } });
        Assert.Contains("dotnet build -nodeReuse:false", Terminal("engine"));
    }

    [Fact]
    public async Task A_machine_that_kept_none_answers_none()
    {
        var state = await AnswerAsync(Module(), "STATE");

        Assert.Equal(0, state.GetProperty("heavyGates").GetArrayLength());
    }
}
