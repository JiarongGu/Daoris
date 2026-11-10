using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The apply of a system whose adapter declares its precondition <c>none</c> (design §5.5), over a store that checks nothing: each
/// resource read just before its write and compared with the version the plan read, a moved one stale and nothing written; each
/// written alone and read back, a read-back that differs changed while applying; a resource already holding the plan left alone.
/// </summary>
public sealed class SystemReadBackTests : IDisposable
{
    private static readonly SystemPlace Place = new("acme.console", "tickets");
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-system-readback-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly Unguarded _system = new();

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A store with a version per key and no guard: a plan leaves out what it holds; an apply writes whatever it is sent.</summary>
    private sealed class Unguarded : IHookChannel
    {
        private int _serial;

        public Dictionary<string, (string Value, string Version)> Items { get; } = [];

        public List<string> Asked { get; } = [];

        /// <summary>Another writer, run after each write the apply sends.</summary>
        public Action? AfterWrite { get; set; }

        public void Set(string key, string value) => Items[key] = (value, $"v{++_serial}");

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
                var changes = asked.GetProperty("changes").EnumerateArray().Select(change => (Key: change.GetProperty("resource").GetString()!, Value: change.GetProperty("value").GetString()!));
                Asked.Add($"read {string.Join(",", changes.Select(change => change.Key))}");
                answer = new
                {
                    operations = changes.Where(change => !(Items.TryGetValue(change.Key, out var held) && held.Value == change.Value)).Select(change =>
                    {
                        var held = Items.TryGetValue(change.Key, out var present);
                        return new
                        {
                            resource = change.Key, op = held ? "update" : "create", before = held ? present.Value : null, after = change.Value,
                            precondition = new { kind = SystemPreconditions.None, version = held ? present.Version : null },
                        };
                    }).ToList(),
                };
            }
            else
            {
                var operations = asked.GetProperty("operations").EnumerateArray().Select(operation => (Key: operation.GetProperty("resource").GetString()!, After: operation.GetProperty("after").GetString()!)).ToList();
                Asked.Add($"write {string.Join(",", operations.Select(operation => operation.Key))}");
                foreach (var operation in operations) Set(operation.Key, operation.After);
                AfterWrite?.Invoke();
                answer = new { state = "applied", applied = operations.Select(operation => operation.Key).ToList() };
            }

            return Task.FromResult(JsonSerializer.SerializeToElement(answer));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private SystemAdapter Adapter() => new(_system, Place, new SystemPlans(_home), () => Now, SystemPreconditions.None);

    private async Task<SystemChangeSet> PlanAsync(params (string Key, string Value)[] changes)
    {
        var plan = await Adapter().PlanAsync("rec-1", [.. changes.Select(change => new SystemIntent(change.Key, change.Value))], CancellationToken.None);
        _system.Asked.Clear();
        return plan;
    }

    [Fact]
    public async Task Each_resource_is_read_before_its_write_and_read_back_after_it()
    {
        _system.Set("release", "1");
        _system.Set("owner", "ops");
        var plan = await PlanAsync(("release", "2"), ("owner", "team"));

        var applied = await Adapter().ApplyAsync("rec-1", plan, CancellationToken.None);

        Assert.Equal((SystemApplyStates.Applied, "release owner"), (applied.State, string.Join(" ", applied.Applied)));
        Assert.Equal(["read release", "write release", "read release", "read owner", "write owner", "read owner"], _system.Asked);
        Assert.Equal(("2", "team"), (_system.Items["release"].Value, _system.Items["owner"].Value));
        Assert.Equal(SystemApplyStates.Applied, new SystemPlans(_home).AppliedOf("rec-1", plan.Plan)!.State);
    }

    [Fact]
    public async Task A_resource_that_moved_since_the_plan_read_it_is_stale_before_its_write_and_nothing_of_it_is_written()
    {
        _system.Set("release", "1");
        _system.Set("owner", "ops");
        var plan = await PlanAsync(("release", "2"), ("owner", "team"));
        _system.Set("owner", "by hand");

        var stale = await Adapter().ApplyAsync("rec-1", plan, CancellationToken.None);

        Assert.Equal((SystemApplyStates.Stale, "owner", "release"), (stale.State, stale.Refused, string.Join(" ", stale.Applied)));
        Assert.Equal(new SystemPresent("owner", true, "by hand", "v3"), Assert.Single(stale.Present));
        Assert.Equal(["read release", "write release", "read release", "read owner"], _system.Asked);
        Assert.Equal("by hand", _system.Items["owner"].Value);
    }

    [Fact]
    public async Task A_read_back_that_differs_from_the_write_is_changed_while_applying()
    {
        _system.Set("release", "1");
        var plan = await PlanAsync(("release", "2"));
        _system.AfterWrite = () => _system.Set("release", "by another");

        var changed = await Adapter().ApplyAsync("rec-1", plan, CancellationToken.None);

        Assert.Equal((SystemApplyStates.Changed, "release", ""), (changed.State, changed.Refused, string.Join(" ", changed.Applied)));
        Assert.Equal(new SystemPresent("release", true, "by another", "v3"), Assert.Single(changed.Present));
        Assert.Contains("changed while applying", SystemStaleWords.Of("apply", plan.Plan, changed), StringComparison.Ordinal);
        Assert.Contains("- `release` is now `by another` at version `v3`", SystemStaleWords.Of("apply", plan.Plan, changed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_resource_already_holding_the_plan_is_not_written_and_a_plan_held_whole_is_unchanged()
    {
        _system.Set("release", "1");
        var plan = await PlanAsync(("release", "2"));
        _system.Set("release", "2");

        var unchanged = await Adapter().ApplyAsync("rec-1", plan, CancellationToken.None);

        Assert.Equal((SystemApplyStates.Unchanged, 0), (unchanged.State, unchanged.Applied.Count));
        Assert.Equal(["read release"], _system.Asked);
    }

    [Fact]
    public void An_adapter_answering_unchanged_is_read_and_one_answering_none_is_not()
    {
        Assert.Equal(SystemApplyStates.Unchanged, SystemWire.ReadApplied(JsonDocument.Parse("""{ "state": "unchanged", "applied": [] }""").RootElement)!.State);
        Assert.Null(SystemWire.ReadApplied(JsonDocument.Parse("""{ "state": "none", "applied": [] }""").RootElement));
        Assert.Null(SystemWire.ReadApplied(JsonDocument.Parse("""{ "state": "changed", "applied": [] }""").RootElement));
    }
}
