using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1a (D157 point 7, the workflow design §2.7, §3.2): a repository's Current workflow, derived from its rules as they
/// stand, each step with its source and its limit, and a version both runtimes build by hand. Nothing here writes: the rules
/// stay the authority.
/// </summary>
/// <remarks>
/// The driver's half of a TWIN with the CLI's <c>workflows.ts</c>: both are held, cell for cell, to ONE table, this suite's
/// <c>fixtures/workflow-current.json</c>, which <c>workflows.test.ts</c> reads too. A row changed on one side alone is the
/// other side's failure.
/// </remarks>
public sealed class WorkflowCurrentTests
{
    public static TheoryData<string, int> CurrentRows()
    {
        var data = new TheoryData<string, int>();
        foreach (var (row, index) in Table("current").Select((row, index) => (row, index)))
        {
            data.Add(row.GetProperty("name").GetString()!, index);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CurrentRows))]
    public void Current_is_derived_from_the_rules_as_the_cli_derives_it(string name, int index)
    {
        var row = Table("current")[index];
        var plugins = row.GetProperty("plugins").EnumerateArray()
            .Select(each => new WorkflowPlugin(
                each.GetProperty("id").GetString()!, [.. each.GetProperty("points").EnumerateArray().Select(point => point.GetString()!)]))
            .ToList();

        var drawn = WorkflowCurrent.Derive(
            DriverConfig.Parse(row.GetProperty("config").GetRawText()), Text(row, "repository"), Text(row, "workspace"), plugins);

        Assert.Equal(Strings(row.GetProperty("startHolds")), drawn.StartHolds);
        var steps = row.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(steps.Select(step => step.GetProperty("id").GetString()), drawn.Steps.Select(step => step.Id));
        foreach (var (expected, derived) in steps.Zip(drawn.Steps))
        {
            var actual = JsonNode.Parse(WorkflowCurrent.ToJson(derived))!.AsObject();
            foreach (var cell in new[] { "id", "kind", "participation", "executor", "press", "source", "settings", "runtime", "limit" })
            {
                var want = JsonNode.Parse(expected.GetProperty(cell).GetRawText());
                Assert.True(JsonNode.DeepEquals(want, actual[cell]), $"{name}: {derived.Id}'s {cell} is {actual[cell]?.ToJsonString() ?? "null"}");
            }

            // The order the version reads a step's settings in is the table's.
            Assert.Equal(
                expected.GetProperty("settings").EnumerateObject().Select(setting => setting.Name),
                derived.Settings.Select(setting => setting.Name));
        }

        Assert.True(row.GetProperty("version").GetString() == drawn.Version, $"{name}: the version is {drawn.Version}");
    }

    [Fact]
    public void Each_limit_is_said_in_the_table_s_words_in_its_order()
    {
        var rows = Table("limits").Select(row => (row.GetProperty("code").GetString()!, row.GetProperty("says").GetString()!)).ToList();

        Assert.Equal(rows, WorkflowLimits.Table.Select(limit => (limit.Code, limit.Says)));
        // XAGENT1f: the opinion's limit says what its gate runs, never that nothing reads the rule.
        Assert.StartsWith("Partial: work here lands only once another agent's reading of it is settled", WorkflowLimits.Says(WorkflowLimits.OpinionPartial));
        Assert.DoesNotContain(OpinionRules.DeclaredOnly, WorkflowLimits.Table.Select(limit => limit.Says));
    }

    [Fact]
    public void The_version_is_a_digest_of_the_text_drawn_escaped_by_hand()
    {
        var plain = WorkflowCurrent.Derive(DriverConfig.Parse("{}"), "web-app", null, []);
        Assert.Matches("^[0-9a-f]{12}$", plain.Version);

        var said = WorkflowCurrent.Derive(
            DriverConfig.Parse("""{"standing":{"web-app":{"says":"a \"quoted\"\\word\nand a line"}}}"""), "web-app", null, []);
        Assert.NotEqual(plain.Version, said.Version);
        Assert.Contains("""standing="a \"quoted\"\\word\u000aand a line" """.TrimEnd(), WorkflowCurrent.Canonical(said), StringComparison.Ordinal);
    }

    [Fact]
    public void The_plugins_handed_are_those_switched_on_and_sound_in_catalogue_order_with_their_points()
    {
        // A home of its own, as PluginCatalogTests makes one.
        var home = Path.Combine(Path.GetTempPath(), "daoris-workflow-plugins-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            foreach (var (folder, manifest) in new[]
            {
                ("example.hold", """{"id":"example.hold","hooks":{"command":["node","hold.mjs"],"points":["quest/consider"]}}"""),
                ("example.idle", """{"id":"example.idle"}"""),
                ("example.pull-request", """{"id":"example.pull-request","hooks":{"command":["node","land.mjs"],"points":["work/land","work/state"]}}"""),
                ("example.refused", """{"id":"example.refused","hooks":{"command":["node","x.mjs"]}}"""),
            })
            {
                Directory.CreateDirectory(Path.Combine(home, PluginCatalog.Folder, folder));
                File.WriteAllText(Path.Combine(home, PluginCatalog.Folder, folder, PluginCatalog.ManifestName), manifest);
            }

            var plugins = WorkflowCurrent.PluginsOf(PluginCatalog.Load(home));

            Assert.Equal(["example.hold", "example.idle", "example.pull-request"], plugins.Select(plugin => plugin.Id));
            Assert.Equal(["quest/consider"], plugins[0].Points);
            Assert.Empty(plugins[1].Points);
            Assert.Equal(["work/land", "work/state"], plugins[2].Points);
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    private static string? Text(JsonElement row, string name) =>
        row.GetProperty(name).ValueKind == JsonValueKind.String ? row.GetProperty(name).GetString() : null;

    private static string[] Strings(JsonElement list) => [.. list.EnumerateArray().Select(each => each.GetString()!)];

    /// <summary>One section of the shared table's rows.</summary>
    private static IReadOnlyList<JsonElement> Table(string section)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "workflow-current.json")));
        return [.. document.RootElement.GetProperty(section).EnumerateArray().Select(row => row.Clone())];
    }
}
