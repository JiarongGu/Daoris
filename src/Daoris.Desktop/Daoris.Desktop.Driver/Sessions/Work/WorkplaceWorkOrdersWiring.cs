using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkplaceWorkOrdersOperations : IWorkplaceWorkOrders
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceWorkOrders.Current = new WorkplaceWorkOrdersOperations();

    public int ForgetAbandoned(string home, Func<AbandonEntry, bool> known) => new AbandonRecord(home).Forget(known);

    public IReadOnlySet<string> Owing(string home) => WorkObligations.Owing(home);

    public IReadOnlyList<IKnowledgeMilestone> Derive(InventoryFacts facts) => WorkInventory.Derive(facts);

    public QuestSubmissionView? CommitWanted(QuestView? quest, EvidenceAt at) => EvidenceCheck.CommitWanted(quest, at);

    public Task<string?> AtEndAsync(ServiceClient service, QuestView? quest, EvidenceAt at, string? commits, WorkingTree.GitRead git, CancellationToken ct) =>
        EvidenceCheck.AtEndAsync(service, quest, at, commits, git, ct);
}
