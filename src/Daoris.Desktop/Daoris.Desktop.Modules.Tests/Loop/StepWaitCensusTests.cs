using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The census of waits: every wait a run's step can stand at (<see cref="PersonWaits.All"/>), read by the driver's one reading
/// (<see cref="StepWaiting.Read"/>) where a step's record makes it, is a row of <i>What needs you</i> carrying at least one press of its
/// own or the page where its act is taken. Seen failing first on a plugin's role wait (its row carried the page but no press field a
/// census could read) and on a second opinion's vacancy (no reading of it at all).
/// </summary>
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

    /// <summary>A step standing at <paramref name="why"/>, as a run's record keeps it: its declaration and the record.</summary>
    private static (NodeDeclaration Node, NodeRecord Record)? StepAt(string why) => why switch
    {
        PersonWaits.Task => (new("sign", NodeKinds.Human, 1, NodeReach.Pressed) { Outcomes = ["done"] }, Standing("sign", Active("sign"))),
        PersonWaits.Approval => (new("sign", NodeKinds.Human, 1, NodeReach.Pressed) { Outcomes = HumanNode.Approval }, Standing("sign", Active("sign"))),
        PersonWaits.GoAhead => (new("go", NodeKinds.GoAhead, 1, NodeReach.Pressed), Standing("go", Active("go"))),
        PersonWaits.Choose => (new("return", NodeKinds.Return, 1, NodeReach.Performed), Standing("return", Active("return") with { Code = ReturnNode.BothChanged })),
        PersonWaits.Vacant => (new("opinion", NodeKinds.Opinion, 1, NodeReach.Observed), Standing("opinion", Active("opinion") with { Holds = OpinionGateReading.Vacant })),
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
            var held = HeldSteps.Step(Run, "engine", "aurora", Run, step.Node, step.Record);
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

    public static TheoryData<string> Waits => [.. PersonWaits.All];

    [Theory]
    [MemberData(nameof(Waits))]
    public void Every_wait_a_step_can_stand_at_is_a_row_with_a_press_or_its_page(string why)
    {
        var row = Assert.Single(Rows(FactsAt(why)));
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
    public void A_plugins_role_wait_carries_its_page_and_a_second_opinions_vacancy_opens_the_run_where_it_is_answered()
    {
        var actor = (JsonObject)Assert.Single(Rows(FactsAt(PersonWaits.Actor)))["step"]!;
        Assert.Equal((Page, "approver", "person"), (actor["url"]!.GetValue<string>(), actor["role"]!.GetValue<string>(), actor["holder"]!.GetValue<string>()));

        var vacancy = (JsonObject)Assert.Single(Rows(FactsAt(PersonWaits.Vacant)))["step"]!;
        Assert.Equal((StepWaiting.OpensRun, LineActors.Lead), (vacancy["opens"]!.GetValue<string>(), vacancy["role"]!.GetValue<string>()));
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
            Steps = [JsonSerializer.SerializeToNode(HeldSteps.Step(Run, "engine", "aurora", Run, node, bare), Shenora.Core.Ipc.IpcJson.Options)!.AsObject()],
        }))["step"]!;
        Assert.Contains(HeldResolutions.DoneOutside, ((JsonArray)row["presses"]!).Select(each => each!.GetValue<string>()));
        Assert.Equal((Unit, 1), (row["held"]!["unit"]!.GetValue<string>(), row["held"]!["attempt"]!.GetValue<int>()));

        var paged = HeldSteps.Step(Run, "engine", "aurora", Run, node, record)!;
        Assert.DoesNotContain(HeldResolutions.DoneOutside, paged.Presses);
        Assert.Null(paged.Held);
    }
}
