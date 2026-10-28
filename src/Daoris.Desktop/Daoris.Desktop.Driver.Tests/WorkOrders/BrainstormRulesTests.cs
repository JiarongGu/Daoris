using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRAINSTORM1b (D176 point 9): the brainstorm policy, <c>brainstorms</c> and <c>workspaceBrainstorms</c> in <c>driver.json</c> —
/// read, refused, resolved, said and edited, cell for cell, by ONE table, <c>fixtures/brainstorm-rules.json</c> (the CLI's twin,
/// <c>brainstorms.ts</c>, left with the driver family's TypeScript in PUSH1f2b).
/// </summary>
public sealed class BrainstormRulesTests
{
    public static TheoryData<string, string, string?, string, string> ReadRows() => Rows(
        "read", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2), Cell(row, 3)!, Cell(row, 4)!));

    [Theory]
    [MemberData(nameof(ReadRows))]
    public void A_brainstorms_rule_resolves_as_the_cli_resolves_it(string name, string file, string? workspace, string source, string rule)
    {
        var resolved = BrainstormRules.Resolve(DriverConfig.Parse(file), workspace);

        Assert.True(source == resolved.Source, $"{name}: from {resolved.Source}");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rule), JsonNode.Parse(BrainstormRules.ToJson(resolved.Rule))), $"{name}: {BrainstormRules.ToJson(resolved.Rule)}");
    }

    public static TheoryData<string, string, string?> ProblemRows() => Rows(
        "problems", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)));

    [Theory]
    [MemberData(nameof(ProblemRows))]
    public void A_brainstorms_rule_s_first_problem_is_the_cli_s_in_its_words(string name, string value, string? problem)
    {
        using var document = JsonDocument.Parse(value);

        Assert.True(problem == BrainstormRules.Read(document.RootElement).Problem, $"{name}: {BrainstormRules.Read(document.RootElement).Problem}");
    }

    public static TheoryData<string, string, string> ListedRows() => Rows(
        "listed", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!));

    [Theory]
    [MemberData(nameof(ListedRows))]
    public void A_rule_is_listed_in_the_cli_s_words(string name, string rule, string said)
    {
        using var document = JsonDocument.Parse(rule);

        Assert.Equal(said, BrainstormRules.Listed(BrainstormRules.Read(document.RootElement).Rule!));
        _ = name;
    }

    public static TheoryData<string, string, string, string?, string?> EditRows() => Rows(
        "edits", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4)));

    [Theory]
    [MemberData(nameof(EditRows))]
    public void A_brainstorms_edit_writes_what_the_cli_writes_or_refuses_in_its_words(
        string name, string file, string edit, string? after, string? refusal)
    {
        var config = DriverConfig.Parse(file);
        using var document = JsonDocument.Parse(edit);
        var change = BrainstormRules.EditOf(document.RootElement);
        if (refusal is not null)
        {
            Assert.Equal(refusal, Assert.Throws<DriverException>(() => BrainstormRules.Apply(config, change)).Message);
            return;
        }

        var written = JsonNode.Parse(BrainstormRules.Apply(config, change).ToJson())!.AsObject();
        var kept = new JsonObject();
        foreach (var key in new[] { "brainstorms", "workspaceBrainstorms" })
        {
            if (written[key] is { } value) kept[key] = value.DeepClone();
        }

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after!), kept), $"{name}: {kept.ToJsonString()}");
    }

    /// <summary>The rule round-trips through the file, kept whole, and a driver.json that names none writes none.</summary>
    [Fact]
    public void A_rule_is_kept_whole_and_nothing_is_written_where_none_is_set()
    {
        var again = DriverConfig.Parse(DriverConfig.Parse("""{"brainstorms":{"agents":["dsh"]},"workspaceBrainstorms":{"work":{"mode":"off","minutes":{"pass":5}}}}""").ToJson());

        Assert.Equal("""{"mode":"ask","agents":["dsh"],"minutes":{"pass":10,"synthesis":10,"challenge":10,"request":30}}""", BrainstormRules.ToJson(again.Brainstorms!));
        Assert.Equal("""{"mode":"off","agents":[],"minutes":{"pass":5,"synthesis":10,"challenge":10,"request":30}}""", BrainstormRules.ToJson(again.WorkspaceBrainstorms["work"]));
        Assert.DoesNotContain("brainstorms", DriverConfig.Parse("{}").ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Cell(JsonElement row, int at) => row[at].ValueKind == JsonValueKind.Null ? null : row[at].GetString();

    private static TheoryData<T1, T2, T3> Rows<T1, T2, T3>(string kind, Func<JsonElement, (T1, T2, T3)> cells)
    {
        var data = new TheoryData<T1, T2, T3>();
        foreach (var row in Table(kind))
        {
            var (a, b, c) = cells(row);
            data.Add(a, b, c);
        }

        return data;
    }

    private static TheoryData<T1, T2, T3, T4, T5> Rows<T1, T2, T3, T4, T5>(string kind, Func<JsonElement, (T1, T2, T3, T4, T5)> cells)
    {
        var data = new TheoryData<T1, T2, T3, T4, T5>();
        foreach (var row in Table(kind))
        {
            var (a, b, c, d, e) = cells(row);
            data.Add(a, b, c, d, e);
        }

        return data;
    }

    /// <summary>One kind of the shared table's rows.</summary>
    private static IReadOnlyList<JsonElement> Table(string kind)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "brainstorm-rules.json")));
        return [.. document.RootElement.GetProperty(kind).EnumerateArray().Select(row => row.Clone())];
    }
}
