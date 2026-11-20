using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A done nothing checked (its ask gave its quest no requirement) waits on the role its <c>quest.holds</c> step names, the
/// <c>asker</c> where it names none, and is answered by whoever the actor configuration makes that role's holder: the place's cast (an
/// agent such as the steward, or a supervisor's agent), the line standing in for a person holder, or the owner where the step names the
/// owner, whose wait card offers the pass. Seen failing first: the wait stood on the <c>acceptor</c>, a role no cast may give an agent, so
/// the steward that filed the ask stood refused as not its holder and only the person's skip passed it.
/// </summary>
public sealed class UncheckedDoneHoldersTests : IDisposable
{
    private const string Run = "q-holds";
    private const string Asker = "asker";

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

    /// <summary>A run whose <c>holds</c> step (naming <paramref name="role"/>, or none) holds an unchecked done at its first attempt, held as the place casts it now.</summary>
    private (NodeDeclaration Node, NodeRecord Record, StepTenure Tenure) Start(string? role = null)
    {
        var named = role is null ? "" : $$""","role":"{{role}}" """;
        var digest = RunHome.SaveGraph(_home, $$"""
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                      {"id":"holds","kind":"quest.holds","version":1,"reach":"observed"{{named}}},
                      {"id":"handed","kind":"land","version":1,"reach":"performed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"holds"},{"id":"h>l","kind":"after","from":"holds","to":"handed"}]}
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

    [Fact]
    public void An_unchecked_done_waits_on_the_asker_its_kind_gives_held_by_the_person_until_a_cast_names_another()
    {
        var (node, record, tenure) = Start();

        var wait = StepWaiting.Read(record, node, WorkPerformer.StatusOf(record, "holds"), tenure);

        Assert.Equal(Asker, StepRoles.Of(node));
        Assert.Equal((PersonWaits.Pass, Asker, StepHolders.Person), (wait?.Why, wait?.Role, wait?.Holder.Kind));
    }

    [Fact]
    public void The_steward_cast_to_the_asker_settles_the_unchecked_done_with_its_words_and_another_agent_is_refused()
    {
        Assert.Null(AgentSettles.CastAt(Casts, "machine", Asker, "agent:steward", "it files the asks", _now));
        var (node, record, tenure) = Start();
        Assert.Equal("agent:steward", tenure.Holder);
        var wait = StepWaiting.Read(record, node, WorkPerformer.StatusOf(record, "holds"), tenure);
        Assert.Equal((PersonWaits.Pass, Asker, StepHolders.Agent, "steward"), (wait?.Why, wait?.Role, wait?.Holder.Kind, wait?.Holder.Name));

        var other = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Looks fine.", () => _now, settle: new SettleBy("agent:builder", 1));
        var steward = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "It does what the ask said: the stall is gone.", () => _now, settle: new SettleBy("agent:steward", 1));

        Assert.Equal(SettleDoor.NotHolder, other.Refused);
        Assert.True(steward.Done, steward.Refused);
    }

    [Fact]
    public void A_supervisors_agent_cast_to_the_asker_at_the_workspace_settles_it()
    {
        Assert.Null(AgentSettles.CastAt(Casts, "workspace/work", Asker, "agent:lead", "the line reads the dones", _now));
        Start();

        var lead = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Read against the ask: done.", () => _now, settle: new SettleBy("agent:lead", 1));

        Assert.True(lead.Done, lead.Refused);
    }

    [Fact]
    public void Where_the_person_holds_the_asker_the_lines_supervisor_stands_in_for_them()
    {
        Start();

        var line = RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Read against the ask: done.", () => _now, settle: new SettleBy(SettleBy.Line, 1));

        Assert.True(line.Done, line.Refused);
    }

    [Fact]
    public void A_step_naming_the_owner_waits_on_the_owner_whose_row_offers_the_pass_and_whose_press_passes_it()
    {
        Casts.Cast("machine", Asker, "agent:steward", "it files the asks", _now);
        var (node, record, tenure) = Start(StepRoles.Owner);

        var row = HeldSteps.Step(Run, "engine", "work", Run, node, record, tenure);
        Assert.NotNull(row);
        Assert.Equal((PersonWaits.Pass, StepRoles.Owner, StepHolders.Person), (row.Why, row.Role, row.Holder));
        Assert.Contains(NodeWordActs.Skip, row.Presses);

        Assert.Equal(SettleDoor.NotHolder, RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "Fine.", () => _now, settle: new SettleBy("agent:steward", 1)).Refused);
        Assert.True(RunNodeWords.Word(Records, Run, "holds", NodeWordActs.Skip, "It does what I asked.", () => _now, epoch: row.Epoch).Done);
    }

    [Fact]
    public void The_unchecked_reason_names_the_role_that_answers_it_never_you()
    {
        var quest = new QuestView("q7", "ask #a1", "engine", "Fix the stall", "", "Done");

        var said = UncheckedDones.Of(quest);

        Assert.NotNull(said);
        Assert.True(UncheckedDones.Says(said));
        Assert.Contains("`asker`", said, StringComparison.Ordinal);
        Assert.DoesNotContain("what you asked", said, StringComparison.Ordinal);
    }
}
