using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What the tests of the two hold kinds share: a hold's node, the quest and the branch it reads, its binding and the observation of it.</summary>
internal static class HoldFixture
{
    public const string Run = "run:r";
    public static readonly NodeScope Scope = new("graph-1", "run-1");

    public static NodeDeclaration Hold(string kind, string? options = null) => new(kind == NodeKinds.QuestHolds ? "holds" : "content", kind, 1, NodeReach.Observed)
    {
        Options = options is null ? new Dictionary<string, string>()
            : JsonDocument.Parse(options).RootElement.EnumerateObject().ToDictionary(option => option.Name, option => option.Value.GetRawText()),
    };

    public static QuestView Quest(string status, bool held = false, string? hold = null) =>
        new("q1", "engine", "engine", "Fix the gap", "Body.", status) { Held = held, Hold = hold };

    public static WorkUnit QuestUnit(QuestView quest) =>
        new(QuestUnits.IdOf(quest.Id), WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
            new WorkSubject($"#{quest.Id}", "q-rev"), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Records = new WorkUnitRecords(null, [], quest),
        };

    public static readonly WorkUnit Branch = new("unit-b1", WorkUnitKind.Branch, new WorkOwner("engine", "default", SessionBranches.FileName, WorkRecord.DriverWriter),
        new WorkSubject("daoris/s1", "tip-1"), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    public static NodeAcceptance Bound(WorkUnit unit, NodeDeclaration node, string? revision) =>
        new(Run, revision, Scope.GraphDigest, node.Id, node.Version, 1) { Subject = unit.Id };

    public static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, string? revision, HoldContent? content = null) =>
        NodeObserving.Observe(unit, node, Bound(unit, node, revision), new NodeGitReads { Content = (_, _) => content });
}
