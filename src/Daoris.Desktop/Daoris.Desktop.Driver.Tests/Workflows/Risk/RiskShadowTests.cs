using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests.Risk;

/// <summary>
/// The judge in shadow: at each hold today's gates open and release (an unchecked done, a review's Accept, a
/// task's approval, a go-ahead, the recovery waits, a declaration's acceptance) one <c>risk.judged</c> line is journaled in the home with what
/// today's gate did beside the verdict, once per open and once per release, deciding nothing.
/// </summary>
public sealed class RiskShadowTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "risk-shadow", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    private const string Run = "q7";
    private const string Unit = "run:q7";
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static NodeStatus Active(string node) => new(Unit, node, NodeStates.Active) { Attempt = 1, Tries = 1, Since = Now };

    private static NodeRecord Standing(params NodeStatus[] statuses) => new(Unit)
    {
        Statuses = statuses.ToDictionary(status => status.Node, StringComparer.Ordinal),
    };

    private static readonly NodeDeclaration Holds = new("holds", NodeKinds.QuestHolds, 1, NodeReach.Observed);
    private static readonly NodeDeclaration Review = new("review", NodeKinds.Review, 1, NodeReach.Pressed);
    private static readonly NodeDeclaration Approve = new("approve", NodeKinds.Task, 1, NodeReach.Pressed) { Outcomes = TaskNode.Approval };
    private static readonly NodeDeclaration GoAhead = new("go", NodeKinds.GoAhead, 1, NodeReach.Pressed);
    private static readonly NodeDeclaration Work = new("work", NodeKinds.Task, 1, NodeReach.Pressed);

    private static readonly NodeDeclaration[] Nodes = [Holds, Review, Approve, GoAhead, Work];

    private static NodeRecord AllWaiting() => Standing(
        Active("holds") with { Holds = "its done is unchecked: ask #3 gave its quest no requirement" }, Active("review"), Active("approve"), Active("go"), Active("work"));

    private static RiskPolicy Seed => RiskPolicy.Read(JsonDocument.Parse("{}").RootElement);

    private IReadOnlyList<RiskJudged> Journal() => RiskJournal.Read(_home);

    [Fact]
    public void Each_wired_step_hold_journals_one_verdict_with_todays_gate_beside_it_and_a_task_waiting_on_its_worker_none()
    {
        var holds = RiskStepHolds.Of(Run, "work", "app", full: false, Nodes, AllWaiting());
        var shadow = new RiskShadow(_home, () => Now);

        shadow.Look(RiskStepHolds.Domain, Seed, holds, _ => "released");

        var lines = Journal();
        Assert.Equal(
            new[] { (Approve.Id, RiskActs.TaskApprove), (GoAhead.Id, RiskActs.TaskApprove), (Holds.Id, RiskActs.WorkDone), (Review.Id, RiskActs.WorkAccept) },
            lines.Select(line => (line.Step!, line.Kind)).OrderBy(pair => pair.Item1, StringComparer.Ordinal));
        Assert.All(lines, line =>
        {
            Assert.Equal((RiskJudged.Opened, RiskJudged.Event, Run, "work", "app"), (line.Stage, line.Name, line.Run, line.Workspace, line.Repository));
            // Today each of these waits on a holder; the seed would pass it: a disagreement, measurable.
            Assert.True(line.TodayHeld);
            Assert.StartsWith("Waiting on `", line.Today, StringComparison.Ordinal);
            Assert.Equal((RiskVerdict.Pass, RiskClasses.None, RiskVerdict.Seed), (line.Outcome, line.Class, line.Source));
            Assert.False(line.Agrees);
        });
    }

    [Fact]
    public void A_hold_is_journaled_once_at_its_open_and_once_at_its_release_read_again_under_the_policy_then()
    {
        var shadow = new RiskShadow(_home, () => Now);
        var review = RiskStepHolds.Of(Run, "work", "app", full: false, [Review], Standing(Active("review")));

        shadow.Look(RiskStepHolds.Domain, Seed, review, _ => "released");
        shadow.Look(RiskStepHolds.Domain, Seed, review, _ => "released");
        Assert.Single(Journal());

        // A restart reads what the journal holds open: the same hold is not opened twice.
        var again = new RiskShadow(_home, () => Now);
        again.Look(RiskStepHolds.Domain, Seed, review, _ => "released");
        Assert.Single(Journal());

        var raised = RiskPolicy.Read(JsonDocument.Parse("""{ "risk": { "workspace/work": [ { "kind": "work.accept", "class": "owner" } ] } }""").RootElement);
        again.Look(RiskStepHolds.Domain, raised, [], key => $"released: {key}");

        var released = Journal()[^1];
        Assert.Equal((RiskJudged.Released, RiskVerdict.OwnerOutcome, "workspace/work"), (released.Stage, released.Outcome, released.Source));
        Assert.StartsWith("released: step:q7/review", released.Today, StringComparison.Ordinal);
        Assert.True(released.Agrees);
    }

    [Fact]
    public void A_declaration_waiting_on_the_owner_and_one_full_access_accepts_as_it_lands_journal_command_change_with_todays_outcome()
    {
        var owed = new DeclarationOwed("app", "work", "main", "1111111111111111111111111111111111111111", "changed", Now);
        var byMode = new DeclarationOwed("lib", "work", "main", "2222222222222222222222222222222222222222", "changed", Now);
        var config = DriverConfig.Parse("""{ "access": { "lib": "full" } }""");

        var holds = RiskDeclarationHolds.Of([owed], [byMode], config);
        new RiskShadow(_home, () => Now).Look(RiskDeclarationHolds.Domain, RiskPolicy.Of(config), holds, _ => RiskDeclarationHolds.Released);

        var lines = Journal().OrderBy(line => line.Repository, StringComparer.Ordinal).ToList();
        Assert.Equal(["app", "lib"], lines.Select(line => line.Repository));
        Assert.All(lines, line => Assert.Equal(RiskActs.CommandChange, line.Kind));
        // Restricted: today waits on the owner, and so would the seed.
        Assert.Equal((true, RiskVerdict.OwnerOutcome, true), (lines[0].TodayHeld, lines[0].Outcome, lines[0].Agrees));
        // Full access: today accepts it as it lands, and the seed passes it.
        Assert.Equal((false, RiskVerdict.Pass, true, RiskPlace.FullMode), (lines[1].TodayHeld, lines[1].Outcome, lines[1].Agrees, lines[1].Mode));
    }

    [Fact]
    public void The_why_of_a_run_prints_its_verdict_rows_and_a_run_with_none_says_so()
    {
        var shadow = new RiskShadow(_home, () => Now);
        shadow.Look(RiskStepHolds.Domain, Seed, RiskStepHolds.Of(Run, "work", "app", full: false, [Holds, Review], AllWaiting()), _ => "released: reached");

        var all = RiskWhy.Lines(_home, Run, null);
        var one = RiskWhy.Lines(_home, Run, "review");
        var none = RiskWhy.Lines(_home, "q8", null);

        Assert.Equal(2, all.Count);
        Assert.Single(one);
        Assert.Contains("review", one[0], StringComparison.Ordinal);
        Assert.Contains("work.accept", one[0], StringComparison.Ordinal);
        Assert.Contains("pass", one[0], StringComparison.Ordinal);
        Assert.Contains("today: Waiting on `", one[0], StringComparison.Ordinal);
        Assert.Equal(["no verdict is journaled for the run `q8`."], none);
    }

    [Fact]
    [Trait(Category.Name, Category.Census)]
    public void Every_wait_a_step_can_stand_at_is_a_hold_of_one_kind_or_said_to_be_none()
    {
        foreach (var why in PersonWaits.All)
        {
            var kind = RiskStepHolds.KindOf(why, Work);
            if (why is PersonWaits.Task or PersonWaits.Actor) Assert.Null(kind);
            else Assert.True(kind is not null && RiskActs.Is(kind), $"`{why}` names no act kind");
        }

        Assert.Equal("release.start/production", RiskStepHolds.KindOf(PersonWaits.GoAhead, GoAhead with { Options = new Dictionary<string, string> { ["act"] = "release.start/production" } }));
    }
}
