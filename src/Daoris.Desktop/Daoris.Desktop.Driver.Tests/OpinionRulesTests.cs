using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1a (D155 point 3, the second-agent design §2.2–§2.6): the second-opinion rule, <c>opinions</c> and
/// <c>workspaceOpinions</c> in <c>driver.json</c> — read, refused, resolved, said and edited. Nothing here chooses a reviewer
/// or starts a process: the rule is declared only.
/// </summary>
/// <remarks>
/// The driver's half of a TWIN with the CLI's <c>opinions.ts</c>: both are held, cell for cell, to ONE table, this suite's
/// <c>fixtures/opinion-rules.json</c>, which <c>driverconfig.test.ts</c> reads too. A row changed on one side alone is the
/// other side's failure.
/// </remarks>
public sealed class OpinionRulesTests
{
    public static TheoryData<string, string, string, string?, string?, string?> ReadRows() => Rows(
        "read", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4), Cell(row, 5)));

    [Theory]
    [MemberData(nameof(ReadRows))]
    public void An_opinion_rule_resolves_as_the_cli_resolves_it(
        string name, string file, string repository, string? workspace, string? source, string? rule)
    {
        var resolved = OpinionRules.Resolve(DriverConfig.Parse(file), repository, workspace);

        Assert.True(source == resolved?.Source, $"{name}: from {resolved?.Source ?? "nothing"}");
        Assert.True(Same(rule, resolved is null ? null : OpinionRules.ToJson(resolved.Rule)), $"{name}: the rule");
    }

    public static TheoryData<string, string, string?> ProblemRows() => Rows(
        "problems", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)));

    [Theory]
    [MemberData(nameof(ProblemRows))]
    public void An_opinion_rule_s_first_problem_is_the_cli_s_in_its_words(string name, string value, string? problem)
    {
        using var document = JsonDocument.Parse(value);

        Assert.True(problem == OpinionRules.Read(document.RootElement).Problem, $"{name}: {OpinionRules.Read(document.RootElement).Problem}");
    }

    public static TheoryData<string, string, string[], string[]> SaysRows() => Rows(
        "says", row => (Cell(row, 0)!, Cell(row, 1)!, Strings(row[2]), Strings(row[3])));

    [Theory]
    [MemberData(nameof(SaysRows))]
    public void Each_door_says_what_a_rule_lets_a_reviewer_do_in_the_cli_s_words(string name, string rule, string[] sameAgent, string[] sentences)
    {
        using var document = JsonDocument.Parse(rule);
        var read = OpinionRules.Read(document.RootElement);

        Assert.True(read.Problem is null, $"{name}: {read.Problem}");
        Assert.Equal(sentences, OpinionRules.Says(read.Rule!, sameAgent));
    }

    public static TheoryData<string, string, string, string?, string?> EditRows() => Rows(
        "edits", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, Cell(row, 3), Cell(row, 4)));

    [Theory]
    [MemberData(nameof(EditRows))]
    public void An_opinion_edit_writes_what_the_cli_writes_or_refuses_in_its_words(
        string name, string file, string edit, string? after, string? refusal)
    {
        var config = DriverConfig.Parse(file);
        using var document = JsonDocument.Parse(edit);
        var change = OpinionRules.EditOf(document.RootElement);
        if (refusal is not null)
        {
            Assert.Equal(refusal, Assert.Throws<DriverException>(() => OpinionRules.Apply(config, change)).Message);
            return;
        }

        var written = JsonNode.Parse(OpinionRules.Apply(config, change).ToJson())!.AsObject();
        var maps = new JsonObject();
        foreach (var key in new[] { "opinions", "workspaceOpinions" })
        {
            if (written[key] is { } map) maps[key] = map.DeepClone();
        }

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(after!), maps), $"{name}: {maps.ToJsonString()}");
    }

    public static TheoryData<string, string, string, bool> FamilyRows() => Rows(
        "families", row => (Cell(row, 0)!, Cell(row, 1)!, Cell(row, 2)!, row[3].GetBoolean()));

    /// <summary>
    /// Design §3.1: one family is one owner (AGT7) or one declared maker, read from the adapters this build carries, as the
    /// CLI reads its own toolchain table. Which reviewers a door names as the working agent's own comes from this.
    /// </summary>
    [Theory]
    [MemberData(nameof(FamilyRows))]
    public void Two_agents_are_one_family_as_the_cli_judges_them(string name, string working, string reviewer, bool same)
    {
        Assert.True(same == OpinionRules.OneFamily(working, reviewer), name);
        Assert.Equal(same ? [reviewer] : [], OpinionRules.SameAgent(new OpinionRule([reviewer], [OpinionRules.Landing]), working));
    }

    /// <summary>The file a door writes is the file the table reads: written only once set, and read back the same.</summary>
    [Fact]
    public void A_rule_is_written_only_once_set_and_read_back_as_written()
    {
        Assert.DoesNotContain("opinions", DriverConfig.Empty.ToJson(), StringComparison.OrdinalIgnoreCase);

        var config = OpinionRules.Apply(DriverConfig.Empty, new OpinionEdit
        {
            Repository = "web-app",
            Set = new OpinionSet { Reviewers = ["codex-acp"], On = ["landing", "steps"], Verify = true, Minutes = 30 },
        });
        config = OpinionRules.Apply(config, new OpinionEdit { Repository = "notes-site", None = true });
        config = OpinionRules.Apply(config, new OpinionEdit { Workspace = "work", Set = new OpinionSet { Reviewers = ["codex-acp", "dsh"], Required = true } });

        var again = DriverConfig.Parse(config.ToJson());
        Assert.Equal(OpinionRules.ToJson(config.Opinions["web-app"]), OpinionRules.ToJson(again.Opinions["WEB-APP"]));
        Assert.True(again.Opinions["notes-site"].IsNone);
        Assert.Equal("""{"on":["landing"],"reviewers":["codex-acp","dsh"],"required":true}""", OpinionRules.ToJson(again.WorkspaceOpinions["work"]));
    }

    /// <summary>
    /// The room's cell (design §2.5–§2.6): what Ask Daoris's room shows of the rule standing for a repository, and
    /// <c>no second opinion</c> where nothing is set.
    /// </summary>
    [Fact]
    public void The_room_names_each_reviewer_when_it_reads_and_where_it_was_set()
    {
        Assert.Equal("no second opinion", OpinionRules.RoomCell(null));
        Assert.Equal("no second opinion (set for it)", OpinionRules.RoomCell(new ResolvedOpinion(OpinionRule.None, OpinionSource.Repository)));
        Assert.Equal(
            "`codex-acp`, else `dsh`; before landing and each next step; required; may build and run what is declared safe; "
            + "at most 30 minutes a pass; no recheck (its workspace's rule)",
            OpinionRules.RoomCell(new ResolvedOpinion(
                new OpinionRule(["codex-acp", "dsh"], ["landing", "steps"], Required: true, Verify: true, Minutes: 30, Recheck: false),
                OpinionSource.Workspace)));
        Assert.Equal(
            "`codex-acp`; before each next step; at most 20 minutes a pass (set for it)",
            OpinionRules.RoomCell(new ResolvedOpinion(new OpinionRule(["codex-acp"], ["steps"]), OpinionSource.Repository)));
    }

    private static bool Same(string? expected, string? actual) =>
        expected is null || actual is null ? expected == actual : JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual));

    private static string? Cell(JsonElement row, int at) => row[at].ValueKind == JsonValueKind.Null ? null : row[at].GetString();

    private static string[] Strings(JsonElement cell) => [.. cell.EnumerateArray().Select(each => each.GetString()!)];

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

    private static TheoryData<T1, T2, T3, T4> Rows<T1, T2, T3, T4>(string kind, Func<JsonElement, (T1, T2, T3, T4)> cells)
    {
        var data = new TheoryData<T1, T2, T3, T4>();
        foreach (var row in Table(kind))
        {
            var (a, b, c, d) = cells(row);
            data.Add(a, b, c, d);
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
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "opinion-rules.json")));
        return [.. document.RootElement.GetProperty(kind).EnumerateArray().Select(row => row.Clone())];
    }
}
