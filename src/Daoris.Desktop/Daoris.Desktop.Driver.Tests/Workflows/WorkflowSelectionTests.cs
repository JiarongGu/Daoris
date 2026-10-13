using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WORKFLOW1e (D157 point 10, the workflow design §2.5, §4.1–§4.3): which workflow work follows — <c>workflows</c> and
/// <c>workspaceWorkflows</c> in <c>driver.json</c> read, written, edited and resolved in §4.1's order — and the versions a kept run
/// names.
/// </summary>
/// <remarks>
/// The driver's half of a TWIN with the CLI's <c>workflowchoice.ts</c>: both are held, cell for cell, to ONE table, this suite's
/// <c>fixtures/workflow-selection.json</c>, which <c>driverconfig.test.ts</c> reads too. A row changed on one side alone is the
/// other side's failure.
/// </remarks>
public sealed class WorkflowSelectionTests
{
    public static TheoryData<string, int> Rows(string section)
    {
        var data = new TheoryData<string, int>();
        foreach (var (row, index) in Table(section).Select((row, index) => (row, index))) data.Add(row.GetProperty("name").GetString()!, index);
        return data;
    }

    [Theory]
    [MemberData(nameof(Rows), "read")]
    public void Both_maps_are_read_and_written_as_the_cli_reads_and_writes_them(string name, int index)
    {
        var row = Table("read")[index];
        var config = DriverConfig.Parse(row.GetProperty("file").GetRawText());

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("read").GetRawText()), Maps(config)), $"{name}: {Maps(config).ToJsonString()}");
    }

    [Theory]
    [MemberData(nameof(Rows), "problems")]
    public void An_entry_s_first_problem_is_the_cli_s(string name, int index)
    {
        var row = Table("problems")[index];
        var problem = WorkflowSelection.Problem(row.GetProperty("entry"), row.GetProperty("scope").GetString() == "workspace");

        Assert.True(Text(row, "problem") == problem, $"{name}: {problem}");
    }

    [Theory]
    [MemberData(nameof(Rows), "resolve")]
    public void A_selection_resolves_as_the_cli_resolves_it(string name, int index)
    {
        var row = Table("resolve")[index];
        var task = row.GetProperty("task") is { ValueKind: JsonValueKind.Object } chosen
            ? new WorkflowTaskChoice(Text(chosen, "kind"), Text(chosen, "workflow"))
            : null;

        var selected = WorkflowSelection.Resolve(
            DriverConfig.Parse(row.GetProperty("file").GetRawText()), Text(row, "repository"), Text(row, "workspace"), task);

        var said = new JsonObject
        {
            ["workflow"] = selected.Workflow,
            ["level"] = selected.Level,
            ["kind"] = selected.Kind,
            ["label"] = selected.Label,
            ["paths"] = new JsonArray([.. selected.Paths.Select(path => (JsonNode)path)]),
            ["undeclared"] = selected.Undeclared,
        };
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("selection").GetRawText()), said), $"{name}: {said.ToJsonString()}");
    }

    [Theory]
    [MemberData(nameof(Rows), "edits")]
    public void An_edit_is_the_cli_s_or_refused_in_its_words(string name, int index)
    {
        var row = Table("edits")[index];
        var config = DriverConfig.Parse(row.GetProperty("file").GetRawText());
        var edit = Edit(row.GetProperty("edit"));

        if (Text(row, "refusal") is { } refusal)
        {
            var error = Assert.Throws<DriverException>(() => WorkflowSelection.Apply(config, edit));
            Assert.Equal(refusal, error.Message);
            return;
        }

        var after = Maps(WorkflowSelection.Apply(config, edit));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("read").GetRawText()), after), $"{name}: {after.ToJsonString()}");
    }

    [Theory]
    [MemberData(nameof(Rows), "kept")]
    public void The_versions_a_kept_run_names_are_the_cli_s(string name, int index)
    {
        var row = Table("kept")[index];
        var kept = WorkflowRunBindings.KeptVersions([.. row.GetProperty("runs").EnumerateArray()], row.GetProperty("workflow").GetString()!);

        Assert.True(row.GetProperty("versions").EnumerateArray().Select(version => version.GetInt32()).SequenceEqual(kept), $"{name}: {string.Join(", ", kept)}");
    }

    /// <summary>Kinds compare as written, so a kind's order in a choice is held, not only its fields.</summary>
    [Fact]
    public void A_choice_keeps_its_kinds_in_the_order_written()
    {
        var config = DriverConfig.Parse("""
            {"workspaceWorkflows":{"work":{"kinds":{"feature":{"label":"Feature"},"docs":{"label":"Documentation"}}}}}
            """);

        Assert.Equal(["feature", "docs"], config.WorkspaceWorkflows["work"].Kinds.Select(pair => pair.Key));
        Assert.Contains("\"feature\": {", config.ToJson(), StringComparison.Ordinal);
        Assert.True(config.ToJson().IndexOf("\"feature\"", StringComparison.Ordinal) < config.ToJson().IndexOf("\"docs\"", StringComparison.Ordinal));
    }

    /// <summary>🔴 Each writer keeps the other's sections: a file the driver saves for another setting keeps both maps.</summary>
    [Fact]
    public void Saving_another_setting_keeps_the_choices()
    {
        var config = DriverConfig.Parse("""
            {"workflows":{"web-app":{"default":"feature-review"}},"workspaceWorkflows":{"work":{"kinds":{"docs":{"label":"Documentation","paths":["docs/**"]}}}}}
            """);

        var again = DriverConfig.Parse(config.WithLine("web-app", "main").ToJson());

        Assert.Equal("feature-review", again.Workflows["web-app"].Default);
        Assert.Equal(["docs/**"], again.WorkspaceWorkflows["work"].KindOf("docs")!.Paths);
    }

    private static JsonObject Maps(DriverConfig config)
    {
        var written = JsonNode.Parse(config.ToJson())!.AsObject();
        return new JsonObject
        {
            ["workflows"] = written["workflows"]?.DeepClone() ?? new JsonObject(),
            ["workspaceWorkflows"] = written["workspaceWorkflows"]?.DeepClone() ?? new JsonObject(),
        };
    }

    /// <summary>An edit as the table writes it: <c>use</c>, <c>declare</c> or <c>drop</c>.</summary>
    internal static WorkflowChoiceEdit Edit(JsonElement edit)
    {
        if (edit.TryGetProperty("declare", out var declared))
        {
            return new WorkflowDeclare(
                declared.GetString()!, Text(edit, "workspace")!, Text(edit, "label")!,
                [.. edit.GetProperty("paths").EnumerateArray().Select(path => path.GetString()!)]);
        }

        if (edit.TryGetProperty("drop", out var dropped)) return new WorkflowDrop(dropped.GetString()!, Text(edit, "workspace")!);

        return new WorkflowUse(Text(edit, "use"), Text(edit, "repository"), Text(edit, "workspace"), Text(edit, "kind"))
        {
            InKnown = edit.TryGetProperty("in", out _),
            In = Text(edit, "in"),
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IReadOnlyList<JsonElement> Table(string section)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "workflow-selection.json")));
        return [.. document.RootElement.GetProperty(section).EnumerateArray().Select(row => row.Clone())];
    }
}
