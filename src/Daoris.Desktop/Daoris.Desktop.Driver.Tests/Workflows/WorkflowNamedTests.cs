using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1d (D157 points 5, 6 and 8, the workflow design §2.4–§2.6, §3.2, §3.3, §3.9): named workflows and their versions —
/// what a file and each version may say, a version's digest, the diff by step id, the presets, and a version added.
/// </summary>
/// <remarks>
/// The driver's half of a TWIN with the CLI's <c>namedworkflows.ts</c>: both are held, cell for cell, to ONE table, this suite's
/// <c>fixtures/workflow-named.json</c>, which <c>namedworkflows.test.ts</c> reads too. A row changed on one side alone is the
/// other side's failure.
/// </remarks>
public sealed class WorkflowNamedTests
{
    public static TheoryData<string, int> Rows(string section)
    {
        var data = new TheoryData<string, int>();
        foreach (var (row, index) in Table(section).Select((row, index) => (row, index)))
        {
            data.Add(row.GetProperty(section == "presets" ? "id" : section == "variants" ? "preset" : "name").GetString()!, index);
        }

        return data;
    }

    [Fact]
    public void The_kinds_are_the_table_s_each_its_runtime_on_main_its_limit_and_its_fields_in_order()
    {
        var table = Table("kinds");
        Assert.Equal(table.Select(row => row.GetProperty("kind").GetString()), WorkflowKindTable.Table.Select(row => row.Kind));
        foreach (var (expected, row) in table.Zip(WorkflowKindTable.Table))
        {
            Assert.Equal(expected.GetProperty("runtime").GetString(), row.Runtime);
            Assert.Equal(Text(expected, "limit"), row.Limit);
            var fields = new JsonArray([.. row.Fields.Select(field =>
            {
                var cell = new JsonObject { ["name"] = field.Name, ["type"] = field.Type };
                if (field.Choices is not null) cell["choices"] = new JsonArray([.. field.Choices.Select(choice => (JsonNode)choice)]);
                cell["required"] = field.Required;
                cell["default"] = field.Default switch
                {
                    null => (JsonNode?)null,
                    bool flag => JsonValue.Create(flag),
                    var text => JsonValue.Create((string)text),
                };
                return (JsonNode)cell;
            })]);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.GetProperty("fields").GetRawText()), fields),
                $"{row.Kind}'s fields are {fields.ToJsonString()}");
            if (row.Limit is not null) Assert.Contains(WorkflowLimits.Table, limit => limit.Code == row.Limit);
        }

        Assert.Equal(24, WorkflowKindTable.MaxSteps);
        Assert.Equal(20, WorkflowKindTable.KeptVersions);
    }

    [Theory]
    [MemberData(nameof(Rows), "files")]
    public void A_workflow_file_is_read_as_the_cli_reads_it(string name, int index)
    {
        var row = Table("files")[index];

        var read = WorkflowNamed.Read(row.GetProperty("file"));

        Assert.True(Text(row, "problem") == read.Problem, $"{name}: the file's problem is {read.Problem}");
        var versions = row.GetProperty("versions").EnumerateArray().ToList();
        Assert.Equal(versions.Count, read.Versions.Count);
        foreach (var (expected, version) in versions.Zip(read.Versions))
        {
            Assert.Equal(expected.GetProperty("version").GetInt32(), version.Version);
            Assert.True(Text(expected, "problem") == version.Problem, $"{name}: v{version.Version}'s problem is {version.Problem}");
            Assert.True(Text(expected, "digest") == version.Digest, $"{name}: v{version.Version}'s digest is {version.Digest}");
        }
    }

    [Theory]
    [MemberData(nameof(Rows), "versions")]
    public void A_version_s_steps_are_read_as_the_cli_reads_them(string name, int index)
    {
        var row = Table("versions")[index];
        var version = JsonDocument.Parse(
            $$"""{"version":1,"at":"2026-10-09T09:12:00Z","door":"terminal","steps":{{row.GetProperty("steps").GetRawText()}}}""").RootElement;

        var read = WorkflowNamed.ReadVersion(version, 1, []);

        Assert.True(Text(row, "problem") == read.Problem, $"{name}: the problem is {read.Problem}");
        Assert.True(Text(row, "digest") == read.Digest, $"{name}: the digest is {read.Digest}");
    }

    [Fact]
    public void The_digest_is_of_a_text_built_by_hand_every_field_written_whatever_was_left_out()
    {
        var steps = Read("""
            [{"id":"work","kind":"work"},{"id":"ahead","kind":"go-ahead","act":"say \"yes\" to C:\\work\nthen go","on":"dev"},
             {"id":"landing","kind":"landing","form":"merge","accept":"you"}]
            """);

        Assert.Equal(string.Join('\n',
            "workflow", "step \"work\" work", "step \"ahead\" go-ahead", "  act=\"say \\\"yes\\\" to C:\\\\work\\u000athen go\"", "  on=\"dev\"",
            "  refused=\"stop\"", "step \"landing\" landing", "  form=\"merge\"", "  accept=\"you\"", "  pattern=-", "  plugin=-"),
            WorkflowNamed.Canonical(steps));
    }

    [Theory]
    [MemberData(nameof(Rows), "diffs")]
    public void Two_versions_are_compared_by_step_id_as_the_cli_compares_them(string name, int index)
    {
        var row = Table("diffs")[index];
        var before = row.GetProperty("before").ValueKind == JsonValueKind.Null ? null : Read(row.GetProperty("before").GetRawText());

        var diff = WorkflowDiff.Of(before, Read(row.GetProperty("after").GetRawText()));

        var entries = new JsonArray([.. diff.Entries.Select(entry => (JsonNode)new JsonObject
        {
            ["id"] = entry.Id, ["kind"] = entry.Kind, ["change"] = entry.Change,
            ["fields"] = new JsonArray([.. entry.Fields.Select(field => (JsonNode)field)]), ["moved"] = entry.Moved,
        })]);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("entries").GetRawText()), entries), $"{name}: the entries are {entries.ToJsonString()}");
        Assert.Equal(Strings(row.GetProperty("said")), diff.Said);
    }

    [Theory]
    [MemberData(nameof(Rows), "presets")]
    public void The_presets_are_the_table_s_in_order_each_line_composed_from_its_steps(string id, int index)
    {
        var row = Table("presets")[index];
        var preset = WorkflowPresets.Table[index];

        Assert.Equal(id, preset.Id);
        Assert.Equal(row.GetProperty("name").GetString(), preset.Name);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("steps").GetRawText()), preset.Steps()), $"{id}: its steps");
        Assert.Equal(row.GetProperty("line").GetString(), WorkflowPresets.Line(Read(preset.StepsJson)));
        Assert.Equal(Table("presets").Count, WorkflowPresets.Table.Count);
    }

    [Theory]
    [MemberData(nameof(Rows), "variants")]
    public void A_preset_with_a_second_opinion_your_look_first_or_both(string preset, int index)
    {
        var row = Table("variants")[index];

        var steps = WorkflowPresets.WithGates(WorkflowPresets.Of(preset)!.Steps(), row.GetProperty("opinion").GetBoolean(), row.GetProperty("look").GetBoolean());

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("steps").GetRawText()), steps), $"{preset}: its steps are {steps.ToJsonString()}");
        Assert.Equal(row.GetProperty("line").GetString(), WorkflowPresets.Line(Read(steps.ToJsonString())));
    }

    [Theory]
    [MemberData(nameof(Rows), "lines")]
    public void The_line_other_steps_compose(string name, int index)
    {
        var row = Table("lines")[index];

        Assert.True(row.GetProperty("line").GetString() == WorkflowPresets.Line(Read(row.GetProperty("steps").GetRawText())), name);
    }

    [Theory]
    [MemberData(nameof(Rows), "store")]
    public void A_version_added_is_planned_as_the_cli_plans_it(string name, int index)
    {
        var row = Table("store")[index];
        var add = row.GetProperty("add");

        var plan = WorkflowStore.PlanAdd(
            Node(row.GetProperty("file")),
            new WorkflowVersionAdded(add.GetProperty("id").GetString()!, Text(add, "name"), add.GetProperty("door").GetString()!,
                add.GetProperty("at").GetString()!, Node(add.GetProperty("steps"))),
            [.. row.GetProperty("kept").EnumerateArray().Select(each => each.GetInt32())]);

        Assert.True(Text(row, "problem") == plan.Problem, $"{name}: the problem is {plan.Problem}");
        Assert.Equal(row.GetProperty("version").ValueKind == JsonValueKind.Null ? null : row.GetProperty("version").GetInt32(), plan.Version);
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("result")), plan.Result), $"{name}: the file after is {plan.Result?.ToJsonString()}");
    }

    /// <summary>The store on disk: a new workflow's file written whole, LF and no BOM, a version added to it, and both read back.</summary>
    [Fact]
    public void A_workflow_is_written_under_the_home_atomically_and_read_back_by_its_id()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-workflow-store-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var steps = JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]""");
            Assert.Empty(WorkflowStore.List(home));

            Assert.Equal(1, WorkflowStore.Add(home, new WorkflowVersionAdded("docs-to-pr", "文档直接开拉取请求", "screen", "2026-10-09T09:12:00Z", steps)));
            Assert.Equal(2, WorkflowStore.Add(home, new WorkflowVersionAdded("docs-to-pr", null, "terminal", "2026-10-09T09:20:00Z",
                JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"branch","accept":"you","plugin":"none"}]"""))));

            var path = Path.Combine(home, "workflows", "docs-to-pr.json");
            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "no BOM");
            var text = Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
            Assert.EndsWith("}\n", text, StringComparison.Ordinal);
            Assert.Contains("文档直接开拉取请求", text, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(Path.Combine(home, "workflows"), "*.writing"));

            var found = Assert.Single(WorkflowStore.List(home));
            Assert.Null(found.Read.Problem);
            Assert.Equal(new[] { 1, 2 }, found.Read.Versions.Select(each => each.Version));

            // A refusal writes nothing, and says why.
            var refused = Assert.Throws<DriverException>(() => WorkflowStore.Add(home, new WorkflowVersionAdded("docs-to-pr", null, "terminal",
                "2026-10-09T09:30:00Z", JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"merge","accept":"automatic"}]"""))));
            Assert.StartsWith("v3, step `landing`: only a branch lands with no press", refused.Message, StringComparison.Ordinal);
            Assert.Equal(text, File.ReadAllText(path));

            // An id that is no id names no path.
            Assert.Throws<DriverException>(() => WorkflowStore.Add(home, new WorkflowVersionAdded("../escape", "Docs", "terminal", "2026-10-09T09:30:00Z", steps)));
            Assert.False(File.Exists(Path.Combine(home, "escape.json")));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    /// <summary>A file that is not JSON, or holds another workflow than its name, is listed with its problem and never written over.</summary>
    [Fact]
    public void A_file_that_does_not_read_is_listed_with_its_problem_and_never_written_over()
    {
        var home = Path.Combine(Path.GetTempPath(), "daoris-workflow-unread-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var folder = Path.Combine(home, "workflows");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "broken.json"), "{ not json");
            File.WriteAllText(Path.Combine(folder, "misnamed.json"),
                """{"id":"other","name":"Other","versions":[{"version":1,"at":"2026-10-09T09:12:00Z","door":"terminal","steps":[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]}]}""");

            var listed = WorkflowStore.List(home);

            Assert.Equal(new[] { "broken", "misnamed" }, listed.Select(each => each.Id));
            Assert.Equal("`broken.json` is not readable JSON.", listed[0].Read.Problem);
            Assert.Equal("`misnamed.json` holds the workflow `other`; a workflow's file is named by its id.", listed[1].Read.Problem);
            var refused = Assert.Throws<DriverException>(() => WorkflowStore.Add(home, new WorkflowVersionAdded("broken", null, "terminal",
                "2026-10-09T09:30:00Z", JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]"""))));
            Assert.Equal("nothing was written: the workflow's file cannot be read — `broken.json` is not readable JSON.", refused.Message);
            Assert.Equal("{ not json", File.ReadAllText(Path.Combine(folder, "broken.json")));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
        }
    }

    private static IReadOnlyList<NamedStep> Read(string steps)
    {
        var (read, problem) = WorkflowNamed.ReadSteps(JsonDocument.Parse(steps).RootElement, 1);
        Assert.True(problem is null, $"the table's steps do not read: {problem}");
        return read!;
    }

    private static JsonNode? Node(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(element.GetRawText());

    private static string? Text(JsonElement row, string name) =>
        row.GetProperty(name).ValueKind == JsonValueKind.String ? row.GetProperty(name).GetString() : null;

    private static string[] Strings(JsonElement list) => [.. list.EnumerateArray().Select(each => each.GetString()!)];

    /// <summary>One section of the shared table's rows.</summary>
    private static IReadOnlyList<JsonElement> Table(string section)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "workflow-named.json")));
        return [.. document.RootElement.GetProperty(section).EnumerateArray().Select(row => row.Clone())];
    }
}
