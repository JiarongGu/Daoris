using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's <c>apply</c> through its door against a system held in memory with an etag per key: the work's plan applied and the node
/// reached; a plan read before someone changed the system refused as stale, the node failed with words naming the present versions,
/// and the next plan, read from the system as it stands, applied on the next attempt.
/// </summary>
public sealed class ApplyDoorTests : IDisposable
{
    private const string Unit = "run:r1";
    private static readonly NodeDeclaration Apply = new(NodeKinds.Apply, NodeKinds.Apply, 1, NodeReach.Performed);
    private static readonly SystemPlace Place = new("acme.console", "store");

    private readonly string _scratch = Path.Combine(TestHomes.Root, "daoris-apply-door-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _said = [];
    private readonly Store _system = new();

    public ApplyDoorTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The system: keys with a value and an etag, typing a plan and applying it under each etag, every precondition checked first.</summary>
    private sealed class Store : IHookChannel
    {
        private int _serial;

        public Dictionary<string, (string Value, string Etag)> Items { get; } = [];

        public void Set(string key, string value) => Items[key] = (value, $"e{++_serial}");

        public IReadOnlyList<string> Points => HookPoints.System;

        public bool Alive => true;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => throw new NotSupportedException();

        public Task EndedAsync(object payload, CancellationToken ct) => throw new NotSupportedException();

        public Task<JsonElement> SystemAsync(string point, object payload, CancellationToken ct)
        {
            var asked = JsonSerializer.SerializeToElement(payload);
            object answer;
            if (point == HookPoints.PlanRead)
            {
                answer = new
                {
                    operations = asked.GetProperty("changes").EnumerateArray().Select(change =>
                    {
                        var key = change.GetProperty("resource").GetString()!;
                        var held = Items.TryGetValue(key, out var present);
                        return new
                        {
                            resource = key, op = held ? "update" : "create", before = held ? present.Value : null, after = change.GetProperty("value").GetString(),
                            precondition = new { kind = "etag", version = held ? present.Etag : null },
                        };
                    }).ToList(),
                };
            }
            else
            {
                var operations = asked.GetProperty("operations").EnumerateArray().ToList();
                var stale = operations.Where(operation =>
                {
                    var key = operation.GetProperty("resource").GetString()!;
                    var version = operation.GetProperty("precondition").GetProperty("version").GetString();
                    return Items.TryGetValue(key, out var present) ? present.Etag != version : version is not null;
                }).Select(operation => operation.GetProperty("resource").GetString()!).ToList();
                if (stale.Count > 0)
                {
                    answer = new
                    {
                        state = "stale", applied = Array.Empty<string>(), refused = stale[0],
                        present = stale.Select(key => new { resource = key, exists = true, value = Items[key].Value, version = Items[key].Etag }).ToList(),
                    };
                }
                else
                {
                    foreach (var operation in operations) Set(operation.GetProperty("resource").GetString()!, operation.GetProperty("after").GetString()!);
                    answer = new { state = "applied", applied = operations.Select(operation => operation.GetProperty("resource").GetString()).ToList() };
                }
            }

            return Task.FromResult(JsonSerializer.SerializeToElement(answer));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static WorkUnit Run() =>
        new(Unit, WorkUnitKind.Run, new WorkOwner("console", "default", null, WorkRecord.DriverWriter), new WorkSubject("r1", null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []) { Executors = ["rec-1"] };

    private void Active(int attempt) =>
        new NodeRecordFiles(_scratch, () => true).Write(new NodeRecord(Unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                [Apply.Id] = new(Unit, Apply.Id, NodeStates.Active) { Attempt = attempt, Tries = attempt, Binding = new NodeAcceptance(Unit, null, "graph-1", Apply.Id, 1, attempt) },
            },
        });

    private NodeStatus Status() => new NodeRecordFiles(_scratch, () => true).Read(Unit)!.Statuses[Apply.Id];

    private ApplyDoor Door() => new(_scratch, (_, _) => Task.FromResult<IHookChannel>(_system), _said.Add, holdsLease: () => true);

    private Task<SystemChangeSet> PlanAsync() =>
        new SystemAdapter(_system, Place, new SystemPlans(_scratch), () => DateTimeOffset.UtcNow).PlanAsync("rec-1", [new SystemIntent("release", "2")], CancellationToken.None);

    private static ApplyStart Start(int attempt) => new(Run(), Apply, attempt) { Authority = StepAuthority.OfDoor(Unit, Apply.Id, attempt) };

    [Fact]
    public async Task The_reviewed_plan_is_applied_under_its_etags_and_the_node_reached()
    {
        _system.Set("release", "1");
        await PlanAsync();
        Active(1);

        await Door().PerformAsync(Start(1), CancellationToken.None);

        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Equal("2", _system.Items["release"].Value);
        Assert.Contains(_said, line => line.Contains("plan 1 applied to `acme.console/store`", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_plan_read_before_the_system_moved_fails_stale_with_the_present_versions_and_the_next_plan_applies()
    {
        _system.Set("release", "1");
        await PlanAsync();
        _system.Set("release", "by hand");
        Active(1);

        await Door().PerformAsync(Start(1), CancellationToken.None);

        var failed = Status();
        Assert.Equal((NodeStates.Failed, ApplyDoor.Stale), (failed.State, failed.Code));
        Assert.Contains("- `release` is now `by hand` at version `e2`", failed.Words);
        Assert.Equal("by hand", _system.Items["release"].Value);

        // The way back reopens the work, which plans again from the system as it stands; the next attempt applies that plan.
        var second = await PlanAsync();
        Assert.Equal((2, "e2"), (second.Plan, second.Operations[0].Precondition.Version));
        Active(2);

        await Door().PerformAsync(Start(2), CancellationToken.None);

        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Equal("2", _system.Items["release"].Value);
    }

    [Fact]
    public async Task A_run_whose_work_planned_nothing_is_said_and_left_active()
    {
        Active(1);

        await Door().PerformAsync(Start(1), CancellationToken.None);

        Assert.Equal(NodeStates.Active, Status().State);
        Assert.Contains(_said, line => line.Contains("has no change set to apply", StringComparison.Ordinal));
    }
}
