using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (D163 point 3; the mixed agents design §5): the hand-over rule, <c>handOver</c> and <c>workspaceHandOver</c> in
/// <c>driver.json</c> — read, refused, resolved and edited, held cell for cell to ONE table, <c>fixtures/hand-over-rules.json</c>
/// (the CLI's twin, <c>handover.ts</c>, left with the driver family's TypeScript in PUSH1f2b).
/// </summary>
public sealed class HandOverRulesTests
{
    public static TheoryData<string, string, string, string?, string, string> ReadRows()
    {
        var data = new TheoryData<string, string, string, string?, string, string>();
        foreach (var row in Table("read")) data.Add(Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4)!, Cell(row, 5)!);
        return data;
    }

    [Theory]
    [MemberData(nameof(ReadRows))]
    public void A_hand_over_rule_resolves_as_the_cli_resolves_it(
        string name, string file, string repository, string? workspace, string source, string rule)
    {
        var resolved = HandOverRules.Resolve(DriverConfig.Parse(file), repository, workspace);

        Assert.True(source == resolved.Source, $"{name}: from {resolved.Source}");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rule), JsonNode.Parse(HandOverRules.ToJson(resolved.Rule))), $"{name}: the rule");
    }

    public static TheoryData<string, string, string?> ProblemRows()
    {
        var data = new TheoryData<string, string, string?>();
        foreach (var row in Table("problems")) data.Add(Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2));
        return data;
    }

    [Theory]
    [MemberData(nameof(ProblemRows))]
    public void A_hand_over_rule_s_first_problem_is_the_cli_s_in_its_words(string name, string value, string? problem)
    {
        using var document = JsonDocument.Parse(value);

        Assert.True(problem == HandOverRules.Read(document.RootElement).Problem, name);
    }

    public static TheoryData<string, string, string, string?, string?> EditRows()
    {
        var data = new TheoryData<string, string, string, string?, string?>();
        foreach (var row in Table("edits")) data.Add(Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4));
        return data;
    }

    [Theory]
    [MemberData(nameof(EditRows))]
    public void A_hand_over_edit_writes_what_the_cli_writes_or_refuses_in_its_words(
        string name, string file, string edit, string? after, string? refusal)
    {
        var config = DriverConfig.Parse(file);
        using var document = JsonDocument.Parse(edit);
        var change = HandOverRules.EditOf(document.RootElement);
        if (refusal is not null)
        {
            Assert.Equal(refusal, Assert.Throws<DriverException>(() => HandOverRules.Apply(config, change)).Message);
            return;
        }

        var written = JsonNode.Parse(HandOverRules.Apply(config, change).ToJson())!.AsObject();
        var maps = new JsonObject();
        foreach (var key in new[] { "handOver", "workspaceHandOver" })
        {
            if (written[key] is { } map) maps[key] = map.DeepClone();
        }

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after!), maps), $"{name}: {maps.ToJsonString()}");
    }

    /// <summary>The rule round-trips through the file, kept exactly, after the helpers rule; a driver.json that names none writes none.</summary>
    [Fact]
    public void A_rule_is_kept_as_written_after_the_helpers_rule_and_nothing_is_written_where_none_is_set()
    {
        var made = HandOverRules.Apply(DriverConfig.Empty, new HandOverEdit { Repository = "web-app", Set = new HandOverSet { Mode = "on", ModeGiven = true } });
        made = HandOverRules.Apply(made, new HandOverEdit { Workspace = "work", Set = new HandOverSet { Mode = "off", ModeGiven = true } });
        made = HelperRules.Apply(made, new HelperEdit { Repository = "web-app", Set = new HelperSet { Mode = "on" } });
        var text = made.ToJson();
        var again = DriverConfig.Parse(text);

        Assert.Equal("""{"mode":"on"}""", HandOverRules.ToJson(again.HandOver["web-app"]));
        Assert.Equal("""{"mode":"off"}""", HandOverRules.ToJson(again.WorkspaceHandOver["work"]));
        Assert.True(text.IndexOf("\"helpers\"", StringComparison.Ordinal) < text.IndexOf("\"handOver\"", StringComparison.Ordinal));
        Assert.DoesNotContain("handOver", DriverConfig.Parse("{}").ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Cell(JsonElement row, int at) => row[at].ValueKind == JsonValueKind.Null ? null : row[at].GetString();

    /// <summary>One kind of the shared table's rows.</summary>
    private static IReadOnlyList<JsonElement> Table(string kind)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "hand-over-rules.json")));
        return [.. document.RootElement.GetProperty(kind).EnumerateArray().Select(row => row.Clone())];
    }
}
