using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The connector's <c>system_plan</c> on the stand-in system's own process, installed in a home as a person installs it: a session in a
/// system department plans through the loop's half of the tool and the plan the review shows is kept under its record; two sessions
/// planning against the one system at once each keep their own plan, the applies run one at a time, and the second, whose key the first
/// moved, is refused as stale with the present value on its node's words. An apply the system answers with nothing to change settles
/// the node without a change.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SystemPlanToolProcessTests : IDisposable
{
    private static readonly NodeDeclaration Apply = new(NodeKinds.Apply, NodeKinds.Apply, 1, NodeReach.Performed);
    private static readonly SystemPlace Place = new("stand-in-system", "stand-in");
    private static readonly RepoView Store = new("store", false, null) { Kind = PlaceKind.System, System = Place };

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-system-tool-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly List<string> _said = [];

    public SystemPlanToolProcessTests()
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

    private void Holds(string value, string etag)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(State)!);
        File.WriteAllText(State, $$"""{ "items": { "release": { "value": "{{value}}", "etag": "{{etag}}" } }, "serial": 9 }""");
    }

    private string? Held() => JsonDocument.Parse(File.ReadAllText(State)).RootElement.GetProperty("items").GetProperty("release").GetProperty("value").GetString();

    private Task<SystemPlanAnswer> PlanAsync(string record, string value) =>
        new SystemPlanning(_home, () => null).PlanAsync(record, Store, [new SystemIntent("release", value)], CancellationToken.None);

    private static WorkUnit Run(string unit, string record) =>
        new(unit, WorkUnitKind.Run, new WorkOwner("console", "default", null, WorkRecord.DriverWriter), new WorkSubject(unit, null),
            new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "running"), []) { Executors = [record] };

    private async Task<NodeStatus> ApplyAsync(ApplyDoor door, string unit, string record)
    {
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                [Apply.Id] = new(unit, Apply.Id, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(unit, null, "graph-1", Apply.Id, 1, 1) },
            },
        });
        await door.PerformAsync(new ApplyStart(Run(unit, record), Apply, 1) { Authority = StepAuthority.OfDoor(unit, Apply.Id, 1) }, CancellationToken.None);
        return new NodeRecordFiles(_home, () => true).Read(unit)!.Statuses[Apply.Id];
    }

    [Fact]
    public async Task A_session_s_plan_through_the_tool_is_kept_under_its_record_and_named_with_its_resources()
    {
        Holds("1", "e1");

        var answer = await PlanAsync("rec-1", "2");

        Assert.True(answer.Done, answer.Said);
        Assert.StartsWith("Plan 1 is kept for `store` (system `stand-in-system/stand-in`): update `release`.", answer.Said, StringComparison.Ordinal);
        var review = Assert.IsType<TreeDiff>(SystemChanges.Of(_home, "rec-1"));
        Assert.Equal("release modified", $"{review.Files[0].Path} {review.Files[0].Status}");
        Assert.StartsWith("Plan 2 is kept", (await PlanAsync("rec-1", "3")).Said, StringComparison.Ordinal);
        Assert.Equal("1", Held());
    }

    [Fact]
    public async Task Two_sessions_planning_at_once_keep_their_own_plans_and_the_second_apply_is_refused_as_stale()
    {
        Holds("1", "e1");

        var planned = await Task.WhenAll(PlanAsync("rec-1", "2"), PlanAsync("rec-2", "3"));

        Assert.All(planned, answer => Assert.StartsWith("Plan 1 is kept", answer.Said, StringComparison.Ordinal));
        var (first, second) = (new SystemPlans(_home).Latest("rec-1")!, new SystemPlans(_home).Latest("rec-2")!);
        Assert.Equal(("2", "e1"), (first.Operations[0].After, first.Operations[0].Precondition.Version));
        Assert.Equal(("3", "e1"), (second.Operations[0].After, second.Operations[0].Precondition.Version));

        var door = new ApplyDoor(_home, SystemAdapters.Starter(_home), _said.Add, holdsLease: () => true);
        var applies = await Task.WhenAll(ApplyAsync(door, "run:a", "rec-1"), ApplyAsync(door, "run:b", "rec-2"));

        var reached = Assert.Single(applies, status => status.State == NodeStates.Reached);
        var stale = Assert.Single(applies, status => status.State == NodeStates.Failed);
        Assert.Equal(ApplyDoor.Stale, stale.Code);
        Assert.Contains($"- `release` is now `{Held()}`", stale.Words);
        Assert.Equal(reached.Unit == "run:a" ? "2" : "3", Held());
    }

    [Fact]
    public async Task An_apply_the_system_answers_with_nothing_to_change_settles_without_a_change()
    {
        Holds("1", "e1");
        await PlanAsync("rec-1", "2");
        // The value the plan asks for is already there by another hand, under the version the plan read.
        File.WriteAllText(State, """{ "items": { "release": { "value": "2", "etag": "e1" } }, "serial": 9 }""");

        var status = await ApplyAsync(new ApplyDoor(_home, SystemAdapters.Starter(_home), _said.Add, holdsLease: () => true), "run:a", "rec-1");

        Assert.Equal(NodeStates.Reached, status.State);
        Assert.Equal(SystemApplyStates.None, new SystemPlans(_home).AppliedOf("rec-1", 1)!.State);
        Assert.Contains(_said, line => line.Contains("nothing to change", StringComparison.Ordinal));
        Assert.Equal("""{ "items": { "release": { "value": "2", "etag": "e1" } }, "serial": 9 }""", File.ReadAllText(State));
    }
}
