using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PROCESS1a (D164's amendment point 1): the commit rule, <c>commits</c> and <c>workspaceCommits</c> in <c>driver.json</c> — read,
/// refused, resolved, said and edited, held cell for cell to ONE table, <c>fixtures/commit-rules.json</c> (the CLI's twin,
/// <c>commits.ts</c>, left with the driver family's TypeScript in PUSH1f2b). A cell's <c>CONV400</c> and
/// <c>CONV401</c> stand for a convention of that many characters.
/// </summary>
public sealed class CommitRulesTests
{
    public static TheoryData<string, string, string, string?, string, string> ReadRows() => Rows(
        "read", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4)!, Cell(row, 5)!));

    [Theory]
    [MemberData(nameof(ReadRows))]
    public void A_commit_rule_resolves_as_the_cli_resolves_it(
        string name, string file, string repository, string? workspace, string source, string rule)
    {
        var resolved = CommitRules.Resolve(DriverConfig.Parse(file), repository, workspace);

        Assert.True(source == resolved.Source, $"{name}: from {resolved.Source}");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(rule), JsonNode.Parse(CommitRules.ToJson(resolved.Rule))), $"{name}: the rule");
    }

    public static TheoryData<string, string, string?> ProblemRows() => Rows(
        "problems", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)));

    [Theory]
    [MemberData(nameof(ProblemRows))]
    public void A_commit_rule_s_first_problem_is_the_cli_s_in_its_words(string name, string value, string? problem)
    {
        using var document = JsonDocument.Parse(value);
        var read = CommitRules.Read(document.RootElement);

        Assert.True(problem == read.Problem, $"{name}: {read.Problem}");
    }

    public static TheoryData<string, string, string> ListedRows() => Rows(
        "listed", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!));

    [Theory]
    [MemberData(nameof(ListedRows))]
    public void A_rule_is_listed_in_the_cli_s_words(string name, string rule, string said)
    {
        using var document = JsonDocument.Parse(rule);

        Assert.True(said == CommitRules.Listed(CommitRules.Read(document.RootElement).Rule!), name);
    }

    public static TheoryData<string, string, string, string?, string?> EditRows() => Rows(
        "edits", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4)));

    [Theory]
    [MemberData(nameof(EditRows))]
    public void A_commit_edit_writes_what_the_cli_writes_or_refuses_in_its_words(
        string name, string file, string edit, string? after, string? refusal)
    {
        var config = DriverConfig.Parse(file);
        using var document = JsonDocument.Parse(edit);
        var change = CommitRules.EditOf(document.RootElement);
        if (refusal is not null)
        {
            Assert.Equal(refusal, Assert.Throws<DriverException>(() => CommitRules.Apply(config, change)).Message);
            return;
        }

        var written = JsonNode.Parse(CommitRules.Apply(config, change).ToJson())!.AsObject();
        var maps = new JsonObject();
        foreach (var key in new[] { "commits", "workspaceCommits" })
        {
            if (written[key] is { } map) maps[key] = map.DeepClone();
        }

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after!), maps), $"{name}: {maps.ToJsonString()}");
    }

    /// <summary>The rule round-trips through the file, kept exactly, and a driver.json that names none writes none.</summary>
    [Fact]
    public void A_rule_is_kept_as_written_and_nothing_is_written_where_none_is_set()
    {
        var again = DriverConfig.Parse(DriverConfig.Parse("""{"commits":{"web-app":{"mode":"ask","convention":"Short subject."}},"workspaceCommits":{"work":{"mode":"at-end"}}}""").ToJson());

        Assert.Equal("""{"mode":"ask","convention":"Short subject."}""", CommitRules.ToJson(again.Commits["web-app"]));
        Assert.Equal("""{"mode":"at-end"}""", CommitRules.ToJson(again.WorkspaceCommits["work"]));
        Assert.DoesNotContain("commits", DriverConfig.Parse("{}").ToJson(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Cell(JsonElement row, int at) =>
        row[at].ValueKind == JsonValueKind.Null
            ? null
            : row[at].GetString()!.Replace("CONV400", new string('x', 400)).Replace("CONV401", new string('x', 401));

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

    private static TheoryData<T1, T2, T3, T4, T5, T6> Rows<T1, T2, T3, T4, T5, T6>(string kind, Func<JsonElement, (T1, T2, T3, T4, T5, T6)> cells)
    {
        var data = new TheoryData<T1, T2, T3, T4, T5, T6>();
        foreach (var row in Table(kind))
        {
            var (a, b, c, d, e, f) = cells(row);
            data.Add(a, b, c, d, e, f);
        }

        return data;
    }

    /// <summary>One kind of the shared table's rows.</summary>
    private static IReadOnlyList<JsonElement> Table(string kind)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "commit-rules.json")));
        return [.. document.RootElement.GetProperty(kind).EnumerateArray().Select(row => row.Clone())];
    }
}
