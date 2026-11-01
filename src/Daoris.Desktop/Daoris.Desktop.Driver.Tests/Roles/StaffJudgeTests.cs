using System.Globalization;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1a (D226; the staff policy design §3, §4): the policy's two pure judges held to <c>fixtures/staff-policy.json</c>.
/// <see cref="StaffJudge.Arrange"/> orders a role's own entries with each one's reason, passes those over budget or held, and leaves those
/// not taking the work; a sequence feeds each pick back into the window and today's starts; <see cref="StaffJudge.OnCondition"/> answers
/// a met condition's action, today's for every condition where no policy or no move is set.
/// </summary>
public sealed class StaffJudgeTests
{
    public static TheoryData<string> Arrangements() => StaffPolicyFixture.Names("arrangements");

    public static TheoryData<string> Sequences() => StaffPolicyFixture.Names("sequences");

    public static TheoryData<string> Conditions() => StaffPolicyFixture.Names("conditions");

    [Theory]
    [MemberData(nameof(Arrangements))]
    public void An_arrangement_is_its_table_s(string name)
    {
        var row = StaffPolicyFixture.Row("arrangements", name);

        var arranged = StaffJudge.Arrange(StaffPolicyFixture.Policy(row), row["work"]!.GetValue<string>(), StaffPolicyFixture.Order(row), StaffPolicyFixture.Facts(row["facts"] as JsonObject));

        var said = $"{name}: walk {string.Join(", ", arranged.Walk.Select(each => $"{each.Entry.From}/{each.By}"))}; passed {string.Join(", ", arranged.Passed.Select(each => $"{each.Entry.From}/{each.Code}"))}; left {string.Join(", ", arranged.Left.Select(each => each.From))}";
        Assert.True(Pairs(row["walk"]).SequenceEqual(arranged.Walk.Select(each => (each.Entry.From, each.By))), said);
        Assert.True(Pairs(row["passed"]).SequenceEqual(arranged.Passed.Select(each => (each.Entry.From, each.Code))), said);
        Assert.True(row["left"]!.AsArray().Select(each => each!.GetValue<string>()).SequenceEqual(arranged.Left.Select(each => each.From)), said);
        Assert.All(arranged.Walk, each => Assert.False(string.IsNullOrWhiteSpace(each.Said)));
        Assert.All(arranged.Passed, each => Assert.False(string.IsNullOrWhiteSpace(each.Why)));
    }

    [Theory]
    [MemberData(nameof(Sequences))]
    public void A_sequence_of_new_starts_is_its_table_s(string name)
    {
        var row = StaffPolicyFixture.Row("sequences", name);
        var policy = StaffPolicyFixture.Policy(row);
        var order = StaffPolicyFixture.Order(row);
        var window = row["window"]!.AsArray().Select(each => StaffPolicyFixture.Key(each!.GetValue<int>())).ToList();
        var today = new Dictionary<StaffKey, int>();
        var picks = new List<int>();

        foreach (var _ in row["picks"]!.AsArray())
        {
            var arranged = StaffJudge.Arrange(policy, row["work"]!.GetValue<string>(), order, new StaffFacts { Now = StaffPolicyFixture.Now, Window = [.. window], Today = new Dictionary<StaffKey, int>(today) });
            var top = arranged.Walk[0].Entry;
            picks.Add(int.Parse(top.From, CultureInfo.InvariantCulture));
            window.Add(StaffKey.Of(top));
            today[StaffKey.Of(top)] = today.GetValueOrDefault(StaffKey.Of(top)) + 1;
        }

        Assert.True(row["picks"]!.AsArray().Select(each => each!.GetValue<int>()).SequenceEqual(picks), $"{name}: {string.Join(", ", picks)}");
    }

    [Theory]
    [MemberData(nameof(Conditions))]
    public void A_condition_s_action_is_its_table_s(string name)
    {
        var row = StaffPolicyFixture.Row("conditions", name);

        var action = StaffJudge.OnCondition(
            StaffPolicyFixture.Policy(row), row["condition"]!.GetValue<string>(), new StaffLine(row["elsewhere"]!.GetValue<bool>(), row["moves"]!.GetValue<int>()));

        Assert.Equal(
            (row["condition"]!.GetValue<string>(), row["do"]!.GetValue<string>(), StaffPolicyFixture.Int(row["after"]), StaffPolicyFixture.Int(row["upTo"]),
                row["asToday"]!.GetValue<bool>(), row["bound"]!.GetValue<bool>()),
            (action.Condition, action.Do, action.After, action.UpTo, action.AsToday, action.MovesBound));
    }

    [Fact]
    public void With_no_policy_every_condition_answers_as_today()
    {
        foreach (var condition in StaffConditions.Codes)
        {
            Assert.True(StaffJudge.OnCondition(null, condition, new StaffLine(true, 99)) is { AsToday: true, MovesBound: false }, condition);
        }

        Assert.Throws<ArgumentException>(() => StaffJudge.OnCondition(null, "slow", new StaffLine(true, 0)));
    }

    [Fact]
    public void The_weighted_pick_says_its_proportion_and_its_aim()
    {
        var row = StaffPolicyFixture.Row("arrangements", "3:1 after two starts on choice 1 puts choice 2 first");

        var arranged = StaffJudge.Arrange(StaffPolicyFixture.Policy(row), RoleWork.Build, StaffPolicyFixture.Order(row), StaffPolicyFixture.Facts(row["facts"] as JsonObject));

        Assert.Equal("by weight: 0 of the last 2 were choice 2, aim 1 in 4", arranged.Walk[0].Said);
        Assert.Equal("by weight: 2 of the last 2 were choice 1, aim 3 in 4", arranged.Walk[1].Said);
    }

    [Fact]
    public void The_balance_forgets_an_old_run_and_a_share_reads_the_last_twenty_starts()
    {
        var row = StaffPolicyFixture.Row("sequences", "1:1 from an empty window alternates, ties by order");
        var one = StaffPolicyFixture.Key(1);
        var two = StaffPolicyFixture.Key(2);

        // Thirty starts on choice 2 and then twenty on choice 1: the balance is held within -W and 2W, so choice 2 is first.
        var arranged = StaffJudge.Arrange(StaffPolicyFixture.Policy(row), RoleWork.Build, StaffPolicyFixture.Order(row),
            new StaffFacts { Now = StaffPolicyFixture.Now, Window = [.. Enumerable.Repeat(two, 30), .. Enumerable.Repeat(one, 20)] });

        Assert.Equal("2", arranged.Walk[0].Entry.From);
    }

    private static IEnumerable<(string, string)> Pairs(JsonNode? node) =>
        node!.AsArray().Select(pair => (pair![0]!.GetValue<string>(), pair[1]!.GetValue<string>()));
}
