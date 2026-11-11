using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Row 1h's proof on the stand-in system's own process, installed in a home as a person installs it: a work's change set planned at
/// <c>plan/read</c> and shown on the review as each key before and after; the reviewed plan applied by the <c>apply</c>'s door at
/// <c>apply/perform</c>, reaching the node; then a plan whose key someone changed in the system before the apply, refused as stale with
/// the present version on the node's words, and the next plan, read from the system as it stands, applied.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SystemPlanProcessTests : IDisposable
{
    private const string Unit = "run:r1";
    private static readonly NodeDeclaration Apply = new(NodeKinds.Apply, NodeKinds.Apply, 1, NodeReach.Performed);
    private static readonly SystemPlace Place = new("stand-in-system", "stand-in");

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-system-proc-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _said = [];

    public SystemPlanProcessTests()
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

    private void System(string value, string etag) =>
        File.WriteAllText(State, $$"""{ "items": { "release": { "value": "{{value}}", "etag": "{{etag}}" } }, "serial": 9 }""");

    private static WorkUnit Run() =>
        new(Unit, WorkUnitKind.Run, new WorkOwner("console", "default", null, WorkRecord.DriverWriter), new WorkSubject("r1", null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []) { Executors = ["rec-1"] };

    private void Active(int attempt) =>
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(Unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                [Apply.Id] = new(Unit, Apply.Id, NodeStates.Active) { Attempt = attempt, Tries = attempt, Binding = new NodeAcceptance(Unit, null, "graph-1", Apply.Id, 1, attempt) },
            },
        });

    private NodeStatus Status() => new NodeRecordFiles(_home, () => true).Read(Unit)!.Statuses[Apply.Id];

    private async Task<SystemChangeSet> PlanAsync(string value)
    {
        await using var channel = await SystemAdapters.Starter(_home)(Place, CancellationToken.None);
        return await new SystemAdapter(channel, Place, new SystemPlans(_home), () => DateTimeOffset.UtcNow)
            .PlanAsync("rec-1", [new SystemIntent("release", value)], CancellationToken.None);
    }

    private Task ApplyAsync(int attempt) =>
        new ApplyDoor(_home, SystemAdapters.Starter(_home), _said.Add, holdsLease: () => true)
            .PerformAsync(new ApplyStart(Run(), Apply, attempt) { Authority = StepAuthority.OfDoor(Unit, Apply.Id, attempt) }, CancellationToken.None);

    private string? Held() => global::System.Text.Json.JsonDocument.Parse(File.ReadAllText(State)).RootElement.GetProperty("items").GetProperty("release").GetProperty("value").GetString();

    [Fact]
    public async Task A_plan_is_reviewed_and_applied_and_a_stale_one_goes_back_and_applies_on_its_next_plan()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(State)!);
        System("1", "e1");

        var first = await PlanAsync("2");
        Assert.Equal("release update 1 2 e1", string.Join(" ", first.Operations.Select(each => $"{each.Resource} {each.Op} {each.Before} {each.After} {each.Precondition.Version}")));
        var review = Assert.IsType<TreeDiff>(SystemChanges.Of(_home, "rec-1"));
        Assert.Equal("release modified", $"{review.Files[0].Path} {review.Files[0].Status}");

        Active(1);
        await ApplyAsync(1);
        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Equal("2", Held());

        // Someone changes the key in the system's console after the next plan read it.
        var second = await PlanAsync("3");
        System("by hand", "e-hand");
        Active(2);
        await ApplyAsync(2);
        var stale = Status();
        Assert.Equal((NodeStates.Failed, ApplyDoor.Stale), (stale.State, stale.Code));
        Assert.Contains($"refused plan {second.Plan} as stale at `release`", stale.Words);
        Assert.Contains("- `release` is now `by hand` at version `e-hand`", stale.Words);
        Assert.Equal("by hand", Held());

        var third = await PlanAsync("3");
        Assert.Equal(("by hand", "e-hand"), (third.Operations[0].Before, third.Operations[0].Precondition.Version));
        Active(3);
        await ApplyAsync(3);
        Assert.Equal(NodeStates.Reached, Status().State);
        Assert.Equal("3", Held());
    }
}
