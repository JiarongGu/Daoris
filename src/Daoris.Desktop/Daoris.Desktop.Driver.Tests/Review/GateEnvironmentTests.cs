using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1f part f1 (the integration design §5.5, §5.9): the environment a gate is composed with, and the digest of a gate's
/// composition, the driver's twins of <c>gateEnvironment</c> and <c>compositionDigest</c> in <c>tools/merge-branch.mjs</c>. Held cell for
/// cell to ONE table, <c>fixtures/gate-environment.json</c>, which <c>src/Daoris.Cli/tool-test/integration-gates.test.ts</c> holds the tool
/// to, row for row; its <c>driverOnly</c> rows are what the driver's gate is given beyond the tool's. Values only, no process.
/// </summary>
public sealed class GateEnvironmentTests
{
    private static JsonDocument Table() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "gate-environment.json")));

    private static IReadOnlyList<KeyValuePair<string, string>> Pairs(JsonElement values) =>
        [.. values.EnumerateObject().Select(each => KeyValuePair.Create(each.Name, each.Value.GetString()!))];

    private static IReadOnlyList<string> Names(JsonElement values) => [.. values.EnumerateArray().Select(each => each.GetString()!)];

    private static TheoryData<int> Indexes(Func<JsonElement, JsonElement> section)
    {
        var data = new TheoryData<int>();
        using var table = Table();
        for (var at = 0; at < section(table.RootElement).GetArrayLength(); at += 1) data.Add(at);
        return data;
    }

    public static TheoryData<int> EnvironmentRows() => Indexes(root => root.GetProperty("environments"));

    public static TheoryData<int> HoldRows() => Indexes(root => root.GetProperty("driverOnly").GetProperty("holds"));

    public static TheoryData<int> CompositionRows() => Indexes(root => root.GetProperty("compositions"));

    /// <summary>The composed list read as the environment a process gets: each name once, the table's expectation as a map.</summary>
    private static void AssertEnvironment(JsonElement row, IReadOnlyList<KeyValuePair<string, string>> composed)
    {
        var name = row.GetProperty("name").GetString();
        var expected = Pairs(row.GetProperty("expect")).ToDictionary(each => each.Key, each => each.Value, StringComparer.Ordinal);
        Assert.True(composed.Count == composed.Select(each => each.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count(), $"{name}: a name is doubled");
        Assert.Equal(expected.OrderBy(each => each.Key, StringComparer.Ordinal), composed.OrderBy(each => each.Key, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(EnvironmentRows))]
    public void A_gate_is_composed_with_the_environment_the_shared_table_says(int at)
    {
        using var table = Table();
        var row = table.RootElement.GetProperty("environments")[at];
        AssertEnvironment(row, GateEnvironment.Compose(Pairs(row.GetProperty("start")), Names(row.GetProperty("pass"))));
    }

    [Theory]
    [MemberData(nameof(HoldRows))]
    public void A_gate_is_given_the_hold_marker_and_a_build_server_name_once_as_the_driver_only_rows_say(int at)
    {
        using var table = Table();
        var row = table.RootElement.GetProperty("driverOnly").GetProperty("holds")[at];
        var hold = row.GetProperty("hold").ValueKind == JsonValueKind.String ? row.GetProperty("hold").GetString() : null;
        AssertEnvironment(row, GateEnvironment.Compose(Pairs(row.GetProperty("start")), Names(row.GetProperty("pass")), hold));
    }

    [Fact]
    public void The_rule_and_the_build_servers_are_the_tables()
    {
        using var table = Table();
        Assert.Equal(Names(table.RootElement.GetProperty("removed")), GateEnvironment.RemovedRule);
        Assert.Equal(Pairs(table.RootElement.GetProperty("beside")), GateEnvironment.Beside);
        Assert.Equal(MachineAdmission.InsideVariable, GateEnvironment.HoldVariable);
        var rule = table.RootElement.GetProperty("compositions")[0].GetProperty("settings").GetProperty("environment");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rule.GetRawText()), GateEnvironment.Rule()), "the rule a composition records is the one the gate is composed by");
    }

    [Fact]
    public void The_list_is_the_kept_names_in_order_then_the_passed_then_the_build_servers_then_the_hold()
    {
        var composed = GateEnvironment.Compose(
            [KeyValuePair.Create("B", "1"), KeyValuePair.Create("DAORIS_HOME", "x"), KeyValuePair.Create("a", "2"), KeyValuePair.Create("GIT_DIR", "g"), KeyValuePair.Create("Z", "3")],
            ["git_dir"], "h-1");
        Assert.Equal(
            ["B", "a", "Z", "GIT_DIR", "MSBUILDDISABLENODEREUSE", "DOTNET_CLI_USE_MSBUILD_SERVER", "UseSharedCompilation", "DAORIS_ADMISSION_HOLD"],
            composed.Select(each => each.Key));
    }

    [Theory]
    [InlineData("DAORIS_HOME", true)]
    [InlineData("daoris_anything", true)]
    [InlineData("GIT_CONFIG_COUNT", true)]
    [InlineData("git_ceiling_directories", true)]
    [InlineData("MERGE_BRANCH_MEMORY", true)]
    [InlineData("GIT_AUTHOR_NAME", false)]
    [InlineData("GIT_OPTIONAL_LOCKS", false)]
    [InlineData("MERGE_BRANCHES", false)]
    [InlineData("DAORIS", false)]
    [InlineData("PATH", false)]
    public void A_name_is_removed_by_the_rule_in_either_spelling(string name, bool removed) =>
        Assert.Equal(removed, GateEnvironment.IsRemoved(name));

    [Theory]
    [MemberData(nameof(CompositionRows))]
    public void A_compositions_digest_is_the_shared_tables(int at)
    {
        using var table = Table();
        var row = table.RootElement.GetProperty("compositions")[at];
        var units = row.GetProperty("units").ValueKind == JsonValueKind.Null ? null : Names(row.GetProperty("units"));
        var settings = JsonNode.Parse(row.GetProperty("settings").GetRawText())!;
        Assert.Equal((row.GetProperty("name").GetString(), row.GetProperty("digest").GetString()), (row.GetProperty("name").GetString(), GateComposition.DigestOf(units, settings)));
    }

    [Fact]
    public void A_composition_made_of_its_parts_digests_as_the_table_says_with_its_classes_sorted()
    {
        using var table = Table();
        var row = table.RootElement.GetProperty("compositions")[2];
        var settings = row.GetProperty("settings");
        var runsettings = settings.GetProperty("runsettings");
        var made = GateComposition.Of(
            settings.GetProperty("gate").GetString()!, settings.GetProperty("run").GetString()!, settings.GetProperty("cwd").GetString(),
            new RunSettingsText(runsettings.GetProperty("path").GetString()!, runsettings.GetProperty("text").GetString()), [], ["N.A"], GateEnvironment.Rule());
        Assert.Equal(row.GetProperty("digest").GetString(), made.Digest);
        Assert.Equal(["N.A"], made.Units);

        var narrowed = table.RootElement.GetProperty("compositions")[1];
        var sorted = GateComposition.Of("driver", "dotnet test src/D.Tests --no-build --settings src/D/process.runsettings", null, null, ["N.C"], ["N.B", "N.A"], GateEnvironment.Rule());
        Assert.Equal(narrowed.GetProperty("digest").GetString(), sorted.Digest);
        Assert.Equal(["N.A", "N.B"], sorted.Units);
        Assert.Null(GateComposition.Of("driver", "x", null, null, [], null, GateEnvironment.Rule()).Units);
    }

    [Fact]
    public void The_canonical_form_orders_every_objects_keys_and_writes_values_as_JavaScript_does()
    {
        Assert.Equal(
            """{"a":[true,null,"x\"y\\z\n",12],"b":{"c":null,"d":"é"}}""",
            GateComposition.Canonical(JsonNode.Parse("""{"b":{"d":"é","c":null},"a":[true,null,"x\"y\\z\n",12]}""")));
        Assert.Equal("null", GateComposition.Canonical(null));
        Assert.Equal("[]", GateComposition.Canonical(new JsonArray()));
    }
}
