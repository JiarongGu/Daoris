namespace Daoris.Driver;

public interface IKnowledgeSessionLook
{
    IReadOnlyList<SessionRecord> Records { get; }
    IReadOnlyDictionary<string, TreeWork> Trees { get; }
    IKnowledgeSessionLook WithTrees(IReadOnlyDictionary<string, TreeWork> trees);
    IKnowledgeSessionLook WithRecords(IReadOnlyList<SessionRecord> records);
}

public interface IKnowledgeSnapshot
{
    IReadOnlyList<QuestView> Quests { get; }
    IReadOnlyList<RepoView> Repositories { get; }
    IReadOnlyList<SessionView> Active { get; }
    IKnowledgeSnapshot Within(IReadOnlyList<QuestView> quests, IReadOnlyList<RepoView> repositories, IReadOnlyList<SessionView> active);
}

public interface IKnowledgeMilestone
{
    string Unit { get; }
    string Repository { get; }
    string? Branch { get; }
    MilestoneEnd End { get; }
    IReadOnlyList<InventoryEntry> Result { get; }
    MilestoneCleanup Cleanup { get; }
    IReadOnlyList<InventoryEntry> Recovery { get; }
    IReadOnlyList<InventoryEntry> Entries { get; }
}

internal interface IKnowledgeWorkOrders
{
    IReadOnlyList<string> TreesToJudge(IKnowledgeSessionLook look, Func<string, bool> held, IReadOnlyCollection<string>? only = null);
    string Normal(string tree);
    Task<IKnowledgeSessionLook> JudgeAsync(IKnowledgeSessionLook look, Func<string, bool> held,
        Func<string, CancellationToken, Task<TreeWork?>> judge, IReadOnlyCollection<string>? only, CancellationToken ct, int atOnce);
    IReadOnlyList<RecordedTree> RecordedOf(IEnumerable<SessionRecord> records);
    Task<HelpWork> ReadWorkAsync(string home, HelpWorkPart part, CancellationToken ct);
    bool WaitsOnYou(string id, string state, string? answer);
    HelpSessions Placed(IKnowledgeSessionLook look);
    IEnumerable<string> Milestone(IKnowledgeMilestone unit);
}

internal static class KnowledgeWorkOrders
{
    private static IKnowledgeWorkOrders? _current;

    internal static IKnowledgeWorkOrders Current
    {
        get => _current ?? throw new InvalidOperationException("no reading of work orders is installed");
        set => _current = value;
    }
}
