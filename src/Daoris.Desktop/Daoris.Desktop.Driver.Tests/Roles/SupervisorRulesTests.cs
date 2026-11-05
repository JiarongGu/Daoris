using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1c (the context counters design §4.1, §4.4, §4.5): the rule supervisor's pure half over
/// <c>fixtures/supervisor-rules.json</c>. Every refusal code, a line's effective rules by place, by role and by <c>off</c>, a ladder, the
/// narrowest act, and the two bounds judged on a narrower place's effective set after a wider place's write.
/// </summary>
public sealed class SupervisorRulesTests
{
    private static readonly JsonNode Fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "supervisor-rules.json")))!;

    private static readonly IReadOnlyList<string> Roles = [.. Fixture["roles"]!.AsArray().Select(role => role!.GetValue<string>())];

    internal static IReadOnlyList<SupervisorRule> Rules(JsonNode? node, string place)
    {
        if (node is JsonValue) node = Fixture["base"];
        return [.. node!.AsArray().Select(rule =>
        {
            var each = rule!.AsObject();
            var count = each["count"]!.GetValue<string>();
            return new SupervisorRule(
                each["id"]!.GetValue<string>(), count, each["window"]?.GetValue<string>() ?? SupervisorRules.Windows.GetValueOrDefault(count, "none"),
                each["at"]!.GetValue<int>(), each["do"]!.GetValue<string>(), each["upTo"]?.GetValue<int>(),
                each["place"]?.GetValue<string>() ?? place,
                each["roles"]?.AsArray().Select(role => role!.GetValue<string>()).ToList());
        })];
    }

    private static IReadOnlyList<SupervisorRuleSet> Sets(JsonNode? node) =>
        [.. node!.AsArray().Select(set => new SupervisorRuleSet(
            set!["place"]!.GetValue<string>(), set["revision"]?.GetValue<long>() ?? 1, Rules(set["rules"], set["place"]!.GetValue<string>())))];

    [Fact]
    public void Judge_refuses_a_malformed_set_by_code_and_passes_a_sound_one()
    {
        var failures = new List<string>();
        var codes = new HashSet<string>();
        foreach (var each in Fixture["judge"]!.AsArray())
        {
            var place = each!["place"]!.GetValue<string>();
            var held = each["held"] is { } given ? Sets(given) : [new SupervisorRuleSet("machine", 1, Rules(Fixture["base"], "machine"))];
            var world = new SupervisorRuleWorld(Roles, each["referOffered"]?.GetValue<bool>() ?? false);
            var refusal = SupervisorRules.Judge(place, each["revision"]!.GetValue<long>(), Rules(each["rules"], place), held, world);
            var expect = each["expect"]?.GetValue<string>();
            var name = each["name"]!.GetValue<string>();
            if (refusal?.Code != expect) failures.Add($"{name}: expected {expect ?? "none"}, got {refusal?.Code ?? "none"} ({refusal?.Message})");
            else if (expect is not null)
            {
                codes.Add(expect);
                if (each["expectPlace"]?.GetValue<string>() is { } at && refusal!.Place != at) failures.Add($"{name}: named {refusal.Place}, expected {at}");
                if (each["expectRole"]?.GetValue<string>() is { } role && refusal!.Role != role) failures.Add($"{name}: named role {refusal.Role}, expected {role}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.Equal(SupervisorRules.Codes.Order(), codes.Order());
    }

    [Fact]
    public void Effective_reads_each_count_from_the_most_specific_place_that_applies_to_the_role()
    {
        var failures = new List<string>();
        foreach (var each in Fixture["effective"]!.AsArray())
        {
            var chain = SupervisorRules.Chain(each!["line"]?.GetValue<string>(), each["repository"]?.GetValue<string>(), each["workspace"]?.GetValue<string>());
            var effective = SupervisorRules.Effective(Sets(each["sets"]), chain, each["role"]?.GetValue<string>());
            var actual = effective.ToDictionary(pair => pair.Key, pair => string.Join(",", pair.Value.Select(rule => rule.Id)));
            var expected = each["expect"]!.AsObject().ToDictionary(pair => pair.Key, pair => string.Join(",", pair.Value!.AsArray().Select(id => id!.GetValue<string>())));
            if (!actual.OrderBy(pair => pair.Key).SequenceEqual(expected.OrderBy(pair => pair.Key)))
            {
                failures.Add($"{each["name"]}: expected {JsonSerializer.Serialize(expected)}, got {JsonSerializer.Serialize(actual)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Decide_acts_on_the_highest_at_reached_and_the_narrowest_act_of_the_counts()
    {
        var failures = new List<string>();
        foreach (var each in Fixture["decide"]!.AsArray())
        {
            var rules = Rules(each!["rules"], "machine");
            var effective = rules.GroupBy(rule => rule.Count).ToDictionary(group => group.Key, group => (IReadOnlyList<SupervisorRule>)[.. group.OrderBy(rule => rule.At)]);
            var counts = each["counts"]!.AsArray().Select(count => new RuleCountRead(
                count!["count"]!.GetValue<string>(), count["n"]?.GetValue<int>(), (count["known"]?.GetValue<string>() ?? "exact") != "unknown")).ToList();
            var act = SupervisorRules.Decide(counts, effective);
            var expect = each["expect"]?.GetValue<string>();
            if (act?.Rule.Id != expect) failures.Add($"{each["name"]}: expected {expect ?? "none"}, got {act?.Rule.Id ?? "none"}");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void A_place_the_world_names_wider_is_read_by_the_bounds_of_its_lines()
    {
        // A line under repository:daoris also reads workspace:w1, which holds a move and no moves rule once the machine's moves rule is gone.
        var world = new SupervisorRuleWorld(Roles, Wider: place => place == "repository:daoris" ? ["workspace:w1"] : []);
        var held = new List<SupervisorRuleSet>
        {
            new("machine", 1, Rules(Fixture["base"], "machine")),
            new("workspace:w1", 1, Rules(JsonNode.Parse("""[{ "id": "w", "count": "quiet", "at": 1, "do": "next" }]"""), "workspace:w1")),
            new("repository:daoris", 1, Rules(JsonNode.Parse("""[{ "id": "r", "count": "answers-alone", "at": 3, "do": "hold" }]"""), "repository:daoris")),
        };
        var machine = Rules(JsonNode.Parse("""[{ "id": "t", "count": "tries", "at": 5, "do": "hold" }]"""), "machine");
        var refusal = SupervisorRules.Judge("machine", 1, machine, held, world);
        Assert.Equal("moves-unbounded", refusal?.Code);
        Assert.Equal("repository:daoris", refusal?.Place);
    }
}
