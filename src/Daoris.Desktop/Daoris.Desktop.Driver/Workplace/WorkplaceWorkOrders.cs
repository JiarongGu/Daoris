namespace Daoris.Driver;

/// <summary>
/// What the workplace asks of the work: the abandons' record a history clear tidies, what a unit still owes beyond its branch records,
/// the inventory read over the records the workplace gathers, and a done's evidence read where a session's tree ends. Work orders
/// keeps each and implements this, installed when the driver loads (<c>Sessions/Work/WorkplaceWorkOrdersWiring.cs</c>); each
/// operation does exactly what its caller did before it was a port.
/// </summary>
internal interface IWorkplaceWorkOrders
{
    /// <summary>Drop the abandon entries <paramref name="known"/> no longer holds (<see cref="AbandonEntry"/>); how many went comes back.</summary>
    int ForgetAbandoned(string home, Func<AbandonEntry, bool> known);

    /// <summary>The units that still owe anything beyond their branch records, by id: what the traces' pruning never drops.</summary>
    IReadOnlySet<string> Owing(string home);

    /// <summary>Every unit the facts name, read as the inventory derives it: its milestone, with the entries it was read from.</summary>
    IReadOnlyList<IKnowledgeMilestone> Derive(InventoryFacts facts);

    /// <summary>The submission whose commit a session's end posts alone, or null (see <c>EvidenceCheck.CommitWanted</c>).</summary>
    QuestSubmissionView? CommitWanted(QuestView? quest, EvidenceAt at);

    /// <summary>A done's evidence read and posted where the end <paramref name="at"/> says, beneath <paramref name="commits"/> (see <c>EvidenceCheck.AtEndAsync</c>).</summary>
    Task<string?> AtEndAsync(ServiceClient service, QuestView? quest, EvidenceAt at, string? commits, WorkingTree.GitRead git, CancellationToken ct);
}

internal static class WorkplaceWorkOrders
{
    private static IWorkplaceWorkOrders? _current;

    internal static IWorkplaceWorkOrders Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace work-orders operations are installed: Work orders installs them when the driver loads");
        set => _current = value;
    }
}
