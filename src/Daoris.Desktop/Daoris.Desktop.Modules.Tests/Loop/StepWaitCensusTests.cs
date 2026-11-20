using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The census of waits: every wait a run's step can stand at (<see cref="PersonWaits.All"/>), read by the driver's one reading
/// (<see cref="StepWaiting.Read"/>) where a step's record makes it, is a row of <i>What needs you</i> carrying at least one press of its
/// own or the page where its act is taken. Seen failing first on a plugin's role wait (its row carried the page but no press field a
/// census could read), on a second opinion's vacancy (no reading of it at all), and on an unchecked done held at <c>quest.holds</c> for the
/// person's pass (read as no wait, so no row offered the pass).
/// </summary>
[Trait(Category.Name, Category.Census)]
public sealed class StepWaitCensusTests
{
    private const string Run = "q7";
    private const string Unit = "run:q7";
    private const string Page = "https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90";
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 51, 0, TimeSpan.Zero);

    /// <summary>The row kinds whose door is their press: a review opens its Accept card, a part left its session (rule 57).</summary>
    private static readonly HashSet<string> DoorIsThePress = new(StringComparer.Ordinal) { "review", "left" };

    private static NodeRecord Standing(string node, NodeStatus status, params NodeAttempt[] attempts) => new(Unit)
    {
        Statuses = new Dictionary<string, NodeStatus> { [node] = status },
        Attempts = [.. attempts],
    };

    private static NodeStatus Active(string node) => new(Unit, node, NodeStates.Active) { Attempt = 1, Tries = 1, Since = Now };

    private static readonly Func<string, string> PersonCast = _ => CastHolders.Person;

    /// <summary>A step standing at <paramref name="why"/>, as a run's record keeps it: its declaration and the record.</summary>
    private static (NodeDeclaration Node, NodeRecord Record)? StepAt(string why) => why switch
    {
        PersonWaits.Task => (new("sign", NodeKinds.Task, 1, NodeReach.Pressed) { Outcomes = ["done"] }, Standing("sign", Active("sign"))),
        PersonWaits.Approval => (new("sign", NodeKinds.Task, 1, NodeReach.Pressed) { Outcomes = ["approved"] }, Standing("sign", Active("sign"))),
        PersonWaits.GoAhead => (new("go", NodeKinds.GoAhead, 1, NodeReach.Pressed), Standing("go", Active("go"))),
        PersonWaits.Choose => (new("return", NodeKinds.Return, 1, NodeReach.Performed), Standing("return", Active("return") with { Code = ReturnNode.BothChanged })),
        PersonWaits.Vacant => (new("opinion", NodeKinds.Opinion, 1, NodeReach.Observed), Standing("opinion", Active("opinion") with { Holds = OpinionGateReading.Vacant })),
        PersonWaits.Pass => (new("holds", NodeKinds.QuestHolds, 1, NodeReach.Observed), Standing("holds", Active("holds") with
        {
            Holds = UncheckedDones.Of(new("q7", "ask #a1", "engine", "Docs", "", "Taken")),
        })),
        PersonWaits.Actor => (
            new("release", "acme.deploy/release", 1, NodeReach.Performed),
            Standing("release", Active("release") with { Unreconciled = 1 }, new NodeAttempt("release", 1, "acme.deploy",
                new WorkPerformCall(Unit, "acme.deploy/release", 1, "i-1", "k-1", "tip", [], Now.AddHours(1)), AttemptStates.Sent)
            {
                RecordedAt = Now, Generation = 1, Deliveries = 1,
                Progress = new StepProgress("approver", "Approve Production in Azure DevOps.", Page, "Production", Now),
            })),
        _ => null,
    };

    /// <summary>The facts the attention slice gathers for a step standing at <paramref name="why"/>: the driver's own writing of it.</summary>
    private static AttentionFacts FactsAt(string why)
    {
        if (StepAt(why) is { } step)
        {
            var status = WorkPerformer.StatusOf(step.Record, step.Node.Id);
            var wait = StepWaiting.Read(step.Record, step.Node, status);
            Assert.Equal(why, wait?.Why);
            var held = HeldSteps.Step(Run, "engine", "aurora", Run, step.Node, step.Record, cast: PersonCast);
            Assert.NotNull(held);
            return new() { Steps = [JsonSerializer.SerializeToNode(held, Shenora.Core.Ipc.IpcJson.Options)!.AsObject()] };
        }

        return why switch
        {
            // Sessions' placing of the run's newest session in Needs you, by the wait the run stands at.
            PersonWaits.Accept or PersonWaits.Left => new()
            {
                Sessions = [Session("""{"id":"s1","quest":"q7","state":"completed","updated":"2026-11-16T09:51:00Z"}""")],
                Groups = [With("""{"session":"s1","group":"needs-you","reason":"review","shown":"completed","archived":false,"teammate":false}""",
                    $$"""{"reason":"{{(why == PersonWaits.Accept ? SessionReason.Review : SessionReason.Left)}}"}""")],
            },
            // An attempt held unknown for the person's word is the held attempt's row, with its words.
            PersonWaits.Held => new()
            {
                Held = [JsonNode.Parse("""
                    {"unit":"run:q7","node":"release","attempt":1,"plugin":"acme.deploy","step":"acme.deploy/release","nodeState":"unknown","why":"unknown",
                     "at":"2026-11-16T09:51:00Z","said":"its plugin was asked and cannot say what became of it"}
                    """)!.AsObject()],
            },
            _ => throw new InvalidOperationException($"the census has no step for the wait `{why}`: add one, with the row it is"),
        };
    }

    private const string Going = "going";

    /// <summary>Every run state a wait stands in: a run going on holds every kind, one ended failed-held its left step alone. Seen failing first on that left step.</summary>
    private static readonly (string Why, string State)[] States = [.. PersonWaits.All.Select(why => (why, Going)), (PersonWaits.Left, EmptyRunEnds.FailedHeld)];

    public static TheoryData<string, string> Waits
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (why, state) in States) data.Add(why, state);
            return data;
        }
    }

    private static readonly NodeDeclaration Push = new("push", NodeKinds.Push, 1, NodeReach.Performed);

    private static NodeRecord EndedAtPush => new(Unit)
    {
        Ended = new RunEnd(Run, Now) { Outcome = RunOutcomes.Failed, Node = "push" },
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["push"] = new(Unit, "push", NodeStates.Failed) { Attempt = 1, Tries = 1, Code = PushDoor.Refused, Since = Now },
            ["sign"] = Active("sign"),
        },
    };

    private static AttentionFacts FactsAt(string why, string state) => state == Going ? FactsAt(why) : new()
    {
        Steps = [JsonSerializer.SerializeToNode(LeftSteps.Step(Run, "engine", "aurora", Push, EndedAtPush), Shenora.Core.Ipc.IpcJson.Options)!.AsObject()],
        Sessions = [Session("""{"id":"s1","quest":"q7","state":"awaiting-step","updated":"2026-11-16T09:51:00Z"}""")],
        Groups = [With("""{"session":"s1","group":"needs-you","reason":"left","shown":"awaiting-step","archived":false,"teammate":false}""",
            """{"reason":"left","waits":[{"run":"q7","node":"push","why":"left","repository":"engine","role":"lead","ended":true}]}""")],
    };

    [Fact]
    public void A_run_ended_failed_waits_on_its_left_step_alone()
    {
        var graph = new NodeGraph([Push, new("sign", NodeKinds.Task, 1, NodeReach.Pressed) { Outcomes = ["done"] }], [], ["push"], "push");
        var binding = new WorkflowRunBinding(Run, "engine", "aurora", "docs", WorkflowLevels.Repository, Now) { Version = 1, Digest = "gph-0123456789abcdef0123456789abcdef" };
        var run = new GraphRun(binding, new DecodedVersion(1, binding.Digest, graph, binding.Digest, null), EndedAtPush.Ended);

        var wait = Assert.Single(PersonWaitReader.Of([run], _ => EndedAtPush));
        Assert.Equal(("push", PersonWaits.Left, true), (wait.Node, wait.Why, wait.Ended));
        Assert.Empty(PersonWaitReader.Of([run], _ => EndedAtPush with { Pending = [new NodeWord("push", NodeWordActs.Again, null, Now)] }));
    }

    [Theory]
    [MemberData(nameof(Waits))]
    public void Every_wait_a_step_can_stand_at_is_a_row_with_a_press_or_its_page(string why, string state)
    {
        var row = Assert.Single(Rows(FactsAt(why, state)));
        var kind = row["kind"]!.GetValue<string>();
        if (DoorIsThePress.Contains(kind)) return;
        if (kind == "work")
        {
            Assert.NotNull(row["held"]);
            return;
        }

        Assert.Equal("step", kind);
        var step = (JsonObject)row["step"]!;
        Assert.Equal(why, step["why"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(step["role"]?.GetValue<string>()), $"`{why}` names no role it waits on");
        var presses = (step["presses"] as JsonArray)?.Count ?? 0;
        Assert.True(presses > 0 || step["url"] is not null || step["opens"] is not null, $"`{why}`'s row has no press and no page");
        Assert.Equal(Normal(row), Normal(AttentionStateSlice.Picked(row)));
    }

    [Fact]
    public void Every_wait_a_step_can_stand_at_has_its_card_in_the_session_pages_census()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);
        while (at is not null && !File.Exists(Path.Combine(at.FullName, "daoris.json"))) at = at.Parent;
        Assert.NotNull(at);
        var census = File.ReadAllText(Path.Combine(at.FullName, "src", "Daoris.Web", "src", "work", "head", "RunWaitsHead.test.tsx"));

        Assert.All(States, each => Assert.Contains($"'{each.Why}{(each.State == Going ? "" : "@" + each.State)}': ", census, StringComparison.Ordinal));
    }

    [Fact]
    public void A_plugins_role_wait_carries_its_page_and_a_second_opinions_vacancy_opens_the_run_where_it_is_answered()
    {
        var actor = (JsonObject)Assert.Single(Rows(FactsAt(PersonWaits.Actor)))["step"]!;
        Assert.Equal((Page, "approver", "person"), (actor["url"]!.GetValue<string>(), actor["role"]!.GetValue<string>(), actor["holder"]!.GetValue<string>()));

        var vacancy = (JsonObject)Assert.Single(Rows(FactsAt(PersonWaits.Vacant)))["step"]!;
        Assert.Equal((StepWaiting.OpensRun, AppointedRoles.Supervisor),(vacancy["opens"]!.GetValue<string>(), vacancy["role"]!.GetValue<string>()));
    }

    /// <summary>Every hold its role's agent holds by default is served (a task's Done, handed) or a vacancy listed apart. Seen failing first on each.</summary>
    [Theory]
    [InlineData(PersonWaits.Task, false)]
    [InlineData(PersonWaits.Accept, true)]
    [InlineData(PersonWaits.Approval, true)]
    [InlineData(PersonWaits.GoAhead, true)]
    [InlineData(PersonWaits.Choose, true)]
    [InlineData(PersonWaits.Vacant, true)]
    [InlineData(PersonWaits.Pass, true)]
    [InlineData(PersonWaits.Actor, true)]
    public void Every_hold_its_roles_agent_holds_by_default_is_served_or_a_vacancy_listed_apart(string why, bool vacant)
    {
        var (node, record) = why == PersonWaits.Accept
            ? (new NodeDeclaration("review", NodeKinds.Review, 1, NodeReach.Pressed), Standing("review", Active("review")))
            : StepAt(why)!.Value;
        if (node.Kind == NodeKinds.Task) node = node with { Role = why == PersonWaits.Task ? "worker" : StepRoles.Approver };
        var held = HeldSteps.Step(Run, "engine", "aurora", Run, node, record, cast: PlaceCastBook.Uncast);

        Assert.NotNull(held);
        Assert.Equal((why, StepHolders.Agent, vacant, !vacant), (held.Why, held.Holder, held.Vacant, held.Handed));
        Assert.Equal(vacant ? Now.AddHours(VacancySetting.Default) : null, held.GivesUp);
        var row = Assert.Single(Rows(new AttentionFacts { Steps = [JsonSerializer.SerializeToNode(held, Shenora.Core.Ipc.IpcJson.Options)!.AsObject()] }));
        Assert.Equal(vacant ? AttentionOwnership.VacantClass : AttentionOwnership.AgentsClass, AttentionOwnership.Of(row, AttentionTakers.None).Class);
    }

    [Fact]
    public void A_plugins_role_wait_an_agent_is_cast_to_is_served_and_its_default_agent_is_a_vacancy()
    {
        var (node, record) = StepAt(PersonWaits.Actor)!.Value;

        Assert.Null(HeldSteps.Step(Run, "engine", "aurora", Run, node, record, cast: _ => "agent:beta", serves: _ => true));
        Assert.True(HeldSteps.Step(Run, "engine", "aurora", Run, node, record, cast: PlaceCastBook.Uncast, serves: _ => false)!.Vacant);
    }

    /// <summary>
    /// A plugin's role wait with no page is acted on outside Daoris, where nothing observes it: its row offers <i>Done outside Daoris</i> on
    /// the attempt out now, its link the evidence; one with a page is taken on the page and observed there.
    /// </summary>
    [Fact]
    public void A_plugins_role_wait_with_no_page_offers_done_outside_daoris_on_its_attempt()
    {
        var (node, record) = StepAt(PersonWaits.Actor)!.Value;
        var attempt = record.Attempts[0];
        var bare = record with { Attempts = [attempt with { Progress = attempt.Progress! with { Url = null } }] };

        var row = (JsonObject)Assert.Single(Rows(new AttentionFacts
        {
            Steps = [JsonSerializer.SerializeToNode(HeldSteps.Step(Run, "engine", "aurora", Run, node, bare, cast: PersonCast), Shenora.Core.Ipc.IpcJson.Options)!.AsObject()],
        }))["step"]!;
        Assert.Contains(HeldResolutions.DoneOutside, ((JsonArray)row["presses"]!).Select(each => each!.GetValue<string>()));
        Assert.Equal((Unit, 1), (row["held"]!["unit"]!.GetValue<string>(), row["held"]!["attempt"]!.GetValue<int>()));

        var paged = HeldSteps.Step(Run, "engine", "aurora", Run, node, record, cast: PersonCast)!;
        Assert.DoesNotContain(HeldResolutions.DoneOutside, paged.Presses);
        Assert.Null(paged.Held);
    }
}
