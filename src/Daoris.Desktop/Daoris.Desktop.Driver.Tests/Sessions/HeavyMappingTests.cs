using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1b2a (D180's HEAVYRUN1b2a note): this machine's heavy gates for a repository that marks none of its own, <c>heavyGates</c>
/// in <c>driver.json</c>, by repository, each a list of gates in the order they run. Read as a stored shape, held cell for cell to ONE
/// table, <c>fixtures/heavy-gates.json</c> (the CLI's twin left in PUSH1f2b); written only when set; and edited
/// through one door (<see cref="HeavyMapping.Apply"/>) the terminal's verb and the page's go through, refusing alike. Reads text only.
/// </summary>
public sealed class HeavyMappingTests
{
    public static TheoryData<string, string, string> ReadRows()
    {
        var data = new TheoryData<string, string, string>();
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "heavy-gates.json")));
        foreach (var row in document.RootElement.GetProperty("read").EnumerateArray())
        {
            data.Add(row.GetProperty("name").GetString()!, row.GetProperty("heavyGates").GetRawText(), row.GetProperty("read").GetRawText());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ReadRows))]
    public void The_mapping_reads_as_the_shared_table_says(string name, string value, string read)
    {
        var config = DriverConfig.Parse($$"""{"heavyGates":{{value}}}""");

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(read), Mapped(config)), $"{name}: read {Mapped(config)?.ToJsonString()}");
        // In the order the file holds them, which DeepEquals does not compare for an object's keys.
        Assert.Equal(JsonNode.Parse(read)!.AsObject().Select(pair => pair.Key), config.HeavyGates.Keys);
        Assert.All(config.HeavyGates.Values.SelectMany(gates => gates), gate => Assert.True(gate.Heavy));
    }

    /// <summary>A repository's gates are found in any case, its name trimmed; one with none set has none.</summary>
    [Fact]
    public void A_repository_s_gates_are_found_by_its_name_in_any_case()
    {
        var config = DriverConfig.Parse("""{"heavyGates":{"Engine":[{"name":"build","run":"dotnet build"}]}}""");

        Assert.Equal(["build"], HeavyMapping.For(config, " ENGINE ")!.Select(gate => gate.Name));
        Assert.Null(HeavyMapping.For(config, "shop"));
    }

    /// <summary>
    /// Written only when set, after the access setup, in the order they run: a gate's name, its command, and its folder only where it
    /// has one; repositories sorted ordinally, as every map of the file is.
    /// </summary>
    [Fact]
    public void The_mapping_is_written_only_when_set_each_gate_its_name_command_and_folder()
    {
        var none = JsonNode.Parse(DriverConfig.Empty.ToJson())!.AsObject();
        Assert.False(none.ContainsKey("heavyGates"));

        var set = DriverConfig.Empty
            .WithAccess(new AccessEdit { Machine = true, Full = true })
            .WithHeavyGates("shop", [Gate("web", "npm test -- --maxWorkers=2", "web"), Gate("build", "npm run build")])
            .WithHeavyGates("engine", [Gate("build", "dotnet build")]);
        var written = set.ToJson().ReplaceLineEndings("\n");

        Assert.Contains(
            """
              "heavyGates": {
                "engine": [
                  {
                    "name": "build",
                    "run": "dotnet build"
                  }
                ],
                "shop": [
                  {
                    "name": "web",
                    "run": "npm test -- --maxWorkers=2",
                    "cwd": "web"
                  },
                  {
                    "name": "build",
                    "run": "npm run build"
                  }
                ]
              },
            """.ReplaceLineEndings("\n"),
            written);
        var keys = JsonNode.Parse(written)!.AsObject().Select(pair => pair.Key).ToList();
        Assert.Equal(keys.IndexOf("heavyGates"), keys.IndexOf("machineAccess") + 1);
    }

    /// <summary>
    /// The door: a set replaces the repository's list whole, in the order given, read as the file is read (trimmed, a blank folder
    /// none); a key keeps the spelling first written; no gates, or a clear, takes the repository's list away.
    /// </summary>
    [Fact]
    public void A_set_replaces_the_list_whole_keeping_the_first_spelling_and_a_clear_takes_it_away()
    {
        var first = DriverConfig.Empty.WithHeavyGates(" Engine ", [Gate(" build ", " dotnet build ", " "), Gate("e2e", "npm run e2e", " web ")]);
        Assert.Equal("""{"Engine":[{"name":"build","run":"dotnet build"},{"name":"e2e","run":"npm run e2e","cwd":"web"}]}""", Mapped(first)!.ToJsonString());

        var again = first.WithHeavyGates("ENGINE", [Gate("unit", "npm test")]);
        Assert.Equal("""{"Engine":[{"name":"unit","run":"npm test"}]}""", Mapped(again)!.ToJsonString());

        Assert.Empty(again.WithHeavyGates("engine", null).HeavyGates);
        Assert.Empty(again.WithHeavyGates("engine", []).HeavyGates);
    }

    public static TheoryData<string, string?, string> Refusals() => new()
    {
        { "no repository named", null, "this machine's heavy gates are set for a repository: name one." },
        { "a gate with no name", "=npm test", "a gate needs a name: each is `<name>=<command>`, in the order they run." },
        { "a gate with no command", "build= ", "`build` needs the command it runs: each is `<name>=<command>`, in the order they run." },
        { "a name twice in any case", "build=dotnet build|BUILD=npm run build", "`BUILD` is named twice: each gate is named once." },
        { "a folder that climbs out", "web=npm test@../web", "`web`'s folder, `../web`, is not under the repository's root: name it from there, with no `..`." },
        { "a folder on a drive", "web=npm test@X:/web", "`web`'s folder, `X:/web`, is not under the repository's root: name it from there, with no `..`." },
        { "a rooted folder", "web=npm test@/web", "`web`'s folder, `/web`, is not under the repository's root: name it from there, with no `..`." },
    };

    /// <summary>What the door refuses it refuses in one sentence, the first problem, and nothing changes.</summary>
    [Theory]
    [MemberData(nameof(Refusals))]
    public void The_door_refuses_the_first_problem_in_its_words(string name, string? gates, string refusal)
    {
        var before = DriverConfig.Empty.WithHeavyGates("engine", [Gate("build", "dotnet build")]);

        var refused = Assert.Throws<DriverException>(() => before.WithHeavyGates(gates is null ? " " : "engine", gates is null ? [Gate("build", "x")] : Gates(gates)));

        Assert.Equal(refusal, refused.Message);
        Assert.Equal(["build"], HeavyMapping.For(before, "engine")!.Select(gate => gate.Name));
        _ = name;
    }

    /// <summary>A file holding the mapping is read and written with no edit as the same bytes, as every key of the file is.</summary>
    [Fact]
    public void A_file_holding_the_mapping_reads_and_writes_back_the_same_bytes()
    {
        const string Text = """
            {
              "heavyGates": {
                "engine": [
                  { "name": "build", "run": "dotnet build", "later": "a newer build's field" }
                ]
              }
            }

            """;

        Assert.Equal(Text, DriverConfig.Parse(Text).ToJson());
    }

    private static HeavyGate Gate(string name, string run, string? cwd = null) => new(name, run, cwd, Heavy: true);

    /// <summary><c>name=run@cwd</c>, gates parted by <c>|</c>.</summary>
    private static IReadOnlyList<HeavyGate> Gates(string spelled) =>
    [
        .. spelled.Split('|').Select(gate =>
        {
            var at = gate.IndexOf('@');
            var (pair, cwd) = at < 0 ? (gate, null) : (gate[..at], gate[(at + 1)..]);
            var equals = pair.IndexOf('=');
            return Gate(pair[..equals], pair[(equals + 1)..], cwd);
        }),
    ];

    /// <summary>The config's mapping as its writer writes it whole, read from no text; an empty object for none.</summary>
    private static JsonNode? Mapped(DriverConfig config) =>
        JsonNode.Parse((config with { Source = null }).ToJson())!.AsObject()["heavyGates"]?.DeepClone() ?? new JsonObject();
}
