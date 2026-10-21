using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PERFORM1d (the fabric events design §D3): what a plugin's attempt created, taken as receipts on its unit's resource record before
/// its outcome, once however often the completion comes; cleaned only by its plugin through <c>work/cleanup</c>, the request persisted with
/// its key before it is sent, so a crash between the plugin's removal and the recording of its answer converges on <c>already-gone</c>
/// (canary 34); a failure tried again on its back-off and read as blocked after seven days; a superseded attempt's receipts temporary and
/// cleaned at once (canary 36); nothing cleaned before its unit's end, a result kept, an undeclared schema never cleaned by itself.
/// </summary>
public sealed class ResourceCleanupTests : IDisposable
{
    private const string Unit = "unit-a";
    private const string Plugin = "acme.deploy";
    private const string Tip = "0123456789abcdef0123456789abcdef01234567";
    private const string Preview = "acme.deploy/preview@1";
    private const string Site = "acme.deploy/site@1";

    private static readonly NodeDeclaration Deploy = new("deploy", Plugin + "/deploy", 1, NodeReach.Performed)
    {
        Perform = new NodePerform(Idempotent: true, Reconcile: true, Transient: []),
    };

    private static readonly IReadOnlyList<PluginResource> Declared =
        [new(Preview, PluginResourceRoles.Temporary, PluginSteps.CleanupPoint), new(Site, PluginResourceRoles.Result, PluginSteps.CleanupPoint)];

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-resources-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly Port _port = new();

    private DateTimeOffset _now = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    /// <summary>A plugin's work process: what it sends back, and a cleanup journal of the keys it removed by, as a durable plugin keeps one.</summary>
    private sealed class Port : IWorkPerformPort
    {
        public List<WorkPerformCall> Sent { get; } = [];

        public List<WorkCleanupCall> Cleaned { get; } = [];

        /// <summary>The keys whose resource the plugin removed: a second delivery of one is answered already gone.</summary>
        public HashSet<string> Removed { get; } = new(StringComparer.Ordinal);

        /// <summary>What a cleanup does after the plugin acted: a test loses the answer, or ends the loop, here.</summary>
        public Func<WorkCleanupCall, WorkCleanupAnswer?>? OnCleanup { get; set; }

        public Func<WorkReconcileCall, WorkReconcileAnswer> Answer { get; set; } = _ => new(ReconcileStates.Unknown);

        public Task<int> PerformAsync(string plugin, WorkPerformCall call, CancellationToken ct)
        {
            Sent.Add(call);
            return Task.FromResult(1);
        }

        public Task<(WorkReconcileAnswer Answer, int Generation)> ReconcileStepAsync(string plugin, WorkReconcileCall call, CancellationToken ct) =>
            Task.FromResult((Answer(call), 1));

        public Task<(WorkCleanupAnswer Answer, int Generation)> CleanupAsync(string plugin, WorkCleanupCall call, CancellationToken ct)
        {
            Assert.Equal(Plugin, plugin);
            Cleaned.Add(call);
            var answer = OnCleanup?.Invoke(call);
            if (answer is null)
            {
                answer = Removed.Add(call.IdempotencyKey) ? new WorkCleanupAnswer(CleanupDispositions.Removed, "deleted:" + call.Resource)
                    : new WorkCleanupAnswer(CleanupDispositions.AlreadyGone);
            }

            return Task.FromResult((answer, 1));
        }

        public int? GenerationOf(string plugin) => 1;
    }

    private NodeRecordFiles Nodes => new(_home, () => true);

    private ResourceRecordFiles Resources => new(_home, () => true);

    private WorkPerformer Performer() => new(Nodes, _port, () => _now) { Receipts = Resources, Resources = plugin => plugin == Plugin ? Declared : null };

    private ResourceCleaner Cleaner() => new(Resources, _port, () => _now);

    private static WatchedUnit? Watched(string unit) => unit == Unit ? new WatchedUnit("engine", "default", "checkout", "", Tip, null, null, []) : null;

    private static NodeDeclaration? Of(string unit, string node) => unit == Unit && node == Deploy.Id ? Deploy : null;

    private IReadOnlyList<ResourceReceipt> Receipts() => Resources.Read(Unit)!.Receipts;

    private async Task<string> StartAsync(WorkPerformer performer)
    {
        var record = Nodes.Read(Unit)!;
        var before = new Dictionary<string, NodeStatus>(StringComparer.Ordinal) { [Deploy.Id] = record.Statuses.GetValueOrDefault(Deploy.Id) ?? NodeLifecycle.Start(Unit, Deploy.Id) };
        var evaluated = WireEvaluation.Evaluate([Deploy], [], before, new WireRun("graph-1", "run-1") { Revisions = new Dictionary<string, string?> { [Unit] = Tip } });
        performer.Keep(before, evaluated);
        await performer.StartAsync(Deploy, Assert.Single(evaluated.Starts), CancellationToken.None);
        return _port.Sent[^1].Invocation;
    }

    private WorkStepHeard Heard(string invocation, WorkOutcome outcome, string id = "e1") =>
        new(Plugin, 1, new WorkStepFrame("deploy", outcome, _now, id, Invocation: invocation));

    private static WorkOutcome Reached(params WorkResource[] resources) => new(WorkStates.Reached) { Resources = resources };

    private void Stop()
    {
        var record = Nodes.Read(Unit)!;
        var moved = NodeLifecycle.Apply(Deploy, record.Statuses[Deploy.Id], new NodeMove(NodeStates.Stopped, NodeCause.Person));
        Assert.True(moved.Moved, moved.Refused);
        Nodes.Write(record with { Statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal) { [Deploy.Id] = moved.Status } });
    }

    [Fact]
    public async Task A_completions_resources_are_taken_as_receipts_with_their_declared_roles_once_however_often_it_comes()
    {
        var performer = Performer();
        var invocation = await StartAsync(performer);
        var outcome = Reached(new WorkResource(Preview, "p-1"), new(Site, "s-1"), new("acme.deploy/log@1", "l-1"));

        var first = performer.Hear([Heard(invocation, outcome)], Of, Watched);
        var again = performer.Hear([Heard(invocation, outcome, id: "e2")], Of, Watched);

        Assert.Equal(PerformedWhat.Applied, Assert.Single(first).What);
        Assert.Equal(PerformedWhat.Replay, Assert.Single(again).What);
        var receipts = Receipts();
        Assert.Equal(
            [(Preview, "p-1", PluginResourceRoles.Temporary), (Site, "s-1", PluginResourceRoles.Result), ("acme.deploy/log@1", "l-1", ResourceReceipt.Undeclared)],
            receipts.Select(receipt => (receipt.Schema, receipt.Resource, receipt.Role)));
        Assert.All(receipts, receipt => Assert.Equal((Plugin, Deploy.Id, 1, invocation, false), (receipt.Plugin, receipt.Node, receipt.Attempt, receipt.Invocation, receipt.Superseded)));
        Assert.Equal(NodeStates.Reached, Nodes.Read(Unit)!.Statuses[Deploy.Id].State);
    }

    /// <summary>A completion whose receipts cannot be written is not applied: what it made is never left unaccounted for behind its outcome.</summary>
    [Fact]
    public async Task A_completion_whose_receipts_cannot_be_kept_is_not_applied()
    {
        var performer = Performer();
        var invocation = await StartAsync(performer);
        Directory.CreateDirectory(ResourceRecordFiles.FolderOf(_home));
        File.WriteAllText(ResourceRecordFiles.PathOf(_home, Unit), """{"unit":"unit-a"}""");

        var heard = performer.Hear([Heard(invocation, Reached(new WorkResource(Preview, "p-1")))], Of, Watched);

        Assert.Equal((PerformedWhat.NotTaken, ResourceRecordFiles.Unreadable), (Assert.Single(heard).What, heard[0].Code));
        Assert.Equal(NodeStates.Active, Nodes.Read(Unit)!.Statuses[Deploy.Id].State);
    }

    /// <summary>§D3's bound: a completion naming more than the record keeps takes none and is held for the person.</summary>
    [Fact]
    public async Task A_completion_past_the_receipts_bound_is_held_for_the_person()
    {
        var performer = Performer();
        var invocation = await StartAsync(performer);
        Resources.Write(new ResourceRecord(Unit)
        {
            Receipts = [.. Enumerable.Range(0, ResourceRecord.ReceiptsKept).Select(at =>
                new ResourceReceipt($"r-{at}", Plugin, Preview, $"old-{at}", PluginResourceRoles.Temporary, "deploy", 1, "i-old", _now))],
        });

        var heard = performer.Hear([Heard(invocation, Reached(new WorkResource(Preview, "p-new")))], Of, Watched);

        Assert.Equal((PerformedWhat.Held, WorkPerformer.TooManyResources), (Assert.Single(heard).What, heard[0].Code));
        Assert.Equal(ResourceRecord.ReceiptsKept, Receipts().Count);
        Assert.Equal(AttemptStates.Held, Nodes.Read(Unit)!.Invoked(invocation)!.State);
    }

    /// <summary>§D6's <c>resources?</c>: a reconciliation's answer gives its receipts as a completion does.</summary>
    [Fact]
    public async Task A_reconciliations_resources_are_taken_as_receipts()
    {
        var performer = Performer();
        var invocation = await StartAsync(performer);
        _now += WorkPerformer.DefaultTimeout + TimeSpan.FromMinutes(1);
        _port.Answer = _ => new(ReconcileStates.Done, Reached(new WorkResource(Site, "s-1"))) { Resources = [new(Preview, "p-1")] };

        var reconciled = await Performer().ReconcileAsync(Of, CancellationToken.None);

        Assert.Equal(PerformedWhat.Applied, Assert.Single(reconciled).What);
        Assert.Equal([("p-1", false), ("s-1", false)], Receipts().Select(receipt => (receipt.Resource, receipt.Superseded)).Order());
        Assert.Equal(invocation, Receipts()[0].Invocation);
    }

    /// <summary>Canary 36: a superseded attempt's completion is recorded as a stale one's, and what it made is temporary and cleaned at once.</summary>
    [Fact]
    public async Task A_superseded_attempts_receipts_are_temporary_and_cleaned_at_once_whatever_their_declared_role()
    {
        var performer = Performer();
        var invocation = await StartAsync(performer);
        Stop();

        var heard = performer.Hear([Heard(invocation, Reached(new WorkResource(Site, "s-1")))], Of, Watched);

        Assert.Equal(PerformedWhat.Stale, Assert.Single(heard).What);
        Assert.Equal(NodeStates.Stopped, Nodes.Read(Unit)!.Statuses[Deploy.Id].State);
        var receipt = Assert.Single(Receipts());
        Assert.Equal((PluginResourceRoles.Result, true, PluginResourceRoles.Temporary), (receipt.Role, receipt.Superseded, receipt.Effective));

        // Its unit's end is not read: a superseded attempt's is asked all the same.
        var cleaned = await Cleaner().CleanAsync(_ => null, WorkSends.Unbounded, CancellationToken.None);

        Assert.Equal((CleanedWhat.Cleaned, CleanupDispositions.Removed), (Assert.Single(cleaned).What, cleaned[0].Code));
        Assert.Equal(("s-1", receipt.Id), (Assert.Single(_port.Cleaned).Resource, _port.Cleaned[0].Receipt));
        var settled = Assert.Single(Receipts()).Cleanup!;
        Assert.Equal((CleanupStates.Settled, CleanupDispositions.Removed, "deleted:s-1"), (settled.State, settled.Disposition, settled.Proof));
    }

    /// <summary>
    /// 🔴 Canary 34: the plugin removes the resource and the answer is lost (the loop ends, or the call fails): the request was persisted with
    /// its key before the send, so a later pass, and a loop started again, send the same key, which is answered already gone and ends it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_crash_between_the_plugins_removal_and_its_recorded_answer_converges_on_the_same_key(bool loopEnds)
    {
        Resources.Write(new ResourceRecord(Unit) { Receipts = [new("r-1", Plugin, Preview, "p-1", PluginResourceRoles.Temporary, "deploy", 1, "i-1", _now)] });
        _port.OnCleanup = call =>
        {
            _port.Removed.Add(call.IdempotencyKey);
            throw loopEnds ? new OperationCanceledException() : new IOException("the answer was lost");
        };

        if (loopEnds) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None));
        else Assert.Equal(CleanedWhat.Again, Assert.Single(await Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None)).What);
        var requested = Assert.Single(Receipts()).Cleanup!;
        Assert.Equal((CleanupStates.Requested, loopEnds ? 0 : 1), (requested.State, requested.Tries));

        _port.OnCleanup = null;
        _now += TimeSpan.FromMinutes(1);
        var again = await Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None);

        Assert.Equal((CleanedWhat.Cleaned, CleanupDispositions.AlreadyGone), (Assert.Single(again).What, again[0].Code));
        Assert.Equal([requested.Key, requested.Key], _port.Cleaned.Select(call => call.IdempotencyKey));
        Assert.Single(_port.Removed);
        Assert.Equal(CleanupStates.Settled, Assert.Single(Receipts()).Cleanup!.State);
        Assert.Empty(await Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None));
    }

    /// <summary>§D3: a failure is asked again after 1, 5, 15 and 60 minutes and then daily; past seven days it reads blocked with the plugin's words, and is still asked.</summary>
    [Fact]
    public async Task A_failed_cleanup_backs_off_and_reads_as_blocked_after_seven_days_never_disappearing()
    {
        Resources.Write(new ResourceRecord(Unit) { Receipts = [new("r-1", Plugin, Preview, "p-1", PluginResourceRoles.Temporary, "deploy", 1, "i-1", _now)] });
        _port.OnCleanup = _ => new WorkCleanupAnswer(CleanupDispositions.Failed, Message: "the environment is locked");
        var started = _now;
        var waits = new List<TimeSpan>();
        for (var at = 0; at < 6; at++)
        {
            Assert.Equal(CleanedWhat.Again, Assert.Single(await Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None)).What);
            var next = Assert.Single(Receipts()).Cleanup!.NextAt!.Value;
            waits.Add(next - _now);
            Assert.Empty(await Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None));
            _now = next;
        }

        Assert.Equal([1, 5, 15, 60, 1440, 1440], waits.Select(wait => wait.TotalMinutes));
        var entry = Assert.Single(WorkObligations.Of([], [Resources.Read(Unit)!], _now)[Unit]);
        Assert.Equal(InventoryDisposition.Pending, entry.Disposition);

        _now = started + ResourceCleaner.BlockedAfter;
        var blocked = Assert.Single(WorkObligations.Of([], [Resources.Read(Unit)!], _now)[Unit]);
        Assert.Equal((InventoryDisposition.Blocked, InventoryCode.CleanupBlocked), (blocked.Disposition, blocked.Code));
        Assert.Contains("the environment is locked", blocked.Detail, StringComparison.Ordinal);
        Assert.Equal(CleanedWhat.Again, Assert.Single(await Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None)).What);
        Assert.Equal(7, _port.Cleaned.Count);
        Assert.Single(_port.Cleaned.Select(call => call.IdempotencyKey).Distinct());
    }

    /// <summary>
    /// §D3: nothing is cleaned before its unit's end, nor where it is not read; at the end, a temporary resource is, while a result is kept, an
    /// undeclared schema is never asked and blocks, and a retention is kept with its reason.
    /// </summary>
    [Fact]
    public async Task Only_temporary_receipts_are_cleaned_and_only_at_their_units_true_end()
    {
        Resources.Write(new ResourceRecord(Unit)
        {
            Receipts =
            [
                new("r-1", Plugin, Preview, "p-1", PluginResourceRoles.Temporary, "deploy", 1, "i-1", _now),
                new("r-2", Plugin, Preview, "p-2", PluginResourceRoles.Temporary, "deploy", 1, "i-1", _now),
                new("r-3", Plugin, Site, "s-1", PluginResourceRoles.Result, "deploy", 1, "i-1", _now),
                new("r-4", Plugin, "acme.deploy/log@1", "l-1", ResourceReceipt.Undeclared, "deploy", 1, "i-1", _now),
            ],
        });
        _port.OnCleanup = call => call.Resource == "p-2" ? new WorkCleanupAnswer(CleanupDispositions.Retained, Message: "a reused environment") : null;

        Assert.Empty(await Cleaner().CleanAsync(_ => false, WorkSends.Unbounded, CancellationToken.None));
        Assert.Empty(await Cleaner().CleanAsync(_ => null, WorkSends.Unbounded, CancellationToken.None));
        Assert.Empty(Cleaner().Owed(_ => false));
        Assert.Equal([Plugin], Cleaner().Owed(_ => true));

        var cleaned = await Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None);

        Assert.Equal(["p-1", "p-2"], _port.Cleaned.Select(call => call.Resource));
        Assert.Equal([CleanupDispositions.Removed, CleanupDispositions.Retained], cleaned.Select(each => each.Code));
        var entries = WorkObligations.Of([], [Resources.Read(Unit)!], _now)[Unit].ToDictionary(entry => entry.Resource);
        Assert.Equal(InventoryDisposition.Removed, entries[$"{Preview}:p-1"].Disposition);
        Assert.Equal(InventoryDisposition.Retained, entries[$"{Preview}:p-2"].Disposition);
        Assert.Contains("a reused environment", entries[$"{Preview}:p-2"].Detail, StringComparison.Ordinal);
        Assert.Equal((InventoryRole.Result, InventoryDisposition.Kept), (entries[$"{Site}:s-1"].Role, entries[$"{Site}:s-1"].Disposition));
        Assert.Equal((InventoryDisposition.Blocked, InventoryCode.Undeclared), (entries["acme.deploy/log@1:l-1"].Disposition, entries["acme.deploy/log@1:l-1"].Code));
        Assert.Empty(Cleaner().Owed(_ => true));
    }

    /// <summary>The pass's calls bound the cleanups too (FABRIC-RUN1e): what is left is asked at the next pass.</summary>
    [Fact]
    public async Task Cleanups_take_the_passs_calls()
    {
        Resources.Write(new ResourceRecord(Unit)
        {
            Receipts = [.. Enumerable.Range(0, 3).Select(at => new ResourceReceipt($"r-{at}", Plugin, Preview, $"p-{at}", PluginResourceRoles.Temporary, "deploy", 1, "i-1", _now))],
        });

        var sends = new WorkSends(2);
        Assert.Equal(2, (await Cleaner().CleanAsync(_ => true, sends, CancellationToken.None)).Count);
        Assert.Equal(0, sends.Left);
        Assert.Single(await Cleaner().CleanAsync(_ => true, WorkSends.Unbounded, CancellationToken.None));
    }

    [Fact]
    public void A_resource_record_reads_back_as_written_and_a_broken_one_never_reads_as_empty()
    {
        var record = new ResourceRecord(Unit)
        {
            Receipts =
            [
                new("r-1", Plugin, Preview, "p-1", PluginResourceRoles.Temporary, "deploy", 2, "i-1", _now) { Superseded = true },
                new("r-2", Plugin, Site, "s-1", PluginResourceRoles.Result, "deploy", 1, "i-0", _now)
                {
                    Cleanup = new ResourceCleanup(CleanupStates.Settled, "c-1", _now, 2)
                    {
                        Disposition = CleanupDispositions.Retained, Message = "kept", SettledAt = _now.AddMinutes(5),
                    },
                },
            ],
        };

        Assert.Equal(ResourceRecordFiles.ToJson(record), ResourceRecordFiles.ToJson(ResourceRecordFiles.Parse(ResourceRecordFiles.ToJson(record))!));
        Assert.Null(ResourceRecordFiles.Parse("""{"unit":"u"}"""));
        Assert.Null(ResourceRecordFiles.Parse(ResourceRecordFiles.ToJson(record).Replace("\"r-2\"", "\"r-1\"", StringComparison.Ordinal)));
        Assert.Null(ResourceRecordFiles.Parse(ResourceRecordFiles.ToJson(record).Replace("\"retained\"", "\"failed\"", StringComparison.Ordinal)));
        Assert.Null(ResourceRecordFiles.Parse(ResourceRecordFiles.ToJson(record).Replace("\"temporary\"", "\"forever\"", StringComparison.Ordinal)));

        // Beside the node records, never among them: a build before this one lists those, and would read this as one that does not read.
        Assert.NotEqual(NodeRecordFiles.FolderOf(_home), ResourceRecordFiles.FolderOf(_home));
        Resources.Write(record);
        Assert.Empty(Nodes.All().Unreadable);
    }
}
