using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HoldFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A done nothing checked (its ask gave its quest no requirement) waits only where the actor configuration names who reads it: a role its
/// <c>quest.holds</c> step names, or the place's cast of the <c>ask-reader</c> (a high-risk place's holder, the person, among them). With
/// none named it goes on by itself, said in one line. Where it waits, the role's holder passes it, never the run's own working session,
/// and the person's Accept naming it takes it from an agent holder in the same press. Seen failing first: it always waited on a role the
/// person held by default, and any session speaking as an agent passed a bare <c>agent</c> holder's hold, the worker among them.
/// </summary>
public sealed class UncheckedDoneHoldersTests : IDisposable
{
    private const string Run = "q-holds";

    private const string Unchecked = "its done is unchecked: ask #a1 gave its quest no requirement, so nothing compared the done with what was asked";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-holdsactor-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly DateTimeOffset _now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private PlaceCastFiles Casts => new(_home, () => true);

    private NodeRecordFiles Records => new(_home, () => true);

    private static PlaceRiskBook Risk => PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = PlaceRisks.Low });

    /// <summary>
    /// A run whose <c>holds</c> step (naming <paramref name="role"/>, or none) holds an unchecked done at its first attempt, a <c>review</c>
    /// after it not reached yet, held as the place casts it now.
    /// </summary>
    private (NodeDeclaration Node, NodeRecord Record, StepTenure Tenure) Start(string? role = null)
    {
        var named = role is null ? "" : $$""","role":"{{role}}" """;
        var digest = RunHome.SaveGraph(_home, $$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"holds","kind":"quest.holds","version":1,"reach":"observed"{{named}}},
                      {"id":"review","kind":"review","version":1,"reach":"pressed"},
                      {"id":"handed","kind":"land","version":1,"reach":"performed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"holds"},{"id":"h>r","kind":"after","from":"holds","to":"review"},
                      {"id":"r>l","kind":"after","from":"review","to":"handed"}]}
            """);
        RunHome.Bind(_home, Run, digest, _now);
        var unit = RunUnits.IdOf(Run);
        Records.Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["claimed"] = NodeLifecycle.Start(unit, "claimed") with { State = NodeStates.Reached, Attempt = 1, Since = _now },
                ["holds"] = NodeLifecycle.Start(unit, "holds") with
                {
                    State = NodeStates.Active, Attempt = 1, Tries = 1, Since = _now, Holds = Unchecked,
                    Binding = new NodeAcceptance(unit, "c1", digest, "holds", 1, 1),
                },
            },
        });
        var graph = new GraphRuns(Records).Find(Run)!.Version.Graph!;
        AttemptTenures.Keep(Records, unit, graph.Nodes, "work", null, Risk, _now);
        return (graph.Nodes.Single(each => each.Id == "holds"), Records.Read(unit)!, new StepTenureFiles(_home, () => true).Current(unit, "holds", 1)!);
    }

    /// <summary>An ask's quest with no requirement, its done in, observed by the hold with the place's cast as <paramref name="cast"/> reads it.</summary>
    private static NodeObservation Observed(NodeDeclaration hold, Func<string, CastNow>? cast)
    {
        var asked = QuestUnit(Quest("Done") with { From = "ask #a1b2c3" });
        return NodeObserving.Observe(asked, hold, Bound(asked, hold, "q-rev"), new NodeGitReads { Content = (_, _) => null, Cast = cast });
    }

    private static Func<string, CastNow> CastOf(PlaceCastBook book, string risk = PlaceRisks.Low)
    {
        var place = PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "default", Risk = risk }).At("default", "engine");
        return role => book.At(role, "default", "engine", place);
    }

    private static NodeDeclaration Named(string role) => Hold(NodeKinds.QuestHolds) with { Role = role };

    [Fact]
    public void With_no_role_named_and_no_cast_an_unchecked_done_goes_on_by_itself_said_in_one_line()
    {
        var seen = Observed(Hold(NodeKinds.QuestHolds), CastOf(PlaceCastBook.Empty));

        Assert.Equal((NodeStates.Reached, NodeSource.Holds), (seen.Outcome, seen.Source));
        Assert.Equal(UncheckedDones.GoesOn, seen.Words);
        Assert.Equal("no one is set to read this done against its ask, so it goes on", UncheckedDones.GoesOn);
        Assert.Null(seen.Holds);
    }

    [Fact]
    public void A_cast_a_named_role_or_a_high_risk_place_makes_it_wait()
    {
        var steward = PlaceCastBook.Empty.With(PlaceRiskBook.MachineKey, StepRoles.AskReader, new RoleCast("agent:steward", "it files the asks", DateTimeOffset.UnixEpoch));

        Assert.Null(Observed(Hold(NodeKinds.QuestHolds), CastOf(steward)).Outcome);
        Assert.Null(Observed(Named(StepRoles.Owner), CastOf(PlaceCastBook.Empty)).Outcome);
        Assert.Null(Observed(Named(StepRoles.Approver), CastOf(PlaceCastBook.Empty)).Outcome);
        Assert.Null(Observed(Hold(NodeKinds.QuestHolds), CastOf(PlaceCastBook.Empty, PlaceRisks.High)).Outcome);
        Assert.True(UncheckedDones.Says(Observed(Hold(NodeKinds.QuestHolds), CastOf(steward)).Holds));
    }

    [Fact]
    public void The_pass_reads_the_cast_from_the_home_as_its_files_say_it_now()
    {
        Assert.Equal(CastWhy.Default, CastReading.Now(_home, "work", "engine")(StepRoles.AskReader).Why);

        Assert.Null(AgentSettles.CastAt(Casts, "workspace/work", StepRoles.AskReader, "agent:lead", "the line reads the dones", _now));

        Assert.Equal((CastWhy.Cast, "agent:lead"), (CastReading.Now(_home, "work", "engine")(StepRoles.AskReader).Why, CastReading.Now(_home, "work", "engine")(StepRoles.AskReader).Holder));
    }

    [Fact]
    public void The_ask_reader_is_never_cast_or_handed_to_the_bare_agent_whose_word_any_agent_speaks()
    {
        Assert.NotNull(AgentSettles.CastAt(Casts, "machine", StepRoles.AskReader, CastHolders.Agent, "any agent", _now));
        Assert.Empty(Casts.Journal());
        Assert.NotNull(AgentSettles.Hand(Hold(NodeKinds.QuestHolds), CastHolders.Agent));
        Assert.Null(AgentSettles.Hand(Hold(NodeKinds.QuestHolds), "agent:steward"));
    }

    [Fact]
    public void The_steward_cast_to_the_ask_reader_settles_the_unchecked_done_with_its_words_and_another_agent_is_refused()
    {
        Assert.Null(AgentSettles.CastAt(Casts, "machine", StepRoles.AskReader, "agent:steward", "it files the asks", _now));
        var (node, record, tenure) = Start();
        Assert.Equal("agent:steward", tenure.Holder);
        var wait = StepWaiting.Read(record, node, WorkPerformer.StatusOf(record, "holds"), tenure);
        Assert.Equal((PersonWaits.Pass, StepRoles.AskReader, StepHolders.Agent, "steward"), (wait?.Why, wait?.Role, wait?.Holder.Kind, wait?.Holder.Name));

        var other = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Looks fine.", () => _now, settle: new SettleBy("agent:builder", 1));
        var steward = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "It does what the ask said: the stall is gone.", () => _now,
            settle: new SettleBy("agent:steward", 1) { Session = "s-steward" }, workers: ["s-worker"]);

        Assert.Equal(SettleDoor.NotHolder, other.Refused);
        Assert.True(steward.Done, steward.Refused);
    }

    [Theory]
    [InlineData(CastHolders.Agent)]
    [InlineData("agent:claude")]
    public void The_runs_own_working_session_never_passes_its_own_done_whoever_it_is_cast_as(string holder)
    {
        // A bare `agent` is refused at the cast today; one kept from before is read as it stands.
        Assert.Null(Casts.Cast("machine", StepRoles.AskReader, holder, "the provider reads the dones", _now));
        Start();

        var worker = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Done as asked.", () => _now,
            settle: new SettleBy("agent:claude", 1) { Session = "s-worker" }, workers: ["s-helper", "s-worker"]);
        var unread = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Done as asked.", () => _now,
            settle: new SettleBy("agent:claude", 1) { Session = "s-other" }, workers: null);
        var another = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Read against the ask: done.", () => _now,
            settle: new SettleBy("agent:claude", 1) { Session = "s-other" }, workers: ["s-helper", "s-worker"]);

        Assert.Equal(RunNodeWords.OwnDone, worker.Refused);
        Assert.Contains("never passes", RunNodeWords.Said(worker), StringComparison.Ordinal);
        Assert.Equal(RunNodeWords.OwnDone, unread.Refused);
        Assert.True(another.Done, another.Refused);
    }

    [Fact]
    public void A_step_naming_the_owner_waits_on_the_owner_whose_row_offers_the_pass_and_whose_press_passes_it()
    {
        Casts.Cast("machine", StepRoles.AskReader, "agent:steward", "it files the asks", _now);
        var (node, record, tenure) = Start(StepRoles.Owner);

        var row = HeldSteps.Step(Run, "engine", "work", Run, node, record, tenure);
        Assert.NotNull(row);
        Assert.Equal((PersonWaits.Pass, StepRoles.Owner, StepHolders.Person), (row.Why, row.Role, row.Holder));
        Assert.Contains(NodeWordActs.Skip, row.Presses);

        Assert.Equal(SettleDoor.NotHolder, RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Fine.", () => _now, settle: new SettleBy("agent:steward", 1)).Refused);
        Assert.True(RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "It does what I asked.", () => _now, epoch: row.Epoch).Done);
    }

    [Fact]
    public void The_persons_accept_naming_the_unchecked_done_takes_it_from_its_agent_holder_in_the_same_press()
    {
        Assert.Null(AgentSettles.CastAt(Casts, "machine", StepRoles.AskReader, "agent:steward", "it files the asks", _now));
        Start();

        var kept = UncheckedAccepts.Keep(Records, Run, "review", "rev-1", () => _now);

        Assert.True(kept.Done, kept.Refused);
        var now = new StepTenureFiles(_home, () => true).Current(RunUnits.IdOf(Run), "holds", 1)!;
        Assert.Equal((CastHolders.Person, 2), (now.Holder, now.Epoch));
        var pass = Records.Read(RunUnits.IdOf(Run))!.Pending.Single(word => word.Node == "holds");
        Assert.Equal((NodeWordActs.Skip, new NodeWordEpoch(1, 2)), (pass.Act, pass.Epoch));
        Assert.Contains(Casts.Journal(), entry => entry is { Act: CastActs.Take, Node: "holds", Holder: CastHolders.Person, Why: "pressed accept" });
    }

    [Fact]
    public void The_unchecked_reason_names_the_role_that_answers_it_never_you()
    {
        var quest = new QuestView("q7", "ask #a1", "engine", "Fix the stall", "", "Done");

        var said = UncheckedDones.Of(quest);

        Assert.NotNull(said);
        Assert.True(UncheckedDones.Says(said));
        Assert.Contains("`ask-reader`", said, StringComparison.Ordinal);
        Assert.DoesNotContain("what you asked", said, StringComparison.Ordinal);
    }
}
