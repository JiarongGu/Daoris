using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1b: a unit's node record kept whole across a restart: its statuses with their bindings, scopes and acceptances, its
/// attempts with their calls, its firings and its history; written BOM-less and LF; and a file that does not read never taken for an empty
/// record, since an attempt it holds could then be sent twice.
/// </summary>
public sealed class NodeRecordFilesTests : IDisposable
{
    private const string Unit = "quest:q-17";

    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-noderecord-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private static NodeRecord Full()
    {
        var binding = new NodeAcceptance(Unit, "tip-1", "graph-1", "deploy", 2, 3)
        {
            Inputs = new Dictionary<string, NodePort> { ["approval"] = new("acme/approval@1", "acme/approval@1:a1", "tip-1", "acc-up") },
            PrerequisiteReceipts = ["acc-up", "skp-1"],
        };
        var reached = binding with
        {
            Evidence = new NodePort("acme/receipt@1", "acme/receipt@1:r1", "tip-1", "acc-x"),
            Outputs = new Dictionary<string, NodePort> { ["site"] = new("acme/site@1", "acme/site@1:s1", null, "acc-x") },
        };
        var status = new NodeStatus(Unit, "deploy", NodeStates.Failed)
        {
            Attempt = 3, Tries = 2, Unreconciled = 3, Binding = binding, Scope = new NodeScope("graph-1", "run-1"), Acceptance = reached,
            By = NodeSource.Plugin, Code = "unavailable", Transient = true, Words = "a person's words",
        };
        var call = new WorkPerformCall(Unit, "acme.deploy/deploy", 2, "i-1", "k-1", "tip-1", ["acme/approval@1:a1"], Then.AddMinutes(15));
        return new NodeRecord(Unit)
        {
            Statuses = new Dictionary<string, NodeStatus> { ["deploy"] = status, ["plain"] = NodeLifecycle.Start(Unit, "plain") },
            Attempts =
            [
                new NodeAttempt("deploy", 3, "acme.deploy", call, AttemptStates.Settled)
                {
                    RecordedAt = Then, Generation = 2, Deliveries = 2, Outcome = WorkStates.Failed, Code = "unavailable",
                    RetryAt = Then.AddSeconds(30.5), AskAt = Then.AddMinutes(20),
                },
            ],
            Fired = ["trg-1"],
            History = [new NodeHistory(Then, WireWhy.RunScope, status with { State = NodeStates.Invalidated })],
        };
    }

    /// <summary>The records under the test's home, written as the loop holding its lease writes them.</summary>
    private NodeRecordFiles Files() => new(_home, () => true);

    [Fact]
    public void A_record_is_read_back_whole_after_its_write()
    {
        var files = Files();
        var written = Full();
        files.Write(written);

        var read = Files().Read(Unit)!;

        Assert.Equal(NodeRecordFiles.ToJson(written), NodeRecordFiles.ToJson(read));
        Assert.Equal(written.Statuses["deploy"] with { Binding = null, Acceptance = null }, read.Statuses["deploy"] with { Binding = null, Acceptance = null });
        Assert.Equal(written.Statuses["deploy"].Binding!.Id, read.Statuses["deploy"].Binding!.Id);
        Assert.True(written.Statuses["deploy"].Acceptance!.SameResult(read.Statuses["deploy"].Acceptance!));
        Assert.Equal(written.Attempts[0] with { Call = read.Attempts[0].Call }, read.Attempts[0]);
        Assert.Equal(written.Attempts[0].Call.Inputs, read.Attempts[0].Call.Inputs);
        Assert.Equal("i-1", read.Invoked("i-1")!.Call.Invocation);

        var bytes = File.ReadAllBytes(NodeRecordFiles.PathOf(_home, Unit));
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.DoesNotContain("q-17", Path.GetFileName(NodeRecordFiles.PathOf(_home, Unit)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_unit_with_no_record_reads_empty_and_a_file_that_does_not_read_is_never_taken_for_empty()
    {
        var files = Files();
        Assert.Empty(files.Read(Unit)!.Statuses);

        Directory.CreateDirectory(NodeRecordFiles.FolderOf(_home));
        File.WriteAllText(NodeRecordFiles.PathOf(_home, Unit), "{ \"unit\": \"quest:q-17\", \"attempts\": [ { \"node\": 1 } ] }");

        Assert.Null(files.Read(Unit));
        var (records, unreadable) = files.All();
        Assert.Empty(records);
        Assert.Single(unreadable);
    }

    [Fact]
    public void Every_unsettled_attempt_is_kept_and_only_the_newest_settled_ones()
    {
        var call = new WorkPerformCall(Unit, "acme.deploy/deploy", 1, "i", "k", "tip-1", [], Then);
        var attempts = Enumerable.Range(1, NodeRecord.SettledKept + 5)
            .Select(number => new NodeAttempt("deploy", number, "acme.deploy", call with { Invocation = $"i-{number}" }, AttemptStates.Settled) { RecordedAt = Then })
            .Append(new NodeAttempt("deploy", 99, "acme.deploy", call with { Invocation = "i-open" }, AttemptStates.Unknown) { RecordedAt = Then })
            .ToList();
        var files = Files();
        var open = new NodeStatus(Unit, "deploy", NodeStates.Unknown) { Attempt = 99, Tries = 1, Unreconciled = 99 };
        files.Write(new NodeRecord(Unit) { Statuses = new Dictionary<string, NodeStatus> { ["deploy"] = open }, Attempts = attempts });

        var kept = files.Read(Unit)!.Attempts;

        Assert.Equal(NodeRecord.SettledKept + 1, kept.Count);
        Assert.Equal("i-6", kept[0].Call.Invocation);
        Assert.Equal("i-open", kept[^1].Call.Invocation);
    }

    // ——— FABRIC-PERFORM1h: the review's findings, each seen failing on PERFORM1b first

    [Fact]
    public void Trimming_never_drops_an_attempt_the_status_still_needs()
    {
        var call = new WorkPerformCall(Unit, "acme.deploy/deploy", 1, "i", "k", "tip-1", [], Then);
        NodeAttempt Settled(string node, int number, string invocation) =>
            new(node, number, "acme.deploy", call with { Invocation = invocation }, AttemptStates.Settled) { RecordedAt = Then, Outcome = WorkStates.Reached };
        var newest = NodeRecord.SettledKept + 5;
        var attempts = new List<NodeAttempt>
        {
            // The current attempt of a node reached long ago, and a failure whose declared retry is still to come (Due reads its RetryAt).
            Settled("early", 1, "i-early"),
            Settled("retrying", 1, "i-retrying") with { Outcome = WorkStates.Failed, Code = "unavailable", RetryAt = Then.AddSeconds(30) },
        };
        attempts.AddRange(Enumerable.Range(1, newest).Select(number => Settled("deploy", number, $"i-{number}")));
        var statuses = new Dictionary<string, NodeStatus>
        {
            ["early"] = new(Unit, "early", NodeStates.Reached) { Attempt = 1, Tries = 1 },
            ["retrying"] = new(Unit, "retrying", NodeStates.Failed) { Attempt = 1, Tries = 1, Code = "unavailable", Transient = true },
            ["deploy"] = new(Unit, "deploy", NodeStates.Reached) { Attempt = newest, Tries = 1 },
        };
        var files = Files();
        files.Write(new NodeRecord(Unit) { Statuses = statuses, Attempts = attempts });

        var kept = files.Read(Unit)!.Attempts.Select(attempt => attempt.Call.Invocation).ToList();

        Assert.Contains("i-early", kept);
        Assert.Contains("i-retrying", kept);
        Assert.Equal(NodeRecord.SettledKept + 3, kept.Count);
        Assert.DoesNotContain("i-4", kept);
        Assert.Equal(("i-5", $"i-{newest}"), (kept[2], kept[^1]));
    }

    [Theory]
    [InlineData("no-containers")]
    [InlineData("nodes-not-an-object")]
    [InlineData("no-attempts")]
    [InlineData("no-fired")]
    [InlineData("no-history")]
    [InlineData("a-firing-not-text")]
    [InlineData("an-input-not-text")]
    [InlineData("a-receipt-not-text")]
    [InlineData("no-receipts")]
    [InlineData("an-attempt-state-unknown")]
    [InlineData("a-node-state-unknown")]
    [InlineData("an-attempt-of-no-node")]
    [InlineData("an-attempt-past-its-node")]
    [InlineData("the-current-attempt-missing")]
    [InlineData("a-status-of-another-unit")]
    [InlineData("an-invocation-twice")]
    public void A_record_whose_structure_does_not_hold_is_unreadable_never_empty(string fault)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(NodeRecordFiles.ToJson(Full()))!.AsObject();
        Assert.NotNull(NodeRecordFiles.Parse(root.ToJsonString()));
        var nodes = root["nodes"]!.AsObject();
        var attempt = root["attempts"]![0]!.AsObject();
        switch (fault)
        {
            case "no-containers": root = new System.Text.Json.Nodes.JsonObject { ["unit"] = Unit }; break;
            case "nodes-not-an-object": root["nodes"] = new System.Text.Json.Nodes.JsonArray(); break;
            case "no-attempts": root.Remove("attempts"); break;
            case "no-fired": root.Remove("fired"); break;
            case "no-history": root.Remove("history"); break;
            case "a-firing-not-text": root["fired"]!.AsArray().Add(7); break;
            case "an-input-not-text": attempt["call"]!["inputs"]!.AsArray().Add(7); break;
            case "a-receipt-not-text": nodes["deploy"]!["binding"]!["receipts"]!.AsArray().Add(7); break;
            case "no-receipts": nodes["deploy"]!["binding"]!.AsObject().Remove("receipts"); break;
            case "an-attempt-state-unknown": attempt["state"] = "lost"; break;
            case "a-node-state-unknown": nodes["plain"]!["state"] = "lost"; break;
            case "an-attempt-of-no-node": nodes.Remove("deploy"); break;
            case "an-attempt-past-its-node": nodes["deploy"]!["attempt"] = 2; break;
            case "the-current-attempt-missing": nodes["deploy"]!["attempt"] = 4; break;
            case "a-status-of-another-unit": nodes["plain"]!["unit"] = "quest:q-18"; break;
            case "an-invocation-twice": root["attempts"]!.AsArray().Add(attempt.DeepClone()); break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }

        var json = root.ToJsonString();
        Assert.Null(NodeRecordFiles.Parse(json));
        Directory.CreateDirectory(NodeRecordFiles.FolderOf(_home));
        File.WriteAllText(NodeRecordFiles.PathOf(_home, Unit), json);
        Assert.Null(Files().Read(Unit));
        Assert.Single(Files().All().Unreadable);
    }
}
