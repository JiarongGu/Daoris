using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1f part f1 (the integration design §5.9; D192's runner contract, extended; D215's notes on INTEGRATE1j): a repository's runner
/// answer read as D192 judges it, and more. The answer is unknown, never a pass, where the file is missing, does not read or names another
/// schema, names another commit, base or declaration blob than the accepted one, names a gate the accepted declaration does not declare, or
/// lacks a gate its plan named or holds one ended or not-run; promotion consumes only a run for the exact tip and base under the accepted
/// blob, every planned gate pass or flake beside the plan's kept. Held to ONE table, <c>fixtures/runner-answers.json</c>, whose answers the
/// merge tool writes (the CLI's tool test <c>integration-runner-answers.test.ts</c> builds each piece with its own writers). Text
/// only, no file and no process.
/// </summary>
public sealed class RunnerAnswerTests
{
    private static readonly JsonElement Table = JsonDocument.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "runner-answers.json"))).RootElement;

    private static TheoryData<int> Indexes(string section)
    {
        var data = new TheoryData<int>();
        for (var at = 0; at < Table.GetProperty(section).GetArrayLength(); at += 1) data.Add(at);
        return data;
    }

    public static TheoryData<int> ReadRows() => Indexes("reads");

    public static TheoryData<int> StandingRows() => Indexes("standings");

    public static TheoryData<int> PromotionRows() => Indexes("promotions");

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonNode? Node(JsonElement element) => JsonNode.Parse(element.GetRawText());

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(value => (JsonNode?)JsonValue.Create(value))]);

    private static RunnerAsk AskOf(string name)
    {
        var ask = Table.GetProperty("asks").GetProperty(name);
        return new RunnerAsk(Text(ask, "commit")!, [.. ask.GetProperty("declared").EnumerateArray().Select(each => each.GetString()!)])
        {
            Mode = Text(ask, "mode"),
            Base = Text(ask, "base"),
            Declaration = Text(ask, "declaration"),
            Gates = ask.TryGetProperty("gates", out var gates) ? [.. gates.EnumerateArray().Select(each => each.GetString()!)] : null,
        };
    }

    /// <summary>The answer a row names, the properties of <c>with</c> set over it (a null removing one), as the text a runner would have written; null where the row says the file is missing.</summary>
    private static string? TextOf(JsonElement row)
    {
        if (row.TryGetProperty("missing", out var missing) && missing.GetBoolean()) return null;
        if (Text(row, "raw") is { } raw) return raw;
        var answer = (JsonObject)Node(Table.GetProperty("answers").GetProperty(Text(row, "answer")!))!;
        if (row.TryGetProperty("with", out var overrides))
        {
            foreach (var each in overrides.EnumerateObject())
            {
                if (each.Value.ValueKind == JsonValueKind.Null) answer.Remove(each.Name);
                else answer[each.Name] = Node(each.Value);
            }
        }

        return answer.ToJsonString();
    }

    private static string Short(string id) => id.Length > 7 ? id[..7] : id;

    private static string Entry(RunnerPlanEntry each) => each.Standing is null ? each.Name : $"{each.Name}:{each.Standing}";

    private static string GateText(RunnerGate gate) => $"{gate.Name}:{gate.Verdict}";

    private static IEnumerable<string> FailureTexts(IEnumerable<RunnerGate> gates) => gates.SelectMany(gate => gate.Failures.Select(failure => $"{gate.Name}/{failure.Test}"));

    private static JsonObject Project(RunnerRead read)
    {
        var projected = new JsonObject { ["kind"] = read.Kind.ToString().ToLowerInvariant() };
        switch (read.Kind)
        {
            case RunnerKind.Unknown:
                projected["why"] = read.Why;
                break;
            case RunnerKind.Gates:
                projected["gates"] = Strings(read.Gates.Select(GateText));
                projected["failures"] = Strings(FailureTexts(read.Gates));
                break;
            case RunnerKind.Run:
                projected["gates"] = Strings(read.Gates.Select(GateText));
                projected["failures"] = Strings(FailureTexts(read.Gates));
                projected["kept"] = Strings(read.Kept.Select(Entry));
                projected["deferred"] = Strings(read.Deferred.Select(each => each.Name));
                break;
            case RunnerKind.Plan:
                projected["run"] = Strings(read.Run.Select(each => each.Name));
                projected["kept"] = Strings(read.Kept.Select(Entry));
                projected["deferred"] = Strings(read.Deferred.Select(each => each.Name));
                break;
            case RunnerKind.Probe:
                projected["result"] = read.ProbeResult;
                projected["signature"] = read.ProbeSignature is { } signature ? signature.Test ?? signature.Gate : null;
                break;
            case RunnerKind.Commits:
                projected["problems"] = Strings(read.Problems);
                break;
        }

        return projected;
    }

    [Theory]
    [MemberData(nameof(ReadRows))]
    public void A_runner_answer_is_read_as_the_shared_table_says(int at)
    {
        var row = Table.GetProperty("reads")[at];
        var read = RunnerAnswers.Read(TextOf(row), AskOf(Text(row, "ask")!));
        var projected = Project(read);
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{Text(row, "name")}: {projected.ToJsonString()}");
        // Unknown is never a pass: nothing of an unknown answer is a gate that passed.
        if (read.Kind == RunnerKind.Unknown) Assert.Empty(read.Gates);
    }

    private static RunnerGate GateOf(JsonElement gate) => new(Text(gate, "name")!, Text(gate, "verdict")!, gate.TryGetProperty("exit", out var exit) ? exit.GetInt32() : null);

    [Theory]
    [MemberData(nameof(StandingRows))]
    public void The_gates_alone_are_judged_as_D192_judges_them_per_the_shared_table(int at)
    {
        var row = Table.GetProperty("standings")[at];
        var standing = RunnerAnswers.Standing(
            [.. row.GetProperty("gates").EnumerateArray().Select(GateOf)], [.. row.GetProperty("required").EnumerateArray().Select(each => each.GetString()!)]);
        var projected = new JsonObject { ["kind"] = standing.Kind.ToString().ToLowerInvariant() };
        if (standing.Why is not null) projected["why"] = standing.Why;
        if (standing.Kind == RunnerStandingKind.Reached) projected["flaked"] = Strings(standing.Flaked);
        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{Text(row, "name")}: {projected.ToJsonString()}");
    }

    private static RunnerRead ReadOf(JsonElement part, string defaultAsk)
    {
        var row = part;
        return RunnerAnswers.Read(TextOf(row), AskOf(Text(part, "ask") ?? defaultAsk));
    }

    [Theory]
    [MemberData(nameof(PromotionRows))]
    public void Promotion_consumes_only_a_run_for_the_exact_tip_and_base_as_the_shared_table_says(int at)
    {
        var row = Table.GetProperty("promotions")[at];
        var plan = RunnerAnswers.Read(TextOf(row.GetProperty("plan")), AskOf("plan"));
        var run = RunnerAnswers.Read(TextOf(row.GetProperty("run")), AskOf(Text(row, "askRun") ?? "run"));
        var promoted = RunnerAnswers.Promotion(plan, run);
        var projected = new JsonObject { ["kind"] = promoted.Kind.ToString().ToLowerInvariant() };
        if (promoted.Why is not null) projected["why"] = promoted.Why;
        if (promoted.Kind is RunnerStandingKind.Reached or RunnerStandingKind.Failed)
        {
            projected["flaked"] = Strings(promoted.Flaked);
            projected["kept"] = Strings(promoted.Kept.Select(Entry));
            projected["failures"] = Strings(promoted.Failures.Select(failure => $"{failure.Gate}/{failure.Test}"));
        }

        Assert.True(JsonNode.DeepEquals(Node(row.GetProperty("expect")), projected), $"{Text(row, "name")}: {projected.ToJsonString()}");
        // Only a reached run promotes.
        Assert.Equal(promoted.Kind == RunnerStandingKind.Reached, promoted.Promotes);
    }

    [Fact]
    public void A_failure_of_a_gate_is_handed_to_the_judge_as_a_failure_naming_a_test_or_only_its_gate()
    {
        const string answer = """
            {"schema":2,"mode":"run","commit":"a1b2c3d4e5f60718293a4b5c6d7e8f9012345678","base":"0f1e2d3c4b5a69788796a5b4c3d2e1f001122334","declaration":"9a8b7c6d5e4f30211203f4e5d6c7b8a998877665","gates":[
              {"name":"driver","verdict":"fail","exit":1,"failures":[{"unit":"N.T","test":"N.T.M","signature":{"test":"N.T.M","exception":"System.Exception","message":"boom"}}]},
              {"name":"universal","verdict":"fail","exit":2,"failures":[{"unit":"universal","test":"universal","signature":{"gate":"universal","step":"check","message":"docs/README.md: error: a broken link"}}]}
            ],"kept":[],"deferred":[]}
            """;
        var test = RunnerAnswers.Read(answer, AskOf("run"));
        var judged = test.Gates.SelectMany(gate => gate.Failures.Select(failure => failure.ToJudged(gate.Name))).ToList();
        Assert.Equal([(FailureNames.Test, "driver", "N.T.M"), (FailureNames.Gate, "universal", null)], judged.Select(each => (each.Names, each.Gate, each.Test)));
        Assert.Equal(["N.T.M", "universal"], judged.Select(each => each.Label));
    }

    [Fact]
    public void The_table_holds_its_rows()
    {
        Assert.True(Table.GetProperty("reads").GetArrayLength() >= 40);
        Assert.True(Table.GetProperty("standings").GetArrayLength() >= 10);
        Assert.True(Table.GetProperty("promotions").GetArrayLength() >= 12);
        Assert.Equal(Short(Table.GetProperty("ids").GetProperty("tip").GetString()!), Short(AskOf("run").Commit));
    }
}
