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

    [Fact]
    public void A_record_is_read_back_whole_after_its_write()
    {
        var files = new NodeRecordFiles(_home);
        var written = Full();
        files.Write(written);

        var read = new NodeRecordFiles(_home).Read(Unit)!;

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
        var files = new NodeRecordFiles(_home);
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
        var files = new NodeRecordFiles(_home);
        files.Write(new NodeRecord(Unit) { Attempts = attempts });

        var kept = files.Read(Unit)!.Attempts;

        Assert.Equal(NodeRecord.SettledKept + 1, kept.Count);
        Assert.Equal("i-6", kept[0].Call.Invocation);
        Assert.Equal("i-open", kept[^1].Call.Invocation);
    }
}
