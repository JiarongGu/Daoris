using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTEXTDRIVE1e's rest (the context counters design §4.7): the acts a look's plan made, read from the plan and the snapshot alone: a quest
/// parked by its failed sessions, held on a transient episode, moved by a rule, held by the bound of moves; each as its rule's act on the
/// quest's last session, and none where no rule acts by it.
/// </summary>
public sealed class PlannedRuleActsTests : IDisposable
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-03T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly QuestView Quest = new("q1", "ask #a1", "engine", "The work", "", "Taken");
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "planned-rule-acts", Guid.NewGuid().ToString("N")[..8]);

    public PlannedRuleActsTests() => Directory.CreateDirectory(_home);

    public void Dispose() => GitSeed.Discard(_home);

    private static PriorSession Last => new("s1", "trees/default/engine/s-1", "failed")
    {
        Quest = "q1", Actor = "build", Adapter = "acp-stub", Said = [], Updated = At.AddMinutes(-1),
    };

    private static Snapshot Looking(StaffMoving? moving = null) =>
        new([Quest], [], [], new Dictionary<string, int> { ["q1"] = 3 })
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = Last },
            PolicyMoving = moving is null
                ? new Dictionary<string, StaffMoving>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, StaffMoving>(StringComparer.OrdinalIgnoreCase) { ["q1"] = moving },
        };

    private static SupervisorRuleBook Book(params SupervisorRule[] more) =>
        new([new SupervisorRuleSet(SupervisorRules.Machine, 2, [.. SupervisorRuleBook.Seeded(3), .. more])]);

    private static IReadOnlyList<RuleActed> Acts(Snapshot snapshot, IReadOnlyList<Consideration> plan, SupervisorRuleBook book, int? moves = 4) =>
        PlannedRuleActs.Of(snapshot, plan, book, (quest, _) => new RuledLine(RoleWork.Build, quest.To, null, quest.Id), _ => moves, At);

    private static PriorSession EndedTransient(string session, int tries) => Last with
    {
        Session = session,
        NoteParts = [new NotePart { Code = EndNoteCodes.EndedTransient.Code, Values = [new("count", (long)tries), new("minutes", 3L)] }],
    };

    [Fact]
    public void CONTEXTDRIVE1s_a_park_says_the_failed_sessions_past_try_again_s_mark_not_the_records_count()
    {
        // Parked at 3, Try again, three more failed: six in the records, three past the mark.
        var parked = new Consideration(Quest, StartVerdict.Exhausted, "parked") { Failures = 6, FailedSince = 3, ParksAt = 3 };

        var act = Assert.Single(Acts(Looking(), [parked], Book()));

        Assert.Equal(("q1", "s1", SupervisorRules.Failures, 3, 3, "start", 2L), (act.Line, act.Session, act.Rule.Count, act.Rule.At, act.N, act.Point, act.Revision));
        Assert.Equal(SupervisorRules.Hold, act.Rule.Do);
    }

    [Fact]
    public void CONTEXTDRIVE1s_a_transient_hold_says_the_tries_its_episode_made()
    {
        var held = HoldVerdicts.TransientHeld(Quest, EndedTransient("s0", 7));

        var act = Assert.Single(Acts(Looking(), [held], Book()));

        Assert.Equal(("s0", SupervisorRules.Tries, 7, "episode"), (act.Session, act.Rule.Count, act.N, act.Point));
    }

    [Fact]
    public void CONTEXTDRIVE1s_a_move_and_the_bound_of_moves_say_the_count_each_read()
    {
        var tries = new SupervisorRule("build-tries", SupervisorRules.Tries, SupervisorRules.EpisodeWindow, 2, StaffActions.Weighted, null, SupervisorRules.Machine, [RoleWork.Build]);
        var moves = new SupervisorRule("build-moves", SupervisorRules.Moves, SupervisorRules.Windows[SupervisorRules.Moves], 3, SupervisorRules.Ask, null, SupervisorRules.Machine, [RoleWork.Build]);
        var book = Book(tries, moves);
        var transient = Looking(new StaffMoving("s1", StaffConditions.Transient, null)) with
        {
            LastRun = new Dictionary<string, PriorSession>(StringComparer.OrdinalIgnoreCase) { ["q1"] = EndedTransient("s1", 3) },
        };

        var moved = Assert.Single(Acts(transient, [], book));
        Assert.Equal((SupervisorRules.Tries, StaffActions.Weighted, 3, "s1"), (moved.Rule.Count, moved.Rule.Do, moved.N, moved.Session));

        var bound = new Consideration(Quest, StartVerdict.PolicyHeld, "bound") { HeldBy = Last, Said = new(SittingFact.MovesBound) { Session = "s1" } };
        var held = Assert.Single(Acts(Looking(), [bound], book));
        Assert.Equal((SupervisorRules.Moves, SupervisorRules.Ask, 4), (held.Rule.Count, held.Rule.Do, held.N));

        // A line whose moves did not read says no act it cannot count; an ask the policy made on a limit is no moves bound.
        Assert.Empty(Acts(Looking(), [bound], book, moves: null));
        Assert.Empty(Acts(Looking(), [bound with { Said = new(SittingFact.PolicyAsk) }], book));
    }

    [Fact]
    public void A_quest_no_rule_acts_on_says_nothing()
    {
        var off = new SupervisorRule("no-failures", SupervisorRules.Failures, "in-a-row", 3, SupervisorRules.Off, null, SupervisorRules.Machine, null);
        var book = new SupervisorRuleBook([new SupervisorRuleSet(SupervisorRules.Machine, 1, [off])]);

        Assert.Empty(Acts(Looking(), [new Consideration(Quest, StartVerdict.Exhausted, "parked") { Failures = 4, FailedSince = 4 }], book));
        Assert.Empty(Acts(new Snapshot([Quest], [], [], new Dictionary<string, int>()), [new Consideration(Quest, StartVerdict.Exhausted, "parked") { Failures = 4, FailedSince = 4 }], Book()));
    }

    [Fact]
    public void An_act_is_kept_once_on_its_session_through_the_rules_acts()
    {
        var acts = new SupervisorRuleActs(_home);
        var act = Assert.Single(Acts(Looking(), [new Consideration(Quest, StartVerdict.Exhausted, "parked") { Failures = 3, FailedSince = 3 }], Book()));

        acts.Record(act);

        var kept = Assert.Single(acts.Of("s1"));
        Assert.Equal((act.Rule.Count, act.Rule.Do, act.Point), (kept.Rule.Count, kept.Rule.Do, kept.Point));
    }
}
