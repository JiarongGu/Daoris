using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Who an outside system credits with a step's act (W3c row 1): the evidence's <c>by</c> is read at the wire, kept on the attempt its outcome
/// settles, and written on the unit's record beside the older fields, which an older build reads as before and passes over. Seen failing first.
/// </summary>
public sealed class StepCreditTests
{
    private const string Unit = "unit-r";
    private const string Plugin = "daoris-azure-devops";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private static readonly DateTimeOffset At = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly NodeDeclaration Create = new("release-create", Plugin + "/release-create", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    private static WorkStepFrame? Read(JsonObject evidence)
    {
        var frame = new JsonObject { ["step"] = "release-create", ["invocation"] = "i-1", ["state"] = "reached", ["evidence"] = evidence, ["at"] = "2026-11-16T09:00:00Z", ["id"] = "i-1" };
        using var document = JsonDocument.Parse(frame.ToJsonString());
        return WorkWire.ReadStep(document.RootElement, out _);
    }

    private static NodeRecord Sent()
    {
        var call = new WorkPerformCall(Unit, Plugin + "/release-create", 1, "i-1", "k-1", Tip, [], At.AddHours(1));
        var attempt = new NodeAttempt(Create.Id, 1, Plugin, call, AttemptStates.Sent) { RecordedAt = At, Generation = 1, Deliveries = 1 };
        var status = new NodeStatus(Unit, Create.Id, NodeStates.Active)
        {
            Attempt = 1, Tries = 1, Unreconciled = 1, Binding = new NodeAcceptance(Unit, Tip, "graph-1", Create.Id, 1, 1),
        };
        return new NodeRecord(Unit) { Attempts = [attempt], Statuses = new Dictionary<string, NodeStatus> { [Create.Id] = status } };
    }

    [Fact]
    public void The_wire_reads_the_identity_an_outside_system_credits_and_refuses_one_past_its_bound()
    {
        var read = Read(new JsonObject { ["message"] = "Release-91 created by outside: A Person.", ["by"] = "outside: A Person" });
        var unbounded = Read(new JsonObject { ["by"] = new string('a', WorkWire.NameChars + 1) });

        Assert.Equal("outside: A Person", read!.Outcome.Evidence!.By);
        Assert.Null(unbounded);
        Assert.Equal("outside: A Person", WorkWire.Step(read)["params"]!["evidence"]!["by"]!.GetValue<string>());
    }

    [Fact]
    public void A_settled_outcome_keeps_its_credit_on_the_attempt_and_the_record_an_older_build_still_reads()
    {
        var outcome = new WorkOutcome(WorkStates.Reached, Evidence: new WorkEvidence(null, "https://dev.example/release/91", null, "created") { By = "outside: A Person" });

        var (next, _) = new WorkOutcomes(() => At).Outcome(Sent(), Sent().Attempts[0], Create, outcome, 1);
        var settled = next!.Attempts.Single();
        var json = NodeRecordFiles.ToJson(next);
        var back = NodeRecordFiles.Parse(json)!.Attempts.Single();
        var older = JsonNode.Parse(json)!.AsObject();
        older["attempts"]![0]!.AsObject().Remove("by");
        var olderBack = NodeRecordFiles.Parse(older.ToJsonString())!;

        Assert.Equal((AttemptStates.Settled, "outside: A Person"), (settled.State, settled.By));
        Assert.Equal("outside: A Person", back.By);
        // The record without the field (an older build's, or one an older build wrote again) reads as every part but the credit.
        Assert.Equal(NodeRecordFiles.ToJson(next with { Attempts = [settled with { By = null }] }), NodeRecordFiles.ToJson(olderBack));
        Assert.Null(olderBack.Attempts.Single().By);
    }
}
