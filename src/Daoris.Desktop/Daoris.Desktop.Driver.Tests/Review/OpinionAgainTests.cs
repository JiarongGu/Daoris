using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the gate counts of reviewers that ended without an opinion, as a state table over stood-in reads.</summary>
public sealed class OpinionAgainTests
{
    private const string Read = "1111111111111111111111111111111111111111";
    private static readonly DateTimeOffset Asked = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    private static OpinionView Pass(string id, string why) =>
        new(id, "landing", "first", "s1", "r-" + id, "web-app", "0000000000000000000000000000000000000000", Read, "codex-acp", ReviewerLabels.AnotherMaker, "failed")
        {
            Product = "Codex", Maker = "OpenAI", Minutes = 20, Why = why,
        };

    private static OpinionAskKept Look(string id, int minute, string tip = Read) => new(Asked.AddMinutes(minute), "landing", "s1", tip) { Opinion = id };

    private static OpinionAskKept Person(string id, int minute) => Look(id, minute) with { By = OpinionAskers.Person };

    private static Task<OpinionGateState> JudgeAsync(Dictionary<string, OpinionView> opinions, bool required, params OpinionAskKept[] asks)
    {
        var rule = new ResolvedOpinion(new OpinionRule(["codex-acp"], [OpinionRules.Landing], required), OpinionSource.Repository);
        var facts = new OpinionGateFacts("web-app", rule)
        {
            Session = "s1", Tip = Read, Asks = asks,
            Quest = new QuestView("q1", "ask #a1", "web-app", "Work q1", "", "Done"),
        };
        var reads = new OpinionReads(
            id => Task.FromResult(opinions.GetValueOrDefault(id)), _ => OpinionDelivered.None, _ => Task.FromResult<bool?>(true), _ => Task.FromResult<int?>(0));
        return OpinionGate.JudgeAsync(facts, reads);
    }

    /// <summary>Passes that ended in a row are counted back to the person's own ask, which counts afresh; a cap's cool-off ends the run.</summary>
    [Fact]
    public async Task Passes_that_ended_with_no_opinion_are_counted_in_a_row_and_the_person_s_own_ask_counts_afresh()
    {
        var opinions = new Dictionary<string, OpinionView>(StringComparer.Ordinal);
        foreach (var id in new[] { "o0", "o1", "o2", "o3" }) opinions[id] = Pass(id, "ended");

        var first = await JudgeAsync(opinions, true, Look("o1", 1));
        var second = await JudgeAsync(opinions, true, Look("o1", 1), Look("o2", 2));
        var spent = await JudgeAsync(opinions, true, Look("o1", 1), Look("o2", 2), Look("o3", 3));
        var afresh = await JudgeAsync(opinions, true, Look("o0", 0), Look("o1", 1), Look("o2", 2), Person("o3", 3));
        opinions["o2"] = Pass("o2", OpinionViews.Limited);
        var cooled = await JudgeAsync(opinions, true, Look("o1", 1), Look("o2", 2), Look("o3", 3));

        Assert.Equal((1, true, false), (first.Ended, OpinionAgain.Pending(first), OpinionAgain.Spent(first)));
        Assert.Equal((2, true, false), (second.Ended, OpinionAgain.Pending(second), OpinionAgain.Spent(second)));
        Assert.Equal((OpinionAgain.Bound, false, true), (spent.Ended, OpinionAgain.Pending(spent), OpinionAgain.Spent(spent)));
        Assert.Equal((1, true), (afresh.Ended, OpinionAgain.Pending(afresh)));
        Assert.Equal(1, cooled.Ended);
        Assert.Contains("ask 1 of 3", first.Says);
        Assert.Contains("Nothing waits for you", first.Says);
        Assert.Contains("Daoris gave up the second opinion after 3 asks", spent.Says);
        Assert.Contains("Nothing waits for you", spent.Says);
    }

    [Fact]
    public async Task Passes_asked_at_an_earlier_tip_are_not_counted_toward_the_new_tip_s_bound()
    {
        var opinions = new Dictionary<string, OpinionView>(StringComparer.Ordinal);
        foreach (var id in new[] { "o1", "o2", "o3" }) opinions[id] = Pass(id, "ended");
        const string Later = "2222222222222222222222222222222222222222";

        var gate = await JudgeAsync(opinions, true, Look("o1", 1), Look("o2", 2), Look("o3", 3, Later));

        Assert.Equal((1, true), (gate.Ended, OpinionAgain.Pending(gate)));
    }

    /// <summary>A rule that does not require an opinion never asks again, gives up or waits: it lets the work go.</summary>
    [Fact]
    public async Task An_opinion_the_rule_does_not_require_is_never_asked_again_and_lets_the_work_go()
    {
        var opinions = new Dictionary<string, OpinionView>(StringComparer.Ordinal) { ["o1"] = Pass("o1", "ended") };

        var gate = await JudgeAsync(opinions, false, Look("o1", 1));

        Assert.Equal((false, false, true), (OpinionAgain.Pending(gate), OpinionAgain.Spent(gate), gate.LetsGo));
        Assert.Contains("nothing waits for it", gate.Says);
    }

    /// <summary>The next ready reviewer in the rule's order reads first; the one that ended is last, and the only one is asked again.</summary>
    [Fact]
    public void A_reviewer_that_ended_is_rotated_behind_the_next_in_the_rule_s_order()
    {
        Assert.Equal(["claude-code", "codex-acp"], OpinionAgain.Rotated(["codex-acp", "claude-code"], "codex-acp"));
        Assert.Equal(["a", "c", "b"], OpinionAgain.Rotated(["a", "b", "c"], "B"));
        Assert.Equal(["codex-acp"], OpinionAgain.Rotated(["codex-acp"], "codex-acp"));
        Assert.Equal(["a", "b"], OpinionAgain.Rotated(["a", "b"], null));
    }
}
