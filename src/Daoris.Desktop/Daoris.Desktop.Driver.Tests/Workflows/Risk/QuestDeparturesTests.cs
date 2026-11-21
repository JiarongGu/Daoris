using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest's done held by its departure is answered by the risk policy at the quest's place: <c>work.done</c>, or <c>direction.set</c> where it
/// departs from every requirement. A pass is posted as the policy's yes with its rule and revision, journaled and said; a row classing it
/// <c>owner</c> keeps the person's yes, a <c>role</c> row names the role; evidence still awaited beside it waits on its check.
/// </summary>
public sealed class QuestDeparturesTests : TestHome
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private static readonly QuestRequirementView Bridge = new("through the v3 bridge", "The report opens through the bridge's route.");
    private static readonly QuestRequirementView Report = new("write the bridge report", "The report is in docs/report-bridge.md.");

    private static QuestView Departed(bool every = false) => new QuestView("q1", "game", "engine", "Write the bridge report", "Through the bridge.", "Done")
    {
        Held = true, Hold = EvidenceCodes.Departed, Requirements = [Report, Bridge],
        Answers =
        [
            every ? new QuestAnswerView(1, null, "It writes no report.", Report.Quote) : new QuestAnswerView(1, "Done as the check says.", null, null),
            new QuestAnswerView(2, null, "It opens on its own page.", Bridge.Quote),
        ],
    };

    private readonly List<(string Quest, RiskPassWire Pass)> _posted = [];

    private readonly HashSet<string> _told = [];
    private bool _accepts = true;

    private Task<IReadOnlyList<string>> AnswerAsync(string config, params QuestView[] quests) =>
        QuestDepartures.AnswerAsync(Home, DriverConfig.Parse(config), quests, (quest, pass, _) =>
        {
            _posted.Add((quest, pass));
            return Task.FromResult((_accepts, _accepts ? "Accepted." : "the quest is no longer departed."));
        }, _told, Now, CancellationToken.None);

    [Fact]
    public void Duplicate_answer_rows_for_one_requirement_count_once_and_a_quest_with_no_requirements_is_work_done()
    {
        var twice = Departed(every: true) with
        {
            Answers = [new(1, null, "It writes no report.", Report.Quote), new(1, null, "It writes no report, said again.", Report.Quote), new(2, null, "Opens elsewhere.", Bridge.Quote)],
        };
        Assert.Equal(RiskActs.DirectionSet, QuestDepartures.ActOf(twice)!.Kind);
        Assert.Equal(RiskActs.WorkDone, QuestDepartures.ActOf(twice with { Answers = [twice.Answers[0], twice.Answers[1]] })!.Kind);
        Assert.Equal(RiskActs.WorkDone, QuestDepartures.ActOf(twice with { Requirements = [] })!.Kind);
        Assert.Null(QuestDepartures.ActOf(twice with { Hold = null }));
    }

    [Fact]
    public async Task A_refusal_of_the_services_is_said_once_for_the_quest_and_revision_and_asked_again_at_every_look()
    {
        _accepts = false;

        var first = await AnswerAsync("""{"risk":{}}""", Departed());
        var second = await AnswerAsync("""{"risk":{}}""", Departed());

        Assert.Equal(2, _posted.Count);
        Assert.Single(first);
        Assert.Contains("no longer departed", first[0], StringComparison.Ordinal);
        Assert.Empty(second);
        Assert.Empty(RiskJournal.Read(Home));
    }

    [Theory]
    [InlineData("work.done", "seed", "r1", true)]
    [InlineData("direction.set", "row", "r1", true)]
    [InlineData("work.accept", "seed", "r1", false)]
    [InlineData("work.done", "seed", "r1\nr2", false)]
    [InlineData("work.done", "se\ned", "r1", false)]
    [InlineData("work.done", "seed", "", false)]
    public void A_policy_body_names_one_of_the_two_acts_and_no_line_break_in_its_rule_or_revision(string act, string rule, string revision, bool reads) =>
        Assert.Equal(reads, new RiskPassWire(act, rule, revision).Malformed is null);

    [Fact]
    public async Task With_nothing_configured_a_departure_passes_posted_as_the_policys_yes_with_its_rule_journaled_and_said()
    {
        var said = await AnswerAsync("""{"risk":{}}""", Departed());

        var (quest, pass) = Assert.Single(_posted);
        var revision = RiskPolicy.Of(DriverConfig.Parse("""{"risk":{}}""")).Revision;
        Assert.Equal(("q1", RiskActs.WorkDone, RiskVerdict.Seed, revision), (quest, pass.Act, pass.Rule, pass.Revision));
        Assert.Equal($"policy:work.done · rule seed at {revision}", pass.By);
        var line = Assert.Single(RiskJournal.Read(Home));
        Assert.Equal((RiskActs.WorkDone, RiskVerdict.Pass, "engine"), (line.Kind, line.Outcome, line.Repository));
        Assert.Contains(said, each => each.Contains("#q1", StringComparison.Ordinal) && each.Contains("risk policy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_departure_from_every_requirement_is_a_target_change_which_a_direction_set_owner_row_keeps_for_your_yes()
    {
        const string owner = """{"risk":{"repository/engine":[{"kind":"direction.set","class":"owner"}]}}""";

        Assert.Empty(await AnswerAsync(owner, Departed(every: true)));
        Assert.Empty(_posted);
        Assert.Empty(RiskJournal.Read(Home));
        var judge = QuestDepartures.JudgeAt(DriverConfig.Parse(owner), Departed(every: true));
        Assert.Contains("held for your yes", QuestHoldsReading.Held(Departed(every: true), judge: judge), StringComparison.Ordinal);

        await AnswerAsync(owner, Departed());
        Assert.Equal(RiskActs.WorkDone, Assert.Single(_posted).Pass.Act);
    }

    [Fact]
    public async Task A_role_row_names_the_role_the_departure_waits_on_and_evidence_still_awaited_waits_on_its_check()
    {
        const string role = """{"risk":{"machine":[{"kind":"work.done","class":"role","by":"supervisor"}]}}""";

        Assert.Empty(await AnswerAsync(role, Departed()));
        var held = QuestHoldsReading.Held(Departed(), judge: QuestDepartures.JudgeAt(DriverConfig.Parse(role), Departed()));
        Assert.Contains("role `supervisor`", held, StringComparison.Ordinal);
        Assert.DoesNotContain("your yes", held, StringComparison.Ordinal);
        Assert.True(QuestHoldsReading.YesLifts(held));

        Assert.Empty(await AnswerAsync("""{"risk":{}}""", Departed() with { AwaitsEvidence = true }));
        Assert.Empty(_posted);
        Assert.Contains("held for your yes", QuestHoldsReading.Held(Departed()), StringComparison.Ordinal);
    }
}
