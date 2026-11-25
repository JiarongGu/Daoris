using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The machine's check budgets in the home: the derivation the merge tool shares, the keeper that writes the file beside
/// the tool's own writes without losing any key, and the one door's refusals. The derivation is held to the shared cases
/// (<c>tools/merge/fixtures/check-budget-cases.json</c>), which the tool's tests read too.
/// </summary>
public sealed class CheckBudgetsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-budgets-" + Guid.NewGuid().ToString("N")[..8]);

    public CheckBudgetsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private string File => CheckBudgets.PathOf(_home);

    private JsonObject Written() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(File))!;

    private static JsonElement Cases() =>
        JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(GitSeed.RepoRoot(), "tools", "merge", "fixtures", "check-budget-cases.json"))).RootElement;

    public static TheoryData<string> CaseNames()
    {
        var names = new TheoryData<string>();
        foreach (var each in Cases().GetProperty("cases").EnumerateArray()) names.Add(each.GetProperty("name").GetString()!);
        return names;
    }

    [Fact]
    public void The_product_s_numbers_are_the_shared_fixture_s()
    {
        var constants = Cases().GetProperty("constants");

        Assert.Equal(constants.GetProperty("factor").GetDouble(), CheckBudgets.Factor);
        Assert.Equal(constants.GetProperty("floorMinutes").GetInt32(), CheckBudgets.FloorMinutes);
        Assert.Equal(constants.GetProperty("greensNeeded").GetInt32(), CheckBudgets.GreensNeeded);
        Assert.Equal(constants.GetProperty("greensKept").GetInt32(), CheckBudgets.GreensKept);
        Assert.Equal(constants.GetProperty("riseBound").GetDouble(), CheckBudgets.RiseBound);
        Assert.Equal(constants.GetProperty("mergeDefault").GetInt32(), CheckBudgets.MergeDefaultMinutes);
        Assert.Equal(constants.GetProperty("fullDefault").GetInt32(), CheckBudgets.FullDefaultMinutes);
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Each_shared_case_gives_the_budget_the_tool_gives(string name)
    {
        var each = Cases().GetProperty("cases").EnumerateArray().Single(found => found.GetProperty("name").GetString() == name);
        var greens = each.GetProperty("greens").EnumerateArray().Select(minutes => new GreenRun(minutes.GetDouble() * 60_000, "2026-11-20T00:00:00Z")).ToList();
        var pin = each.TryGetProperty("pin", out var pinned) ? new GatePin(pinned.GetProperty("minutes").GetDouble(), pinned.GetProperty("why").GetString()!) : null;
        double? said = each.TryGetProperty("said", out var told) ? told.GetDouble() : null;

        var budget = CheckBudgets.TimeoutOf(new GateKept(greens, pin, null, false, said), each.GetProperty("lane").GetString());

        var expect = each.GetProperty("expect");
        Assert.Equal(expect.GetProperty("minutes").GetDouble(), budget.Minutes);
        Assert.Equal(expect.GetProperty("source").GetString(), budget.Source);
        Assert.Equal(expect.TryGetProperty("why", out var why) ? why.GetString() : null, budget.Why);
        if (expect.TryGetProperty("rise", out var rise))
        {
            Assert.Equal((rise.GetProperty("from").GetDouble(), rise.GetProperty("to").GetDouble()), (budget.Rise!.From, budget.Rise.To));
        }
        else
        {
            Assert.Null(budget.Rise);
        }
    }

    [Fact]
    public void A_read_with_no_home_no_file_or_no_record_gives_the_product_s_defaults()
    {
        Assert.Same(RepositoryBudgets.None, CheckBudgets.Of(null, "engine"));
        Assert.Same(RepositoryBudgets.None, CheckBudgets.Of(_home, "engine"));
        Assert.Equal(new GateTimeout(5, "default"), CheckBudgets.TimeoutOf(RepositoryBudgets.None.Gate("merge"), "merge"));

        CheckBudgets.SetDevkit(_home, "other", "0.0.1");

        Assert.Same(RepositoryBudgets.None, CheckBudgets.Of(_home, "engine"));
        Assert.Equal("0.0.1", CheckBudgets.Of(_home, "other").Devkit);
    }

    [Fact]
    public void A_file_that_does_not_read_is_refused_by_name_and_never_written_over()
    {
        System.IO.File.WriteAllText(File, "{ not json");

        var read = Assert.Throws<DriverException>(() => CheckBudgets.Of(_home, "engine"));
        Assert.Contains(File, read.Message, StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => CheckBudgets.SetPin(_home, "engine", "merge", 20, "slow disk"));
        Assert.Equal("{ not json", System.IO.File.ReadAllText(File));

        System.IO.File.WriteAllText(File, """{ "schema": 1 }""");
        Assert.Contains("repositories", Assert.Throws<DriverException>(() => CheckBudgets.Of(_home, "engine")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_write_keeps_the_tool_s_greens_and_every_key_it_does_not_model_and_the_other_repositories()
    {
        System.IO.File.WriteAllText(File, """
            { "schema": 1, "future": { "a": 1 },
              "repositories": {
                "engine": { "gates": { "merge": { "green": [{ "ms": 60000, "at": "2026-11-20T00:00:00Z" }], "said": 5, "later": true } }, "overruns": [{ "at": "x" }], "extra": "kept" },
                "game": { "devkit": "0.0.1" } } }
            """);

        CheckBudgets.SetPin(_home, "engine", "merge", 20, "slow disk");

        var written = Written();
        Assert.Equal(1, (int)written["schema"]!);
        Assert.Equal(1, (int)written["future"]!["a"]!);
        var engine = written["repositories"]!["engine"]!;
        Assert.Equal("kept", (string)engine["extra"]!);
        Assert.Single(engine["overruns"]!.AsArray());
        var merge = engine["gates"]!["merge"]!;
        Assert.Equal(60000, (int)merge["green"]![0]!["ms"]!);
        Assert.Equal(5, (int)merge["said"]!);
        Assert.True((bool)merge["later"]!);
        Assert.Equal(("20", "slow disk"), (merge["pin"]!["minutes"]!.ToString(), (string)merge["pin"]!["why"]!));
        Assert.Equal("0.0.1", (string)written["repositories"]!["game"]!["devkit"]!);
        Assert.Equal(new GateTimeout(20, "pin", "slow disk"), CheckBudgets.TimeoutOf(CheckBudgets.Of(_home, "engine").Gate("merge"), "merge"));
    }

    [Fact]
    public void A_pin_stands_over_the_learned_value_and_an_unpin_gives_it_back_and_leaves_no_empty_entry()
    {
        System.IO.File.WriteAllText(File, """
            { "schema": 1, "repositories": { "engine": { "gates": { "merge": { "green": [
              { "ms": 600000, "at": "a" }, { "ms": 600000, "at": "b" }, { "ms": 600000, "at": "c" }] } } } } }
            """);
        Assert.Equal(new GateTimeout(15, "learned"), CheckBudgets.TimeoutOf(CheckBudgets.Of(_home, "engine").Gate("merge"), "merge"));

        CheckBudgets.SetPin(_home, "engine", "merge", 40, "the gate is slow on this disk");
        Assert.Equal(new GateTimeout(40, "pin", "the gate is slow on this disk"), CheckBudgets.TimeoutOf(CheckBudgets.Of(_home, "engine").Gate("merge"), "merge"));

        CheckBudgets.ClearPin(_home, "engine", "merge");
        Assert.Equal(new GateTimeout(15, "learned"), CheckBudgets.TimeoutOf(CheckBudgets.Of(_home, "engine").Gate("merge"), "merge"));

        CheckBudgets.ClearPin(_home, "engine", "ghost");
        Assert.Null(Written()["repositories"]!["engine"]!["gates"]!["ghost"]);
    }

    [Theory]
    [InlineData(0, "slow", "at least 1")]
    [InlineData(2.5, "slow", "whole number")]
    [InlineData(20, " ", "--why")]
    [InlineData(20, null, "--why")]
    public void A_pin_is_whole_minutes_with_its_reason_or_it_is_refused_and_nothing_is_written(double minutes, string? why, string said)
    {
        var refused = Assert.Throws<DriverException>(() => CheckBudgets.SetPin(_home, "engine", "merge", minutes, why));

        Assert.Contains(said, refused.Message, StringComparison.Ordinal);
        Assert.False(System.IO.File.Exists(File));
    }

    [Fact]
    public void The_machine_s_other_members_are_set_and_taken_away_one_by_one()
    {
        CheckBudgets.SetMemoryGb(_home, "engine", "merge", 12);
        CheckBudgets.SetAlone(_home, "engine", "merge", true);
        CheckBudgets.SetSetTimeout(_home, "engine", "cli/unit", 30_000);
        CheckBudgets.SetTreeBaseline(_home, "engine", " node tools/base.mjs ");
        CheckBudgets.SetDevkit(_home, "engine", "0.0.1");
        CheckBudgets.SetLocalInputs(_home, "engine", ["local\\a.txt", "local/b.txt", "local/a.txt"]);
        CheckBudgets.SetPatternsFile(_home, "engine", "local/patterns.txt");

        var kept = CheckBudgets.Of(_home, "engine");
        Assert.Equal(12d, kept.Gate("merge").MemoryGb);
        Assert.True(kept.Gate("merge").Alone);
        Assert.Equal(30_000, kept.SetTimeoutMs["cli/unit"]);
        Assert.Equal(("node tools/base.mjs", "0.0.1", "local/patterns.txt"), (kept.TreeBaseline, kept.Devkit, kept.PatternsFile));
        Assert.Equal(["local/a.txt", "local/b.txt"], kept.LocalInputs);

        CheckBudgets.SetMemoryGb(_home, "engine", "merge", null);
        CheckBudgets.SetAlone(_home, "engine", "merge", false);
        CheckBudgets.SetSetTimeout(_home, "engine", "cli/unit", null);
        CheckBudgets.SetTreeBaseline(_home, "engine", null);
        CheckBudgets.SetDevkit(_home, "engine", " ");
        CheckBudgets.SetLocalInputs(_home, "engine", null);
        CheckBudgets.SetPatternsFile(_home, "engine", null);

        var empty = CheckBudgets.Of(_home, "engine");
        Assert.Empty(empty.Gates);
        Assert.Empty(empty.SetTimeoutMs);
        Assert.Null(empty.TreeBaseline);
        Assert.Null(empty.Devkit);
        Assert.Null(empty.LocalInputs);
        Assert.Null(empty.PatternsFile);
    }

    [Fact]
    public void An_empty_list_of_inputs_is_the_owner_s_word_that_there_are_none_and_is_not_a_missing_member()
    {
        CheckBudgets.SetLocalInputs(_home, "engine", []);

        Assert.Empty(CheckBudgets.Of(_home, "engine").LocalInputs!);
    }

    [Theory]
    [InlineData("secrets/token.txt")]
    [InlineData("local/../token.txt")]
    [InlineData("local//token.txt")]
    [InlineData("local/c:token.txt")]
    [InlineData("local/")]
    public void An_input_or_a_patterns_file_that_is_no_file_under_local_is_refused(string path)
    {
        Assert.Contains("`local/`", Assert.Throws<DriverException>(() => CheckBudgets.SetLocalInputs(_home, "engine", [path])).Message, StringComparison.Ordinal);
        Assert.Throws<DriverException>(() => CheckBudgets.SetPatternsFile(_home, "engine", path));
        Assert.False(System.IO.File.Exists(File));
    }

    private static CheckBudgetsAsk Ask(string member, string? name, string? json, bool clear = false, string? why = null) =>
        new("engine", member, name, json is null ? null : JsonDocument.Parse(json).RootElement.Clone(), clear, why);

    [Fact]
    public void The_door_sets_each_member_through_the_one_judge_and_answers_the_record_with_each_gate_s_budget()
    {
        CheckBudgetsDoor.Apply(_home, Ask("pin", "merge", "33", why: "slow disk"));
        CheckBudgetsDoor.Apply(_home, Ask("memoryGb", "merge", "8"));
        CheckBudgetsDoor.Apply(_home, Ask("alone", "merge", "true"));
        CheckBudgetsDoor.Apply(_home, Ask("timeoutMs", "cli/unit", "45000"));
        CheckBudgetsDoor.Apply(_home, Ask("treeBaseline", null, "\"node base.mjs\""));
        CheckBudgetsDoor.Apply(_home, Ask("devkit", null, "\"0.0.1\""));
        CheckBudgetsDoor.Apply(_home, Ask("patternsFile", null, "\"local/p.txt\""));
        var view = CheckBudgetsDoor.Apply(_home, Ask("localInputs", null, """["local/a.txt"]"""), lane: "merge");

        var merge = view.Gates["merge"];
        Assert.Equal(new GateTimeout(33, "pin", "slow disk"), merge.Budget);
        Assert.Equal(8d, merge.MemoryGb);
        Assert.True(merge.Alone);
        Assert.Equal(45_000, view.SetTimeoutMs["cli/unit"]);
        Assert.Equal(("node base.mjs", "0.0.1", "local/p.txt"), (view.TreeBaseline, view.Devkit, view.PatternsFile));
        Assert.Equal(["local/a.txt"], view.LocalInputs);

        var cleared = CheckBudgetsDoor.Apply(_home, Ask("pin", "merge", null, clear: true));
        Assert.Equal(new GateTimeout(5, "default"), cleared.Gates["merge"].Budget);
    }

    [Fact]
    public void The_view_names_the_declared_gates_it_is_asked_for_each_with_the_lane_s_default()
    {
        var view = CheckBudgetsDoor.View(_home, "engine", ["merge", "full"], "full");

        Assert.Equal(["full", "merge"], view.Gates.Keys);
        Assert.All(view.Gates.Values, gate => Assert.Equal(new GateTimeout(10, "default"), gate.Budget));
        Assert.Null(view.LocalInputs);
    }

    [Theory]
    [InlineData("", "pin", "merge", "20", "name one")]
    [InlineData("engine", "verifyBudget", null, "1", "no member")]
    [InlineData("engine", "pin", "", "20", "name the gate")]
    [InlineData("engine", "pin", "merge", "\"fast\"", "takes a number")]
    [InlineData("engine", "alone", "merge", "1", "true or false")]
    [InlineData("engine", "timeoutMs", "cli/unit", "1.5", "whole number")]
    [InlineData("engine", "localInputs", null, "[1]", "list of paths")]
    [InlineData("engine", "devkit", null, "7", "takes text")]
    public void The_door_refuses_what_it_does_not_take_and_writes_nothing(string repository, string member, string? name, string json, string said)
    {
        var ask = new CheckBudgetsAsk(repository, member, name, JsonDocument.Parse(json).RootElement.Clone(), false, "because");

        Assert.Contains(said, Assert.Throws<DriverException>(() => CheckBudgetsDoor.Apply(_home, ask)).Message, StringComparison.Ordinal);
        Assert.False(System.IO.File.Exists(File));
    }

    [Fact]
    public void The_door_refuses_a_pin_with_no_reason_alike()
    {
        Assert.Contains("--why", Assert.Throws<DriverException>(() => CheckBudgetsDoor.Apply(_home, Ask("pin", "merge", "20"))).Message, StringComparison.Ordinal);
        Assert.False(System.IO.File.Exists(File));
    }
}
