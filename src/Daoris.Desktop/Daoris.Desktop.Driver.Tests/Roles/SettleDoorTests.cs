using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One settle door: a run step's word is taken from the holder its tenure names, in its epoch, or from an observation; a step at a
/// destination the owner marked is held by the owner role alone; and no settle path asks the step's kind who may settle it.
/// </summary>
public sealed class SettleDoorTests : IDisposable
{
    private const string Run = "q-settle";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-settle-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly DateTimeOffset _now = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private PlaceCastFiles Casts => new(_home, () => true);

    private NodeRecordFiles Records => new(_home, () => true);

    private static PlaceRiskBook Risk(params string[] marks) =>
        PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = PlaceRisks.Low, Mark = marks });

    /// <summary>A run whose step <c>step</c> names <paramref name="role"/>, under way at its first attempt and held as the place casts it now.</summary>
    private StepTenure Start(string role, string outcome, PlaceRiskBook risk)
    {
        var digest = RunHome.SaveGraph(_home, $$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"step",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"step","kind":"task","version":1,"reach":"pressed","outcomes":["{{outcome}}"],"role":"{{role}}"}],
             "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"step"}]}
            """);
        RunHome.Bind(_home, Run, digest, _now);
        var unit = RunUnits.IdOf(Run);
        Records.Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = NodeLifecycle.Start(unit, "claimed") with { State = NodeStates.Reached, Attempt = 1, Since = _now },
                ["step"] = NodeLifecycle.Start(unit, "step") with
                {
                    State = NodeStates.Active, Attempt = 1, Since = _now, Binding = new NodeAcceptance(unit, "c1", digest, "step", 1, 1),
                },
            },
        });
        var graph = new GraphRuns(Records).Find(Run)!.Version.Graph!;
        AttemptTenures.Keep(Records, unit, graph.Nodes, "work", null, risk, _now);
        return new StepTenureFiles(_home, () => true).Current(unit, "step", 1)!;
    }

    [Fact]
    public void The_holders_word_settles_and_another_agents_is_refused_naming_who_holds_it()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", "the release path", _now);
        Assert.Equal("agent:alpha", Start(StepRoles.ReleaseManager, "done", Risk()).Holder);

        var other = RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, null, () => _now, settle: new SettleBy("agent:beta", 1));
        Assert.Equal(SettleDoor.NotHolder, other.Refused);
        Assert.Contains("agent:alpha", other.Detail, StringComparison.Ordinal);

        Assert.True(RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, "released", () => _now, settle: new SettleBy("agent:alpha", 1)).Done);
    }

    [Fact]
    public void The_persons_press_on_a_step_an_agent_holds_takes_it_and_settles_in_one_act()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        Start(StepRoles.ReleaseManager, "done", Risk());
        var tenures = new StepTenureFiles(_home, () => true);

        // An agent's word never takes, and a press read in an ended epoch is refused and takes nothing.
        Assert.Equal(SettleDoor.NotHolder, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, null, () => _now, settle: new SettleBy("agent:beta", 1)).Refused);
        Assert.Equal(StepTenureFiles.EndedEpoch, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, null, () => _now, epoch: 2).Refused);
        Assert.Equal("agent:alpha", tenures.Current(RunUnits.IdOf(Run), "step", 1)!.Holder);

        var pressed = RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, "released by hand", () => _now.AddMinutes(1), epoch: 1);
        Assert.True(pressed.Done, pressed.Refused);
        var now = tenures.Current(RunUnits.IdOf(Run), "step", 1)!;
        Assert.Equal((CastHolders.Person, 2, TenureWhy.TakeBack), (now.Holder, now.Epoch, now.Why));
        var take = Assert.Single(Casts.Journal(), entry => entry.Act == CastActs.Take);
        Assert.Equal(("pressed done", Run, "step", 2), (take.Why, take.Run, take.Node, take.Epoch));
        Assert.Contains(Records.Read(RunUnits.IdOf(Run))!.Pending, word => word is { Node: "step", Act: NodeWordActs.Done });
    }

    [Fact]
    public void A_word_in_an_ended_epoch_is_refused_and_the_holder_now_settles()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        Start(StepRoles.ReleaseManager, "done", Risk());
        Assert.True(StepHolding.Act(Records, Casts, Risk(), Run, "step", CastActs.Take, null, "mine now", _now.AddMinutes(1)).Done);

        Assert.Equal(StepTenureFiles.EndedEpoch, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, null, () => _now, settle: new SettleBy("agent:alpha", 1)).Refused);
        Assert.Equal(SettleDoor.NotHolder, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, null, () => _now, settle: new SettleBy("agent:alpha")).Refused);
        Assert.True(RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, null, () => _now, epoch: 2).Done);
    }

    [Fact]
    public void A_marked_step_is_held_and_settled_by_the_owner_role_alone()
    {
        var marked = Risk(PlaceMarks.ProductionReleases);
        Casts.Cast("machine", StepRoles.Approver, "agent:alpha", "approves the test stages", _now);
        var held = Start(StepRoles.Approver, "approved", marked);
        Assert.Equal((CastHolders.Person, TenureWhy.Marked), (held.Holder, held.Why));

        // No cast hands it on: the owner's mark keeps it theirs.
        var handed = StepHolding.Act(Records, Casts, marked, Run, "step", CastActs.Hand, "agent:beta", null, _now.AddMinutes(1));
        Assert.Equal(SettleDoor.Marked, handed.Refused);

        Assert.Equal(SettleDoor.Marked, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Approve, null, () => _now, settle: new SettleBy("agent:alpha")).Refused);
        Assert.Equal(SettleDoor.Marked, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Reject, "no", () => _now, settle: new SettleBy(SettleBy.Line)).Refused);
        Assert.True(RunNodeWords.Word(Records, Run, "step", NodeWordActs.Approve, null, () => _now).Done);
    }

    [Fact]
    public void An_observation_settles_whoever_holds_the_step()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        Start(StepRoles.ReleaseManager, "done", Risk());

        Assert.True(RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, "seen released", () => _now, settle: SettleBy.Observation).Done);
    }

    [Fact]
    public void The_owner_role_ends_the_work_on_any_step_and_answers_a_decision_whoever_holds_it()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        Start(StepRoles.ReleaseManager, "done", Risk());

        Assert.Equal(SettleBy.Owner, SettleBy.Of(NodeWordActs.Stop, null, null, null).Who);
        Assert.Equal(SettleBy.Owner, SettleBy.Of(NodeWordActs.Decide, null, null, null).Who);
        Assert.Equal(CastHolders.Person, SettleBy.Of(NodeWordActs.Decide, null, "release manager", 1).Who);
        Assert.Equal(SettleBy.Line, SettleBy.Of(NodeWordActs.Accept, new NodeWordBy("landing", "d1"), null, null).Who);
        Assert.True(RunNodeWords.Word(Records, Run, "step", NodeWordActs.Stop, "end it", () => _now).Done);
    }

    /// <summary>
    /// A press speaks as its channel: a working session's own press as its agent, Ask Daoris's as its own, an appointed role's as an agent; the
    /// person's window, card and terminal as the person. A delegated approval of a marked step is refused, its stop too; the person's is taken.
    /// </summary>
    [Fact]
    public void A_delegated_press_on_a_marked_approval_is_refused_and_the_persons_own_is_admitted()
    {
        Assert.Equal(CastHolders.Agent, SettleBy.OfChannel(TerminalTierBy.Session, false, 1)!.Who);
        Assert.Equal(SettleBy.AskDaoris, SettleBy.OfChannel(TerminalTierBy.AskDaoris, false, 1)!.Who);
        Assert.Equal(CastHolders.Agent, SettleBy.OfChannel(TerminalTierBy.Card, true, null)!.Who);
        Assert.All(new[] { TerminalTierBy.Window, TerminalTierBy.Card, TerminalTierBy.Terminal }, by => Assert.Null(SettleBy.OfChannel(by, false, 1)));

        var marked = Risk(PlaceMarks.ProductionReleases);
        Start(StepRoles.Approver, "approved", marked);
        foreach (var channel in new[] { TerminalTierBy.Session, TerminalTierBy.AskDaoris })
        {
            var delegated = SettleBy.OfChannel(channel, false, 1);
            Assert.Equal(SettleDoor.Marked, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Approve, null, () => _now, epoch: 1, settle: delegated).Refused);
            Assert.Equal(SettleDoor.Marked, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Stop, "end it", () => _now, epoch: 1, settle: delegated).Refused);
        }

        Assert.True(RunNodeWords.Word(Records, Run, "step", NodeWordActs.Approve, null, () => _now, epoch: 1).Done);
    }

    [Fact]
    public void An_owner_stop_from_a_stale_row_is_admitted()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        Start(StepRoles.ReleaseManager, "done", Risk());
        Assert.True(StepHolding.Act(Records, Casts, Risk(), Run, "step", CastActs.Hand, "agent:beta", null, _now.AddMinutes(1)).Done);

        Assert.True(RunNodeWords.Word(Records, Run, "step", NodeWordActs.Stop, "end it", () => _now, epoch: 1).Done);
    }

    [Fact]
    public void The_lines_word_is_refused_on_an_agent_held_step_and_taken_on_a_person_held_one()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        Start(StepRoles.ReleaseManager, "done", Risk());
        Assert.Equal(SettleDoor.NotHolder, RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, null, () => _now, settle: new SettleBy(SettleBy.Line, 1)).Refused);

        Assert.True(StepHolding.Act(Records, Casts, Risk(), Run, "step", CastActs.Take, null, null, _now.AddMinutes(1)).Done);
        Assert.True(RunNodeWords.Word(Records, Run, "step", NodeWordActs.Done, null, () => _now, settle: new SettleBy(SettleBy.Line, 2)).Done);
    }

    /// <summary>
    /// A hold act names the epoch its row read and the channel it came by: a row read before a hand-over moves nothing, and an agent hands on
    /// only a step it holds and never takes one.
    /// </summary>
    [Fact]
    public void A_hold_act_from_an_ended_epoch_or_an_agent_that_does_not_hold_the_step_moves_nothing()
    {
        Casts.Cast("machine", StepRoles.ReleaseManager, "agent:alpha", null, _now);
        Start(StepRoles.ReleaseManager, "done", Risk());
        var agent = (string who) => new SettleBy(who, 1);

        Assert.Equal(SettleDoor.NotHolder, StepHolding.Act(Records, Casts, Risk(), Run, "step", CastActs.Take, null, null, _now, 1, agent(CastHolders.Agent)).Refused);
        Assert.Equal(SettleDoor.NotHolder, StepHolding.Act(Records, Casts, Risk(), Run, "step", CastActs.Hand, "agent:gamma", null, _now, 1, agent("agent:beta")).Refused);
        Assert.True(StepHolding.Act(Records, Casts, Risk(), Run, "step", CastActs.Hand, "agent:beta", null, _now.AddMinutes(1), 1, agent("agent:alpha")).Done);

        var stale = StepHolding.Act(Records, Casts, Risk(), Run, "step", CastActs.Take, null, null, _now.AddMinutes(2), epoch: 1);
        Assert.Equal(StepTenureFiles.EndedEpoch, stale.Refused);
        Assert.Equal(("agent:beta", 2), (stale.Tenure!.Holder, stale.Tenure.Epoch));
    }

    [Fact]
    public void An_owner_role_step_is_handed_to_nobody_but_the_person()
    {
        var held = Start(StepRoles.Owner, "done", Risk());
        Assert.Equal(CastHolders.Person, held.Holder);

        Assert.Equal(SettleDoor.OwnerRole, StepHolding.Act(Records, Casts, Risk(), Run, "step", CastActs.Hand, "agent:beta", null, _now.AddMinutes(1)).Refused);
        Assert.Equal(CastHolders.Person, new StepTenureFiles(_home, () => true).Current(RunUnits.IdOf(Run), "step", 1)!.Holder);
    }

    /// <summary>
    /// The census: who may settle a step is the door's alone, and the door reads the tenure, the speaker and the mark, never the step's kind;
    /// every door that settles a run step's attempt asks it.
    /// </summary>
    [Fact]
    public void No_settle_path_asks_the_steps_kind_who_may_settle_it()
    {
        var door = Code(File.ReadAllText(SourceFiles.Driver("SettleDoor.cs")));
        Assert.DoesNotMatch(new Regex(@"\bKind\b|NodeKinds\.|NodeKit\.|\w+Node\b|NodeAuthorit"), door);

        foreach (var (settles, asks) in new[] { ("RunNodeWords.cs", "SettleDoor.Admit("), ("WorkPersonWord.cs", "SettleDoor.Admit("), ("StepHolding.cs", "SettleDoor.HandRefused(") })
        {
            var source = Code(File.ReadAllText(SourceFiles.Driver(settles)));
            Assert.Contains(asks, source, StringComparison.Ordinal);
            // The epoch is judged by the door alone: no settle path reads an ended tenure for itself.
            Assert.DoesNotContain(".Ended(", source, StringComparison.Ordinal);
        }

        // Every route that settles or passes on a step names its speaker by the caller's channel, never the person by default, and its epoch.
        foreach (var route in new[] { "WorkflowRoutes.cs", "LinesRoutes.cs", "CastRoutes.cs" })
        {
            var source = Code(File.ReadAllText(SourceFiles.Find(SourceFiles.Project("Daoris.Desktop.Modules"), route)));
            Assert.Contains("SpeakerOf(press.Epoch)", source, StringComparison.Ordinal);
            Assert.Contains("EpochOf(press.Epoch)", source, StringComparison.Ordinal);
        }

        // Each standing attempt is cast its holder before the pass's rounds, not only after them.
        var runtime = Code(File.ReadAllText(SourceFiles.Driver("NodeRuntime.cs")));
        Assert.True(runtime.IndexOf("AttemptTenures.Keep(", StringComparison.Ordinal) < runtime.IndexOf("for (var round = 0;", StringComparison.Ordinal));

        static string Code(string text) => Regex.Replace(text, @"^\s*///.*$|//.*$", "", RegexOptions.Multiline);
    }
}
