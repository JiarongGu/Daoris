using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A role's policy as a record of its own, its choices known by their tuple, the
/// digest binding it to the staffing it is set for, the presets that fill a draft, and its judge, held both ways to
/// <c>fixtures/staff-policy.json</c>'s <c>judged</c> rows.
/// </summary>
public sealed class StaffPolicyTests
{
    private static readonly IReadOnlyList<ActorChoice> Choices = StaffPolicyFixture.Choices;

    public static TheoryData<string> Judged() => StaffPolicyFixture.Names("judged");

    [Theory]
    [MemberData(nameof(Judged))]
    public void The_judge_answers_its_table_both_ways(string name)
    {
        var row = StaffPolicyFixture.Row("judged", name);
        var takes = row["takes"] is JsonArray works ? works.Select(each => each!.GetValue<string>()).ToList() : [RoleWork.Build];
        var role = new RoleRecord("night", AuthoredText.Of("Night"), RoleRecord.Always, takes, RoleScope.Everywhere);
        var staffing = Choices.Take(StaffPolicyFixture.Int(row["choices"]) ?? 2).ToList();
        var refused = StaffPolicy.Judge(role, staffing, StaffPolicyFixture.Policy(row)!);

        if (StaffPolicyFixture.Text(row["refused"]) is { } said)
        {
            Assert.NotNull(refused);
            Assert.Equal("policy", refused.Code);
            Assert.Contains(said, refused.Said);
        }
        else
        {
            Assert.True(refused is null, $"{name}: {refused?.Said}");
        }
    }

    [Fact]
    public void The_policy_code_is_in_the_role_refusals_closed_set() => Assert.Contains(StaffPolicy.Code, RoleCatalog.Codes);

    [Fact]
    public void A_record_is_written_compact_in_the_staffing_s_order_and_read_back_whole()
    {
        var policy = new StaffPolicy(
            StaffRules.Weight,
            [
                new StaffChoice(StaffKey.Of(Choices[0]), Weight: 3),
                new StaffChoice(StaffKey.Of(Choices[2]), Weight: 1, Share: 40, Budget: new StaffBudget(Starts: 6), Takes: [RoleWork.Design]),
            ],
            new Dictionary<string, StaffMove>());

        var text = policy.Write();

        Assert.Equal(
            """{"rule":"weight","choices":[{"agent":"claude-code","model":"sonnet","effort":"high","weight":3},{"agent":"codex","effort":"medium","weight":1,"share":40,"budget":{"starts":6},"takes":["design"]}]}""",
            text);
        var (read, refused) = StaffPolicy.Parse(text);
        Assert.Null(refused);
        Assert.Equal(text, read!.Write());
    }

    [Fact]
    public void A_stored_moves_or_most_is_passed_over_and_the_next_write_holds_neither()
    {
        var (read, refused) = StaffPolicy.Parse("""{"rule":"order","choices":[{"agent":"a"}],"moves":{"limit":"next","transient":{"do":"weighted","after":2}},"most":3}""");

        Assert.Null(refused);
        Assert.Empty(read!.Moves);
        Assert.Null(read.Most);
        Assert.Equal("""{"rule":"order","choices":[{"agent":"a","weight":1}]}""", read.Write());
        Assert.DoesNotContain(StaffCapabilities.Fields, field => field.StartsWith("moves", StringComparison.Ordinal) || field == "most");
    }

    [Theory]
    [InlineData("not json", "does not read")]
    [InlineData("[]", "not an object")]
    [InlineData("""{"choices":[]}""", "rule")]
    [InlineData("""{"rule":"order"}""", "choices")]
    [InlineData("""{"rule":"order","choices":[{"model":"opus"}]}""", "agent")]
    [InlineData("""{"rule":"order","choices":[{"agent":"a","weight":1.5}]}""", "weight")]
    [InlineData("""{"rule":"order","choices":[{"agent":"a","budget":{"starts":2.5}}]}""", "budget")]
    [InlineData("""{"rule":"order","choices":[{"agent":"a","takes":"design"}]}""", "takes")]
    public void A_record_of_the_wrong_shape_is_refused_by_the_policy_code_saying_what(string text, string said)
    {
        var (read, refused) = StaffPolicy.Parse(text);

        Assert.Null(read);
        Assert.Equal("policy", refused!.Code);
        Assert.Contains(said, refused.Said);
    }

    [Fact]
    public void A_choice_is_known_by_its_tuple_and_its_text_reads_back()
    {
        var key = StaffKey.Of(Choices[2]);

        Assert.Equal(new StaffKey("codex", null, "medium"), key);
        Assert.Equal(key, StaffKey.Of(new ActorEffective("codex", null, "medium", "3")));
        Assert.NotEqual(key, new StaffKey("Codex", null, "medium"));
        Assert.Equal("""{"agent":"codex","effort":"medium"}""", key.Text);
        Assert.Equal(key, StaffKey.Read(key.Text));
        Assert.Null(StaffKey.Read("{"));
    }

    [Fact]
    public void The_digest_is_the_sha256_of_the_choices_as_the_staffing_writes_them_and_moves_with_any_change_to_them()
    {
        var written = """[{"agent":"claude-code","model":"sonnet","effort":"high"},{"agent":"claude-code","model":"opus","effort":"high"}]""";

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(written))).ToLowerInvariant(), StaffPolicy.Digest(Choices.Take(2).ToList()));
        Assert.Equal(StaffPolicy.Digest([Choices[0], Choices[1]]), StaffPolicy.Digest([new("claude-code", "sonnet", "high"), new("claude-code", "opus", "high")]));
        Assert.NotEqual(StaffPolicy.Digest([Choices[0], Choices[1]]), StaffPolicy.Digest([Choices[1], Choices[0]]));
        Assert.NotEqual(StaffPolicy.Digest([Choices[0], Choices[1]]), StaffPolicy.Digest([Choices[0], Choices[1] with { Effort = "low" }]));
    }

    [Fact]
    public void Re_keying_keeps_each_listed_entry_in_the_staffing_s_order_gives_a_new_choice_weight_1_and_drops_a_removed_one()
    {
        var policy = new StaffPolicy(
            StaffRules.Weight,
            [new StaffChoice(StaffKey.Of(Choices[0]), Weight: 3, Share: 60), new StaffChoice(StaffKey.Of(Choices[1]), Weight: 1, Takes: [RoleWork.Design])],
            new Dictionary<string, StaffMove>());

        var rekeyed = policy.Rekeyed([Choices[2], Choices[0]]);

        Assert.Equal(
            [(StaffKey.Of(Choices[2]), 1, (int?)null), (StaffKey.Of(Choices[0]), 3, 60)],
            rekeyed.Choices.Select(each => (each.Key, each.Weight, each.Share)).ToList());
        Assert.Equal(StaffRules.Weight, rekeyed.Rule);
    }

    [Fact]
    public void A_preset_fills_a_draft_and_first_is_the_order_and_nothing_else()
    {
        var draft = new StaffPolicy(
            StaffRules.Weight,
            [new StaffChoice(StaffKey.Of(Choices[0]), Weight: 3, Budget: new StaffBudget(Starts: 6)), new StaffChoice(StaffKey.Of(Choices[1]), Weight: 1)],
            new Dictionary<string, StaffMove>());
        var staffing = Choices.Take(2).ToList();

        var first = StaffPolicy.Preset(StaffPresets.First, staffing, draft);
        Assert.Equal(StaffRules.Order, first.Rule);
        Assert.All(first.Choices, each => Assert.True(each is { Weight: 1, Share: null, Budget: null, Takes: null }, $"{each}"));
        Assert.Empty(first.Moves);
        Assert.Null(first.Most);

        var even = StaffPolicy.Preset(StaffPresets.Even, staffing, draft);
        Assert.Equal(StaffRules.Weight, even.Rule);
        Assert.Equal([1, 1], even.Choices.Select(each => each.Weight));
        Assert.Equal(new StaffBudget(Starts: 6), even.Choices[0].Budget);
        Assert.Empty(even.Moves);
        Assert.Null(even.Most);

        var weighted = StaffPolicy.Preset(StaffPresets.Weighted, staffing, draft);
        Assert.Equal([3, 1], weighted.Choices.Select(each => each.Weight));
        Assert.Empty(weighted.Moves);

        Assert.Equal([1, 1], StaffPolicy.Preset(StaffPresets.Weighted, staffing, null).Choices.Select(each => each.Weight));
        Assert.Equal([StaffPresets.First, StaffPresets.Even, StaffPresets.Weighted], StaffPresets.Names);
        Assert.All(StaffPresets.Names, preset => Assert.Null(StaffPolicy.Judge(
            new RoleRecord("build", AuthoredText.Of("Build"), RoleRecord.Always, [RoleWork.Build], RoleScope.Everywhere), staffing,
            StaffPolicy.Preset(preset, staffing, draft))));
    }

    [Fact]
    public void Most_is_absent_while_every_move_is_as_today_and_three_once_any_is_set()
    {
        var choices = Choices.Take(2).Select(each => new StaffChoice(StaffKey.Of(each))).ToList();

        Assert.Null(new StaffPolicy(StaffRules.Order, choices, new Dictionary<string, StaffMove>()).Bound);
        Assert.Equal(3, new StaffPolicy(StaffRules.Order, choices, new Dictionary<string, StaffMove> { [StaffConditions.Quiet] = new(StaffActions.Next) }).Bound);
        Assert.Equal(1, new StaffPolicy(StaffRules.Order, choices, new Dictionary<string, StaffMove>(), Most: 1).Bound);
    }
}
