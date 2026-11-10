using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Design §5.5 on the stand-in system's own process, installed in a home: its <c>stand-in-unguarded</c> system declares the
/// precondition <c>none</c>, so the <c>apply</c>'s door reads each key before its write and reads it back after. A key changed in
/// the system after the plan read it fails the node stale with nothing written; a key another writer changes between the write and
/// the read-back fails it changed while applying, each with the key's present value on the node's words.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SystemReadBackProcessTests : IDisposable
{
    private const string Unit = "run:r1";
    private static readonly NodeDeclaration Apply = new(NodeKinds.Apply, NodeKinds.Apply, 1, NodeReach.Performed);
    private static readonly SystemPlace Place = new("stand-in-system", "stand-in-unguarded");

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-system-readback-proc-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _said = [];

    public SystemReadBackProcessTests()
    {
        var source = Path.Combine(WorkspaceRoot.Folder, "examples", "plugins", "stand-in-system");
        var installed = DaorisHome.Entry(_home, HomeEntry.Plugins, Place.Plugin);
        Directory.CreateDirectory(installed);
        foreach (var file in new[] { "plugin.json", "hooks.mjs", "system.mjs" }) File.Copy(Path.Combine(source, file), Path.Combine(installed, file));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string State => Path.Combine(Assert.Single(PluginCatalog.Load(_home).Plugins).Data, "state.json");

    private void Holds(string value, string etag, int serial)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(State)!);
        File.WriteAllText(State, $$"""{ "items": { "release": { "value": "{{value}}", "etag": "{{etag}}" } }, "serial": {{serial}} }""");
    }

    private string? Held() => JsonDocument.Parse(File.ReadAllText(State)).RootElement.GetProperty("items").GetProperty("release").GetProperty("value").GetString();

    /// <summary>The stand-in's channel, with another writer run once its <c>apply/perform</c> has answered: between the write and the read-back.</summary>
    private sealed class Raced(IHookChannel inner, Action between) : IHookChannel
    {
        public IReadOnlyList<string> Points => inner.Points;

        public bool Alive => inner.Alive;

        public Task<HookDecision> ConsiderAsync(object payload, CancellationToken ct) => inner.ConsiderAsync(payload, ct);

        public Task EndedAsync(object payload, CancellationToken ct) => inner.EndedAsync(payload, ct);

        public async Task<JsonElement> SystemAsync(string point, object payload, CancellationToken ct)
        {
            var answer = await inner.SystemAsync(point, payload, ct);
            if (point == HookPoints.ApplyPerform) between();
            return answer;
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private static WorkUnit Run() =>
        new(Unit, WorkUnitKind.Run, new WorkOwner("console", "default", null, WorkRecord.DriverWriter), new WorkSubject("r1", null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []) { Executors = ["rec-1"] };

    private async Task<NodeStatus> ApplyAsync(int attempt, Action? between = null)
    {
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(Unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                [Apply.Id] = new(Unit, Apply.Id, NodeStates.Active) { Attempt = attempt, Tries = attempt, Binding = new NodeAcceptance(Unit, null, "graph-1", Apply.Id, 1, attempt) },
            },
        });
        var starter = SystemAdapters.Starter(_home);
        Func<SystemPlace, CancellationToken, Task<IHookChannel>> adapters = between is null
            ? starter
            : async (place, ct) => new Raced(await starter(place, ct), between);
        await new ApplyDoor(_home, adapters, _said.Add, holdsLease: () => true)
            .PerformAsync(new ApplyStart(Run(), Apply, attempt) { Authority = StepAuthority.OfDoor(Unit, Apply.Id, attempt) }, CancellationToken.None);
        return new NodeRecordFiles(_home, () => true).Read(Unit)!.Statuses[Apply.Id];
    }

    private async Task<SystemChangeSet> PlanAsync(string value)
    {
        await using var channel = await SystemAdapters.Starter(_home)(Place, CancellationToken.None);
        return await new SystemAdapter(channel, Place, new SystemPlans(_home), () => DateTimeOffset.UtcNow)
            .PlanAsync("rec-1", [new SystemIntent("release", value)], CancellationToken.None);
    }

    [Fact]
    public async Task An_unguarded_system_is_read_before_each_write_and_read_back_after_it()
    {
        Holds("1", "e1", 1);
        var first = await PlanAsync("2");
        Assert.Equal(("none", "e1"), (first.Operations[0].Precondition.Kind, first.Operations[0].Precondition.Version));

        // Someone changes the key in the system's console after the plan read it: stale before the write, and nothing written.
        Holds("by hand", "e-hand", 5);
        var stale = await ApplyAsync(1);
        Assert.Equal((NodeStates.Failed, ApplyDoor.Stale), (stale.State, stale.Code));
        Assert.Contains("- `release` is now `by hand` at version `e-hand`", stale.Words);
        Assert.Equal("by hand", Held());

        // Planned again; another writer changes the key between the apply's write and its read-back.
        await PlanAsync("3");
        var changed = await ApplyAsync(2, () => Holds("by another", "e-other", 20));
        Assert.Equal((NodeStates.Failed, ApplyDoor.ChangedWhileApplying), (changed.State, changed.Code));
        Assert.Contains("changed while applying", changed.Words);
        Assert.Contains("- `release` is now `by another` at version `e-other`", changed.Words);
        Assert.Equal(SystemApplyStates.Changed, new SystemPlans(_home).AppliedOf("rec-1", 2)!.State);

        // Planned once more from the system as it stands, and applied: written, read back as written.
        await PlanAsync("4");
        var reached = await ApplyAsync(3);
        Assert.Equal(NodeStates.Reached, reached.State);
        Assert.Equal("4", Held());
    }
}
